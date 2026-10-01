using System.IO;

namespace MuChilaAdmin.Web.Servicos;

/// <summary>
/// Tarefas de fundo do painel (rodam enquanto ele estiver ligado):
///  - a cada 30 s: alertas de processo parado, vigia parado, mensagens novas do site, pedidos com problema, tentativas de
///    login erradas, disco quase cheio e backup do banco atrasado;
///  - a cada minuto: bans temporários vencidos;
///  - a cada 5 min: jogadores online (gráfico do dashboard);
///  - uma vez por dia: limpa alertas resolvidos antigos.
/// No ambiente de desenvolvimento (sem servidor rodando) os alertas de processo, vigia e backup ficam desligados.
/// </summary>
public sealed class MonitorPainel(Configuracao cfg, Alertas alertas, Bans bans, IAuditoria auditoria) : BackgroundService
{
    DateTime proximaMetrica = DateTime.MinValue, proximoBan = DateTime.MinValue, proximaLimpeza = DateTime.MinValue, proximoBackup = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            var agora = DateTime.Now;
            Tentar("alertas", Verificar);
            if (agora >= proximoBan) { Tentar("bans vencidos", ExpirarBans); proximoBan = agora.AddMinutes(1); }
            if (agora >= proximaMetrica) { Tentar("métricas", Metricas.Registrar); proximaMetrica = agora.AddMinutes(5); }
            if (agora >= proximaLimpeza) { Tentar("limpeza de alertas", () => alertas.Limpar()); proximaLimpeza = agora.AddDays(1); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        }
    }

    static void Tentar(string o, Action a)
    {
        try { a(); } catch (Exception ex) { Registro.Erro($"monitor ({o})", ex); }
    }

    void ExpirarBans()
    {
        foreach (var conta in bans.ExpirarVencidos())
            auditoria.Registrar(new EventoAuditoria(DateTime.Now, "sistema", null, "ban.expirou", conta, null, true, $"{conta}: o prazo do ban terminou; conta liberada."));
    }

    void Verificar()
    {
        if (!cfg.Desenvolvimento) VerificarServidor();
        VerificarSite();
        VerificarLogins();
        VerificarDisco();
        if (!cfg.Desenvolvimento && DateTime.Now >= proximoBackup) { VerificarBackup(); proximoBackup = DateTime.Now.AddHours(1); }
    }

    void VerificarServidor()
    {
        var procs = ServerControl.Processos();
        int rodando = procs.Count(p => p.Rodando);
        if (rodando == 0)
        {
            alertas.ResolverExceto("processo:", Array.Empty<string>());
            alertas.Resolver("vigia");
            alertas.Abrir("servidor:desligado", "servidor", Severidade.Aviso, "Servidor desligado",
                "Nenhum dos 8 processos do servidor está rodando. Se não foi de propósito, ligue pela tela Servidor.", "/servidor");
            return;
        }
        alertas.Resolver("servidor:desligado");
        var parados = procs.Where(p => !p.Rodando).Select(p => "processo:" + p.Nome).ToList();
        foreach (var p in procs.Where(p => !p.Rodando))
            alertas.Abrir("processo:" + p.Nome, "processo", Severidade.Critico, $"{p.Nome} parado",
                $"{p.Nome} não está rodando, mas outros processos do servidor estão. Pode ter fechado com erro: confira a janela dele e o log.", "/servidor");
        alertas.ResolverExceto("processo:", parados);

        if (ResetWatcher.IsRunning()) alertas.Resolver("vigia");
        else alertas.Abrir("vigia", "vigia", Severidade.Critico, "Vigia parado",
            "Sem o vigia, as skills evoluídas travam depois de reiniciar o GameServer (issue #12) e bônus, passe e avisos não funcionam. Ligue pela tela Servidor.", "/servidor");
    }

    void VerificarSite()
    {
        if (Db.TableExists("MUCHILA_CONTATO"))
        {
            int n = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_CONTATO WHERE status = 'nova'"));
            if (n > 0) alertas.Abrir("contato", "site", Severidade.Info, n == 1 ? "1 mensagem nova no Contate-nos" : $"{n} mensagens novas no Contate-nos",
                "Jogadores escreveram pelo site. Responda em Site → Mensagens (quem mandou logado vê a resposta no site).", "/site/mensagens");
            else alertas.Resolver("contato");
        }
        int problema = 0;
        if (Db.TableExists("MUCHILA_PEDIDOS")) problema += Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_PEDIDOS WHERE status = 'falhou'"));
        if (Db.TableExists("MUCHILA_MERCADO_PEDIDOS")) problema += Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_MERCADO_PEDIDOS WHERE status = 'falhou'"));
        if (problema > 0) alertas.Abrir("pedidos", "vendas", Severidade.Aviso, problema == 1 ? "1 compra paga com problema na entrega" : $"{problema} compras pagas com problema na entrega",
            "Pagamento confirmado, mas a entrega falhou. Veja o motivo no painel admin do site (Mu Chila → Pedidos da loja / Mercado) e use Entregar de novo.", "/vendas");
        else alertas.Resolver("pedidos");
    }

    void VerificarLogins()
    {
        if (auditoria is not AuditoriaSql sql) return;   // auditoria em memória (testes): nada a contar
        int falhas = sql.FalhasDeLogin(TimeSpan.FromMinutes(10));
        if (falhas >= 10) alertas.Abrir("login:falhas", "seguranca", Severidade.Aviso, $"{falhas} tentativas de login erradas em 10 minutos",
            "Alguém pode estar tentando adivinhar uma senha do painel. Veja quem e de qual IP em Auditoria (ação \"Login recusado\").", "/auditoria?acao=login.falha");
        else alertas.Resolver("login:falhas");
    }

    void VerificarDisco()
    {
        var raiz = Path.GetPathRoot(Path.GetFullPath(cfg.PastaServidor));
        if (string.IsNullOrEmpty(raiz) || !Directory.Exists(raiz)) return;
        var d = new DriveInfo(raiz);
        double livreGb = d.AvailableFreeSpace / 1024d / 1024 / 1024;
        if (livreGb < 5)
            alertas.Abrir("disco", "servidor", livreGb < 1 ? Severidade.Critico : Severidade.Aviso, $"Pouco espaço no disco {d.Name.TrimEnd('\\')} ({livreGb:0.0} GB livres)",
                "Com o disco cheio o banco e os logs param de gravar. Apague logs antigos (pastas LOG dos servidores) ou backups velhos.", null);
        else alertas.Resolver("disco");
    }

    void VerificarBackup()
    {
        // data do último backup completo, pelo histórico do próprio SQL Server (vale qualquer pasta de destino)
        object? ultimo;
        try { ultimo = Db.Scalar("SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name = DB_NAME() AND type = 'D'"); }
        catch { return; }   // sem acesso ao msdb: não dá para conferir
        var dias = ultimo is DateTime u ? (DateTime.Now - u).TotalDays : double.MaxValue;
        if (dias > 2)
            alertas.Abrir("backup", "banco", Severidade.Aviso, ultimo is DateTime u2 ? $"Último backup do banco foi em {u2:dd/MM HH:mm}" : "Nenhum backup do banco registrado",
                "O backup diário (tarefa \"Mu Chila - Backup do banco\", tools\\Backup-Banco.ps1) não rodou nos últimos 2 dias. Confira o Agendador de Tarefas.", null);
        else alertas.Resolver("backup");
    }
}

/// <summary>
/// Recarga dos monstros depois de mudar drops comuns, zen ou respawn: Reload Monster só nos GameServers e fora da invasão.
/// Com uma invasão no ar, agenda para quando ela acabar (o painel web fica ligado, então não se perde ao fechar a tela).
/// </summary>
public sealed class RecargaMonstros : IDisposable
{
    readonly object trava = new();
    Timer? timer;
    public DateTime? AgendadoPara { get; private set; }

    public List<string> Recarregar()
    {
        lock (trava)
        {
            var fim = ServerControl.InvasionEnd(DateTime.Now);
            if (fim == null)
            {
                timer?.Dispose(); timer = null; AgendadoPara = null;
                var r = ServerControl.ReloadMonstersNow();
                r.Add("Monstros recarregados nos GameServers. O Castle Siege (mapas 31 e 41) pega a mudança quando reiniciar.");
                return r;
            }
            if (AgendadoPara != null) return new() { $"A recarga dos monstros já está agendada para depois da invasão (até {fim:HH:mm:ss})." };
            AgendadoPara = fim;
            var espera = fim.Value - DateTime.Now + TimeSpan.FromSeconds(1);
            timer = new Timer(_ =>
            {
                lock (trava) { timer?.Dispose(); timer = null; AgendadoPara = null; }
                try { Registro.Escrever("recarga de monstros agendada: " + string.Join(" | ", Recarregar())); }
                catch (Exception ex) { Registro.Erro("recarga de monstros agendada", ex); }
            }, null, espera < TimeSpan.Zero ? TimeSpan.Zero : espera, Timeout.InfiniteTimeSpan);
            return new() { $"Invasão no ar até {fim:HH:mm:ss}: os monstros recarregam sozinhos depois dela (o painel cuida disso)." };
        }
    }

    public void Dispose() => timer?.Dispose();
}

/// <summary>Liga o vigia (processo próprio) se ele não estiver rodando.</summary>
public static class Vigia
{
    public static bool LigarSeParado()
    {
        if (ResetWatcher.IsRunning()) return false;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--vigia-reset")
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory,
        });
        return true;
    }
}

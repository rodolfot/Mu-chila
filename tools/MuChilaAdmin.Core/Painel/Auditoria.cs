using System.Data;
using System.IO;
using System.Text;

namespace MuChilaAdmin.Core.Painel;

/// <summary>Uma linha da auditoria: quem fez o quê, em quê, de onde e se deu certo.</summary>
public sealed record EventoAuditoria(
    DateTime Quando, string Usuario, string? Ip, string Acao, string? Alvo, string? Detalhes, bool Ok, string? Mensagem, long Id = 0);

public interface IAuditoria
{
    /// <summary>Grava o evento. Nunca lança exceção: se o banco falhar, vai para o arquivo auditoria-reserva.log.</summary>
    void Registrar(EventoAuditoria e);
}

/// <summary>Filtro da tela de auditoria e dos relatórios.</summary>
public sealed class FiltroAuditoria
{
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
    public string? Usuario { get; set; }
    public string? Acao { get; set; }          // prefixo: "conta." pega conta.vip, conta.ban...
    public string? Texto { get; set; }         // procura em alvo, detalhes e mensagem
    public bool? SoFalhas { get; set; }
    public int Pagina { get; set; } = 1;
    public int PorPagina { get; set; } = 50;
}

public sealed class AuditoriaSql : IAuditoria
{
    public AuditoriaSql() => Esquema.Garantir();

    public void Registrar(EventoAuditoria e)
    {
        try
        {
            Db.Execute(@"INSERT INTO dbo.MUCHILA_ADMIN_AUDITORIA (quando, usuario, ip, acao, alvo, detalhes, ok, mensagem)
                         VALUES (@q, @u, @ip, @a, @al, @d, @ok, @m)",
                ("@q", e.Quando), ("@u", Cortar(e.Usuario, 30)), ("@ip", (object?)Cortar(e.Ip, 45) ?? DBNull.Value), ("@a", Cortar(e.Acao, 60)),
                ("@al", (object?)Cortar(e.Alvo, 120) ?? DBNull.Value), ("@d", (object?)e.Detalhes ?? DBNull.Value), ("@ok", e.Ok),
                ("@m", (object?)Cortar(e.Mensagem, 2000) ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "auditoria-reserva.log"),
                    $"{e.Quando:s}\t{e.Usuario}\t{e.Ip}\t{e.Acao}\t{e.Alvo}\t{(e.Ok ? "ok" : "erro")}\t{e.Mensagem}\t{e.Detalhes}\t(banco: {ex.Message}){Environment.NewLine}", Encoding.UTF8);
            }
            catch { }
        }
    }

    static string? Cortar(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];

    static (string Where, List<(string, object?)> Pars) Montar(FiltroAuditoria f)
    {
        var w = new List<string>(); var p = new List<(string, object?)>();
        if (f.De is { } de) { w.Add("quando >= @de"); p.Add(("@de", de)); }
        if (f.Ate is { } ate) { w.Add("quando < @ate"); p.Add(("@ate", ate)); }
        if (!string.IsNullOrWhiteSpace(f.Usuario)) { w.Add("usuario = @u"); p.Add(("@u", f.Usuario.Trim())); }
        if (!string.IsNullOrWhiteSpace(f.Acao)) { w.Add("acao LIKE @a"); p.Add(("@a", Like(f.Acao.Trim()) + "%")); }
        if (!string.IsNullOrWhiteSpace(f.Texto)) { w.Add("(alvo LIKE @t OR detalhes LIKE @t OR mensagem LIKE @t)"); p.Add(("@t", "%" + Like(f.Texto.Trim()) + "%")); }
        if (f.SoFalhas == true) w.Add("ok = 0");
        return (w.Count == 0 ? "" : "WHERE " + string.Join(" AND ", w), p);
    }

    /// <summary>Escapa os curingas do LIKE (%, _ e [) digitados na busca.</summary>
    public static string Like(string s) => s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    static EventoAuditoria Ler(DataRow r) => new((DateTime)r["quando"], (string)r["usuario"], r["ip"] as string, (string)r["acao"],
        r["alvo"] as string, r["detalhes"] as string, (bool)r["ok"], r["mensagem"] as string, (long)r["id"]);

    public (List<EventoAuditoria> Itens, int Total) Buscar(FiltroAuditoria f)
    {
        var (where, pars) = Montar(f);
        int total = Convert.ToInt32(Db.Scalar($"SELECT COUNT(*) FROM dbo.MUCHILA_ADMIN_AUDITORIA {where}", pars.ToArray()));
        int pular = Math.Max(0, (f.Pagina - 1) * f.PorPagina);
        var t = Db.Query($@"SELECT id, quando, usuario, ip, acao, alvo, detalhes, ok, mensagem FROM dbo.MUCHILA_ADMIN_AUDITORIA {where}
                            ORDER BY id DESC OFFSET {pular} ROWS FETCH NEXT {Math.Clamp(f.PorPagina, 1, 5000)} ROWS ONLY", pars.ToArray());
        return (t.Rows.Cast<DataRow>().Select(Ler).ToList(), total);
    }

    public List<string> Usuarios() =>
        Db.Query("SELECT DISTINCT usuario FROM dbo.MUCHILA_ADMIN_AUDITORIA ORDER BY usuario").Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();

    public List<string> Acoes() =>
        Db.Query("SELECT DISTINCT acao FROM dbo.MUCHILA_ADMIN_AUDITORIA ORDER BY acao").Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();

    /// <summary>Logins errados nos últimos N minutos (para o alerta de tentativa e erro).</summary>
    public int FalhasDeLogin(TimeSpan janela) =>
        Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_ADMIN_AUDITORIA WHERE acao = 'login.falha' AND quando > @q", ("@q", DateTime.Now - janela)));
}

/// <summary>Nomes das ações gravadas na auditoria, com o texto mostrado na tela.</summary>
public static class Acoes
{
    static readonly Dictionary<string, string> nomes = new()
    {
        ["login.ok"] = "Entrou no painel", ["login.falha"] = "Login recusado", ["login.sair"] = "Saiu do painel",
        ["usuario.criar"] = "Criou usuário do painel", ["usuario.editar"] = "Alterou usuário do painel", ["usuario.senha"] = "Trocou a própria senha",
        ["usuario.redefinir"] = "Redefiniu senha de usuário", ["usuario.desbloquear"] = "Desbloqueou usuário",
        ["conta.vip"] = "Aplicou VIP", ["conta.vip.remover"] = "Removeu VIP", ["conta.ban"] = "Baniu conta", ["conta.desban"] = "Desbaniu conta",
        ["conta.senha"] = "Trocou senha da conta do jogo", ["conta.logout"] = "Deslogou conta", ["conta.pontos"] = "Deu/tirou pontos",
        ["conta.master"] = "Zerou árvore master", ["conta.personagem"] = "Nível/reset de personagem", ["conta.cash"] = "Deu/tirou Cash",
        ["item.colocar"] = "Colocou item", ["item.remover"] = "Removeu item", ["item.presente"] = "Cancelou presente",
        ["passe.dar"] = "Deu Passe dos Mapas", ["passe.tirar"] = "Tirou Passe dos Mapas", ["passe.cobranca"] = "Ligou/desligou a cobrança do passe",
        ["servidor.iniciar"] = "Iniciou os servidores", ["servidor.parar"] = "Parou os servidores", ["servidor.recarregar"] = "Recarregou no servidor",
        ["servidor.desconectar"] = "Desconectou jogadores", ["servidor.selecao"] = "Levou à seleção de personagem", ["servidor.abrir"] = "Abriu programa no servidor",
        ["servidor.vigia"] = "Ligou o vigia", ["servidor.publicar"] = "Publicou atualização do cliente",
        ["evento.disparar"] = "Disparou evento", ["evento.limpar"] = "Limpou disparos de evento",
        ["bonus.agendar"] = "Agendou bônus", ["bonus.cancelar"] = "Cancelou bônus", ["bonus.limpar"] = "Limpou bônus terminados",
        ["aviso.enviar"] = "Enviou aviso a todos", ["aviso.salvar"] = "Salvou avisos automáticos",
        ["loja.salvar"] = "Salvou loja de NPC", ["cashshop.salvar"] = "Salvou Loja de Cash", ["cashshop.adicionar"] = "Adicionou item na Loja de Cash",
        ["cashshop.remover"] = "Removeu item da Loja de Cash", ["drops.salvar"] = "Salvou drops", ["drops.taxas"] = "Salvou drop comum e zen",
        ["opcoes.salvar"] = "Salvou taxas e opções", ["exp.salvar"] = "Salvou EXP dinâmica", ["resets.salvar"] = "Salvou valores dos resets",
        ["monstros.salvar"] = "Salvou respawn de monstros", ["itemnovo.criar"] = "Criou item novo", ["itemnovo.remover"] = "Removeu item novo",
        ["lojaitens.salvar"] = "Salvou a loja de itens do site", ["site.noticia"] = "Publicou notícia", ["site.noticia.apagar"] = "Apagou notícia",
        ["site.paginas"] = "Salvou páginas do site", ["site.zen"] = "Salvou pacotes de Zen", ["site.mensagem"] = "Respondeu/marcou mensagem",
        ["relatorio.exportar"] = "Exportou relatório", ["alerta.lido"] = "Marcou alerta como lido", ["ban.expirou"] = "Ban temporário venceu",
    };

    public static string Nome(string acao) => nomes.TryGetValue(acao, out var n) ? n : acao;
    public static IReadOnlyDictionary<string, string> Todas => nomes;
}

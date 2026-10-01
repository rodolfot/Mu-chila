namespace MuChilaAdmin.Web.Servicos;

/// <summary>
/// Regras de "mexeu na conta" do painel antigo, iguais no web (decisão do dono, 28/09/2026): o servidor grava o personagem
/// ao sair, então pontos, árvore master e itens só mudam com a conta FORA do jogo. Online → desloga sozinho (sem perguntar),
/// espera o DataServer gravar e só então mexe. VIP e ban valem no próximo login: com a conta online, desloga depois.
/// </summary>
public static class OperacoesConta
{
    /// <summary>Desloga se estiver online e espera 3 s. Lança exceção se a conta continuar online (nada é alterado).</summary>
    public static List<string> GarantirFora(string conta, string para)
    {
        var log = new List<string>();
        if (!Db.IsOnline(conta)) return log;
        log.Add($"{conta} está online: deslogando para {para}...");
        log.Add(ServerControl.ForceLogout(conta));
        Thread.Sleep(3000);   // margem para o DataServer terminar de gravar o personagem
        if (Db.IsOnline(conta)) throw new InvalidOperationException($"{conta} ainda aparece online; nada foi alterado. Tente de novo em alguns segundos.");
        return log;
    }

    /// <summary>Depois de VIP/ban: se a conta estiver online, desloga para valer já no próximo login.</summary>
    public static string? DeslogarSeOnline(string conta, string porque) =>
        Db.IsOnline(conta) ? $"{conta} estava online: {ServerControl.ForceLogout(conta)} ({porque})" : null;

    /// <summary>Junta as linhas de resultado num texto só (para o aviso e a auditoria).</summary>
    public static string Juntar(IEnumerable<string?> linhas) => string.Join("\n", linhas.Where(l => !string.IsNullOrWhiteSpace(l)));
}

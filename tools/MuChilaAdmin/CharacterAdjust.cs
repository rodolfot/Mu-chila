namespace MuChilaAdmin;

/// <summary>
/// Painel, aba "VIP e contas" → "Nível e reset...": definir nível, dar EXP, definir master level e fazer Reset, Master Reset
/// ou Supreme Reset num personagem (para testes). Usa a mesma fila do reset pelo site (dbo.MuChila_ResetPedido): com o vigia
/// rodando, o personagem que estiver jogando vai para a seleção de personagem e a mudança é aplicada ali, sem desconectar;
/// fora do jogo é aplicada em ~1 s. Sem o vigia, só com a conta fora do jogo. Procedures em DB\1 - Querys\MuChila-Resets.sql.
/// </summary>
public static class CharacterAdjust
{
    public sealed record Action(string Kind, string Text, bool NeedsValue, long Min, long Max, long Default, string Hint);

    public static readonly Action[] Actions =
    {
        new("nivel", "Definir nível", true, 1, 400, 400, "1 a 400. Subindo, soma os pontos de atributo do jogo (5 ou 7 por nível, +1 da 3ª classe)."),
        new("exp", "Dar EXP", true, 1, 100_000_000_000, 10_000_000, "Soma EXP e sobe os níveis que ela der (até o 400), com os pontos de cada nível."),
        new("mlevel", "Definir master level", true, 0, 600, 600, "0 a 600. Subindo, soma 1 ponto master por nível. Precisa ter a árvore master (já ter chegado ao 400 e entrado no jogo)."),
        new("reset", "Reset", false, 0, 0, 0, "Precisa estar no nível 400. Volta ao nível 1 com os atributos iniciais e (resets × 300) pontos; o master level fica."),
        new("master", "Master Reset", false, 0, 0, 0, "Precisa master level 600. Zera a árvore master e dá os créditos do site."),
        new("supreme", "Supreme Reset", false, 0, 0, 0, "Precisa nível 400, master 600 e os 4 atributos no máximo. Zera tudo e dá os créditos do site."),
    };

    public static string Describe(int code) => code switch
    {
        0 => "feito",
        1 => "personagem não encontrado",
        2 => "a conta está online (o vigia não está rodando para mandar à seleção de personagem)",
        3 => "precisa estar no nível 400",
        4 => "precisa de master level 600",
        5 => "os atributos ainda não estão no máximo",
        6 => "classe sem atributos iniciais cadastrados",
        7 => "valor fora do permitido",
        8 => "o personagem ainda não tem árvore master (precisa chegar ao 400 e entrar no jogo uma vez)",
        _ => $"erro {code}",
    };

    /// <summary>Grava o pedido na fila do vigia. Devolve o Id.</summary>
    public static int Queue(string account, string character, string kind, long value)
    {
        var cfg = ResetConfig.Carregar();
        int coins = kind == "master" ? cfg.MasterCreditos : kind == "supreme" ? cfg.SupremeCreditos : 0;
        if (Db.Query("SELECT 1 FROM dbo.MuChila_ResetPedido WHERE Personagem = @n AND Status = 'pendente'", ("@n", character)).Rows.Count > 0)
            throw new InvalidOperationException($"{character} já tem um pedido em andamento (site ou painel). Espere terminar.");
        var t = Db.Query("INSERT dbo.MuChila_ResetPedido (Conta, Personagem, Tipo, Coins, MaxStat, Valor) OUTPUT INSERTED.Id VALUES (@a, @n, @t, @c, @m, @v)",
            ("@a", account), ("@n", character), ("@t", kind), ("@c", coins), ("@m", cfg.MaxStat), ("@v", value));
        return Convert.ToInt32(t.Rows[0][0]);
    }

    /// <summary>Situação do pedido: null = ainda pendente; senão o texto do resultado.</summary>
    public static string? Result(int id)
    {
        var t = Db.Query("SELECT Status, Resultado, Mensagem FROM dbo.MuChila_ResetPedido WHERE Id = @id", ("@id", id));
        if (t.Rows.Count == 0) return "pedido não encontrado";
        var status = (string)t.Rows[0]["Status"];
        if (status == "pendente") return null;
        if (status == "expirado") return t.Rows[0]["Mensagem"] as string ?? "expirou";
        return t.Rows[0]["Resultado"] is int r ? Describe(r) : status;
    }

    /// <summary>Sem o vigia: aplica direto (a procedure recusa se a conta estiver online).</summary>
    public static string Direct(string character, string kind, long value)
    {
        var cfg = ResetConfig.Carregar();
        int coins = kind == "master" ? cfg.MasterCreditos : kind == "supreme" ? cfg.SupremeCreditos : 0;
        return Describe(ResetWatcher.RunReset(character, kind, coins, cfg.MaxStat, value, ignoreOnline: false));
    }

    /// <summary>Nível, EXP, master level e resets do personagem, para mostrar antes e depois.</summary>
    public static string Status(string character)
    {
        var t = Db.Query(@"SELECT c.cLevel, dbo.MuChila_ExpLer(c.Experience) AS Exp, c.LevelUpPoint, c.ResetCount, c.MasterResetCount, c.SupremeResetCount,
                                  m.MasterLevel, m.MasterPoint
                           FROM dbo.[Character] c LEFT JOIN dbo.MasterSkillTree m ON m.Name = c.Name WHERE c.Name = @n", ("@n", character));
        if (t.Rows.Count == 0) return "(não encontrado)";
        var r = t.Rows[0];
        return $"nível {r["cLevel"]} (EXP {Convert.ToInt64(r["Exp"]):N0}, {r["LevelUpPoint"]} pontos livres), " +
               $"master {(r["MasterLevel"] is DBNull ? "-" : r["MasterLevel"])} ({(r["MasterPoint"] is DBNull ? 0 : r["MasterPoint"])} pontos), " +
               $"resets {r["ResetCount"]}/{r["MasterResetCount"]}/{r["SupremeResetCount"]}";
    }
}

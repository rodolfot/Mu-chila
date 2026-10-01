using System.Data;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Core;

/// <summary>Uma conta do jogo com o que a tela de detalhes mostra.</summary>
public sealed record DetalheConta(
    string Conta, string Nome, string? Email, DateTime? Criada, int NivelVip, DateTime? VipAte, bool Banida, bool Online,
    string? Servidor, string? Ip, DateTime? Entrou, DateTime? Saiu, int HorasOnline, int CashC, int CashP, int Goblin, int Creditos,
    DateTime? PasseAte)
{
    public bool VipAtivo => NivelVip > 0 && VipAte > DateTime.Now;
    public string Plano => VipAtivo ? Accounts.Levels[Math.Clamp(NivelVip, 0, 3)] : "Free";
}

/// <summary>
/// Contas do jogo para o painel web: lista com busca e filtros, detalhes, personagens, senha do jogo e Cash.
/// (VIP, ban, pontos e árvore master continuam em Accounts e Painel.Bans.)
/// </summary>
public static class Contas
{
    public static readonly (string Id, string Nome)[] Filtros =
        { ("todas", "Todas"), ("online", "Online agora"), ("vip", "VIP ativo"), ("banidas", "Banidas"), ("novas", "Criadas em 7 dias") };

    static int Int(object? v) => v == null || v is DBNull ? 0 : Convert.ToInt32(v);

    public static DataTable Lista(string? busca, string filtro, int max = 2000)
    {
        var w = new List<string>();
        var pars = new List<(string, object?)>();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            w.Add("(m.memb___id LIKE @b OR EXISTS (SELECT 1 FROM Character c2 WHERE c2.AccountID = m.memb___id AND c2.Name LIKE @b) OR m.mail_addr LIKE @b)");
            pars.Add(("@b", "%" + Painel.AuditoriaSql.Like(busca.Trim()) + "%"));
        }
        w.Add(filtro switch
        {
            "online" => "s.ConnectStat = 1",
            "vip" => "m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE()",
            "banidas" => "m.bloc_code = '1'",
            "novas" => "m.appl_days > DATEADD(day, -7, GETDATE())",
            _ => "1 = 1",
        });
        return Db.Query($@"
            SELECT TOP ({Math.Clamp(max, 1, 20000)}) m.memb___id AS Conta,
                   CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE() THEN 'VIP ' + CAST(m.AccountLevel AS varchar) ELSE 'Free' END AS Plano,
                   CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE() THEN m.AccountExpireDate END AS [VIP até],
                   CASE WHEN m.bloc_code = '1' THEN 'banida' WHEN s.ConnectStat = 1 THEN 'online' ELSE '' END AS Situação,
                   ISNULL(cs.WCoinC, 0) AS Cash,
                   m.appl_days AS Criada,
                   (SELECT COUNT(*) FROM Character c WHERE c.AccountID = m.memb___id) AS Personagens,
                   ISNULL((SELECT STRING_AGG(c.Name, ', ') FROM Character c WHERE c.AccountID = m.memb___id), '') AS Nomes,
                   s.ServerName AS _servidor
            FROM MEMB_INFO m
            LEFT JOIN MEMB_STAT s ON s.memb___id = m.memb___id
            LEFT JOIN CashShopData cs ON cs.AccountID = m.memb___id
            WHERE {string.Join(" AND ", w)}
            ORDER BY CASE WHEN s.ConnectStat = 1 THEN 0 ELSE 1 END, m.memb___id", pars.ToArray());
    }

    public static DetalheConta? Detalhes(string conta)
    {
        var t = Db.Query(@"
            SELECT m.memb___id, m.memb_name, m.mail_addr, m.appl_days, m.AccountLevel, m.AccountExpireDate, m.bloc_code,
                   s.ConnectStat, s.ServerName, s.IP, s.ConnectTM, s.DisConnectTM, s.OnlineHours,
                   ISNULL(cs.WCoinC, 0) AS c, ISNULL(cs.WCoinP, 0) AS p, ISNULL(cs.GoblinPoint, 0) AS g
            FROM MEMB_INFO m LEFT JOIN MEMB_STAT s ON s.memb___id = m.memb___id LEFT JOIN CashShopData cs ON cs.AccountID = m.memb___id
            WHERE m.memb___id = @a", ("@a", conta));
        if (t.Rows.Count == 0) return null;
        var r = t.Rows[0];
        int creditos = Db.TableExists("MuChila_Creditos") ? Int(Db.Scalar("SELECT Creditos FROM dbo.MuChila_Creditos WHERE AccountID = @a", ("@a", conta))) : 0;
        DateTime? passe = null;
        try { if (Db.TableExists("MuChila_PasseMapas")) passe = PassMaps.Expiry(conta); } catch { }
        return new DetalheConta((string)r["memb___id"], (r["memb_name"] as string ?? "").Trim(), (r["mail_addr"] as string)?.Trim(), r["appl_days"] as DateTime?,
            Int(r["AccountLevel"]), r["AccountExpireDate"] as DateTime?, (r["bloc_code"] as string ?? "").Trim() == "1", Int(r["ConnectStat"]) == 1,
            r["ServerName"] as string, r["IP"] as string, r["ConnectTM"] as DateTime?, r["DisConnectTM"] as DateTime?, Int(r["OnlineHours"]),
            Int(r["c"]), Int(r["p"]), Int(r["g"]), creditos, passe);
    }

    public static DataTable Personagens(string conta) => Db.Query(@"
        SELECT c.Name AS Personagem, c.Class AS _classe, c.cLevel AS Nível, ISNULL(m.MasterLevel, 0) AS Master,
               ISNULL(c.ResetCount, 0) AS Resets, ISNULL(c.MasterResetCount, 0) AS [M. resets], ISNULL(c.SupremeResetCount, 0) AS [S. resets],
               c.LevelUpPoint AS [Pontos livres], ISNULL(m.MasterPoint, 0) AS [Pontos master], c.Money AS Zen, c.PkCount AS PK, c.MapNumber AS _mapa,
               CASE WHEN a.GameIDC = c.Name AND s.ConnectStat = 1 THEN 1 ELSE 0 END AS _jogando, c.CtlCode AS _ctl
        FROM Character c
        LEFT JOIN MasterSkillTree m ON m.Name = c.Name
        LEFT JOIN AccountCharacter a ON a.Id = c.AccountID
        LEFT JOIN MEMB_STAT s ON s.memb___id = c.AccountID
        WHERE c.AccountID = @a ORDER BY c.cLevel + ISNULL(m.MasterLevel, 0) DESC, c.Name", ("@a", conta));

    /// <summary>Troca a senha do jogo (MEMB_INFO.memb__pwd, a mesma do site). O jogo aceita até 10 letras e números.</summary>
    public static string TrocarSenha(string conta, string nova)
    {
        nova = (nova ?? "").Trim();
        if (!Regex.IsMatch(nova, "^[A-Za-z0-9]{4,10}$")) throw new InvalidOperationException("A senha do jogo precisa ter de 4 a 10 letras ou números (sem espaço nem acento).");
        if (Db.Execute("UPDATE MEMB_INFO SET memb__pwd = @p WHERE memb___id = @a", ("@p", nova), ("@a", conta)) != 1)
            throw new InvalidOperationException($"A conta {conta} não existe.");
        return $"{conta}: senha do jogo trocada. Vale no próximo login (no jogo e no site).";
    }

    public static readonly (string Coluna, string Nome)[] Moedas = { ("WCoinC", "W Coin (C) — Cash"), ("WCoinP", "W Coin (P)"), ("GoblinPoint", "Goblin Point") };

    /// <summary>Soma (ou tira, com negativo) uma moeda da Cash Shop. Cria a linha da conta se ela nunca abriu a loja. Nunca fica negativo.</summary>
    public static string AjustarCash(string conta, string coluna, int quantidade)
    {
        if (!Moedas.Any(m => m.Coluna == coluna)) throw new ArgumentException("Moeda inválida.");
        if (quantidade == 0) throw new InvalidOperationException("Informe uma quantidade diferente de 0.");
        if (Db.Scalar("SELECT 1 FROM MEMB_INFO WHERE memb___id = @a", ("@a", conta)) == null) throw new InvalidOperationException($"A conta {conta} não existe.");
        var antes = Int(Db.Scalar($"SELECT {coluna} FROM CashShopData WHERE AccountID = @a", ("@a", conta)));
        long depois = Math.Clamp((long)antes + quantidade, 0, int.MaxValue);
        Db.Execute($@"IF EXISTS (SELECT 1 FROM CashShopData WHERE AccountID = @a) UPDATE CashShopData SET {coluna} = @v WHERE AccountID = @a
                      ELSE INSERT INTO CashShopData (AccountID, WCoinC, WCoinP, GoblinPoint) VALUES (@a, 0, 0, 0);
                      UPDATE CashShopData SET {coluna} = @v WHERE AccountID = @a;", ("@a", conta), ("@v", (int)depois));
        var nome = Moedas.First(m => m.Coluna == coluna).Nome;
        return $"{conta}: {nome} {(quantidade > 0 ? "+" : "")}{quantidade:N0} ({antes:N0} → {depois:N0}). Aparece ao abrir a Cash Shop (se não, relogar).".Replace(',', '.');
    }

    /// <summary>Nome do mapa pelo código (dos arquivos de spawn), para a tela.</summary>
    public static string NomeDoMapa(int codigo, Dictionary<int, string>? nomes = null)
    {
        try { return (nomes ?? Monsters.NomesDosMapas()).TryGetValue(codigo, out var n) ? n : $"mapa {codigo}"; }
        catch { return $"mapa {codigo}"; }
    }
}

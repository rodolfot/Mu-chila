using System.Data;

namespace MuChilaAdmin;

/// <summary>VIP (MEMB_INFO.AccountLevel + AccountExpireDate) e bloqueio de contas.</summary>
public static class Accounts
{
    public static readonly string[] Levels = { "Free", "VIP 1", "VIP 2", "VIP 3" };

    public static DataTable List() => Db.Query(@"
        SELECT m.memb___id AS Conta,
               CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE()
                    THEN 'VIP ' + CAST(m.AccountLevel AS varchar) ELSE 'Free' END AS Nivel,
               CASE WHEN m.AccountLevel > 0 THEN CONVERT(varchar(16), m.AccountExpireDate, 103) + ' ' + CONVERT(varchar(5), m.AccountExpireDate, 108) ELSE '' END AS Expira,
               CASE WHEN m.bloc_code = '1' THEN 'SIM' ELSE '' END AS Banida,
               CASE WHEN s.ConnectStat = 1 THEN 'online' ELSE '' END AS Status,
               (SELECT STRING_AGG(c.Name, ', ') FROM Character c WHERE c.AccountID = m.memb___id) AS Personagens
        FROM MEMB_INFO m LEFT JOIN MEMB_STAT s ON s.memb___id = m.memb___id
        ORDER BY m.memb___id");

    /// <summary>Define o VIP com validade de hoje + dias (level 0 remove o VIP). Vale no proximo login da conta.</summary>
    public static void SetVip(string account, int level, int days)
    {
        var expire = level == 0 ? DateTime.Now : DateTime.Now.AddDays(days);
        Db.Execute("UPDATE MEMB_INFO SET AccountLevel = @l, AccountExpireDate = @e WHERE memb___id = @a",
            ("@l", level), ("@e", expire), ("@a", account));
    }

    public static void SetBanned(string account, bool banned) =>
        Db.Execute("UPDATE MEMB_INFO SET bloc_code = @b WHERE memb___id = @a", ("@b", banned ? "1" : "0"), ("@a", account));

    public static DataTable Online() => Db.Query(@"
        SELECT s.memb___id AS Conta, a.GameIDC AS Personagem, s.IP, CONVERT(varchar(5), s.ConnectTM, 108) AS Desde
        FROM MEMB_STAT s LEFT JOIN AccountCharacter a ON a.Id = s.memb___id
        WHERE s.ConnectStat = 1 ORDER BY s.memb___id");
}

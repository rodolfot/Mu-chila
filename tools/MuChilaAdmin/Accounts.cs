using System.Data;
using System.IO;

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

    public static string[] Characters(string account) =>
        Db.Query("SELECT Name FROM Character WHERE AccountID = @a ORDER BY Name", ("@a", account))
          .Rows.Cast<DataRow>().Select(r => (string)r["Name"]).ToArray();

    /// <summary>
    /// Apaga as habilidades master do personagem e devolve os pontos (1 por Master Level, MasterSkillTreePoint = 1).
    /// Usado quando a janela (C) mostra "Suces de Atq" negativo (ver docs/PROBLEMAS-E-SOLUCOES.md). A conta precisa estar offline.
    /// </summary>
    public static string ClearMasterSkills(string account, string character)
    {
        if (Db.IsOnline(account)) throw new InvalidOperationException($"A conta {account} está online. Peça para sair do jogo e tente de novo.");
        var t = Db.Query("SELECT MasterLevel, MasterPoint, MasterSkill FROM MasterSkillTree WHERE Name = @n", ("@n", character));
        if (t.Rows.Count == 0) return $"{character}: não tem árvore master; nada a fazer.";
        var row = t.Rows[0];
        int level = row["MasterLevel"] is int l ? l : 0, points = row["MasterPoint"] is int p ? p : 0;
        var old = row["MasterSkill"] as byte[] ?? new byte[360];

        // 3 bytes por habilidade; FF 00 FF = vazio
        var empty = new byte[old.Length == 0 ? 360 : old.Length];
        for (int i = 0; i + 2 < empty.Length; i += 3) { empty[i] = 0xFF; empty[i + 1] = 0; empty[i + 2] = 0xFF; }
        int learned = 0;
        for (int i = 0; i + 2 < old.Length; i += 3) if (!(old[i] == 0xFF && old[i + 1] == 0 && old[i + 2] == 0xFF)) learned++;

        var backup = Path.Combine(ServerControl.ServerRoot, "DB", "backup-caixas-master-habilidades.csv");
        File.AppendAllText(backup, $"{DateTime.Now:s};{account};{character};MasterLevel={level};MasterPoint={points};{Convert.ToHexString(old)}{Environment.NewLine}");

        int n = Db.Execute(@"UPDATE MasterSkillTree SET MasterSkill = @b, MasterPoint = ISNULL(MasterLevel, 0)
                             WHERE Name = @n AND NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1)",
            ("@b", empty), ("@n", character), ("@a", account));
        if (n != 1) throw new InvalidOperationException($"A conta {account} entrou no jogo durante a operação; nada foi alterado.");
        return $"{character}: {learned} habilidade(s) master apagada(s); pontos master agora = {level} (antes {points}). Backup em {backup}";
    }

    public static DataTable Online() => Db.Query(@"
        SELECT s.memb___id AS Conta, a.GameIDC AS Personagem, s.IP, CONVERT(varchar(5), s.ConnectTM, 108) AS Desde
        FROM MEMB_STAT s LEFT JOIN AccountCharacter a ON a.Id = s.memb___id
        WHERE s.ConnectStat = 1 ORDER BY s.memb___id");
}

using System.Data;
using System.IO;

namespace MuChilaAdmin;

/// <summary>
/// Passe dos Mapas Exclusivos (mapas acima do nível 400), aba "Passe dos Mapas": dar e tirar passe de uma conta para testes,
/// ver quem tem, a lista de mapas, o histórico e o que o vigia fez. Banco: DB\1 - Querys\MuChila-PasseMapas.sql.
/// Quem cobra é o vigia (ResetWatcher.Passe.cs), ligado pelo arquivo vigia-passe-mapas.ligado.
/// </summary>
public static class PassMaps
{
    public static readonly string[] Units = { "minuto(s)", "hora(s)", "dia(s)" };
    static readonly int[] UnitSeconds = { 60, 3600, 86400 };

    public static DataTable Passes() => Db.Query(@"
        SELECT p.Conta,
               CONVERT(varchar(10), p.Expira, 103) + ' ' + CONVERT(varchar(8), p.Expira, 108) AS [Vale até],
               CASE WHEN p.Expira <= GETDATE() THEN 'vencido'
                    WHEN DATEDIFF(second, GETDATE(), p.Expira) >= 86400 THEN CAST(DATEDIFF(second, GETDATE(), p.Expira) / 86400 AS varchar) + ' dia(s) e ' + CAST(DATEDIFF(second, GETDATE(), p.Expira) % 86400 / 3600 AS varchar) + ' h'
                    WHEN DATEDIFF(second, GETDATE(), p.Expira) >= 3600 THEN CAST(DATEDIFF(second, GETDATE(), p.Expira) / 3600 AS varchar) + ' h e ' + CAST(DATEDIFF(second, GETDATE(), p.Expira) % 3600 / 60 AS varchar) + ' min'
                    ELSE CAST(DATEDIFF(second, GETDATE(), p.Expira) / 60 AS varchar) + ' min e ' + CAST(DATEDIFF(second, GETDATE(), p.Expira) % 60 AS varchar) + ' s' END AS Falta,
               CASE WHEN s.ConnectStat = 1 THEN 'online' ELSE '' END AS Status
        FROM dbo.MuChila_PasseMapas p LEFT JOIN dbo.MEMB_STAT s ON s.memb___id = p.Conta
        ORDER BY CASE WHEN p.Expira > GETDATE() THEN 0 ELSE 1 END, p.Expira DESC");

    public static DataTable Maps() => Db.Query("SELECT Mapa, Nome FROM dbo.MuChila_PasseMapasLista ORDER BY Mapa");

    public static DataTable History(int top = 100) => Db.Query($@"
        SELECT TOP ({top}) CONVERT(varchar(10), Criado, 103) + ' ' + CONVERT(varchar(8), Criado, 108) AS Quando, Conta,
               CASE WHEN Segundos % 86400 = 0 THEN CAST(Segundos / 86400 AS varchar) + ' dia(s)'
                    WHEN Segundos % 3600 = 0 THEN CAST(Segundos / 3600 AS varchar) + ' h'
                    WHEN Segundos % 60 = 0 THEN CAST(Segundos / 60 AS varchar) + ' min'
                    ELSE CAST(Segundos AS varchar) + ' s' END AS Tempo,
               Origem, CONVERT(varchar(10), ExpiraDepois, 103) + ' ' + CONVERT(varchar(8), ExpiraDepois, 108) AS [Vale até]
        FROM dbo.MuChila_PasseMapasLog ORDER BY Id DESC");

    public static string[] AccountNames() =>
        Db.Query("SELECT memb___id FROM dbo.MEMB_INFO ORDER BY memb___id").Rows.Cast<DataRow>().Select(r => ((string)r[0]).Trim()).ToArray();

    /// <summary>Soma tempo ao passe da conta (ativo: soma ao vencimento; vencido ou novo: começa agora). Vale na hora, até no jogo.</summary>
    public static string Give(string account, int amount, int unit)
    {
        if (amount <= 0) throw new ArgumentException("A quantidade precisa ser maior que zero.");
        int seconds = checked(amount * UnitSeconds[unit]);
        var t = Db.Query("DECLARE @r int; EXEC @r = dbo.MuChila_PasseAdicionar @Conta = @c, @Segundos = @s, @Origem = 'painel'; SELECT @r AS r",
            ("@c", account), ("@s", seconds));
        if (t.Rows.Count == 0 || Convert.ToInt32(t.Rows[^1]["r"]) != 0) throw new InvalidOperationException($"A conta {account} não existe.");
        return $"{account}: passe + {amount} {Units[unit]}, vale até {Expiry(account):dd/MM/yyyy HH:mm:ss}";
    }

    /// <summary>Vence o passe agora (fica no histórico). Quem estiver num mapa do passe sai em até ~5 s.</summary>
    public static string Remove(string account)
    {
        var before = Expiry(account);
        if (before == null || before <= DateTime.Now) return $"{account}: não tem passe ativo.";
        Db.Execute("UPDATE dbo.MuChila_PasseMapas SET Expira = GETDATE(), Atualizado = GETDATE() WHERE Conta = @c; " +
                   "INSERT dbo.MuChila_PasseMapasLog (Conta, Segundos, Origem, ExpiraAntes, ExpiraDepois) VALUES (@c, 0, 'painel: passe tirado', @a, GETDATE())",
            ("@c", account), ("@a", before.Value));
        return $"{account}: passe tirado (valia até {before:dd/MM/yyyy HH:mm})";
    }

    public static DateTime? Expiry(string account)
    {
        var t = Db.Query("SELECT Expira FROM dbo.MuChila_PasseMapas WHERE Conta = @c", ("@c", account));
        return t.Rows.Count == 0 ? null : (DateTime)t.Rows[0]["Expira"];
    }

    /// <summary>O vigia só tira quem está sem passe com este arquivo presente.</summary>
    public static bool Enforced
    {
        get => File.Exists(ResetWatcher.PassSwitchPath);
        set
        {
            if (value && !Enforced) File.WriteAllText(ResetWatcher.PassSwitchPath, $"ligado pelo painel em {DateTime.Now:dd/MM/yyyy HH:mm}");
            else if (!value && Enforced) File.Delete(ResetWatcher.PassSwitchPath);
        }
    }

    /// <summary>Últimas linhas do vigia.log sobre o passe (quem foi tirado e quando).</summary>
    public static string[] WatcherLog(int max = 60)
    {
        try
        {
            if (!File.Exists(ResetWatcher.LogPath)) return Array.Empty<string>();
            using var fs = new FileStream(ResetWatcher.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Contains(" passe: ")).TakeLast(max).Reverse().ToArray();
        }
        catch (IOException) { return Array.Empty<string>(); }
    }
}

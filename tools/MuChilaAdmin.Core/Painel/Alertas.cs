using System.Data;

namespace MuChilaAdmin.Core.Painel;

public static class Severidade
{
    public const string Info = "info", Aviso = "aviso", Critico = "critico";
}

public sealed record Alerta(
    int Id, DateTime Criado, DateTime Atualizado, string Chave, string Tipo, string Severidade, string Titulo, string? Texto, string? Link,
    int Vezes, DateTime? ResolvidoEm, DateTime? LidoEm, string? LidoPor)
{
    public bool Aberto => ResolvidoEm == null;
    public bool Lido => LidoEm != null;
}

/// <summary>
/// Alertas do painel (sino no topo). Cada problema tem uma chave ("processo:GameServer", "vigia", "contato"...): enquanto
/// ele continua, o mesmo alerta é atualizado em vez de repetir; quando some, o alerta é resolvido sozinho. Quem gera é o
/// monitor do painel (a cada 30 s) e algumas ações (ex.: usuário bloqueado por senha errada).
/// </summary>
public sealed class Alertas
{

    /// <summary>Disparado quando um alerta abre, muda ou fecha (o sino de todas as telas abertas se atualiza).</summary>
    public static event Action? Mudou;
    static void Avisar() { try { Mudou?.Invoke(); } catch { } }

    static Alerta Ler(DataRow r) => new((int)r["id"], (DateTime)r["criado"], (DateTime)r["atualizado"], (string)r["chave"], (string)r["tipo"],
        (string)r["severidade"], (string)r["titulo"], r["texto"] as string, r["link"] as string, (int)r["vezes"],
        r["resolvido_em"] as DateTime?, r["lido_em"] as DateTime?, r["lido_por"] as string);

    /// <summary>Abre (ou atualiza, se já estiver aberto com a mesma chave) um alerta. Devolve true se é novo.</summary>
    public bool Abrir(string chave, string tipo, string severidade, string titulo, string? texto = null, string? link = null)
    {
        var aberto = BancoPainel.Query("SELECT id, titulo, texto, severidade FROM dbo.MUCHILA_ADMIN_ALERTAS WHERE chave = @c AND resolvido_em IS NULL", ("@c", chave));
        if (aberto.Rows.Count > 0)
        {
            var r = aberto.Rows[0];
            bool mudou = (string)r["titulo"] != titulo || (r["texto"] as string) != texto || (string)r["severidade"] != severidade;
            if (mudou)
            {
                // texto novo (ex.: "3 mensagens" virou "4 mensagens"): volta a aparecer como não lido
                BancoPainel.Execute(@"UPDATE dbo.MUCHILA_ADMIN_ALERTAS SET titulo = @t, texto = @x, severidade = @s, link = @l, atualizado = SYSDATETIME(),
                             vezes = vezes + 1, lido_em = NULL, lido_por = NULL WHERE id = @id",
                    ("@t", titulo), ("@x", (object?)texto ?? DBNull.Value), ("@s", severidade), ("@l", (object?)link ?? DBNull.Value), ("@id", (int)r["id"]));
                Avisar();
            }
            return false;
        }
        try
        {
            BancoPainel.Execute(@"INSERT INTO dbo.MUCHILA_ADMIN_ALERTAS (chave, tipo, severidade, titulo, texto, link) VALUES (@c, @tp, @s, @t, @x, @l)",
                ("@c", chave), ("@tp", tipo), ("@s", severidade), ("@t", titulo), ("@x", (object?)texto ?? DBNull.Value), ("@l", (object?)link ?? DBNull.Value));
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627) { return false; }   // outro processo abriu ao mesmo tempo
        Avisar();
        return true;
    }

    /// <summary>Fecha o alerta aberto com essa chave (o problema sumiu). Devolve true se havia um.</summary>
    public bool Resolver(string chave)
    {
        int n = BancoPainel.Execute("UPDATE dbo.MUCHILA_ADMIN_ALERTAS SET resolvido_em = SYSDATETIME(), atualizado = SYSDATETIME() WHERE chave = @c AND resolvido_em IS NULL", ("@c", chave));
        if (n > 0) Avisar();
        return n > 0;
    }

    /// <summary>Fecha todos os abertos cujas chaves começam com o prefixo e não estão na lista (ex.: processos que voltaram).</summary>
    public int ResolverExceto(string prefixo, IEnumerable<string> continuam)
    {
        var manter = continuam.ToHashSet();
        int n = 0;
        foreach (DataRow r in BancoPainel.Query("SELECT chave FROM dbo.MUCHILA_ADMIN_ALERTAS WHERE resolvido_em IS NULL AND chave LIKE @p", ("@p", AuditoriaSql.Like(prefixo) + "%")).Rows)
            if (!manter.Contains((string)r[0]) && Resolver((string)r[0])) n++;
        return n;
    }

    public void MarcarLido(int id, string usuario)
    {
        if (BancoPainel.Execute("UPDATE dbo.MUCHILA_ADMIN_ALERTAS SET lido_em = SYSDATETIME(), lido_por = @u WHERE id = @id AND lido_em IS NULL", ("@u", usuario), ("@id", id)) > 0) Avisar();
    }

    public void MarcarTodosLidos(string usuario)
    {
        if (BancoPainel.Execute("UPDATE dbo.MUCHILA_ADMIN_ALERTAS SET lido_em = SYSDATETIME(), lido_por = @u WHERE lido_em IS NULL", ("@u", usuario)) > 0) Avisar();
    }

    /// <summary>Abertos primeiro (críticos antes), depois os resolvidos mais recentes.</summary>
    public List<Alerta> Lista(bool incluirResolvidos = true, int max = 200) =>
        BancoPainel.Query($@"SELECT TOP ({Math.Clamp(max, 1, 2000)}) * FROM dbo.MUCHILA_ADMIN_ALERTAS
                    {(incluirResolvidos ? "" : "WHERE resolvido_em IS NULL")}
                    ORDER BY CASE WHEN resolvido_em IS NULL THEN 0 ELSE 1 END,
                             CASE severidade WHEN 'critico' THEN 0 WHEN 'aviso' THEN 1 ELSE 2 END, atualizado DESC").Rows.Cast<DataRow>().Select(Ler).ToList();

    public (int Abertos, int NaoLidos, int Criticos) Contagem()
    {
        var r = BancoPainel.Query(@"SELECT COUNT(*) AS a, SUM(CASE WHEN lido_em IS NULL THEN 1 ELSE 0 END) AS n, SUM(CASE WHEN severidade = 'critico' THEN 1 ELSE 0 END) AS c
                           FROM dbo.MUCHILA_ADMIN_ALERTAS WHERE resolvido_em IS NULL").Rows[0];
        return (Convert.ToInt32(r["a"]), r["n"] is DBNull ? 0 : Convert.ToInt32(r["n"]), r["c"] is DBNull ? 0 : Convert.ToInt32(r["c"]));
    }

    /// <summary>Apaga alertas resolvidos há mais de N dias (o monitor chama uma vez por dia).</summary>
    public int Limpar(int dias = 60) =>
        BancoPainel.Execute("DELETE FROM dbo.MUCHILA_ADMIN_ALERTAS WHERE resolvido_em IS NOT NULL AND resolvido_em < DATEADD(day, -@d, SYSDATETIME())", ("@d", dias));
}

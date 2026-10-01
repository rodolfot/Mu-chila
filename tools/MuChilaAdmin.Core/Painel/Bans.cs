using System.Data;

namespace MuChilaAdmin.Core.Painel;

public sealed record Ban(
    int Id, string Conta, string Motivo, DateTime Inicio, DateTime? Fim, string Por, bool Ativo,
    DateTime? EncerradoEm, string? EncerradoPor, string? MotivoEncerramento)
{
    public bool Permanente => Fim == null;
    public string Prazo => Fim is { } f ? $"até {f:dd/MM/yyyy HH:mm}" : "permanente";
}

/// <summary>
/// Bans com motivo, prazo e histórico. O jogo só olha MEMB_INFO.bloc_code (1 = não entra); esta tabela guarda o porquê,
/// quem baniu, até quando, e quem desbaniu. Ban temporário vence sozinho (o painel confere a cada minuto). Contas com
/// bloc_code = 1 sem linha aqui (banidas antes do painel web ou pelo banco) aparecem como "sem registro".
/// </summary>
public sealed class Bans
{

    static Ban Ler(DataRow r) => new((int)r["id"], (string)r["conta"], (string)r["motivo"], (DateTime)r["inicio"], r["fim"] as DateTime?, (string)r["por"],
        (bool)r["ativo"], r["encerrado_em"] as DateTime?, r["encerrado_por"] as string, r["motivo_encerramento"] as string);

    public static bool ContaExiste(string conta) => BancoPainel.Scalar("SELECT 1 FROM MEMB_INFO WHERE memb___id = @a", ("@a", conta)) != null;
    public static bool Banida(string conta) => BancoPainel.Scalar("SELECT bloc_code FROM MEMB_INFO WHERE memb___id = @a", ("@a", conta)) is string s && s.Trim() == "1";

    /// <summary>Bane a conta (até "ate", ou para sempre com null). Se já estava banida, o ban anterior é encerrado e substituído.</summary>
    public Ban Banir(string conta, string motivo, DateTime? ate, string por)
    {
        conta = conta.Trim();
        if (!ContaExiste(conta)) throw new InvalidOperationException($"A conta {conta} não existe.");
        motivo = (motivo ?? "").Trim();
        if (motivo.Length < 3) throw new InvalidOperationException("Escreva o motivo do ban (aparece no histórico da conta).");
        if (motivo.Length > 300) motivo = motivo[..300];
        if (ate is { } a && a <= DateTime.Now.AddMinutes(1)) throw new InvalidOperationException("O fim do ban precisa ser no futuro.");
        BancoPainel.Execute(@"SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.MUCHILA_ADMIN_BANS SET ativo = 0, encerrado_em = SYSDATETIME(), encerrado_por = @por, motivo_encerramento = N'substituído por um ban novo'
              WHERE conta = @c AND ativo = 1;
            INSERT INTO dbo.MUCHILA_ADMIN_BANS (conta, motivo, fim, por) VALUES (@c, @m, @fim, @por);
            UPDATE MEMB_INFO SET bloc_code = '1' WHERE memb___id = @c;
            COMMIT;", ("@c", conta), ("@m", motivo), ("@fim", (object?)ate ?? DBNull.Value), ("@por", por));
        return Historico(conta).First();
    }

    public void Desbanir(string conta, string motivo, string por)
    {
        conta = conta.Trim();
        if (!ContaExiste(conta)) throw new InvalidOperationException($"A conta {conta} não existe.");
        motivo = (motivo ?? "").Trim();
        BancoPainel.Execute(@"SET XACT_ABORT ON; BEGIN TRAN;
            UPDATE dbo.MUCHILA_ADMIN_BANS SET ativo = 0, encerrado_em = SYSDATETIME(), encerrado_por = @por, motivo_encerramento = @m
              WHERE conta = @c AND ativo = 1;
            UPDATE MEMB_INFO SET bloc_code = '0' WHERE memb___id = @c;
            COMMIT;", ("@c", conta), ("@m", motivo.Length == 0 ? "desbanida pelo painel" : motivo.Length > 300 ? motivo[..300] : motivo), ("@por", por));
    }

    /// <summary>Contas banidas agora, com o registro do painel quando houver.</summary>
    public DataTable Ativos() => BancoPainel.Query(@"
        SELECT m.memb___id AS Conta, b.id AS Id, b.motivo AS Motivo, b.inicio AS Inicio, b.fim AS Fim, b.por AS Por,
               CASE WHEN b.id IS NULL THEN 1 ELSE 0 END AS SemRegistro,
               CASE WHEN s.ConnectStat = 1 THEN 1 ELSE 0 END AS Online
        FROM MEMB_INFO m
        LEFT JOIN dbo.MUCHILA_ADMIN_BANS b ON b.conta = m.memb___id AND b.ativo = 1
        LEFT JOIN MEMB_STAT s ON s.memb___id = m.memb___id
        WHERE m.bloc_code = '1'
        ORDER BY b.inicio DESC, m.memb___id");

    public List<Ban> Historico(string? conta = null, int max = 500) =>
        BancoPainel.Query($"SELECT TOP ({Math.Clamp(max, 1, 5000)}) * FROM dbo.MUCHILA_ADMIN_BANS {(conta == null ? "" : "WHERE conta = @c")} ORDER BY id DESC",
            conta == null ? Array.Empty<(string, object?)>() : new[] { ("@c", (object?)conta) }).Rows.Cast<DataRow>().Select(Ler).ToList();

    /// <summary>Desbane os bans temporários vencidos. Devolve as contas desbanidas (para a auditoria).</summary>
    public List<string> ExpirarVencidos()
    {
        var vencidos = BancoPainel.Query("SELECT conta FROM dbo.MUCHILA_ADMIN_BANS WHERE ativo = 1 AND fim IS NOT NULL AND fim <= SYSDATETIME()")
            .Rows.Cast<DataRow>().Select(r => (string)r[0]).Distinct().ToList();
        foreach (var c in vencidos) Desbanir(c, "prazo do ban terminou", "sistema");
        return vencidos;
    }
}

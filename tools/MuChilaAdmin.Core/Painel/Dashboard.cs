using System.Data;
using System.Text.Json;

namespace MuChilaAdmin.Core.Painel;

/// <summary>Números do dashboard. Tudo vem do banco; tabelas do site que ainda não existam valem zero.</summary>
public sealed class ResumoDashboard
{
    public DateTime Gerado { get; init; } = DateTime.Now;
    public int Online { get; init; }
    public Dictionary<string, int> OnlinePorServidor { get; init; } = new();
    public int Contas { get; init; }
    public int ContasNovas24h { get; init; }
    public int ContasNovas7d { get; init; }
    public int ContasNovas30d { get; init; }
    public int ContasBanidas { get; init; }
    public Dictionary<int, int> VipPorNivel { get; init; } = new();
    public int VipTotal => VipPorNivel.Values.Sum();
    public int Personagens { get; init; }
    public List<(string Familia, int Quantidade)> PersonagensPorClasse { get; init; } = new();
    public List<(string Nome, string Conta, int Nivel, int Master, int Resets)> TopPersonagens { get; init; } = new();
    public decimal Receita24h { get; init; }
    public decimal Receita7d { get; init; }
    public decimal Receita30d { get; init; }
    public int PedidosPendentes { get; init; }
    public int PedidosComProblema { get; init; }
    public int CashLojaItens30d { get; init; }
    public int ComprasLojaItens30d { get; init; }
    public int CashZen30d { get; init; }
    public int MensagensNovas { get; init; }
    public int AnunciosMercado { get; init; }
    public List<(DateTime Dia, int Quantidade)> ContasPorDia { get; init; } = new();
}

public static class Dashboard
{
    /// <summary>Jogadores conectados agora, por servidor (MEMB_STAT.ServerName).</summary>
    public static Dictionary<string, int> OnlinePorServidor() =>
        Db.Query(@"SELECT ISNULL(NULLIF(LTRIM(RTRIM(ServerName)), ''), '?') AS s, COUNT(*) AS n FROM MEMB_STAT WHERE ConnectStat = 1
                   GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(ServerName)), ''), '?') ORDER BY COUNT(*) DESC")
          .Rows.Cast<DataRow>().ToDictionary(r => (string)r["s"], r => Convert.ToInt32(r["n"]));

    static int Int(object? v) => v == null || v is DBNull ? 0 : Convert.ToInt32(v);
    static decimal Dec(object? v) => v == null || v is DBNull ? 0 : Convert.ToDecimal(v);

    public static readonly Dictionary<int, string> Familias = new()
    {
        [0] = "Dark Wizard", [16] = "Dark Knight", [32] = "Fairy Elf", [48] = "Magic Gladiator", [64] = "Dark Lord", [80] = "Summoner",
        [96] = "Rage Fighter", [112] = "Grow Lancer", [128] = "Rune Wizard", [144] = "Slayer", [160] = "Gun Crusher",
    };

    public static string Familia(int classe) => Familias.TryGetValue(classe / 16 * 16, out var f) ? f : $"Classe {classe}";

    public static ResumoDashboard Resumo()
    {
        var online = OnlinePorServidor();
        var contas = Db.Query(@"SELECT COUNT(*) AS total,
                SUM(CASE WHEN appl_days > DATEADD(hour, -24, GETDATE()) THEN 1 ELSE 0 END) AS d1,
                SUM(CASE WHEN appl_days > DATEADD(day, -7, GETDATE()) THEN 1 ELSE 0 END) AS d7,
                SUM(CASE WHEN appl_days > DATEADD(day, -30, GETDATE()) THEN 1 ELSE 0 END) AS d30,
                SUM(CASE WHEN bloc_code = '1' THEN 1 ELSE 0 END) AS ban
            FROM MEMB_INFO").Rows[0];
        var vip = Db.Query("SELECT AccountLevel AS n, COUNT(*) AS q FROM MEMB_INFO WHERE AccountLevel > 0 AND AccountExpireDate > GETDATE() GROUP BY AccountLevel")
            .Rows.Cast<DataRow>().ToDictionary(r => Int(r["n"]), r => Int(r["q"]));
        var classes = Db.Query("SELECT Class / 16 * 16 AS f, COUNT(*) AS q FROM Character GROUP BY Class / 16 * 16 ORDER BY COUNT(*) DESC")
            .Rows.Cast<DataRow>().Select(r => (Familia(Int(r["f"])), Int(r["q"]))).ToList();
        var top = Db.Query(@"SELECT TOP 8 c.Name, c.AccountID, c.cLevel, ISNULL(m.MasterLevel, 0) AS ml, ISNULL(c.ResetCount, 0) AS rs
                FROM Character c LEFT JOIN MasterSkillTree m ON m.Name = c.Name
                WHERE ISNULL(c.CtlCode, 0) = 0
                ORDER BY ISNULL(c.ResetCount, 0) DESC, c.cLevel + ISNULL(m.MasterLevel, 0) DESC")
            .Rows.Cast<DataRow>().Select(r => ((string)r["Name"], (string)r["AccountID"], Int(r["cLevel"]), Int(r["ml"]), Int(r["rs"]))).ToList();
        var porDia = Db.Query(@"SELECT CAST(appl_days AS date) AS d, COUNT(*) AS q FROM MEMB_INFO
                WHERE appl_days >= DATEADD(day, -13, CAST(GETDATE() AS date)) GROUP BY CAST(appl_days AS date)")
            .Rows.Cast<DataRow>().ToDictionary(r => (DateTime)r["d"], r => Int(r["q"]));
        var hoje = DateTime.Today;
        var serie = Enumerable.Range(0, 14).Select(i => hoje.AddDays(i - 13)).Select(d => (d, porDia.GetValueOrDefault(d))).ToList();

        decimal r1 = 0, r7 = 0, r30 = 0; int pend = 0, prob = 0;
        if (Db.TableExists("MUCHILA_PEDIDOS"))
        {
            var p = Db.Query(@"SELECT
                    SUM(CASE WHEN status = 'entregue' AND provedor <> 'simulado' AND criado > DATEADD(hour, -24, GETDATE()) THEN valor ELSE 0 END) AS r1,
                    SUM(CASE WHEN status = 'entregue' AND provedor <> 'simulado' AND criado > DATEADD(day, -7, GETDATE()) THEN valor ELSE 0 END) AS r7,
                    SUM(CASE WHEN status = 'entregue' AND provedor <> 'simulado' AND criado > DATEADD(day, -30, GETDATE()) THEN valor ELSE 0 END) AS r30,
                    SUM(CASE WHEN status = 'pendente' THEN 1 ELSE 0 END) AS pend,
                    SUM(CASE WHEN status = 'falhou' THEN 1 ELSE 0 END) AS prob
                FROM dbo.MUCHILA_PEDIDOS").Rows[0];
            r1 = Dec(p["r1"]); r7 = Dec(p["r7"]); r30 = Dec(p["r30"]); pend = Int(p["pend"]); prob = Int(p["prob"]);
        }
        int cashLoja = 0, comprasLoja = 0, cashZen = 0, msgs = 0, anuncios = 0;
        if (Db.TableExists("MUCHILA_LOJAITENS_COMPRAS"))
        {
            var l = Db.Query("SELECT ISNULL(SUM(preco), 0) AS c, COUNT(*) AS n FROM dbo.MUCHILA_LOJAITENS_COMPRAS WHERE criado > DATEADD(day, -30, GETDATE())").Rows[0];
            cashLoja = Int(l["c"]); comprasLoja = Int(l["n"]);
        }
        if (Db.TableExists("MUCHILA_ZEN_COMPRAS"))
            cashZen = Int(Db.Scalar("SELECT ISNULL(SUM(cash), 0) FROM dbo.MUCHILA_ZEN_COMPRAS WHERE criado > DATEADD(day, -30, GETDATE())"));
        if (Db.TableExists("MUCHILA_CONTATO"))
            msgs = Int(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_CONTATO WHERE status = 'nova'"));
        if (Db.TableExists("MUCHILA_MERCADO_ANUNCIOS"))
        {
            prob += Db.TableExists("MUCHILA_MERCADO_PEDIDOS") ? Int(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_MERCADO_PEDIDOS WHERE status = 'falhou'")) : 0;
            anuncios = Int(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_MERCADO_ANUNCIOS WHERE status = 'ativo'"));
        }

        return new ResumoDashboard
        {
            Online = online.Values.Sum(), OnlinePorServidor = online,
            Contas = Int(contas["total"]), ContasNovas24h = Int(contas["d1"]), ContasNovas7d = Int(contas["d7"]), ContasNovas30d = Int(contas["d30"]),
            ContasBanidas = Int(contas["ban"]), VipPorNivel = vip,
            Personagens = classes.Sum(c => c.Item2), PersonagensPorClasse = classes, TopPersonagens = top,
            Receita24h = r1, Receita7d = r7, Receita30d = r30, PedidosPendentes = pend, PedidosComProblema = prob,
            CashLojaItens30d = cashLoja, ComprasLojaItens30d = comprasLoja, CashZen30d = cashZen, MensagensNovas = msgs, AnunciosMercado = anuncios,
            ContasPorDia = serie,
        };
    }
}

/// <summary>Jogadores online a cada 5 minutos (MUCHILA_ADMIN_METRICAS), para o gráfico do dashboard. Guarda 90 dias.</summary>
public static class Metricas
{
    public static void Registrar()
    {
        Esquema.Garantir();
        var por = Dashboard.OnlinePorServidor();
        var t = Db.Query(@"SELECT (SELECT COUNT(*) FROM MEMB_INFO) AS contas, (SELECT COUNT(*) FROM Character) AS pers,
                (SELECT COUNT(*) FROM MEMB_INFO WHERE AccountLevel > 0 AND AccountExpireDate > GETDATE()) AS vip").Rows[0];
        var agora = DateTime.Now;
        var quando = new DateTime(agora.Year, agora.Month, agora.Day, agora.Hour, agora.Minute / 5 * 5, 0);
        Db.Execute(@"IF NOT EXISTS (SELECT 1 FROM dbo.MUCHILA_ADMIN_METRICAS WHERE quando = @q)
                     INSERT INTO dbo.MUCHILA_ADMIN_METRICAS (quando, online, por_servidor, contas, personagens, vip) VALUES (@q, @o, @p, @c, @pe, @v);
                     DELETE FROM dbo.MUCHILA_ADMIN_METRICAS WHERE quando < DATEADD(day, -90, SYSDATETIME());",
            ("@q", quando), ("@o", por.Values.Sum()), ("@p", JsonSerializer.Serialize(por)), ("@c", Convert.ToInt32(t["contas"])),
            ("@pe", Convert.ToInt32(t["pers"])), ("@v", Convert.ToInt32(t["vip"])));
    }

    /// <summary>Pontos (hora, online) das últimas N horas; buracos (painel desligado) ficam de fora.</summary>
    public static List<(DateTime Quando, int Online)> Online(int horas = 24)
    {
        Esquema.Garantir();
        return Db.Query("SELECT quando, online FROM dbo.MUCHILA_ADMIN_METRICAS WHERE quando > DATEADD(hour, -@h, SYSDATETIME()) ORDER BY quando", ("@h", horas))
            .Rows.Cast<DataRow>().Select(r => ((DateTime)r["quando"], Convert.ToInt32(r["online"]))).ToList();
    }

    /// <summary>Pico de jogadores por dia (últimos N dias).</summary>
    public static List<(DateTime Dia, int Pico, double Media)> PicoPorDia(int dias = 30)
    {
        Esquema.Garantir();
        return Db.Query(@"SELECT CAST(quando AS date) AS d, MAX(online) AS pico, AVG(CAST(online AS float)) AS media FROM dbo.MUCHILA_ADMIN_METRICAS
                          WHERE quando > DATEADD(day, -@d, SYSDATETIME()) GROUP BY CAST(quando AS date) ORDER BY d", ("@d", dias))
            .Rows.Cast<DataRow>().Select(r => ((DateTime)r["d"], Convert.ToInt32(r["pico"]), Convert.ToDouble(r["media"]))).ToList();
    }
}

using System.Data;

namespace MuChilaAdmin.Core.Painel.Relatorios;

/// <summary>Período e filtro de texto de um relatório.</summary>
public sealed class ParametrosRelatorio
{
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
    public string? Texto { get; set; }
}

/// <summary>Um relatório da tela Relatórios: quem pode tirar e como monta.</summary>
public sealed record DefinicaoRelatorio(string Id, string Nome, string Descricao, string PapelMinimo, bool UsaPeriodo, Func<ParametrosRelatorio, Relatorio> Gerar);

/// <summary>
/// Relatórios prontos (CSV e PDF). Os de dinheiro e de contas pedem moderador; auditoria só administrador. Tabelas do site
/// que não existam viram relatório vazio com a explicação no subtítulo.
/// </summary>
public static class Catalogo
{
    static string Periodo(ParametrosRelatorio p) =>
        p.De == null && p.Ate == null ? "todo o período" : $"de {(p.De is { } d ? d.ToString("dd/MM/yyyy") : "início")} até {(p.Ate is { } a ? a.AddDays(-1).ToString("dd/MM/yyyy") : "hoje")}";

    static (string Where, List<(string, object?)> Pars) Datas(ParametrosRelatorio p, string coluna, string? extra = null)
    {
        var w = new List<string>(); var pars = new List<(string, object?)>();
        if (p.De is { } de) { w.Add($"{coluna} >= @de"); pars.Add(("@de", de)); }
        if (p.Ate is { } ate) { w.Add($"{coluna} < @ate"); pars.Add(("@ate", ate)); }
        if (extra != null) w.Add(extra);
        return (w.Count == 0 ? "" : "WHERE " + string.Join(" AND ", w), pars);
    }

    static Relatorio Vazio(string titulo, string motivo) => new() { Titulo = titulo, Subtitulo = motivo, Colunas = new() { new("Aviso") }, Linhas = new() { new[] { motivo } } };

    static Relatorio ComTotal(Relatorio r, Func<List<string[]>, string[]?> total) => new()
    {
        Titulo = r.Titulo, Subtitulo = r.Subtitulo, Colunas = r.Colunas, Linhas = r.Linhas, Rodape = r.Linhas.Count > 0 ? total(r.Linhas) : null,
    };

    static decimal Soma(List<string[]> linhas, int col) =>
        linhas.Sum(l => decimal.TryParse(l[col], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out var v) ? v : 0);

    public static readonly List<DefinicaoRelatorio> Todos = new()
    {
        new("contas", "Contas", "Todas as contas com plano VIP, validade, ban, data de criação e personagens.", Papeis.Moderador, false, p =>
        {
            var filtro = string.IsNullOrWhiteSpace(p.Texto) ? "" : "WHERE m.memb___id LIKE @t";
            var t = Db.Query($@"SELECT m.memb___id AS Conta,
                       CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE() THEN 'VIP ' + CAST(m.AccountLevel AS varchar) ELSE 'Free' END AS Plano,
                       CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE() THEN m.AccountExpireDate END AS [VIP até],
                       CASE WHEN m.bloc_code = '1' THEN 'sim' ELSE '' END AS Banida,
                       m.appl_days AS Criada,
                       (SELECT COUNT(*) FROM Character c WHERE c.AccountID = m.memb___id) AS Personagens,
                       ISNULL((SELECT STRING_AGG(c.Name, ', ') FROM Character c WHERE c.AccountID = m.memb___id), '') AS Nomes
                FROM MEMB_INFO m {filtro} ORDER BY m.memb___id", string.IsNullOrWhiteSpace(p.Texto) ? Array.Empty<(string, object?)>() : new[] { ("@t", (object?)("%" + AuditoriaSql.Like(p.Texto.Trim()) + "%")) });
            return Relatorio.DeTabela("Contas", t, $"{t.Rows.Count:N0} conta(s)".Replace(',', '.'));
        }),

        new("personagens", "Personagens", "Personagens com classe, nível, master level e resets (os mais fortes primeiro).", Papeis.Leitura, false, p =>
        {
            var t = Db.Query(@"SELECT c.Name AS Personagem, c.AccountID AS Conta, c.Class AS _classe, c.cLevel AS Nível, ISNULL(m.MasterLevel, 0) AS Master,
                       ISNULL(c.ResetCount, 0) AS Resets, ISNULL(c.MasterResetCount, 0) AS [M. resets], ISNULL(c.SupremeResetCount, 0) AS [S. resets]
                FROM Character c LEFT JOIN MasterSkillTree m ON m.Name = c.Name
                ORDER BY ISNULL(c.SupremeResetCount, 0) DESC, ISNULL(c.MasterResetCount, 0) DESC, ISNULL(c.ResetCount, 0) DESC, c.cLevel + ISNULL(m.MasterLevel, 0) DESC");
            var r = Relatorio.DeTabela("Personagens", t, $"{t.Rows.Count:N0} personagem(ns)".Replace(',', '.'));
            r.Colunas.Insert(2, new ColunaRelatorio("Classe"));
            for (int i = 0; i < r.Linhas.Count; i++)
            {
                var l = r.Linhas[i].ToList();
                l.Insert(2, Dashboard.Familia(Convert.ToInt32(t.Rows[i]["_classe"])));
                r.Linhas[i] = l.ToArray();
            }
            return r;
        }),

        new("bans", "Bans", "Histórico de bans feitos pelo painel: motivo, prazo, quem baniu e quem desbaniu.", Papeis.Moderador, true, p =>
        {
            var (w, pars) = Datas(p, "inicio");
            var t = Db.Query($@"SELECT conta AS Conta, motivo AS Motivo, inicio AS Início, ISNULL(CONVERT(varchar(16), fim, 103) + ' ' + CONVERT(varchar(5), fim, 108), 'permanente') AS Prazo,
                       por AS [Banido por], CASE WHEN ativo = 1 THEN 'ativo' ELSE 'encerrado' END AS Situação, encerrado_em AS [Encerrado em],
                       ISNULL(encerrado_por, '') AS [Encerrado por], ISNULL(motivo_encerramento, '') AS [Motivo do fim]
                FROM dbo.MUCHILA_ADMIN_BANS {w} ORDER BY id DESC", pars.ToArray());
            return Relatorio.DeTabela("Bans", t, Periodo(p));
        }),

        new("pedidos", "Vendas: VIP, Cash e Passe", "Pedidos da loja do site (PIX): tipo, valor, situação e entrega.", Papeis.Moderador, true, p =>
        {
            if (!Db.TableExists("MUCHILA_PEDIDOS")) return Vazio("Vendas: VIP, Cash e Passe", "A tabela de pedidos do site ainda não existe neste banco.");
            var (w, pars) = Datas(p, "criado");
            var t = Db.Query($@"SELECT id AS Pedido, criado AS Data, conta AS Conta, tipo AS Tipo, descricao AS Descrição, valor AS [Valor (R$)], status AS Situação,
                       provedor AS Provedor, entregue_em AS Entregue FROM dbo.MUCHILA_PEDIDOS {w} ORDER BY id DESC", pars.ToArray());
            var r = Relatorio.DeTabela("Vendas: VIP, Cash e Passe", t, $"{Periodo(p)} · pedidos de teste (provedor simulado) aparecem, mas não somam");
            int col = r.Colunas.FindIndex(c => c.Titulo == "Valor (R$)"), sit = r.Colunas.FindIndex(c => c.Titulo == "Situação"), prov = r.Colunas.FindIndex(c => c.Titulo == "Provedor");
            return ComTotal(r, ls =>
            {
                var reais = ls.Where(l => l[sit] == "entregue" && l[prov] != "simulado").ToList();
                var tot = new string[r.Colunas.Count]; tot[0] = "Total entregue"; tot[col] = Soma(reais, col).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
                return tot;
            });
        }),

        new("lojaitens", "Vendas: loja de itens", "Itens comprados com Cash na loja de itens do site.", Papeis.Moderador, true, p =>
        {
            if (!Db.TableExists("MUCHILA_LOJAITENS_COMPRAS")) return Vazio("Vendas: loja de itens", "A tabela da loja de itens ainda não existe neste banco.");
            var (w, pars) = Datas(p, "criado");
            var t = Db.Query($@"SELECT id AS Compra, criado AS Data, conta AS Conta, descricao AS Item, preco AS [Cash] FROM dbo.MUCHILA_LOJAITENS_COMPRAS {w} ORDER BY id DESC", pars.ToArray());
            var r = Relatorio.DeTabela("Vendas: loja de itens", t, Periodo(p));
            return ComTotal(r, ls => { var tot = new string[r.Colunas.Count]; tot[0] = "Total"; tot[^1] = Soma(ls, r.Colunas.Count - 1).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")); return tot; });
        }),

        new("zen", "Vendas: Zen por Cash", "Compras de Zen pelo site (Cash → Zen no baú).", Papeis.Moderador, true, p =>
        {
            if (!Db.TableExists("MUCHILA_ZEN_COMPRAS")) return Vazio("Vendas: Zen por Cash", "A tabela de compras de Zen ainda não existe neste banco.");
            var (w, pars) = Datas(p, "criado");
            var t = Db.Query($@"SELECT id AS Compra, criado AS Data, conta AS Conta, pacote AS Pacote, zen AS Zen, cash AS Cash FROM dbo.MUCHILA_ZEN_COMPRAS {w} ORDER BY id DESC", pars.ToArray());
            var r = Relatorio.DeTabela("Vendas: Zen por Cash", t, Periodo(p));
            return ComTotal(r, ls => { var tot = new string[r.Colunas.Count]; tot[0] = "Total"; tot[^1] = Soma(ls, r.Colunas.Count - 1).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")); return tot; });
        }),

        new("mercado", "Mercado entre jogadores", "Compras do mercado: anúncio, comprador, vendedor, valor, taxa da loja e situação.", Papeis.Moderador, true, p =>
        {
            if (!Db.TableExists("MUCHILA_MERCADO_PEDIDOS")) return Vazio("Mercado entre jogadores", "As tabelas do mercado ainda não existem neste banco.");
            var (w, pars) = Datas(p, "criado");
            var t = Db.Query($@"SELECT id AS Pedido, criado AS Data, comprador AS Comprador, vendedor AS Vendedor, descricao AS Anúncio,
                       valor AS [Valor (R$)], taxa AS [Taxa (R$)], status AS Situação, provedor AS Provedor
                FROM dbo.MUCHILA_MERCADO_PEDIDOS {w} ORDER BY id DESC", pars.ToArray());
            return Relatorio.DeTabela("Mercado entre jogadores", t, Periodo(p));
        }),

        new("online", "Jogadores online por dia", "Pico e média de jogadores conectados por dia (amostras do painel a cada 5 minutos).", Papeis.Leitura, true, p =>
        {
            var (w, pars) = Datas(p, "quando");
            var t = Db.Query($@"SELECT CAST(quando AS date) AS Dia, MAX(online) AS Pico, CAST(AVG(CAST(online AS float)) AS decimal(10,1)) AS Média, COUNT(*) AS Amostras
                FROM dbo.MUCHILA_ADMIN_METRICAS {w} GROUP BY CAST(quando AS date) ORDER BY Dia DESC", pars.ToArray());
            return Relatorio.DeTabela("Jogadores online por dia", t, Periodo(p) + " · dias com o painel desligado não aparecem");
        }),

        new("mensagens", "Mensagens do Contate-nos", "Mensagens enviadas pelo site, com a situação e a resposta.", Papeis.Moderador, true, p =>
        {
            if (!Db.TableExists("MUCHILA_CONTATO")) return Vazio("Mensagens do Contate-nos", "A tabela de mensagens do site ainda não existe neste banco.");
            var (w, pars) = Datas(p, "criado");
            var t = Db.Query($@"SELECT id AS Nº, criado AS Data, ISNULL(conta, '(visitante)') AS Conta, ISNULL(contato, '') AS Contato, assunto AS Assunto,
                       status AS Situação, ISNULL(resposta, '') AS Resposta FROM dbo.MUCHILA_CONTATO {w} ORDER BY id DESC", pars.ToArray());
            return Relatorio.DeTabela("Mensagens do Contate-nos", t, Periodo(p));
        }),

        new("auditoria", "Auditoria do painel", "Tudo o que foi feito no painel: quem, quando, de onde e o resultado.", Papeis.Admin, true, p =>
        {
            var aud = new AuditoriaSql();
            var (itens, total) = aud.Buscar(new FiltroAuditoria { De = p.De, Ate = p.Ate, Texto = p.Texto, PorPagina = 5000 });
            return new Relatorio
            {
                Titulo = "Auditoria do painel", Subtitulo = $"{Periodo(p)}{(total > itens.Count ? $" · mostrando as {itens.Count} mais recentes de {total}" : "")}",
                Colunas = new() { new("Quando"), new("Usuário"), new("IP"), new("Ação"), new("Alvo"), new("Resultado"), new("Mensagem") },
                Linhas = itens.Select(e => new[] { e.Quando.ToString("dd/MM/yyyy HH:mm:ss"), e.Usuario, e.Ip ?? "", Acoes.Nome(e.Acao), e.Alvo ?? "", e.Ok ? "ok" : "erro", e.Mensagem ?? "" }).ToList(),
            };
        }),
    };

    public static DefinicaoRelatorio? Por(string id) => Todos.FirstOrDefault(r => r.Id == id);
}

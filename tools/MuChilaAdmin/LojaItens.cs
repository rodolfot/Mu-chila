using System.Data;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MuChilaAdmin;

/// <summary>
/// Loja de itens do site (usercp/lojaitens, 30/09/2026): catálogo e preços em Site\...\includes\config\muchila.lojaitens.json.
/// O site lê esse arquivo a cada página (mudou aqui, vale na hora). Grava na cópia ativa (www) e na do repositório
/// (muchila\www), como o ResetConfig, preservando o que o painel não conhece (ex.: "_comentario", "item_txt").
/// Preço em Cash = preço do item + nível[0-15] + adicional[0-7] + sorte + skill + excelente[quantidade 0-6].
/// </summary>
public static class LojaItens
{
    static string Www => Path.Combine(ServerControl.ServerRoot, @"Site\www\includes\config\muchila.lojaitens.json");
    static string Overlay => Path.Combine(ServerControl.ServerRoot, @"Site\muchila\www\includes\config\muchila.lojaitens.json");
    static readonly JsonSerializerOptions Indentado = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Ícones de categoria que o site conhece (templates\muchila\img\icones.svg).</summary>
    public static readonly string[] Icones = { "helm", "armor", "pants", "gloves", "boots", "shield", "ring", "wings", "sword", "axe", "mace", "spear", "bow", "staff", "item" };
    /// <summary>"" = automático pela seção do item (o site decide: armas/pingentes = arma, armaduras/anéis = defesa, asas = nenhuma).</summary>
    public static readonly string[] TiposExc = { "", "arma", "defesa", "nenhuma" };

    public sealed class Item
    {
        public int Secao, Tipo, Preco;
        public int? NivelMax;
        public string Exc = "";
        public bool Ativo = true, Destaque;
        public string Nome => Shops.Name(Secao, Tipo);
    }

    public sealed class Categoria
    {
        public string Id = "", Nome = "", Grupo = "Defesa", Icone = "item";
        public bool Ativo = true;
        public List<Item> Itens = new();
        public override string ToString() => (Ativo ? "" : "(oculta) ") + $"{Grupo} / {Nome}  ({Itens.Count})";
    }

    public sealed class Config
    {
        public bool Ativo = true;
        public int NivelMax = 15, ExcMax = 6, Sorte, Skill;
        public int[] Nivel = new int[16], Adicional = new int[8], Excelente = new int[7];
        public List<Categoria> Categorias = new();

        /// <summary>Mesma conta do site (MuChilaLojaItens::precoDetalhado).</summary>
        public int Preco(int baseItem, int nivel, int adicional, bool sorte, bool skill, int exc) =>
            baseItem + Nivel[nivel] + Adicional[adicional] + (sorte ? Sorte : 0) + (skill ? Skill : 0) + Excelente[exc];
    }

    public static string Arquivo => File.Exists(Www) ? Www : Overlay;
    /// <summary>Os arquivos que o Salvar grava (os que existem).</summary>
    public static IEnumerable<string> Arquivos => new[] { Www, Overlay }.Where(File.Exists);

    public static Config Carregar()
    {
        if (!File.Exists(Arquivo)) throw new FileNotFoundException("Não achei o muchila.lojaitens.json (o site está instalado?).", Arquivo);
        var o = JsonNode.Parse(File.ReadAllText(Arquivo)) as JsonObject ?? throw new InvalidDataException("muchila.lojaitens.json inválido.");
        var c = new Config
        {
            Ativo = Bool(o["ativo"], true),
            NivelMax = Math.Clamp(Int(o["nivel_max"], 15), 0, 15),
            ExcMax = Math.Clamp(Int(o["exc_max"], 6), 0, 6),
        };
        var p = o["precos"] as JsonObject ?? new JsonObject();
        c.Nivel = Tabela(p["nivel"], 16);
        c.Adicional = Tabela(p["adicional"], 8);
        c.Excelente = Tabela(p["excelente"], 7);
        c.Sorte = Int(p["sorte"]);
        c.Skill = Int(p["skill"]);
        foreach (var n in o["categorias"] as JsonArray ?? new JsonArray())
        {
            if (n is not JsonObject co) continue;
            var cat = new Categoria
            {
                Id = Str(co["id"]), Nome = Str(co["nome"]), Grupo = Str(co["grupo"], "Defesa"), Icone = Str(co["icone"], "item"), Ativo = Bool(co["ativo"], true),
            };
            foreach (var ni in co["itens"] as JsonArray ?? new JsonArray())
            {
                if (ni is not JsonObject io) continue;
                cat.Itens.Add(new Item
                {
                    Secao = Int(io["secao"]), Tipo = Int(io["tipo"]), Preco = Int(io["preco"]),
                    NivelMax = io["nivel_max"] == null ? null : Int(io["nivel_max"]),
                    Exc = Str(io["exc"]), Ativo = Bool(io["ativo"], true), Destaque = Bool(io["destaque"], false),
                });
            }
            c.Categorias.Add(cat);
        }
        return c;
    }

    /// <summary>Problemas que impedem salvar (lista vazia = pode salvar).</summary>
    public static List<string> Validar(Config c)
    {
        var e = new List<string>();
        if (c.Nivel.Length != 16 || c.Adicional.Length != 8 || c.Excelente.Length != 7) e.Add("Tabelas de preço com tamanho errado.");
        if (c.Nivel.Concat(c.Adicional).Concat(c.Excelente).Append(c.Sorte).Append(c.Skill).Any(v => v < 0)) e.Add("Preços não podem ser negativos.");
        var ids = new HashSet<string>();
        foreach (var cat in c.Categorias)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(cat.Id, "^[a-z0-9_-]{1,30}$")) e.Add($"Categoria \"{cat.Nome}\": o id \"{cat.Id}\" precisa ter só letras minúsculas, números, - ou _.");
            else if (!ids.Add(cat.Id)) e.Add($"Duas categorias com o id \"{cat.Id}\".");
            if (string.IsNullOrWhiteSpace(cat.Nome)) e.Add($"Categoria \"{cat.Id}\" sem nome.");
            if (!Icones.Contains(cat.Icone)) e.Add($"Categoria \"{cat.Nome}\": ícone \"{cat.Icone}\" não existe.");
            var vistos = new HashSet<(int, int)>();
            foreach (var it in cat.Itens)
            {
                if (Shops.Find(it.Secao, it.Tipo) == null) e.Add($"Categoria \"{cat.Nome}\": o item {it.Secao},{it.Tipo} não existe no Item.txt.");
                if (!vistos.Add((it.Secao, it.Tipo))) e.Add($"Categoria \"{cat.Nome}\": {it.Nome} aparece duas vezes.");
                if (it.Preco < 0) e.Add($"{it.Nome}: preço negativo.");
                if (it.NivelMax is < 0 or > 15) e.Add($"{it.Nome}: nível máximo vai de 0 a 15.");
                if (!TiposExc.Contains(it.Exc)) e.Add($"{it.Nome}: tipo de excelente \"{it.Exc}\" inválido.");
            }
        }
        return e;
    }

    /// <summary>Grava nos dois arquivos (o que existir), com backup .bak-data do que estava lá.</summary>
    public static string Salvar(Config c)
    {
        var erros = Validar(c);
        if (erros.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, erros));
        int gravados = 0;
        foreach (var f in new[] { Www, Overlay })
        {
            if (!File.Exists(f)) continue;
            var o = (JsonNode.Parse(File.ReadAllText(f)) as JsonObject) ?? new JsonObject();
            o["ativo"] = c.Ativo;
            o["nivel_max"] = c.NivelMax;
            o["exc_max"] = c.ExcMax;
            var p = o["precos"] as JsonObject ?? new JsonObject();
            p["nivel"] = new JsonArray(c.Nivel.Select(v => (JsonNode)v).ToArray());
            p["adicional"] = new JsonArray(c.Adicional.Select(v => (JsonNode)v).ToArray());
            p["sorte"] = c.Sorte;
            p["skill"] = c.Skill;
            p["excelente"] = new JsonArray(c.Excelente.Select(v => (JsonNode)v).ToArray());
            o["precos"] = p;
            var cats = new JsonArray();
            foreach (var cat in c.Categorias)
            {
                var co = new JsonObject { ["id"] = cat.Id, ["nome"] = cat.Nome, ["grupo"] = cat.Grupo, ["icone"] = cat.Icone };
                if (!cat.Ativo) co["ativo"] = false;
                var itens = new JsonArray();
                foreach (var it in cat.Itens)
                {
                    var io = new JsonObject { ["secao"] = it.Secao, ["tipo"] = it.Tipo, ["nome"] = it.Nome, ["preco"] = it.Preco };
                    if (it.NivelMax != null) io["nivel_max"] = it.NivelMax;
                    if (it.Exc != "") io["exc"] = it.Exc;
                    if (it.Destaque) io["destaque"] = true;
                    if (!it.Ativo) io["ativo"] = false;
                    itens.Add(io);
                }
                co["itens"] = itens;
                cats.Add(co);
            }
            o["categorias"] = cats;
            File.Copy(f, $"{f}.bak-{DateTime.Now:yyyyMMdd-HHmmss}", true);
            File.WriteAllText(f, o.ToJsonString(Indentado) + "\n");
            gravados++;
        }
        if (gravados == 0) throw new FileNotFoundException("Não achei o muchila.lojaitens.json (o site está instalado?).");
        int itensAtivos = c.Categorias.Where(x => x.Ativo).Sum(x => x.Itens.Count(i => i.Ativo));
        return $"Loja de itens salva ({gravados} arquivo(s)): {(c.Ativo ? "aberta" : "FECHADA")}, {c.Categorias.Count} categorias, {itensAtivos} itens à venda. O site já usa os valores novos.";
    }

    /// <summary>Últimas compras (tabela MUCHILA_LOJAITENS_COMPRAS, criada pelo Instalar-Modulos.ps1 do site).</summary>
    public static DataTable Compras(int quantas = 200) => Db.Query(@"
        SELECT TOP (@n) id AS Compra, criado AS Data, conta AS Conta, descricao AS Item, preco AS Cash, posicao AS [Posição no baú], serial AS Serial
        FROM MUCHILA_LOJAITENS_COMPRAS ORDER BY id DESC", ("@n", quantas));

    /// <summary>Total de Cash gasto na loja em 1, 7 e 30 dias.</summary>
    public static (int Dia, int Semana, int Mes) Totais()
    {
        var t = Db.Query(@"SELECT ISNULL(SUM(CASE WHEN criado > DATEADD(day, -1, GETDATE()) THEN preco END), 0),
                                  ISNULL(SUM(CASE WHEN criado > DATEADD(day, -7, GETDATE()) THEN preco END), 0),
                                  ISNULL(SUM(preco), 0)
                           FROM MUCHILA_LOJAITENS_COMPRAS WHERE criado > DATEADD(day, -30, GETDATE())");
        return (Convert.ToInt32(t.Rows[0][0]), Convert.ToInt32(t.Rows[0][1]), Convert.ToInt32(t.Rows[0][2]));
    }

    static int Int(JsonNode? n, int padrao = 0) { try { return n?.GetValue<int>() ?? padrao; } catch { return int.TryParse(n?.ToString(), out var v) ? v : padrao; } }
    static bool Bool(JsonNode? n, bool padrao) { try { return n?.GetValue<bool>() ?? padrao; } catch { return padrao; } }
    static string Str(JsonNode? n, string padrao = "") { try { return n?.GetValue<string>() ?? padrao; } catch { return padrao; } }
    static int[] Tabela(JsonNode? n, int tamanho)
    {
        var v = (n as JsonArray ?? new JsonArray()).Select(x => Int(x)).Take(tamanho).ToList();
        while (v.Count < tamanho) v.Add(v.Count > 0 ? v[^1] : 0);
        return v.ToArray();
    }
}

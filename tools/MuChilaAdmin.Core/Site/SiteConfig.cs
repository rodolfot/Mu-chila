using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MuChilaAdmin.Core;

/// <summary>
/// Aba "Site" (01/10/2026): notícias, páginas de texto (Termos, Privacidade, Reembolso, Contate-nos), pacotes do Comprar Zen e
/// as mensagens do Contate-nos.
/// - Páginas e Zen: muchila.paginas.json e muchila.zen.json em Site\...\includes\config (cópia ativa e do repositório, como o
///   ResetConfig), preservando o que o painel não conhece; o site lê a cada página, então vale na hora.
/// - Notícias: pelo próprio site (Site\www\includes\muchila\noticias-cli.php com o PHP do site), que grava na WEBENGINE_NEWS e
///   refaz o cache do WebEngine igual ao painel admin dele.
/// - Mensagens: tabela MUCHILA_CONTATO (criada pelo Instalar-Modulos.ps1 do site).
/// </summary>
public static class SiteConfig
{
    static readonly JsonSerializerOptions Indentado = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static string Www(string nome) => Path.Combine(ServerControl.ServerRoot, @"Site\www\includes\config", nome);
    static string Overlay(string nome) => Path.Combine(ServerControl.ServerRoot, @"Site\muchila\www\includes\config", nome);
    public static IEnumerable<string> Arquivos(string nome) => new[] { Www(nome), Overlay(nome) }.Where(File.Exists);

    static JsonObject LerJson(string nome)
    {
        var f = File.Exists(Www(nome)) ? Www(nome) : Overlay(nome);
        if (!File.Exists(f)) throw new FileNotFoundException($"Não achei o {nome} (o site está instalado e atualizado?).", f);
        return JsonNode.Parse(File.ReadAllText(f)) as JsonObject ?? throw new InvalidDataException($"{nome} inválido.");
    }

    /// <summary>Aplica a mudança em cada cópia do arquivo (o resto do JSON fica como estava), com backup .bak-data.</summary>
    static int GravarJson(string nome, Action<JsonObject> mudar)
    {
        int n = 0;
        foreach (var f in Arquivos(nome).ToList())
        {
            var o = (JsonNode.Parse(File.ReadAllText(f)) as JsonObject) ?? new JsonObject();
            mudar(o);
            File.Copy(f, $"{f}.bak-{DateTime.Now:yyyyMMdd-HHmmss}", true);
            File.WriteAllText(f, o.ToJsonString(Indentado) + "\n");
            n++;
        }
        if (n == 0) throw new FileNotFoundException($"Não achei o {nome} (o site está instalado e atualizado?).");
        return n;
    }

    static string Str(JsonNode? n, string padrao = "") { try { return n?.GetValue<string>() ?? padrao; } catch { return n?.ToString() ?? padrao; } }
    static bool Bool(JsonNode? n, bool padrao) { try { return n?.GetValue<bool>() ?? padrao; } catch { return padrao; } }
    static long Long(JsonNode? n) { try { return n?.GetValue<long>() ?? 0; } catch { return long.TryParse(n?.ToString(), out var v) ? v : 0; } }

    // ================================================================ páginas de texto e Contate-nos
    public static readonly (string Id, string Nome)[] Paginas = { ("tos", "Termos de Serviço"), ("privacy", "Política de Privacidade"), ("refunds", "Política de Reembolso") };
    public static readonly string[] TiposCanal = { "discord", "whatsapp", "mail", "instagram", "facebook" };

    public sealed class Pagina { public string Titulo = "", Atualizado = "", Html = ""; }
    public sealed class Canal { public string Tipo = "discord", Rotulo = "", Texto = "", Link = ""; }
    public sealed class Contato { public string Texto = ""; public bool Formulario = true; public List<Canal> Canais = new(); }

    public static (Dictionary<string, Pagina> Paginas, Contato Contato) CarregarPaginas()
    {
        var o = LerJson("muchila.paginas.json");
        var paginas = new Dictionary<string, Pagina>();
        foreach (var (id, nome) in Paginas)
        {
            var p = o["paginas"]?[id] as JsonObject;
            paginas[id] = new Pagina { Titulo = Str(p?["titulo"], nome), Atualizado = Str(p?["atualizado"]), Html = Str(p?["html"]) };
        }
        var c = o["contato"] as JsonObject;
        var contato = new Contato { Texto = Str(c?["texto"]), Formulario = Bool(c?["formulario"], true) };
        foreach (var n in c?["canais"] as JsonArray ?? new JsonArray())
            if (n is JsonObject x) contato.Canais.Add(new Canal { Tipo = Str(x["tipo"], "discord"), Rotulo = Str(x["rotulo"]), Texto = Str(x["texto"]), Link = Str(x["link"]) });
        return (paginas, contato);
    }

    public static string SalvarPaginas(Dictionary<string, Pagina> paginas, Contato contato)
    {
        foreach (var c in contato.Canais)
            if (c.Link.Length > 0 && !c.Link.StartsWith("http://") && !c.Link.StartsWith("https://") && !c.Link.StartsWith("mailto:"))
                throw new InvalidOperationException($"Canal \"{c.Rotulo}\": o link precisa começar com https://, http:// ou mailto: (ex.: https://wa.me/5511999999999).");
        int n = GravarJson("muchila.paginas.json", o =>
        {
            var p = o["paginas"] as JsonObject ?? new JsonObject();
            foreach (var (id, pg) in paginas) p[id] = new JsonObject { ["titulo"] = pg.Titulo.Trim(), ["atualizado"] = pg.Atualizado.Trim(), ["html"] = pg.Html.Trim() };
            o["paginas"] = p;
            o["contato"] = new JsonObject
            {
                ["texto"] = contato.Texto.Trim(), ["formulario"] = contato.Formulario,
                ["canais"] = new JsonArray(contato.Canais.Select(c => (JsonNode)new JsonObject
                    { ["tipo"] = c.Tipo, ["rotulo"] = c.Rotulo.Trim(), ["texto"] = c.Texto.Trim(), ["link"] = c.Link.Trim() }).ToArray()),
            };
        });
        return $"Páginas do site salvas ({n} arquivo(s)). O site já mostra os textos novos.";
    }

    // ================================================================ Comprar Zen
    public const long ZenMaximo = 2_000_000_000;
    public sealed class PacoteZen { public string Id = ""; public long Zen; public int Cash; public bool Ativo = true; }

    public static (bool Ativo, List<PacoteZen> Pacotes) CarregarZen()
    {
        var o = LerJson("muchila.zen.json");
        var lista = new List<PacoteZen>();
        foreach (var n in o["pacotes"] as JsonArray ?? new JsonArray())
            if (n is JsonObject x) lista.Add(new PacoteZen { Id = Str(x["id"]), Zen = Long(x["zen"]), Cash = (int)Long(x["cash"]), Ativo = Bool(x["ativo"], true) });
        return (Bool(o["ativo"], true), lista);
    }

    public static List<string> ValidarZen(IReadOnlyList<PacoteZen> pacotes)
    {
        var e = new List<string>(); var ids = new HashSet<string>();
        foreach (var p in pacotes)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(p.Id, "^[a-z0-9_-]{1,30}$")) e.Add($"Pacote \"{p.Id}\": o id precisa ter só letras minúsculas, números, - ou _ (ex.: zen-500kk).");
            else if (!ids.Add(p.Id)) e.Add($"Dois pacotes com o id \"{p.Id}\".");
            if (p.Zen < 1 || p.Zen > ZenMaximo) e.Add($"Pacote \"{p.Id}\": o Zen vai de 1 a 2.000.000.000 (limite do baú).");
            if (p.Cash < 1) e.Add($"Pacote \"{p.Id}\": o preço precisa ser de pelo menos 1 Cash.");
        }
        return e;
    }

    public static string SalvarZen(bool ativo, IReadOnlyList<PacoteZen> pacotes)
    {
        var e = ValidarZen(pacotes);
        if (e.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, e));
        int n = GravarJson("muchila.zen.json", o =>
        {
            o["ativo"] = ativo;
            o["pacotes"] = new JsonArray(pacotes.Select(p =>
            {
                var x = new JsonObject { ["id"] = p.Id, ["zen"] = p.Zen, ["cash"] = p.Cash };
                if (!p.Ativo) x["ativo"] = false;
                return (JsonNode)x;
            }).ToArray());
        });
        return $"Comprar Zen salvo ({n} arquivo(s)): {(ativo ? "aberto" : "FECHADO")}, {pacotes.Count(p => p.Ativo)} pacote(s) à venda. O site já usa os valores novos.";
    }

    public static DataTable ComprasZen(int quantas = 200) => Db.Query(@"
        SELECT TOP (@n) id AS Compra, criado AS Data, conta AS Conta, pacote AS Pacote, zen AS Zen, cash AS Cash, zen_antes AS [Zen antes no baú]
        FROM MUCHILA_ZEN_COMPRAS ORDER BY id DESC", ("@n", quantas));

    // ================================================================ mensagens do Contate-nos
    public static readonly (string Id, string Nome)[] FiltrosMensagem = { ("abertas", "A responder (novas e lidas)"), ("novas", "Só as novas"), ("respondidas", "Respondidas"), ("todas", "Todas") };

    public static DataTable Mensagens(string filtro)
    {
        var onde = filtro switch
        {
            "novas" => "WHERE status = 'nova'",
            "abertas" => "WHERE status IN ('nova', 'lida')",
            "respondidas" => "WHERE status = 'respondida'",
            _ => "",
        };
        return Db.Query($@"SELECT TOP 300 id AS Id, criado AS Data, ISNULL(conta, '(visitante)') AS Conta, ISNULL(contato, '') AS Contato,
                assunto AS Assunto, status AS Situação, mensagem AS Mensagem, ISNULL(resposta, '') AS Resposta
            FROM MUCHILA_CONTATO {onde} ORDER BY id DESC");
    }

    public static int MensagensNovas() => Convert.ToInt32(Db.Query("SELECT COUNT(*) FROM MUCHILA_CONTATO WHERE status = 'nova'").Rows[0][0]);

    public static void MarcarMensagem(int id, string status) =>
        Db.Execute("UPDATE MUCHILA_CONTATO SET status = @s WHERE id = @id", ("@s", status), ("@id", id));

    public static void ResponderMensagem(int id, string resposta)
    {
        if (string.IsNullOrWhiteSpace(resposta)) throw new InvalidOperationException("Escreva a resposta.");
        if (resposta.Length > 2000) throw new InvalidOperationException("A resposta pode ter no máximo 2.000 caracteres.");
        Db.Execute("UPDATE MUCHILA_CONTATO SET resposta = @r, status = 'respondida', respondido_em = GETDATE() WHERE id = @id", ("@r", resposta.Trim()), ("@id", id));
    }

    // ================================================================ notícias (pelo PHP do site)
    public sealed record Noticia(int Id, string Titulo, string Autor, string Data, bool Comentarios, string Conteudo = "");

    static string PhpExe => Path.Combine(ServerControl.ServerRoot, @"Site\php\php.exe");
    static string NoticiasCli => Path.Combine(ServerControl.ServerRoot, @"Site\www\includes\muchila\noticias-cli.php");

    /// <summary>Roda o noticias-cli.php e devolve o JSON da resposta (erro se ok = false).</summary>
    static JsonObject Php(params string[] args)
    {
        if (!File.Exists(PhpExe)) throw new FileNotFoundException($"PHP do site não encontrado em {PhpExe}. As notícias passam pelo PHP do site (no PC de desenvolvimento, rode o Preparar-Ambiente-Dev.ps1 com -Php).", PhpExe);
        if (!File.Exists(NoticiasCli)) throw new FileNotFoundException("noticias-cli.php não encontrado: rode o Instalar-Modulos.ps1 do site.", NoticiasCli);
        var psi = new ProcessStartInfo(PhpExe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(NoticiasCli)! };
        psi.ArgumentList.Add(NoticiasCli);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var saida = p.StandardOutput.ReadToEnd();
        var erro = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } throw new TimeoutException("O PHP do site não respondeu em 60 s."); }
        JsonObject? o = null;
        try { o = JsonNode.Parse(saida.Trim()) as JsonObject; } catch { }
        if (o == null) throw new InvalidOperationException("Resposta inesperada do site: " + (saida + " " + erro).Trim());
        if (o["ok"]?.GetValue<bool>() != true) throw new InvalidOperationException(Str(o["erro"], "erro desconhecido"));
        return o;
    }

    public static List<Noticia> Noticias() =>
        (Php("listar")["noticias"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
            .Select(n => new Noticia((int)Long(n["id"]), Str(n["titulo"]), Str(n["autor"]), Str(n["data"]), Long(n["comentarios"]) == 1)).ToList();

    public static Noticia ObterNoticia(int id)
    {
        var n = Php("obter", id.ToString())["noticia"] as JsonObject ?? throw new InvalidOperationException("Notícia não encontrada.");
        return new Noticia((int)Long(n["id"]), Str(n["titulo"]), Str(n["autor"]), Str(n["data"]), Long(n["comentarios"]) == 1, Str(n["conteudo"]));
    }

    /// <summary>Cria (id null) ou altera a notícia. Devolve o id.</summary>
    public static int SalvarNoticia(int? id, string titulo, string autor, DateTime data, bool comentarios, string conteudo)
    {
        var arq = Path.Combine(Path.GetTempPath(), $"muchila-noticia-{Guid.NewGuid():N}.json");
        var o = new JsonObject { ["titulo"] = titulo.Trim(), ["autor"] = autor.Trim(), ["conteudo"] = conteudo, ["comentarios"] = comentarios ? 1 : 0, ["data"] = data.ToString("yyyy-MM-dd HH:mm") };
        if (id != null) o["id"] = id.Value;
        File.WriteAllText(arq, o.ToJsonString(), new UTF8Encoding(false));
        try { return (int)Long(Php("salvar", arq)["id"]); }
        finally { try { File.Delete(arq); } catch { } }
    }

    public static void ApagarNoticia(int id) => Php("apagar", id.ToString());
}

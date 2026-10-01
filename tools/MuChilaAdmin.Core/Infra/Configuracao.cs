using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuChilaAdmin.Core;

/// <summary>
/// Configuração do Mu Chila Admin: banco, pastas do servidor e do cliente, endereço do painel web. O mesmo programa roda
/// no PC do servidor (produção, valores padrão) e no PC de quem desenvolve (banco local e cópia da pasta do servidor).
///
/// Onde procura o arquivo muchila-admin.json, nesta ordem:
///   1. variável de ambiente MUCHILA_ADMIN_CONFIG (caminho do arquivo);
///   2. %ProgramData%\MuChilaAdmin\muchila-admin.json (fica fora de C:\MuServer: o Sincronizar-Servidor copia
///      C:\MuServer para o repositório público, e a configuração não deve ir junto);
///   3. ao lado do executável.
/// Sem arquivo, valem os padrões do PC do servidor. As variáveis MUCHILA_ROOT (pasta do servidor), MUCHILA_CLIENTE
/// (pasta do cliente) e MUCHILA_BANCO (conexão) passam por cima do arquivo: os testes usam isso para apontar para cópias.
/// Exemplo comentado: tools\MuChilaAdmin\muchila-admin.exemplo.json.
/// </summary>
public sealed class Configuracao
{
    public const string NomeArquivo = "muchila-admin.json";

    /// <summary>Conexão com o banco do jogo (MuOnlineS14). Padrão: instância .\MUONLINE com o usuário do Windows.</summary>
    public string Banco { get; set; } = @"Server=.\MUONLINE;Database=MuOnlineS14;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=5";

    /// <summary>Pasta do servidor (C:\MuServer no PC do servidor).</summary>
    public string PastaServidor { get; set; } = @"C:\MuServer";

    /// <summary>Pasta do cliente do repositório (loja de cash e itens novos gravam também no cliente).</summary>
    public string PastaCliente { get; set; } = @"C:\Projetos\MuServer-Season14\2 - Cliente Season 14 Full";

    /// <summary>Raiz do repositório (scripts como tools\Publicar-Launcher.ps1).</summary>
    public string PastaRepositorio { get; set; } = @"C:\Projetos\MuServer-Season14";

    /// <summary>"producao" ou "desenvolvimento" (o painel mostra uma faixa e não liga o vigia sozinho).</summary>
    public string Ambiente { get; set; } = "producao";

    public PainelWeb Web { get; set; } = new();

    public sealed class PainelWeb
    {
        /// <summary>Porta do painel (http://localhost:5170).</summary>
        public int Porta { get; set; } = 5170;

        /// <summary>
        /// Endereços onde o painel escuta. "localhost" = só este PC. Para abrir em outros PCs da rede do Radmin, acrescente o IP
        /// do Radmin deste PC (ex.: "26.139.39.123"); o Publicar-Painel.ps1 cria a regra do firewall só para a rede 26.x.
        /// </summary>
        public List<string> Enderecos { get; set; } = new() { "localhost" };

        /// <summary>Minutos sem usar o painel até pedir o login de novo.</summary>
        public int MinutosSessao { get; set; } = 480;

        /// <summary>Abre o navegador ao iniciar o painel pelo atalho (não vale para o modo --web, que liga com o Windows).</summary>
        public bool AbrirNavegador { get; set; } = true;
    }

    [JsonIgnore] public bool Desenvolvimento => string.Equals(Ambiente, "desenvolvimento", StringComparison.OrdinalIgnoreCase);

    /// <summary>Arquivo de onde a configuração veio (null = padrões).</summary>
    [JsonIgnore] public string? Origem { get; private set; }

    static Configuracao? atual;
    static readonly object trava = new();

    /// <summary>A configuração em uso (carregada na primeira vez que alguém pede).</summary>
    public static Configuracao Atual
    {
        get { lock (trava) return atual ??= Carregar(); }
    }

    /// <summary>Troca a configuração em uso (o painel chama ao iniciar; os testes, para usar cópias).</summary>
    public static void Definir(Configuracao c) { lock (trava) atual = c; }

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true,
    };

    public static IEnumerable<string> Candidatos()
    {
        if (Environment.GetEnvironmentVariable("MUCHILA_ADMIN_CONFIG") is { Length: > 0 } env) yield return env;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MuChilaAdmin", NomeArquivo);
        yield return Path.Combine(AppContext.BaseDirectory, NomeArquivo);
    }

    /// <summary>Lê o arquivo (o primeiro que existir, ou o indicado) e aplica as variáveis de ambiente.</summary>
    public static Configuracao Carregar(string? arquivo = null)
    {
        var caminho = arquivo ?? Candidatos().FirstOrDefault(File.Exists);
        Configuracao c;
        if (caminho != null && File.Exists(caminho))
        {
            try { c = JsonSerializer.Deserialize<Configuracao>(File.ReadAllText(caminho), Json) ?? new(); }
            catch (JsonException ex) { throw new InvalidOperationException($"{caminho}: configuração inválida ({ex.Message})", ex); }
            c.Origem = caminho;
        }
        else c = new();
        if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") is { Length: > 0 } raiz) c.PastaServidor = raiz;
        if (Environment.GetEnvironmentVariable("MUCHILA_CLIENTE") is { Length: > 0 } cliente) c.PastaCliente = cliente;
        if (Environment.GetEnvironmentVariable("MUCHILA_BANCO") is { Length: > 0 } banco) c.Banco = banco;
        c.PastaServidor = c.PastaServidor.TrimEnd('\\', '/');
        c.Web.Enderecos = c.Web.Enderecos.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (c.Web.Enderecos.Count == 0) c.Web.Enderecos.Add("localhost");
        if (c.Web.Porta is < 1 or > 65535) throw new InvalidOperationException($"Porta inválida no {NomeArquivo}: {c.Web.Porta}");
        c.Web.MinutosSessao = Math.Clamp(c.Web.MinutosSessao, 5, 7 * 24 * 60);
        return c;
    }

    public string ParaJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Endereços http:// em que o Kestrel escuta (localhost vira 127.0.0.1 e [::1]).</summary>
    public IEnumerable<string> UrlsDeEscuta()
    {
        foreach (var e in Web.Enderecos)
            yield return e.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? $"http://localhost:{Web.Porta}" : $"http://{e}:{Web.Porta}";
    }

    /// <summary>Endereço para abrir no navegador deste PC.</summary>
    public string UrlLocal => $"http://localhost:{Web.Porta}/";
}

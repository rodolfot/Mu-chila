using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MuChilaAdmin.Web;

namespace MuChilaAdmin.Testes;

/// <summary>
/// O painel web inteiro num servidor de teste em memória: usuários e auditoria na memória, sem banco e sem o monitor.
/// Confere o que protege o painel: login, bloqueio, troca de senha obrigatória, anti-CSRF, papéis e cabeçalhos.
/// </summary>
public sealed class PainelWebTestes : IAsyncLifetime
{
    // como no MuChilaAdmin.runtimeconfig.json (BlazorDisableThrowNavigationException no .csproj do painel)
    static PainelWebTestes() => AppContext.SetSwitch("Microsoft.AspNetCore.Components.Endpoints.NavigationManager.DisableThrowNavigationException", true);

    readonly PastaTemporaria pasta = new();
    readonly RepositorioMemoria repo = new();
    readonly AuditoriaMemoria auditoria = new();
    WebApplication app = default!;

    public async Task InitializeAsync()
    {
        SenhaHasher.Iteracoes = 1_000;
        var cfg = new Configuracao { PastaServidor = pasta.Caminho, PastaCliente = pasta.Caminho, Banco = "Server=127.0.0.1,1;Connect Timeout=1" };
        app = WebHost.Criar(Array.Empty<string>(), cfg, b =>
        {
            b.WebHost.UseTestServer();
            b.Services.RemoveAll<IRepositorioUsuarios>();
            b.Services.AddSingleton<IRepositorioUsuarios>(repo);
            b.Services.RemoveAll<IAuditoria>();
            b.Services.AddSingleton<IAuditoria>(auditoria);
            foreach (var d in b.Services.Where(d => d.ImplementationType?.Name == "MonitorPainel").ToList()) b.Services.Remove(d);
        });
        await app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await app.DisposeAsync();
        pasta.Dispose();
    }

    HttpClient Cliente() =>
        new(new CookieContainerHandler { InnerHandler = app.GetTestServer().CreateHandler() }) { BaseAddress = new Uri("http://localhost") };

    ServicoUsuarios Servico => app.Services.GetRequiredService<ServicoUsuarios>();

    static string Caminho(Uri? u) => u == null ? "" : u.IsAbsoluteUri ? u.PathAndQuery : u.OriginalString;
    static async Task<string> Texto(HttpResponseMessage r) => WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());

    static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"|name=\"__RequestVerificationToken\" value=\"([^\"]+)\"") is { Success: true } m
            ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            : throw new Exception("token anti-CSRF não encontrado na página de login");

    static async Task<HttpResponseMessage> Entrar(HttpClient c, string usuario, string senha, string? voltar = null)
    {
        var pagina = await c.GetStringAsync("/entrar" + (voltar != null ? "?voltar=" + Uri.EscapeDataString(voltar) : ""));
        return await c.PostAsync("/entrar" + (voltar != null ? "?voltar=" + Uri.EscapeDataString(voltar) : ""), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "entrar", ["__RequestVerificationToken"] = Token(pagina), ["Dados.Usuario"] = usuario, ["Dados.Senha"] = senha,
        }));
    }

    [Fact]
    public async Task Saude_responde_sem_login()
    {
        var r = await Cliente().GetAsync("/api/saude");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("\"ok\":true", await r.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/contas")]
    [InlineData("/usuarios")]
    [InlineData("/drops")]
    public async Task Tela_sem_login_vai_para_o_login(string caminho)
    {
        var r = await Cliente().GetAsync(caminho);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/entrar?voltar=" + Uri.EscapeDataString(caminho), Caminho(r.Headers.Location));
    }

    [Theory]
    [InlineData("/api/itens/foto/0/0")]
    [InlineData("/api/relatorios/contas.csv")]
    public async Task Dados_sem_login_dao_401(string caminho)
    {
        var r = await Cliente().GetAsync(caminho);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Sem_usuarios_o_login_manda_para_o_primeiro_acesso()
    {
        var r = await Cliente().GetAsync("/entrar");
        Assert.True(r.StatusCode == HttpStatusCode.Redirect, $"veio {r.StatusCode}: {(await Texto(r))[..Math.Min(3000, (await Texto(r)).Length)]}");
        Assert.Equal("/primeiro-acesso", Caminho(r.Headers.Location));
    }

    [Fact]
    public async Task Cabecalhos_de_seguranca()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var r = await Cliente().GetAsync("/entrar");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        var csp = r.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("script-src 'self'", csp);
    }

    [Fact]
    public async Task Login_certo_abre_a_sessao_e_audita()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var c = Cliente();
        var r = await Entrar(c, "admin", "Senha123", voltar: "/drops");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/drops", Caminho(r.Headers.Location));
        // com a sessão aberta, os dados respondem (sem foto na pasta de teste: 204)
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync("/api/itens/foto/0/0")).StatusCode);
        Assert.Contains(auditoria.Eventos, e => e.Acao == "login.ok" && e.Usuario == "admin");
    }

    [Fact]
    public async Task Voltar_para_outro_site_nao_vale()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var r = await Entrar(Cliente(), "admin", "Senha123", voltar: "https://site-malicioso.com/");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.DoesNotContain("malicioso", r.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Senha_errada_nao_entra_e_audita()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var c = Cliente();
        var r = await Entrar(c, "admin", "errada123");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Usuário ou senha incorretos.", await Texto(r));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/itens/foto/0/0")).StatusCode);
        Assert.Contains(auditoria.Eventos, e => e.Acao == "login.falha" && !e.Ok);
    }

    [Fact]
    public async Task Cinco_erros_bloqueiam_pela_tela_de_login()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var c = Cliente();
        for (int i = 0; i < 5; i++) await Entrar(c, "admin", "errada123");
        var r = await Entrar(c, "admin", "Senha123");
        Assert.Contains("Muitas tentativas erradas", await Texto(r));
    }

    [Fact]
    public async Task Login_sem_token_anti_csrf_e_recusado()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var r = await Cliente().PostAsync("/entrar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "entrar", ["Dados.Usuario"] = "admin", ["Dados.Senha"] = "Senha123",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Senha_temporaria_obriga_a_trocar_antes_de_tudo()
    {
        Servico.Criar("mod", "Mod", Papeis.Moderador, "Senha123", trocarSenha: true, null);
        var c = Cliente();
        var r = await Entrar(c, "mod", "Senha123");
        Assert.Equal("/minha-conta/senha", Caminho(r.Headers.Location));
        var outra = await c.GetAsync("/api/itens/foto/0/0");
        Assert.Equal(HttpStatusCode.Redirect, outra.StatusCode);
        Assert.Equal("/minha-conta/senha", Caminho(outra.Headers.Location));
    }

    [Fact]
    public async Task Papel_leitura_nao_exporta_relatorio_de_administrador()
    {
        Servico.Criar("olho", "Olho", Papeis.Leitura, "Senha123", false, null);
        var c = Cliente();
        await Entrar(c, "olho", "Senha123");
        var r = await c.GetAsync("/api/relatorios/auditoria.csv");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Tela_de_administrador_manda_o_moderador_para_sem_permissao()
    {
        Servico.Criar("mod", "Mod", Papeis.Moderador, "Senha123", false, null);
        var c = Cliente();
        await Entrar(c, "mod", "Senha123");
        var r = await c.GetAsync("/usuarios");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.StartsWith("/sem-permissao", Caminho(r.Headers.Location));
    }

    [Fact]
    public async Task Sair_so_por_post_com_token()
    {
        Servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        var c = Cliente();
        await Entrar(c, "admin", "Senha123");
        Assert.NotEqual(HttpStatusCode.Redirect, (await c.GetAsync("/sair")).StatusCode);                  // link não desloga
        var semToken = await c.PostAsync("/sair", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync("/api/itens/foto/0/0")).StatusCode);        // continua logado
    }
}

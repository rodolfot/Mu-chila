using System.IO;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using MuChilaAdmin.Components;
using MuChilaAdmin.Web.Seguranca;
using MuChilaAdmin.Web.Servicos;

namespace MuChilaAdmin.Web;

/// <summary>
/// Monta o painel web: Kestrel nos endereços da configuração, login por cookie, todas as rotas fechadas por padrão
/// (FallbackPolicy: só entra quem fez login; /entrar e /primeiro-acesso são as exceções), papéis (admin, moderador,
/// leitura), anti-CSRF nos formulários, cabeçalhos de segurança, telas em Blazor (servidor) e as tarefas de fundo
/// (alertas, métricas, bans temporários).
/// </summary>
public static class WebHost
{
    /// <param name="ajuste">Para os testes: troca serviços (ex.: usuários em memória) antes de montar.</param>
    public static WebApplication Criar(string[] args, Configuracao cfg, Action<WebApplicationBuilder>? ajuste = null)
    {
        Configuracao.Definir(cfg);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            // nome fixo: os arquivos estáticos (MuChilaAdmin.staticwebassets.*.json) são achados pelo nome do app, também nos testes
            ApplicationName = typeof(WebHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = cfg.Desenvolvimento ? Environments.Development : Environments.Production,
        });
        builder.WebHost.UseStaticWebAssets();   // no build de desenvolvimento os arquivos vêm da pasta do projeto
        builder.WebHost.UseUrls(cfg.UrlsDeEscuta().ToArray());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new LogArquivoProvider());
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var s = builder.Services;
        s.AddSingleton(cfg);
        s.AddSingleton<IRepositorioUsuarios>(_ => new RepositorioUsuariosSql());
        s.AddSingleton(sp => new ServicoUsuarios(sp.GetRequiredService<IRepositorioUsuarios>()));
        s.AddSingleton<IAuditoria>(_ => new AuditoriaSql());
        s.AddSingleton(_ => new Alertas());
        s.AddSingleton(_ => new Bans());
        s.AddSingleton<RecargaMonstros>();
        s.AddHostedService<MonitorPainel>();

        s.AddScoped<Sessao>();
        s.AddScoped<Avisos>();
        s.AddScoped<Confirmacao>();
        s.AddScoped<ExecutorAcoes>();
        s.AddScoped<Downloads>();

        s.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "MuChilaAdmin";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.LoginPath = "/entrar";
            o.LogoutPath = "/sair";
            o.AccessDeniedPath = "/sem-permissao";
            o.ReturnUrlParameter = "voltar";
            o.ExpireTimeSpan = TimeSpan.FromMinutes(cfg.Web.MinutosSessao);
            o.SlidingExpiration = true;
            o.Events.OnValidatePrincipal = ValidacaoSessao.ValidarCookie;
            o.Events.OnRedirectToLogin = ctx =>
            {
                // pedidos de dados (relatórios, fotos) sem login: 401 em vez de mandar a página de login
                if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; SemPaginaDeErro(ctx.HttpContext); }
                else ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = ctx =>
            {
                // e sem permissão: 403 (as telas vão para /sem-permissao)
                if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; SemPaginaDeErro(ctx.HttpContext); }
                else ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
        });
        s.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Moderar, p => p.RequireAuthenticatedUser().RequireRole(Papeis.Admin, Papeis.Moderador))
            .AddPolicy(Politicas.Administrar, p => p.RequireAuthenticatedUser().RequireRole(Papeis.Admin));
        s.AddCascadingAuthenticationState();
        s.AddScoped<AuthenticationStateProvider, EstadoAutenticacaoRevalidado>();
        // o X-Frame-Options já vai como DENY nos cabeçalhos de segurança (o padrão do anti-CSRF, SAMEORIGIN, criaria um segundo cabeçalho)
        s.AddAntiforgery(o => { o.Cookie.Name = "MuChilaAdmin.Anti"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.HttpOnly = true; o.SuppressXFrameOptionsHeader = true; });
        s.AddDataProtection().SetApplicationName("MuChilaAdmin");
        s.AddRazorComponents().AddInteractiveServerComponents(o =>
        {
            o.DetailedErrors = cfg.Desenvolvimento;
            o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
        });
        s.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        ajuste?.Invoke(builder);
        var app = builder.Build();

        if (cfg.Desenvolvimento) app.UseDeveloperExceptionPage();
        else app.UseExceptionHandler("/erro", createScopeForErrors: true);
        app.UseStatusCodePagesWithReExecute("/nao-encontrado");
        app.Use(CabecalhosSeguranca.Aplicar);
        app.UseAuthentication();
        app.UseMiddleware<ExigirTrocaDeSenha>();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapStaticAssets().AllowAnonymous();
        // o frame-ancestors do Blazor sairia num segundo Content-Security-Policy; o painel já manda frame-ancestors 'none'
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = null);
        Endpoints.Mapear(app);
        return app;
    }

    /// <summary>Resposta de dados (401/403 da /api) sai como está: sem isto, a página de "não encontrado" era montada por cima
    /// e, sem login, virava um redirecionamento para o login.</summary>
    static void SemPaginaDeErro(HttpContext ctx)
    {
        if (ctx.Features.Get<IStatusCodePagesFeature>() is { } f) f.Enabled = false;
    }
}

public static class Politicas
{
    public const string Moderar = "Moderar", Administrar = "Administrar";
}

/// <summary>Erros do painel em painel-erros.log (ao lado do executável; *.log fica fora do repositório).</summary>
public static class Registro
{
    static readonly object trava = new();
    public static string Arquivo => Path.Combine(AppContext.BaseDirectory, "painel-erros.log");

    public static void Erro(string contexto, Exception ex) => Escrever($"{contexto}: {ex}");

    public static void Escrever(string texto)
    {
        try
        {
            lock (trava)
            {
                var f = Arquivo;
                if (File.Exists(f) && new FileInfo(f).Length > 5_000_000) File.Move(f, f + ".antigo", overwrite: true);
                File.AppendAllText(f, $"{DateTime.Now:s} {texto}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch { }
    }
}

/// <summary>Leva os avisos e erros do ASP.NET para o painel-erros.log.</summary>
sealed class LogArquivoProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoria) => new LogArquivo(categoria);
    public void Dispose() { }

    sealed class LogArquivo(string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Warning;
        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? ex, Func<TState, Exception?, string> formatar)
        {
            if (!IsEnabled(nivel)) return;
            // desconexão normal do navegador (fechou a aba) não é erro
            if (ex is TaskCanceledException or OperationCanceledException) return;
            Registro.Escrever($"[{nivel}] {categoria}: {formatar(estado, ex)}{(ex != null ? Environment.NewLine + ex : "")}");
        }
    }
}

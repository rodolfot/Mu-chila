using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace MuChilaAdmin.Web.Seguranca;

/// <summary>O que vai dentro do cookie de login (assinado e criptografado pelo ASP.NET).</summary>
public static class Identidade
{
    public const string Id = "uid", Nome = "nome", Carimbo = "carimbo", TrocarSenha = "trocar";

    public static ClaimsPrincipal Criar(Usuario u)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, u.Login), new(ClaimTypes.Role, u.Papel), new(Id, u.Id.ToString()), new(Nome, u.Nome),
            new(Carimbo, u.Carimbo.ToString()), new(TrocarSenha, u.TrocarSenha ? "1" : "0"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role));
    }

    public static int? IdDe(ClaimsPrincipal u) => int.TryParse(u.FindFirst(Id)?.Value, out var i) ? i : null;
    public static Guid? CarimboDe(ClaimsPrincipal u) => Guid.TryParse(u.FindFirst(Carimbo)?.Value, out var g) ? g : null;
    public static string NomeDe(ClaimsPrincipal u) => u.FindFirst(Nome)?.Value ?? u.Identity?.Name ?? "?";
    public static string PapelDe(ClaimsPrincipal u) => u.FindFirst(ClaimTypes.Role)?.Value ?? "";
    public static bool PrecisaTrocarSenha(ClaimsPrincipal u) => u.FindFirst(TrocarSenha)?.Value == "1";
    public static bool Admin(ClaimsPrincipal u) => u.IsInRole(Papeis.Admin);
    public static bool Moderador(ClaimsPrincipal u) => u.IsInRole(Papeis.Admin) || u.IsInRole(Papeis.Moderador);
}

/// <summary>
/// A cada pedido com cookie: o usuário ainda existe, está ativo e o carimbo é o mesmo? (o carimbo muda quando a senha, o
/// papel ou o "ativo" mudam: a sessão antiga cai na hora). Resultado guardado por 20 s para não ir ao banco em cada arquivo.
/// </summary>
public static class ValidacaoSessao
{
    static readonly ConcurrentDictionary<(int, Guid), (bool Ok, DateTime Quando)> cache = new();
    static readonly TimeSpan Validade = TimeSpan.FromSeconds(20);

    public static bool Valida(IServiceProvider sp, ClaimsPrincipal p)
    {
        if (Identidade.IdDe(p) is not int id || Identidade.CarimboDe(p) is not Guid carimbo) return false;
        if (cache.TryGetValue((id, carimbo), out var c) && DateTime.UtcNow - c.Quando < Validade) return c.Ok;
        bool ok;
        try { ok = sp.GetRequiredService<ServicoUsuarios>().SessaoValida(id, carimbo); }
        catch (Exception ex)
        {
            // banco fora do ar: mantém a última resposta (sem banco, o painel também não consegue fazer nada)
            Registro.Erro("validar sessão", ex);
            return !cache.ContainsKey((id, carimbo)) || c.Ok;
        }
        cache[(id, carimbo)] = (ok, DateTime.UtcNow);
        return ok;
    }

    /// <summary>Esquece o que foi guardado (depois de trocar senha ou papel, para valer na hora).</summary>
    public static void Esquecer() => cache.Clear();

    public static async Task ValidarCookie(CookieValidatePrincipalContext ctx)
    {
        if (ctx.Principal == null || !Valida(ctx.HttpContext.RequestServices, ctx.Principal))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

/// <summary>Telas abertas (conexão do Blazor) também conferem a sessão a cada 2 minutos.</summary>
public sealed class EstadoAutenticacaoRevalidado(ILoggerFactory logs, IServiceScopeFactory escopos) : RevalidatingServerAuthenticationStateProvider(logs)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(2);

    protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState estado, CancellationToken ct)
    {
        using var escopo = escopos.CreateScope();
        return Task.FromResult(estado.User.Identity?.IsAuthenticated == true && ValidacaoSessao.Valida(escopo.ServiceProvider, estado.User));
    }
}

/// <summary>Quem entrou com senha temporária só usa o painel depois de trocar a senha.</summary>
public sealed class ExigirTrocaDeSenha(RequestDelegate proximo)
{
    static readonly string[] Livres = { "/minha-conta/senha", "/sair", "/_blazor", "/_framework", "/_content", "/css", "/js", "/img", "/favicon", "/erro", "/nao-encontrado" };

    public Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && Identidade.PrecisaTrocarSenha(ctx.User)
            && !Livres.Any(l => ctx.Request.Path.StartsWithSegments(l, StringComparison.OrdinalIgnoreCase)))
        {
            ctx.Response.Redirect("/minha-conta/senha");
            return Task.CompletedTask;
        }
        return proximo(ctx);
    }
}

/// <summary>Cabeçalhos contra clickjacking, MIME sniffing e vazamento de endereço; conteúdo só do próprio painel.</summary>
public static class CabecalhosSeguranca
{
    public static Task Aplicar(HttpContext ctx, Func<Task> proximo)
    {
        var h = ctx.Response.Headers;
        h["X-Frame-Options"] = "DENY";
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        h["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; script-src 'self'; " +
            "connect-src 'self' ws: wss:; font-src 'self' data:; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'";
        return proximo();
    }
}

/// <summary>Endereço de volta depois do login: só caminhos deste painel (nada de //outro-site ou http://).</summary>
public static class Redirecionamento
{
    public static string Seguro(string? voltar) =>
        !string.IsNullOrEmpty(voltar) && voltar.StartsWith('/') && !voltar.StartsWith("//") && !voltar.StartsWith("/\\") && !voltar.Contains("://")
        && !voltar.StartsWith("/entrar", StringComparison.OrdinalIgnoreCase) ? voltar : "/";
}

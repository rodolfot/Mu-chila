using System.IO;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using MuChilaAdmin.Core.Painel.Relatorios;
using MuChilaAdmin.Web.Seguranca;
using MuChilaAdmin.Web.Servicos;

namespace MuChilaAdmin.Web;

/// <summary>Rotas que não são telas: sair, fotos dos itens, relatórios para baixar e a conferência de "está no ar".</summary>
public static class Endpoints
{
    public static void Mapear(WebApplication app)
    {
        // sair: só por POST com o token anti-CSRF (um link malicioso não desloga ninguém)
        app.MapPost("/sair", async (HttpContext ctx, IAntiforgery anti, IAuditoria auditoria) =>
        {
            if (!await anti.IsRequestValidAsync(ctx)) return Results.BadRequest();
            var login = ctx.User.Identity?.Name;
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (login != null) auditoria.Registrar(new EventoAuditoria(DateTime.Now, login, ctx.Connection.RemoteIpAddress?.ToString(), "login.sair", null, null, true, null));
            return Results.LocalRedirect("/entrar?saiu=1");
        }).AllowAnonymous();

        // foto do item (as do MuEditor); sem foto = 204 e a tela mostra o ícone
        app.MapGet("/api/itens/foto/{secao:int}/{tipo:int}", (int secao, int tipo, HttpContext ctx) =>
        {
            var f = ItemImages.Arquivo(secao, tipo);
            // sem foto: 204 (a tag img cai no onerror e mostra o ícone, sem "404" no console do navegador)
            if (f == null) return Results.NoContent();
            ctx.Response.Headers.CacheControl = "private, max-age=86400";
            return Results.File(f, "image/jpeg");
        });

        // relatórios prontos para baixar: /api/relatorios/contas.csv?de=2026-09-01&ate=2026-10-01&texto=
        app.MapGet("/api/relatorios/{id}.{formato}", async (string id, string formato, DateTime? de, DateTime? ate, string? texto,
                                                            HttpContext ctx, IAuthorizationService autz, IAuditoria auditoria) =>
        {
            var def = Catalogo.Por(id);
            if (def == null || formato is not ("csv" or "pdf")) return Results.NotFound();
            if (!PodeVer(ctx.User, def.PapelMinimo)) return Results.Forbid();
            var p = new ParametrosRelatorio { De = de, Ate = ate?.Date.AddDays(1), Texto = texto };
            Relatorio r;
            byte[] arquivo;
            try
            {
                r = await Task.Run(() => def.Gerar(p));
                r.GeradoPor = ctx.User.Identity?.Name;
                // o arquivo também dentro do try: um erro aqui virava página de erro HTML ("pedidos.htm", issue #48)
                arquivo = formato == "pdf" ? Pdf.Gerar(r) : Csv.Gerar(r);
            }
            catch (Exception ex)
            {
                Registro.Erro($"relatório {id}", ex);
                return Results.Problem(ExecutorAcoes.MensagemDe(ex), statusCode: 500);
            }
            auditoria.Registrar(new EventoAuditoria(DateTime.Now, ctx.User.Identity?.Name ?? "?", ctx.Connection.RemoteIpAddress?.ToString(),
                "relatorio.exportar", def.Nome, $"de={de:yyyy-MM-dd} ate={ate:yyyy-MM-dd} texto={texto}", true, $"{formato.ToUpperInvariant()}, {r.Linhas.Count} linha(s)"));
            var nome = $"{Downloads.NomeArquivo(def.Nome)}-{DateTime.Now:yyyyMMdd-HHmm}.{formato}";
            return Results.File(arquivo, formato == "pdf" ? "application/pdf" : "text/csv; charset=utf-8", nome);
        });

        // para conferir de fora se o painel está no ar (sem dados)
        app.MapGet("/api/saude", () => Results.Json(new { ok = true, painel = "Mu Chila Admin", versao = Versao.Texto })).AllowAnonymous();
    }

    public static bool PodeVer(System.Security.Claims.ClaimsPrincipal u, string papelMinimo) => papelMinimo switch
    {
        Papeis.Admin => Identidade.Admin(u),
        Papeis.Moderador => Identidade.Moderador(u),
        _ => u.Identity?.IsAuthenticated == true,
    };
}

public static class Versao
{
    public static string Texto => typeof(Versao).Assembly.GetName().Version?.ToString(3) ?? "?";
    public static DateTime Compilado => File.GetLastWriteTime(typeof(Versao).Assembly.Location);
}

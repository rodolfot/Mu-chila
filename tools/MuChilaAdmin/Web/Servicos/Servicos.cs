using System.Data;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.JSInterop;
using MuChilaAdmin.Core.Painel.Relatorios;
using MuChilaAdmin.Web.Seguranca;

namespace MuChilaAdmin.Web.Servicos;

/// <summary>Dados da conexão do navegador (por tela aberta): o IP vai para a auditoria.</summary>
public sealed class Sessao
{
    public string? Ip { get; set; }
}

/// <summary>Avisos flutuantes (canto da tela): sucesso, erro, informação. Um por tela aberta.</summary>
public sealed class Avisos
{
    public sealed record Aviso(Guid Id, string Tipo, string Texto, string? Titulo, DateTime Quando);
    readonly List<Aviso> lista = new();
    public event Action? Mudou;

    public IReadOnlyList<Aviso> Atuais { get { lock (lista) return lista.ToList(); } }

    void Add(string tipo, string texto, string? titulo)
    {
        lock (lista)
        {
            lista.Add(new Aviso(Guid.NewGuid(), tipo, texto, titulo, DateTime.Now));
            if (lista.Count > 5) lista.RemoveAt(0);
        }
        Mudou?.Invoke();
    }

    public void Sucesso(string texto, string? titulo = null) => Add("sucesso", texto, titulo);
    public void Erro(string texto, string? titulo = null) => Add("erro", texto, titulo ?? "Não deu certo");
    public void Info(string texto, string? titulo = null) => Add("info", texto, titulo);
    public void Atencao(string texto, string? titulo = null) => Add("atencao", texto, titulo);

    public void Fechar(Guid id) { lock (lista) lista.RemoveAll(a => a.Id == id); Mudou?.Invoke(); }
}

/// <summary>Pergunta "tem certeza?" numa janela do painel (substitui as caixas de confirmação do programa antigo).</summary>
public sealed class Confirmacao
{
    /// <summary>Opcoes != null: escolha entre vários botões (Resposta = índice escolhido; -1 = cancelou).</summary>
    public sealed record Pedido(string Titulo, string Texto, string Botao, bool Perigo, string[]? Opcoes, TaskCompletionSource<int> Resposta);
    public Pedido? Atual { get; private set; }
    public event Action? Mudou;

    public async Task<bool> Perguntar(string titulo, string texto, string botao = "Confirmar", bool perigo = false) =>
        await Abrir(new Pedido(titulo, texto, botao, perigo, null, Nova())) == 0;

    /// <summary>Vários caminhos (ex.: "Só os GameServers" / "GameServers + Castle Siege"). Devolve o índice ou -1 se cancelou.</summary>
    public Task<int> Escolher(string titulo, string texto, params string[] opcoes) => Abrir(new Pedido(titulo, texto, "", false, opcoes, Nova()));

    static TaskCompletionSource<int> Nova() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    Task<int> Abrir(Pedido p)
    {
        Atual?.Resposta.TrySetResult(-1);
        Atual = p;
        Mudou?.Invoke();
        return p.Resposta.Task;
    }

    public void Responder(bool sim) => Responder(sim ? 0 : -1);

    public void Responder(int escolha)
    {
        var p = Atual;
        Atual = null;
        Mudou?.Invoke();
        p?.Resposta.TrySetResult(escolha);
    }
}

/// <summary>
/// Executa uma ação do painel: confere a permissão do usuário, roda fora da tela (as rotinas do servidor podem demorar),
/// grava a auditoria (ok ou erro), mostra o resultado num aviso e registra o erro completo no painel-erros.log.
/// Mensagens das rotinas do núcleo (InvalidOperationException...) vão para a tela como estão; erro de banco vira texto curto.
/// </summary>
public sealed class ExecutorAcoes(AuthenticationStateProvider auth, IAuthorizationService autorizacao, IAuditoria auditoria, Sessao sessao, Avisos avisos)
{
    /// <summary>Uma ação por vez no painel inteiro: duas pessoas gravando os mesmos arquivos do servidor ao mesmo tempo se atropelariam.</summary>
    static readonly SemaphoreSlim fila = new(1, 1);

    /// <summary>Marca de quando os arquivos foram lidos. Ao salvar, marca diferente = alguém gravou depois (outra aba ou outro usuário).</summary>
    public static string Carimbo(params string[] arquivos) =>
        string.Join("|", arquivos.Select(a => File.Exists(a) ? File.GetLastWriteTimeUtc(a).Ticks.ToString() : "-"));

    public async Task<ClaimsPrincipal> Usuario() => (await auth.GetAuthenticationStateAsync()).User;

    public async Task<string> Login() => (await Usuario()).Identity?.Name ?? "?";

    public async Task<bool> Pode(string politica) => (await autorizacao.AuthorizeAsync(await Usuario(), politica)).Succeeded;

    /// <summary>Grava na auditoria sem executar nada (ex.: relatório exportado).</summary>
    public async Task Registrar(string acao, string? alvo, string? mensagem, bool ok = true, string? detalhes = null) =>
        auditoria.Registrar(new EventoAuditoria(DateTime.Now, await Login(), sessao.Ip, acao, alvo, detalhes, ok, mensagem));

    public Task<bool> Executar(string acao, string? alvo, Func<string?> trabalho, string? politica = null, string? detalhes = null, bool avisoSucesso = true) =>
        Rodar(acao, alvo, () => Task.FromResult(trabalho()), politica, detalhes, avisoSucesso);

    public Task<bool> Executar(string acao, string? alvo, Func<IEnumerable<string>> trabalho, string? politica = null, string? detalhes = null, bool avisoSucesso = true) =>
        Rodar(acao, alvo, () => Task.FromResult<string?>(string.Join("\n", trabalho())), politica, detalhes, avisoSucesso);

    public Task<bool> ExecutarAsync(string acao, string? alvo, Func<Task<string?>> trabalho, string? politica = null, string? detalhes = null, bool avisoSucesso = true) =>
        Rodar(acao, alvo, trabalho, politica, detalhes, avisoSucesso);

    async Task<bool> Rodar(string acao, string? alvo, Func<Task<string?>> trabalho, string? politica, string? detalhes, bool avisoSucesso)
    {
        var u = await Usuario();
        var login = u.Identity?.Name ?? "?";
        if (u.Identity?.IsAuthenticated != true || politica != null && !(await autorizacao.AuthorizeAsync(u, politica)).Succeeded)
        {
            auditoria.Registrar(new EventoAuditoria(DateTime.Now, login, sessao.Ip, acao, alvo, detalhes, false, "sem permissão"));
            avisos.Erro("Seu usuário não tem permissão para isso.");
            return false;
        }
        try
        {
            string? msg;
            if (!await fila.WaitAsync(TimeSpan.FromMinutes(3)))
                throw new InvalidOperationException("Outra ação do painel ainda está rodando. Tente de novo em instantes.");
            try { msg = await Task.Run(trabalho); }
            finally { fila.Release(); }
            auditoria.Registrar(new EventoAuditoria(DateTime.Now, login, sessao.Ip, acao, alvo, detalhes, true, msg));
            if (avisoSucesso) avisos.Sucesso(string.IsNullOrWhiteSpace(msg) ? "Feito." : msg!);
            return true;
        }
        catch (Exception ex)
        {
            var msg = MensagemDe(ex);
            Registro.Erro($"{acao} ({alvo})", ex);
            auditoria.Registrar(new EventoAuditoria(DateTime.Now, login, sessao.Ip, acao, alvo, detalhes, false, msg));
            avisos.Erro(msg);
            return false;
        }
    }

    public static string MensagemDe(Exception ex) => ex switch
    {
        AggregateException { InnerException: { } i } => MensagemDe(i),
        SqlException s => $"O banco de dados recusou a operação (erro {s.Number}). O detalhe ficou no painel-erros.log.",
        InvalidOperationException or ArgumentException or FormatException or IOException or UnauthorizedAccessException or KeyNotFoundException => ex.Message,
        _ => $"Erro inesperado ({ex.GetType().Name}): {ex.Message}",
    };
}

/// <summary>Baixa arquivos gerados no servidor (CSV, PDF) pelo navegador.</summary>
public sealed class Downloads(IJSRuntime js, ExecutorAcoes acoes)
{
    public async Task Baixar(string nome, string tipo, byte[] conteudo)
    {
        using var stream = new MemoryStream(conteudo);
        using var refStream = new DotNetStreamReference(stream);
        await js.InvokeVoidAsync("muAdmin.baixar", nome, tipo, refStream);
    }

    /// <summary>Exporta um relatório (CSV ou PDF) e registra na auditoria.</summary>
    public async Task Exportar(Relatorio r, string formato)
    {
        r.GeradoPor ??= await acoes.Login();
        var nome = NomeArquivo(r.Titulo) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm");
        if (formato == "pdf") await Baixar(nome + ".pdf", "application/pdf", Pdf.Gerar(r));
        else await Baixar(nome + ".csv", "text/csv;charset=utf-8", Csv.Gerar(r));
        await acoes.Registrar("relatorio.exportar", r.Titulo, $"{formato.ToUpperInvariant()}, {r.Linhas.Count} linha(s)");
    }

    public static string NomeArquivo(string titulo)
    {
        var s = titulo.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in s)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        }
        var r = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return r.Length == 0 ? "relatorio" : r;
    }
}

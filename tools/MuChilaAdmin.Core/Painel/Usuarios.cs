using System.Collections.Concurrent;
using System.Data;

namespace MuChilaAdmin.Core.Painel;

/// <summary>Papéis do painel. admin: tudo; moderador: contas, bans, itens, mensagens e conteúdo do site; leitura: só olha.</summary>
public static class Papeis
{
    public const string Admin = "admin", Moderador = "moderador", Leitura = "leitura";
    public static readonly string[] Todos = { Admin, Moderador, Leitura };

    public static string Nome(string papel) => papel switch
    {
        Admin => "Administrador",
        Moderador => "Moderador",
        Leitura => "Somente leitura",
        _ => papel,
    };

    public static bool Valido(string papel) => Todos.Contains(papel);
}

/// <summary>Usuário do painel (não é conta do jogo).</summary>
public sealed record Usuario(
    int Id, string Login, string Nome, string Papel, bool Ativo, bool TrocarSenha, int Tentativas, DateTime? BloqueadoAte,
    Guid Carimbo, DateTime Criado, string? CriadoPor, DateTime? UltimoLogin, string? UltimoIp)
{
    public bool BloqueadoEm(DateTime agora) => BloqueadoAte is { } ate && ate > agora;
}

/// <summary>Onde os usuários ficam guardados (SQL no painel; memória nos testes).</summary>
public interface IRepositorioUsuarios
{
    int Contar();
    IReadOnlyList<Usuario> Todos();
    Usuario? PorId(int id);
    Usuario? PorLogin(string login);
    string? Hash(int id);
    int Criar(string login, string nome, string papel, string hash, bool trocarSenha, string? criadoPor);
    /// <summary>Troca o hash e o carimbo (derruba as sessões abertas com o carimbo antigo).</summary>
    void TrocarHash(int id, string hash, bool trocarSenha);
    void Atualizar(int id, string nome, string papel, bool ativo);
    void RegistrarFalha(int id, int tentativas, DateTime? bloqueadoAte);
    void RegistrarEntrada(int id, string? ip);
    /// <summary>Gera carimbo novo: as sessões abertas desse usuário caem na próxima conferência.</summary>
    void NovoCarimbo(int id);
}

public sealed class RepositorioUsuariosSql : IRepositorioUsuarios
{
    const string Colunas = "id, usuario, nome, papel, ativo, trocar_senha, tentativas, bloqueado_ate, carimbo, criado, criado_por, ultimo_login, ultimo_ip";

    static Usuario Ler(DataRow r) => new(
        (int)r["id"], (string)r["usuario"], (string)r["nome"], (string)r["papel"], (bool)r["ativo"], (bool)r["trocar_senha"], (int)r["tentativas"],
        r["bloqueado_ate"] as DateTime?, (Guid)r["carimbo"], (DateTime)r["criado"], r["criado_por"] as string, r["ultimo_login"] as DateTime?, r["ultimo_ip"] as string);

    public RepositorioUsuariosSql() => Esquema.Garantir();

    public int Contar() => Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.MUCHILA_ADMIN_USUARIOS"));

    public IReadOnlyList<Usuario> Todos() =>
        Db.Query($"SELECT {Colunas} FROM dbo.MUCHILA_ADMIN_USUARIOS ORDER BY usuario").Rows.Cast<DataRow>().Select(Ler).ToList();

    public Usuario? PorId(int id) =>
        Db.Query($"SELECT {Colunas} FROM dbo.MUCHILA_ADMIN_USUARIOS WHERE id = @id", ("@id", id)).Rows.Cast<DataRow>().Select(Ler).FirstOrDefault();

    public Usuario? PorLogin(string login) =>
        Db.Query($"SELECT {Colunas} FROM dbo.MUCHILA_ADMIN_USUARIOS WHERE usuario = @u", ("@u", login)).Rows.Cast<DataRow>().Select(Ler).FirstOrDefault();

    public string? Hash(int id) => Db.Scalar("SELECT senha_hash FROM dbo.MUCHILA_ADMIN_USUARIOS WHERE id = @id", ("@id", id)) as string;

    public int Criar(string login, string nome, string papel, string hash, bool trocarSenha, string? criadoPor) =>
        Convert.ToInt32(Db.Scalar(@"INSERT INTO dbo.MUCHILA_ADMIN_USUARIOS (usuario, nome, senha_hash, papel, trocar_senha, criado_por, senha_trocada)
                                    OUTPUT INSERTED.id VALUES (@u, @n, @h, @p, @t, @c, SYSDATETIME())",
            ("@u", login), ("@n", nome), ("@h", hash), ("@p", papel), ("@t", trocarSenha), ("@c", (object?)criadoPor ?? DBNull.Value)));

    public void TrocarHash(int id, string hash, bool trocarSenha) =>
        Db.Execute(@"UPDATE dbo.MUCHILA_ADMIN_USUARIOS SET senha_hash = @h, trocar_senha = @t, carimbo = NEWID(), senha_trocada = SYSDATETIME(),
                     tentativas = 0, bloqueado_ate = NULL WHERE id = @id", ("@h", hash), ("@t", trocarSenha), ("@id", id));

    public void Atualizar(int id, string nome, string papel, bool ativo) =>
        Db.Execute(@"UPDATE dbo.MUCHILA_ADMIN_USUARIOS SET nome = @n, papel = @p, ativo = @a,
                     carimbo = CASE WHEN papel <> @p OR ativo <> @a THEN NEWID() ELSE carimbo END WHERE id = @id",
            ("@n", nome), ("@p", papel), ("@a", ativo), ("@id", id));

    public void RegistrarFalha(int id, int tentativas, DateTime? bloqueadoAte) =>
        Db.Execute("UPDATE dbo.MUCHILA_ADMIN_USUARIOS SET tentativas = @t, bloqueado_ate = @b WHERE id = @id",
            ("@t", tentativas), ("@b", (object?)bloqueadoAte ?? DBNull.Value), ("@id", id));

    public void RegistrarEntrada(int id, string? ip) =>
        Db.Execute("UPDATE dbo.MUCHILA_ADMIN_USUARIOS SET tentativas = 0, bloqueado_ate = NULL, ultimo_login = SYSDATETIME(), ultimo_ip = @ip WHERE id = @id",
            ("@ip", (object?)ip ?? DBNull.Value), ("@id", id));

    public void NovoCarimbo(int id) => Db.Execute("UPDATE dbo.MUCHILA_ADMIN_USUARIOS SET carimbo = NEWID() WHERE id = @id", ("@id", id));
}

public enum SituacaoLogin { Ok, Invalido, Bloqueado, Inativo, IpBloqueado }

public sealed record ResultadoLogin(SituacaoLogin Situacao, Usuario? Usuario = null, DateTime? BloqueadoAte = null)
{
    public bool Ok => Situacao == SituacaoLogin.Ok;

    /// <summary>Mensagem para a tela de login (não diz se o usuário existe).</summary>
    public string Mensagem => Situacao switch
    {
        SituacaoLogin.Ok => "",
        SituacaoLogin.Bloqueado => $"Muitas tentativas erradas. Tente de novo depois das {BloqueadoAte:HH:mm}.",
        SituacaoLogin.IpBloqueado => "Muitas tentativas erradas deste computador. Espere alguns minutos e tente de novo.",
        _ => "Usuário ou senha incorretos.",
    };
}

/// <summary>
/// Regras do login e dos usuários do painel:
///  - 5 senhas erradas seguidas bloqueiam o usuário por 15 minutos (e o painel gera um alerta);
///  - 20 erros em 10 minutos vindos do mesmo IP bloqueiam aquele IP por 10 minutos (contra tentativa e erro);
///  - usuário inexistente gasta o mesmo tempo que um existente (não revela quem existe);
///  - senha com pelo menos 8 caracteres, uma letra e um número, diferente do usuário.
/// Quem chama registra a auditoria (o serviço não sabe de onde veio o pedido além do IP).
/// </summary>
public sealed class ServicoUsuarios
{
    public const int MaxTentativas = 5;
    public static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(15);
    const int MaxErrosPorIp = 20;
    static readonly TimeSpan JanelaIp = TimeSpan.FromMinutes(10), BloqueioIp = TimeSpan.FromMinutes(10);

    readonly IRepositorioUsuarios repo;
    readonly Func<DateTime> agora;
    readonly ConcurrentDictionary<string, List<DateTime>> errosPorIp = new();
    readonly ConcurrentDictionary<string, DateTime> ipsBloqueados = new();

    public ServicoUsuarios(IRepositorioUsuarios repo, Func<DateTime>? relogio = null)
    {
        this.repo = repo;
        agora = relogio ?? (() => DateTime.Now);
    }

    public IRepositorioUsuarios Repositorio => repo;

    /// <summary>Nenhum usuário cadastrado ainda: a tela de primeiro acesso cria o administrador.</summary>
    public bool PrecisaPrimeiroAcesso() => repo.Contar() == 0;

    public static string Normalizar(string login) => (login ?? "").Trim().ToLowerInvariant();

    public ResultadoLogin Entrar(string login, string senha, string? ip)
    {
        var chaveIp = ip ?? "?";
        var t = agora();
        if (ipsBloqueados.TryGetValue(chaveIp, out var ateIp))
        {
            if (ateIp > t) return new(SituacaoLogin.IpBloqueado);
            ipsBloqueados.TryRemove(chaveIp, out _);
        }
        var u = repo.PorLogin(Normalizar(login));
        if (u == null)
        {
            SenhaHasher.Verificar(senha ?? "", SenhaHasher.HashFalso);   // mesmo custo de um usuário que existe
            ErroDoIp(chaveIp, t);
            return new(SituacaoLogin.Invalido);
        }
        if (u.BloqueadoEm(t)) { ErroDoIp(chaveIp, t); return new(SituacaoLogin.Bloqueado, u, u.BloqueadoAte); }
        var hash = repo.Hash(u.Id) ?? "";
        if (!SenhaHasher.Verificar(senha ?? "", hash, out var refazer))
        {
            ErroDoIp(chaveIp, t);
            int tent = u.Tentativas + 1;
            DateTime? bloqueio = tent >= MaxTentativas ? t + TempoBloqueio : null;
            repo.RegistrarFalha(u.Id, bloqueio != null ? 0 : tent, bloqueio);
            return bloqueio != null ? new(SituacaoLogin.Bloqueado, u, bloqueio) : new(SituacaoLogin.Invalido, u);
        }
        if (!u.Ativo) return new(SituacaoLogin.Inativo, u);
        if (refazer) repo.TrocarHash(u.Id, SenhaHasher.Gerar(senha!), u.TrocarSenha);   // hash antigo (menos iterações): regrava
        repo.RegistrarEntrada(u.Id, ip);
        errosPorIp.TryRemove(chaveIp, out _);
        return new(SituacaoLogin.Ok, repo.PorId(u.Id));
    }

    void ErroDoIp(string ip, DateTime t)
    {
        var lista = errosPorIp.GetOrAdd(ip, _ => new List<DateTime>());
        lock (lista)
        {
            lista.RemoveAll(x => t - x > JanelaIp);
            lista.Add(t);
            if (lista.Count >= MaxErrosPorIp) { ipsBloqueados[ip] = t + BloqueioIp; lista.Clear(); }
        }
    }

    /// <summary>Problemas da senha nova (lista vazia = aceita).</summary>
    public static List<string> ValidarSenha(string senha, string login)
    {
        senha ??= "";
        var erros = new List<string>();
        if (string.IsNullOrEmpty(senha) || senha.Length < 8) erros.Add("A senha precisa ter pelo menos 8 caracteres.");
        if (senha.Length > 128) erros.Add("A senha pode ter no máximo 128 caracteres.");
        if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit)) erros.Add("Use pelo menos uma letra e um número.");
        if (Normalizar(senha) == Normalizar(login)) erros.Add("A senha não pode ser igual ao usuário.");
        return erros;
    }

    public static string? ValidarLogin(string login)
    {
        var l = Normalizar(login);
        if (l.Length is < 3 or > 30) return "O usuário precisa ter de 3 a 30 caracteres.";
        if (!l.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-')) return "Use só letras sem acento, números, ponto, hífen e sublinhado no usuário.";
        return null;
    }

    public Usuario Criar(string login, string nome, string papel, string senha, bool trocarSenha, string? criadoPor)
    {
        var l = Normalizar(login);
        if (ValidarLogin(l) is { } e) throw new InvalidOperationException(e);
        if (!Papeis.Valido(papel)) throw new InvalidOperationException($"Papel inválido: {papel}.");
        if (string.IsNullOrWhiteSpace(nome)) nome = l;
        if (nome.Trim().Length > 60) throw new InvalidOperationException("O nome pode ter no máximo 60 caracteres.");
        var erros = ValidarSenha(senha, l);
        if (erros.Count > 0) throw new InvalidOperationException(string.Join(" ", erros));
        if (repo.PorLogin(l) != null) throw new InvalidOperationException($"Já existe o usuário {l}.");
        int id = repo.Criar(l, nome.Trim(), papel, SenhaHasher.Gerar(senha), trocarSenha, criadoPor);
        return repo.PorId(id)!;
    }

    /// <summary>O próprio usuário troca a senha (precisa da atual).</summary>
    public void TrocarSenha(int id, string atual, string nova)
    {
        var u = repo.PorId(id) ?? throw new InvalidOperationException("Usuário não encontrado.");
        if (!SenhaHasher.Verificar(atual ?? "", repo.Hash(id) ?? "")) throw new InvalidOperationException("A senha atual está errada.");
        if (atual == nova) throw new InvalidOperationException("A senha nova precisa ser diferente da atual.");
        var erros = ValidarSenha(nova, u.Login);
        if (erros.Count > 0) throw new InvalidOperationException(string.Join(" ", erros));
        repo.TrocarHash(id, SenhaHasher.Gerar(nova), trocarSenha: false);
    }

    /// <summary>Um administrador redefine a senha de alguém: senha temporária, que precisa ser trocada no próximo login.</summary>
    public string RedefinirSenha(int id)
    {
        var u = repo.PorId(id) ?? throw new InvalidOperationException("Usuário não encontrado.");
        string temp;
        do temp = SenhaHasher.SenhaTemporaria(); while (ValidarSenha(temp, u.Login).Count > 0);
        repo.TrocarHash(id, SenhaHasher.Gerar(temp), trocarSenha: true);
        return temp;
    }

    public void Atualizar(int id, string nome, string papel, bool ativo, int quemFaz)
    {
        var u = repo.PorId(id) ?? throw new InvalidOperationException("Usuário não encontrado.");
        if (!Papeis.Valido(papel)) throw new InvalidOperationException($"Papel inválido: {papel}.");
        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length > 60) throw new InvalidOperationException("O nome precisa ter de 1 a 60 caracteres.");
        if (id == quemFaz && (papel != u.Papel || !ativo)) throw new InvalidOperationException("Você não pode tirar o seu próprio acesso de administrador nem desativar a si mesmo.");
        bool eraAdminAtivo = u.Papel == Papeis.Admin && u.Ativo;
        if (eraAdminAtivo && (papel != Papeis.Admin || !ativo) && repo.Todos().Count(x => x.Papel == Papeis.Admin && x.Ativo) <= 1)
            throw new InvalidOperationException("Precisa sobrar pelo menos um administrador ativo.");
        repo.Atualizar(id, nome.Trim(), papel, ativo);
    }

    public void Desbloquear(int id) => repo.RegistrarFalha(id, 0, null);

    /// <summary>A sessão (cookie) ainda vale? Confere usuário ativo e o carimbo (muda ao trocar senha, papel ou desativar).</summary>
    public bool SessaoValida(int id, Guid carimbo) => repo.PorId(id) is { Ativo: true } u && u.Carimbo == carimbo;
}

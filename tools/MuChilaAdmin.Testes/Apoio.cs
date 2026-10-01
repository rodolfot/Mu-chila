// os testes mexem em estado global (iterações do hash, configuração em uso): um de cada vez
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MuChilaAdmin.Testes;

/// <summary>Usuários do painel na memória (no lugar da tabela MUCHILA_ADMIN_USUARIOS).</summary>
public sealed class RepositorioMemoria : IRepositorioUsuarios
{
    readonly object trava = new();
    readonly List<Usuario> lista = new();
    readonly Dictionary<int, string> hashes = new();
    int proximo = 1;

    public int Contar() { lock (trava) return lista.Count; }
    public IReadOnlyList<Usuario> Todos() { lock (trava) return lista.ToList(); }
    public Usuario? PorId(int id) { lock (trava) return lista.FirstOrDefault(u => u.Id == id); }
    public Usuario? PorLogin(string login) { lock (trava) return lista.FirstOrDefault(u => u.Login == login); }
    public string? Hash(int id) { lock (trava) return hashes.GetValueOrDefault(id); }

    public int Criar(string login, string nome, string papel, string hash, bool trocarSenha, string? criadoPor)
    {
        lock (trava)
        {
            var u = new Usuario(proximo++, login, nome, papel, true, trocarSenha, 0, null, Guid.NewGuid(), DateTime.Now, criadoPor, null, null);
            lista.Add(u);
            hashes[u.Id] = hash;
            return u.Id;
        }
    }

    public void TrocarHash(int id, string hash, bool trocarSenha)
    {
        lock (trava) hashes[id] = hash;
        Mudar(id, u => u with { TrocarSenha = trocarSenha, Carimbo = Guid.NewGuid(), Tentativas = 0, BloqueadoAte = null });
    }

    public void Atualizar(int id, string nome, string papel, bool ativo) =>
        Mudar(id, u => u with { Nome = nome, Papel = papel, Ativo = ativo, Carimbo = u.Papel != papel || u.Ativo != ativo ? Guid.NewGuid() : u.Carimbo });

    public void RegistrarFalha(int id, int tentativas, DateTime? bloqueadoAte) => Mudar(id, u => u with { Tentativas = tentativas, BloqueadoAte = bloqueadoAte });
    public void RegistrarEntrada(int id, string? ip) => Mudar(id, u => u with { Tentativas = 0, BloqueadoAte = null, UltimoLogin = DateTime.Now, UltimoIp = ip });
    public void NovoCarimbo(int id) => Mudar(id, u => u with { Carimbo = Guid.NewGuid() });

    void Mudar(int id, Func<Usuario, Usuario> f)
    {
        lock (trava)
        {
            int i = lista.FindIndex(u => u.Id == id);
            if (i >= 0) lista[i] = f(lista[i]);
        }
    }
}

/// <summary>Auditoria na memória.</summary>
public sealed class AuditoriaMemoria : IAuditoria
{
    readonly List<EventoAuditoria> eventos = new();
    public void Registrar(EventoAuditoria e) { lock (eventos) eventos.Add(e); }
    public List<EventoAuditoria> Eventos { get { lock (eventos) return eventos.ToList(); } }
}

/// <summary>Pasta temporária apagada no fim do teste.</summary>
public sealed class PastaTemporaria : IDisposable
{
    public string Caminho { get; } = Path.Combine(Path.GetTempPath(), "muchila-testes-" + Guid.NewGuid().ToString("N")[..10]);
    public PastaTemporaria() => Directory.CreateDirectory(Caminho);
    public string Arquivo(string relativo)
    {
        var f = Path.Combine(Caminho, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        return f;
    }
    public void Dispose() { try { Directory.Delete(Caminho, recursive: true); } catch { } }
}

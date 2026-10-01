using System.Security.Cryptography;
using System.Text;

namespace MuChilaAdmin.Core.Painel;

/// <summary>
/// Senhas do painel: PBKDF2-HMAC-SHA256 com sal aleatório de 16 bytes e 600.000 iterações (recomendação da OWASP para
/// PBKDF2-SHA256). O banco guarda só "pbkdf2-sha256$iterações$sal$hash" (Base64); a comparação é em tempo constante.
/// </summary>
public static class SenhaHasher
{
    public const int IteracoesPadrao = 600_000;
    const int TamanhoSal = 16, TamanhoHash = 32;
    const string Prefixo = "pbkdf2-sha256";

    /// <summary>Iterações usadas ao gerar hashes novos (os testes baixam para ficarem rápidos).</summary>
    public static int Iteracoes { get; set; } = IteracoesPadrao;

    public static string Gerar(string senha)
    {
        ArgumentNullException.ThrowIfNull(senha);
        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"{Prefixo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Confere a senha com o hash guardado. Hash em formato desconhecido = false (nunca exceção).</summary>
    public static bool Verificar(string senha, string guardado) => Verificar(senha, guardado, out _);

    /// <summary>Como Verificar; precisaRefazer = true quando o hash usa menos iterações que o padrão atual (regravar no próximo login).</summary>
    public static bool Verificar(string senha, string guardado, out bool precisaRefazer)
    {
        precisaRefazer = false;
        if (senha == null || string.IsNullOrEmpty(guardado)) return false;
        var partes = guardado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefixo || !int.TryParse(partes[1], out var iter) || iter < 1) return false;
        byte[] sal, esperado;
        try { sal = Convert.FromBase64String(partes[2]); esperado = Convert.FromBase64String(partes[3]); }
        catch (FormatException) { return false; }
        if (esperado.Length == 0) return false;
        var calculado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(senha), sal, iter, HashAlgorithmName.SHA256, esperado.Length);
        bool ok = CryptographicOperations.FixedTimeEquals(calculado, esperado);
        precisaRefazer = ok && iter < Iteracoes;
        return ok;
    }

    /// <summary>Hash de mentira, para gastar o mesmo tempo quando o usuário não existe (não revela quais usuários existem).</summary>
    static string? falso;
    public static string HashFalso => falso ??= Gerar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)));

    /// <summary>Senha temporária legível (sem letras parecidas: 0/O, 1/l/I), para "redefinir senha".</summary>
    public static string SenhaTemporaria(int tamanho = 12)
    {
        const string letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ", numeros = "23456789";
        var todos = letras + numeros;
        var c = new char[tamanho];
        for (int i = 0; i < tamanho; i++) c[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];
        c[RandomNumberGenerator.GetInt32(tamanho)] = numeros[RandomNumberGenerator.GetInt32(numeros.Length)];   // pelo menos um número
        return new string(c);
    }
}

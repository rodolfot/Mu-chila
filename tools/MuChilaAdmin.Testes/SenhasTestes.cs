namespace MuChilaAdmin.Testes;

public class SenhasTestes
{
    static SenhasTestes() => SenhaHasher.Iteracoes = 1_000;   // rápido nos testes; o painel usa 600.000

    [Fact]
    public void Hash_confere_so_a_senha_certa()
    {
        var h = SenhaHasher.Gerar("Senha123");
        Assert.StartsWith("pbkdf2-sha256$1000$", h);
        Assert.True(SenhaHasher.Verificar("Senha123", h));
        Assert.False(SenhaHasher.Verificar("senha123", h));
        Assert.False(SenhaHasher.Verificar("", h));
    }

    [Fact]
    public void Mesma_senha_gera_hash_diferente()
    {
        Assert.NotEqual(SenhaHasher.Gerar("Senha123"), SenhaHasher.Gerar("Senha123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto qualquer")]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$1000$não-é-base64$AAAA")]
    [InlineData("md5$1$a$b")]
    public void Hash_estranho_nunca_da_excecao(string guardado)
    {
        Assert.False(SenhaHasher.Verificar("Senha123", guardado));
    }

    [Fact]
    public void Hash_com_menos_iteracoes_pede_para_refazer()
    {
        var antigo = SenhaHasher.Gerar("Senha123");   // 1.000 iterações
        int antes = SenhaHasher.Iteracoes;
        try
        {
            SenhaHasher.Iteracoes = 2_000;
            Assert.True(SenhaHasher.Verificar("Senha123", antigo, out var refazer));
            Assert.True(refazer);
            Assert.True(SenhaHasher.Verificar("Senha123", SenhaHasher.Gerar("Senha123"), out refazer));
            Assert.False(refazer);
        }
        finally { SenhaHasher.Iteracoes = antes; }
    }

    [Fact]
    public void Senha_temporaria_passa_nas_regras()
    {
        for (int i = 0; i < 50; i++)
        {
            var s = SenhaHasher.SenhaTemporaria();
            Assert.Equal(12, s.Length);
            Assert.Contains(s, char.IsDigit);
            Assert.DoesNotContain(s, c => "0O1lI".Contains(c));
        }
    }

    [Theory]
    [InlineData("curta1", false)]
    [InlineData("semnumeros", false)]
    [InlineData("12345678", false)]
    [InlineData("Senha123", true)]
    [InlineData(null, false)]
    public void Regras_da_senha(string? senha, bool aceita)
    {
        Assert.Equal(aceita, ServicoUsuarios.ValidarSenha(senha!, "fulano").Count == 0);
    }

    [Fact]
    public void Senha_igual_ao_usuario_nao_vale()
    {
        Assert.NotEmpty(ServicoUsuarios.ValidarSenha("Fulano123", "fulano123"));
    }

    [Theory]
    [InlineData("ab", false)]
    [InlineData("mario", true)]
    [InlineData("Mario.Silva_2", true)]
    [InlineData("joão", false)]
    [InlineData("com espaco", false)]
    public void Regras_do_login(string login, bool aceito)
    {
        Assert.Equal(aceito, ServicoUsuarios.ValidarLogin(login) == null);
    }
}

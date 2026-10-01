namespace MuChilaAdmin.Testes;

public class UsuariosTestes
{
    static UsuariosTestes() => SenhaHasher.Iteracoes = 1_000;

    DateTime agora = new(2026, 10, 1, 12, 0, 0);
    readonly RepositorioMemoria repo = new();
    readonly ServicoUsuarios servico;

    public UsuariosTestes() => servico = new ServicoUsuarios(repo, () => agora);

    [Fact]
    public void Primeiro_acesso_so_sem_usuarios()
    {
        Assert.True(servico.PrecisaPrimeiroAcesso());
        servico.Criar("admin", "Admin", Papeis.Admin, "Senha123", false, null);
        Assert.False(servico.PrecisaPrimeiroAcesso());
    }

    [Fact]
    public void Login_certo_e_errado()
    {
        servico.Criar("Mario", "Mario", Papeis.Admin, "Senha123", false, null);
        var ok = servico.Entrar("  MARIO ", "Senha123", "1.2.3.4");   // o login não diferencia maiúsculas nem espaços
        Assert.True(ok.Ok);
        Assert.Equal("1.2.3.4", ok.Usuario!.UltimoIp);
        var errado = servico.Entrar("mario", "Senha124", "1.2.3.4");
        Assert.Equal(SituacaoLogin.Invalido, errado.Situacao);
        Assert.Equal("Usuário ou senha incorretos.", errado.Mensagem);
    }

    [Fact]
    public void Usuario_que_nao_existe_tem_a_mesma_mensagem()
    {
        var r = servico.Entrar("ninguem", "Senha123", "1.2.3.4");
        Assert.Equal(SituacaoLogin.Invalido, r.Situacao);
        Assert.Equal("Usuário ou senha incorretos.", r.Mensagem);
    }

    [Fact]
    public void Cinco_erros_bloqueiam_por_15_minutos()
    {
        servico.Criar("mario", "Mario", Papeis.Admin, "Senha123", false, null);
        for (int i = 0; i < 4; i++) Assert.Equal(SituacaoLogin.Invalido, servico.Entrar("mario", "errada1", "1.1.1.1").Situacao);
        var quinto = servico.Entrar("mario", "errada1", "1.1.1.1");
        Assert.Equal(SituacaoLogin.Bloqueado, quinto.Situacao);
        Assert.Equal(agora + ServicoUsuarios.TempoBloqueio, quinto.BloqueadoAte);

        // bloqueado: nem a senha certa entra
        Assert.Equal(SituacaoLogin.Bloqueado, servico.Entrar("mario", "Senha123", "1.1.1.1").Situacao);

        agora += TimeSpan.FromMinutes(16);
        Assert.True(servico.Entrar("mario", "Senha123", "1.1.1.1").Ok);
        Assert.Equal(0, repo.PorLogin("mario")!.Tentativas);
    }

    [Fact]
    public void Administrador_desbloqueia_antes()
    {
        var u = servico.Criar("mario", "Mario", Papeis.Admin, "Senha123", false, null);
        for (int i = 0; i < 5; i++) servico.Entrar("mario", "errada1", "1.1.1.1");
        servico.Desbloquear(u.Id);
        Assert.True(servico.Entrar("mario", "Senha123", "1.1.1.1").Ok);
    }

    [Fact]
    public void Muitos_erros_do_mesmo_ip_bloqueiam_o_ip()
    {
        servico.Criar("mario", "Mario", Papeis.Admin, "Senha123", false, null);
        for (int i = 0; i < 20; i++) servico.Entrar($"inexistente{i}", "errada1", "9.9.9.9");
        Assert.Equal(SituacaoLogin.IpBloqueado, servico.Entrar("mario", "Senha123", "9.9.9.9").Situacao);
        Assert.True(servico.Entrar("mario", "Senha123", "8.8.8.8").Ok);   // outro IP continua entrando
        agora += TimeSpan.FromMinutes(11);
        Assert.True(servico.Entrar("mario", "Senha123", "9.9.9.9").Ok);
    }

    [Fact]
    public void Usuario_desativado_nao_entra()
    {
        servico.Criar("chefe", "Chefe", Papeis.Admin, "Senha123", false, null);
        var u = servico.Criar("mod", "Mod", Papeis.Moderador, "Senha123", false, null);
        servico.Atualizar(u.Id, "Mod", Papeis.Moderador, ativo: false, quemFaz: 1);
        Assert.Equal(SituacaoLogin.Inativo, servico.Entrar("mod", "Senha123", null).Situacao);
    }

    [Fact]
    public void Sempre_sobra_um_administrador_ativo()
    {
        var a = servico.Criar("chefe", "Chefe", Papeis.Admin, "Senha123", false, null);
        var b = servico.Criar("outro", "Outro", Papeis.Admin, "Senha123", false, null);
        servico.Atualizar(b.Id, "Outro", Papeis.Leitura, true, quemFaz: a.Id);   // ok: ainda tem o chefe
        var e = Assert.Throws<InvalidOperationException>(() => servico.Atualizar(a.Id, "Chefe", Papeis.Moderador, true, quemFaz: b.Id));
        Assert.Contains("pelo menos um administrador", e.Message);
    }

    [Fact]
    public void Ninguem_tira_o_proprio_acesso()
    {
        var a = servico.Criar("chefe", "Chefe", Papeis.Admin, "Senha123", false, null);
        servico.Criar("outro", "Outro", Papeis.Admin, "Senha123", false, null);
        Assert.Throws<InvalidOperationException>(() => servico.Atualizar(a.Id, "Chefe", Papeis.Admin, ativo: false, quemFaz: a.Id));
    }

    [Fact]
    public void Trocar_senha_derruba_as_sessoes_antigas()
    {
        var u = servico.Criar("mario", "Mario", Papeis.Admin, "Senha123", false, null);
        var carimbo = u.Carimbo;
        Assert.True(servico.SessaoValida(u.Id, carimbo));
        Assert.Throws<InvalidOperationException>(() => servico.TrocarSenha(u.Id, "errada99", "Nova12345"));
        servico.TrocarSenha(u.Id, "Senha123", "Nova12345");
        Assert.False(servico.SessaoValida(u.Id, carimbo));
        Assert.True(servico.Entrar("mario", "Nova12345", null).Ok);
    }

    [Fact]
    public void Redefinir_gera_senha_temporaria_que_obriga_a_trocar()
    {
        var u = servico.Criar("mod", "Mod", Papeis.Moderador, "Senha123", false, null);
        var temp = servico.RedefinirSenha(u.Id);
        var r = servico.Entrar("mod", temp, null);
        Assert.True(r.Ok);
        Assert.True(r.Usuario!.TrocarSenha);
        Assert.False(servico.Entrar("mod", "Senha123", null).Ok);
    }

    [Fact]
    public void Nao_cria_usuario_repetido_nem_papel_inventado()
    {
        servico.Criar("mario", "Mario", Papeis.Admin, "Senha123", false, null);
        Assert.Throws<InvalidOperationException>(() => servico.Criar("MARIO", "Outro", Papeis.Leitura, "Senha123", false, null));
        Assert.Throws<InvalidOperationException>(() => servico.Criar("novo", "Novo", "dono", "Senha123", false, null));
    }
}

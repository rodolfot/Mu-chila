using System.Text;

namespace MuChilaAdmin.Testes;

/// <summary>Gravação do ItemDrop.txt numa pasta de servidor temporária (nada do servidor de verdade é tocado).</summary>
public class DropsTestes : IDisposable
{
    readonly PastaTemporaria pasta = new();
    readonly string arquivo;

    const string Conteudo =
        "//=====================\r\n" +
        "//Index   Level   Grade   Option0   Option1   Option2   Option3   Option4   Option5   Option6   Duration   MapNumber   MonsterClass   MonsterLevelMin   MonsterLevelMax   DropRate   Comment\r\n" +
        "6159      0       0       *         *         *         *         *         *         *         0          *           *              12                150               100        //Jewel of Chaos\r\n" +
        "6672      1       0       *         *         *         *         *         *         *         0          *           *              1                 32                1000       //Scroll of Archangel 1\r\n" +
        "7181      0       0       *         *         *         *         *         *         *         0          3           *              *                 *                 5000       //Jewel of Bless\r\n" +
        "end\r\n";

    public DropsTestes()
    {
        Configuracao.Definir(new Configuracao { PastaServidor = pasta.Caminho });
        arquivo = pasta.Arquivo(@"Data\Item\ItemDrop.txt");
        File.WriteAllText(arquivo, Conteudo, Encoding.Latin1);
    }

    public void Dispose() => pasta.Dispose();

    [Fact]
    public void Le_as_regras()
    {
        var r = Drops.Load();
        Assert.Equal(3, r.Count);
        Assert.Equal(6672, r[1].Item);
        Assert.Equal("1", r[1].LevelMin);
        Assert.Equal("Scroll of Archangel 1", r[1].Comment);
        Assert.Equal("3", r[2].Map);
    }

    [Fact]
    public void Gravar_sem_mudar_nada_deixa_o_arquivo_identico()
    {
        Drops.Save(Drops.Load());
        Assert.Equal(Conteudo, File.ReadAllText(arquivo, Encoding.Latin1));
    }

    [Fact]
    public void So_a_linha_mudada_muda_e_a_nova_entra_antes_do_end()
    {
        var r = Drops.Load();
        r[1].Rate = Drops.ParsePercent("0,5");
        r.RemoveAt(0);
        r.Add(new DropRule { Item = 14 * 512 + 13, Monster = "354", Rate = 10_000, Comment = "Jewel of Bless - teste" });
        Drops.Save(r);
        var linhas = File.ReadAllText(arquivo, Encoding.Latin1).Split("\r\n");
        Assert.DoesNotContain(linhas, l => l.Contains("Jewel of Chaos"));
        Assert.Contains(linhas, l => l.StartsWith("6672") && l.Contains(" 5000 "));
        Assert.Equal("end", linhas[^2]);
        Assert.Contains("Jewel of Bless - teste", linhas[^3]);
        Assert.Contains(linhas, l => l == Conteudo.Split("\r\n")[4]);   // a regra que não mudou fica igual, byte a byte
    }

    [Fact]
    public void Gravar_duas_vezes_com_a_mesma_lista_funciona()
    {
        var r = Drops.Load();
        r[0].Rate = 200;
        Drops.Save(r);
        r[0].Rate = 300;
        Drops.Save(r);
        Assert.Equal(300, Drops.Load()[0].Rate);
        Assert.Equal(3, Drops.Load().Count);
    }

    [Fact]
    public void Duas_pessoas_editando_nao_se_misturam()
    {
        var a = Drops.Load();
        var b = Drops.Load();
        a[0].Rate = 111;
        Drops.Save(a);
        b[2].Rate = 222;
        // b foi lido antes da gravação de a: recusa em vez de apagar a mudança de a
        var e = Assert.Throws<InvalidOperationException>(() => Drops.Save(b));
        Assert.Contains("mudou no disco", e.Message);
        Assert.Equal(111, Drops.Load()[0].Rate);
    }

    [Fact]
    public void Arquivo_sem_end_nao_e_gravado()
    {
        File.WriteAllText(arquivo, Conteudo.Replace("end\r\n", ""), Encoding.Latin1);
        var r = Drops.Load();
        r.Add(new DropRule { Item = 6159, Rate = 1 });
        Assert.Throws<InvalidOperationException>(() => Drops.Save(r));
    }

    [Theory]
    [InlineData("1", 10_000)]
    [InlineData("0,5", 5_000)]
    [InlineData("0.1%", 1_000)]
    [InlineData("100", 1_000_000)]
    public void Porcentagem(string texto, int rate)
    {
        Assert.Equal(rate, Drops.ParsePercent(texto));
        Assert.Equal(texto.TrimEnd('%').Replace(',', '.'), Drops.Percent(rate));
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Porcentagem_invalida(string texto) => Assert.Throws<FormatException>(() => Drops.ParsePercent(texto));

    [Fact]
    public void Um_em_N()
    {
        Assert.Equal("1 em 100", DropsVisao.UmEm(10_000));
        Assert.Equal("nunca", DropsVisao.UmEm(0));
        Assert.Equal("0,5%", DropsVisao.Pct(5_000));
    }
}

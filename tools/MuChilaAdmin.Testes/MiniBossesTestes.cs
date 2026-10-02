using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Testes;

/// <summary>Mini-bosses recorrentes: invasão dedicada (slot 11+) no InvasionManager.dat + anúncio no Portuguese.xml.</summary>
public class MiniBossesTestes : IDisposable
{
    readonly PastaTemporaria pasta = new();
    readonly string invasao;
    readonly string lang;

    const string Invasion =
        "0\r\n" +
        "//Index   Year   Month   Day   DoW   Hour   Minute   Second\r\n" +
        "0         *      *       *     *     0      5        0\r\n" +
        "end\r\n\r\n" +
        "1\r\n" +
        "//Index   RespawnMessage   DespawnMessage   BossIndex   BossMessage   InvasionTime\r\n" +
        "0         192              *                55          202           600\r\n" +
        "end\r\n\r\n" +
        "2\r\n" +
        "//Index   Group   Map   Value\r\n" +
        "0         0       0     0\r\n" +
        "end\r\n\r\n" +
        "3\r\n" +
        "//Index   Group   Value   Class   Count   MapN   MapSX   MapSY   MapTX   MapTY   Element   RegenType   RegenTime\r\n" +
        "0         0       0       55      1       0      125     190     140     203     *\t0           0\r\n" +
        "end\r\n";

    const string Lang =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Language>\r\n\t<Message>\r\n\t\t<Msg ID=\"0\" Text=\"x\" />\r\n" +
        "\t</Message>\r\n\t<InvacionMsg>\r\n\t\t<Msg ID=\"192\" Text=\"comecou\" />\r\n\t</InvacionMsg>\r\n</Language>\r\n";

    public MiniBossesTestes()
    {
        Configuracao.Definir(new Configuracao { PastaServidor = pasta.Caminho });
        invasao = pasta.Arquivo(@"Data\Event\InvasionManager.dat");
        lang = pasta.Arquivo(@"Data\Lang\Portuguese.xml");
        File.WriteAllText(invasao, Invasion, Encoding.Latin1);
        File.WriteAllText(lang, Lang, Encoding.Latin1);
    }

    public void Dispose() => pasta.Dispose();

    static MiniBoss Exemplo(int slot = 11) => new(slot, "Aranha Gigante", 0, 443, 20, 0, false, true, 30);
    string TxtInvasao() => File.ReadAllText(invasao, Encoding.Latin1);
    string TxtLang() => File.ReadAllText(lang, Encoding.Latin1);

    [Fact]
    public void Salvar_poe_o_mini_boss_nos_quatro_blocos_sem_mexer_na_invasao_original()
    {
        MiniBosses.Salvar(Exemplo());
        var txt = TxtInvasao();
        Assert.Equal(4, Regex.Matches(txt, @"//MiniBoss#11(?!\d)").Count);   // uma linha em cada bloco
        Assert.Contains("0         192", txt);                                // invasão 0 (bloco 1) intacta
        Assert.Contains("55      1       0", txt);                            // spawn da invasão 0 (bloco 3) intacto
    }

    [Fact]
    public void Listar_le_todos_os_valores_de_volta()
    {
        MiniBosses.Salvar(Exemplo());
        var b = Assert.Single(MiniBosses.Listar());
        Assert.Equal(11, b.Slot);
        Assert.Equal("Aranha Gigante", b.Nome);
        Assert.Equal(0, b.Mapa);
        Assert.Equal(443, b.Monstro);
        Assert.Equal(20, b.Hora);
        Assert.Equal(0, b.Minuto);
        Assert.False(b.DuasVezesAoDia);
        Assert.True(b.Anuncia);
        Assert.Equal(30, b.DuracaoMinutos);
    }

    [Fact]
    public void Duas_vezes_ao_dia_gera_dois_horarios_na_agenda()
    {
        MiniBosses.Salvar(Exemplo() with { Hora = 8, DuasVezesAoDia = true });
        Assert.Equal(2, Regex.Matches(TxtInvasao(), @"11\s+\*\s+\*\s+\*\s+\*\s+(?:8|20)\s").Count);
        Assert.True(MiniBosses.Listar().Single().DuasVezesAoDia);
    }

    [Fact]
    public void Com_anuncio_escreve_as_mensagens_dentro_do_bloco_de_invasao_do_xml()
    {
        MiniBosses.Salvar(Exemplo());   // slot 11 → IDs 720 e 721
        var xml = TxtLang();
        Assert.Contains("ID=\"720\"", xml);
        Assert.Contains("Aranha Gigante apareceu em", xml);
        Assert.Contains("ID=\"721\"", xml);
        Assert.Contains("derrotou Aranha Gigante", xml);
        int pos = xml.IndexOf("ID=\"720\"", StringComparison.Ordinal);
        Assert.InRange(pos, xml.IndexOf("<InvacionMsg>", StringComparison.Ordinal), xml.IndexOf("</InvacionMsg>", StringComparison.Ordinal));
    }

    [Fact]
    public void Sem_anuncio_nao_escreve_no_xml()
    {
        MiniBosses.Salvar(Exemplo() with { Anuncia = false });
        Assert.DoesNotContain("ID=\"720\"", TxtLang());
        Assert.False(MiniBosses.Listar().Single().Anuncia);
    }

    [Fact]
    public void Salvar_de_novo_no_mesmo_slot_atualiza_sem_duplicar()
    {
        MiniBosses.Salvar(Exemplo());
        MiniBosses.Salvar(Exemplo() with { Mapa = 2, Monstro = 441, Nome = "Aranha Azul" });
        var b = Assert.Single(MiniBosses.Listar());
        Assert.Equal(2, b.Mapa);
        Assert.Equal(441, b.Monstro);
        Assert.Equal("Aranha Azul", b.Nome);
        Assert.Equal(4, Regex.Matches(TxtInvasao(), @"//MiniBoss#11(?!\d)").Count);
        Assert.Single(Regex.Matches(TxtLang(), "ID=\"720\""));   // XML não acumula
    }

    [Fact]
    public void Remover_tira_as_linhas_e_as_mensagens_mas_deixa_a_invasao_original()
    {
        MiniBosses.Salvar(Exemplo());
        MiniBosses.Remover(11);
        Assert.Empty(MiniBosses.Listar());
        Assert.DoesNotContain("MiniBoss#11", TxtInvasao());
        Assert.Contains("0         192", TxtInvasao());
        Assert.DoesNotContain("ID=\"720\"", TxtLang());
    }

    [Fact]
    public void Proximo_slot_comeca_em_11_e_anda_ao_ocupar()
    {
        Assert.Equal(11, MiniBosses.ProximoSlotLivre());
        MiniBosses.Salvar(Exemplo(11));
        Assert.Equal(12, MiniBosses.ProximoSlotLivre());
    }
}

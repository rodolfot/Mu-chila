using System.Text;

namespace MuChilaAdmin.Testes;

/// <summary>Issues #42 e #43: o que a invasão faz (lido do InvasionManager.dat) e o Moss Merchant achado no código do GameServer.</summary>
public class EventosTestes : IDisposable
{
    readonly PastaTemporaria pasta = new();

    // bloco 1/2/3 da invasão 9 do kit ("Demônios invocados") e da Páscoa (5), como no InvasionManager.dat
    const string Invasoes =
        "0\r\n//Index Year Month Day DoW Hour Minute Second\r\n9 * * * * 1 25 0\r\nend\r\n\r\n" +
        "1\r\n//Index RespawnMessage DespawnMessage BossIndex BossMessage InvasionTime Comment\r\n" +
        "5         197              *                *           *             600\t\t//Easter\r\n" +
        "9         210              *                *           *             600\t\t//Summon\r\nend\r\n\r\n" +
        "2\r\n//Index Group Map Value\r\n" +
        "9         0       8     0\r\n9         0       33    1\r\n9         0       80    2\r\nend\r\n\r\n" +
        "3\r\n//Index Group Value Class Count MapN MapSX MapSY MapTX MapTY Element RegenType RegenTime Comment\r\n" +
        "9         0       0       652     10      8      10      10      240     240     *\t   0           0           //Golden Goblin\r\n" +
        "9         0       1       652     10      33     10      10      240     240     *\t   0           0           //Golden Goblin\r\n" +
        "9         0       2       652     5       80     10      10      240     240     *\t   0           0           //Golden Goblin\r\nend\r\n";

    const string Monstro652 =
        "652       0      \"Golden Goblin\"                      100     120000    0         100         200         200       0              500          300           3           0            1             7           400         1400          3600        2           10         20          0              0              0             0             0             0             0             0             0             6                    1                  100                100                  200                  500                   300\r\n";

    public EventosTestes()
    {
        Configuracao.Definir(new Configuracao { PastaServidor = pasta.Caminho });
        File.WriteAllText(pasta.Arquivo(@"Data\Event\InvasionManager.dat"), Invasoes, Encoding.Latin1);
        File.WriteAllText(pasta.Arquivo(@"Data\Monster\Monster.txt"), Monstro652, Encoding.Latin1);
        foreach (var m in new[] { "008 - Tarkan", "033 - Aida", "080 - Karutan 1" })
            File.WriteAllText(pasta.Arquivo($@"Data\Monster\MonsterSetBase\{m}.xml"), "<MonsterSetBase />");
        File.WriteAllText(pasta.Arquivo(@"Data\Lang\Portuguese.xml"),
            "<Language><InvacionMsg><Msg ID=\"210\" Text=\"A invasão dos Demônios invocados começou!\" /></InvacionMsg></Language>", Encoding.Latin1);
    }

    public void Dispose() => pasta.Dispose();

    [Fact]
    public void Invasao_9_diz_monstro_mapas_sorteados_e_aviso()
    {
        var d = EventScheduler.InvasionDescription(9);
        Assert.Equal(2, d.Count);
        Assert.Equal("Aviso ao começar: \"A invasão dos Demônios invocados começou!\"; dura 600 s.", d[0]);
        Assert.Contains("sorteia UM destes", d[1]);
        Assert.Contains("Tarkan: 10× Golden Goblin (nível 100)", d[1]);
        Assert.Contains("Aida: 10× Golden Goblin (nível 100)", d[1]);
        Assert.Contains("Karutan 1: 5× Golden Goblin (nível 100)", d[1]);
    }

    [Fact]
    public void Invasao_sem_monstros_avisa()
    {
        var d = EventScheduler.InvasionDescription(5);   // só o bloco 1
        Assert.Single(d);
        Assert.StartsWith("Aviso ao começar: \"197\"", d[0]);   // mensagem que não está no XML de teste: mostra o número
        Assert.Contains("sem configuração", Assert.Single(EventScheduler.InvasionDescription(3)));
    }

    /// <summary>Bytes reais do Game Server S14.exe desembrulhado, a partir de 0x4B9F80 (início da rotina que abre o Moss).</summary>
    const string MossReal =
        "558BEC83E4F883EC08833D0897E502007F586A0068D00000006A006A006A006A006A006A0068588C9F00E8F181000083C424C7050497E50202000000" +
        "B90097E502E80A020000A10422B9006A00A30897E502FF154882520083C4048954240403050897E502A30C97E5028BE55DC3CCCC";

    [Fact]
    public void Acha_o_objeto_do_Moss_pelo_codigo()
    {
        var lixo = new byte[500]; new Random(43).NextBytes(lixo);
        var img = lixo.Concat(Convert.FromHexString(MossReal)).Concat(lixo).ToArray();
        Assert.Equal(0x2E59700u, ResetWatcher.FindMossBase(img));
    }

    [Fact]
    public void Moss_nao_confunde_sem_a_troca_de_estado()
    {
        // sem o "mov dword [estado],2" logo depois da mensagem 208 não é a rotina do Moss
        var outro = Convert.FromHexString(MossReal.Replace("C7050497E50202000000", "C7050497E50203000000"));
        Assert.Null(ResetWatcher.FindMossBase(outro));
    }
}

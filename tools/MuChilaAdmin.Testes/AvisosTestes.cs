using System.Text;

namespace MuChilaAdmin.Testes;

/// <summary>Issue #44: "Enviar agora" repetia as mensagens anteriores (ainda no Notice.txt, com RepeatTime 1).</summary>
public class AvisosTestes : IDisposable
{
    readonly PastaTemporaria pasta = new();
    readonly string arquivo;

    public AvisosTestes()
    {
        Configuracao.Definir(new Configuracao { PastaServidor = pasta.Caminho });
        arquivo = pasta.Arquivo(@"Data\Util\Notice.txt");
        File.WriteAllText(arquivo,
            "//Message Type Count Opacity Delay Red Green Blue Speed RepeatTime\r\n" +
            "\"Delicia\"                                                              0      0       0         0       0     0       0      0       1   //MuChilaAdmin-agora 2026-10-01 22:37:10\r\n" +
            "\"Bônus de EXP ativo\"                                                   0      0       0         0       0     0       0      0       300   //MuChilaAdmin-bonus 1\r\n" +
            "\"Garanta já o seu Pass e acesse todos os mapas +400!\"                  0      0       0         0       0     0       0      0       600\r\n" +
            "end", Encoding.Latin1);
    }

    public void Dispose() => pasta.Dispose();

    [Fact]
    public void Enviar_agora_tira_os_anteriores_e_mantem_bonus_e_avisos()
    {
        Notices.AddOneShot("Oi amores");
        var linhas = File.ReadAllText(arquivo, Encoding.Latin1).Split("\r\n").Where(l => l.StartsWith('"')).ToList();
        Assert.Equal(3, linhas.Count);
        Assert.StartsWith("\"Oi amores\"", linhas[0]);   // o novo vai para o topo: é o primeiro que o servidor manda
        Assert.DoesNotContain(linhas, l => l.StartsWith("\"Delicia\""));
        Assert.Contains(linhas, l => l.StartsWith("\"Bônus de EXP ativo\""));
        Assert.Contains(linhas, l => l.StartsWith("\"Garanta já"));
        Assert.Single(Notices.Load(), n => n.IsOneShot);
    }
}

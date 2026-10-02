using System.Text;

namespace MuChilaAdmin.Testes;

/// <summary>Issue #41: o Lang.mpr do cliente (ZIP + ZipCrypto + XOR 20 13 77) abre e é remontado sem perder nada.</summary>
public class LangPackTestes : IDisposable
{
    readonly PastaTemporaria pasta = new();
    public void Dispose() => pasta.Dispose();

    static readonly Encoding W1252 = Encoding.Latin1;

    static List<LangPack.Entry> Exemplo() => new()
    {
        new(@"\por\Text(por).txt", W1252.GetBytes("//index\r\n81\t\"Falling Slash skill (Mana:%d)\"\r\n82\t\"Ataque em ação\"\r\n"), 0x48B2, 0x4F02, 0x20),
        new(@"\eng\Text(eng).txt", W1252.GetBytes("//index\r\n81\t\"Falling Slash skill (Mana:%d)\"\r\n"), 0x48B2, 0x4F02, 0x20),
        new(@"\Gate.txt", new byte[0], 0x48B2, 0x4F02, 0x20),
    };

    [Fact]
    public void Montar_e_abrir_devolve_o_mesmo_conteudo()
    {
        var bytes = LangPack.Write(Exemplo());
        // a 1ª camada é o XOR: o arquivo não começa com "PK" e sim com "PK" ^ 20 13 77
        Assert.Equal(0x50 ^ 0x20, bytes[0]);
        Assert.Equal(0x4B ^ 0x13, bytes[1]);
        var lido = LangPack.Read(bytes);
        Assert.Equal(Exemplo().Select(e => e.Name), lido.Select(e => e.Name));
        Assert.All(lido.Zip(Exemplo()), x => Assert.Equal(x.Second.Data, x.First.Data));
        Assert.Equal(0x20u, lido[0].ExternalAttributes);
    }

    [Fact]
    public void Montar_troca_so_o_que_esta_na_pasta()
    {
        var original = pasta.Arquivo("Lang.mpr");
        File.WriteAllBytes(original, LangPack.Write(Exemplo()));
        var textos = Path.Combine(pasta.Caminho, "textos");
        Assert.Equal(3, LangPack.Extract(original, textos));
        File.WriteAllBytes(Path.Combine(textos, "por", "Text(por).txt"), W1252.GetBytes("81\t\"Golpe Descendente (Mana:%d)\"\r\n"));
        File.Delete(Path.Combine(textos, "eng", "Text(eng).txt"));   // ausente na pasta: fica como no original

        var novo = pasta.Arquivo("Lang-novo.mpr");
        var trocados = LangPack.Pack(original, textos, novo);
        Assert.Equal(new[] { @"\por\Text(por).txt" }, trocados);
        var lido = LangPack.Read(novo);
        Assert.Equal("81\t\"Golpe Descendente (Mana:%d)\"\r\n", W1252.GetString(lido[0].Data));
        Assert.Equal(Exemplo()[1].Data, lido[1].Data);
    }

    [Fact]
    public void Arquivo_com_senha_errada_ou_estragado_nao_abre()
    {
        var bytes = LangPack.Write(Exemplo());
        bytes[60] ^= 0xFF;   // um byte do conteúdo cifrado da 1ª entrada
        Assert.ThrowsAny<Exception>(() => LangPack.Read(bytes));
    }

    [Fact]
    public void Termina_com_a_soma_que_o_jogo_confere()
    {
        // o jogo não carrega texto nenhum sem os 4 bytes finais (testado com o Lang.mpr original em 02/10/2026)
        var bytes = LangPack.Write(Exemplo());
        Assert.Equal(LangPack.Checksum(bytes.AsSpan(0, bytes.Length - 4)), BitConverter.ToUInt32(bytes, bytes.Length - 4));
        Assert.ThrowsAny<Exception>(() => LangPack.Read(bytes[..^4]));
        // a soma do MU com a chave do Lang.mpr (0x12DC): (0x12DC << 9) ^ dw0, e no 1º bloco mistura com >> 1
        var quatro = new byte[] { 1, 0, 0, 0 };
        uint r = (0x12DCu << 9) ^ 1u; r ^= (0x12DCu + r) >> 1;
        Assert.Equal(r, LangPack.Checksum(quatro));
    }
}

namespace MuChilaAdmin.Testes;

/// <summary>
/// Issue #35: os pontos onde o GameServer monta os bits de estágio da classe (estágio 0 sai como 2ª classe). Os trechos são
/// os bytes reais do Game Server S14.exe desembrulhado na memória (12 bytes antes do "cmp r8,2" e 4 depois do "cmove").
/// </summary>
public class VigiaClasseTestes
{
    static readonly string[] TrechosReais =
    {
        "80F9017507BA08000000EB1C80F9027507BA0C000000EB1080F903BA08000000B90E0000000F44D10AC233F6",   // 0x45DBA2 lista de personagens
        "80FA017507B808000000EB1C80FA027507B80C000000EB1080FA03B808000000BB0E0000000F44C3F30F6F46",   // 0x45E41A criação
        "80FA017507B908000000EB1C80FA027507B90C000000EB1080FA03B908000000BE0E0000000F44CE0AC18887",   // 0x4C83C3 aparência
        "80F9017507B808000000EB1C80F9027507B80C000000EB1080F903B808000000B90E0000000F44C10FB6C80F",   // 0x4E6C63 entrada no jogo
    };

    /// <summary>Os 4 trechos separados por bytes quaisquer, como num pedaço do executável.</summary>
    static byte[] Imagem()
    {
        var r = new List<byte>();
        var lixo = new Random(35);
        foreach (var t in TrechosReais)
        {
            var b = new byte[300]; lixo.NextBytes(b); r.AddRange(b);
            r.AddRange(Convert.FromHexString(t));
        }
        return r.ToArray();
    }

    [Fact]
    public void Acha_os_4_pontos_originais_no_mov_08()
    {
        var img = Imagem();
        var pontos = ResetWatcher.FindInitialClassSites(img);
        Assert.Equal(4, pontos.Count);
        Assert.All(pontos, p => Assert.False(p.Patched));
        // o imm32 apontado é o 08 do "mov r32,08h" que vem depois do "cmp r8,3"
        Assert.All(pontos, p =>
        {
            Assert.Equal(8u, BitConverter.ToUInt32(img, p.ImmAt));
            Assert.Equal(0x03, img[p.ImmAt - 2]);
            Assert.Equal(0x0Eu, BitConverter.ToUInt32(img, p.ImmAt + 5));
        });
    }

    [Fact]
    public void Depois_de_corrigido_reconhece_e_nao_conta_de_novo()
    {
        var img = Imagem();
        foreach (var p in ResetWatcher.FindInitialClassSites(img)) BitConverter.GetBytes(0u).CopyTo(img, p.ImmAt);
        var depois = ResetWatcher.FindInitialClassSites(img);
        Assert.Equal(4, depois.Count);
        Assert.All(depois, p => Assert.True(p.Patched));
    }

    [Fact]
    public void Nao_confunde_com_trechos_parecidos()
    {
        // 2ª classe com outro valor (não é 08 nem 00): versão diferente, não mexe
        var outro = Convert.FromHexString(TrechosReais[0].Replace("80F903BA08000000", "80F903BA09000000"));
        Assert.Empty(ResetWatcher.FindInitialClassSites(outro));
        // "cmp" do estágio 3 com outro registrador que o do estágio 2: não é a mesma sequência
        var registro = Convert.FromHexString(TrechosReais[0].Replace("EB1080F903", "EB1080FA03"));
        Assert.Empty(ResetWatcher.FindInitialClassSites(registro));
        // sem o 0Eh da 4ª classe
        var sem4a = Convert.FromHexString(TrechosReais[0].Replace("B90E000000", "B90F000000"));
        Assert.Empty(ResetWatcher.FindInitialClassSites(sem4a));
    }
}

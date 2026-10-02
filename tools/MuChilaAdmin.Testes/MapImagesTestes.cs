namespace MuChilaAdmin.Testes;

/// <summary>Issue #46: a imagem do mapa da tecla Tab (.OZP = PNG com 4 bytes na frente).</summary>
public class MapImagesTestes
{
    static readonly byte[] Assinatura = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void Ozp_vira_png_sem_os_4_bytes_da_frente()
    {
        var png = Assinatura.Concat(new byte[] { 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }).ToArray();
        var ozp = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }.Concat(png).ToArray();   // como o Navimap01.OZP do cliente
        Assert.Equal(png, MapImages.Png(ozp));
    }

    [Fact]
    public void Arquivo_que_nao_e_png_nao_vira_imagem()
    {
        Assert.Null(MapImages.Png(new byte[16]));
        Assert.Null(MapImages.Png(Assinatura));   // curto demais
    }

    [Theory]
    [InlineData(0, "Navimap01.OZP")]
    [InlineData(51, "Navimap52.OZP")]
    [InlineData(99, "Navimap100.OZP")]
    public void Nome_do_arquivo_e_o_mapa_mais_1(int mapa, string arquivo) => Assert.EndsWith(@"NaviMap\" + arquivo, MapImages.Arquivo(mapa));
}

using System.IO;

namespace MuChilaAdmin.Core;

/// <summary>
/// Imagem do mapa da tecla Tab, para desenhar os spawns em cima (issue #46). Fica no cliente em
/// Data\Interface\GFx\NaviMap\NavimapNN.OZP, com NN = número do mapa + 1 (Navimap01 = Lorencia, Navimap52 = Elbeland,
/// Navimap100 = mapa 99). O .OZP é um PNG com 4 bytes na frente; 512×512, 2 pixels por posição do mapa (256×256).
/// Orientação conferida com os NPCs de Lorencia e Devias: x para a direita e y para CIMA (pixel = 2x, 511 − 2y).
/// Nem todo mapa tem imagem (os de evento, por exemplo).
/// </summary>
public static class MapImages
{
    public static string Arquivo(int mapa) => Path.Combine(CashShop.ClientDir, @"Data\Interface\GFx\NaviMap", $"Navimap{mapa + 1:00}.OZP");

    /// <summary>O PNG do mapa, ou null se o cliente não tiver imagem para ele.</summary>
    public static byte[]? Png(int mapa)
    {
        var f = Arquivo(mapa);
        if (mapa < 0 || !File.Exists(f)) return null;
        var b = File.ReadAllBytes(f);
        return Png(b);
    }

    /// <summary>Tira os 4 bytes do .OZP; null se não for um PNG.</summary>
    public static byte[]? Png(byte[] ozp) =>
        ozp.Length > 12 && ozp[4] == 0x89 && ozp[5] == (byte)'P' && ozp[6] == (byte)'N' && ozp[7] == (byte)'G' ? ozp[4..] : null;
}

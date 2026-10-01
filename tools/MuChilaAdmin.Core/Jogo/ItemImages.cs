using System.IO;

namespace MuChilaAdmin.Core;

/// <summary>
/// Fotos dos itens: as do MuEditor do kit (1 - MuEditor\Item\{índice}-{seção}.jpg, ~30×60). Cobrem ~42% dos itens do
/// servidor (1.050 de 2.508; faltam os do S9 em diante e toda a seção 16). O painel web serve o arquivo como está
/// (image/jpeg); sem foto = null e a tela mostra o ícone do item.
/// </summary>
public static class ItemImages
{
    static string Dir => Path.Combine(ServerControl.ServerRoot, @"1 - MuEditor\Item");

    /// <summary>Caminho da foto do item, ou null se o MuEditor não tem.</summary>
    public static string? Arquivo(int section, int type)
    {
        if (section is < 0 or > 31 || type is < 0 or > 511) return null;
        var f = Path.Combine(Dir, $"{type}-{section}.jpg");
        return File.Exists(f) ? f : null;
    }
}

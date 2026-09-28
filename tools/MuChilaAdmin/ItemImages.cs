using System.IO;

namespace MuChilaAdmin;

/// <summary>
/// Fotos dos itens: as do MuEditor do kit (1 - MuEditor\Item\{índice}-{seção}.jpg, ~30×60). Cobrem ~42% dos itens do
/// servidor (1.050 de 2.508; faltam os do S9 em diante e toda a seção 16). Sem foto = null.
/// </summary>
public static class ItemImages
{
    static string Dir => Path.Combine(ServerControl.ServerRoot, @"1 - MuEditor\Item");
    static readonly Dictionary<(int, int), Image?> cache = new();

    public static Image? Get(int section, int type)
    {
        if (cache.TryGetValue((section, type), out var img)) return img;
        var f = Path.Combine(Dir, $"{type}-{section}.jpg");
        try
        {
            // carrega numa cópia em memória para não travar o arquivo
            if (File.Exists(f)) using (var tmp = Image.FromFile(f)) img = new Bitmap(tmp);
        }
        catch { img = null; }
        return cache[(section, type)] = img;
    }
}

/// <summary>Quadro com a foto do item e o nome embaixo; "sem imagem" quando o MuEditor não tem.</summary>
public sealed class ItemPicture : Panel
{
    readonly PictureBox pic = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 28, 32) };
    readonly Label caption = new() { Dock = DockStyle.Bottom, Height = 34, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8) };

    public ItemPicture(int width = 120)
    {
        Dock = DockStyle.Right; Width = width; Padding = new Padding(4); BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(pic); Controls.Add(caption);
        Show(null, null);
    }

    public void Show(int? section, int? type, string? name = null)
    {
        var img = section is int s && type is int t ? ItemImages.Get(s, t) : null;
        pic.Image = img;
        caption.Text = section == null ? "" : img == null ? $"{name}\n(sem imagem)" : name ?? "";
    }
}

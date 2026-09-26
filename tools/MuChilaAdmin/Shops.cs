using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin;

/// <summary>Uma linha de loja: Section Type Level Dur Skill Luck Option Excellent (formato de Data\Shop\NNN - Nome.txt).</summary>
public class ShopItem
{
    public int Section { get; set; }
    public int Type { get; set; }
    public int Level { get; set; }
    public int Dur { get; set; }
    public int Skill { get; set; }
    public int Luck { get; set; }
    public int Option { get; set; }
    public int Excellent { get; set; }
    public ShopItem Clone() => (ShopItem)MemberwiseClone();
}

public record ShopInfo(int Index, int MonsterClass, string Name, string File);

/// <summary>Nome e tamanho (largura × altura na grade) de um item do Item.txt.</summary>
public record ItemDef(int Section, int Type, string Name, int Width, int Height);

/// <summary>Lojas dos NPCs: Data\ShopManager.txt (Index MonsterClass //Nome) e um arquivo por loja em Data\Shop.</summary>
public static class Shops
{
    public const int GridWidth = 8, GridHeight = 15;   // janela de loja do jogo: 8 colunas × 15 linhas
    static readonly Encoding Latin1 = Encoding.Latin1;
    static string DataDir => Path.Combine(ServerControl.ServerRoot, "Data");
    static Dictionary<(int, int), ItemDef>? catalog;

    public static List<ShopInfo> List()
    {
        var list = new List<ShopInfo>();
        foreach (var line in File.ReadAllLines(Path.Combine(DataDir, "ShopManager.txt"), Latin1))
        {
            var m = Regex.Match(line, @"^\s*(\d+)\s+(\d+)\s*(?://\s*(.*))?$");
            if (!m.Success) continue;
            int index = int.Parse(m.Groups[1].Value);
            var file = Directory.GetFiles(Path.Combine(DataDir, "Shop"), $"{index:000} - *.txt").FirstOrDefault();
            if (file == null) continue;
            list.Add(new ShopInfo(index, int.Parse(m.Groups[2].Value), m.Groups[3].Value.Trim(), file));
        }
        return list;
    }

    public static List<ShopItem> Load(ShopInfo shop)
    {
        var items = new List<ShopItem>();
        foreach (var line in File.ReadAllLines(shop.File, Latin1))
        {
            var t = line.Trim();
            if (t.StartsWith("end", StringComparison.OrdinalIgnoreCase)) break;
            var m = Regex.Match(t, @"^(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)");
            if (!m.Success) continue;
            var v = m.Groups.Values.Skip(1).Select(g => int.Parse(g.Value)).ToArray();
            items.Add(new ShopItem { Section = v[0], Type = v[1], Level = v[2], Dur = v[3], Skill = v[4], Luck = v[5], Option = v[6], Excellent = v[7] });
        }
        return items;
    }

    /// <summary>Grava a loja: mantém os comentários do começo do arquivo, troca os itens e fecha com "end". Faz backup antes.</summary>
    public static void Save(ShopInfo shop, IReadOnlyList<ShopItem> items)
    {
        foreach (var it in items)
        {
            // itens que não existem no Item.txt (o kit tem alguns nas lojas) passam: a tela mostra "(item x,y não existe)" para o dono tirar
            if (it.Level is < 0 or > 15 || it.Dur is < 0 or > 255 || it.Skill is < 0 or > 1 || it.Luck is < 0 or > 1 || it.Option is < 0 or > 7 || it.Excellent is < 0 or > 63)
                throw new InvalidOperationException($"Valor fora do permitido em {Name(it.Section, it.Type)} (nível 0-15, dur 0-255, skill/sorte 0-1, opção 0-7, excelente 0-63); nada gravado.");
        }
        var text = File.ReadAllText(shop.File, Latin1);
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var head = new List<string>();
        foreach (var line in Regex.Split(text, "\r?\n"))
        {
            var t = line.Trim();
            if (Regex.IsMatch(t, @"^\d") || t.StartsWith("end", StringComparison.OrdinalIgnoreCase)) break;
            head.Add(line);
        }
        while (head.Count > 0 && head[^1].Trim().Length == 0) head.RemoveAt(head.Count - 1);
        if (!head.Any(l => l.TrimStart().StartsWith("//Section"))) head.Add("//Section   Type   Level   Dur   Skill   Luck   Option   Excellent");
        var body = items.Select(i => $"{i.Section,-11} {i.Type,-6} {i.Level,-7} {i.Dur,-5} {i.Skill,-7} {i.Luck,-6} {i.Option,-8} {i.Excellent}");
        var bak = $"{shop.File}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Copy(shop.File, bak);
        File.WriteAllText(shop.File, string.Join(newline, head.Concat(body).Append("end")) + newline, Latin1);
    }

    /// <summary>Encaixa os itens na grade 8×15 como o servidor (primeiro espaço livre, da esquerda para a direita, de cima para baixo).
    /// Devolve os que não cabem (esses não aparecem na loja) e quantas células ficaram ocupadas.</summary>
    public static (List<ShopItem> DoNotFit, int UsedCells) Fit(IEnumerable<ShopItem> items)
    {
        var grid = new bool[GridHeight, GridWidth];
        var fora = new List<ShopItem>(); int used = 0;
        foreach (var it in items)
        {
            var def = Find(it.Section, it.Type);
            int w = def?.Width ?? 1, h = def?.Height ?? 1;
            bool placed = false;
            for (int y = 0; y + h <= GridHeight && !placed; y++)
                for (int x = 0; x + w <= GridWidth && !placed; x++)
                {
                    bool free = true;
                    for (int dy = 0; dy < h && free; dy++) for (int dx = 0; dx < w && free; dx++) free = !grid[y + dy, x + dx];
                    if (!free) continue;
                    for (int dy = 0; dy < h; dy++) for (int dx = 0; dx < w; dx++) grid[y + dy, x + dx] = true;
                    placed = true; used += w * h;
                }
            if (!placed) fora.Add(it);
        }
        return (fora, used);
    }

    // ---------- catálogo do Item.txt ----------
    public static IReadOnlyCollection<ItemDef> Catalog() => LoadCatalog().Values;
    public static ItemDef? Find(int section, int type) => LoadCatalog().TryGetValue((section, type), out var d) ? d : null;
    public static string Name(int section, int type) => Find(section, type)?.Name ?? $"(item {section},{type} não existe)";

    static Dictionary<(int, int), ItemDef> LoadCatalog()
    {
        if (catalog != null) return catalog;
        var dict = new Dictionary<(int, int), ItemDef>();
        int section = -1;
        foreach (var line in File.ReadAllLines(Path.Combine(DataDir, @"Item\Item.txt"), Latin1))
        {
            var t = line.Trim();
            if (Regex.IsMatch(t, @"^\d+$")) { section = int.Parse(t); continue; }
            // Index Slot Skill Width Height HaveSerial HaveOption DropItem "Nome" ...
            var m = Regex.Match(t, @"^(\d+)\s+(?:-?\d+|\*)\s+\d+\s+(\d+)\s+(\d+)\s+\d+\s+\d+\s+\d+\s+""([^""]+)""");   // Slot pode ser "*"
            if (m.Success && section >= 0)
                dict.TryAdd((section, int.Parse(m.Groups[1].Value)), new ItemDef(section, int.Parse(m.Groups[1].Value), m.Groups[4].Value.Trim(), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)));
        }
        return catalog = dict;
    }
}

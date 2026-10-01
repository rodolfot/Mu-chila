using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MuChilaAdmin.Core;

/// <summary>Densidade de um mapa: pontos de spawn de área e total de monstros (soma dos Quantity da seção SPOT).</summary>
public record MapDensity(int Code, string Name, string File, int Pontos, int Total);

/// <summary>Um spawn de área de monstro (seção SPOT): retângulo BeginPos..EndPos, quantidade e raio de perseguição.</summary>
public class MonsterSpawn
{
    public int Class, BeginX, BeginY, EndX, EndY, Quantity, Range = 3;
    public string Name = "";
    public MonsterSpawn Clone() => (MonsterSpawn)MemberwiseClone();
}

/// <summary>
/// Respawn dos monstros: Data\Monster\MonsterSetBase\NNN - Mapa.xml. Mexe SÓ na seção &lt;SPOT&gt; (spawns de área,
/// os de farm); NPC, MONSTER (fixos) e EVENT ficam intactos. Total de monstros = soma dos Quantity — base da densidade.
/// </summary>
public static class Monsters
{
    public const int MapTiles = 256;   // mapas do MU são 256x256
    static string Dir => Path.Combine(ServerControl.ServerRoot, @"Data\Monster\MonsterSetBase");
    static readonly Regex FileName = new(@"^(\d+)\s*-\s*(.+)\.xml$", RegexOptions.IgnoreCase);

    public static List<MapDensity> Maps()
    {
        var list = new List<MapDensity>();
        foreach (var f in Directory.GetFiles(Dir, "*.xml"))
        {
            var m = FileName.Match(Path.GetFileName(f));
            if (!m.Success) continue;
            try
            {
                var spawns = LoadFrom(XDocument.Load(f));
                list.Add(new MapDensity(int.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim(), f, spawns.Count, spawns.Sum(s => s.Quantity)));
            }
            catch { /* arquivo com problema: ignora na lista */ }
        }
        return list.OrderBy(x => x.Code).ToList();
    }

    /// <summary>Código → nome do mapa, só pelos nomes dos arquivos (rápido; não lê os spawns).</summary>
    public static Dictionary<int, string> NomesDosMapas()
    {
        var d = new Dictionary<int, string>();
        if (!Directory.Exists(Dir)) return d;
        foreach (var f in Directory.GetFiles(Dir, "*.xml"))
            if (FileName.Match(Path.GetFileName(f)) is { Success: true } m) d.TryAdd(int.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim());
        return d;
    }

    /// <summary>Classe do monstro → mapas onde ele nasce (spawns de área da seção SPOT), lendo cada arquivo uma vez.</summary>
    public static Dictionary<int, HashSet<int>> MapasPorMonstro()
    {
        var d = new Dictionary<int, HashSet<int>>();
        if (!Directory.Exists(Dir)) return d;
        foreach (var f in Directory.GetFiles(Dir, "*.xml"))
        {
            if (FileName.Match(Path.GetFileName(f)) is not { Success: true } m) continue;
            int mapa = int.Parse(m.Groups[1].Value);
            List<MonsterSpawn> spawns;
            try { spawns = Load(f); }
            catch { continue; }   // arquivo com problema: fica de fora, como na lista de mapas
            foreach (var s in spawns)
            {
                if (!d.TryGetValue(s.Class, out var set)) d[s.Class] = set = new();
                set.Add(mapa);
            }
        }
        return d;
    }

    public static List<MonsterSpawn> Load(string file) => LoadFrom(XDocument.Load(file));

    static List<MonsterSpawn> LoadFrom(XDocument doc)
    {
        var list = new List<MonsterSpawn>();
        var monster = doc.Root?.Element("SPOT");
        if (monster == null) return list;
        foreach (var c in monster.Elements("Config"))
        {
            if (c.Attribute("BeginPosX") == null) continue;   // só spawns de área (ignora fixos por PositionX)
            int At(string a, int d = 0) => int.TryParse((string?)c.Attribute(a), out var v) ? v : d;
            var s = new MonsterSpawn
            {
                Class = At("Class"), Range = At("Range", 3),
                BeginX = At("BeginPosX"), BeginY = At("BeginPosY"),
                EndX = At("EndPosX"), EndY = At("EndPosY"), Quantity = At("Quantity"),
            };
            // nome: comentário logo após o Config (<!-- "Giant" ... -->)
            var cm = c.NodesAfterSelf().OfType<XComment>().FirstOrDefault();
            var nm = cm != null ? Regex.Match(cm.Value, "\"([^\"]+)\"") : Match.Empty;
            s.Name = nm.Success ? nm.Groups[1].Value : $"Classe {s.Class}";
            list.Add(s);
        }
        return list;
    }

    /// <summary>Regrava os spawns de área da seção SPOT; mantém NPC/SPOT/EVENT e os fixos. Faz backup antes.</summary>
    public static void Save(string file, IReadOnlyList<MonsterSpawn> spawns)
    {
        foreach (var s in spawns)
            if (s.Quantity is < 0 or > 500 || s.BeginX is < 0 or > 255 || s.EndX is < 0 or > 255 || s.BeginY is < 0 or > 255 || s.EndY is < 0 or > 255)
                throw new InvalidOperationException($"Valores fora do intervalo em {s.Name} (quantidade 0-500, posições 0-255); nada gravado.");

        var doc = XDocument.Load(file);
        var monster = doc.Root?.Element("SPOT") ?? throw new InvalidOperationException("Seção SPOT não encontrada.");
        // tira os spawns de área antigos (e o comentário logo em seguida) da SPOT; preserva qualquer fixo (PositionX)
        foreach (var c in monster.Elements("Config").Where(c => c.Attribute("BeginPosX") != null).ToList())
        {
            if (c.NextNode is XComment cm) cm.Remove();
            c.Remove();
        }
        // adiciona os spawns novos (com um comentário do nome)
        foreach (var s in spawns)
        {
            var cfg = new XElement("Config",
                new XAttribute("Class", s.Class), new XAttribute("Range", s.Range),
                new XAttribute("BeginPosX", s.BeginX), new XAttribute("BeginPosY", s.BeginY),
                new XAttribute("EndPosX", s.EndX), new XAttribute("EndPosY", s.EndY),
                new XAttribute("Direction", -1), new XAttribute("Quantity", s.Quantity), new XAttribute("Element", 0));
            monster.Add(cfg);
            monster.Add(new XComment($" \"{s.Name}\" (Mu Chila) "));
        }
        File.Copy(file, $"{file}.bak-{DateTime.Now:yyyyMMdd-HHmmss}");
        doc.Save(file);
    }
}

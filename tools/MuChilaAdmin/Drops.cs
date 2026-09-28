using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin;

/// <summary>
/// Uma regra do Data\Item\ItemDrop.txt: o item cai de monstros que se encaixam no mapa, na classe e na faixa de nível,
/// com a chance Rate em 1.000.000 (10000 = 1%). "*" = qualquer; os campos ficam em texto para aceitar o "*".
/// </summary>
public class DropRule
{
    public int Item;   // seção × 512 + índice
    public string Level = "0", Grade = "0", Duration = "0", Map = "*", Monster = "*", LevelMin = "*", LevelMax = "*";
    public string[] Options = { "*", "*", "*", "*", "*", "*", "*" };
    public int Rate;
    public string Comment = "";
    internal string? Raw, RawKey;   // linha original e os campos lidos dela: regra sem mudança é regravada igual

    public int Section => Item / 512;
    public int Type => Item % 512;
    internal string Key() => string.Join("|", new[] { Item.ToString(), Level, Grade }.Concat(Options)
        .Concat(new[] { Duration, Map, Monster, LevelMin, LevelMax, Rate.ToString(), Comment }));
}

/// <summary>Taxas de drop comum de um monstro no Monster.txt: ItemRate (item comum), MoneyRate (zen) e MaxItemLevel.</summary>
public record MonsterRate(int Index, string Name, int Level, int ItemRate, int MoneyRate, int MaxItemLevel);

/// <summary>
/// Drops dos monstros. ItemDrop.txt: regras de item por monstro/mapa/nível (Reload Item). Monster.txt: taxas de drop comum
/// e zen por monstro (Reload Monster, só nos GameServers e fora da invasão). Linhas que não mudaram são regravadas iguais;
/// comentários, cabeçalho e o "end" ficam no lugar. Backup .bak-* ao lado do arquivo a cada gravação.
/// </summary>
public static class Drops
{
    public static string DropFile => Path.Combine(ServerControl.ServerRoot, @"Data\Item\ItemDrop.txt");
    public static string MonsterFile => Path.Combine(ServerControl.ServerRoot, @"Data\Monster\Monster.txt");
    static readonly Encoding Enc = Encoding.Latin1;
    public const int RateBase = 1_000_000;

    // layout do ItemDrop.txt lido por último: cada linha é texto (comentário, "end"...) ou uma regra
    static List<object>? layout;
    static string? loadedText;

    public static List<DropRule> Load()
    {
        var text = File.ReadAllText(DropFile, Enc);
        var lines = text.Split("\r\n");
        var lay = new List<object>(); var rules = new List<DropRule>();
        foreach (var line in lines)
        {
            var r = Parse(line);
            if (r == null) { lay.Add(line); continue; }
            lay.Add(r); rules.Add(r);
        }
        layout = lay; loadedText = text;
        return rules;
    }

    static DropRule? Parse(string line)
    {
        int c = line.IndexOf("//", StringComparison.Ordinal);
        var body = c >= 0 ? line[..c] : line;
        var t = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (t.Length != 16 || !int.TryParse(t[0], out var item) || !int.TryParse(t[15], out var rate)) return null;
        var r = new DropRule
        {
            Item = item, Level = t[1], Grade = t[2], Options = t[3..10], Duration = t[10], Map = t[11], Monster = t[12],
            LevelMin = t[13], LevelMax = t[14], Rate = rate, Comment = c >= 0 ? line[(c + 2)..] : "", Raw = line,
        };
        r.RawKey = r.Key();
        return r;
    }

    // larguras das colunas do kit (Index, Level, Grade, Option0-6, Duration, MapNumber, MonsterClass, LevelMin, LevelMax, DropRate)
    static readonly int[] Widths = { 10, 8, 8, 10, 10, 10, 10, 10, 10, 10, 11, 12, 15, 18, 18, 11 };

    static string Format(DropRule r)
    {
        var v = new[] { r.Item.ToString(), r.Level, r.Grade }.Concat(r.Options)
            .Concat(new[] { r.Duration, r.Map, r.Monster, r.LevelMin, r.LevelMax, r.Rate.ToString() }).ToArray();
        var sb = new StringBuilder();
        for (int i = 0; i < v.Length; i++) sb.Append(v[i].Length >= Widths[i] ? v[i] + " " : v[i].PadRight(Widths[i]));
        if (r.Comment.Length > 0) sb.Append("//").Append(r.Comment);
        return sb.ToString().TrimEnd();
    }

    /// <summary>Confere um campo: "*" ou inteiro dentro do limite.</summary>
    public static bool Valid(string v, int min, int max) => v == "*" || (int.TryParse(v, out var n) && n >= min && n <= max);

    /// <summary>Grava as regras. As que saíram da lista somem; as novas entram antes do "end". Devolve o caminho do backup.</summary>
    public static string Save(IReadOnlyList<DropRule> rules)
    {
        if (layout == null || loadedText == null) throw new InvalidOperationException("Leia as regras antes de gravar.");
        if (File.ReadAllText(DropFile, Enc) != loadedText)
            throw new InvalidOperationException("O ItemDrop.txt mudou no disco desde que foi aberto (outro programa?). Clique em \"Descartar\" para ler de novo.");
        foreach (var r in rules)
        {
            if (r.Item < 0 || r.Item >= 16 * 512) throw new InvalidOperationException($"Código de item inválido: {r.Item}.");
            if (r.Rate < 0 || r.Rate > RateBase) throw new InvalidOperationException($"Chance inválida em {r.Comment}: use de 0 a 100%.");
            if (r.Comment.Contains('\r') || r.Comment.Contains('\n')) throw new InvalidOperationException("Comentário com quebra de linha.");
        }
        var keep = new HashSet<DropRule>(rules, ReferenceEqualityComparer.Instance);
        var placed = new HashSet<DropRule>(ReferenceEqualityComparer.Instance);
        var outLines = new List<string>();
        int endAt = -1;
        foreach (var o in layout)
        {
            if (o is string s) { if (endAt < 0 && s.Trim().Equals("end", StringComparison.OrdinalIgnoreCase)) endAt = outLines.Count; outLines.Add(s); continue; }
            var r = (DropRule)o;
            if (!keep.Contains(r)) continue;
            outLines.Add(r.Raw != null && r.Key() == r.RawKey ? r.Raw : Format(r));
            placed.Add(r);
        }
        var novas = rules.Where(r => !placed.Contains(r)).Select(Format).ToList();
        if (endAt < 0) throw new InvalidOperationException("O ItemDrop.txt não tem a linha \"end\".");
        outLines.InsertRange(endAt, novas);

        var backup = DropFile + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(DropFile, backup, overwrite: true);
        File.WriteAllText(DropFile, string.Join("\r\n", outLines), Enc);
        Load();   // relê: as regras gravadas viram a nova base
        return backup;
    }

    public static string Percent(int rate) => (rate * 100.0 / RateBase).ToString("0.####");

    /// <summary>"0,5" ou "0.5" (%) → chance em 1.000.000.</summary>
    public static int ParsePercent(string text)
    {
        var s = text.Trim().TrimEnd('%').Trim().Replace(',', '.');
        if (!double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) || p < 0 || p > 100)
            throw new FormatException($"Chance inválida: \"{text}\" (use de 0 a 100, ex.: 0,5).");
        return (int)Math.Round(p * RateBase / 100.0);
    }

    // ---------------- Monster.txt ----------------
    static readonly Regex Token = new(@"""[^""]*""|\S+");
    const int TokItemRate = 20, TokMoneyRate = 21, TokMaxItemLevel = 22;   // Index Type "Name" Level ... Attribute ItemRate MoneyRate MaxItemLevel

    static List<Match>? Tokens(string line)
    {
        int c = line.IndexOf("//", StringComparison.Ordinal);
        var body = c >= 0 ? line[..c] : line;
        var m = Token.Matches(body).ToList();
        if (m.Count <= TokMaxItemLevel || !int.TryParse(m[0].Value, out _) || !m[2].Value.StartsWith('"')) return null;
        return m;
    }

    /// <summary>Monstros (Type 0) com nome, nível e as taxas de drop comum.</summary>
    public static List<MonsterRate> MonsterRates()
    {
        var list = new List<MonsterRate>();
        foreach (var line in File.ReadLines(MonsterFile, Enc))
        {
            var t = Tokens(line);
            if (t == null || t[1].Value != "0") continue;
            if (!int.TryParse(t[3].Value, out var lvl) || !int.TryParse(t[TokItemRate].Value, out var ir) ||
                !int.TryParse(t[TokMoneyRate].Value, out var mr) || !int.TryParse(t[TokMaxItemLevel].Value, out var ml)) continue;
            list.Add(new MonsterRate(int.Parse(t[0].Value), t[2].Value.Trim('"'), lvl, ir, mr, ml));
        }
        return list;
    }

    /// <summary>Nome de cada classe de monstro/NPC do Monster.txt.</summary>
    public static Dictionary<int, string> MonsterNames()
    {
        var d = new Dictionary<int, string>();
        foreach (var line in File.ReadLines(MonsterFile, Enc))
            if (Tokens(line) is { } t) d.TryAdd(int.Parse(t[0].Value), t[2].Value.Trim('"'));
        return d;
    }

    /// <summary>Troca ItemRate/MoneyRate/MaxItemLevel dos monstros dados, mantendo o alinhamento das colunas. Devolve quantas linhas mudaram.</summary>
    public static (int Changed, string Backup) SaveMonsterRates(IEnumerable<MonsterRate> rates)
    {
        var want = rates.ToDictionary(r => r.Index);
        var text = File.ReadAllText(MonsterFile, Enc);
        var lines = text.Split("\r\n");
        int changed = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var t = Tokens(lines[i]);
            if (t == null || !want.TryGetValue(int.Parse(t[0].Value), out var r)) continue;
            var line = lines[i];
            // da direita para a esquerda, para as posições das colunas anteriores continuarem valendo
            foreach (var (tok, value) in new[] { (TokMaxItemLevel, r.MaxItemLevel), (TokMoneyRate, r.MoneyRate), (TokItemRate, r.ItemRate) })
                line = Replace(line, t[tok], value.ToString());
            if (line != lines[i]) { lines[i] = line; changed++; }
        }
        if (changed == 0) return (0, "");
        var backup = MonsterFile + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(MonsterFile, backup, overwrite: true);
        File.WriteAllText(MonsterFile, string.Join("\r\n", lines), Enc);
        return (changed, backup);
    }

    /// <summary>Troca o texto de um token; se o novo for maior, come espaços à direita (deixa ao menos um) para não empurrar as colunas.</summary>
    static string Replace(string line, Match tok, string value)
    {
        if (tok.Value == value) return line;
        int start = tok.Index, end = tok.Index + tok.Length;
        int spaces = 0; while (end + spaces < line.Length && line[end + spaces] == ' ') spaces++;
        int room = tok.Length + spaces;   // espaço total até a próxima coluna
        bool last = end + spaces >= line.Length;
        string cell = last ? value : (value.Length + 1 <= room ? value.PadRight(room) : value + " ");
        return line[..start] + cell + line[(end + spaces)..];
    }
}

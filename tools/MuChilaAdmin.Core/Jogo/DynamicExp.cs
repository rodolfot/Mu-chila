using System.IO;
using System.Text;

namespace MuChilaAdmin.Core;

/// <summary>
/// EXP dinâmica (issue #37, 29/09/2026): as faixas do Data\Util\ExperienceTable.txt. Cada linha diz "para quem está entre
/// estes níveis (e master level, resets, master resets), a EXP é X% da taxa do plano" (teste do #24: EXP final = taxa do
/// plano × % / 100; níveis sem faixa ficam em 100%). O painel cuida só do bloco entre os marcadores "Mu Chila - EXP dinamica"
/// (criado pelo tools\Aplicar-EXPDinamica.ps1); o resto do arquivo fica como está. Arquivo em Windows-1252, TAB entre as
/// colunas, termina em "end" sem quebra de linha. Vale com Reload Util.
/// </summary>
public static class DynamicExp
{
    public sealed class Band
    {
        public int LevelMin, LevelMax, MasterMin, MasterMax = 600, ResetMin, ResetMax = 10000, MResetMin, MResetMax = 10000, Rate = 100;
        internal int[] Values => new[] { LevelMin, LevelMax, MasterMin, MasterMax, ResetMin, ResetMax, MResetMin, MResetMax, Rate };
        public override string ToString() => $"níveis {LevelMin}–{LevelMax}: {Rate}%";
    }

    const string Start = "// Mu Chila - EXP dinamica (inicio)", End = "// Mu Chila - EXP dinamica (fim)";
    public static string FilePath => Path.Combine(ServerControl.ServerRoot, @"Data\Util\ExperienceTable.txt");
    static readonly Encoding Enc = Encoding.Latin1;

    /// <summary>A curva aplicada em 28/09/2026 (Aplicar-EXPDinamica.ps1): 100% até o nível 50, caindo até 10% nos 351–399.</summary>
    public static List<Band> Default() => new[] { (1, 50, 100), (51, 100, 85), (101, 150, 70), (151, 200, 55), (201, 250, 40), (251, 300, 30), (301, 350, 20), (351, 399, 10), (400, 400, 100) }
        .Select(t => new Band { LevelMin = t.Item1, LevelMax = t.Item2, Rate = t.Item3 }).ToList();

    static Band? Parse(string line)
    {
        var c = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (c.Length < 9 || !c.Take(9).All(x => int.TryParse(x, out _))) return null;
        var v = c.Take(9).Select(int.Parse).ToArray();
        return new Band { LevelMin = v[0], LevelMax = v[1], MasterMin = v[2], MasterMax = v[3], ResetMin = v[4], ResetMax = v[5], MResetMin = v[6], MResetMax = v[7], Rate = v[8] };
    }

    /// <summary>Faixas do bloco do painel e quantas linhas de faixa existem FORA dele (postas à mão; o servidor também usa).</summary>
    public static (List<Band> Bands, int Outside) Load()
    {
        var bands = new List<Band>(); int outside = 0; bool inside = false;
        foreach (var l in File.ReadAllText(FilePath, Enc).Split('\n').Select(x => x.TrimEnd('\r')))
        {
            if (l.StartsWith(Start)) { inside = true; continue; }
            if (l.StartsWith(End)) { inside = false; continue; }
            if (Parse(l) is { } b) { if (inside) bands.Add(b); else outside++; }
        }
        return (bands, outside);
    }

    /// <summary>Problemas que impedem gravar (vazio = ok).</summary>
    public static List<string> Validate(IReadOnlyList<Band> bands)
    {
        var err = new List<string>();
        for (int i = 0; i < bands.Count; i++)
        {
            var b = bands[i]; var n = $"Faixa {i + 1} ({b.LevelMin}–{b.LevelMax})";
            if (b.Values.Any(v => v < 0)) err.Add($"{n}: número negativo.");
            if (b.LevelMin < 1 || b.LevelMax > 400 || b.LevelMin > b.LevelMax) err.Add($"{n}: nível de 1 a 400, com o \"de\" menor ou igual ao \"até\".");
            if (b.MasterMin > b.MasterMax || b.ResetMin > b.ResetMax || b.MResetMin > b.MResetMax) err.Add($"{n}: em master/resets, o \"de\" tem que ser menor ou igual ao \"até\".");
            if (b.Rate > 10000) err.Add($"{n}: EXP de 0 a 10000%.");
        }
        for (int i = 0; i < bands.Count; i++)
            for (int j = i + 1; j < bands.Count; j++)
            {
                var a = bands[i]; var b = bands[j];
                bool Cruza(int a0, int a1, int b0, int b1) => a0 <= b1 && b0 <= a1;
                if (Cruza(a.LevelMin, a.LevelMax, b.LevelMin, b.LevelMax) && Cruza(a.MasterMin, a.MasterMax, b.MasterMin, b.MasterMax)
                    && Cruza(a.ResetMin, a.ResetMax, b.ResetMin, b.ResetMax) && Cruza(a.MResetMin, a.MResetMax, b.MResetMin, b.MResetMax))
                    err.Add($"As faixas {a.LevelMin}–{a.LevelMax} e {b.LevelMin}–{b.LevelMax} se sobrepõem: o servidor usaria só uma delas.");
            }
        return err;
    }

    /// <summary>Níveis 1–400 que não caem em nenhuma faixa (com master/resets zerados): ficam com 100%.</summary>
    public static string Gaps(IReadOnlyList<Band> bands)
    {
        var sem = Enumerable.Range(1, 400).Where(l => !bands.Any(b => b.LevelMin <= l && l <= b.LevelMax)).ToList();
        if (sem.Count == 0) return "";
        var partes = new List<string>(); int ini = sem[0], ant = sem[0];
        foreach (var l in sem.Skip(1).Append(-1))
        {
            if (l == ant + 1) { ant = l; continue; }
            partes.Add(ini == ant ? $"{ini}" : $"{ini}–{ant}"); ini = ant = l;
        }
        return string.Join(", ", partes);
    }

    /// <summary>Grava as faixas (ordenadas por nível) no bloco do painel, com backup. Não recarrega (quem chama dá Reload Util).</summary>
    public static string Save(IReadOnlyList<Band> bands)
    {
        var err = Validate(bands);
        if (err.Count > 0) throw new InvalidOperationException(string.Join(" ", err));
        var text = File.ReadAllText(FilePath, Enc);
        var lines = text.Split('\n').Select(x => x.TrimEnd('\r')).ToList();
        int s = lines.FindIndex(l => l.StartsWith(Start)), e = lines.FindIndex(l => l.StartsWith(End));
        var bloco = new List<string> { Start + ": % da taxa do plano por faixa de nivel (100 = neutro)" };
        bloco.AddRange(bands.OrderBy(b => b.LevelMin).ThenBy(b => b.MasterMin).ThenBy(b => b.ResetMin).Select(b => string.Join("\t", b.Values)));
        bloco.Add(End);
        if (s >= 0 && e > s) { lines.RemoveRange(s, e - s + 1); lines.InsertRange(s, bloco); }
        else
        {
            int end = lines.FindLastIndex(l => l.Trim().Equals("end", StringComparison.OrdinalIgnoreCase));
            if (end < 0) throw new InvalidOperationException("ExperienceTable.txt sem a linha \"end\".");
            lines.InsertRange(end, bloco);
        }
        var novo = string.Join("\r\n", lines);
        if (novo == text) return "EXP dinâmica: nada mudou.";
        var backup = $"{FilePath}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Copy(FilePath, backup, overwrite: true);
        File.WriteAllText(FilePath, novo, Enc);
        return $"EXP dinâmica gravada: {bands.Count} faixa(s) ({string.Join("; ", bands.OrderBy(b => b.LevelMin).Select(b => b.ToString()))}). Backup {Path.GetFileName(backup)}.";
    }
}

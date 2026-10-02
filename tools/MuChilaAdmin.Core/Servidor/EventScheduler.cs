using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Core;

/// <param name="Block">Bloco do .dat que tem a agenda (Year Month Day DoW Hour Minute Second).</param>
/// <param name="Index">Indice do evento quando a agenda tem a coluna Index (InvasionManager); null quando nao tem.</param>
/// <param name="LeadMinutes">Antecedencia padrao: eventos com sala de espera precisam de tempo para os jogadores entrarem.</param>
public record EventDef(string Name, string File, int Block, int? Index, int LeadMinutes)
{
    /// <summary>Roda no servidor do Castle Siege (CastleDeepEvent = 1 só lá; nos GameServers, 0).</summary>
    public bool OnCastleSiege => File == "CastleDeepEvent.dat";
    /// <summary>Invasão do InvasionManager.dat (Index = número da invasão): o disparo vai direto na memória do GameServer.</summary>
    public bool IsInvasion => File == "InvasionManager.dat" && Index != null;
    /// <summary>Moss Merchant: o disparo também vai direto na memória (issue #43; o Reload Event não recalcula o horário).</summary>
    public bool IsMoss => File == "MossMerchant.dat";
}

/// <summary>
/// Dispara eventos agendando uma linha unica (data e hora exatas) no .dat do evento e recarregando os GameServers.
/// O MuDevs FREE nao tem comando para iniciar evento na hora; tudo sai das agendas em Data\Event.
/// </summary>
public static class EventScheduler
{
    const string Marker = "//MuChilaAdmin";
    static string EventDir => Path.Combine(ServerControl.ServerRoot, @"Data\Event");
    static readonly Encoding Latin1 = Encoding.Latin1;

    public static readonly EventDef[] Events =
    {
        new("Invasão: Red Dragon", "InvasionManager.dat", 0, 1, 1),
        new("Invasão: Golden (dragões dourados)", "InvasionManager.dat", 0, 2, 1),
        new("Invasão: Skeleton King (Underworld)", "InvasionManager.dat", 0, 0, 1),
        new("Invasão: White Wizard", "InvasionManager.dat", 0, 3, 1),
        new("Invasão: Medusa", "InvasionManager.dat", 0, 8, 1),
        new("Invasão: Natal", "InvasionManager.dat", 0, 7, 1),
        new("Invasão: Ano Novo", "InvasionManager.dat", 0, 4, 1),
        new("Invasão: Coelhos (Páscoa)", "InvasionManager.dat", 0, 5, 1),
        new("Invasão: Verão", "InvasionManager.dat", 0, 6, 1),
        new("Invasão: Demônios invocados", "InvasionManager.dat", 0, 9, 1),
        new("Invasão: Ovos", "InvasionManager.dat", 0, 10, 1),
        new("Blood Castle", "BloodCastle.dat", 1, null, 6),
        new("Devil Square", "DevilSquare.dat", 1, null, 6),
        new("Chaos Castle", "ChaosCastle.dat", 1, null, 6),
        new("Illusion Temple", "IllusionTemple.dat", 1, null, 6),
        new("Castle Deep (invasão de Valley of Loren)", "CastleDeepEvent.dat", 0, null, 1),
        new("Moss Merchant (mercador de apostas)", "MossMerchant.dat", 0, null, 1),
    };

    public static string Schedule(EventDef ev, DateTime when)
    {
        var path = Path.Combine(EventDir, ev.File);
        var (lines, newline) = Read(path);
        int end = FindBlockEnd(lines, ev.Block) ?? throw new InvalidOperationException($"Bloco {ev.Block} nao encontrado em {ev.File}");

        var time = $"{when.Year} {when.Month} {when.Day} * {when.Hour} {when.Minute} 0";
        var entry = ev.Index is int idx ? $"{idx,-9} {time}" : time;
        lines.Insert(end, $"{entry}   {Marker} {ev.Name} ({when:dd/MM HH:mm})");

        Backup(path);
        File.WriteAllText(path, string.Join(newline, lines), Latin1);
        return $"{ev.Name} agendado para {when:dd/MM/yyyy HH:mm} em {ev.File}";
    }

    /// <summary>Disparos ainda registrados nos .dat (pendentes ou ja executados).</summary>
    public static List<(DateTime When, string Name, string File)> Pending()
    {
        var list = new List<(DateTime, string, string)>();
        foreach (var file in Events.Select(e => e.File).Distinct())
        {
            var path = Path.Combine(EventDir, file);
            if (!File.Exists(path)) continue;
            foreach (var line in File.ReadAllLines(path, Latin1))
            {
                if (!line.Contains(Marker)) continue;
                var when = ParseWhen(line);
                var name = line[(line.IndexOf(Marker) + Marker.Length)..].Trim();
                if (when != null) list.Add((when.Value, name, file));
            }
        }
        return list.OrderBy(x => x.Item1).ToList();
    }

    /// <summary>Remove disparos cujo horario ja passou ha mais de <paramref name="olderThan"/>.</summary>
    public static int CleanupPast(TimeSpan olderThan)
    {
        int removed = 0;
        foreach (var file in Events.Select(e => e.File).Distinct())
        {
            var path = Path.Combine(EventDir, file);
            if (!File.Exists(path)) continue;
            var (lines, newline) = Read(path);
            int before = lines.Count;
            lines.RemoveAll(l => l.Contains(Marker) && ParseWhen(l) is DateTime w && w < DateTime.Now - olderThan);
            if (lines.Count == before) continue;
            removed += before - lines.Count;
            Backup(path);
            File.WriteAllText(path, string.Join(newline, lines), Latin1);
        }
        return removed;
    }

    static (List<string> Lines, string Newline) Read(string path)
    {
        var text = File.ReadAllText(path, Latin1);
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        return (Regex.Split(text, "\r?\n").ToList(), newline);
    }

    /// <summary>Linha do "end" que fecha o bloco N (blocos comecam com uma linha contendo so o numero).</summary>
    static int? FindBlockEnd(List<string> lines, int block)
    {
        bool inside = false; int current = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (!inside && Regex.IsMatch(t, @"^\d+$")) { inside = true; current = int.Parse(t); continue; }
            if (inside && t.StartsWith("end", StringComparison.OrdinalIgnoreCase))
            {
                if (current == block) return i;
                inside = false;
            }
        }
        return null;
    }

    static DateTime? ParseWhen(string line)
    {
        // "[Index] Year Month Day DoW Hour Minute Second //MuChilaAdmin ..."
        var m = Regex.Match(line, @"(\d{4})\s+(\d{1,2})\s+(\d{1,2})\s+\*\s+(\d{1,2})\s+(\d{1,2})\s+(\d{1,2})");
        if (!m.Success) return null;
        var p = m.Groups.Values.Skip(1).Select(g => int.Parse(g.Value, CultureInfo.InvariantCulture)).ToArray();
        try { return new DateTime(p[0], p[1], p[2], p[3], p[4], p[5]); } catch { return null; }
    }

    /// <summary>
    /// O que a invasão faz, lido do InvasionManager.dat (issue #42: o nome "Demônios invocados" não diz que são Golden Goblins
    /// num mapa sorteado). Bloco 1: aviso e duração; bloco 2: Index Group Map Value (por grupo, o servidor sorteia um Value);
    /// bloco 3: Index Group Value Class Count MapN... (os monstros de cada Value).
    /// </summary>
    public static List<string> InvasionDescription(int index)
    {
        var path = Path.Combine(EventDir, "InvasionManager.dat");
        var rows = new Dictionary<int, List<string[]>>();
        int block = -1;
        foreach (var raw in File.ReadLines(path, Latin1))
        {
            var t = raw.Split("//")[0].Trim();
            if (t.Length == 0) continue;
            if (Regex.IsMatch(t, @"^\d+$") && block < 0) { block = int.Parse(t); continue; }
            if (t.StartsWith("end", StringComparison.OrdinalIgnoreCase)) { block = -1; continue; }
            var c = Regex.Split(t, @"\s+");
            if (block >= 0 && c.Length > 1 && c[0] == index.ToString(CultureInfo.InvariantCulture))
                (rows.TryGetValue(block, out var l) ? l : rows[block] = new()).Add(c);
        }
        var maps = Monsters.NomesDosMapas();
        var mons = Drops.MonsterRates().ToDictionary(m => m.Index);
        string Mapa(string v) => int.TryParse(v, out var n) && maps.TryGetValue(n, out var s) ? s : $"mapa {v}";
        string Monstro(string v) => int.TryParse(v, out var n) && mons.TryGetValue(n, out var m) ? $"{m.Name} (nível {m.Level})" : $"monstro {v} (não está no Monster.txt!)";

        var log = new List<string>();
        if (rows.GetValueOrDefault(1)?.FirstOrDefault() is { Length: >= 6 } cfg)
        {
            var msg = int.TryParse(cfg[1], out var id) ? ServerMessage(id) : null;
            log.Add($"Aviso ao começar: \"{msg ?? cfg[1]}\"; dura {cfg[5]} s.");
        }
        var spawns = rows.GetValueOrDefault(3) ?? new();
        foreach (var g in (rows.GetValueOrDefault(2) ?? new()).Where(c => c.Length >= 4).GroupBy(c => c[1]))
        {
            var opcoes = g.Select(c =>
            {
                var ms = spawns.Where(s => s.Length >= 6 && s[1] == g.Key && s[2] == c[3]).ToList();
                var quem = string.Join(" + ", ms.GroupBy(s => s[3]).Select(x => $"{x.Sum(s => int.TryParse(s[4], out var q) ? q : 0)}× {Monstro(x.Key)}"));
                return $"{Mapa(c[2])}: {(quem == "" ? "nenhum monstro configurado" : quem)}";
            }).ToList();
            log.Add(opcoes.Count == 1 ? $"Grupo {g.Key}: {opcoes[0]}."
                                      : $"Grupo {g.Key}: o servidor sorteia UM destes na hora — {string.Join("; ", opcoes)}.");
        }
        if (log.Count == 0) log.Add($"Invasão {index} sem configuração no InvasionManager.dat.");
        return log;
    }

    /// <summary>Texto da mensagem do servidor (Data\Lang\Portuguese.xml, em Windows-1252), ou null.</summary>
    static string? ServerMessage(int id)
    {
        var path = Path.Combine(ServerControl.ServerRoot, @"Data\Lang\Portuguese.xml");
        if (!File.Exists(path)) return null;
        var m = Regex.Match(File.ReadAllText(path, Latin1), $"<Msg ID=\"{id}\" Text=\"([^\"]*)\"");
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    static void Backup(string path)
    {
        var bak = $"{path}.bak-{DateTime.Now:yyyyMMdd}";
        if (!File.Exists(bak)) File.Copy(path, bak);
    }
}

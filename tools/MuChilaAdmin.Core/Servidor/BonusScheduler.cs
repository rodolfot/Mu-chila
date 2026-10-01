using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Core;

/// <summary>Um bônus agendado por este programa no BonusManager.dat.</summary>
public record BonusEntry(int Slot, DateTime Start, int Seconds, string Description)
{
    public DateTime End => Start.AddSeconds(Seconds);
    public string State => DateTime.Now < Start ? "agendado" : DateTime.Now < End ? "ATIVO" : "terminado";
}

/// <summary>
/// Bônus por tempo (EXP, EXP master, drop) pelo BonusManager.dat do servidor.
/// Formato validado num GameServer de testes em 26/09/2026:
///   bloco 0 = agenda (Index Year Month Day DoW Hour Minute Second), bloco 1 = Index StartMessage FinalMessage BonusTime (s),
///   bloco 2 = Index BonusIndex BonusValue_AL0..AL3 ItemIndex ItemLevel MapNumber MonsterClass MonsterLevelMin MonsterLevelMax.
///   BonusIndex 0 = experiência, 1 = experiência master, 2 = drop. O valor soma na taxa do tipo de conta
///   (AddExperienceRate_ALn etc.), então "x2" = somar a própria taxa de cada plano.
/// Só use os índices 3 a 9: o kit usa 0-2, e um índice alto (90) derrubou os GameServers na recarga.
/// Mensagens de início e fim: IDs 700-713 (2 por vaga) no Portuguese.xml e English.xml, nas seções Message e InvacionMsg.
/// </summary>
public static class BonusScheduler
{
    const string Marker = "//MuChilaAdmin";
    public const int FirstSlot = 3, LastSlot = 9;
    static readonly Encoding Latin1 = Encoding.Latin1;   // preserva os bytes do arquivo (Windows-1252)
    static string BonusFile => Path.Combine(ServerControl.ServerRoot, @"Data\Event\BonusManager.dat");
    static string LangDir => Path.Combine(ServerControl.ServerRoot, @"Data\Lang");

    public static readonly (int Index, string Name, string RateKey)[] Types =
    {
        (0, "EXP", "AddExperienceRate"),
        (1, "EXP master", "AddMasterExperienceRate"),
        (2, "Drop", "ItemDropRate"),
    };

    static int StartMessageId(int slot) => 700 + (slot - FirstSlot) * 2;

    /// <summary>Taxas atuais por tipo de conta (AL0..AL3), lidas do Common.dat do GameServer principal.</summary>
    public static int[] Rates(string rateKey)
    {
        var common = Path.Combine(ServerControl.ServerRoot, @"GameServer\DATA\GameServerInfo - Common.dat");
        if (!File.Exists(common))   // cópia de testes: usa a primeira pasta GameServer* que existir
            common = Directory.GetDirectories(ServerControl.ServerRoot, "GameServer*").Select(d => Path.Combine(d, @"DATA\GameServerInfo - Common.dat")).First(File.Exists);
        var text = File.ReadAllText(common, Latin1);
        return Enumerable.Range(0, 4).Select(al =>
        {
            var m = Regex.Match(text, $@"(?m)^\s*{rateKey}_AL{al}\s*=\s*(\d+)");
            if (!m.Success) throw new InvalidOperationException($"{rateKey}_AL{al} não encontrado em {common}");
            return int.Parse(m.Groups[1].Value);
        }).ToArray();
    }

    /// <summary>Agenda um bônus e escreve as mensagens. Depois é preciso recarregar Common (mensagens) e Event (bônus).</summary>
    public static string Schedule(IReadOnlyList<int> typeIndexes, decimal multiplier, int minutes, DateTime start)
    {
        if (typeIndexes.Count == 0) throw new ArgumentException("Escolha pelo menos um tipo de bônus.");
        if (multiplier <= 1) throw new ArgumentException("O multiplicador precisa ser maior que 1.");
        if (minutes < 1) throw new ArgumentException("Duração mínima: 1 minuto.");
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0);

        var (lines, newline) = Read(BonusFile);
        var used = UsedSlots(lines);
        int slot = Enumerable.Range(FirstSlot, LastSlot - FirstSlot + 1).FirstOrDefault(s => !used.Contains(s));
        if (slot == 0) throw new InvalidOperationException($"As {LastSlot - FirstSlot + 1} vagas de bônus estão ocupadas. Use \"Limpar bônus terminados\" ou espere algum acabar.");

        var types = Types.Where(t => typeIndexes.Contains(t.Index)).ToList();
        var mult = multiplier.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
        var names = string.Join(" + ", types.Select(t => t.Name));
        var desc = $"{names} x{mult} por {Duration(minutes)}";
        var end = start.AddMinutes(minutes);

        // bloco 2: uma linha por tipo; valor por plano = taxa do plano × (multiplicador − 1)
        var valueLines = new List<string>();
        foreach (var t in types)
        {
            var v = Rates(t.RateKey).Select(r => (int)Math.Round(r * (multiplier - 1))).ToArray();
            valueLines.Add($"{slot,-9} {t.Index,-12} {v[0],-16} {v[1],-16} {v[2],-16} {v[3],-16} *           *           *           *              *                 *   {Marker} {desc}");
        }
        int msgStart = StartMessageId(slot), msgEnd = msgStart + 1;
        Insert(lines, 2, valueLines);
        Insert(lines, 1, new[] { $"{slot,-9} {msgStart,-14} {msgEnd,-14} {minutes * 60}   {Marker} {desc}" });
        Insert(lines, 0, new[] { $"{slot,-9} {start.Year}   {start.Month}       {start.Day}     *     {start.Hour}      {start.Minute}        0   {Marker} {desc} ({start:dd/MM HH:mm})" });

        WriteMessages(msgStart, $"Evento: {desc}! Vale para todos os planos até {end:HH:mm}.");
        WriteMessages(msgEnd, $"O evento {names} x{mult} terminou.");
        Save(BonusFile, lines, newline);
        return $"Bônus \"{desc}\" agendado: {start:dd/MM HH:mm} até {end:dd/MM HH:mm} (vaga {slot})";
    }

    /// <summary>Bônus deste programa ainda no arquivo (agendados, ativos ou terminados).</summary>
    public static List<BonusEntry> List()
    {
        if (!File.Exists(BonusFile)) return new();
        var (lines, _) = Read(BonusFile);
        var starts = BlockLines(lines, 0).Where(l => l.Contains(Marker)).Select(l => (Slot: SlotOf(l), When: ParseWhen(l)));
        var info = BlockLines(lines, 1).Where(l => l.Contains(Marker)).ToDictionary(SlotOf, l => l);
        var list = new List<BonusEntry>();
        foreach (var (slot, when) in starts)
        {
            if (when == null || !info.TryGetValue(slot, out var l1)) continue;
            var cols = Regex.Split(l1.Split(Marker)[0].Trim(), @"\s+");
            list.Add(new BonusEntry(slot, when.Value, int.Parse(cols[3]), l1.Split(Marker)[1].Trim()));
        }
        return list.OrderBy(b => b.Start).ToList();
    }

    /// <summary>Tira do arquivo os bônus que já terminaram (libera as vagas). Retorna quantos saíram.</summary>
    public static int CleanupFinished() => Remove(List().Where(b => b.End < DateTime.Now.AddMinutes(-1)).Select(b => b.Slot).ToHashSet());

    /// <summary>Cancela um bônus que ainda não começou (um bônus ativo não é mexido: o servidor já está com ele ligado).</summary>
    public static string Cancel(int slot)
    {
        var b = List().FirstOrDefault(x => x.Slot == slot) ?? throw new InvalidOperationException($"Vaga {slot} não tem bônus deste programa.");
        if (b.State != "agendado") throw new InvalidOperationException($"O bônus da vaga {slot} já começou; espere terminar.");
        Remove(new HashSet<int> { slot });
        return $"Bônus \"{b.Description}\" cancelado.";
    }

    static int Remove(HashSet<int> slots)
    {
        if (slots.Count == 0) return 0;
        var (lines, newline) = Read(BonusFile);
        int before = lines.Count;
        lines.RemoveAll(l => l.Contains(Marker) && slots.Contains(SlotOf(l)));
        Save(BonusFile, lines, newline);
        return slots.Count;
    }

    static HashSet<int> UsedSlots(List<string> lines) =>
        Enumerable.Range(0, 3).SelectMany(b => BlockLines(lines, b)).Where(l => Regex.IsMatch(l, @"^\s*\d+\s")).Select(SlotOf).ToHashSet();

    static int SlotOf(string line) => int.Parse(Regex.Match(line, @"^\s*(\d+)").Groups[1].Value);

    static string Duration(int minutes) =>
        minutes % 60 == 0 ? (minutes == 60 ? "1 hora" : $"{minutes / 60} horas") : minutes < 60 ? $"{minutes} minutos" : $"{minutes / 60}h{minutes % 60:00}";

    // ---------- mensagens (Portuguese.xml / English.xml, seções Message e InvacionMsg) ----------
    static void WriteMessages(int id, string text)
    {
        var esc = text.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
        foreach (var file in new[] { "Portuguese.xml", "English.xml" })
        {
            var path = Path.Combine(LangDir, file);
            if (!File.Exists(path)) continue;
            var xml = File.ReadAllText(path, Latin1);
            var newline = xml.Contains("\r\n") ? "\r\n" : "\n";
            foreach (var section in new[] { "Message", "InvacionMsg" })
            {
                var m = Regex.Match(xml, $@"(?s)<{section}>(.*?)(\t*)</{section}>");
                if (!m.Success) continue;
                var body = m.Groups[1].Value;
                var entry = $"<Msg ID=\"{id}\" Text=\"{esc}\" />";
                var existing = Regex.Match(body, $@"<Msg ID=""{id}""[^>]*/>");
                body = existing.Success ? body.Replace(existing.Value, entry) : body + $"\t\t{entry}{newline}";
                xml = xml[..m.Groups[1].Index] + body + xml[(m.Groups[1].Index + m.Groups[1].Length)..];
            }
            Backup(path);
            File.WriteAllText(path, xml, Latin1);
        }
    }

    // ---------- arquivo do BonusManager ----------
    static (List<string> Lines, string Newline) Read(string path)
    {
        var text = File.ReadAllText(path, Latin1);
        return (Regex.Split(text, "\r?\n").ToList(), text.Contains("\r\n") ? "\r\n" : "\n");
    }

    static void Save(string path, List<string> lines, string newline)
    {
        // confere a estrutura antes de gravar: os 3 blocos precisam continuar com "end"
        for (int b = 0; b < 3; b++) if (FindBlockEnd(lines, b) == null) throw new InvalidOperationException($"BonusManager.dat ficaria sem o bloco {b}; nada gravado.");
        Backup(path);
        File.WriteAllText(path, string.Join(newline, lines), Latin1);
    }

    static void Insert(List<string> lines, int block, IEnumerable<string> newLines)
    {
        int end = FindBlockEnd(lines, block) ?? throw new InvalidOperationException($"Bloco {block} não encontrado no BonusManager.dat");
        lines.InsertRange(end, newLines);
    }

    static IEnumerable<string> BlockLines(List<string> lines, int block)
    {
        int end = FindBlockEnd(lines, block) ?? -1;
        if (end < 0) yield break;
        int start = end - 1;
        while (start >= 0 && lines[start].Trim() != block.ToString()) start--;
        for (int i = start + 1; i < end; i++)
        {
            var t = lines[i].Trim();
            if (t.Length > 0 && !t.StartsWith("//")) yield return lines[i];
        }
    }

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
        // "Index Year Month Day * Hour Minute Second //MuChilaAdmin ..."
        var m = Regex.Match(line, @"^\s*\d+\s+(\d{4})\s+(\d{1,2})\s+(\d{1,2})\s+\*\s+(\d{1,2})\s+(\d{1,2})\s+(\d{1,2})");
        if (!m.Success) return null;
        var p = m.Groups.Values.Skip(1).Select(g => int.Parse(g.Value, CultureInfo.InvariantCulture)).ToArray();
        try { return new DateTime(p[0], p[1], p[2], p[3], p[4], p[5]); } catch { return null; }
    }

    static void Backup(string path)
    {
        var bak = $"{path}.bak-{DateTime.Now:yyyyMMdd}";
        if (!File.Exists(bak)) File.Copy(path, bak);
    }
}

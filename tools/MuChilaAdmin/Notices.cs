using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin;

/// <summary>Um aviso do Data\Util\Notice.txt: o GameServer manda a mensagem para todos os jogadores a cada RepeatTime.</summary>
public class Notice
{
    public string Message = "";
    public int Type, Count, Opacity, Delay, Red, Green, Blue, Speed, RepeatTime = 600;
    public string Comment = "";
    internal string? Raw, RawKey;
    internal string Key() => string.Join("|", Message, Type, Count, Opacity, Delay, Red, Green, Blue, Speed, RepeatTime, Comment);
    /// <summary>Aviso "enviar agora" do painel (sai do arquivo sozinho depois de mandado).</summary>
    public bool IsOneShot => Comment.StartsWith(Notices.OneShotMarker, StringComparison.Ordinal);
}

/// <summary>
/// Avisos para todos os jogadores pelo Data\Util\Notice.txt do kit (colunas: "Mensagem" Type Count Opacity Delay Red Green Blue
/// Speed RepeatTime; linha do kit: "Server Season 14 AM" 0 0 0 0 0 0 0 0 60) + Reload Util. O GameServer não tem comando de
/// aviso no menu, então é o único caminho sem mexer no executável. Formato do MuEmu: os avisos se revezam em ordem, e cada
/// um espera o seu RepeatTime (segundos) depois do anterior; com um aviso só, RepeatTime é o intervalo.
/// "Enviar agora" = aviso marcado no TOPO da lista com RepeatTime 1 (sai logo depois do Reload Util); o vigia tira ele do
/// arquivo em até ~50 s, antes de o rodízio voltar nele. Linhas não alteradas são regravadas iguais; backup .bak-*.
/// </summary>
public static class Notices
{
    public const string OneShotMarker = "MuChilaAdmin-agora";
    public static string File_ => Path.Combine(ServerControl.ServerRoot, @"Data\Util\Notice.txt");
    static readonly Encoding Enc = Encoding.Latin1;   // o cliente lê Windows-1252 (acentos ok)
    static readonly Regex Line = new(@"^\s*""([^""]*)""\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s*(?://(.*))?$");
    static List<object>? layout;
    static string? loadedText;

    public static List<Notice> Load()
    {
        var text = File.ReadAllText(File_, Enc);
        var lay = new List<object>(); var list = new List<Notice>();
        foreach (var line in text.Split("\r\n"))
        {
            var m = Line.Match(line);
            if (!m.Success) { lay.Add(line); continue; }
            int G(int i) => int.Parse(m.Groups[i].Value);
            var n = new Notice
            {
                Message = m.Groups[1].Value, Type = G(2), Count = G(3), Opacity = G(4), Delay = G(5), Red = G(6), Green = G(7), Blue = G(8),
                Speed = G(9), RepeatTime = G(10), Comment = m.Groups[11].Success ? m.Groups[11].Value : "", Raw = line,
            };
            n.RawKey = n.Key();
            lay.Add(n); list.Add(n);
        }
        layout = lay; loadedText = text;
        return list;
    }

    static string Format(Notice n)
    {
        var msg = "\"" + n.Message + "\"";
        var sb = new StringBuilder(msg.PadRight(71));
        foreach (var (v, w) in new[] { (n.Type, 7), (n.Count, 8), (n.Opacity, 10), (n.Delay, 8), (n.Red, 6), (n.Green, 8), (n.Blue, 7), (n.Speed, 8), (n.RepeatTime, 0) })
            sb.Append(w == 0 ? v.ToString() : v.ToString().PadRight(w));
        if (n.Comment.Length > 0) sb.Append("   //").Append(n.Comment);
        return sb.ToString();
    }

    /// <summary>Texto aceito no arquivo: sem aspas nem quebra de linha, até 90 caracteres, só o que existe no Windows-1252.</summary>
    public static string Clean(string message)
    {
        var s = Regex.Replace(message.Replace('"', '\'').Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();
        if (s.Length == 0) throw new InvalidOperationException("Mensagem vazia.");
        if (s.Length > 90) throw new InvalidOperationException($"Mensagem longa demais ({s.Length} caracteres; máximo 90).");
        if (Enc.GetString(Enc.GetBytes(s)) != s) throw new InvalidOperationException("A mensagem tem caracteres que o jogo não mostra (emoji ou símbolo especial).");
        return s;
    }

    public static string Save(IReadOnlyList<Notice> notices)
    {
        if (layout == null || loadedText == null) throw new InvalidOperationException("Leia os avisos antes de gravar.");
        if (File.ReadAllText(File_, Enc) != loadedText) throw new InvalidOperationException("O Notice.txt mudou no disco desde que foi aberto. Clique em \"Descartar\" para ler de novo.");
        foreach (var n in notices) n.Message = Clean(n.Message);
        var keep = new HashSet<Notice>(notices, ReferenceEqualityComparer.Instance);
        var placed = new HashSet<Notice>(ReferenceEqualityComparer.Instance);
        var outLines = new List<string>(); int endAt = -1;
        foreach (var o in layout)
        {
            if (o is string s) { if (endAt < 0 && s.Trim().Equals("end", StringComparison.OrdinalIgnoreCase)) endAt = outLines.Count; outLines.Add(s); continue; }
            var n = (Notice)o;
            if (!keep.Contains(n)) continue;
            outLines.Add(n.Raw != null && n.Key() == n.RawKey ? n.Raw : Format(n));
            placed.Add(n);
        }
        if (endAt < 0) throw new InvalidOperationException("O Notice.txt não tem a linha \"end\".");
        var novas = notices.Where(n => !placed.Contains(n)).ToList();
        outLines.InsertRange(endAt, novas.Where(n => !n.IsOneShot).Select(Format));
        // "enviar agora" vai para antes do primeiro aviso: é o primeiro que o servidor manda depois do Reload
        int first = outLines.FindIndex(l => Line.IsMatch(l));
        outLines.InsertRange(first >= 0 ? first : endAt, novas.Where(n => n.IsOneShot).Select(Format));
        var backup = File_ + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(File_, backup, overwrite: true);
        File.WriteAllText(File_, string.Join("\r\n", outLines), Enc);
        Load();
        return backup;
    }

    /// <summary>Acrescenta um aviso "enviar agora" no topo (RepeatTime 1); depois é preciso dar Reload Util. O vigia tira do arquivo.</summary>
    public static void AddOneShot(string message)
    {
        var list = Load();
        list.Add(new Notice { Message = Clean(message), RepeatTime = 1, Comment = $"{OneShotMarker} {DateTime.Now:yyyy-MM-dd HH:mm:ss}" });
        Save(list);
    }

    /// <summary>Tira do arquivo os avisos "enviar agora" com mais de <paramref name="age"/>. Devolve quantos saíram.</summary>
    public static int RemoveOneShots(TimeSpan age)
    {
        var list = Load();
        var velhos = list.Where(n => n.IsOneShot && DateTime.TryParseExact(n.Comment[OneShotMarker.Length..].Trim(), "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var t) && DateTime.Now - t >= age).ToList();
        if (velhos.Count == 0) return 0;
        Save(list.Except(velhos).ToList());
        return velhos.Count;
    }
}

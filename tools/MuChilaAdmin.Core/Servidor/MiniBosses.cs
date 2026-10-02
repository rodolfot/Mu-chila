using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Core;

/// <summary>
/// Um mini-boss recorrente: uma invasão dedicada no InvasionManager.dat (um slot de 11 a 29, livres no kit) que faz
/// nascer 1 monstro "modelo" num mapa, todo dia num horário fixo (ou de 12 em 12 horas), por um tempo. Todas as linhas
/// levam o comentário <c>//MiniBoss#N nome</c> para o painel reconhecer e reescrever só as suas, sem tocar nas invasões
/// originais. A força vem do monstro escolhido (ex.: a Sapi-Tres é uma aranha gigante de 58 mil de vida); o ajuste fino
/// de vida/dano é no editor de atributos dos monstros (Data\Monster\Monster.txt).
/// </summary>
public sealed record MiniBoss(
    int Slot, string Nome, int Mapa, int Monstro,
    int Hora, int Minuto, bool DuasVezesAoDia, bool Anuncia, int DuracaoMinutos)
{
    public const int SlotMin = 11, SlotMax = 29;   // 0..10 são as invasões do kit
}

/// <summary>Lê e grava os mini-bosses no InvasionManager.dat (+ mensagens de anúncio no Portuguese.xml).</summary>
public static class MiniBosses
{
    const string Marker = "//MiniBoss#";
    static readonly Encoding Latin1 = Encoding.Latin1;
    static string InvasionPath => Path.Combine(ServerControl.ServerRoot, @"Data\Event\InvasionManager.dat");
    static string LangPath => Path.Combine(ServerControl.ServerRoot, @"Data\Lang\Portuguese.xml");

    // Dois IDs de mensagem por slot (apareceu / derrotado), numa faixa livre (o kit vai até 705).
    static int RespawnMsgId(int slot) => 720 + (slot - MiniBoss.SlotMin) * 2;
    static int BossMsgId(int slot) => 721 + (slot - MiniBoss.SlotMin) * 2;

    /// <summary>Mini-bosses já configurados, lidos do InvasionManager.dat.</summary>
    public static List<MiniBoss> Listar()
    {
        if (!File.Exists(InvasionPath)) return new();
        var linhas = File.ReadAllLines(InvasionPath, Latin1).ToList();

        // por slot, junta o que cada bloco diz
        var nome = new Dictionary<int, string>();
        var mapaMonstro = new Dictionary<int, (int Mapa, int Monstro)>();
        var cfg = new Dictionary<int, (int Dur, bool Anuncia)>();
        var horarios = new Dictionary<int, List<(int H, int M)>>();

        foreach (var (bloco, linha) in LinhasComBloco(linhas))
        {
            var slot = SlotDaLinha(linha);
            if (slot == null) continue;
            var c = Colunas(linha);
            nome.TryAdd(slot.Value, NomeDaLinha(linha));
            switch (bloco)
            {
                case 0 when c.Length > 6 && int.TryParse(c[5], out var h) && int.TryParse(c[6], out var m):
                    (horarios.TryGetValue(slot.Value, out var l) ? l : horarios[slot.Value] = new()).Add((h, m));
                    break;
                case 1 when c.Length > 5:
                    cfg[slot.Value] = (int.TryParse(c[5], out var dur) ? dur : 600, c[1] != "*");
                    break;
                case 3 when c.Length > 5:
                    mapaMonstro[slot.Value] = (int.TryParse(c[5], out var mp) ? mp : 0, int.TryParse(c[3], out var mo) ? mo : 0);
                    break;
            }
        }

        var r = new List<MiniBoss>();
        foreach (var slot in mapaMonstro.Keys.OrderBy(x => x))
        {
            var hs = horarios.GetValueOrDefault(slot) ?? new();
            var (h, m) = hs.Count > 0 ? (hs[0].H, hs[0].M) : (0, 0);
            var (dur, anuncia) = cfg.GetValueOrDefault(slot, (600, false));
            r.Add(new MiniBoss(slot, nome.GetValueOrDefault(slot, $"Mini-boss {slot}"),
                mapaMonstro[slot].Mapa, mapaMonstro[slot].Monstro, h, m, hs.Count >= 2, anuncia, Math.Max(1, dur / 60)));
        }
        return r;
    }

    /// <summary>Primeiro slot livre (11..29) ou null se já encheu.</summary>
    public static int? ProximoSlotLivre()
    {
        var usados = Listar().Select(b => b.Slot).ToHashSet();
        for (int s = MiniBoss.SlotMin; s <= MiniBoss.SlotMax; s++) if (!usados.Contains(s)) return s;
        return null;
    }

    /// <summary>Cria/atualiza o mini-boss (reescreve só as linhas do slot dele) e as mensagens de anúncio.</summary>
    public static string Salvar(MiniBoss mb)
    {
        if (mb.Slot < MiniBoss.SlotMin || mb.Slot > MiniBoss.SlotMax)
            throw new InvalidOperationException($"Slot {mb.Slot} fora da faixa {MiniBoss.SlotMin}..{MiniBoss.SlotMax}.");
        if (string.IsNullOrWhiteSpace(mb.Nome)) throw new InvalidOperationException("O mini-boss precisa de um nome.");

        var (linhas, nl) = Ler(InvasionPath);
        TirarSlot(linhas, mb.Slot);

        int resp = mb.Anuncia ? RespawnMsgId(mb.Slot) : -1, boss = mb.Anuncia ? BossMsgId(mb.Slot) : -1;
        var com = $"{Marker}{mb.Slot} {mb.Nome}";

        // Bloco 0 (agenda): recorrente todo dia (ano/mes/dia/semana = *); de 12 em 12 h = duas linhas
        var agenda = new List<string> { Linha0(mb.Slot, mb.Hora, mb.Minuto, com) };
        if (mb.DuasVezesAoDia) agenda.Add(Linha0(mb.Slot, (mb.Hora + 12) % 24, mb.Minuto, com));
        // Bloco 1 (aviso + boss + duração em segundos)
        var conf = new List<string> { $"{mb.Slot,-9} {Tok(resp),-16} *                {mb.Monstro,-11} {Tok(boss),-13} {mb.DuracaoMinutos * 60}\t\t{com}" };
        // Bloco 2 (grupo 0 → um mapa só, sem sorteio)
        var grupo = new List<string> { $"{mb.Slot,-9} 0       {mb.Mapa,-5} 0\t{Marker}{mb.Slot}" };
        // Bloco 3 (1 monstro, no mapa, pela área toda)
        var spawn = new List<string> { $"{mb.Slot,-9} 0       0       {mb.Monstro,-7} 1       {mb.Mapa,-6} 10     10      240     240     *\t0           0\t{com}" };

        Inserir(linhas, 3, spawn);
        Inserir(linhas, 2, grupo);
        Inserir(linhas, 1, conf);
        Inserir(linhas, 0, agenda);

        Backup(InvasionPath);
        File.WriteAllText(InvasionPath, string.Join(nl, linhas), Latin1);
        GravarMensagens(mb);

        var quando = mb.DuasVezesAoDia ? $"{mb.Hora:00}:{mb.Minuto:00} e {(mb.Hora + 12) % 24:00}:{mb.Minuto:00}" : $"{mb.Hora:00}:{mb.Minuto:00}";
        return $"\"{mb.Nome}\" (invasão {mb.Slot}): {Monsters.NomesDosMapas().GetValueOrDefault(mb.Mapa, $"mapa {mb.Mapa}")}, todo dia às {quando}, dura {mb.DuracaoMinutos} min. Reinicie os GameServers para valer.";
    }

    /// <summary>Apaga o mini-boss do slot (linhas no InvasionManager.dat e mensagens).</summary>
    public static string Remover(int slot)
    {
        var (linhas, nl) = Ler(InvasionPath);
        int antes = linhas.Count;
        TirarSlot(linhas, slot);
        if (linhas.Count == antes) return $"Nenhum mini-boss no slot {slot}.";
        Backup(InvasionPath);
        File.WriteAllText(InvasionPath, string.Join(nl, linhas), Latin1);
        ApagarMensagens(slot);
        return $"Mini-boss do slot {slot} removido. Reinicie os GameServers.";
    }

    // ---------- InvasionManager.dat ----------

    static string Linha0(int slot, int hora, int min, string com) =>
        $"{slot,-9} *      *       *     *     {hora,-6} {min,-8} 0\t{com}";

    static string Tok(int id) => id < 0 ? "*" : id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Percorre as linhas sabendo em que bloco (0,1,2,3) cada uma está (abre num número sozinho, fecha em "end").</summary>
    static IEnumerable<(int Bloco, string Linha)> LinhasComBloco(IEnumerable<string> linhas)
    {
        int bloco = -1; bool dentro = false;
        foreach (var raw in linhas)
        {
            var t = raw.Trim();
            if (!dentro && Regex.IsMatch(t, @"^\d+$")) { bloco = int.Parse(t, CultureInfo.InvariantCulture); dentro = true; continue; }
            if (dentro && t.StartsWith("end", StringComparison.OrdinalIgnoreCase)) { dentro = false; continue; }
            if (dentro && t.Length > 0) yield return (bloco, raw);
        }
    }

    static int? SlotDaLinha(string linha)
    {
        var m = Regex.Match(linha, Regex.Escape(Marker) + @"(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    static string NomeDaLinha(string linha)
    {
        var m = Regex.Match(linha, Regex.Escape(Marker) + @"\d+\s*(.*)$");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    static string[] Colunas(string linha) => Regex.Split(linha.Split("//")[0].Trim(), @"\s+").Where(x => x.Length > 0).ToArray();

    /// <summary>Tira todas as linhas marcadas com este slot (em qualquer bloco).</summary>
    static void TirarSlot(List<string> linhas, int slot) =>
        linhas.RemoveAll(l => Regex.IsMatch(l, Regex.Escape(Marker) + slot + @"(?!\d)"));

    /// <summary>Insere linhas logo antes do "end" do bloco indicado.</summary>
    static void Inserir(List<string> linhas, int bloco, List<string> novas)
    {
        int fim = FimDoBloco(linhas, bloco) ?? throw new InvalidOperationException($"Bloco {bloco} não encontrado no InvasionManager.dat.");
        linhas.InsertRange(fim, novas);
    }

    static int? FimDoBloco(List<string> linhas, int bloco)
    {
        bool dentro = false; int atual = -1;
        for (int i = 0; i < linhas.Count; i++)
        {
            var t = linhas[i].Trim();
            if (!dentro && Regex.IsMatch(t, @"^\d+$")) { dentro = true; atual = int.Parse(t, CultureInfo.InvariantCulture); continue; }
            if (dentro && t.StartsWith("end", StringComparison.OrdinalIgnoreCase)) { if (atual == bloco) return i; dentro = false; }
        }
        return null;
    }

    // ---------- Portuguese.xml (anúncio) ----------

    static void GravarMensagens(MiniBoss mb)
    {
        if (!File.Exists(LangPath)) return;
        var text = File.ReadAllText(LangPath, Latin1);
        text = SemMsg(text, RespawnMsgId(mb.Slot));
        text = SemMsg(text, BossMsgId(mb.Slot));
        if (mb.Anuncia)
        {
            var mapa = Monsters.NomesDosMapas().GetValueOrDefault(mb.Mapa, $"mapa {mb.Mapa}");
            var bloco = $"\t\t<Msg ID=\"{RespawnMsgId(mb.Slot)}\" Text=\"{Xml($"{mb.Nome} apareceu em {mapa}!")}\" />\r\n"
                      + $"\t\t<Msg ID=\"{BossMsgId(mb.Slot)}\" Text=\"{Xml($"O jogador %s derrotou {mb.Nome}!")}\" />\r\n";
            var ancora = text.Contains("</InvacionMsg>") ? "</InvacionMsg>" : "</Language>";
            int i = text.IndexOf(ancora, StringComparison.Ordinal);
            if (i >= 0) text = text.Insert(i, bloco + "\t");
        }
        Backup(LangPath);
        File.WriteAllText(LangPath, text, Latin1);
    }

    static void ApagarMensagens(int slot)
    {
        if (!File.Exists(LangPath)) return;
        var text = File.ReadAllText(LangPath, Latin1);
        text = SemMsg(SemMsg(text, RespawnMsgId(slot)), BossMsgId(slot));
        Backup(LangPath);
        File.WriteAllText(LangPath, text, Latin1);
    }

    static string SemMsg(string text, int id) =>
        Regex.Replace(text, $"[ \t]*<Msg ID=\"{id}\"[^>]*/>\r?\n", "");

    static string Xml(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    // ---------- util ----------

    static (List<string> Linhas, string Newline) Ler(string path)
    {
        var text = File.ReadAllText(path, Latin1);
        return (Regex.Split(text, "\r?\n").ToList(), text.Contains("\r\n") ? "\r\n" : "\n");
    }

    static void Backup(string path)
    {
        var bak = $"{path}.bak-{DateTime.Now:yyyyMMdd}";
        if (!File.Exists(bak)) File.Copy(path, bak);
    }
}

using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MuChilaAdmin;

/// <summary>Um bônus por tempo (EXP, EXP master, drop) controlado pelo vigia.</summary>
public class TimedBonus
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public List<int> Types { get; set; } = new();       // índices de TimedBonuses.Types
    public decimal Multiplier { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool Cancelled { get; set; }
    public bool Announced { get; set; }                  // "começou" já avisado
    public bool Closed { get; set; }                     // "terminou" já avisado e lembrete tirado

    [JsonIgnore] public string Description =>
        $"{string.Join(" + ", TimedBonuses.Types.Where(t => Types.Contains(t.Index)).Select(t => t.Name))} x{Multiplier.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"))}";
    public string StateAt(DateTime now) => Cancelled ? "cancelado" : now < Start ? "agendado" : now < End ? "ATIVO" : "terminado";
    public bool ActiveAt(DateTime now) => !Cancelled && Start <= now && now < End;
}

/// <summary>
/// Bônus por tempo. Substitui o BonusManager.dat do kit (27/09/2026): lá o bônus é caixa-preta (não dá para ver se começou,
/// não dá para cancelar um ativo, e cada agendamento pedia Reload Event, que reinicia a contagem do Blood Castle).
/// Aqui o vigia (MuChilaAdmin --vigia-reset) chama Tick a cada poucos segundos: com bônus ativo, grava em cada
/// GameServer*\DATA\GameServerInfo - Common.dat a taxa do plano × (1 + soma dos (multiplicador − 1) dos bônus ativos
/// daquele tipo) e dá Reload Common; sem bônus, devolve a taxa original. Guarda em bonus.json a taxa original e a que
/// gravou: se alguém mudar a taxa à mão durante o bônus, o valor novo vira a base (não é sobrescrito na volta).
/// Avisa os jogadores pelo Notice.txt: "começou" (uma vez), lembrete a cada 5 min enquanto durar e "terminou".
/// </summary>
public static class TimedBonuses
{
    public static readonly (int Index, string Name, string RateKey)[] Types =
    {
        (0, "EXP", "AddExperienceRate"),
        (1, "EXP master", "AddMasterExperienceRate"),
        (2, "Drop", "ItemDropRate"),
    };
    public const string NoticeTag = "MuChilaAdmin-bonus";
    const int ReminderSeconds = 300;
    static string StatePath => Path.Combine(ServerControl.ServerRoot, "MuChilaAdmin", "bonus.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly Encoding Enc = Encoding.Latin1;

    public class State
    {
        public List<TimedBonus> Bonuses { get; set; } = new();
        public Dictionary<string, int> Base { get; set; } = new();      // "pasta|chave" → taxa sem bônus
        public Dictionary<string, int> Applied { get; set; } = new();   // "pasta|chave" → taxa que o vigia gravou
    }

    static State Read() => File.Exists(StatePath) ? JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new() : new();
    static void Write(State s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var tmp = StatePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, StatePath, overwrite: true);
    }

    /// <summary>Uma operação por vez (vigia e painel mexem no mesmo estado e nos mesmos arquivos).</summary>
    static T Locked<T>(Func<T> f)
    {
        using var m = new Mutex(false, @"Local\MuChilaBonus");
        bool got;
        try { got = m.WaitOne(TimeSpan.FromSeconds(20)); } catch (AbandonedMutexException) { got = true; }
        if (!got) throw new TimeoutException("Outro processo está mexendo nos bônus; tente de novo.");
        try { return f(); } finally { m.ReleaseMutex(); }
    }

    public static List<TimedBonus> List() => Locked(() => Read().Bonuses.OrderBy(b => b.Start).ToList());

    public static TimedBonus Schedule(IReadOnlyList<int> types, decimal multiplier, int minutes, DateTime start)
    {
        if (types.Count == 0) throw new ArgumentException("Escolha pelo menos um tipo de bônus.");
        if (multiplier <= 1 || multiplier > 20) throw new ArgumentException("O multiplicador vai de 1,1 a 20.");
        if (minutes < 1) throw new ArgumentException("Duração mínima: 1 minuto.");
        if (start < DateTime.Now.AddMinutes(-1)) throw new ArgumentException("O início já passou.");
        var b = new TimedBonus { Types = types.Distinct().OrderBy(x => x).ToList(), Multiplier = multiplier, Start = start, End = start.AddMinutes(minutes) };
        Locked(() => { var s = Read(); s.Bonuses.Add(b); Write(s); return 0; });
        return b;
    }

    public static string Cancel(string id) => Locked(() =>
    {
        var s = Read();
        var b = s.Bonuses.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("Bônus não encontrado.");
        if (b.Cancelled || b.End <= DateTime.Now) throw new InvalidOperationException("Esse bônus já acabou.");
        b.Cancelled = true;
        Write(s);
        return $"Bônus \"{b.Description}\" cancelado.";
    });

    /// <summary>Tira da lista os que acabaram há mais de 1 minuto (já com o "terminou" avisado).</summary>
    public static int CleanupFinished(DateTime? now = null) => Locked(() =>
    {
        var s = Read(); var agora = now ?? DateTime.Now;
        int n = s.Bonuses.RemoveAll(b => b.Closed && (b.Cancelled || b.End < agora.AddMinutes(-1)));
        if (n > 0) Write(s);
        return n;
    });

    static IEnumerable<string> ServerFolders() =>
        Directory.GetDirectories(ServerControl.ServerRoot, "GameServer*").Where(d => File.Exists(CommonPath(d))).OrderBy(d => d);
    static string CommonPath(string folder) => Path.Combine(folder, @"DATA\GameServerInfo - Common.dat");

    /// <summary>Aplica/desfaz as taxas e manda os avisos. Devolve o que fez (para o log). reload = recarregar os servidores.</summary>
    public static List<string> Tick(DateTime now, bool reload = true) => Locked(() =>
    {
        var log = new List<string>();
        var s = Read();
        var ativos = s.Bonuses.Where(b => b.ActiveAt(now)).ToList();
        bool mudouTaxa = false;

        foreach (var folder in ServerFolders())
        {
            var path = CommonPath(folder); var nome = Path.GetFileName(folder);
            var text = File.ReadAllText(path, Enc); var novo = text;
            foreach (var t in Types)
            {
                decimal mult = 1 + ativos.Where(b => b.Types.Contains(t.Index)).Sum(b => b.Multiplier - 1);
                for (int al = 0; al <= 3; al++)
                {
                    var chave = $"{t.RateKey}_AL{al}"; var id = $"{nome}|{chave}";
                    var m = Regex.Match(novo, $@"(?m)^(\s*{chave}\s*=\s*)(\d+)");
                    if (!m.Success) continue;
                    int atual = int.Parse(m.Groups[2].Value);
                    // mudaram à mão durante o bônus: o valor novo é a base
                    if (s.Applied.TryGetValue(id, out var gravado) && gravado != atual)
                    {
                        log.Add($"{nome}: {chave} foi mudado à mão para {atual} durante o bônus; esse passa a ser o valor normal.");
                        s.Base[id] = atual; s.Applied.Remove(id);
                    }
                    int baseVal = s.Base.TryGetValue(id, out var bv) ? bv : atual;
                    int desejado = mult == 1 ? baseVal : (int)Math.Round(baseVal * mult, MidpointRounding.AwayFromZero);
                    if (mult == 1) { s.Base.Remove(id); s.Applied.Remove(id); }
                    else { s.Base[id] = baseVal; s.Applied[id] = desejado; }
                    if (desejado != atual)
                        novo = novo[..m.Index] + m.Groups[1].Value + desejado + novo[(m.Index + m.Length)..];
                }
            }
            if (novo != text)
            {
                File.Copy(path, path + ".bak-bonus", overwrite: true);
                File.WriteAllText(path, novo, Enc);
                mudouTaxa = true;
            }
        }
        if (mudouTaxa)
        {
            log.Add(ativos.Count > 0 ? $"Taxas com bônus aplicadas ({string.Join("; ", ativos.Select(b => b.Description))})." : "Taxas normais devolvidas.");
            if (reload) log.AddRange(ServerControl.Reload("Common (inclui mensagens)"));
        }

        // avisos na tela
        var notices = Notices.Load(); bool mudouAviso = false;
        foreach (var b in s.Bonuses)
        {
            var tag = $"{NoticeTag} {b.Id}";
            if (b.ActiveAt(now) && !b.Announced)
            {
                notices.Add(new Notice { Message = Notices.Clean($"Começou: {b.Description} para todos, até {b.End:HH:mm}!"), RepeatTime = 1,
                                         Comment = $"{Notices.OneShotMarker} {now:yyyy-MM-dd HH:mm:ss}" });
                notices.Add(new Notice { Message = Notices.Clean($"Bônus ativo: {b.Description} até {b.End:HH:mm}."), RepeatTime = ReminderSeconds, Comment = tag });
                b.Announced = true; mudouAviso = true;
                log.Add($"Bônus começou: {b.Description} até {b.End:HH:mm}.");
            }
            else if (!b.ActiveAt(now) && now >= b.Start && !b.Closed)
            {
                notices.RemoveAll(n => n.Comment == tag);
                if (b.Announced)
                    notices.Add(new Notice { Message = Notices.Clean(b.Cancelled ? $"O bônus {b.Description} foi encerrado." : $"Terminou o bônus {b.Description}. Obrigado por jogar!"),
                                             RepeatTime = 1, Comment = $"{Notices.OneShotMarker} {now:yyyy-MM-dd HH:mm:ss}" });
                b.Closed = true; mudouAviso = true;
                log.Add($"Bônus {(b.Cancelled ? "cancelado" : "terminou")}: {b.Description}.");
            }
            else if (b.Cancelled && now < b.Start && !b.Closed) { b.Closed = true; }   // cancelado antes de começar: nada a avisar
        }
        if (mudouAviso)
        {
            Notices.Save(notices);
            if (reload)
            {
                log.AddRange(ServerControl.Reload("Util (GMs, avisos)"));
                // começou/terminou vai para a 1ª linha: sai já (o Reload sozinho não reinicia o rodízio, #31)
                var primeiro = Notices.Load().FirstOrDefault();
                bool agora = primeiro != null && primeiro.IsOneShot;
                log.AddRange(ResetWatcher.NoticeRestart(sendFirstNow: agora, firstMessage: agora ? primeiro!.Message : null));
            }
        }
        Write(s);
        return log;
    });
}

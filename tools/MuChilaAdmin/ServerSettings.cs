using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin;

/// <summary>
/// Opções dos GameServers: os 7 arquivos "GameServerInfo - X.dat" (INI "Chave = valor") de cada pasta GameServer*\DATA.
/// As 5 pastas (Mu Chila, Non-PvP, VIP, Castle Siege, BattleCore) têm quase tudo igual; o editor grava a mesma mudança
/// em todas e dá o Reload do arquivo. Opções que já são diferentes entre os servidores (nome, porta, PvP, eventos
/// ligados...) e as de conexão/identidade ficam travadas. Opções "_AL0.._AL3" viram uma linha com Free/Vip1/Vip2/Vipzão.
/// Linhas que não mudam são regravadas iguais (troca só o valor, mantendo espaços e tabs), com backup .bak-* antes.
/// </summary>
public static class ServerSettings
{
    public sealed record FileDef(string Name, string Label, string ReloadItem);

    public static readonly FileDef[] Files =
    {
        new("Common", "Geral (Common): EXP, drop, zen, joias, pontos, Helper...", "Common (inclui mensagens)"),
        new("ChaosMix", "Chaos Machine (chances e custos)", "ChaosMix"),
        new("Custom", "Custom: /offattack, loja offline, arena...", "Custom"),
        new("Command", "Comandos: /ware, /post, /reset...", "Command"),
        new("Character", "Personagem: dano, defesa, velocidade", "Character"),
        new("Skill", "Habilidades (buffs)", "Skill"),
        new("Event", "Eventos (Reload Event reinicia a contagem do Blood Castle)", "Event"),
    };

    public const string ItemDropFile = "ItemDrop";
    static readonly Encoding Enc = Encoding.Latin1;
    static readonly string[] LockedSections = { "Customer Settings", "Server Settings", "Server Types", "DB Settings", "Connection Settings" };
    // opções que o bônus por tempo (vigia) multiplica: com bônus ativo, o arquivo tem o valor com bônus
    public static readonly string[] BonusKeys = TimedBonuses.Types.Select(t => t.RateKey).ToArray();

    public static IReadOnlyList<string> ServerFolders() =>
        Directory.GetDirectories(ServerControl.ServerRoot, "GameServer*").Where(d => File.Exists(PathOf(d, "Common"))).OrderBy(d => d).ToList();
    static string PathOf(string folder, string file) => Path.Combine(folder, "DATA", $"GameServerInfo - {file}.dat");

    public static readonly string[] Plans = { "Free", "Vip1", "Vip2", "Vipzão" };

    public sealed class Row
    {
        public string File = "", Section = "", BaseKey = "", Label = "", Help = "";
        public bool PerPlan;
        public string[] Values = new string[4];      // PerPlan: _AL0.._AL3; senão só [0]
        public string? LockReason;                   // não editável (e por quê)
        public bool RestartOnly;                     // só vale ao reiniciar o GameServer
        internal int RuleIndex = -1, RuleItem;       // linhas do ItemDrop.txt (drop de joias)
        public string Id => $"{File}|{BaseKey}";
        public string KeyOf(int plan) => PerPlan ? $"{BaseKey}_AL{plan}" : BaseKey;
        public bool IsNumeric => Values.Where(v => v != null).All(v => Regex.IsMatch(v, @"^-?\d+$"));
    }

    // ---------- leitura ----------
    static readonly Regex KeyLine = new(@"^\s*([A-Za-z0-9_]+)\s*=[ \t]*(.*?)[ \t]*$");

    /// <summary>Chaves do arquivo na ordem, com seção e valor, de uma pasta.</summary>
    static List<(string Key, string Section, string Value)> ReadFile(string path)
    {
        var list = new List<(string, string, string)>();
        string section = ""; bool started = false;
        foreach (var line in File.ReadAllLines(path, Enc))
        {
            var t = line.Trim();
            if (t.StartsWith('[')) { started = true; continue; }
            if (!started) continue;   // cabeçalho do kit (autor, telefone...) antes do [GameServerInfo]
            if (t.StartsWith(';'))
            {
                var c = t.TrimStart(';').Trim();
                if (c.Length > 0 && c.Any(ch => ch != '=')) section = c;
                continue;
            }
            var m = KeyLine.Match(line);
            if (m.Success) list.Add((m.Groups[1].Value, section, m.Groups[2].Value));
        }
        return list;
    }

    public static List<Row> Load(string file)
    {
        var folders = ServerFolders();
        var per = folders.Select(f => File.Exists(PathOf(f, file)) ? ReadFile(PathOf(f, file)) : new()).ToList();
        if (per.Count == 0 || per[0].Count == 0) return new();
        var values = new Dictionary<string, List<string?>>();
        foreach (var (k, _, _) in per[0]) values[k] = per.Select(p => p.FirstOrDefault(x => x.Key == k).Value).ToList();
        // chave repetida no arquivo (o kit tem algumas, ex.: MGDamageRateToRF no Character): não dá para saber qual vale
        var repeated = per[0].GroupBy(x => x.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

        var rows = new List<Row>(); var done = new HashSet<string>();
        foreach (var (key, section, _) in per[0])
        {
            if (done.Contains(key)) continue;
            var m = Regex.Match(key, @"^(.+)_AL([0-3])$");
            if (m.Success && Enumerable.Range(0, 4).All(i => values.ContainsKey($"{m.Groups[1].Value}_AL{i}") && !repeated.Contains($"{m.Groups[1].Value}_AL{i}")))
            {
                var b = m.Groups[1].Value;
                var keys = Enumerable.Range(0, 4).Select(i => $"{b}_AL{i}").ToList();
                keys.ForEach(k => done.Add(k));
                rows.Add(Make(file, section, b, true, keys.Select(k => values[k]).ToList()));
            }
            else
            {
                done.Add(key);
                var row = Make(file, section, key, false, new() { values[key] });
                if (repeated.Contains(key)) row.LockReason = "aparece mais de uma vez no arquivo: mude direto no arquivo";
                rows.Add(row);
            }
        }
        return rows;
    }

    static Row Make(string file, string section, string baseKey, bool perPlan, List<List<string?>> vals)
    {
        var r = new Row { File = file, Section = section, BaseKey = baseKey, PerPlan = perPlan };
        for (int i = 0; i < vals.Count; i++) r.Values[i] = vals[i][0] ?? "";
        bool differs = vals.Any(v => v.Distinct().Count() > 1);
        if (differs) r.LockReason = "diferente em cada servidor (nome, porta, PvP, eventos...): mude direto no arquivo de cada um";
        else if (file == "Common" && LockedSections.Contains(section)) r.LockReason = "identidade/conexão do servidor: não mexer por aqui";
        else if (vals.Any(v => v.Any(x => x == null))) r.LockReason = "falta em algum servidor";
        // os logs são abertos quando o GameServer liga (WriteChaosMixLog: conferido na memória em 29/09/2026)
        if (file == "Common" && baseKey.StartsWith("Write") && baseKey.EndsWith("Log")) r.RestartOnly = true;
        (r.Label, r.Help) = Describe(file, baseKey);
        if (r.RestartOnly) r.Help = (r.Help.Length > 0 ? r.Help + " " : "") + "Só vale depois de reiniciar o GameServer.";
        return r;
    }

    // ---------- visão "Principais" ----------
    static readonly (string File, string Key)[] MainKeys =
    {
        ("Common", "AddExperienceRate"), ("Common", "AddMasterExperienceRate"), ("Common", "MinMasterExperienceMonsterLevel"),
        ("Common", "AddEventExperienceRate"), ("Common", "AddQuestExperienceRate"), ("Common", "MaxLevelUp"),
        ("Common", "ItemDropRate"), ("Common", "ItemDropTime"), ("Common", "MoneyAmountDropRate"), ("Common", "MoneyDropTime"),
        ("Common", "SoulSuccessRate"), ("Common", "LifeSuccessRate"), ("Common", "HarmonySuccessRate"),
        ("ChaosMix", "PlusItemLevelMixRate1"), ("ChaosMix", "PlusItemLevelMixRate2"), ("ChaosMix", "PlusItemLevelMixRate3"),
        ("ChaosMix", "PlusItemLevelMixRate4"), ("ChaosMix", "PlusItemLevelMixRate5"), ("ChaosMix", "PlusItemLevelMixRate6"),
        ("Common", "MaxStatPoint"), ("Common", "DWLevelUpPoint"), ("Common", "DKLevelUpPoint"), ("Common", "FELevelUpPoint"),
        ("Common", "MGLevelUpPoint"), ("Common", "DLLevelUpPoint"), ("Common", "SULevelUpPoint"), ("Common", "RFLevelUpPoint"),
        ("Common", "GLLevelUpPoint"), ("Common", "PlusStatPoint"),
        ("Command", "CommandWareNumber"),
        ("Common", "HelperActiveMoney1"), ("Common", "HelperActiveMoney2"), ("Common", "HelperActiveMoney3"),
        ("Common", "HelperActiveMoney4"), ("Common", "HelperActiveMoney5"),
        ("Custom", "CustomAttackOfflineEnable"), ("Custom", "CustomAttackOfflineRequireMoney"), ("Custom", "CustomAttackOfflineMaxUsingTime"),
        ("Common", "CashShopGoblinPointDelay"), ("Common", "CashShopGoblinPointValue"),
        ("Common", "PartyGeneralExperience1"), ("Common", "PartyGeneralExperience2"), ("Common", "PartyGeneralExperience3"),
        ("Common", "PartyGeneralExperience4"), ("Common", "PartyGeneralExperience5"),
    };

    public static List<Row> Main()
    {
        var all = MainKeys.Select(k => k.File).Distinct().ToDictionary(f => f, f => Load(f).ToDictionary(r => r.BaseKey));
        var rows = new List<Row>();
        foreach (var (f, k) in MainKeys)
            if (all[f].TryGetValue(k, out var r)) rows.Add(r);
        rows.InsertRange(rows.FindIndex(r => r.BaseKey == "SoulSuccessRate") is var i and >= 0 ? i : rows.Count, JewelDropRows());
        return rows;
    }

    /// <summary>Regras do ItemDrop.txt que dão joias (a chance é a mesma para todos os planos: o kit não separa por plano).</summary>
    public static List<Row> JewelDropRows()
    {
        var rows = new List<Row>();
        var rules = Drops.Load();
        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            var name = Shops.Find(r.Section, r.Type)?.Name ?? $"item {r.Section},{r.Type}";
            if (!(name.Contains("Jewel", StringComparison.OrdinalIgnoreCase) || name.Contains("Gemstone", StringComparison.OrdinalIgnoreCase))) continue;
            var onde = new List<string>();
            if (r.LevelMin != "*" || r.LevelMax != "*") onde.Add($"monstros nível {r.LevelMin}–{r.LevelMax}");
            if (r.Map != "*") onde.Add($"mapa {r.Map}");
            if (r.Monster != "*") onde.Add($"monstro {r.Monster}");
            var um = r.Rate > 0 ? $"1 em {Drops.RateBase / (double)r.Rate:N0}" : "não cai";
            rows.Add(new Row
            {
                File = ItemDropFile, Section = "Drop de joias (ItemDrop.txt)", BaseKey = $"regra {i + 1}", PerPlan = false,
                Values = { [0] = Drops.Percent(r.Rate) },
                Label = $"Drop: {name}{(onde.Count > 0 ? " (" + string.Join(", ", onde) + ")" : "")}",
                Help = $"% por monstro morto ({um}). Igual para todos os planos. Salvar dá Reload Item.",
                RuleIndex = i, RuleItem = r.Item,
            });
        }
        return rows;
    }

    // ---------- gravação ----------
    /// <summary>Uma mudança pedida: chave completa (com _ALn) e valor novo.</summary>
    public sealed record Change(string File, string Key, string Value, Row Row);

    /// <summary>
    /// Grava as mudanças de um arquivo INI em todas as pastas GameServer*. Valida antes (nada é gravado se algo estiver
    /// errado). Não recarrega (quem chama recarrega). Devolve o texto para o log.
    /// </summary>
    public static string Save(string file, IReadOnlyList<Change> changes)
    {
        if (changes.Count == 0) return "";
        Validate(changes);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var texts = new Dictionary<string, (string Old, string New)>();
        foreach (var folder in ServerFolders())
        {
            var path = PathOf(folder, file);
            if (!File.Exists(path)) continue;
            var text = File.ReadAllText(path, Enc);
            var lines = text.Split('\n');   // mantém o \r no fim de cada linha
            foreach (var c in changes)
            {
                int hit = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    var cr = lines[i].EndsWith('\r');
                    var body = cr ? lines[i][..^1] : lines[i];
                    var m = Regex.Match(body, $@"^(\s*{Regex.Escape(c.Key)}\s*=[ \t]*)(.*?)([ \t]*)$");
                    if (!m.Success) continue;
                    lines[i] = m.Groups[1].Value + c.Value.Trim() + m.Groups[3].Value + (cr ? "\r" : "");
                    hit++;
                }
                if (hit != 1) throw new InvalidOperationException($"{c.Key} aparece {hit} vez(es) em {Path.GetFileName(folder)}\\DATA\\GameServerInfo - {file}.dat (esperado 1). Nada foi gravado.");
            }
            texts[path] = (text, string.Join('\n', lines));
        }
        foreach (var (path, (old, novo)) in texts)
        {
            if (old == novo) continue;
            File.Copy(path, $"{path}.bak-{stamp}", overwrite: true);
            File.WriteAllText(path, novo, Enc);
        }
        return $"{file}: {changes.Count} opção(ões) gravada(s) em {texts.Count} servidor(es): " +
               string.Join(", ", changes.Select(c => $"{c.Key} = {c.Value.Trim()}")) + $". Backup .bak-{stamp}.";
    }

    /// <summary>Confere as mudanças antes de gravar qualquer arquivo (joga InvalidOperationException com o motivo).</summary>
    public static void Validate(IEnumerable<Change> changes)
    {
        foreach (var c in changes)
        {
            if (c.Row.LockReason != null) throw new InvalidOperationException($"{c.Key} não pode ser mudado por aqui: {c.Row.LockReason}.");
            if (c.Value.Contains('\n') || c.Value.Contains('\r') || c.Value.Trim().Length == 0) throw new InvalidOperationException($"{c.Key}: valor vazio ou com quebra de linha.");
            if (c.Row.IsNumeric && !int.TryParse(c.Value.Trim(), out _)) throw new InvalidOperationException($"{c.Key}: use só números (pode ser negativo).");
            if (BonusKeys.Any(b => c.Key.StartsWith(b + "_AL")) && TimedBonuses.List().Any(b => b.ActiveAt(DateTime.Now)))
                throw new InvalidOperationException($"Há um bônus por tempo ativo: {c.Key} está com o valor do bônus no arquivo. Espere o bônus acabar (ou cancele na aba Bônus) e mude depois.");
        }
    }

    /// <summary>Grava a chance (em %) das regras de joia do ItemDrop.txt. Não recarrega.</summary>
    public static string SaveJewelDrops(IReadOnlyList<(Row Row, string Percent)> changes)
    {
        if (changes.Count == 0) return "";
        var rules = Drops.Load();
        var log = new List<string>();
        foreach (var (row, pct) in changes)
        {
            if (row.RuleIndex < 0 || row.RuleIndex >= rules.Count || rules[row.RuleIndex].Item != row.RuleItem)
                throw new InvalidOperationException("O ItemDrop.txt mudou desde que a lista foi aberta: clique em \"Recarregar da pasta\" e tente de novo.");
            int rate = Drops.ParsePercent(pct);
            rules[row.RuleIndex].Rate = rate;
            log.Add($"{row.Label} = {Drops.Percent(rate)}%");
        }
        return Drops.Save(rules) + " " + string.Join("; ", log);
    }

    // ---------- salvar, recarregar e conferir na memória ----------
    /// <summary>
    /// Grava tudo (arquivos INI + joias), dá o Reload de cada arquivo mexido e confere na memória dos GameServers se as
    /// opções numéricas valeram na hora. Demora alguns segundos: chame fora da tela (Task.Run). O log vai para "log".
    /// </summary>
    public static void SaveAndApply(IReadOnlyList<Change> changes, IReadOnlyList<(Row Row, string Percent)> jewels, Action<string> log)
    {
        // ----- antes: onde cada opção fica na memória e se o endereço confere com o arquivo -----
        var numeric = changes.Where(c => c.Row.IsNumeric).ToList();
        var validation = new Dictionary<string, List<(string Key, int Value)>>();
        foreach (var f in numeric.Select(c => c.File).Distinct())
            validation[f] = Load(f).Where(r => r.LockReason == null && r.IsNumeric && !r.RestartOnly)
                .SelectMany(r => Enumerable.Range(0, r.PerPlan ? 4 : 1).Select(i => (Key: r.KeyOf(i), Value: int.Parse(r.Values[i]))))
                .Where(x => x.Value is < -1 or > 1 && !numeric.Any(c => c.Key == x.Key)).Take(8).ToList();
        var probeKeys = numeric.Select(c => c.Key).Concat(validation.Values.SelectMany(v => v.Select(x => x.Key))).Distinct().ToList();
        Dictionary<string, Dictionary<string, int?>> before = new();
        try { if (probeKeys.Count > 0) before = ResetWatcher.ConfigRead(probeKeys); } catch (Exception ex) { log($"(não deu para ler a memória antes: {ex.Message})"); }

        // ----- grava e recarrega -----
        foreach (var g in changes.GroupBy(c => c.File))
        {
            log(Save(g.Key, g.ToList()));
            var def = Files.First(f => f.Name == g.Key);
            foreach (var l in ServerControl.Reload(def.ReloadItem)) log(l);
        }
        if (jewels.Count > 0)
        {
            log(SaveJewelDrops(jewels));
            foreach (var l in ServerControl.Reload("Item")) log(l);
        }
        if (numeric.Count == 0 || before.Count == 0) return;

        // ----- depois: espera o Reload (até 8 s) e compara -----
        bool BaseOk(string server, string file) =>
            before.TryGetValue(server, out var v) && validation.TryGetValue(file, out var keys) &&
            keys.Count(k => v.GetValueOrDefault(k.Key) is int x && x == k.Value) is var ok && ok >= 2 &&
            keys.All(k => v.GetValueOrDefault(k.Key) is not int x || x == k.Value);
        int Old(Change c) => int.Parse(c.Row.Values[c.Row.PerPlan ? c.Key[^1] - '0' : 0]);
        Dictionary<string, Dictionary<string, int?>> after = new();
        var keysNow = numeric.Select(c => c.Key).ToList();
        for (int i = 0; i < 16; i++)
        {
            Thread.Sleep(500);
            after = ResetWatcher.ConfigRead(keysNow);
            if (numeric.All(c => after.Values.All(v => v.GetValueOrDefault(c.Key) is not int x || x == int.Parse(c.Value.Trim())))) break;
        }
        foreach (var c in numeric)
        {
            int novo = int.Parse(c.Value.Trim()), antigo = Old(c);
            var ok = new List<string>(); var velho = new List<string>(); var outro = new List<string>();
            foreach (var (server, v) in after)
            {
                if (!BaseOk(server, c.File) || before[server].GetValueOrDefault(c.Key) is not int b || b != antigo || v.GetValueOrDefault(c.Key) is not int a) continue;
                (a == novo ? ok : a == antigo ? velho : outro).Add(server);
            }
            if (ok.Count + velho.Count + outro.Count == 0) log($"   {c.Key}: (não deu para conferir na memória)");
            else if (velho.Count == 0) log($"   {c.Key}: ✓ valendo na hora{(outro.Count > 0 ? " (o servidor converte o valor)" : "")} em {ok.Count + outro.Count} GameServer(s)");
            else log($"   {c.Key}: ✗ continua {antigo} na memória de {string.Join(", ", velho)}: esta opção só vale depois de REINICIAR o GameServer");
        }
    }

    // ---------- nomes em português ----------
    static readonly Dictionary<string, (string Label, string Help)> Names = new()
    {
        ["AddExperienceRate"] = ("EXP (x)", "Multiplicador de EXP. A EXP dinâmica (ExperienceTable.txt) e os bônus aplicam por cima."),
        ["AddMasterExperienceRate"] = ("EXP master (x)", "Multiplicador da EXP depois do nível 400."),
        ["MinMasterExperienceMonsterLevel"] = ("EXP master: nível mínimo do monstro", "Monstros abaixo deste nível não dão EXP master."),
        ["AddEventExperienceRate"] = ("EXP de eventos (%)", "100 = normal."),
        ["AddQuestExperienceRate"] = ("EXP de quests (%)", "100 = normal."),
        ["MaxLevelUp"] = ("Níveis máximos por monstro morto", "1 = sobe no máximo 1 nível por monstro."),
        ["ItemDropRate"] = ("Drop de itens (%)", "Drop comum: chance por monstro ≈ valor ÷ ItemRate do monstro (Monster.txt). Não mexe nas joias."),
        ["ItemDropTime"] = ("Item fica no chão (s)", ""),
        ["MoneyAmountDropRate"] = ("Zen: quantidade (%)", "O zen que cai = EXP que o monstro deu × este %. A chance de cair zen é 10 ÷ MoneyRate do monstro (Monster.txt)."),
        ["MoneyDropTime"] = ("Zen fica no chão (s)", ""),
        ["SoulSuccessRate"] = ("Joia da Alma (Soul): chance de sucesso (%)", ""),
        ["LifeSuccessRate"] = ("Joia da Vida (Life): chance de sucesso (%)", ""),
        ["HarmonySuccessRate"] = ("Joia da Harmonia: chance de sucesso (%)", ""),
        ["SmeltStoneSuccessRate1"] = ("Pedra de refino (menor): chance (%)", ""),
        ["SmeltStoneSuccessRate2"] = ("Pedra de refino (maior): chance (%)", ""),
        ["AddLuckSuccessRate1"] = ("Sorte: bônus de sucesso 1 (%)", ""),
        ["AddLuckSuccessRate2"] = ("Sorte: bônus de sucesso 2 (%)", ""),
        ["PlusItemLevelMixRate1"] = ("Chaos Machine: +10 (%)", "Chance base da combinação para +10."),
        ["PlusItemLevelMixRate2"] = ("Chaos Machine: +11 (%)", ""),
        ["PlusItemLevelMixRate3"] = ("Chaos Machine: +12 (%)", ""),
        ["PlusItemLevelMixRate4"] = ("Chaos Machine: +13 (%)", ""),
        ["PlusItemLevelMixRate5"] = ("Chaos Machine: +14 (%)", ""),
        ["PlusItemLevelMixRate6"] = ("Chaos Machine: +15 (%)", ""),
        ["MaxStatPoint"] = ("Máximo por atributo (For/Agi/Vit/Ene/Com)", ""),
        ["DWLevelUpPoint"] = ("Pontos por nível: Dark Wizard", ""),
        ["DKLevelUpPoint"] = ("Pontos por nível: Dark Knight", ""),
        ["FELevelUpPoint"] = ("Pontos por nível: Fairy Elf", ""),
        ["MGLevelUpPoint"] = ("Pontos por nível: Magic Gladiator", ""),
        ["DLLevelUpPoint"] = ("Pontos por nível: Dark Lord", ""),
        ["SULevelUpPoint"] = ("Pontos por nível: Summoner", ""),
        ["RFLevelUpPoint"] = ("Pontos por nível: Rage Fighter", ""),
        ["GLLevelUpPoint"] = ("Pontos por nível: Grow Lancer", ""),
        ["PlusStatPoint"] = ("Pontos extras por nível (depois da 3ª quest)", ""),
        ["CommandWareNumber"] = ("Baús extras (/ware)", "Quantos baús além do principal."),
        ["HelperActiveMoney1"] = ("MU Helper: zen por nível a cada 5 min (faixa 1)", "Custo = (nível + master) × este valor, cobrado a cada 5 minutos."),
        ["HelperActiveMoney2"] = ("MU Helper: zen por nível (faixa 2)", ""),
        ["HelperActiveMoney3"] = ("MU Helper: zen por nível (faixa 3)", ""),
        ["HelperActiveMoney4"] = ("MU Helper: zen por nível (faixa 4)", ""),
        ["HelperActiveMoney5"] = ("MU Helper: zen por nível (faixa 5)", ""),
        ["CustomAttackOfflineEnable"] = ("/offattack liberado (1 = sim)", ""),
        ["CustomAttackOfflineRequireMoney"] = ("/offattack: custo em zen", ""),
        ["CustomAttackOfflineMaxUsingTime"] = ("/offattack: tempo máximo (min)", ""),
        ["CashShopGoblinPointDelay"] = ("Goblin Point: minutos online por entrega", ""),
        ["CashShopGoblinPointValue"] = ("Goblin Point: pontos por entrega", ""),
        ["PartyGeneralExperience1"] = ("Party: EXP com 1 membro (%)", ""),
        ["PartyGeneralExperience2"] = ("Party: EXP com 2 membros (%)", ""),
        ["PartyGeneralExperience3"] = ("Party: EXP com 3 membros (%)", ""),
        ["PartyGeneralExperience4"] = ("Party: EXP com 4 membros (%)", ""),
        ["PartyGeneralExperience5"] = ("Party: EXP com 5 membros (%)", ""),
        ["MaxIpConnection"] = ("Contas por IP", ""),
        ["ServerMaxUserNumber"] = ("Máximo de jogadores", ""),
        ["MonsterLifeRate"] = ("Vida dos monstros (%)", ""),
        ["PKDownTime1"] = ("PK: tempo para baixar o PK (s)", ""),
        ["GuildCreateMinLevel"] = ("Guild: nível mínimo para criar", ""),
        ["ElfBufferMaxLevel"] = ("Buff da Elfa (NPC): até o nível", ""),
        ["WriteChaosMixLog"] = ("Registrar combinações da Chaos Machine (1 = sim)", "Grava em GameServer*\\CHAOS_MIX_LOG."),
    };

    static (string, string) Describe(string file, string baseKey) => Names.TryGetValue(baseKey, out var n) ? n : (baseKey, "");

    /// <summary>Nome da seção em português (as principais); as outras ficam como no arquivo.</summary>
    public static string SectionLabel(string s) => s switch
    {
        "Log Settings" => "Registros (logs)", "Hack Settings" => "Anti-hack", "Common Settings" => "Geral",
        "Welcome System Settings" => "Boas-vindas", "Monster Settings" => "Monstros", "PK Settings" => "PK",
        "Trade Settings" => "Troca", "Personal Shop Settings" => "Loja pessoal", "Duel Settings" => "Duelo", "Guild Settings" => "Guild",
        "Elf Buffer Settings" => "Buff da Elfa (NPC)", "Experience Settings" => "Experiência", "Item Drop Settings" => "Drop de itens",
        "Money Drop Settings" => "Drop de zen", "Item Durability Settings" => "Durabilidade", "Restore Sold Item Settings" => "Recomprar item vendido",
        "Trade Item Block Settings" => "Bloqueio de troca", "Level Up Settings" => "Nível e pontos", "Create Character Settings" => "Criar personagem",
        "Disable Character Settings" => "Classes desligadas", "Shield Gauge Settings" => "SD (escudo)", "Master Skill Tree Settings" => "Árvore master",
        "Gens System Settings" => "Gens", "Helper Settings" => "MU Helper", "Cash Shop Settings" => "Loja de Cash / Goblin Point",
        "Event Inventory Settings" => "Inventário de evento", "Mu Rummy Settings" => "Mu Rummy", "Party Settings" => "Party",
        "Potion Settings" => "Poções", "Transformation Ring Settings" => "Anéis de transformação", "Jewel Settings" => "Joias (chance de sucesso)",
        "Fruit Settings" => "Frutas", "Plus Item Mix Settings" => "Chaos Machine: +10 a +15", "Ware Command Settings" => "/ware (baús)",
        "Custom Attack Settings" => "Ataque automático (/attack)", "Custom Attack Offline Settings" => "/offattack",
        "Custom Store Settings" => "Loja (/store)", "Custom Store Offline Settings" => "Loja offline",
        _ => s,
    };
}

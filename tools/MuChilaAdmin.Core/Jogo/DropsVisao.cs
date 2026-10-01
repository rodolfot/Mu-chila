using System.IO;

namespace MuChilaAdmin.Core;

/// <summary>
/// Drops "por monstro / por item" (pedido do dono, 28/09/2026: a grade do arquivo era confusa). Para um monstro: tudo o que
/// ele dropa, inclusive as regras que valem para vários (por mapa, faixa de nível ou qualquer monstro), com a chance em % e em
/// "1 em N", mais o drop comum e o zen dele. Para um item: de onde ele cai. A grade avançada mexe nas MESMAS regras: tudo fica
/// na memória até o <see cref="Salvar"/>, que grava o ItemDrop.txt e o Monster.txt.
/// </summary>
public sealed class DropsVisao
{
    public DropRules Regras { get; private set; } = new();
    public List<MonsterRate> Monstros { get; private set; } = new();
    /// <summary>Classe do monstro → mapas onde ele nasce. Quem não está aqui só aparece em evento ou invasão.</summary>
    public Dictionary<int, HashSet<int>> MapasDoMonstro { get; private set; } = new();
    public Dictionary<int, string> NomesMapas { get; private set; } = new();
    public Dictionary<int, string> NomesMonstros { get; private set; } = new();
    Dictionary<int, MonsterRate> taxasLidas = new();
    int regrasLidas;

    public static DropsVisao Carregar()
    {
        var v = new DropsVisao
        {
            Regras = Drops.Load(), Monstros = Drops.MonsterRates(), NomesMonstros = Drops.MonsterNames(),
            NomesMapas = Monsters.NomesDosMapas(), MapasDoMonstro = Monsters.MapasPorMonstro(),
        };
        v.taxasLidas = v.Monstros.ToDictionary(m => m.Index);
        v.regrasLidas = v.Regras.Count;
        return v;
    }

    // ---------------- textos ----------------

    public static string Pct(int rate) => rate < 0 ? "?" : Drops.Percent(rate).Replace('.', ',') + "%";
    public static string UmEm(int rate) => rate <= 0 ? "nunca" : $"1 em {Math.Max(1, Math.Round((double)Drops.RateBase / rate)):N0}";
    public static string NomeItem(int item) => Shops.Name(item / 512, item % 512);

    public string NomeMapa(string v) => v == "*" ? "(todos)" : int.TryParse(v, out var n) && NomesMapas.TryGetValue(n, out var s) ? s : $"mapa {v}";
    public string NomeMonstro(string v) => v == "*" ? "(todos)" : int.TryParse(v, out var n) && NomesMonstros.TryGetValue(n, out var s) ? s : "(não existe)";

    /// <summary>Onde o monstro nasce ("Lorencia, Noria..."), ou "(evento/invasão)".</summary>
    public string Onde(int monstro, int max = int.MaxValue) =>
        MapasDoMonstro.TryGetValue(monstro, out var maps)
            ? string.Join(", ", maps.Order().Select(x => NomeMapa(x.ToString())).Distinct().Take(max)) + (maps.Count > max ? "..." : "")
            : "(evento/invasão)";

    // ---------------- para quem a regra vale ----------------

    public bool Vale(DropRule r, MonsterRate m)
    {
        if (r.Monster != "*" && (!int.TryParse(r.Monster, out var c) || c != m.Index)) return false;
        if (r.Map != "*" && (!int.TryParse(r.Map, out var mp) || !MapasDoMonstro.TryGetValue(m.Index, out var maps) || !maps.Contains(mp))) return false;
        if (r.LevelMin != "*" && int.TryParse(r.LevelMin, out var a) && m.Level < a) return false;
        if (r.LevelMax != "*" && int.TryParse(r.LevelMax, out var b) && m.Level > b) return false;
        return true;
    }

    /// <summary>Quantos monstros a regra alcança.</summary>
    public int Alcance(DropRule r) => Monstros.Count(m => Vale(r, m));

    /// <summary>Para quem a regra vale, em português ("só Blade Hunter", "monstros de Kalima de nível 12–150", "qualquer monstro").</summary>
    public string ParaQuem(DropRule r)
    {
        var mapa = r.Map == "*" ? "" : $" em {NomeMapa(r.Map)}";
        if (r.Monster != "*") return $"só {NomeMonstro(r.Monster)}{mapa}";
        var nivel = (r.LevelMin, r.LevelMax) switch
        {
            ("*", "*") => "",
            (_, "*") => $" de nível {r.LevelMin} ou mais",
            ("*", _) => $" até o nível {r.LevelMax}",
            _ => $" de nível {r.LevelMin}–{r.LevelMax}",
        };
        return r.Map == "*" && nivel == "" ? "qualquer monstro" : $"monstros{mapa}{nivel}";
    }

    public IEnumerable<DropRule> DoMonstro(MonsterRate m) => Regras.Where(r => Vale(r, m)).OrderByDescending(r => r.Rate);
    public IEnumerable<DropRule> DoItem(int item) => Regras.Where(r => r.Item == item).OrderByDescending(r => r.Rate);

    /// <summary>Itens que caem de algum lugar, com quantas regras cada um tem.</summary>
    public List<(int Item, string Nome, int Regras)> Itens() =>
        Regras.GroupBy(r => r.Item).Select(g => (g.Key, NomeItem(g.Key), g.Count())).OrderBy(x => x.Item2, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Monstros na ordem da tela: primeiro os que nascem em mapas (por nível), depois os de evento/invasão.</summary>
    public IEnumerable<MonsterRate> MonstrosOrdenados() =>
        Monstros.OrderBy(m => MapasDoMonstro.ContainsKey(m.Index) ? 0 : 1).ThenBy(m => m.Level).ThenBy(m => m.Name);

    public MonsterRate? Monstro(int index) => Monstros.FirstOrDefault(m => m.Index == index);

    // ---------------- mudanças (na memória até Salvar) ----------------

    public DropRule NovaRegra(int item, int rate) => new() { Item = item, Rate = rate, Comment = $"{NomeItem(item)} - Mu Chila" };

    public void MudarTaxas(int monstro, int itemRate, int moneyRate, int maxItemLevel)
    {
        int i = Monstros.FindIndex(m => m.Index == monstro);
        if (i < 0) throw new InvalidOperationException($"Monstro {monstro} não existe no Monster.txt.");
        Monstros[i] = Monstros[i] with { ItemRate = itemRate, MoneyRate = moneyRate, MaxItemLevel = maxItemLevel };
    }

    public List<MonsterRate> TaxasMudadas() => Monstros.Where(m => taxasLidas.TryGetValue(m.Index, out var o) && o != m).ToList();

    public bool TaxaMudou(MonsterRate m) => taxasLidas.TryGetValue(m.Index, out var o) && o != m;

    /// <summary>A regra mudou desde a leitura (ou é nova)?</summary>
    public static bool RegraMudou(DropRule r) => r.Raw == null || r.Key() != r.RawKey;

    public bool RegrasMudaram => Regras.Count != regrasLidas || Regras.Any(r => r.Raw == null || r.Key() != r.RawKey);

    public bool Mudou => RegrasMudaram || TaxasMudadas().Count > 0;

    /// <summary>Confere todas as regras (o mesmo que a grade antiga conferia ao salvar). Devolve o primeiro problema ou null.</summary>
    public string? Problema()
    {
        foreach (var r in Regras)
        {
            var nome = NomeItem(r.Item);
            string? Campo(string rotulo, string v, int min, int max) => Drops.Valid(v.Trim(), min, max) ? null : $"{nome}: \"{rotulo}\" inválido ({v}). Use \"*\" ou um número de {min} a {max}.";
            var p = Campo("Nível", r.Level, 0, 15) ?? Campo("Grade", r.Grade, 0, 255)
                    ?? r.Options.Select((o, i) => Campo($"Op{i}", o, 0, 255)).FirstOrDefault(x => x != null)
                    ?? Campo("Duração", r.Duration, 0, int.MaxValue) ?? Campo("Mapa", r.Map, 0, 255) ?? Campo("Monstro", r.Monster, 0, 65535)
                    ?? Campo("Nível mín", r.LevelMin, 0, 1000) ?? Campo("Nível máx", r.LevelMax, 0, 1000);
            if (p != null) return p;
            if (r.Rate is < 0 or > Drops.RateBase) return $"{nome}: chance fora de 0 a 100%.";
            if (r.Comment.Contains('\r') || r.Comment.Contains('\n')) return $"{nome}: o comentário não pode ter quebra de linha.";
        }
        foreach (var m in TaxasMudadas())
            if (m.ItemRate < 0 || m.MoneyRate < 0 || m.MaxItemLevel is < 0 or > 15)
                return $"{m.Name}: as taxas não podem ser negativas e o nível máximo do item vai de 0 a 15.";
        return null;
    }

    /// <summary>O que vai ser gravado, para a auditoria ("regras: 1 nova, 2 alteradas, 0 removidas; taxas de 3 monstros").</summary>
    public string Resumo()
    {
        int novas = Regras.Count(r => r.Raw == null), mudadas = Regras.Count(r => r.Raw != null && r.Key() != r.RawKey);
        int removidas = regrasLidas - (Regras.Count - novas);
        return $"regras: {novas} nova(s), {mudadas} alterada(s), {removidas} removida(s); taxas de {TaxasMudadas().Count} monstro(s)";
    }

    /// <summary>Grava o que mudou: regras no ItemDrop.txt e taxas no Monster.txt (backup de cada um). Não recarrega nos
    /// servidores: quem chama recarrega o Item (se Regras) e os monstros (se Taxas, fora da invasão).</summary>
    public (bool Regras, bool Taxas, List<string> Log) Salvar()
    {
        if (Problema() is { } p) throw new InvalidOperationException(p + " Nada foi gravado.");
        var log = new List<string>();
        bool regras = RegrasMudaram;
        var taxas = TaxasMudadas();
        if (regras)
        {
            foreach (var r in Regras) { r.Level = r.Level.Trim(); r.Map = r.Map.Trim(); r.Monster = r.Monster.Trim(); r.LevelMin = r.LevelMin.Trim(); r.LevelMax = r.LevelMax.Trim(); }
            var backup = Drops.Save(Regras);
            regrasLidas = Regras.Count;
            log.Add($"Drops salvos ({Regras.Count} regras; backup {Path.GetFileName(backup)}).");
        }
        if (taxas.Count > 0)
        {
            var (n, backup) = Drops.SaveMonsterRates(taxas);
            foreach (var m in taxas) taxasLidas[m.Index] = m;
            log.Add($"Taxas de {n} monstro(s) salvas (backup {Path.GetFileName(backup)}).");
        }
        if (log.Count == 0) log.Add("Nada mudou.");
        return (regras, taxas.Count > 0, log);
    }
}

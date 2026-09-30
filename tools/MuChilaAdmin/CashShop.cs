using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin;

/// <summary>
/// Loja de Cash do jogo (a que abre com a tecla X). São dois lados que precisam bater:
///  - Servidor: Data\CashShop\CashShopPackage.txt — é quem COBRA (coluna CoinValue = preço em W Coin/Goblin).
///  - Cliente:  Data\InGameShopScript\512.2011.006\IBSPackage.txt — é só o que o jogador VÊ (nome e preço na tela).
/// O elo entre os dois é (Categoria, MainIndex): no servidor o MainIndex é a 3ª coluna; no cliente é a 3ª coluna
/// do pacote. Conferido em 27/09/2026: 130 pacotes casam com preço, item e moeda iguais; 16 pacotes existem só no
/// servidor (o cliente não mostra) — esses aparecem como "(sem tela no cliente)" e não dá para mexer no preço deles.
///
/// O editor mexe no PREÇO e em MOSTRAR/ESCONDER um pacote. Esconder tira a linha dos dois arquivos, guardando o
/// original em muchila-cash-oculto.txt para poder voltar. Não mexe no conteúdo dos pacotes do kit.
///
/// 29/09/2026: ADICIONAR pacote novo (um item, um preço) e REMOVER de vez os que o painel criou. Um pacote novo são 4
/// peças que precisam casar: pacote e produto no servidor (CashShopPackage/CashShopProduct) e no cliente
/// (IBSPackage/IBSProduct). O pacote aponta para o produto por (ProductBaseIndex, ProductMainIndex); o produto diz o item,
/// nível, opções, quantidade e prazo. Cada peça nova é copiada de uma linha do kit do mesmo tipo (mesma moeda; item por
/// quantidade ou por prazo) trocando só os campos conhecidos, e recebe números novos (maior existente + 1). O que foi
/// criado fica em muchila-cash-adicionados.txt, para o "remover" apagar exatamente essas linhas.
/// </summary>
public static class CashShop
{
    static string ServerDir => Path.Combine(ServerControl.ServerRoot, @"Data\CashShop");
    static string ServerFile => Path.Combine(ServerDir, "CashShopPackage.txt");
    static string HiddenFile => Path.Combine(ServerDir, "muchila-cash-oculto.txt");
    public static string ClientDir => Environment.GetEnvironmentVariable("MUCHILA_CLIENTE")
        ?? ReadConfig() ?? @"C:\Projetos\MuServer-Season14\2 - Cliente Season 14 Full";
    static string ClientFile => Path.Combine(ClientDir, @"Data\InGameShopScript\512.2011.006\IBSPackage.txt");
    // Latin1 devolve cada byte 0-255 como um caractere, ida e volta sem perda (os arquivos têm bytes fora do ASCII).
    static readonly Encoding Cp1252 = Encoding.Latin1;

    static string? ReadConfig()
    {
        var f = Path.Combine(AppContext.BaseDirectory, "cashshop-cliente.txt");
        return File.Exists(f) ? File.ReadAllText(f).Trim() : null;
    }
    public static bool ClientAvailable => File.Exists(ClientFile);

    /// <summary>Um pacote à venda: identidade (Categoria, MainIndex), nome e moeda vêm do cliente; o preço que vale é o do servidor.</summary>
    public sealed class Package
    {
        public int Category, Main, ItemIndex, CoinIndex;
        public int Price;           // CoinValue do servidor (o que vale)
        public int ClientPrice;     // preço mostrado no cliente (deveria ser igual)
        public string Name = "";
        public bool HasClient;      // false = pacote só do servidor (não dá para editar)
        public bool Hidden;
        public bool Added;          // criado pelo painel (dá para apagar de vez)
        /// <summary>
        /// Opções de compra (1 dia, 7 dias, 10 un....). O jogo MOSTRA e COBRA o preço de cada opção, que fica no PRODUTO
        /// (CashShopProduct no servidor, IBSProduct no cliente); o preço do pacote é só o da vitrine (= 1ª opção no kit).
        /// Vazio = pacote com vários itens e um preço só (ex.: "Level up Package"): aí vale o preço do pacote.
        /// </summary>
        public List<Option> Options = new();
        internal int OrigPrice;     // preço do pacote quando foi lido (para saber se a grade mudou)
        public string CoinLabel => CoinIndex switch { 508 => "W Coin (C)", 509 => "W Coin (P)", 0 => "Goblin Point", _ => $"moeda {CoinIndex}" };
        // linhas cruas para regravar sem perder nada (o texto original preserva o alinhamento do kit)
        internal string[]? ServerRow;
        internal string[]? ClientRow;
        internal string? ServerRaw;
        internal string? ClientRaw;
    }

    /// <summary>Uma opção de compra = um produto (ProductBaseIndex, ProductMainIndex) com preço, prazo e quantidade.</summary>
    public sealed class Option
    {
        public int Base, Main, Price, Seconds, Quantity;
        internal int OrigPrice;
        /// <summary>O mesmo produto é usado por outro pacote (quase sempre o mesmo item na aba W Coin (P)).</summary>
        public bool Shared;
        public string Label => Seconds > 0
            ? Seconds % 86400 == 0 ? $"{Seconds / 86400} dia{(Seconds / 86400 > 1 ? "s" : "")}" : Seconds % 3600 == 0 ? $"{Seconds / 3600} h" : $"{Seconds / 60} min"
            : $"{Math.Max(1, ShownQuantity ?? Quantity)} un";
        internal int? ShownQuantity;   // quantidade que o cliente mostra ("Quantity 1 EA"); a do servidor às vezes é a durabilidade (frutas: 20)
    }

    // ---------- leitura ----------
    static List<string> ReadLines(string file) => File.ReadAllLines(file, Cp1252).ToList();

    /// <summary>Preenche as opções de cada pacote a partir do CashShopProduct (e marca as compartilhadas entre pacotes).</summary>
    static void LoadOptions(IEnumerable<Package> packages)
    {
        var prod = new Dictionary<int, string[]>();   // ProductMainIndex -> campos do produto
        foreach (var l in ReadLines(ProductFile)) if (ServerProductKey(l) is { } k) prod[k.Item2] = Cols(l);
        var shown = new Dictionary<int, int>();   // ProductMainIndex -> quantidade que o cliente mostra
        if (ClientAvailable && File.Exists(ClientProductFile))
            foreach (var l in File.ReadLines(ClientProductFile, Cp1252))
                if (ClientProductKey(l) is { } ck && l.Split('@') is var f && f[2] == "Quantity" && int.TryParse(f[3], out var q)) shown.TryAdd(ck.Item2, q);
        var all = packages.ToList();
        var uses = all.SelectMany(p => MainsOf(p.ServerRow)).GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count());
        foreach (var p in all)
        {
            p.Options = new(); p.OrigPrice = p.Price;
            foreach (var m in MainsOf(p.ServerRow))
                if (prod.TryGetValue(m, out var c))
                    p.Options.Add(new Option { Base = int.Parse(c[0]), Main = m, Price = int.Parse(c[2]), OrigPrice = int.Parse(c[2]),
                                               Quantity = int.Parse(c[17]), Seconds = int.Parse(c[18]), Shared = uses[m] > 1,
                                               ShownQuantity = shown.TryGetValue(m, out var sq) ? sq : null });
        }
    }

    // ProductMainIndex1..10 do pacote do servidor (campos 17..26), sem os zeros
    static IEnumerable<int> MainsOf(string[]? serverRow) =>
        serverRow == null ? Enumerable.Empty<int>() : serverRow.Skip(17).Take(10).Where(IsNum).Select(int.Parse).Where(m => m != 0);

    // devolve, em ordem, os pares (linha crua, campos) de cada pacote e as linhas de cabeçalho/rodapé preservadas
    static (List<string> head, List<(string Raw, string[] Cols)> rows, string tail) ParseServer(IEnumerable<string> lines)
    {
        var head = new List<string>(); var rows = new List<(string, string[])>(); bool started = false; var tail = "end";
        foreach (var l in lines)
        {
            var t = l.Trim();
            if (t.Equals("end", StringComparison.OrdinalIgnoreCase)) { tail = l; break; }
            var c = t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            // linha de dados = 27 campos e o 1º é número (o cabeçalho "//Category ..." também tem 27 palavras)
            if (c.Length == 27 && c[0].All(char.IsDigit)) { started = true; rows.Add((l, c)); }
            else if (!started) head.Add(l);
        }
        return (head, rows, tail);
    }

    static (List<(string Raw, string[] Cols)> rows, string newline) ParseClient(string text)
    {
        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        var rows = new List<(string, string[])>();
        foreach (var l in text.Replace("\r\n", "\n").Split('\n'))
            if (l.Trim().Length > 0) rows.Add((l, l.Split('@')));
        return (rows, nl);
    }

    public static List<Package> List()
    {
        var (_, srv, _) = ParseServer(ReadLines(ServerFile));
        var byKey = new Dictionary<(int, int), Package>();
        foreach (var (raw, r) in srv)
        {
            var p = new Package { Category = int.Parse(r[0]), Main = int.Parse(r[2]), ItemIndex = int.Parse(r[3]),
                CoinIndex = int.Parse(r[4]), Price = int.Parse(r[5]), ServerRow = r, ServerRaw = raw };
            byKey[(p.Category, p.Main)] = p;
        }
        if (ClientAvailable)
            foreach (var (raw, c) in ParseClient(File.ReadAllText(ClientFile, Cp1252)).rows)
            {
                if (c.Length < 26) continue;
                var key = (int.Parse(c[0]), int.Parse(c[2]));
                if (!byKey.TryGetValue(key, out var p)) continue;
                p.HasClient = true; p.Name = c[3]; p.ClientPrice = int.TryParse(c[5], out var cp) ? cp : p.Price; p.ClientRow = c; p.ClientRaw = raw;
            }
        foreach (var h in ReadHidden()) byKey[h.Key] = h.Value;   // esconde-os por cima
        foreach (var a in AddedList()) if (byKey.TryGetValue((a.Category, a.Main), out var p)) p.Added = true;
        LoadOptions(byKey.Values);
        return byKey.Values.OrderByDescending(p => p.HasClient).ThenBy(p => p.Category).ThenBy(p => p.Main).ToList();
    }

    // ---------- ocultos (guardados para poder voltar) ----------
    static Dictionary<(int, int), Package> ReadHidden()
    {
        var dict = new Dictionary<(int, int), Package>();
        if (!File.Exists(HiddenFile)) return dict;
        foreach (var line in File.ReadAllLines(HiddenFile, Cp1252))
        {
            if (line.Trim().Length == 0) continue;
            var parts = line.Split('\u0001');   // linha crua do servidor \u0001 linha crua do cliente (ou vazio)
            var sraw = parts[0]; var sr = sraw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (sr.Length != 27) continue;
            var p = new Package { Category = int.Parse(sr[0]), Main = int.Parse(sr[2]), ItemIndex = int.Parse(sr[3]),
                CoinIndex = int.Parse(sr[4]), Price = int.Parse(sr[5]), ServerRow = sr, ServerRaw = sraw, Hidden = true };
            if (parts.Length > 1 && parts[1].Length > 0) { var craw = parts[1]; var cr = craw.Split('@'); p.ClientRow = cr; p.ClientRaw = craw; p.HasClient = true; p.Name = cr[3]; p.ClientPrice = int.TryParse(cr[5], out var cp) ? cp : p.Price; }
            dict[(p.Category, p.Main)] = p;
        }
        return dict;
    }

    // ---------- gravação ----------
    /// <summary>Aplica preços e o estado mostrar/esconder. Faz backup dos dois arquivos antes. Não recarrega (quem chama recarrega).</summary>
    public static string Save(IReadOnlyList<Package> packages)
    {
        // pacote de uma opção: o preço da grade É o da opção; com opções, a vitrine (preço do pacote) = 1ª opção (padrão do kit)
        foreach (var p in packages)
        {
            if (p.Options.Count == 1 && p.Options[0].Price == p.Options[0].OrigPrice && p.Price != p.OrigPrice) p.Options[0].Price = p.Price;
            if (p.Options.Count > 0) p.Price = p.Options[0].Price;
        }
        foreach (var p in packages)
            foreach (var v in p.Options.Select(o => o.Price).Append(p.Price))
                if (v is < 0 or > 9_000_000) throw new InvalidOperationException($"Preço fora do intervalo (0 a 9.000.000) em {(p.Name.Length > 0 ? p.Name : $"pacote {p.Category},{p.Main}")}.");

        var wanted = packages.ToDictionary(p => (p.Category, p.Main));
        var hidden = packages.Where(p => p.Hidden).ToList();

        // ----- opções com preço novo: é o PRODUTO que o jogo mostra e cobra. Produto usado também por outro pacote (a aba
        // W Coin (P) usa os mesmos da (C)) ganha uma cópia só deste pacote, para o preço não mudar no outro. -----
        var changed = packages.Where(p => !p.Hidden).SelectMany(p => p.Options.Where(o => o.Price != o.OrigPrice).Select(o => (P: p, O: o, Old: o.OrigPrice))).ToList();
        var remap = new Dictionary<(int, int), Dictionary<int, int>>();   // pacote -> main antigo -> main novo
        var inPlace = new Dictionary<int, int>();                         // main -> preço novo
        var newSrvProd = new List<string>(); var newCliProd = new List<string>();
        if (changed.Count > 0)
        {
            var srvProd = ReadLines(ProductFile);
            var cliProd = ClientAvailable ? File.ReadAllLines(ClientProductFile, Cp1252).ToList() : new List<string>();
            int nextMain = srvProd.Select(ServerProductKey).Where(k => k != null).Select(k => k!.Value.Item2)
                .Concat(cliProd.Select(ClientProductKey).Where(k => k != null).Select(k => k!.Value.Item2))
                .Concat(packages.SelectMany(p => MainsOf(p.ServerRow))).DefaultIfEmpty(0).Max() + 1;
            foreach (var (p, o, _) in changed)
            {
                if (!o.Shared) { inPlace[o.Main] = o.Price; continue; }
                int nm = nextMain++;
                var raw = srvProd.First(l => ServerProductKey(l) == (o.Base, o.Main));
                newSrvProd.Add(SetFields(raw, ' ', new Dictionary<int, object> { [1] = nm, [2] = o.Price }));
                foreach (var l in cliProd.Where(l => ClientProductKey(l) == (o.Base, o.Main)))
                    newCliProd.Add(SetFields(l, '@', new Dictionary<int, object> { [5] = o.Price, [6] = nm }));
                if (!remap.TryGetValue((p.Category, p.Main), out var r)) remap[(p.Category, p.Main)] = r = new();
                r[o.Main] = nm;
                o.Main = nm; o.Shared = false;
            }
            Rewrite(ProductFile, l => ServerProductKey(l) is { } k && inPlace.TryGetValue(k.Item2, out var v) && Cols(l)[2] != v.ToString() ? SetField(l, ' ', 2, v) : l, newSrvProd, beforeEnd: true);
            if (ClientAvailable)
                Rewrite(ClientProductFile, l => ClientProductKey(l) is { } k && inPlace.TryGetValue(k.Item2, out var v) && l.Split('@')[5] != v.ToString() ? SetField(l, '@', 5, v) : l, newCliProd, beforeEnd: false);
        }
        var descNotes = new List<string>();

        // ----- servidor: mantém a ordem e o texto original; só re-renderiza o que muda, remove o oculto, re-inclui o que voltou -----
        var (head, srvRows, tail) = ParseServer(ReadLines(ServerFile));
        var present = new HashSet<(int, int)>();
        var outLines = new List<string>();
        foreach (var (raw, cols) in srvRows)
        {
            var key = (int.Parse(cols[0]), int.Parse(cols[2]));
            present.Add(key);
            if (!wanted.TryGetValue(key, out var p) || p.Hidden) continue;   // some se foi escondido ou sumiu da lista
            var mods = new Dictionary<int, object>();
            if (p.Price != int.Parse(cols[5])) mods[5] = p.Price;
            if (remap.TryGetValue(key, out var rm))
                for (int i = 17; i <= 26; i++) if (int.TryParse(cols[i], out var m) && rm.TryGetValue(m, out var nm)) mods[i] = nm;
            outLines.Add(mods.Count == 0 ? raw : SetFields(raw, ' ', mods));
        }
        foreach (var p in packages.Where(p => !p.Hidden && !present.Contains((p.Category, p.Main))))   // voltou a aparecer
            outLines.Add(SetField(p.ServerRaw ?? throw new InvalidOperationException("linha do servidor perdida"), ' ', 5, p.Price));
        Backup(ServerFile);
        File.WriteAllText(ServerFile, string.Join("\r\n", head.Concat(outLines).Append(tail)) + "\r\n", Cp1252);

        // ----- cliente (se disponível): mesma ideia -----
        int cli = 0;
        if (ClientAvailable)
        {
            var (rows, nl) = ParseClient(File.ReadAllText(ClientFile, Cp1252));
            var cPresent = new HashSet<(int, int)>();
            var cOut = new List<string>();
            foreach (var (raw, cols) in rows)
            {
                if (cols.Length < 3) { cOut.Add(raw); continue; }
                var key = (int.Parse(cols[0]), int.Parse(cols[2]));
                cPresent.Add(key);
                if (!wanted.TryGetValue(key, out var p) || p.Hidden) continue;
                var mods = new Dictionary<int, object>();
                if (p.Price != int.Parse(cols[5])) mods[5] = p.Price;
                if (cols.Length > 23 && remap.TryGetValue(key, out var rm))
                    mods[23] = string.Concat(cols[23].Split('|', StringSplitOptions.RemoveEmptyEntries).Select(t => (int.TryParse(t, out var m) && rm.TryGetValue(m, out var nm) ? nm.ToString() : t) + "|"));
                var mine = changed.Where(c => c.P == p).Select(c => (c.O, c.Old)).ToList();
                if (mine.Count > 0)
                {
                    var d = UpdateDescription(cols[6], mine, out var falta);
                    if (d != cols[6]) mods[6] = d;
                    if (falta.Count > 0) descNotes.Add($"{p.Name}: {string.Join(", ", falta)}");
                }
                cOut.Add(mods.Count == 0 ? raw : SetFields(raw, '@', mods)); cli++;
            }
            foreach (var p in packages.Where(p => !p.Hidden && p.HasClient && p.ClientRaw != null && !cPresent.Contains((p.Category, p.Main))))
            { cOut.Add(SetField(p.ClientRaw!, '@', 5, p.Price)); cli++; }
            Backup(ClientFile);
            File.WriteAllText(ClientFile, string.Join(nl, cOut) + nl, Cp1252);
        }

        // ----- lista de ocultos (guarda a linha crua para poder voltar exatamente igual) -----
        var lines = hidden.Select(p => (p.ServerRaw ?? "") + "\u0001" + (p.ClientRaw ?? ""));
        File.WriteAllText(HiddenFile, string.Join("\r\n", lines) + (hidden.Count > 0 ? "\r\n" : ""), Cp1252);

        foreach (var (_, o, _) in changed) o.OrigPrice = o.Price;
        foreach (var p in packages) p.OrigPrice = p.Price;

        var resumo = changed.Count == 0 ? "" : $" {changed.Count} preço(s) de opção mudado(s) (produto: o que o jogo cobra)" +
            (newSrvProd.Count > 0 ? $"; {remap.Values.Sum(r => r.Count)} opção(ões) ganharam produto próprio (era compartilhado com outra aba)" : "") + ".";
        if (descNotes.Count > 0) resumo += " Descrição sem o preço escrito (não mudou): " + string.Join("; ", descNotes) + ".";
        return ClientAvailable
            ? $"Cash Shop salvo: servidor + cliente ({cli} pacotes na tela).{resumo} Backup .bak-* ao lado dos arquivos."
            : $"Cash Shop salvo só no SERVIDOR (o cliente não foi achado em {ClientDir}). O jogo vai cobrar o preço novo, mas a tela mostra o antigo até você atualizar o cliente.{resumo}";
    }

    /// <summary>
    /// Troca o preço escrito na descrição do pacote ("100 W Coin - 1Day", "1EA - 100 W Coin", "3 dias - 200 W Coin",
    /// "6 Hour - 200 Goblin Point"...). Só mexe no número colado a "W Coin"/"Goblin Point" (o "200 Point" das frutas de reset
    /// são pontos, não preço). Com mais de uma linha com o mesmo preço antigo, escolhe a do prazo/quantidade da opção.
    /// "falta" = opções cujo preço não estava escrito (a descrição fica como estava).
    /// </summary>
    static string UpdateDescription(string desc, IReadOnlyList<(Option O, int Old)> changes, out List<string> falta)
    {
        falta = new();
        var lines = desc.Split('#');
        var price = new Regex(@"(\d+)(?=\s*(?:W\s?Coin|WCoin|Goblin\s+Point))", RegexOptions.IgnoreCase);
        var done = new HashSet<int>();
        foreach (var (o, old) in changes)
        {
            var cand = Enumerable.Range(0, lines.Length).Where(i => !done.Contains(i) && price.Matches(lines[i]).Any(m => m.Value == old.ToString())).ToList();
            if (cand.Count > 1) cand = cand.Where(i => AmountMatches(lines[i], o)).ToList() is { Count: > 0 } c ? c : cand;
            if (cand.Count == 0) { falta.Add(o.Label); continue; }
            int at = cand[0];
            var m1 = price.Matches(lines[at]).First(m => m.Value == old.ToString());
            lines[at] = lines[at][..m1.Index] + o.Price + lines[at][(m1.Index + m1.Length)..];
            done.Add(at);
        }
        return string.Join("#", lines);
    }

    // a linha fala do prazo/quantidade desta opção? (1Day, 7Days, 3 dias, 6 Hour, 10EA...)
    static bool AmountMatches(string line, Option o)
    {
        foreach (Match m in Regex.Matches(line, @"(\d+)\s*(Days?|dias?|Hours?|horas?|EA|un)\b", RegexOptions.IgnoreCase))
        {
            int n = int.Parse(m.Groups[1].Value); var u = m.Groups[2].Value.ToLowerInvariant();
            if ((u.StartsWith("day") || u.StartsWith("dia")) && o.Seconds == n * 86400) return true;
            if ((u.StartsWith("hour") || u.StartsWith("hora")) && o.Seconds == n * 3600) return true;
            if ((u == "ea" || u == "un") && o.Seconds == 0 && Math.Max(1, o.ShownQuantity ?? o.Quantity) == n) return true;
        }
        return false;
    }

    // milissegundos no nome: dois salvamentos no mesmo segundo (script + painel) não colidem
    static void Backup(string file) => File.Copy(file, $"{file}.bak-{DateTime.Now:yyyyMMdd-HHmmss-fff}", overwrite: false);

    // troca um campo de uma linha crua, mantendo o alinhamento (mesma quantidade de espaços entre campos).
    static string SetField(string raw, char sep, int field, object value) => SetFields(raw, sep, new Dictionary<int, object> { [field] = value });

    static string SetFields(string raw, char sep, IReadOnlyDictionary<int, object> values)
    {
        if (sep == '@') { var c = raw.Split('@'); foreach (var (k, v) in values) c[k] = v.ToString()!; return string.Join("@", c); }
        // separado por espaços/tabs: percorre mantendo os separadores originais
        var parts = System.Text.RegularExpressions.Regex.Split(raw, "(\\s+)");
        int col = -1;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || char.IsWhiteSpace(parts[i][0])) continue;
            if (values.TryGetValue(++col, out var v)) parts[i] = v.ToString()!;
        }
        return string.Concat(parts);
    }

    // ================= adicionar / remover pacote novo =================
    static string ProductFile => Path.Combine(ServerDir, "CashShopProduct.txt");
    static string AddedFile => Path.Combine(ServerDir, "muchila-cash-adicionados.txt");
    static string ClientScriptDir => Path.Combine(ClientDir, @"Data\InGameShopScript\512.2011.006");
    static string ClientProductFile => Path.Combine(ClientScriptDir, "IBSProduct.txt");
    static string ClientCategoryFile => Path.Combine(ClientScriptDir, "IBSCategory.txt");

    static string[] Cols(string line) => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    static bool IsNum(string s) => s.Length > 0 && s.All(char.IsDigit);
    // chaves de cada tipo de linha (null = não é linha de dados desse arquivo)
    static (int, int)? ServerPackageKey(string l) { var c = Cols(l); return c.Length == 27 && IsNum(c[0]) ? (int.Parse(c[0]), int.Parse(c[2])) : null; }
    static (int, int)? ServerProductKey(string l) { var c = Cols(l); return c.Length == 19 && IsNum(c[0]) ? (int.Parse(c[0]), int.Parse(c[1])) : null; }
    static (int, int)? ClientPackageKey(string l) { var c = l.Split('@'); return c.Length >= 27 && IsNum(c[0]) && IsNum(c[2]) ? (int.Parse(c[0]), int.Parse(c[2])) : null; }
    static (int, int)? ClientProductKey(string l) { var c = l.Split('@'); return c.Length >= 17 && IsNum(c[0]) && IsNum(c[6]) ? (int.Parse(c[0]), int.Parse(c[6])) : null; }

    /// <summary>
    /// Regrava um arquivo linha a linha: map devolve a linha (igual ou trocada) ou null para tirar; add entra antes do "end"
    /// (arquivos do servidor) ou no fim (cliente). Mantém a quebra de linha e se o arquivo termina com ela. Só grava (com
    /// backup) se algo mudou.
    /// </summary>
    static bool Rewrite(string file, Func<string, string?> map, IReadOnlyList<string> add, bool beforeEnd)
    {
        var text = File.ReadAllText(file, Cp1252);
        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        bool trailing = text.EndsWith('\n');
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (trailing) lines.RemoveAt(lines.Count - 1);
        var outLines = new List<string>(); bool changed = add.Count > 0;
        foreach (var l in lines)
        {
            var m = map(l);
            if (m != l) changed = true;
            if (m != null) outLines.Add(m);
        }
        if (!changed) return false;
        int at = outLines.Count;
        if (beforeEnd) { int e = outLines.FindLastIndex(l => l.Trim().Equals("end", StringComparison.OrdinalIgnoreCase)); if (e >= 0) at = e; }
        outLines.InsertRange(at, add);
        Backup(file);
        File.WriteAllText(file, string.Join(nl, outLines) + (trailing ? nl : ""), Cp1252);
        return true;
    }

    /// <summary>Aba da loja (W Coin (C) › Special...). A moeda vem da aba-raiz (10, 11, 20 no kit), conferida nos pacotes que já existem nela.</summary>
    public sealed record Category(int Id, string Name, string CoinName, int CoinIndex)
    {
        public override string ToString() => $"{CoinName} › {Name}";
    }

    public static List<Category> Categories()
    {
        if (!File.Exists(ClientCategoryFile)) return new();
        // IBSCategory.txt: id@nome@200@201@pai@ordem@raiz (raiz 1 = aba da moeda)
        var rows = File.ReadAllLines(ClientCategoryFile, Cp1252).Select(l => l.Split('@')).Where(c => c.Length >= 7 && IsNum(c[0])).ToList();
        var roots = rows.Where(c => c[6].Trim() == "1").ToDictionary(c => c[0], c => c[1]);
        var parent = rows.ToDictionary(c => c[0], c => c[4]);
        // moeda de cada raiz = a dos pacotes do cliente que estão debaixo dela (508 W Coin (C), 509 W Coin (P), 0 Goblin)
        var coin = new Dictionary<string, int>();
        foreach (var (_, c) in ParseClient(File.ReadAllText(ClientFile, Cp1252)).rows)
            if (c.Length >= 27 && parent.TryGetValue(c[0], out var root) && roots.ContainsKey(root) && int.TryParse(c[25], out var ci)) coin.TryAdd(root, ci);
        return rows.Where(c => c[6].Trim() != "1" && roots.ContainsKey(c[4]) && coin.ContainsKey(c[4]))
                   .Select(c => new Category(int.Parse(c[0]), c[1].Trim(), roots[c[4]].Trim(), coin[c[4]])).ToList();
    }

    /// <summary>Quanto o item empilha (Data\Item\ItemStack.txt); 1 = não empilha.</summary>
    public static int MaxStack(int section, int type)
    {
        var f = Path.Combine(ServerControl.ServerRoot, @"Data\Item\ItemStack.txt");
        if (!File.Exists(f)) return 1;
        int index = section * 512 + type;
        foreach (var l in File.ReadLines(f, Cp1252))
        {
            var c = Cols(l);
            if (c.Length >= 2 && c[0] == index.ToString() && int.TryParse(c[1], out var max)) return Math.Max(1, max);
        }
        return 1;
    }

    public sealed record Added(int Category, int Main, int ProductBase, int ProductMain, string Name);

    // para o teste (--testar-cashshop-adicionar)
    internal static IReadOnlyList<string> TestFiles => new[] { ServerFile, ProductFile, ClientFile, ClientProductFile, HiddenFile, AddedFile };
    internal static (List<string[]> SrvPkg, List<string[]> SrvProd, List<string[]> CliPkg, List<string[]> CliProd) TestLines() => (
        ReadLines(ServerFile).Where(l => ServerPackageKey(l) != null).Select(Cols).ToList(),
        ReadLines(ProductFile).Where(l => ServerProductKey(l) != null).Select(Cols).ToList(),
        File.ReadAllLines(ClientFile, Cp1252).Where(l => ClientPackageKey(l) != null).Select(l => l.Split('@')).ToList(),
        File.ReadAllLines(ClientProductFile, Cp1252).Where(l => ClientProductKey(l) != null).Select(l => l.Split('@')).ToList());

    public static List<Added> AddedList()
    {
        if (!File.Exists(AddedFile)) return new();
        var list = new List<Added>();
        foreach (var l in File.ReadAllLines(AddedFile, Cp1252))
        {
            var c = l.Split('\t');
            if (c.Length >= 5 && IsNum(c[0])) list.Add(new Added(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2]), int.Parse(c[3]), c[4]));
        }
        return list;
    }

    static void SaveAdded(IEnumerable<Added> list) =>
        File.WriteAllText(AddedFile, string.Concat(list.Select(a => $"{a.Category}\t{a.Main}\t{a.ProductBase}\t{a.ProductMain}\t{a.Name}\r\n")), Cp1252);

    /// <summary>O que o painel pede para criar. Days 0 = permanente; Quantity > 1 só para item que empilha.</summary>
    public sealed record NewPackage(int Category, int Section, int Type, int Level, bool Skill, bool Luck, int Option, int Excellent,
                                    int Quantity, int Days, int Price, string Name, string Description);

    // a fonte da loja do cliente não tem acento; '@' e '|' são separadores do arquivo e '#' é quebra de linha da descrição
    static string Clean(string s, int max)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark && ch is >= ' ' and <= '~' && ch is not ('@' or '|' or '#')) sb.Append(ch);
        var r = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " {2,}", " ").Trim();
        return r.Length > max ? r[..max].TrimEnd() : r;
    }

    /// <summary>
    /// Cria um pacote novo na loja (servidor + cliente) com um item e um preço. Devolve a mensagem para o log.
    /// Não recarrega (quem chama recarrega) e não publica o cliente.
    /// </summary>
    public static string Add(NewPackage n)
    {
        if (!ClientAvailable || !File.Exists(ClientProductFile)) throw new InvalidOperationException($"Cliente não encontrado em {ClientDir}: sem ele o item não aparece na loja.");
        var cat = Categories().FirstOrDefault(c => c.Id == n.Category) ?? throw new InvalidOperationException($"Aba {n.Category} não existe na loja do cliente.");
        var def = Shops.Find(n.Section, n.Type) ?? throw new InvalidOperationException($"Item {n.Section},{n.Type} não existe no Item.txt.");
        if (n.Price is < 1 or > 9_000_000) throw new InvalidOperationException("Preço fora do intervalo (1 a 9.000.000).");
        if (n.Level is < 0 or > 15 || n.Option is < 0 or > 7 || n.Excellent is < 0 or > 63) throw new InvalidOperationException("Nível 0–15, opção 0–7 e excelente 0–63.");
        if (n.Days is < 0 or > 3650) throw new InvalidOperationException("Prazo de 0 (permanente) a 3650 dias.");
        int stack = MaxStack(n.Section, n.Type);
        if (n.Quantity < 1 || n.Quantity > stack)
            throw new InvalidOperationException(stack <= 1 ? $"{def.Name} não empilha: a quantidade tem que ser 1." : $"{def.Name} empilha até {stack}: quantidade de 1 a {stack}.");
        if (n.Days > 0 && n.Quantity > 1) throw new InvalidOperationException("Item com prazo vai 1 por pacote (quantidade 1).");
        var name = Clean(n.Name.Length > 0 ? n.Name : def.Name, 50);
        if (name.Length == 0) throw new InvalidOperationException("Dê um nome ao pacote.");
        if (cat.CoinIndex == 509 && !name.StartsWith('[')) name = $"[{name}]";   // padrão do kit para os pacotes de W Coin (P)
        var desc = string.Join("#", n.Description.Replace("\r\n", "\n").Split('\n').Select(l => Clean(l, 90)).Where(l => l.Length > 0).Take(4));
        int item = n.Section * 512 + n.Type, seconds = n.Days * 86400;

        // ----- tudo o que existe hoje (inclusive os escondidos, que podem voltar) -----
        var srvPkg = ParseServer(ReadLines(ServerFile)).rows.Select(r => r.Cols).ToList();
        var hiddenSrv = ReadHidden().Values.Select(p => p.ServerRow!).ToList();
        var hiddenCli = ReadHidden().Values.Where(p => p.ClientRow != null).Select(p => p.ClientRow!).ToList();
        var srvProdRaw = ReadLines(ProductFile).Where(l => ServerProductKey(l) != null).ToList();
        var srvProd = srvProdRaw.Select(Cols).ToList();
        var cliPkg = ParseClient(File.ReadAllText(ClientFile, Cp1252)).rows.Select(r => r.Cols).Where(c => c.Length >= 27 && IsNum(c[0])).ToList();
        var cliProd = ParseClient(File.ReadAllText(ClientProductFile, Cp1252)).rows.Select(r => r.Cols).Where(c => c.Length >= 17 && IsNum(c[0])).ToList();
        var allSrvPkg = srvPkg.Concat(hiddenSrv).ToList();
        var allCliPkg = cliPkg.Concat(hiddenCli).ToList();
        static IEnumerable<int> Nums(IEnumerable<string> s) => s.Where(IsNum).Select(int.Parse);

        // ----- números novos: maior + 1 -----
        int main = Nums(allSrvPkg.Select(c => c[2]).Concat(allCliPkg.Select(c => c[2]))).DefaultIfEmpty(0).Max() + 1;
        int srvBase = Nums(allSrvPkg.Where(c => c[0] == cat.Id.ToString()).Select(c => c[1])).DefaultIfEmpty(0).Max() + 1;
        int cliSeq = Nums(allCliPkg.Where(c => c[0] == cat.Id.ToString()).Select(c => c[1])).DefaultIfEmpty(0).Max() + 1;
        int prodBase = Nums(srvProd.Select(c => c[0]).Concat(cliProd.Select(c => c[0])).Concat(allSrvPkg.SelectMany(c => c.Skip(7).Take(10)))
                            .Concat(allCliPkg.SelectMany(c => c[19].Split('|')))).DefaultIfEmpty(0).Max() + 1;
        int prodMain = Nums(srvProd.Select(c => c[1]).Concat(cliProd.Select(c => c[6])).Concat(allSrvPkg.SelectMany(c => c.Skip(17).Take(10)))
                            .Concat(allCliPkg.SelectMany(c => c[23].Split('|')))).DefaultIfEmpty(0).Max() + 1;

        // ----- modelos do kit -----
        // pacote: um produto e um preço, mesma moeda (de preferência da mesma aba)
        bool Simple(string[] c) => c[25] == cat.CoinIndex.ToString() && c[13] == "1" && c[22] == "1" && c[19].Split('|', StringSplitOptions.RemoveEmptyEntries).Length == 1;
        var srvByKey = srvPkg.GroupBy(c => (c[0], c[2])).ToDictionary(g => g.Key, g => g.First());
        var cliTpl = cliPkg.Where(c => Simple(c) && srvByKey.ContainsKey((c[0], c[2]))).OrderByDescending(c => c[0] == cat.Id.ToString()).FirstOrDefault()
                     ?? throw new InvalidOperationException($"Não achei um pacote do kit em {cat.CoinName} para servir de modelo.");
        var srvTplRaw = ParseServer(ReadLines(ServerFile)).rows.First(r => r.Cols[0] == cliTpl[0] && r.Cols[2] == cliTpl[2]).Raw;
        // produto: as linhas de um produto do kit do mesmo jeito (por prazo: "Duration" tipo 10; por quantidade: "Quantity" tipo 7)
        var (kind, code) = seconds > 0 ? ("Duration", "10") : ("Quantity", "7");
        var tplKey = cliProd.Where(c => c[2] == kind && c[14] == code).Select(c => (c[0], c[6]))
                            .FirstOrDefault(k => srvProd.Any(s => s[0] == k.Item1 && s[1] == k.Item2));
        if (tplKey == default) throw new InvalidOperationException($"Não achei um produto do kit por {(seconds > 0 ? "prazo" : "quantidade")} para servir de modelo.");
        var tplRows = cliProd.Where(c => (c[0], c[6]) == tplKey).OrderByDescending(c => c[2] == kind).ToList();   // a linha "Quantity"/"Duration" primeiro
        var srvProdTplRaw = srvProdRaw.First(l => { var c = Cols(l); return c[0] == tplKey.Item1 && c[1] == tplKey.Item2; });

        // ----- as 4 peças -----
        int srvQty = seconds > 0 ? 0 : stack > 1 ? n.Quantity : 0;   // item que não empilha: 0 = durabilidade normal do item
        var newSrvProd = SetFields(srvProdTplRaw, ' ', new Dictionary<int, object>
        {
            [0] = prodBase, [1] = prodMain, [2] = n.Price, [3] = item, [4] = n.Level, [5] = n.Skill ? 1 : 0, [6] = n.Luck ? 1 : 0, [7] = n.Option,
            [8] = n.Excellent, [9] = 0, [10] = 0, [11] = 0, [12] = 255, [13] = 255, [14] = 255, [15] = 255, [16] = 255, [17] = srvQty, [18] = seconds,
        });
        var pk = new Dictionary<int, object> { [0] = cat.Id, [1] = srvBase, [2] = main, [3] = item, [4] = cat.CoinIndex, [5] = n.Price, [6] = 0, [7] = prodBase, [17] = prodMain };
        for (int i = 8; i <= 16; i++) pk[i] = 0;
        for (int i = 18; i <= 26; i++) pk[i] = 0;
        var newSrvPkg = SetFields(srvTplRaw, ' ', pk);
        var newCliProd = tplRows.Select((r, i) =>
        {
            var c = (string[])r.Clone();
            c[0] = prodBase.ToString(); c[1] = name; c[5] = n.Price.ToString(); c[6] = prodMain.ToString(); c[13] = item.ToString();
            if (i == 0) c[3] = (seconds > 0 ? seconds : n.Quantity).ToString();
            return string.Join("@", c);
        }).ToList();
        var cp = (string[])cliTpl.Clone();
        cp[0] = cat.Id.ToString(); cp[1] = cliSeq.ToString(); cp[2] = main.ToString(); cp[3] = name; cp[5] = n.Price.ToString(); cp[6] = desc;
        cp[19] = $"{prodBase}|"; cp[20] = item.ToString(); cp[22] = "1"; cp[23] = $"{prodMain}|";
        var newCliPkg = string.Join("@", cp);

        // ----- grava (com backup) -----
        Rewrite(ProductFile, l => l, new[] { newSrvProd }, beforeEnd: true);
        Rewrite(ServerFile, l => l, new[] { newSrvPkg }, beforeEnd: true);
        Rewrite(ClientProductFile, l => l, newCliProd, beforeEnd: false);
        Rewrite(ClientFile, l => l, new[] { newCliPkg }, beforeEnd: false);
        SaveAdded(AddedList().Append(new Added(cat.Id, main, prodBase, prodMain, name)));

        var what = seconds > 0 ? $"{n.Days} dia(s)" : $"{n.Quantity} un.";
        return $"Loja de Cash: \"{name}\" ({def.Name} +{n.Level}, {what}) adicionado em {cat} por {n.Price:N0} (pacote {cat.Id},{main}; produto {prodBase},{prodMain}).";
    }

    /// <summary>Apaga de vez um pacote que o painel criou (as 4 peças, e da lista de escondidos se estiver lá).</summary>
    public static string RemoveAdded(int category, int main)
    {
        var all = AddedList();
        var a = all.FirstOrDefault(x => x.Category == category && x.Main == main)
                ?? throw new InvalidOperationException("Esse pacote é do kit: não dá para apagar, só esconder (desmarque \"Na loja\").");
        var pkg = (category, main); var prod = (a.ProductBase, a.ProductMain);
        Rewrite(ServerFile, l => ServerPackageKey(l) == pkg ? null : l, Array.Empty<string>(), beforeEnd: true);
        Rewrite(ProductFile, l => ServerProductKey(l) == prod ? null : l, Array.Empty<string>(), beforeEnd: true);
        if (ClientAvailable)
        {
            Rewrite(ClientFile, l => ClientPackageKey(l) == pkg ? null : l, Array.Empty<string>(), beforeEnd: false);
            Rewrite(ClientProductFile, l => ClientProductKey(l) == prod ? null : l, Array.Empty<string>(), beforeEnd: false);
        }
        if (File.Exists(HiddenFile)) Rewrite(HiddenFile, l => ServerPackageKey(l.Split('\u0001')[0]) == pkg ? null : l, Array.Empty<string>(), beforeEnd: false);
        SaveAdded(all.Where(x => x != a));
        return $"Loja de Cash: \"{a.Name}\" apagado de vez (pacote {category},{main}).";
    }
}

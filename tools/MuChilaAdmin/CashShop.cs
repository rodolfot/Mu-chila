using System.IO;
using System.Text;

namespace MuChilaAdmin;

/// <summary>
/// Loja de Cash do jogo (a que abre com a tecla X). São dois lados que precisam bater:
///  - Servidor: Data\CashShop\CashShopPackage.txt — é quem COBRA (coluna CoinValue = preço em W Coin/Goblin).
///  - Cliente:  Data\InGameShopScript\512.2011.006\IBSPackage.txt — é só o que o jogador VÊ (nome e preço na tela).
/// O elo entre os dois é (Categoria, MainIndex): no servidor o MainIndex é a 3ª coluna; no cliente é a 3ª coluna
/// do pacote. Conferido em 27/09/2026: 130 pacotes casam com preço, item e moeda iguais; 16 pacotes existem só no
/// servidor (o cliente não mostra) — esses aparecem como "(sem tela no cliente)" e não dá para mexer no preço deles.
///
/// O editor só mexe no PREÇO e em MOSTRAR/ESCONDER um pacote. Esconder tira a linha dos dois arquivos, guardando o
/// original em muchila-cash-oculto.json para poder voltar. Não mexe no conteúdo dos pacotes (isso é mais arriscado).
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
        public string CoinLabel => CoinIndex switch { 508 => "W Coin (C)", 509 => "W Coin (P)", 0 => "Goblin Point", _ => $"moeda {CoinIndex}" };
        // linhas cruas para regravar sem perder nada (o texto original preserva o alinhamento do kit)
        internal string[]? ServerRow;
        internal string[]? ClientRow;
        internal string? ServerRaw;
        internal string? ClientRaw;
    }

    // ---------- leitura ----------
    static List<string> ReadLines(string file) => File.ReadAllLines(file, Cp1252).ToList();

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
        foreach (var p in packages)
            if (p.Price is < 0 or > 9_000_000) throw new InvalidOperationException($"Preço fora do intervalo (0 a 9.000.000) em {(p.Name.Length > 0 ? p.Name : $"pacote {p.Category},{p.Main}")}.");

        var wanted = packages.ToDictionary(p => (p.Category, p.Main));
        var hidden = packages.Where(p => p.Hidden).ToList();

        // troca o campo de preço de uma linha crua, mantendo o alinhamento (mesma quantidade de espaços entre campos).
        static string SetField(string raw, char sep, int field, int value)
        {
            if (sep == '@') { var c = raw.Split('@'); c[field] = value.ToString(); return string.Join("@", c); }
            // separado por espaços/tabs: percorre mantendo os separadores originais
            var parts = System.Text.RegularExpressions.Regex.Split(raw, "(\\s+)");
            int col = -1;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || char.IsWhiteSpace(parts[i][0])) continue;
                if (++col == field) { parts[i] = value.ToString(); break; }
            }
            return string.Concat(parts);
        }

        // ----- servidor: mantém a ordem e o texto original; só re-renderiza o que muda, remove o oculto, re-inclui o que voltou -----
        var (head, srvRows, tail) = ParseServer(ReadLines(ServerFile));
        var present = new HashSet<(int, int)>();
        var outLines = new List<string>();
        foreach (var (raw, cols) in srvRows)
        {
            var key = (int.Parse(cols[0]), int.Parse(cols[2]));
            present.Add(key);
            if (!wanted.TryGetValue(key, out var p) || p.Hidden) continue;   // some se foi escondido ou sumiu da lista
            outLines.Add(p.Price == int.Parse(cols[5]) ? raw : SetField(raw, ' ', 5, p.Price));
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
                cOut.Add(p.Price == int.Parse(cols[5]) ? raw : SetField(raw, '@', 5, p.Price)); cli++;
            }
            foreach (var p in packages.Where(p => !p.Hidden && p.HasClient && p.ClientRaw != null && !cPresent.Contains((p.Category, p.Main))))
            { cOut.Add(SetField(p.ClientRaw!, '@', 5, p.Price)); cli++; }
            Backup(ClientFile);
            File.WriteAllText(ClientFile, string.Join(nl, cOut) + nl, Cp1252);
        }

        // ----- lista de ocultos (guarda a linha crua para poder voltar exatamente igual) -----
        var lines = hidden.Select(p => (p.ServerRaw ?? "") + "\u0001" + (p.ClientRaw ?? ""));
        File.WriteAllText(HiddenFile, string.Join("\r\n", lines) + (hidden.Count > 0 ? "\r\n" : ""), Cp1252);

        return ClientAvailable
            ? $"Cash Shop salvo: servidor + cliente ({cli} pacotes na tela). Backup .bak-* ao lado dos arquivos."
            : $"Cash Shop salvo só no SERVIDOR (o cliente não foi achado em {ClientDir}). O jogo vai cobrar o preço novo, mas a tela mostra o antigo até você atualizar o cliente.";
    }

    static void Backup(string file) => File.Copy(file, $"{file}.bak-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: false);
}

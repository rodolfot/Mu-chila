using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Core;

/// <summary>Item criado pelo painel (registro em MuChilaAdmin\itens-novos.json, para listar e poder remover).</summary>
public record NewItemInfo(int Section, int Index, string Name, int BaseSection, int BaseIndex, DateTime Created);

/// <summary>Um item do Data\Item\Item.txt do servidor, com as colunas pelo cabeçalho da seção ("//Type Slot Skill ...").</summary>
public class ServerItem
{
    public int Section, Index, Line;
    public string Name = "", Raw = "";
    public Dictionary<string, string> Values = new();
}

/// <summary>
/// Itens novos "a partir de um existente": o item novo usa o modelo 3D (visual) do item base e ganha código, nome e
/// atributos próprios. Mexe no servidor (Data\Item\Item.txt) e no cliente (pasta do repositório; os jogadores recebem
/// pelo launcher depois de "Publicar atualização do cliente"):
///   Data\Local\{Eng,Por}\item_{eng,por}.bmd       contador + registros de 672 bytes + checksum
///   Data\Local\{Eng,Por}\itemtooltip_{eng,por}.bmd 10.240 registros de 124 bytes (chave seção/índice; vagas vazias no fim) + checksum
///   Data\Local\ItemTRSData.bmd                     contador + registros de 32 bytes (posição/rotação/escala no inventário) + checksum
/// Formato descoberto em 28/09/2026: cada registro com XOR FC CF AB a partir do início dele; checksum = GenerateCheckSum2
/// da Webzen (chave 0xE2F1) sobre os registros cifrados. Posições dos atributos conferidas contra os 2.508 itens do servidor
/// (100% em dano, defesa, velocidade, requisitos, nível, skill e tamanho).
/// </summary>
public static class NewItems
{
    static readonly byte[] Key = { 0xFC, 0xCF, 0xAB };
    const uint CrcKey = 0xE2F1;
    const int ItemRec = 672, TipRec = 124, TipSlots = 10240, TrsRec = 32;
    const int OffCode = 0, OffSection = 4, OffIndex = 6, OffName = 528, NameLen = 64, TipOffName = 4;
    static readonly Encoding Enc = Encoding.Latin1;

    /// <summary>Atributos editáveis: coluna do Item.txt, posição no registro do cliente e nome na tela.</summary>
    public static readonly (string Column, int Offset, string Label)[] Fields =
    {
        ("Level", 596, "Nível do item"), ("DamageMin", 604, "Dano mínimo"), ("DamageMax", 606, "Dano máximo"),
        ("AttackSpeed", 614, "Velocidade de ataque"), ("MagicDamageRate", 620, "Dano mágico (%)"), ("Defense", 610, "Defesa"),
        ("MagicDefense", 612, "Defesa mágica"), ("DefenseSuccessRate", 608, "Taxa de defesa"), ("Durability", 616, "Durabilidade"),
        ("ReqLevel", 634, "Nível exigido"), ("ReqStrength", 624, "Força exigida"), ("ReqDexterity", 626, "Agilidade exigida"),
        ("ReqEnergy", 628, "Energia exigida"), ("ReqVitality", 630, "Vitalidade exigida"), ("ReqLeadership", 632, "Comando exigido"),
    };

    public static string ClientDir => CashShop.ClientDir;
    static string ItemTxt => Path.Combine(ServerControl.ServerRoot, @"Data\Item\Item.txt");
    static string RegistryPath => Path.Combine(ServerControl.ServerRoot, "MuChilaAdmin", "itens-novos.json");
    static string TrsFile => Path.Combine(ClientDir, @"Data\Local\ItemTRSData.bmd");
    static IEnumerable<(string Items, string Tips)> LangFiles() =>
        new[] { ("Eng", "eng"), ("Por", "por") }
            .Select(l => (Path.Combine(ClientDir, $@"Data\Local\{l.Item1}\item_{l.Item2}.bmd"), Path.Combine(ClientDir, $@"Data\Local\{l.Item1}\itemtooltip_{l.Item2}.bmd")))
            .Where(p => File.Exists(p.Item1) && File.Exists(p.Item2));
    public static bool ClientAvailable => LangFiles().Any() && File.Exists(TrsFile);

    // ---------------- cifra e checksum ----------------
    static byte[] Plain(byte[] d, int start, int len) { var r = new byte[len]; for (int j = 0; j < len; j++) r[j] = (byte)(d[start + j] ^ Key[j % 3]); return r; }
    static byte[] Cipher(byte[] plain) { var r = new byte[plain.Length]; for (int j = 0; j < plain.Length; j++) r[j] = (byte)(plain[j] ^ Key[j % 3]); return r; }

    /// <summary>GenerateCheckSum2 da Webzen sobre os registros cifrados.</summary>
    public static uint Checksum(byte[] buf, int start, int len)
    {
        uint res = CrcKey << 9;
        for (int w = 0; w < len / 4; w++)
        {
            uint t = BitConverter.ToUInt32(buf, start + w * 4);
            if ((w + CrcKey) % 2 == 0) res ^= t; else res += t;
            if (w % 4 == 0) res ^= (CrcKey + res) >> (w % 8 + 1);
        }
        return res;
    }

    /// <summary>Arquivo "contador + registros + checksum" → registros em claro.</summary>
    static List<byte[]> ReadCounted(string path, int recSize)
    {
        var d = File.ReadAllBytes(path);
        int n = BitConverter.ToInt32(d, 0);
        if (4 + n * recSize + 4 != d.Length) throw new InvalidDataException($"{Path.GetFileName(path)}: tamanho inesperado ({d.Length} bytes para {n} registros).");
        if (Checksum(d, 4, n * recSize) != BitConverter.ToUInt32(d, d.Length - 4)) throw new InvalidDataException($"{Path.GetFileName(path)}: checksum não confere (arquivo de outra versão?).");
        return Enumerable.Range(0, n).Select(i => Plain(d, 4 + i * recSize, recSize)).ToList();
    }

    static byte[] BuildCounted(List<byte[]> recs)
    {
        using var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(recs.Count));
        foreach (var r in recs) ms.Write(Cipher(r));
        var body = ms.ToArray();
        return body.Concat(BitConverter.GetBytes(Checksum(body, 4, body.Length - 4))).ToArray();
    }

    static List<byte[]> ReadTips(string path)
    {
        var d = File.ReadAllBytes(path);
        if (d.Length != TipSlots * TipRec + 4) throw new InvalidDataException($"{Path.GetFileName(path)}: tamanho inesperado ({d.Length} bytes).");
        if (Checksum(d, 0, TipSlots * TipRec) != BitConverter.ToUInt32(d, d.Length - 4)) throw new InvalidDataException($"{Path.GetFileName(path)}: checksum não confere.");
        return Enumerable.Range(0, TipSlots).Select(i => Plain(d, i * TipRec, TipRec)).ToList();
    }

    static byte[] BuildTips(List<byte[]> recs)
    {
        var body = recs.SelectMany(Cipher).ToArray();
        return body.Concat(BitConverter.GetBytes(Checksum(body, 0, body.Length))).ToArray();
    }

    static bool TipUsed(byte[] r) => r[TipOffName] != 0;
    static (int S, int I) TipKey(byte[] r) => (BitConverter.ToUInt16(r, 0), BitConverter.ToUInt16(r, 2));
    static int Code(byte[] itemRec) => BitConverter.ToInt32(itemRec, OffCode);
    static void SetName(byte[] rec, int off, string name) { Array.Clear(rec, off, NameLen); Enc.GetBytes(name).CopyTo(rec, off); }

    /// <summary>Relê e regrava todos os arquivos do cliente sem mudar nada (tem de sair idêntico). Para teste.</summary>
    public static bool RoundTripIdentical()
    {
        foreach (var (items, tips) in LangFiles())
        {
            if (!BuildCounted(ReadCounted(items, ItemRec)).SequenceEqual(File.ReadAllBytes(items))) return false;
            if (!BuildTips(ReadTips(tips)).SequenceEqual(File.ReadAllBytes(tips))) return false;
        }
        return BuildCounted(ReadCounted(TrsFile, TrsRec)).SequenceEqual(File.ReadAllBytes(TrsFile));
    }

    // ---------------- servidor (Item.txt) ----------------
    static readonly Regex ItemLine = new(@"^\s*(.*?)""([^""]*)""(.*)$");

    public static List<ServerItem> ServerItems()
    {
        var list = new List<ServerItem>();
        var lines = File.ReadAllText(ItemTxt, Enc).Split("\r\n");
        int section = -1; string[]? cols = null;
        for (int n = 0; n < lines.Length; n++)
        {
            var l = lines[n];
            if (l.StartsWith("//Type")) { cols = l[2..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); continue; }
            var s = l.Split("//")[0].Trim();
            if (Regex.IsMatch(s, @"^\d+$")) { section = int.Parse(s); continue; }
            if (s.Length == 0 || s == "end" || cols == null || section < 0) continue;
            var m = ItemLine.Match(s);
            if (!m.Success) continue;
            var vals = m.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Append(m.Groups[2].Value)
                        .Concat(m.Groups[3].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToArray();
            if (vals.Length < cols.Length || !int.TryParse(vals[0], out var idx)) continue;
            list.Add(new ServerItem { Section = section, Index = idx, Name = m.Groups[2].Value, Line = n, Raw = l, Values = cols.Zip(vals).ToDictionary(p => p.First, p => p.Second) });
        }
        return list;
    }

    /// <summary>Linha nova a partir da do item base: troca o Type (índice), o nome e os atributos, mantendo o espaçamento.</summary>
    static string NewServerLine(ServerItem baseItem, int newIndex, string name, IReadOnlyDictionary<string, int> stats)
    {
        var tokens = Regex.Matches(baseItem.Raw, @"""[^""]*""|\S+").ToList();
        var cols = baseItem.Values.Keys.ToList();
        var line = baseItem.Raw;
        // da direita para a esquerda, para as posições continuarem valendo
        for (int t = Math.Min(tokens.Count, cols.Count) - 1; t >= 0; t--)
        {
            string? novo = cols[t] == "Type" ? newIndex.ToString() : cols[t] == "Name" ? $"\"{name}\"" : stats.TryGetValue(cols[t], out var v) ? v.ToString() : null;
            if (novo == null || novo == tokens[t].Value) continue;
            // só o valor muda; espaços e TABs entre as colunas ficam como no item base
            line = line[..tokens[t].Index] + novo + line[(tokens[t].Index + tokens[t].Length)..];
        }
        return line;
    }

    // ---------------- registro ----------------
    public static List<NewItemInfo> List() =>
        File.Exists(RegistryPath) ? JsonSerializer.Deserialize<List<NewItemInfo>>(File.ReadAllText(RegistryPath)) ?? new() : new();
    static void SaveList(List<NewItemInfo> l)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
        File.WriteAllText(RegistryPath, JsonSerializer.Serialize(l, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string CleanName(string name)
    {
        var s = Regex.Replace(name.Replace('"', '\''), @"\s+", " ").Trim();
        if (s.Length < 2 || s.Length > 40) throw new InvalidOperationException("Nome do item: de 2 a 40 caracteres.");
        if (Enc.GetString(Enc.GetBytes(s)) != s) throw new InvalidOperationException("O nome tem caracteres que o jogo não mostra.");
        return s;
    }

    /// <summary>Índice livre na seção: acima de todos os usados no servidor e no cliente (o cliente tem itens que o servidor não tem).</summary>
    public static int NextFreeIndex(int section)
    {
        var usados = ServerItems().Where(i => i.Section == section).Select(i => i.Index).ToList();
        foreach (var (items, tips) in LangFiles())
        {
            usados.AddRange(ReadCounted(items, ItemRec).Select(r => BitConverter.ToUInt16(r, OffSection) == section ? BitConverter.ToUInt16(r, OffIndex) : -1));
            usados.AddRange(ReadTips(tips).Where(TipUsed).Select(TipKey).Where(k => k.S == section).Select(k => k.I));
        }
        usados.AddRange(ReadCounted(TrsFile, TrsRec).Select(r => BitConverter.ToInt32(r, 0)).Where(c => c / 512 == section).Select(c => c % 512));
        int next = usados.Where(x => x >= 0).DefaultIfEmpty(-1).Max() + 1;
        if (next >= 512) throw new InvalidOperationException($"A seção {section} não tem mais índices livres (máximo 511).");
        return next;
    }

    /// <summary>Atributos do item base (os que existem na seção dele), para preencher a tela.</summary>
    public static Dictionary<string, int> BaseStats(int section, int index)
    {
        var b = ServerItems().FirstOrDefault(i => i.Section == section && i.Index == index) ?? throw new InvalidOperationException($"Item {section},{index} não existe no servidor.");
        return Fields.Where(f => b.Values.TryGetValue(f.Column, out var v) && int.TryParse(v, out _)).ToDictionary(f => f.Column, f => int.Parse(b.Values[f.Column]));
    }

    /// <summary>Cria o item. Faz backup .bak-* de cada arquivo. Depois: Reload Item nos servidores e publicar o cliente.</summary>
    public static NewItemInfo Create(int baseSection, int baseIndex, string name, IReadOnlyDictionary<string, int> stats)
    {
        name = CleanName(name);
        if (!ClientAvailable) throw new InvalidOperationException($"Cliente não encontrado em {ClientDir}: o item precisa existir no servidor E no cliente.");
        var baseItem = ServerItems().FirstOrDefault(i => i.Section == baseSection && i.Index == baseIndex) ?? throw new InvalidOperationException("Item base não existe no servidor.");
        foreach (var (col, v) in stats)
        {
            if (!Fields.Any(f => f.Column == col)) throw new InvalidOperationException($"Atributo desconhecido: {col}.");
            if (!baseItem.Values.ContainsKey(col)) throw new InvalidOperationException($"Itens desta seção não têm \"{col}\".");
            if (v < 0 || v > 65535) throw new InvalidOperationException($"{col}: use de 0 a 65535.");
        }
        int idx = NextFreeIndex(baseSection), code = baseSection * 512 + idx, baseCode = baseSection * 512 + baseIndex;
        string stamp = ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

        // monta tudo em memória antes de gravar (se algo faltar, nada é alterado)
        var escrever = new List<(string Path, byte[] Data)>();
        foreach (var (items, tips) in LangFiles())
        {
            var recs = ReadCounted(items, ItemRec);
            var b = recs.FirstOrDefault(r => Code(r) == baseCode) ?? throw new InvalidOperationException($"O item base não existe no cliente ({Path.GetFileName(items)}).");
            var novo = (byte[])b.Clone();
            BitConverter.GetBytes(code).CopyTo(novo, OffCode);
            BitConverter.GetBytes((ushort)baseSection).CopyTo(novo, OffSection);
            BitConverter.GetBytes((ushort)idx).CopyTo(novo, OffIndex);
            SetName(novo, OffName, name);
            foreach (var f in Fields) if (stats.TryGetValue(f.Column, out var v)) BitConverter.GetBytes((ushort)v).CopyTo(novo, f.Offset);
            recs.Add(novo);
            escrever.Add((items, BuildCounted(recs)));

            var tipRecs = ReadTips(tips);
            var bt = tipRecs.FirstOrDefault(r => TipUsed(r) && TipKey(r) == (baseSection, baseIndex)) ?? throw new InvalidOperationException($"O item base não tem tooltip ({Path.GetFileName(tips)}).");
            int livre = tipRecs.FindIndex(r => !TipUsed(r));
            if (livre < 0) throw new InvalidOperationException("Sem vaga no arquivo de tooltip.");
            var nt = (byte[])bt.Clone();
            BitConverter.GetBytes((ushort)baseSection).CopyTo(nt, 0);
            BitConverter.GetBytes((ushort)idx).CopyTo(nt, 2);
            SetName(nt, TipOffName, name);
            tipRecs[livre] = nt;
            escrever.Add((tips, BuildTips(tipRecs)));
        }
        var trs = ReadCounted(TrsFile, TrsRec);
        if (trs.FirstOrDefault(r => BitConverter.ToInt32(r, 0) == baseCode) is { } btrs)
        {
            var n = (byte[])btrs.Clone(); BitConverter.GetBytes(code).CopyTo(n, 0);
            trs.Add(n);
            escrever.Add((TrsFile, BuildCounted(trs)));
        }

        var serverLines = File.ReadAllText(ItemTxt, Enc).Split("\r\n").ToList();
        int ultima = ServerItems().Where(i => i.Section == baseSection).Max(i => i.Line);
        serverLines.Insert(ultima + 1, NewServerLine(baseItem, idx, name, stats));

        foreach (var (p, data) in escrever) { File.Copy(p, p + stamp, overwrite: true); File.WriteAllBytes(p, data); }
        File.Copy(ItemTxt, ItemTxt + stamp, overwrite: true);
        File.WriteAllText(ItemTxt, string.Join("\r\n", serverLines), Enc);

        var info = new NewItemInfo(baseSection, idx, name, baseSection, baseIndex, DateTime.Now);
        var reg = List(); reg.Add(info); SaveList(reg);
        Shops.ResetCatalog();
        return info;
    }

    /// <summary>Remove um item criado pelo painel (servidor e cliente). Itens do kit não podem ser removidos por aqui.</summary>
    public static void Remove(int section, int index)
    {
        var reg = List();
        var info = reg.FirstOrDefault(i => i.Section == section && i.Index == index) ?? throw new InvalidOperationException("Só dá para remover itens criados por este painel.");
        int code = section * 512 + index;
        string stamp = ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var escrever = new List<(string Path, byte[] Data)>();
        foreach (var (items, tips) in LangFiles())
        {
            var recs = ReadCounted(items, ItemRec);
            if (recs.RemoveAll(r => Code(r) == code) > 0) escrever.Add((items, BuildCounted(recs)));
            var tipRecs = ReadTips(tips);
            int t = tipRecs.FindIndex(r => TipUsed(r) && TipKey(r) == (section, index));
            if (t >= 0) { tipRecs.RemoveAt(t); tipRecs.Add(new byte[TipRec]); escrever.Add((tips, BuildTips(tipRecs))); }   // mantém as usadas juntas no começo
        }
        var trs = ReadCounted(TrsFile, TrsRec);
        if (trs.RemoveAll(r => BitConverter.ToInt32(r, 0) == code) > 0) escrever.Add((TrsFile, BuildCounted(trs)));

        var linha = ServerItems().FirstOrDefault(i => i.Section == section && i.Index == index && i.Name == info.Name);
        var serverLines = File.ReadAllText(ItemTxt, Enc).Split("\r\n").ToList();
        if (linha != null) serverLines.RemoveAt(linha.Line);

        foreach (var (p, data) in escrever) { File.Copy(p, p + stamp, overwrite: true); File.WriteAllBytes(p, data); }
        if (linha != null) { File.Copy(ItemTxt, ItemTxt + stamp, overwrite: true); File.WriteAllText(ItemTxt, string.Join("\r\n", serverLines), Enc); }
        reg.Remove(info); SaveList(reg);
        Shops.ResetCatalog();
    }
}

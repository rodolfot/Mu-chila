using System.Data;
using System.IO;
using System.Text;

namespace MuChilaAdmin.Core;

/// <summary>Um item decodificado de uma posição do inventário (Character.Inventory) ou do baú (warehouse.Items).</summary>
public record ItemView(int Slot, string Place, int Section, int Type, string Name, int Level, bool Skill, bool Luck, int Option,
                       int Excellent, int SetOption, int Durability, uint Serial, string Hex);

/// <summary>
/// Itens dos personagens e do baú. Cada item ocupa 16 bytes (conferido em 26/09/2026 contra inventários reais):
///   [0] índice (8 bits baixos)  [1] nível (bits 3-6), skill (bit 7), sorte (bit 2), opção (bits 0-1)  [2] durabilidade
///   [3..6] serial  [7] excelente (bits 0-5), opção +4 (bit 6), índice bit 8 (bit 7)  [8] set (ancient)
///   [9] seção (bits 4-7)  [10] harmonia  [11..15] sockets.  Posição vazia = 16 bytes 0xFF.
/// Inventário: 237 posições (0-11 equipado). Baú: 240 posições (0-119 baú, 120-239 baú estendido).
/// </summary>
public static class Items
{
    public const int ItemSize = 16;
    static readonly string BackupDir = Path.Combine(ServerControl.ServerRoot, "DB");
    static readonly string[] Equip = { "arma (direita)", "arma/escudo (esquerda)", "elmo", "armadura", "calça", "luvas", "botas", "asa", "pet", "pendente", "anel", "anel" };

    public static string InventoryPlace(int slot) => slot switch
    {
        < 12 => "equipado: " + Equip[slot],
        < 76 => "inventário",
        < 140 => "inventário expandido",
        < 172 => "loja pessoal",
        _ => "outros",
    };

    public static string VaultPlace(int slot) => slot < 120 ? "baú" : "baú estendido";

    public static ItemView? Decode(byte[] data, int slot, Func<int, string> place)
    {
        int o = slot * ItemSize;
        if (o + ItemSize > data.Length) return null;
        var it = data.AsSpan(o, ItemSize);
        if (it.ToArray().All(b => b == 0xFF)) return null;
        int index = it[0] | ((it[7] & 0x80) << 1) | ((it[9] & 0xF0) << 5);
        int section = index / 512, type = index % 512;
        uint serial = (uint)(it[3] << 24 | it[4] << 16 | it[5] << 8 | it[6]);
        return new ItemView(slot, place(slot), section, type, Shops.Name(section, type), (it[1] >> 3) & 0xF, (it[1] & 0x80) != 0, (it[1] & 0x04) != 0,
                            (it[1] & 3) + ((it[7] & 0x40) != 0 ? 4 : 0), it[7] & 0x3F, it[8], it[2], serial, Convert.ToHexString(it));
    }

    public static List<ItemView> Inventory(string character)
    {
        var t = Db.Query("SELECT Inventory FROM Character WHERE Name = @n", ("@n", character));
        if (t.Rows.Count == 0 || t.Rows[0][0] is not byte[] data) return new();
        return Enumerable.Range(0, data.Length / ItemSize).Select(s => Decode(data, s, InventoryPlace)).OfType<ItemView>().ToList();
    }

    public static List<ItemView> Vault(string account)
    {
        var t = Db.Query("SELECT Items FROM warehouse WHERE AccountID = @a", ("@a", account));
        if (t.Rows.Count == 0 || t.Rows[0][0] is not byte[] data) return new();
        return Enumerable.Range(0, data.Length / ItemSize).Select(s => Decode(data, s, VaultPlace)).OfType<ItemView>().ToList();
    }

    /// <summary>Apaga um item do inventário (character != null) ou do baú da conta. Só com a conta fora do jogo; confere que o
    /// item ainda é o mesmo e grava o inventário/baú inteiro antes em C:\MuServer\DB\backup-itens-*.csv.</summary>
    public static string Remove(string account, string? character, ItemView item)
    {
        if (Db.IsOnline(account)) throw new InvalidOperationException($"A conta {account} está no jogo. O servidor regravaria o item ao sair: remova com a conta fora do jogo.");
        var (table, column, key, keyValue) = character != null ? ("Character", "Inventory", "Name", character) : ("warehouse", "Items", "AccountID", account);
        var t = Db.Query($"SELECT {column} FROM {table} WHERE {key} = @k", ("@k", keyValue));
        if (t.Rows.Count == 0 || t.Rows[0][0] is not byte[] data) throw new InvalidOperationException("Inventário/baú não encontrado.");
        int o = item.Slot * ItemSize;
        if (Convert.ToHexString(data, o, ItemSize) != item.Hex) throw new InvalidOperationException("O item mudou desde que a lista foi carregada. Atualize e tente de novo.");

        Directory.CreateDirectory(BackupDir);
        var backup = Path.Combine(BackupDir, $"backup-itens-{account}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        File.WriteAllText(backup, $"tabela;chave;coluna;hex{Environment.NewLine}{table};{keyValue};{column};{Convert.ToHexString(data)}{Environment.NewLine}", Encoding.UTF8);

        var novo = (byte[])data.Clone();
        for (int i = 0; i < ItemSize; i++) novo[o + i] = 0xFF;
        const string offline = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1)";
        int n = Db.Execute($"UPDATE {table} SET {column} = @novo WHERE {key} = @k AND {column} = @velho AND {offline}",
            ("@novo", novo), ("@velho", data), ("@k", keyValue), ("@a", account));
        if (n != 1) throw new InvalidOperationException("Nada alterado: a conta entrou no jogo ou o item mudou no meio do caminho.");
        return $"{item.Name} +{item.Level} removido de {(character ?? "baú de " + account)} (posição {item.Slot}). Backup: {backup}";
    }

    // ---------- colocar item novo (29/09/2026) ----------
    // Área onde o item novo pode entrar: baú = posições 0-119 (8 x 15); inventário = 12-75 (8 x 8, sem os equipados e sem
    // as expansões, que só existem para quem liberou).
    const int GridW = 8;

    /// <summary>
    /// Coloca um item NOVO no baú da conta (character == null) ou no inventário do personagem, na 1ª posição livre em que ele
    /// cabe (respeita largura x altura do Item.txt e os itens que já estão lá). Só com a conta fora do jogo: o servidor
    /// regravaria o inventário/baú ao sair. Série nova pelo WZ_GetItemSerial (o mesmo contador do servidor). Durabilidade =
    /// a do Item.txt; item que empilha (ItemStack.txt) usa a durabilidade como quantidade. Backup em DB\backup-itens-*.csv.
    /// </summary>
    public static string Place(string account, string? character, int section, int type, int level, bool skill, bool luck, int option, int excellent, int quantity)
    {
        var def = Shops.Find(section, type) ?? throw new InvalidOperationException($"Item {section},{type} não existe no Item.txt.");
        if (level is < 0 or > 15 || option is < 0 or > 7 || excellent is < 0 or > 63) throw new InvalidOperationException("Nível 0–15, opção 0–7 e excelente 0–63.");
        int stack = CashShop.MaxStack(section, type);
        if (quantity < 1 || quantity > Math.Max(1, stack)) throw new InvalidOperationException(stack > 1 ? $"{def.Name} empilha até {stack}." : $"{def.Name} não empilha: quantidade 1.");
        if (Db.IsOnline(account)) throw new InvalidOperationException($"A conta {account} está no jogo: o servidor regravaria o inventário/baú ao sair. Coloque com a conta fora do jogo.");

        var (table, column, key, keyValue) = character != null ? ("Character", "Inventory", "Name", character) : ("warehouse", "Items", "AccountID", account);
        var t = Db.Query($"SELECT {column} FROM {table} WHERE {key} = @k", ("@k", keyValue));
        if (t.Rows.Count == 0 || t.Rows[0][0] is not byte[] data)
            throw new InvalidOperationException(character != null ? "Personagem não encontrado." : $"A conta {account} ainda não tem baú (abra o baú uma vez no jogo).");
        var (start, rows) = character != null ? (12, 8) : (0, 15);
        if (data.Length < (start + GridW * rows) * ItemSize) throw new InvalidOperationException("Inventário/baú com tamanho inesperado; nada foi alterado.");

        // ocupação da grade pelos itens que já estão lá
        var busy = new bool[rows, GridW];
        for (int s = start; s < start + GridW * rows; s++)
        {
            var it = Decode(data, s, _ => "");
            if (it == null) continue;
            var d = Shops.Find(it.Section, it.Type);
            int x0 = (s - start) % GridW, y0 = (s - start) / GridW;
            for (int y = y0; y < Math.Min(rows, y0 + (d?.Height ?? 1)); y++)
                for (int x = x0; x < Math.Min(GridW, x0 + (d?.Width ?? 1)); x++) busy[y, x] = true;
        }
        int slot = -1;
        for (int y = 0; y + def.Height <= rows && slot < 0; y++)
            for (int x = 0; x + def.Width <= GridW && slot < 0; x++)
            {
                bool free = true;
                for (int dy = 0; dy < def.Height && free; dy++) for (int dx = 0; dx < def.Width && free; dx++) free = !busy[y + dy, x + dx];
                if (free) slot = start + y * GridW + x;
            }
        if (slot < 0) throw new InvalidOperationException($"Não há espaço {def.Width}x{def.Height} livre no {(character != null ? "inventário de " + character : "baú de " + account)}.");

        int dur = stack > 1 ? quantity : Math.Clamp(BaseDurability(section, type), 1, 255);
        // NULL quando a tabela GameServerInfo está vazia (banco recém-restaurado em que o GameServer ainda não ligou)
        long serialDb = Db.Scalar("EXEC WZ_GetItemSerial") is { } sv and not DBNull ? Convert.ToInt64(sv) : -1;
        if (serialDb <= 0) throw new InvalidOperationException("O banco não deu um número de série para o item (WZ_GetItemSerial). Se o banco acabou de ser restaurado, a tabela GameServerInfo está vazia: ela é preenchida quando o GameServer liga.");
        uint serial = (uint)serialDb;
        var item = new byte[ItemSize];
        item[0] = (byte)(type & 0xFF);
        item[1] = (byte)((level << 3) | (skill ? 0x80 : 0) | (luck ? 0x04 : 0) | (option & 3));
        item[2] = (byte)dur;
        item[3] = (byte)(serial >> 24); item[4] = (byte)(serial >> 16); item[5] = (byte)(serial >> 8); item[6] = (byte)serial;
        item[7] = (byte)((excellent & 0x3F) | (option >= 4 ? 0x40 : 0) | ((type & 0x100) != 0 ? 0x80 : 0));
        item[8] = 0;
        item[9] = (byte)(section << 4);
        item[10] = 0;
        for (int i = 11; i < ItemSize; i++) item[i] = 0xFF;   // sem sockets (igual aos itens comuns do servidor)

        Directory.CreateDirectory(BackupDir);
        var backup = Path.Combine(BackupDir, $"backup-itens-{account}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        File.WriteAllText(backup, $"tabela;chave;coluna;hex{Environment.NewLine}{table};{keyValue};{column};{Convert.ToHexString(data)}{Environment.NewLine}", Encoding.UTF8);
        var novo = (byte[])data.Clone();
        Buffer.BlockCopy(item, 0, novo, slot * ItemSize, ItemSize);
        const string offline = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1)";
        int n = Db.Execute($"UPDATE {table} SET {column} = @novo WHERE {key} = @k AND {column} = @velho AND {offline}",
            ("@novo", novo), ("@velho", data), ("@k", keyValue), ("@a", account));
        if (n != 1) throw new InvalidOperationException("Nada alterado: a conta entrou no jogo ou o inventário/baú mudou no meio do caminho.");
        var extras = string.Concat(skill ? " +skill" : "", luck ? " +sorte" : "", option > 0 ? $" +{option * 4} opção" : "", excellent > 0 ? $" exc {excellent}" : "", stack > 1 ? $" x{quantity}" : "");
        return $"{def.Name} +{level}{extras} colocado no {(character != null ? "inventário de " + character : "baú de " + account)} (posição {slot}, série {serial}). Backup: {backup}";
    }

    static Dictionary<(int, int), int>? durability;
    /// <summary>Durabilidade base do item (coluna "Durability" da seção no Item.txt); 1 se a seção não tem essa coluna.</summary>
    public static int BaseDurability(int section, int type)
    {
        if (durability == null)
        {
            var dict = new Dictionary<(int, int), int>();
            int sec = -1; List<string>? head = null;
            foreach (var line in File.ReadAllLines(Path.Combine(ServerControl.ServerRoot, @"Data\Item\Item.txt"), Encoding.Latin1))
            {
                var tt = line.Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(tt, @"^\d+$")) { sec = int.Parse(tt); head = null; continue; }
                if (tt.StartsWith("//Type") || tt.StartsWith("//Index")) { var h = tt[2..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList(); head = h.Skip(h.IndexOf("Name") + 1).ToList(); continue; }
                var m = System.Text.RegularExpressions.Regex.Match(tt, @"^(\d+)\s+(?:\S+\s+){7}""[^""]*""\s+(.*)$");
                if (!m.Success || sec < 0 || head == null) continue;
                int di = head.IndexOf("Durability");
                var rest = m.Groups[2].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                dict.TryAdd((sec, int.Parse(m.Groups[1].Value)), di >= 0 && di < rest.Length && int.TryParse(rest[di], out var dv) ? dv : 1);
            }
            durability = dict;
        }
        return durability.TryGetValue((section, type), out var v) ? v : 1;
    }

    // ---------- Gremory Case (presentes) ----------
    // RewardSource: de onde veio o presente. O valor certo sai de uma linha criada pelo próprio servidor (/gremgif);
    // enquanto não for calibrado (arquivo gremory.txt ao lado do programa com "RewardSource=N"), o painel não cria presentes.
    static readonly string GremoryConfig = Path.Combine(AppContext.BaseDirectory, "gremory.txt");

    public static int? RewardSource()
    {
        if (!File.Exists(GremoryConfig)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(GremoryConfig), @"RewardSource\s*=\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    public static DataTable Gifts(string account) => Db.Query(@"
        SELECT AuthCode AS Codigo, CASE StorageType WHEN 1 THEN 'conta' ELSE Name END AS Para, ItemID, ItemLevel AS Nivel,
               ItemOp1 AS Skill, ItemOp2 AS Sorte, ItemOp3 AS Opcao, ItemExcOption AS Exc,
               DATEADD(second, ExpireDate, '1970-01-01') AS Expira
        FROM GremoryCase WHERE AccountID = @a ORDER BY ReceiveDate", ("@a", account));

    public static string Gift(string account, string? character, int section, int type, int level, bool skill, bool luck, int option, int excellent, int days)
    {
        var source = RewardSource() ?? throw new InvalidOperationException(
            "A Gremory Case ainda não foi calibrada: use uma vez o /gremgif no jogo (com um GM) para o servidor criar um presente de exemplo.");
        if (Shops.Find(section, type) == null) throw new InvalidOperationException($"Item {section},{type} não existe no Item.txt.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var t = Db.Query("EXEC GremoryCaseAddItem @a, @n, @st, @src, @id, @lvl, @dur, @o1, @o2, @o3, @exc, 0, 0, 0, 0, 0, @rec, @exp",
            ("@a", account), ("@n", (object?)character ?? ""), ("@st", character != null ? 2 : 1), ("@src", source), ("@id", section * 512 + type),
            ("@lvl", level), ("@dur", 255), ("@o1", skill ? 1 : 0), ("@o2", luck ? 1 : 0), ("@o3", option), ("@exc", excellent),
            ("@rec", now), ("@exp", now + days * 86400L));
        return $"Presente {Shops.Name(section, type)} +{level} criado para {(character ?? "a conta " + account)} (vale {days} dia(s); o jogador recebe na Gremory Case ao entrar).";
    }

    public static void CancelGift(string account, int authCode) =>
        Db.Execute("DELETE FROM GremoryCase WHERE AccountID = @a AND AuthCode = @c", ("@a", account), ("@c", authCode));
}

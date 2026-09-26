using System.Data;
using System.IO;
using System.Text;

namespace MuChilaAdmin;

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

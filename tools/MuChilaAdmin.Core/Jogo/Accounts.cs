using System.Data;
using System.IO;

namespace MuChilaAdmin.Core;

/// <summary>VIP (MEMB_INFO.AccountLevel + AccountExpireDate) e bloqueio de contas.</summary>
public static class Accounts
{
    public static readonly string[] Levels = { "Free", "VIP 1", "VIP 2", "VIP 3" };

    public static DataTable List() => Db.Query(@"
        SELECT m.memb___id AS Conta,
               CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE()
                    THEN 'VIP ' + CAST(m.AccountLevel AS varchar) ELSE 'Free' END AS Nivel,
               CASE WHEN m.AccountLevel > 0 THEN CONVERT(varchar(16), m.AccountExpireDate, 103) + ' ' + CONVERT(varchar(5), m.AccountExpireDate, 108) ELSE '' END AS Expira,
               CASE WHEN m.bloc_code = '1' THEN 'SIM' ELSE '' END AS Banida,
               CASE WHEN s.ConnectStat = 1 THEN 'online' ELSE '' END AS Status,
               (SELECT STRING_AGG(c.Name, ', ') FROM Character c WHERE c.AccountID = m.memb___id) AS Personagens
        FROM MEMB_INFO m LEFT JOIN MEMB_STAT s ON s.memb___id = m.memb___id
        ORDER BY m.memb___id");

    /// <summary>Define o VIP com validade de hoje + dias (level 0 remove o VIP). Vale no proximo login da conta.</summary>
    public static void SetVip(string account, int level, int days)
    {
        var expire = level == 0 ? DateTime.Now : DateTime.Now.AddDays(days);
        Db.Execute("UPDATE MEMB_INFO SET AccountLevel = @l, AccountExpireDate = @e WHERE memb___id = @a",
            ("@l", level), ("@e", expire), ("@a", account));
    }

    public static void SetBanned(string account, bool banned) =>
        Db.Execute("UPDATE MEMB_INFO SET bloc_code = @b WHERE memb___id = @a", ("@b", banned ? "1" : "0"), ("@a", account));

    public static string[] Characters(string account) =>
        Db.Query("SELECT Name FROM Character WHERE AccountID = @a ORDER BY Name", ("@a", account))
          .Rows.Cast<DataRow>().Select(r => (string)r["Name"]).ToArray();

    /// <summary>
    /// Apaga as habilidades master do personagem e devolve os pontos (1 por Master Level, MasterSkillTreePoint = 1).
    /// Usado quando a janela (C) mostra "Suces de Atq" negativo (ver docs/PROBLEMAS-E-SOLUCOES.md). A conta precisa estar offline.
    /// </summary>
    public static string ClearMasterSkills(string account, string character)
    {
        if (Db.IsOnline(account)) throw new InvalidOperationException($"A conta {account} está online. Peça para sair do jogo e tente de novo.");
        var t = Db.Query(@"SELECT c.MagicList, m.Name AS Tree, m.MasterLevel, m.MasterPoint, m.MasterSkill
                           FROM Character c LEFT JOIN MasterSkillTree m ON m.Name = c.Name
                           WHERE c.Name = @n AND c.AccountID = @a", ("@n", character), ("@a", account));
        if (t.Rows.Count == 0) return $"{character}: personagem não encontrado na conta {account}.";
        var row = t.Rows[0];
        bool hasTree = row["Tree"] is string;
        int level = row["MasterLevel"] is int l ? l : 0, points = row["MasterPoint"] is int p ? p : 0;
        var oldTree = row["MasterSkill"] as byte[] ?? Array.Empty<byte>();
        var oldMagic = row["MagicList"] as byte[] ?? Array.Empty<byte>();

        // Arvore master: 3 bytes por habilidade (indice baixo, nivel, indice alto); FF 00 FF = vazio
        var emptyTree = new byte[oldTree.Length == 0 ? 360 : oldTree.Length];
        for (int i = 0; i + 2 < emptyTree.Length; i += 3) { emptyTree[i] = 0xFF; emptyTree[i + 1] = 0; emptyTree[i + 2] = 0xFF; }
        int learned = 0;
        for (int i = 0; i + 2 < oldTree.Length; i += 3) if (!IsEmptySlot(oldTree, i)) learned++;

        // Lista de habilidades do personagem: tira os poderes da arvore master (e devolve a habilidade original que eles substituiam)
        var (newMagic, changes) = ResetMagicList(oldMagic);

        var backup = Path.Combine(ServerControl.ServerRoot, "DB", "backup-caixas-master-habilidades.csv");
        File.AppendAllText(backup, $"{DateTime.Now:s};{account};{character};MasterLevel={level};MasterPoint={points};" +
                                   $"MasterSkill={Convert.ToHexString(oldTree)};MagicList={Convert.ToHexString(oldMagic)}{Environment.NewLine}");

        const string offline = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1)";
        int n = Db.Execute($@"SET XACT_ABORT ON; BEGIN TRAN;
            {(hasTree ? $"UPDATE MasterSkillTree SET MasterSkill = @b, MasterPoint = ISNULL(MasterLevel, 0) WHERE Name = @n AND {offline};" : "")}
            UPDATE Character SET MagicList = @m WHERE Name = @n AND AccountID = @a AND {offline};
            COMMIT;",
            ("@b", emptyTree), ("@m", newMagic), ("@n", character), ("@a", account));
        if (n != (hasTree ? 2 : 1)) throw new InvalidOperationException($"A conta {account} entrou no jogo durante a operação; nada foi alterado.");

        var poderes = changes.Count == 0 ? "nenhum poder master na lista de habilidades" : $"poderes: {string.Join(", ", changes)}";
        return $"{character}: {learned} habilidade(s) da árvore master apagada(s); {poderes}; " +
               $"pontos master agora = {(hasTree ? level : 0)} (antes {points}). Backup em {backup}";
    }

    public static readonly string[] PointKinds = { "Pontos de atributo", "Pontos master" };

    /// <summary>
    /// Soma pontos livres ao personagem: kind 0 = Character.LevelUpPoint (distribuir em força, agilidade...),
    /// kind 1 = MasterSkillTree.MasterPoint (árvore master; só quem já tem registro master). Quantidade negativa tira
    /// (sem deixar abaixo de 0). A conta precisa estar offline: o servidor grava o personagem ao sair e desfaria a mudança.
    /// Cada entrega fica em DB\muchila-pontos-dados.csv.
    /// </summary>
    public static string GivePoints(string account, string character, int kind, int amount)
    {
        if (amount == 0) throw new ArgumentException("Informe uma quantidade diferente de 0.");
        if (Db.IsOnline(account)) throw new InvalidOperationException($"A conta {account} está online. Peça para sair do jogo e tente de novo.");
        const string offline = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1)";
        var (table, column) = kind == 1 ? ("MasterSkillTree", "MasterPoint") : ("Character", "LevelUpPoint");
        var owner = kind == 1 ? "EXISTS (SELECT 1 FROM Character c WHERE c.Name = t.Name AND c.AccountID = @a)" : "t.AccountID = @a";

        var t = Db.Query($"SELECT ISNULL(t.{column}, 0) AS P FROM {table} t WHERE t.Name = @n AND {owner}", ("@n", character), ("@a", account));
        if (t.Rows.Count == 0)
            return kind == 1 ? $"{character}: ainda não tem árvore master (precisa chegar ao nível 400 e entrar no jogo uma vez); nada foi alterado."
                             : $"{character}: personagem não encontrado na conta {account}.";
        long before = Convert.ToInt64(t.Rows[0]["P"]);
        long after = Math.Clamp(before + amount, 0, int.MaxValue);

        int n = Db.Execute($"UPDATE t SET t.{column} = @v FROM {table} t WHERE t.Name = @n AND {owner} AND ISNULL(t.{column}, 0) = @old AND {offline}",
            ("@v", (int)after), ("@n", character), ("@a", account), ("@old", (int)before));
        if (n != 1) throw new InvalidOperationException($"A conta {account} entrou no jogo (ou os pontos mudaram) durante a operação; nada foi alterado.");

        var log = Path.Combine(ServerControl.ServerRoot, "DB", "muchila-pontos-dados.csv");
        File.AppendAllText(log, $"{DateTime.Now:s};{account};{character};{PointKinds[kind]};{amount};{before};{after}{Environment.NewLine}");
        var verb = amount > 0 ? $"+{amount}" : $"{amount}";
        return $"{character}: {PointKinds[kind].ToLowerInvariant()} {verb} ({before} → {after}). Registro em {log}";
    }

    static bool IsEmptySlot(byte[] b, int i) => b[i] == 0xFF && b[i + 1] == 0 && b[i + 2] == 0xFF;

    /// <summary>
    /// Raio-x da árvore master de um personagem (01/10/2026), só leitura: Master Level, pontos livres e cada posição da árvore
    /// (MasterSkillTree.MasterSkill) e da lista de habilidades (Character.MagicList), com o nome (Skill.txt) e a regra da habilidade
    /// no MasterSkillTree.txt (seção, Index, Group, níveis, requisito, o que substitui). Serve para conferir o formato que o
    /// GameServer grava antes de distribuir pontos master pelo site: aprenda algumas habilidades no jogo e rode antes e depois.
    /// </summary>
    public static string DumpMasterTree(string character)
    {
        var t = Db.Query(@"SELECT c.Name, c.AccountID, c.Class, c.cLevel, c.MagicList, m.MasterLevel, m.MasterPoint, m.MasterSkill
                           FROM Character c LEFT JOIN MasterSkillTree m ON m.Name = c.Name WHERE c.Name = @n", ("@n", character));
        if (t.Rows.Count == 0) return $"{character}: personagem não encontrado.";
        var r = t.Rows[0];
        var nomes = SkillNames(); var regras = MasterTreeRules();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss}  personagem {r["Name"]} (conta {r["AccountID"]}), classe {r["Class"]}, nível {r["cLevel"]}, " +
                      $"Master Level {(r["MasterLevel"] is DBNull ? "-" : r["MasterLevel"])}, pontos master livres {(r["MasterPoint"] is DBNull ? "-" : r["MasterPoint"])}");
        void Slots(string titulo, byte[]? b)
        {
            sb.AppendLine().AppendLine($"== {titulo}: {(b == null ? "vazio (NULL)" : $"{b.Length} bytes, {b.Length / 3} posições")}");
            if (b == null) return;
            sb.AppendLine(Convert.ToHexString(b));
            int soma = 0;
            for (int i = 0; i + 2 < b.Length; i += 3)
            {
                if (IsEmptySlot(b, i)) continue;
                int skill = b[i] | (b[i + 2] << 8);
                soma += b[i + 1];
                sb.AppendLine($"  posição {i / 3,3}: habilidade {skill,4}  nível {b[i + 1],2}  {(nomes.TryGetValue(skill, out var n) ? n : "?"),-36} {(regras.TryGetValue(skill, out var g) ? g : "")}");
            }
            sb.AppendLine($"  soma dos níveis: {soma}");
        }
        Slots("Árvore master (MasterSkillTree.MasterSkill)", r["MasterSkill"] as byte[]);
        Slots("Lista de habilidades (Character.MagicList)", r["MagicList"] as byte[]);
        return sb.ToString();
    }

    static Dictionary<int, string> SkillNames()
    {
        var d = new Dictionary<int, string>();
        foreach (var l in File.ReadLines(Path.Combine(ServerControl.ServerRoot, @"Data\Skill\Skill.txt"), System.Text.Encoding.Latin1))
        {
            var m = System.Text.RegularExpressions.Regex.Match(l, @"^\s*(\d+)\s+""([^""]*)""");
            if (m.Success) d.TryAdd(int.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim());
        }
        return d;
    }

    /// <summary>Habilidade da árvore -> "seção S, Index I, Group G, níveis min-max, requer X/Y, substitui Z" (MasterSkillTree.txt).</summary>
    static Dictionary<int, string> MasterTreeRules()
    {
        var d = new Dictionary<int, string>(); int secao = -1;
        foreach (var line in File.ReadLines(Path.Combine(ServerControl.ServerRoot, @"Data\Skill\MasterSkillTree.txt"), System.Text.Encoding.Latin1))
        {
            var v = line.Split("//")[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (v.Length == 1 && int.TryParse(v[0], out var s)) { secao = s; continue; }
            if (v.Length < 10 || !v.Take(10).All(x => int.TryParse(x, out _))) continue;
            d.TryAdd(int.Parse(v[1]), $"seção {secao}, Index {v[0]}, Group {v[2]}, níveis {v[3]}-{v[4]}, requer {v[8]}/{v[9]}, substitui {v[7]}");
        }
        return d;
    }

    static Dictionary<int, int>? replaceMap;

    /// <summary>Habilidade da arvore master -> habilidade que ela substitui na lista (0 = passiva). Lido do MasterSkillTree.txt do servidor.</summary>
    static Dictionary<int, int> MasterSkillReplace()
    {
        if (replaceMap != null) return replaceMap;
        var map = new Dictionary<int, int>();
        foreach (var line in File.ReadLines(Path.Combine(ServerControl.ServerRoot, @"Data\Skill\MasterSkillTree.txt"), System.Text.Encoding.Latin1))
        {
            // Index Skill Group MinLevel MaxLevel ExtValue RelatedSkill ReplaceSkill RequireSkill RequireSkill
            var v = line.Split("//")[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (v.Length < 10 || !v.Take(10).All(x => int.TryParse(x, out _))) continue;
            map.TryAdd(int.Parse(v[1]), int.Parse(v[7]));
        }
        return replaceMap = map;
    }

    /// <summary>
    /// Na MagicList (3 bytes por habilidade: indice baixo, nivel, indice alto), cada habilidade da arvore master sai.
    /// Se ela substituia outra (ex.: 330 "Twisting Slash Improved" substitui 41 "Twisting Slash"), segue a cadeia ate a
    /// habilidade comum e a devolve no mesmo lugar, nivel 0, se o personagem ainda nao a tiver.
    /// </summary>
    static (byte[] List, List<string> Changes) ResetMagicList(byte[] magic)
    {
        var rep = MasterSkillReplace();
        var list = (byte[])magic.Clone(); var changes = new List<string>(); var present = new HashSet<int>();
        for (int i = 0; i + 2 < list.Length; i += 3) if (!IsEmptySlot(list, i)) present.Add(list[i] | (list[i + 2] << 8));
        for (int i = 0; i + 2 < list.Length; i += 3)
        {
            if (IsEmptySlot(list, i)) continue;
            int skill = list[i] | (list[i + 2] << 8);
            if (!rep.ContainsKey(skill)) continue;
            int original = skill; var seen = new HashSet<int>();
            while (rep.TryGetValue(original, out var r) && r != 0 && seen.Add(original)) original = r;
            present.Remove(skill);
            if (!rep.ContainsKey(original) && !present.Contains(original))
            {
                list[i] = (byte)(original & 0xFF); list[i + 1] = 0; list[i + 2] = (byte)(original >> 8);
                present.Add(original); changes.Add($"{skill}→{original}");
            }
            else { list[i] = 0xFF; list[i + 1] = 0; list[i + 2] = 0xFF; changes.Add($"{skill} removida"); }
        }
        return (list, changes);
    }

    public static DataTable Online() => Db.Query(@"
        SELECT s.memb___id AS Conta, a.GameIDC AS Personagem, s.IP, CONVERT(varchar(5), s.ConnectTM, 108) AS Desde
        FROM MEMB_STAT s LEFT JOIN AccountCharacter a ON a.Id = s.memb___id
        WHERE s.ConnectStat = 1 ORDER BY s.memb___id");
}

using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MuChilaAdmin.Core;

/// <summary>
/// Atributos dos monstros (issue #49): as colunas do Data\Monster\Monster.txt que dá para mudar sem mexer na estrutura do
/// monstro (vida, dano, defesa, velocidade, visão, renascer, resistências e o elemental). Ficam de fora: Type, AttackType,
/// Attribute, MonsterSkill e o tipo/padrão elemental (definem o que o monstro é), e as taxas de drop (página Drops).
/// Colunas: Index Type "Name" Level MaxLife MaxMana DamageMin DamageMax Defense MagicDefense AttackRate DefenseRate MoveRange
/// AttackType AttackRange ViewRange MoveSpeed AttackSpeed RegenTime Attribute ItemRate MoneyRate MaxItemLevel MonsterSkill
/// Resistance1..7 ElementalAttribute ElementalPattern ElementalDefense ElementalDamageMin ElementalDamageMax
/// ElementalAttackRate ElementalDefenseRate (38 colunas). Grava só as linhas mudadas, sem desalinhar as colunas; backup .bak-*.
/// Vale com Reload Monster (o painel deixa para depois da invasão, se houver uma no ar).
/// </summary>
public static class MonsterStats
{
    public sealed record Campo(int Coluna, string Nome, string Grupo, int Min, int Max, string Ajuda = "");

    public const int Colunas = 38;

    public static readonly Campo[] Campos =
    {
        new(3, "Nível", "Principais", 1, 1000, "Também define o EXP que ele dá e as regras de drop por nível."),
        new(4, "Vida", "Principais", 1, 2_000_000_000),
        new(5, "Mana", "Principais", 0, 2_000_000_000),
        new(6, "Dano mínimo", "Ataque", 0, 10_000_000),
        new(7, "Dano máximo", "Ataque", 0, 10_000_000),
        new(10, "Taxa de ataque", "Ataque", 0, 10_000_000, "Chance de acertar o golpe."),
        new(14, "Alcance do ataque", "Ataque", 0, 30, "Em posições do mapa (1 = corpo a corpo)."),
        new(17, "Velocidade de ataque", "Ataque", 0, 60_000, "Milissegundos entre os golpes: menor = ataca mais rápido."),
        new(8, "Defesa", "Defesa", 0, 10_000_000),
        new(9, "Defesa mágica", "Defesa", 0, 10_000_000),
        new(11, "Taxa de defesa", "Defesa", 0, 10_000_000, "Chance de desviar do golpe."),
        new(12, "Raio de movimento", "Comportamento", 0, 100, "Quanto anda em volta do ponto onde nasceu."),
        new(15, "Visão", "Comportamento", 0, 30, "Distância em que enxerga o jogador e vem atacar."),
        new(16, "Velocidade de movimento", "Comportamento", 0, 60_000, "Milissegundos por passo: menor = mais rápido."),
        new(18, "Tempo para renascer", "Comportamento", 0, 86_400, "Segundos depois de morrer."),
        new(24, "Gelo", "Resistências", 0, 255),
        new(25, "Veneno", "Resistências", 0, 255),
        new(26, "Raio", "Resistências", 0, 255),
        new(27, "Fogo", "Resistências", 0, 255),
        new(28, "Terra", "Resistências", 0, 255),
        new(29, "Vento", "Resistências", 0, 255),
        new(30, "Água", "Resistências", 0, 255),
        new(33, "Defesa elemental", "Elemental", 0, 10_000_000),
        new(34, "Dano elemental mínimo", "Elemental", 0, 10_000_000),
        new(35, "Dano elemental máximo", "Elemental", 0, 10_000_000),
        new(36, "Taxa de ataque elemental", "Elemental", 0, 10_000_000),
        new(37, "Taxa de defesa elemental", "Elemental", 0, 10_000_000),
    };

    /// <summary>Um monstro (Type 0) com os valores de todas as colunas numéricas (índice = coluna; a 2, o nome, fica 0).</summary>
    public sealed class Monstro
    {
        public int Index { get; init; }
        public string Nome { get; init; } = "";
        public int[] Valores { get; init; } = new int[Colunas];
        public int Valor(Campo c) => Valores[c.Coluna];
        public Monstro Copia() => new() { Index = Index, Nome = Nome, Valores = (int[])Valores.Clone() };
    }

    static readonly Regex Token = new(@"""[^""]*""|\S+");
    static readonly Encoding Enc = Encoding.Latin1;

    static List<Match>? Tokens(string line)
    {
        int c = line.IndexOf("//", StringComparison.Ordinal);
        var m = Token.Matches(c >= 0 ? line[..c] : line).ToList();
        if (m.Count < Colunas || !int.TryParse(m[0].Value, out _) || !m[2].Value.StartsWith('"')) return null;
        for (int i = 3; i < Colunas; i++) if (!int.TryParse(m[i].Value, out _)) return null;
        return m;
    }

    /// <summary>Os monstros do Monster.txt (Type 0; os NPCs ficam de fora).</summary>
    public static List<Monstro> Carregar()
    {
        var list = new List<Monstro>();
        foreach (var line in File.ReadLines(Drops.MonsterFile, Enc))
        {
            var t = Tokens(line);
            if (t == null || t[1].Value != "0") continue;
            var v = new int[Colunas];
            for (int i = 0; i < Colunas; i++) if (i != 2) v[i] = int.Parse(t[i].Value);
            list.Add(new Monstro { Index = v[0], Nome = t[2].Value.Trim('"'), Valores = v });
        }
        return list;
    }

    /// <summary>Problema nos valores (fora da faixa, dano mínimo maior que o máximo) ou null.</summary>
    public static string? Problema(Monstro m)
    {
        foreach (var c in Campos)
            if (m.Valor(c) < c.Min || m.Valor(c) > c.Max) return $"{m.Nome}: \"{c.Nome}\" fora da faixa ({c.Min:N0} a {c.Max:N0}).";
        if (m.Valores[6] > m.Valores[7]) return $"{m.Nome}: o dano mínimo é maior que o máximo.";
        if (m.Valores[34] > m.Valores[35]) return $"{m.Nome}: o dano elemental mínimo é maior que o máximo.";
        return null;
    }

    /// <summary>Grava os monstros dados (só as colunas de <see cref="Campos"/>). Devolve (linhas mudadas, backup).</summary>
    public static (int Mudados, string Backup) Salvar(IEnumerable<Monstro> monstros)
    {
        var want = monstros.ToDictionary(m => m.Index);
        foreach (var m in want.Values) if (Problema(m) is { } p) throw new InvalidOperationException(p + " Nada foi gravado.");
        var lines = File.ReadAllText(Drops.MonsterFile, Enc).Split("\r\n");
        int mudados = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var t = Tokens(lines[i]);
            if (t == null || t[1].Value != "0" || !want.TryGetValue(int.Parse(t[0].Value), out var m)) continue;
            var line = lines[i];
            // da direita para a esquerda: as posições das colunas anteriores continuam valendo
            foreach (var c in Campos.OrderByDescending(c => c.Coluna))
                line = Drops.Replace(line, t[c.Coluna], m.Valor(c).ToString());
            if (line != lines[i]) { lines[i] = line; mudados++; }
        }
        if (mudados == 0) return (0, "");
        var backup = Drops.MonsterFile + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(Drops.MonsterFile, backup, overwrite: true);
        File.WriteAllText(Drops.MonsterFile, string.Join("\r\n", lines), Enc);
        return (mudados, backup);
    }
}

/// <summary>
/// Poderes dos monstros (só leitura, issue #49), em Data\Monster\Skill: MonsterSkill.txt (monstro → até 10 pares tipo/unidade),
/// MonsterSkillUnit.txt (alvo, área, intervalo e até 5 efeitos) e MonsterSkillElement.txt (o efeito: tipo, chance, duração).
/// Os nomes dos tipos de efeito seguem a lista do MuEmu (MonsterSkillElement); mudar poderes é mexer nos três arquivos à mão.
/// </summary>
public static class MonsterSkills
{
    static readonly string[] Efeitos =
    {
        "atordoa", "prende (não anda)", "mexe na vida", "mexe na mana", "mexe no AG", "mexe na defesa", "mexe no ataque", "gasta a durabilidade",
        "invoca monstros", "empurra", "mexe na energia", "mexe na força", "mexe na agilidade", "mexe na vitalidade", "tira buffs",
        "resiste a skill", "imune a skill", "teleporta", "dobra a vida", "envenena", "ataque normal", "fúria",
    };

    static string Dir => Path.Combine(ServerControl.ServerRoot, @"Data\Monster\Skill");
    static readonly Regex Token = new(@"""[^""]*""|\S+");

    static Dictionary<int, string[]> Ler(string arquivo)
    {
        var d = new Dictionary<int, string[]>();
        var f = Path.Combine(Dir, arquivo);
        if (!File.Exists(f)) return d;
        foreach (var line in File.ReadLines(f, Encoding.Latin1))
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            var t = Token.Matches(c >= 0 ? line[..c] : line).Select(m => m.Value).ToArray();
            if (t.Length > 2 && int.TryParse(t[0], out var i) && t[1].StartsWith('"')) d.TryAdd(i, t);
        }
        return d;
    }

    /// <summary>Monstro → descrição de cada poder ("unidade 4: alvo 1, área 6, a cada 300 ms; atordoa 50% por 3 s").</summary>
    public static Dictionary<int, List<string>> Todos()
    {
        var skills = Ler("MonsterSkill.txt"); var unidades = Ler("MonsterSkillUnit.txt"); var elementos = Ler("MonsterSkillElement.txt");
        var r = new Dictionary<int, List<string>>();
        foreach (var (monstro, t) in skills)
        {
            var lista = new List<string>();
            for (int k = 2; k + 1 < t.Length; k += 2)
            {
                if (!int.TryParse(t[k + 1], out var u)) continue;   // "*" = vazio
                if (!unidades.TryGetValue(u, out var un) || un.Length < 7) { lista.Add($"unidade {u} (não está no MonsterSkillUnit.txt)"); continue; }
                var efeitos = new List<string>();
                for (int s = 7; s < un.Length; s++)
                {
                    if (!int.TryParse(un[s], out var e)) continue;
                    if (!elementos.TryGetValue(e, out var el) || el.Length < 5 || !int.TryParse(el[2], out var tipo)) { efeitos.Add($"efeito {e}"); continue; }
                    var nome = tipo >= 0 && tipo < Efeitos.Length ? Efeitos[tipo] : $"efeito tipo {tipo}";
                    efeitos.Add($"{nome} ({el[3]}% de chance{(el[4] is "0" or "*" ? "" : $", {el[4]} s")})");
                }
                lista.Add($"unidade {u}: alcance {un[5]}, a cada {un[6]} ms" + (efeitos.Count > 0 ? "; " + string.Join(", ", efeitos) : ""));
            }
            if (lista.Count > 0) r[monstro] = lista;
        }
        return r;
    }
}

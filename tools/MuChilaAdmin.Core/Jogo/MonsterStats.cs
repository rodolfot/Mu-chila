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
/// Poderes dos monstros (issue #49), em Data\Monster\Skill, três arquivos encadeados:
///   MonsterSkill.txt    : monstro → até 10 pares (TipoComportamento, Unidade).
///   MonsterSkillUnit.txt : a unidade (um "golpe"): alvo, área (ScopeValue), intervalo (Delay, ms) e até 5 efeitos (Slot1..5).
///   MonsterSkillElement.txt: o efeito: Type (o que faz), SuccessRate (%), ContinuanceTime (s), e mais colunas preservadas.
/// Grava só o que mudou, coluna a coluna, sem desalinhar (como Drops/MonsterStats); backup .bak-* de cada arquivo tocado.
/// ATENÇÃO: uma unidade pode ser usada por vários monstros, e um efeito por várias unidades — mudar aqui muda em todos que
/// compartilham (o painel avisa). Vale com Reload Monster (o painel deixa para depois da invasão, se houver uma no ar).
/// </summary>
public static class MonsterSkills
{
    /// <summary>Nomes dos tipos de efeito (coluna Type do MonsterSkillElement), pela lista do MuEmu.</summary>
    public static readonly (int Tipo, string Nome)[] TiposEfeito =
    {
        (0, "atordoa"), (1, "prende (não anda)"), (2, "mexe na vida"), (3, "mexe na mana"), (4, "mexe no AG"), (5, "mexe na defesa"),
        (6, "mexe no ataque"), (7, "gasta a durabilidade"), (8, "invoca monstros"), (9, "empurra"), (10, "mexe na energia"),
        (11, "mexe na força"), (12, "mexe na agilidade"), (13, "mexe na vitalidade"), (14, "tira buffs"), (15, "resiste a skill"),
        (16, "imune a skill"), (17, "teleporta"), (18, "dobra a vida"), (19, "envenena"), (20, "ataque normal"), (21, "fúria"),
    };

    public static string NomeEfeito(int tipo) => TiposEfeito.FirstOrDefault(x => x.Tipo == tipo).Nome is { Length: > 0 } n ? n : $"tipo {tipo}";

    // colunas (índice do token na linha): Element 2=Type 3=SuccessRate 4=ContinuanceTime; Unit 5=ScopeValue(área) 6=Delay(ms), efeitos de 7 em diante
    const int ElType = 2, ElChance = 3, ElDur = 4, UnArea = 5, UnDelay = 6, UnEfeito0 = 7;

    public sealed class Efeito { public int Index, Tipo, Chance, Duracao; public Efeito Copia() => (Efeito)MemberwiseClone(); }
    public sealed class Unidade { public int Index, Delay, Area; public List<int> Efeitos = new(); public Unidade Copia() => new() { Index = Index, Delay = Delay, Area = Area, Efeitos = new(Efeitos) }; }
    public sealed record Poder(int Tipo, int Unidade);

    /// <summary>Tudo pronto para a tela: skills, efeitos, unidades, poderes por monstro e quantos compartilham cada um.</summary>
    public sealed class Dados
    {
        public Dictionary<int, string> Skills = new();          // nº da skill → nome (Data\Skill\Skill.txt; 0 = ataque básico)
        public Dictionary<int, Efeito> Efeitos = new();
        public Dictionary<int, Unidade> Unidades = new();
        public Dictionary<int, List<Poder>> PoderesDoMonstro = new();
        public Dictionary<int, int> UnidadeUsadaPor = new();   // unidade → nº de monstros
        public Dictionary<int, int> EfeitoUsadoPor = new();     // efeito → nº de unidades
        public HashSet<int> SkillsDeMonstro = new();            // skills que algum monstro do kit solta (as que o servidor sabe fazer)
        public HashSet<int> MonstrosQueSoltam = new();          // monstros que o GameServer realmente faz soltar skill (têm skill != 0 na AI dele)
    }

    /// <summary>Número da skill → nome, do Data\Skill\Skill.txt (0 = ataque básico, sem skill nomeada).</summary>
    public static Dictionary<int, string> NomesSkills()
    {
        var d = new Dictionary<int, string> { [0] = "ataque básico (sem skill)" };
        var f = Path.Combine(ServerControl.ServerRoot, @"Data\Skill\Skill.txt");
        if (!File.Exists(f)) return d;
        foreach (var line in File.ReadLines(f, Enc))
        {
            var m = Regex.Match(line, @"^\s*(\d+)\s+""([^""]+)""");
            if (m.Success) d.TryAdd(int.Parse(m.Groups[1].Value), m.Groups[2].Value);
        }
        return d;
    }

    public static string NomeSkill(int skill, Dados d) => d.Skills.GetValueOrDefault(skill, $"skill {skill}");

    static string Dir => Path.Combine(ServerControl.ServerRoot, @"Data\Monster\Skill");
    static string Arq(string nome) => Path.Combine(Dir, nome);
    static readonly Regex Token = new(@"""[^""]*""|\S+");
    static readonly Encoding Enc = Encoding.Latin1;

    static List<string[]> LinhasDado(string arquivo)
    {
        var r = new List<string[]>();
        var f = Arq(arquivo);
        if (!File.Exists(f)) return r;
        foreach (var line in File.ReadLines(f, Enc))
        {
            var t = Token.Matches(line).Select(m => m.Value).ToArray();
            if (t.Length > 2 && int.TryParse(t[0], out _) && t[1].StartsWith('"')) r.Add(t);
        }
        return r;
    }

    public static Dados Carregar()
    {
        var d = new Dados { Skills = NomesSkills() };
        foreach (var t in LinhasDado("MonsterSkillElement.txt"))
            if (t.Length > ElDur && int.TryParse(t[0], out var i))
                d.Efeitos.TryAdd(i, new Efeito { Index = i, Tipo = N(t[ElType]), Chance = N(t[ElChance]), Duracao = N(t[ElDur]) });
        foreach (var t in LinhasDado("MonsterSkillUnit.txt"))
            if (t.Length > UnDelay && int.TryParse(t[0], out var i))
            {
                var u = new Unidade { Index = i, Area = N(t[UnArea]), Delay = N(t[UnDelay]) };
                for (int s = UnEfeito0; s < t.Length; s++) if (int.TryParse(t[s], out var e)) u.Efeitos.Add(e);
                d.Unidades.TryAdd(i, u);
            }
        foreach (var t in LinhasDado("MonsterSkill.txt"))
            if (int.TryParse(t[0], out var m))
            {
                var poderes = new List<Poder>();
                for (int k = 2; k + 1 < t.Length; k++) if (int.TryParse(t[k], out var tipo) && int.TryParse(t[k + 1], out var u)) { poderes.Add(new Poder(tipo, u)); k++; }
                d.PoderesDoMonstro[m] = poderes;
            }
        foreach (var (m, poderes) in d.PoderesDoMonstro)
        {
            foreach (var p in poderes.Select(p => p.Unidade).Distinct()) d.UnidadeUsadaPor[p] = d.UnidadeUsadaPor.GetValueOrDefault(p) + 1;
            foreach (var p in poderes) d.SkillsDeMonstro.Add(p.Tipo);
            if (poderes.Any(p => p.Tipo != 0)) d.MonstrosQueSoltam.Add(m);   // só quem tem skill real é que o GS faz soltar
        }
        d.SkillsDeMonstro.Add(0);   // ataque básico sempre disponível
        foreach (var u in d.Unidades.Values) foreach (var e in u.Efeitos.Distinct()) d.EfeitoUsadoPor[e] = d.EfeitoUsadoPor.GetValueOrDefault(e) + 1;
        return d;
    }

    static int N(string s) => int.TryParse(s, out var v) ? v : 0;

    /// <summary>Monstro → descrição de cada poder (nome da skill + o que a unidade faz), para a lista de consulta.</summary>
    public static Dictionary<int, List<string>> Todos()
    {
        var d = Carregar();
        var r = new Dictionary<int, List<string>>();
        foreach (var (monstro, poderes) in d.PoderesDoMonstro)
        {
            var lista = poderes.Select(p => $"{NomeSkill(p.Tipo, d)} — {DescreverUnidade(p.Unidade, d)}").ToList();
            if (lista.Count > 0) r[monstro] = lista;
        }
        return r;
    }

    /// <summary>"alcance 6, a cada 300 ms; atordoa (50%, 3 s)" — o que a unidade (configuração) faz.</summary>
    public static string DescreverUnidade(int unidade, Dados d)
    {
        if (!d.Unidades.TryGetValue(unidade, out var u)) return $"configuração {unidade} (não está no MonsterSkillUnit.txt)";
        var efeitos = u.Efeitos.Select(e => d.Efeitos.TryGetValue(e, out var el)
            ? $"{NomeEfeito(el.Tipo)} ({el.Chance}%{(el.Duracao > 0 ? $", {el.Duracao} s" : "")})" : $"efeito {e}");
        return $"alcance {u.Area}, a cada {u.Delay} ms" + (u.Efeitos.Count > 0 ? "; " + string.Join(", ", efeitos) : "");
    }

    /// <summary>O que o painel manda gravar de UM monstro: efeitos e unidades mudados, e (opcional) a nova lista de poderes do monstro.</summary>
    public sealed class Alteracoes
    {
        public Dictionary<int, (int Tipo, int Chance, int Duracao)> Efeitos = new();
        public Dictionary<int, (int Delay, int Area)> Unidades = new();
        public (int Monstro, string Nome, List<Poder> Poderes)? Poderes;
    }

    /// <summary>Grava as alterações nos três arquivos (só o que mudou, coluna a coluna, backup por arquivo). Devolve o log.</summary>
    public static List<string> Salvar(Alteracoes a)
    {
        var log = new List<string>();
        if (a.Efeitos.Count > 0 && GravarColunas("MonsterSkillElement.txt", a.Efeitos.Keys,
            (t, idx) => a.Efeitos.TryGetValue(idx, out var v) ? new[] { (ElDur, v.Duracao.ToString()), (ElChance, v.Chance.ToString()), (ElType, v.Tipo.ToString()) } : null) is { } b1)
            log.Add($"{a.Efeitos.Count} efeito(s) alterado(s) (backup {Path.GetFileName(b1)}).");
        if (a.Unidades.Count > 0 && GravarColunas("MonsterSkillUnit.txt", a.Unidades.Keys,
            (t, idx) => a.Unidades.TryGetValue(idx, out var v) ? new[] { (UnDelay, v.Delay.ToString()), (UnArea, v.Area.ToString()) } : null) is { } b2)
            log.Add($"{a.Unidades.Count} unidade(s) alterada(s) (backup {Path.GetFileName(b2)}).");
        if (a.Poderes is { } p && GravarPoderes(p.Monstro, p.Nome, p.Poderes) is { } b3)
            log.Add($"poderes de {p.Nome} atualizados (backup {Path.GetFileName(b3)}).");
        if (log.Count == 0) log.Add("Nada mudou nos poderes.");
        return log;
    }

    /// <summary>Edita colunas de linhas (por índice na 1ª coluna) de um arquivo, direita→esquerda para não desalinhar. null = nada mudou.</summary>
    static string? GravarColunas(string arquivo, IEnumerable<int> indices, Func<string[], int, (int Col, string Valor)[]?> campos)
    {
        var alvo = indices.ToHashSet();
        var lines = File.ReadAllText(Arq(arquivo), Enc).Split("\r\n");
        int mudou = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var toks = Token.Matches(lines[i]).ToList();
            if (toks.Count < 3 || !int.TryParse(toks[0].Value, out var idx) || !alvo.Contains(idx) || !toks[1].Value.StartsWith('"')) continue;
            var cols = campos(toks.Select(x => x.Value).ToArray(), idx);
            if (cols == null) continue;
            var line = lines[i];
            foreach (var (col, valor) in cols.OrderByDescending(c => c.Col)) if (col < toks.Count) line = Drops.Replace(line, toks[col], valor);
            if (line != lines[i]) { lines[i] = line; mudou++; }
        }
        if (mudou == 0) return null;
        var backup = Arq(arquivo) + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(Arq(arquivo), backup, overwrite: true);
        File.WriteAllText(Arq(arquivo), string.Join("\r\n", lines), Enc);
        return backup;
    }

    /// <summary>Reescreve (ou cria) a linha do monstro no MonsterSkill.txt com os pares novos (10 pares, resto "*"). null = igual.</summary>
    static string? GravarPoderes(int monstro, string nome, List<Poder> poderes)
    {
        var f = Arq("MonsterSkill.txt");
        var lines = File.ReadAllText(f, Enc).Split("\r\n").ToList();
        var pares = new List<string>();
        foreach (var p in poderes.Take(10)) { pares.Add(p.Tipo.ToString()); pares.Add(p.Unidade.ToString()); }
        while (pares.Count < 20) pares.Add("*");
        int at = lines.FindIndex(l => Token.Matches(l) is { Count: > 2 } t && int.TryParse(t[0].Value, out var m) && m == monstro && t[1].Value.StartsWith('"'));
        string nova;
        if (at >= 0)
        {
            var toks = Token.Matches(lines[at]).ToList();
            var prefixo = lines[at].Substring(0, toks[2].Index);   // índice + nome + espaços até o 1º par
            nova = prefixo + string.Join("\t", pares);
            if (nova == lines[at]) return null;
            lines[at] = nova;
        }
        else
        {
            if (poderes.Count == 0) return null;
            int end = lines.FindIndex(l => l.Trim().Equals("end", StringComparison.OrdinalIgnoreCase));
            nova = $"{monstro,-10}\"{nome}\"{new string(' ', Math.Max(1, 37 - nome.Length))}{string.Join("\t", pares)}";
            lines.Insert(end < 0 ? lines.Count : end, nova);
        }
        var backup = f + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(f, backup, overwrite: true);
        File.WriteAllText(f, string.Join("\r\n", lines), Enc);
        return backup;
    }
}

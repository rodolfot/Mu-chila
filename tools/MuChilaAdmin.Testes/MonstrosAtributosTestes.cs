using System.Text;

namespace MuChilaAdmin.Testes;

/// <summary>Issue #49: editor dos atributos dos monstros (Monster.txt) e os poderes (Data\Monster\Skill), numa pasta temporária.</summary>
public class MonstrosAtributosTestes : IDisposable
{
    readonly PastaTemporaria pasta = new();
    readonly string arquivo;

    const string Monstros =
        "//Index   Type   Name                                 Level   MaxLife   MaxMana   DamageMin   DamageMax   Defense   MagicDefense   AttackRate   DefenseRate   MoveRange   AttackType   AttackRange   ViewRange   MoveSpeed   AttackSpeed   RegenTime   Attribute   ItemRate   MoneyRate   MaxItemLevel   MonsterSkill   Resistance1   Resistance2   Resistance3   Resistance4   Resistance5   Resistance6   Resistance7   ElementalAttribute   ElementalPattern   ElementalDefense   ElementalDamageMin   ElementalDamageMax   ElementalAttackRate   ElementalDefenseRate\r\n" +
        "2         0      \"Budge Dragon\"                       4       60        0         10          13          3         0              18           3             3           0            1             4           400         2000          10          2           120        20          6              0              0             0             0             0             0             0             0             0                    0                  2                  0                    0                    0                     0\r\n" +
        "304       0      \"Witch Queen\"                        95      45000     0         550         600         250       0              450          200           3           1            4             6           400         1600          10          2           150        20          9              0              5             5             5             5             0             0             0             0                    0                  10                 0                    0                    0                     0\r\n" +
        "492       1      \"Moss\"                               2       10        0         10          10          10        1              10           10            0           0            0             0           0           0             0           0           0          0           0              0              0             0             0             0             0             0             0             0                    0                  0                  0                    0                    0                     0\r\n" +
        "end";

    public MonstrosAtributosTestes()
    {
        Configuracao.Definir(new Configuracao { PastaServidor = pasta.Caminho });
        arquivo = pasta.Arquivo(@"Data\Monster\Monster.txt");
        File.WriteAllText(arquivo, Monstros, Encoding.Latin1);
        File.WriteAllText(pasta.Arquivo(@"Data\Monster\Skill\MonsterSkill.txt"),
            "0\r\n304         \"Witch Queen\"                        0       4       0       1       *       *\r\nend", Encoding.Latin1);
        File.WriteAllText(pasta.Arquivo(@"Data\Monster\Skill\MonsterSkillUnit.txt"),
            "0\r\n1         \"Monster Skill Unit\"                 1            0           *            6            300     1       *       *       *       *\r\n" +
            "4         \"Monster Skill Unit\"                 1            0           *            4            500     19      *       *       *       *\r\nend", Encoding.Latin1);
        File.WriteAllText(pasta.Arquivo(@"Data\Monster\Skill\MonsterSkillElement.txt"),
            "0\r\n1         \"Monster Skill Element\"              0      50            3                 *               *                *                0                0\r\n" +
            "19        \"Monster Skill Element\"              19     30            5                 *               *                *                0                0\r\nend", Encoding.Latin1);
        File.WriteAllText(pasta.Arquivo(@"Data\Skill\Skill.txt"),
            "//Index  Name\r\n1\t\"Poison\"\t12\t42\r\n9\t\"Evil Spirit\"\t45\t90\r\n41\t\"Twisting Slash\"\t0\t10\r\nend", Encoding.Latin1);
    }

    public void Dispose() => pasta.Dispose();

    [Fact]
    public void Le_so_os_monstros_com_todas_as_colunas()
    {
        var l = MonsterStats.Carregar();
        Assert.Equal(new[] { 2, 304 }, l.Select(m => m.Index));   // o Moss (Type 1, NPC) fica de fora
        var w = l[1];
        Assert.Equal("Witch Queen", w.Nome);
        Assert.Equal(95, w.Valores[3]);
        Assert.Equal(45000, w.Valores[4]);
        Assert.Equal(5, w.Valores[24]);      // resistência ao gelo
        Assert.Equal(10, w.Valores[33]);     // defesa elemental
    }

    [Fact]
    public void Salvar_muda_so_o_monstro_e_as_colunas_editadas_sem_desalinhar()
    {
        var w = MonsterStats.Carregar().Single(m => m.Index == 304).Copia();
        w.Valores[4] = 1_200_000;   // vida com mais dígitos que a coluna tinha
        w.Valores[18] = 5;          // renascer
        w.Valores[27] = 9;          // fogo
        var (n, backup) = MonsterStats.Salvar(new[] { w });
        Assert.Equal(1, n);
        Assert.True(File.Exists(backup));
        var linhas = File.ReadAllText(arquivo, Encoding.Latin1).Split("\r\n");
        var antes = Monstros.Split("\r\n");
        Assert.Equal(antes[1], linhas[1]);   // Budge Dragon intacto
        Assert.Equal(antes[3], linhas[3]);   // Moss intacto
        Assert.Equal(antes[1].IndexOf(" 0      \"", StringComparison.Ordinal), linhas[2].IndexOf(" 0      \"", StringComparison.Ordinal));
        var depois = MonsterStats.Carregar().Single(m => m.Index == 304);
        Assert.Equal(1_200_000, depois.Valores[4]);
        Assert.Equal(5, depois.Valores[18]);
        Assert.Equal(9, depois.Valores[27]);
        Assert.Equal(550, depois.Valores[6]);   // o resto como estava
        Assert.Equal(150, depois.Valores[20]);  // taxa de drop (não é deste editor)
    }

    [Fact]
    public void Nao_grava_valor_invalido()
    {
        var w = MonsterStats.Carregar().Single(m => m.Index == 304).Copia();
        w.Valores[6] = 700;   // dano mínimo > máximo (600)
        Assert.Throws<InvalidOperationException>(() => MonsterStats.Salvar(new[] { w }));
        Assert.Equal(Monstros, File.ReadAllText(arquivo, Encoding.Latin1));
    }

    [Fact]
    public void Poderes_lidos_com_unidades_efeitos_e_uso()
    {
        var d = MonsterSkills.Carregar();
        Assert.Equal(new[] { new MonsterSkills.Poder(0, 4), new MonsterSkills.Poder(0, 1) }, d.PoderesDoMonstro[304]);
        Assert.Equal(500, d.Unidades[4].Delay); Assert.Equal(4, d.Unidades[4].Area); Assert.Equal(new[] { 19 }, d.Unidades[4].Efeitos);
        Assert.Equal((19, 30, 5), (d.Efeitos[19].Tipo, d.Efeitos[19].Chance, d.Efeitos[19].Duracao));
        Assert.Equal(1, d.UnidadeUsadaPor[4]); Assert.Equal(1, d.EfeitoUsadoPor[19]);
        Assert.Equal("alcance 4, a cada 500 ms; envenena (30%, 5 s)", MonsterSkills.DescreverUnidade(4, d));
    }

    [Fact]
    public void Skills_tem_nome_e_da_para_adicionar_uma_skill_ao_monstro()
    {
        var d = MonsterSkills.Carregar();
        Assert.Equal("Evil Spirit", MonsterSkills.NomeSkill(9, d));
        Assert.Equal("Twisting Slash", MonsterSkills.NomeSkill(41, d));
        Assert.StartsWith("ataque básico", MonsterSkills.NomeSkill(0, d));
        // só as skills que algum monstro solta ficam disponíveis: aqui só a 0 (os pares do fixture são (0,4),(0,1))
        Assert.Contains(0, d.SkillsDeMonstro);
        Assert.DoesNotContain(9, d.SkillsDeMonstro);   // nenhum monstro do kit solta Evil Spirit
        // gravar outra skill ainda é possível no dado (a restrição é no dropdown da tela)
        MonsterSkills.Salvar(new MonsterSkills.Alteracoes { Poderes = (2, "Budge Dragon", new List<MonsterSkills.Poder> { new(9, 1) }) });
        Assert.Equal(new[] { new MonsterSkills.Poder(9, 1) }, MonsterSkills.Carregar().PoderesDoMonstro[2]);
    }

    [Fact]
    public void Salvar_efeito_muda_so_as_colunas_certas()
    {
        MonsterSkills.Salvar(new MonsterSkills.Alteracoes { Efeitos = { [19] = (19, 75, 8) } });   // chance 30→75, duração 5→8
        var d = MonsterSkills.Carregar();
        Assert.Equal((19, 75, 8), (d.Efeitos[19].Tipo, d.Efeitos[19].Chance, d.Efeitos[19].Duracao));
        Assert.Equal((0, 50, 3), (d.Efeitos[1].Tipo, d.Efeitos[1].Chance, d.Efeitos[1].Duracao));   // o outro efeito intacto
    }

    [Fact]
    public void Salvar_unidade_muda_delay_e_area_sem_mexer_nos_efeitos()
    {
        MonsterSkills.Salvar(new MonsterSkills.Alteracoes { Unidades = { [1] = (1000, 10) } });
        var d = MonsterSkills.Carregar();
        Assert.Equal(1000, d.Unidades[1].Delay); Assert.Equal(10, d.Unidades[1].Area); Assert.Equal(new[] { 1 }, d.Unidades[1].Efeitos);
        Assert.Equal(500, d.Unidades[4].Delay);   // outra unidade intacta
    }

    [Fact]
    public void Salvar_poderes_reduz_a_lista_do_monstro()
    {
        MonsterSkills.Salvar(new MonsterSkills.Alteracoes { Poderes = (304, "Witch Queen", new List<MonsterSkills.Poder> { new(0, 1) }) });
        Assert.Equal(new[] { new MonsterSkills.Poder(0, 1) }, MonsterSkills.Carregar().PoderesDoMonstro[304]);
    }

    [Fact]
    public void Salvar_poderes_cria_linha_para_monstro_sem_poder()
    {
        MonsterSkills.Salvar(new MonsterSkills.Alteracoes { Poderes = (2, "Budge Dragon", new List<MonsterSkills.Poder> { new(0, 4) }) });
        var d = MonsterSkills.Carregar();
        Assert.Equal(new[] { new MonsterSkills.Poder(0, 4) }, d.PoderesDoMonstro[2]);
        Assert.Equal(new[] { new MonsterSkills.Poder(0, 4), new MonsterSkills.Poder(0, 1) }, d.PoderesDoMonstro[304]);   // 304 intacto
    }
}

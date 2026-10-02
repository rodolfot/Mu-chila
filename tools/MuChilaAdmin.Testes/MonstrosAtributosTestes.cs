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
    public void Poderes_descritos_pelas_unidades_e_efeitos()
    {
        var p = MonsterSkills.Todos();
        Assert.Single(p);
        Assert.Equal(new[] { "unidade 4: alcance 4, a cada 500 ms; envenena (30% de chance, 5 s)", "unidade 1: alcance 6, a cada 300 ms; atordoa (50% de chance, 3 s)" }, p[304]);
    }
}

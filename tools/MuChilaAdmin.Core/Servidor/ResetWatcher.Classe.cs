using System.IO;

namespace MuChilaAdmin.Core;

/// <summary>
/// Issue #35 (causa achada em 01/10/2026): o GameServer manda ao jogo a 1ª classe como se fosse a 2ª (Dark Knight aparece
/// como "Blade Knight", e o jogo deixa tentar vestir itens de 2ª classe que o servidor recusa).
///
/// O byte de classe que vai para o cliente é [família &lt;&lt; 4] + bits de estágio: 08 = 2ª classe, 0C = 3ª, 0E = 4ª. O GameServer
/// monta os bits com "estágio 1 → 08, 2 → 0C, 3 → 0E, qualquer outro → 08", e o estágio 0 (1ª classe) cai no "qualquer
/// outro". O cliente só traduz esse byte (0x9CBE62 no main.exe) e mostra o nome pelo resultado; ele tem os nomes da 1ª classe.
/// São 4 pontos no Game Server S14.exe (lista de personagens, criação, aparência para os outros jogadores, entrada no jogo),
/// todos com a mesma sequência:
///   cmp r8,2 / jne +7 / mov r32,0Ch / jmp +10h / cmp r8,3 / mov r32,08h / mov r32,0Eh / cmove
/// O vigia troca o 08h desse "mov" por 00h. MG, DL, RF e GL não mudam na prática: o nome deles não depende desse bit e nenhum
/// item deles exige o estágio 2 (só 1, 3 e 4 no Item.txt).
/// Liga com o arquivo vigia-classe-inicial.ligado; para desfazer, apague o arquivo e reinicie o GameServer.
/// </summary>
public static partial class ResetWatcher
{
    public static readonly string InitialClassSwitchPath = Path.Combine(AppContext.BaseDirectory, "vigia-classe-inicial.ligado");
    static bool InitialClassEnabled => File.Exists(InitialClassSwitchPath);

    /// <summary>Posição do imm32 do "mov r32,08h" dentro da sequência de 28 bytes que começa no "cmp r8,2".</summary>
    public const int InitialClassImmOffset = 16;
    const int InitialClassLength = 28;

    /// <summary>
    /// Acha a sequência dos bits de estágio. Devolve a posição (no buffer) do imm32 do "mov r32,08h" e se já está corrigido
    /// (imm 00h). Sequência: 80 F8+r 02 | 75 07 | B8+r 0C000000 | EB 10 | 80 F8+r 03 | B8+r imm32 | B8+r 0E000000 | 0F 44 modrm.
    /// </summary>
    public static List<(int ImmAt, bool Patched)> FindInitialClassSites(ReadOnlySpan<byte> image)
    {
        var sites = new List<(int, bool)>();
        for (int p = 0; p + InitialClassLength <= image.Length; p++)
        {
            var s = image.Slice(p, InitialClassLength);
            if (s[0] != 0x80 || s[1] < 0xF8 || s[2] != 0x02 || s[3] != 0x75 || s[4] != 0x07) continue;
            if (!IsMovImm(s[5..10], 0x0C) || s[10] != 0xEB || s[11] != 0x10) continue;
            if (s[12] != 0x80 || s[13] != s[1] || s[14] != 0x03) continue;
            if (s[15] < 0xB8 || s[15] > 0xBF || !IsMovImm(s[20..25], 0x0E) || s[25] != 0x0F || s[26] != 0x44) continue;
            uint imm = BitConverter.ToUInt32(s[16..20]);
            if (imm != 8 && imm != 0) continue;
            sites.Add((p + InitialClassImmOffset, imm == 0));
        }
        return sites;
    }

    static bool IsMovImm(ReadOnlySpan<byte> s, uint value) => s[0] >= 0xB8 && s[0] <= 0xBF && BitConverter.ToUInt32(s[1..5]) == value;

    /// <summary>Com apply, troca o 08h por 00h nos pontos ainda originais. Devolve (situação, algo mudou).</summary>
    static (string State, bool Changed) InitialClassFix(Target t, byte[] image, bool apply)
    {
        if (!InitialClassEnabled) return ("classe inicial: desligado", false);
        var sites = FindInitialClassSites(image);
        if (sites.Count == 0) return ("classe inicial: trecho não reconhecido (versão diferente?); nada alterado", false);
        var pending = sites.Where(s => !s.Patched).ToList();
        string where = string.Join(" ", sites.Select(s => $"0x{ImageStart + s.ImmAt - InitialClassImmOffset:X}"));
        if (!apply || pending.Count == 0)
            return ($"classe inicial: {sites.Count - pending.Count} de {sites.Count} ponto(s) mandando a 1ª classe certa ({where}; issue #35)", false);
        int ok = 0;
        foreach (var s in pending)
            if (Write(t, (uint)(ImageStart + s.ImmAt), BitConverter.GetBytes(0u))) ok++;
        FlushInstructionCache(t.Handle, IntPtr.Zero, UIntPtr.Zero);
        return ($"classe inicial: corrigida agora em {ok} de {pending.Count} ponto(s); {sites.Count - pending.Count + ok} de {sites.Count} no total ({where}; issue #35)", true);
    }
}

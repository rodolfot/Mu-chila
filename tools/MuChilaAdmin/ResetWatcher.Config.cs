using System.Diagnostics;
using System.Text;

namespace MuChilaAdmin;

/// <summary>
/// Lê na memória dos GameServers o valor que eles estão USANDO de cada opção dos "GameServerInfo - X.dat" (só leitura).
/// Serve para dizer, depois de salvar e dar Reload, se a opção valeu na hora ou só vale ao reiniciar (29/09/2026: a
/// WriteChaosMixLog estava 1 no arquivo e 0 na memória, porque o GameServer só a lê quando liga).
/// Como acha: o GameServer lê cada chave com GetPrivateProfileInt(seção, "Chave", 0, arquivo) e guarda o resultado com
/// "mov [reg+deslocamento], eax" depois da chamada; o deslocamento é somado ao objeto de configuração (gServerInfo,
/// 0xB91B30 nesta versão). Quem chama confere o endereço comparando várias opções com o arquivo antes de confiar nele.
/// </summary>
public static partial class ResetWatcher
{
    const uint ServerInfoBase = 0xB91B30;

    sealed record ConfigImage(byte[] Image, Dictionary<string, int?> Offsets);
    static readonly Dictionary<int, ConfigImage> configImages = new();

    /// <summary>Por GameServer (rótulo = pasta): chave → valor na memória (null = não achou onde a opção fica).</summary>
    public static Dictionary<string, Dictionary<string, int?>> ConfigRead(IReadOnlyCollection<string> keys)
    {
        var result = new Dictionary<string, Dictionary<string, int?>>();
        foreach (var p in Process.GetProcessesByName(ServerControl.GameServerProcess))
        {
            if (!ServerControl.UnderRoot(p)) continue;
            var t = new Target { Process = p };
            try
            {
                t.Handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                if (t.Handle == IntPtr.Zero) continue;
                if (!configImages.TryGetValue(p.Id, out var ci))
                {
                    var image = new byte[ImageEnd - ImageStart];
                    var buf = new byte[1 << 20];
                    for (int off = 0; off < image.Length; off += buf.Length)
                    {
                        int n = Math.Min(buf.Length, image.Length - off);
                        if (ReadProcessMemory(t.Handle, (IntPtr)(ImageStart + off), buf, n, out _)) Buffer.BlockCopy(buf, 0, image, off, n);
                    }
                    configImages[p.Id] = ci = new ConfigImage(image, new());
                }
                var vals = new Dictionary<string, int?>();
                foreach (var k in keys)
                {
                    if (!ci.Offsets.TryGetValue(k, out var off)) ci.Offsets[k] = off = FindConfigOffset(ci.Image, k);
                    vals[k] = off is int o && Read(t, (uint)(ServerInfoBase + o), 4) is { } b ? BitConverter.ToInt32(b) : null;
                }
                result[ServerControl.FolderOf(p)] = vals;
            }
            finally { Close(t); }
        }
        return result;
    }

    static int? FindConfigOffset(byte[] img, string key)
    {
        var pat = Encoding.ASCII.GetBytes(key + "\0");
        var span = img.AsSpan();
        for (int from = 0; from < img.Length;)
        {
            int s = span[from..].IndexOf(pat);
            if (s < 0) break;
            s += from; from = s + 1;
            if (s > 0 && img[s - 1] != 0) continue;   // o texto tem que começar ali (não o fim de outra chave)
            uint addr = (uint)(ImageStart + s);
            var push = new byte[] { 0x68, (byte)addr, (byte)(addr >> 8), (byte)(addr >> 16), (byte)(addr >> 24) };
            for (int pf = 0; pf < img.Length;)
            {
                int p = span[pf..].IndexOf(push);
                if (p < 0) break;
                p += pf; pf = p + 1;
                if (DecodeStore(img, p + 5) is int off) return off;
            }
        }
        return null;
    }

    /// <summary>Depois do push da chave: pula pushes e o "mov" da chave anterior, acha a chamada e o 1º "mov [reg+x], eax" depois dela.</summary>
    static int? DecodeStore(byte[] img, int i)
    {
        bool called = false;
        for (int n = 0; n < 16 && i + 8 < img.Length; n++)
        {
            byte op = img[i];
            if (op == 0x68) { i += 5; continue; }                                            // push imm32
            if (op == 0x6A) { i += 2; continue; }                                            // push imm8
            if (op >= 0x50 && op <= 0x5F) { i += 1; continue; }                              // push/pop reg
            if (op == 0xE8) { called = true; i += 5; continue; }                             // call rel32
            if (op == 0xFF && (img[i + 1] & 0xF8) == 0xD0) { called = true; i += 2; continue; }  // call reg
            if (op == 0xFF && img[i + 1] == 0x15) { called = true; i += 6; continue; }      // call [abs]
            if (op == 0x89 || op == 0x8B)
            {
                byte m = img[i + 1]; int mod = m >> 6, reg = (m >> 3) & 7, rm = m & 7;
                if (rm == 4 && mod != 3) return null;                                         // SIB: fora do padrão
                if (op == 0x89 && called && reg == 0 && mod is 1 or 2)
                    return mod == 1 ? (sbyte)img[i + 2] : BitConverter.ToInt32(img, i + 2);
                i += mod switch { 0 => rm == 5 ? 6 : 2, 1 => 3, 2 => 6, _ => 2 };
                continue;
            }
            return null;
        }
        return null;
    }
}

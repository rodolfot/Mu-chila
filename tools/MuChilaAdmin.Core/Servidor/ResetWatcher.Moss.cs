using System.Diagnostics;

namespace MuChilaAdmin.Core;

/// <summary>
/// Moss Merchant na hora (issue #43, 01/10/2026). Mesmo problema das invasões: o Reload Event relê o MossMerchant.dat mas não
/// recalcula o próximo horário (o GameServer só calcula ao ligar e quando o Moss vai embora), então o disparo do painel não
/// fazia nada. Estrutura no Game Server S14.exe (lida no código desembrulhado; 0x2E59700 nesta versão):
///   +04 estado (0 desligado, 1 esperando, 2 aberto), +08 segundos que faltam, +0C horário-alvo (time_t), +10 tick, +14 NPC.
/// A cada segundo (0x4B9F00): com estado 1 e o horário passado, manda a mensagem 208 ("O mercador Moss apareceu!"), cria o
/// Moss em Elbeland e abre a loja por MossMerchantEventTime minutos; no fim, mensagem 209, apaga o NPC e calcula o próximo.
/// O endereço sai do código pelos bytes: "cmp dword [faltam],0 / jg / push 0 / push 0D0h" e, logo depois, "mov dword [estado],2".
/// </summary>
public static partial class ResetWatcher
{
    static readonly Dictionary<int, uint?> mossBase = new();

    static uint? FindMossBase(Target t)
    {
        if (mossBase.TryGetValue(t.Process.Id, out var cached)) return cached;
        var img = new byte[ImageEnd - ImageStart];
        var buf = new byte[1 << 20];
        for (int off = 0; off < img.Length; off += buf.Length)
        {
            int n = Math.Min(buf.Length, img.Length - off);
            if (ReadProcessMemory(t.Handle, (IntPtr)(ImageStart + off), buf, n, out _)) Buffer.BlockCopy(buf, 0, img, off, n);
        }
        var found = FindMossBase(img);
        if (found != null) mossBase[t.Process.Id] = found;
        return found;
    }

    /// <summary>Endereço do objeto do Moss achado no código (imagem a partir de 0x401000), ou null.</summary>
    public static uint? FindMossBase(ReadOnlySpan<byte> img)
    {
        byte[] msg = { 0x6A, 0x00, 0x68, 0xD0, 0x00, 0x00, 0x00 };   // push 0 / push 0D0h (mensagem 208)
        for (int i = img.IndexOf(msg); i >= 0;)
        {
            // antes: cmp dword ptr [faltam],0 (83 3D imm32 00) / jg rel8 (7F xx)
            if (i >= 9 && img[i - 9] == 0x83 && img[i - 8] == 0x3D && img[i - 3] == 0x00 && img[i - 2] == 0x7F)
            {
                uint remain = BitConverter.ToUInt32(img.Slice(i - 7, 4));
                // depois (até 0x40 bytes): mov dword ptr [estado],2 (C7 05 imm32 02000000), com estado = faltam - 4
                var after = img.Slice(i, Math.Min(0x40, img.Length - i));
                for (int j = 0; j + 10 <= after.Length; j++)
                    if (after[j] == 0xC7 && after[j + 1] == 0x05 && BitConverter.ToUInt32(after.Slice(j + 2, 4)) == remain - 4 &&
                        BitConverter.ToUInt32(after.Slice(j + 6, 4)) == 2)
                        return remain - 8;
            }
            int next = img[(i + 1)..].IndexOf(msg);
            i = next < 0 ? -1 : i + 1 + next;
        }
        return null;
    }

    public sealed record MossState(string Server, int State, DateTime Target);

    /// <summary>Estado do Moss em cada GameServer (null se a estrutura não foi achada).</summary>
    public static List<MossState?> MossRead() => MossDo(null, null);

    /// <summary>Faz o Moss aparecer em "when" em todos os GameServers (só onde ele está esperando). Devolve o log.</summary>
    public static List<string> MossStartAt(DateTime when)
    {
        var log = new List<string>();
        MossDo(when, log);
        return log;
    }

    static List<MossState?> MossDo(DateTime? when, List<string>? log)
    {
        var result = new List<MossState?>();
        foreach (var p in Process.GetProcessesByName(ServerControl.GameServerProcess))
        {
            if (!ServerControl.UnderRoot(p)) continue;
            var t = new Target { Process = p };
            var label = ServerControl.FolderOf(p);
            try
            {
                t.Handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION | (when != null ? PROCESS_VM_WRITE | PROCESS_VM_OPERATION : 0), false, p.Id);
                if (t.Handle == IntPtr.Zero) { log?.Add($"{label}: sem acesso à memória"); result.Add(null); continue; }
                var b = FindMossBase(t);
                var d = b == null ? null : Read(t, b.Value, 16);
                int state = d == null ? -1 : BitConverter.ToInt32(d, 4);
                if (d == null || state is < 0 or > 2) { log?.Add($"{label}: Moss Merchant não encontrado na memória (versão desconhecida); nada foi mudado"); result.Add(null); continue; }
                var target = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt32(d, 12)).LocalDateTime;
                result.Add(new MossState(label, state, target));
                if (when == null) continue;
                if (state == 2) { log!.Add($"{label}: o Moss já está em Elbeland (vai embora {target:HH:mm})"); continue; }
                if (state != 1) { log!.Add($"{label}: Moss Merchant desligado neste servidor (MossMerchantEvent = 0 ou sem agenda)"); continue; }
                int alvo = (int)new DateTimeOffset(when.Value).ToUnixTimeSeconds();
                log!.Add(Write(t, b!.Value + 12, BitConverter.GetBytes(alvo))
                    ? $"{label}: Moss marcado para {when:HH:mm:ss} (antes: {target:dd/MM HH:mm})"
                    : $"{label}: falha ao gravar na memória");
            }
            finally { Close(t); }
        }
        if (log != null && log.Count == 0) log.Add("Nenhum GameServer rodando");
        return result;
    }
}

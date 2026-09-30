using System.Diagnostics;

namespace MuChilaAdmin;

/// <summary>
/// Disparar invasão na hora (29/09/2026). O Reload Event relê o InvasionManager.dat mas NÃO recalcula o próximo horário de
/// cada invasão: o GameServer só calcula o horário quando liga e quando a rodada anterior termina (CheckSync). Por isso o
/// disparo do painel (linha avulsa no arquivo + Reload) nunca começava: a Páscoa continuava marcada para as 02:15 do dia
/// seguinte. Conferido no servidor de testes: linha nova + Reload = nada; horário-alvo gravado na memória = começa na hora.
/// Estrutura (igual ao INVASION_INFO do MuEmu), 30 entradas de 0x900 bytes; o Load zera a parte de configuração começando
/// em "base" (RespawnMessage, DespawnMessage, BossIndex, BossMessage, InvasionTime, listas) e o estado da invasão k fica em
/// base + k*0x900 - 0x7E4: Index, State (0 desligada, 1 esperando, 2 acontecendo), RemainTime, TargetTime (time_t), TickCount,
/// e 500 índices de monstros. Com State 1, o GameServer começa quando time(0) passa de TargetTime.
/// </summary>
public static partial class ResetWatcher
{
    const int InvasionStride = 0x900, InvasionStateBack = 0x7E4, InvasionMax = 30;
    static readonly Dictionary<int, uint?> invasionBase = new();

    /// <summary>No Load do InvasionManager: "mov edx, base+4 ... add edx, 900h / cmp edx, fim".</summary>
    static uint? FindInvasionBase(Target t)
    {
        if (invasionBase.TryGetValue(t.Process.Id, out var cached)) return cached;
        var img = new byte[ImageEnd - ImageStart];
        var buf = new byte[1 << 20];
        for (int off = 0; off < img.Length; off += buf.Length)
        {
            int n = Math.Min(buf.Length, img.Length - off);
            if (ReadProcessMemory(t.Handle, (IntPtr)(ImageStart + off), buf, n, out _)) Buffer.BlockCopy(buf, 0, img, off, n);
        }
        byte[] pat = { 0x81, 0xC2, 0x00, 0x09, 0x00, 0x00, 0x81, 0xFA };
        uint? found = null;
        for (int i = img.AsSpan().IndexOf(pat); i >= 0 && found == null;)
        {
            for (int j = i - 1; j > i - 0x80 && j > 0; j--)
                if (img[j] == 0xBA)
                {
                    uint b = BitConverter.ToUInt32(img, j + 1) - 4;
                    uint end = BitConverter.ToUInt32(img, i + 8);
                    if ((end - (b + 4)) == InvasionStride * InvasionMax) { found = b; break; }
                }
            int next = img.AsSpan(i + 1).IndexOf(pat);
            i = next < 0 ? -1 : i + 1 + next;
        }
        if (found != null) invasionBase[t.Process.Id] = found;
        return found;
    }

    public sealed record InvasionState(string Server, int State, DateTime Target);

    /// <summary>Estado da invasão "index" em cada GameServer (null se a estrutura não foi achada/conferida).</summary>
    public static List<InvasionState?> InvasionRead(int index) => InvasionDo(index, null, null);

    /// <summary>
    /// Faz a invasão "index" começar em "when" em todos os GameServers (só onde ela está esperando). Devolve o log.
    /// </summary>
    public static List<string> InvasionStartAt(int index, DateTime when)
    {
        var log = new List<string>();
        InvasionDo(index, when, log);
        return log;
    }

    static List<InvasionState?> InvasionDo(int index, DateTime? when, List<string>? log)
    {
        var result = new List<InvasionState?>();
        foreach (var p in Process.GetProcessesByName(ServerControl.GameServerProcess))
        {
            if (!ServerControl.UnderRoot(p)) continue;
            var t = new Target { Process = p };
            var label = ServerControl.FolderOf(p);
            try
            {
                t.Handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION | (when != null ? PROCESS_VM_WRITE | PROCESS_VM_OPERATION : 0), false, p.Id);
                if (t.Handle == IntPtr.Zero) { log?.Add($"{label}: sem acesso à memória"); result.Add(null); continue; }
                var b = FindInvasionBase(t);
                if (b == null || index < 0 || index >= InvasionMax) { log?.Add($"{label}: gerenciador de invasões não encontrado (versão desconhecida)"); result.Add(null); continue; }
                uint s = (uint)(b.Value + index * InvasionStride - InvasionStateBack);
                var d = Read(t, s, 16);
                if (d == null || BitConverter.ToInt32(d, 0) != index) { log?.Add($"{label}: invasão {index} não conferiu na memória; nada foi mudado"); result.Add(null); continue; }
                int state = BitConverter.ToInt32(d, 4);
                var target = DateTimeOffset.FromUnixTimeSeconds(BitConverter.ToInt32(d, 12)).LocalDateTime;
                result.Add(new InvasionState(label, state, target));
                if (when == null) continue;
                if (state == 2) { log!.Add($"{label}: essa invasão já está acontecendo (termina {target:HH:mm})"); continue; }
                if (state != 1) { log!.Add($"{label}: invasões desligadas neste servidor"); continue; }
                int alvo = (int)new DateTimeOffset(when.Value).ToUnixTimeSeconds();
                log!.Add(Write(t, s + 12, BitConverter.GetBytes(alvo))
                    ? $"{label}: invasão marcada para {when:HH:mm:ss} (antes: {target:dd/MM HH:mm})"
                    : $"{label}: falha ao gravar na memória");
            }
            finally { Close(t); }
        }
        if (log != null && log.Count == 0) log.Add("Nenhum GameServer rodando");
        return result;
    }
}

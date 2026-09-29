using System.Diagnostics;
using System.Text;

namespace MuChilaAdmin;

/// <summary>
/// Issue #31 (29/09/2026): o aviso "enviar agora" do painel não aparecia. Causa comprovada na memória: a rotina de avisos
/// do GameServer (0x4C1ED0 no GS) faz "se (agora - último_envio &lt; intervalo[próximo]) espera; senão envia aviso[próximo] e
/// próximo++", e o Reload Util NÃO reinicia "próximo" nem o relógio. O aviso posto no topo do Notice.txt ficava esperando o
/// intervalo de outro aviso (ex.: 600 s) e o vigia o tirava do arquivo antes da vez dele. Com a lista menor, "próximo" ainda
/// podia apontar para uma entrada velha (ex.: "Bônus ativo" de um bônus que já acabou).
/// Correção: depois de mexer no Notice.txt e dar Reload Util, grava na memória de cada GameServer/Castle Siege
/// "próximo = 0" e, para mandar já, "último envio" bem no passado (o aviso 0 sai no segundo seguinte); senão "último envio = agora".
/// Endereços achados pelos bytes da rotina (servem para o GameServer e para o Castle Siege, que é outra versão).
/// </summary>
public static partial class ResetWatcher
{
    sealed record NoticeAddr(uint Count, uint Index, uint Time, uint Array);
    const int NoticeEntry = 0x9C, NoticeMessage = 128;
    static readonly Dictionary<int, NoticeAddr?> noticeAddr = new();

    /// <summary>cmp [count],0 / push esi / je / imul esi,[index],9Ch / add esi,array / call [GetTickCount] / sub eax,[time] / cmp eax,[esi+98h]</summary>
    static NoticeAddr? FindNoticeRoutine(byte[] img)
    {
        for (int i = 0; i + 48 < img.Length; i++)
        {
            if (img[i] != 0x83 || img[i + 1] != 0x3D || img[i + 6] != 0x00 || img[i + 7] != 0x56 || img[i + 8] != 0x0F || img[i + 9] != 0x84) continue;
            if (img[i + 14] != 0x69 || img[i + 15] != 0x35 || BitConverter.ToUInt32(img, i + 20) != NoticeEntry || img[i + 24] != 0x81 || img[i + 25] != 0xC6) continue;
            if (img[i + 30] != 0xFF || img[i + 31] != 0x15 || img[i + 36] != 0x2B || img[i + 37] != 0x05) continue;
            if (img[i + 42] != 0x3B || img[i + 43] != 0x86 || BitConverter.ToUInt32(img, i + 44) != 0x98) continue;
            var a = new NoticeAddr(BitConverter.ToUInt32(img, i + 2), BitConverter.ToUInt32(img, i + 16), BitConverter.ToUInt32(img, i + 38), BitConverter.ToUInt32(img, i + 26));
            if (a.Index == a.Count + 4 && a.Time == a.Count + 8) return a;
        }
        return null;
    }

    static NoticeAddr? NoticeAddress(Target t)
    {
        if (noticeAddr.TryGetValue(t.Process.Id, out var a)) return a;
        var image = new byte[ImageEnd - ImageStart];
        var buf = new byte[1 << 20];
        for (int off = 0; off < image.Length; off += buf.Length)
        {
            int n = Math.Min(buf.Length, image.Length - off);
            if (ReadProcessMemory(t.Handle, (IntPtr)(ImageStart + off), buf, n, out _)) Buffer.BlockCopy(buf, 0, image, off, n);
        }
        a = FindNoticeRoutine(image);
        if (a != null) noticeAddr[t.Process.Id] = a;   // só guarda o achado: GameServer recém-aberto pode ainda estar descompactando
        return a;
    }

    /// <summary>
    /// Reinicia o rodízio de avisos em todos os GameServers e no Castle Siege (chamar DEPOIS do Reload Util). sendFirstNow =
    /// manda o aviso da 1ª linha já (o "enviar agora" fica no topo). firstMessage: espera até 5 s o servidor ter lido o arquivo
    /// novo (a 1ª linha na memória igual a ela) antes de mandar, para não repetir o aviso antigo.
    /// </summary>
    public static List<string> NoticeRestart(bool sendFirstNow, string? firstMessage = null)
    {
        var log = new List<string>();
        byte[]? want = firstMessage == null ? null : Encoding.Latin1.GetBytes(firstMessage);
        foreach (var name in new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess })
            foreach (var p in Process.GetProcessesByName(name))
            {
                if (!ServerControl.UnderRoot(p)) continue;
                var t = new Target { Process = p };
                try
                {
                    t.Handle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_LIMITED_INFORMATION, false, p.Id);
                    var label = $"{p.ProcessName} ({ServerControl.FolderOf(p)})";
                    if (t.Handle == IntPtr.Zero) { log.Add($"{label}: sem acesso à memória"); continue; }
                    var a = NoticeAddress(t);
                    if (a == null) { log.Add($"{label}: rotina de avisos não encontrada (versão desconhecida)"); continue; }
                    if (want != null)
                        for (int i = 0; i < 25; i++)
                        {
                            var cur = Read(t, a.Array, NoticeMessage);
                            int n = Math.Min(want.Length, NoticeMessage - 1);
                            if (cur != null && cur.AsSpan(0, n).SequenceEqual(want.AsSpan(0, n)) && cur[n] == 0) break;
                            Thread.Sleep(200);
                        }
                    uint agora = (uint)Environment.TickCount;
                    uint tempo = sendFirstNow ? unchecked(agora - 3_600_000u) : agora;
                    bool ok = Write(t, a.Index, BitConverter.GetBytes(0)) && Write(t, a.Time, BitConverter.GetBytes(tempo));
                    int total = BitConverter.ToInt32(Read(t, a.Count, 4) ?? new byte[4]);
                    log.Add(ok ? $"{label}: rodízio de avisos reiniciado ({total} aviso(s){(sendFirstNow ? ", o 1º sai agora" : "")})"
                               : $"{label}: falha ao gravar na memória");
                }
                finally { Close(t); }
            }
        return log;
    }
}

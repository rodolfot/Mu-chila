using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MuChilaAdmin;

/// <summary>
/// Vigia dos GameServers (processo próprio: "MuChilaAdmin.exe --vigia-reset"). Faz duas coisas:
///  1. Issue #12: desliga a recusa da checagem anti-hack de ataques do MuDevs assim que acha um GameServer/Castle Siege
///     (inclusive depois de reiniciado) — ver AttackCheckSignatures.
///  2. Issue #7 (só com o arquivo vigia-reset-selecao.ligado): depois do /reset, leva o jogador para a seleção de personagem.
///
/// Sobre o /reset: o /reset do MuDevs não manda o nível novo para a janela de atributos (C), que continua
/// mostrando 400 até o personagem subir de nível ou reentrar. Quando um personagem cai de nível (de 50 ou mais para 10
/// ou menos, o que só o /reset faz), o vigia leva o jogador para a seleção de personagem, como a opção "Trocar
/// personagem" do menu do jogo.
///
/// Como: a rotina do servidor que atende essa opção (0x4C6420 no GameServer, 0x4DD520 no Castle Siege) só grava três
/// campos no objeto do jogador: +0x0B tipo (1 = seleção de personagem), +0x0C = 1 e +0x0A contagem = 6. A cada segundo
/// o servidor desconta a contagem e, ao chegar em 1, salva o personagem e manda o cliente para a seleção. O vigia grava
/// os mesmos campos pela memória. Antes de gravar, acha essa rotina no executável pelos bytes; se o GameServer for de
/// outra versão (bytes não encontrados), não grava nada.
/// </summary>
public static class ResetWatcher
{
    const string MutexName = @"Local\MuChilaVigiaReset";
    public static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "vigia.log");

    /// <summary>O envio à seleção de personagem depois do /reset (issue #7) só liga depois de testado no jogo: basta criar este arquivo.</summary>
    public static readonly string ResetSwitchPath = Path.Combine(AppContext.BaseDirectory, "vigia-reset-selecao.ligado");
    static bool ResetToSelectEnabled => File.Exists(ResetSwitchPath);

    /// <summary>
    /// Issue #12 (causa comprovada em 25/09/2026): a checagem anti-hack de ataques do MuDevs (código protegido, 0x40ED40 no
    /// GameServer, 0x40ED60 no Castle Siege) passa a recusar todos os ataques de um jogador depois de usar skills evoluídas.
    /// As 6 rotinas de ataque fazem "call checagem; test al,al; je descartar". O vigia troca o "je" por NOPs: a checagem
    /// continua rodando, mas não descarta mais o ataque. Bytes depois do call em cada rotina (o que identifica os 6 pontos):
    /// </summary>
    static readonly string[] AttackCheckSignatures =
        { "84C074538B4D0857", "84C00F84D7000000", "84C0742333C08986", "84C0743B33C0B9", "84C0745B668B4708", "84C074628B4C2410" };

    // mov byte [esi+0Ah],6 / mov [esi+0Bh],al / mov dword [esi+0Ch],1
    static readonly byte[] CloseSetBytes = Convert.FromHexString("C6460A0688460BC7460C01000000");
    // mov esi,[edi*4+TABELA] no começo da mesma rotina, seguido de cmp byte [esi+0Ah],0
    static readonly byte[] TableLoad = { 0x8B, 0x34, 0xBD };
    static readonly byte[] TableCheck = { 0x80, 0x7E, 0x0A, 0x00 };
    const int ImageStart = 0x401000, ImageEnd = 0x2F6F000;

    const int FirstUser = 11000, UserSlots = 1000;
    const int OffIndex = 0x00, OffConnected = 0x04, OffCloseCount = 0x0A, OffCloseType = 0x0B, OffEnableDel = 0x0C;
    const int OffName = 0x62, OffLevel = 0x8C, ObjHead = 0x90;
    const int Playing = 3, CharacterSelect = 1;

    sealed class Target
    {
        public required Process Process;
        public IntPtr Handle;
        public uint Table;
        public DateTime NextTry;
        public bool ApplyAttackFix;          // só o laço do vigia aplica; sondagem e botão do painel só leem
        public string AttackFixState = "checagem de ataques ainda não verificada";
        public readonly Dictionary<int, (string Name, int Level)> Seen = new();
    }

    public static bool IsRunning()
    {
        if (!Mutex.TryOpenExisting(MutexName, out var m)) return false;
        m.Dispose();
        return true;
    }

    /// <summary>Laço do vigia ("MuChilaAdmin.exe --vigia-reset"). Uma instância só; 2 = já estava rodando.</summary>
    public static int Run()
    {
        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first) return 2;
        Log("vigia iniciado");
        var targets = new Dictionary<int, Target>();
        while (true)
        {
            try
            {
                foreach (var name in new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess })
                    foreach (var p in Process.GetProcessesByName(name))
                        if (!targets.ContainsKey(p.Id)) targets[p.Id] = new Target { Process = p, ApplyAttackFix = true };

                foreach (var (pid, t) in targets.ToList())
                {
                    if (t.Process.HasExited) { Close(t); targets.Remove(pid); continue; }
                    if (t.Table == 0 && DateTime.Now >= t.NextTry) Attach(t);
                    if (t.Table != 0) Poll(t);
                }
            }
            catch (Exception ex) { Log("erro: " + ex.Message); }
            Thread.Sleep(1000);
        }
    }

    /// <summary>Leva um personagem online para a seleção de personagem (teste do vigia e botão do painel).</summary>
    public static string SendToCharacterSelect(string character)
    {
        foreach (var name in new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess })
            foreach (var p in Process.GetProcessesByName(name))
            {
                var t = new Target { Process = p };
                try
                {
                    Attach(t);
                    if (t.Table == 0) continue;
                    foreach (var (idx, obj) in Players(t))
                        if (string.Equals(ReadName(obj), character, StringComparison.OrdinalIgnoreCase))
                            return Trigger(t, idx, character, "pedido pelo painel");
                }
                finally { Close(t); }
            }
        return $"{character} não está jogando em nenhum GameServer (ou o GameServer não é da versão conhecida).";
    }

    /// <summary>Só leitura: o que o vigia enxerga agora (rotina, tabela e jogadores com nível e contagem de saída).</summary>
    public static string Probe()
    {
        var sb = new StringBuilder();
        foreach (var name in new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess })
            foreach (var p in Process.GetProcessesByName(name))
            {
                var t = new Target { Process = p };
                try
                {
                    Attach(t);
                    sb.AppendLine(t.Table == 0 ? $"{name} (pid {p.Id}): versão não reconhecida" : $"{name} (pid {p.Id}): tabela 0x{t.Table:X}");
                    sb.AppendLine($"   {t.AttackFixState}");
                    sb.AppendLine($"   /reset leva à seleção de personagem: {(ResetToSelectEnabled ? "ligado" : "desligado (falta testar a #7)")}");
                    if (t.Table == 0) continue;
                    foreach (var (idx, obj) in Players(t))
                        sb.AppendLine($"   {idx}: {ReadName(obj)} nível {BitConverter.ToInt16(obj, OffLevel)} " +
                                      $"contagem {(sbyte)obj[OffCloseCount]} tipo {(sbyte)obj[OffCloseType]}");
                }
                finally { Close(t); }
            }
        return sb.Length == 0 ? "nenhum GameServer rodando" : sb.ToString();
    }

    static void Attach(Target t)
    {
        t.NextTry = DateTime.Now.AddSeconds(30);   // GameServer recém-aberto ainda pode estar descompactando o código
        if (t.Handle == IntPtr.Zero)
            t.Handle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_LIMITED_INFORMATION, false, t.Process.Id);
        if (t.Handle == IntPtr.Zero) return;

        var image = new byte[ImageEnd - ImageStart];
        var buf = new byte[1 << 20];
        for (int off = 0; off < image.Length; off += buf.Length)
        {
            int n = Math.Min(buf.Length, image.Length - off);
            if (ReadProcessMemory(t.Handle, (IntPtr)(ImageStart + off), buf, n, out _)) Buffer.BlockCopy(buf, 0, image, off, n);
        }
        var (state, changed) = AttackCheckFix(t, image, t.ApplyAttackFix);
        t.AttackFixState = state;
        if (changed) Log($"{t.Process.ProcessName} (pid {t.Process.Id}): {state}");

        var hits = new List<int>();
        for (int i = image.AsSpan().IndexOf(CloseSetBytes); i >= 0; )
        {
            hits.Add(i);
            int next = image.AsSpan(i + 1).IndexOf(CloseSetBytes);
            i = next < 0 ? -1 : i + 1 + next;
        }
        if (hits.Count != 1) { LogOnce(t, $"{t.Process.ProcessName}: rotina de troca de personagem não encontrada ({hits.Count} ocorrências); vigia desligado para ele"); return; }

        int at = hits[0];
        int from = Math.Max(0, at - 0x120);
        int load = image.AsSpan(from, at - from).LastIndexOf(TableLoad);
        if (load < 0 || !image.AsSpan(from + load + 7, 4).SequenceEqual(TableCheck))
        { LogOnce(t, $"{t.Process.ProcessName}: tabela de jogadores não encontrada; vigia desligado para ele"); return; }
        t.Table = BitConverter.ToUInt32(image, from + load + 3);
        Log($"{t.Process.ProcessName} (pid {t.Process.Id}): vigiando (rotina 0x{ImageStart + at:X}, tabela 0x{t.Table:X})");
    }

    /// <summary>
    /// Acha as 6 chamadas à checagem de ataques (mesmo alvo, cada uma seguida de uma das assinaturas) e, com apply, troca
    /// o desvio por NOPs nas que ainda estão originais. Só age se achar exatamente as 6. Devolve (situação, algo mudou).
    /// </summary>
    static (string State, bool Changed) AttackCheckFix(Target t, byte[] image, bool apply)
    {
        var signatures = AttackCheckSignatures.Select(Convert.FromHexString).ToArray();
        var byTarget = new Dictionary<long, List<(int At, bool Done, bool Long)>>();
        int end = Math.Min(image.Length, 0x200000) - 13;   // as rotinas de ataque ficam no começo do código (0x401000–0x600000)
        for (int i = 0; i < end; i++)
        {
            if (image[i] != 0xE8 || image[i + 5] != 0x84 || image[i + 6] != 0xC0) continue;
            var after = image.AsSpan(i + 5, 8);
            bool original = false;
            foreach (var s in signatures) if (after.StartsWith(s)) { original = true; break; }
            bool done = after[2] == 0x90 && after[3] == 0x90;
            if (!original && !done) continue;
            bool isLong = after[2] == 0x0F || (done && after[4] == 0x90 && after[5] == 0x90 && after[6] == 0x90 && after[7] == 0x90);
            long target = ImageStart + i + 5 + BitConverter.ToInt32(image, i + 1);
            if (!byTarget.TryGetValue(target, out var list)) byTarget[target] = list = new();
            list.Add((ImageStart + i, done, isLong));
        }
        var found = byTarget.Where(kv => kv.Value.Count == 6).ToList();
        if (found.Count != 1) return ("checagem de ataques não reconhecida (versão diferente?); nada alterado", false);
        var (check, sites) = (found[0].Key, found[0].Value);
        int pending = sites.Count(s => !s.Done);
        if (!apply || pending == 0) return ($"checagem de ataques 0x{check:X}: {6 - pending} de 6 pontos com a recusa desligada (issue #12)", false);
        int ok = 0;
        foreach (var s in sites.Where(s => !s.Done))
            if (Write(t, (uint)(s.At + 7), Enumerable.Repeat((byte)0x90, s.Long ? 6 : 2).ToArray())) ok++;
        FlushInstructionCache(t.Handle, IntPtr.Zero, UIntPtr.Zero);
        return ($"checagem de ataques 0x{check:X}: recusa desligada agora em {ok} de {pending} ponto(s); {6 - pending + ok} de 6 no total (issue #12)", true);
    }

    static readonly HashSet<string> logged = new();
    static void LogOnce(Target t, string msg) { if (logged.Add($"{t.Process.Id}:{msg}")) Log(msg); }

    /// <summary>Objetos de jogador conectados e jogando: (índice, primeiros bytes do objeto).</summary>
    static IEnumerable<(int Index, byte[] Obj)> Players(Target t)
    {
        var table = Read(t, t.Table + (uint)FirstUser * 4, UserSlots * 4);
        if (table == null) yield break;
        var ptrs = new uint[UserSlots];
        for (int i = 0; i < UserSlots; i++) ptrs[i] = BitConverter.ToUInt32(table, i * 4);
        // posições livres apontam todas para o mesmo objeto vazio: ponteiros repetidos são ignorados
        var repeated = ptrs.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        for (int i = 0; i < UserSlots; i++)
        {
            if (ptrs[i] == 0 || repeated.Contains(ptrs[i])) continue;
            var obj = Read(t, ptrs[i], ObjHead);
            if (obj == null || BitConverter.ToInt32(obj, OffIndex) != FirstUser + i) continue;
            if (BitConverter.ToInt32(obj, OffConnected) != Playing) continue;
            yield return (FirstUser + i, obj);
        }
    }

    static void Poll(Target t)
    {
        var alive = new HashSet<int>();
        foreach (var (idx, obj) in Players(t))
        {
            var name = ReadName(obj);
            if (name == null) continue;
            alive.Add(idx);
            int level = BitConverter.ToInt16(obj, OffLevel);
            if (ResetToSelectEnabled && t.Seen.TryGetValue(idx, out var prev) && prev.Name == name && prev.Level >= 50 && level <= 10)
                Log(Trigger(t, idx, name, $"/reset detectado (nível {prev.Level} → {level})"));
            t.Seen[idx] = (name, level);
        }
        foreach (var gone in t.Seen.Keys.Where(k => !alive.Contains(k)).ToList()) t.Seen.Remove(gone);
    }

    static string Trigger(Target t, int idx, string name, string why)
    {
        uint ptr = BitConverter.ToUInt32(Read(t, t.Table + (uint)idx * 4, 4) ?? new byte[4]);
        var head = ptr == 0 ? null : Read(t, ptr, ObjHead);
        if (head == null || BitConverter.ToInt32(head, OffIndex) != idx || ReadName(head) != name)
            return $"{name}: {why}, mas o personagem saiu antes; nada feito";
        if ((sbyte)head[OffCloseCount] > 0) return $"{name}: {why}; já estava saindo";
        // o tipo primeiro e a contagem por último: o servidor só olha o tipo quando a contagem é maior que zero
        bool ok = Write(t, ptr + OffCloseType, new byte[] { CharacterSelect })
               && Write(t, ptr + OffEnableDel, BitConverter.GetBytes(1))
               && Write(t, ptr + OffCloseCount, new byte[] { 6 });
        return ok ? $"{name} ({t.Process.ProcessName}): {why}; indo para a seleção de personagem em 5 s"
                  : $"{name}: {why}; falha ao gravar na memória do {t.Process.ProcessName} (erro {Marshal.GetLastWin32Error()})";
    }

    static string? ReadName(byte[] obj)
    {
        int end = Array.IndexOf(obj, (byte)0, OffName, 11);
        if (end <= OffName) return null;
        var s = Encoding.ASCII.GetString(obj, OffName, end - OffName);
        return s.All(c => c > ' ' && c < 127) ? s : null;
    }

    static byte[]? Read(Target t, uint addr, int size)
    {
        var buf = new byte[size];
        return ReadProcessMemory(t.Handle, (IntPtr)addr, buf, size, out var got) && (int)got == size ? buf : null;
    }

    static bool Write(Target t, uint addr, byte[] data) =>
        WriteProcessMemory(t.Handle, (IntPtr)addr, data, data.Length, out var put) && (int)put == data.Length;

    static void Close(Target t) { if (t.Handle != IntPtr.Zero) CloseHandle(t.Handle); t.Handle = IntPtr.Zero; }

    static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:dd/MM HH:mm:ss} {msg}{Environment.NewLine}"); } catch { }
    }

    const uint PROCESS_VM_OPERATION = 0x0008, PROCESS_VM_READ = 0x0010, PROCESS_VM_WRITE = 0x0020, PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, [Out] byte[] buf, int size, out IntPtr read);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out IntPtr written);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FlushInstructionCache(IntPtr h, IntPtr addr, UIntPtr size);
}

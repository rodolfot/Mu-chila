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
///
/// Também: resets pedidos pelo site com a conta online, bônus por tempo, avisos, Passe dos Mapas e Magic Backpack
/// (os dois últimos em ResetWatcher.Passe.cs).
/// </summary>
public static partial class ResetWatcher
{
    const string MutexName = @"Local\MuChilaVigiaReset";
    public static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "vigia.log");

    /// <summary>O envio à seleção de personagem depois do /reset (issue #7) só liga depois de testado no jogo: basta criar este arquivo.</summary>
    public static readonly string ResetSwitchPath = Path.Combine(AppContext.BaseDirectory, "vigia-reset-selecao.ligado");
    static bool ResetToSelectEnabled => File.Exists(ResetSwitchPath);

    /// <summary>Comandos curtos de atributo (/f /a /v /e /c). Liga com este arquivo; desliga apagando-o e reiniciando o GameServer.</summary>
    public static readonly string ShortCmdSwitchPath = Path.Combine(AppContext.BaseDirectory, "vigia-comandos-curtos.ligado");
    static bool ShortCommandsEnabled => File.Exists(ShortCmdSwitchPath);

    /// <summary>
    /// Renomeia os comandos de distribuir pontos no binário (a sintaxe é fixa; o kit só liga/desliga). Cada comando é uma
    /// std::string: [id 4][texto 16][tamanho 4][capacidade 4]. Trocamos o texto E o campo de tamanho (a comparação usa o
    /// tamanho). O nome novo é mais curto, então cabe no lugar e nada é deslocado. Há 2 cópias da tabela; as duas são trocadas.
    /// </summary>
    static readonly (string Antigo, string Novo)[] CommandRenames =
        { ("/addstr", "/f"), ("/addagi", "/a"), ("/addvit", "/v"), ("/addene", "/e"), ("/addcmd", "/c") };

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
    // +0x57 conta, +0x10D mapa, +0x648 faixas extras do inventário (Magic Backpack) e +0x64C baú extra: conferidos com o
    // banco em 28/09/2026 (a rotina do Magic Backpack no GameServer, 0x421FF9, lê e soma +0x648 até 2)
    const int OffName = 0x62, OffLevel = 0x8C, OffAccount = 0x57, OffMap = 0x10D, OffMasterLevel = 0x61C, OffExtInventory = 0x648, ObjHead = 0x650;
    const int Playing = 3, LoggedIn = 2, CharacterSelect = 1, ServerSelect = 2;   // +0x04 estado; +0x0B tipo de saída

    sealed class Target
    {
        public required Process Process;
        public IntPtr Handle;
        public uint Table;
        public DateTime NextTry;
        public bool ApplyAttackFix;          // só o laço do vigia aplica; sondagem e botão do painel só leem
        public string AttackFixState = "checagem de ataques ainda não verificada";
        public string CommandFixState = "comandos curtos: não verificado";
        public readonly Dictionary<int, (string Name, int Level, int ExtInventory)> Seen = new();
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
        var nextHousekeeping = DateTime.MinValue;
        var nextBonus = DateTime.MinValue;
        var nextQueue = DateTime.MinValue; var nextQueueError = DateTime.MinValue;
        var nextPass = DateTime.MinValue; var nextPassError = DateTime.MinValue;
        while (true)
        {
            try
            {
                // a cada 1 s: resets pedidos pelo site com a conta online (manda para a seleção e aplica)
                if (DateTime.Now >= nextQueue)
                {
                    nextQueue = DateTime.Now.AddSeconds(1);
                    try { foreach (var l in ProcessResetRequests(targets.Values.ToList())) Log("reset pelo site: " + l); }
                    catch (Exception ex) { if (DateTime.Now >= nextQueueError) { nextQueueError = DateTime.Now.AddMinutes(5); Log("reset pelo site: " + ex.Message); } }
                }

                // a cada 1 s: Passe dos Mapas (quem está num mapa acima do 400 sem passe vai para a seleção e depois para Lorencia)
                if (DateTime.Now >= nextPass)
                {
                    nextPass = DateTime.Now.AddSeconds(1);
                    try { foreach (var l in ProcessMapPass(targets.Values.ToList())) Log("passe: " + l); }
                    catch (Exception ex) { if (DateTime.Now >= nextPassError) { nextPassError = DateTime.Now.AddMinutes(5); Log("passe: " + ex.Message); } }
                }

                // a cada 5 s: bônus por tempo (liga e desliga as taxas na hora certa, avisa os jogadores)
                if (DateTime.Now >= nextBonus)
                {
                    nextBonus = DateTime.Now.AddSeconds(5);
                    try { foreach (var l in TimedBonuses.Tick(DateTime.Now)) Log("bônus: " + l); }
                    catch (Exception ex) { Log("bônus: " + ex.Message); }
                }

                // a cada 30 s: tira do Notice.txt os avisos "enviar agora" já mandados (o painel pode ter sido fechado)
                if (DateTime.Now >= nextHousekeeping)
                {
                    nextHousekeeping = DateTime.Now.AddSeconds(30);
                    try
                    {
                        int n = Notices.RemoveOneShots(TimeSpan.FromSeconds(20));
                        if (n > 0) { ServerControl.Reload("Util (GMs, avisos)"); Log($"{n} aviso(s) \"enviar agora\" tirado(s) do Notice.txt"); }
                    }
                    catch (Exception ex) { Log("avisos: " + ex.Message); }
                }

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
            Thread.Sleep(250);   // o /reset é percebido em até 1/4 de segundo
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
                    sb.AppendLine($"   {t.CommandFixState}");
                    sb.AppendLine($"   /reset leva à seleção de personagem: {(ResetToSelectEnabled ? "ligado" : "desligado (falta testar a #7)")}");
                    sb.AppendLine($"   Passe dos Mapas: {(PassEnabled ? "ligado" : "desligado")}; Magic Backpack leva à seleção: {(BackpackEnabled ? "ligado" : "desligado")}");
                    if (t.Table == 0) continue;
                    foreach (var (idx, obj) in Players(t))
                        sb.AppendLine($"   {idx}: {ReadName(obj)} (conta {ReadAccount(obj)}) nível {BitConverter.ToInt16(obj, OffLevel)} " +
                                      $"mapa {obj[OffMap]} faixas extras {BitConverter.ToInt32(obj, OffExtInventory)} " +
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

        var (cmdState, cmdChanged) = CommandNameFix(t, image, apply: t.ApplyAttackFix);
        t.CommandFixState = cmdState;
        if (cmdChanged) Log($"{t.Process.ProcessName} (pid {t.Process.Id}): {cmdState}");

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

    /// <summary>
    /// Acha cada comando de atributo (texto seguido de \0, com o campo tamanho = comprimento certo logo depois do buffer de
    /// 16 bytes) e, com apply, troca o texto pelo curto e ajusta o tamanho. Só age se o arquivo de liga estiver presente.
    /// Devolve (situação, algo mudou).
    /// </summary>
    static (string State, bool Changed) CommandNameFix(Target t, byte[] image, bool apply)
    {
        if (!ShortCommandsEnabled) return ("comandos curtos: desligado", false);
        int pendentes = 0, trocados = 0, prontos = 0;
        foreach (var (antigo, novo) in CommandRenames)
        {
            var alvo = Encoding.ASCII.GetBytes(antigo).Concat(new byte[] { 0 }).ToArray();   // "/addstr\0"
            var curto = Encoding.ASCII.GetBytes(novo).Concat(new byte[] { 0 }).ToArray();     // "/f\0"
            // conta as cópias já trocadas (texto curto com o tamanho novo no lugar)
            for (int i = image.AsSpan().IndexOf(curto); i >= 0; )
            {
                if (i + 20 <= image.Length && BitConverter.ToInt32(image, i + 16) == novo.Length && image[i + novo.Length + 1] == 0) prontos++;
                int n = image.AsSpan(i + 1).IndexOf(curto); i = n < 0 ? -1 : i + 1 + n;
            }
            // acha e troca as que ainda têm o nome antigo (tamanho == comprimento antigo confirma que é a entrada da tabela)
            for (int i = image.AsSpan().IndexOf(alvo); i >= 0; )
            {
                if (i + 20 <= image.Length && BitConverter.ToInt32(image, i + 16) == antigo.Length)
                {
                    pendentes++;
                    if (apply)
                    {
                        var buf = new byte[antigo.Length + 1];                 // apaga o texto antigo inteiro
                        Encoding.ASCII.GetBytes(novo).CopyTo(buf, 0);          // resto fica 0 (fim de string)
                        if (Write(t, (uint)(ImageStart + i), buf) && Write(t, (uint)(ImageStart + i + 16), BitConverter.GetBytes(novo.Length))) trocados++;
                    }
                }
                int n = image.AsSpan(i + 1).IndexOf(alvo); i = n < 0 ? -1 : i + 1 + n;
            }
        }
        string mapa = string.Join(" ", CommandRenames.Select(c => $"{c.Antigo}→{c.Novo}"));
        if (apply && trocados > 0) return ($"comandos curtos aplicados ({trocados} entrada(s)): {mapa}", true);
        if (pendentes == 0 && prontos > 0) return ($"comandos curtos já aplicados: {mapa}", false);
        return ($"comandos curtos: {pendentes} entrada(s) a trocar ({mapa})", false);
    }

    static readonly HashSet<string> logged = new();
    static void LogOnce(Target t, string msg) { if (logged.Add($"{t.Process.Id}:{msg}")) Log(msg); }

    /// <summary>Objetos de jogador conectados e jogando: (índice, primeiros bytes do objeto).</summary>
    static IEnumerable<(int Index, byte[] Obj)> Players(Target t) => Objects(t, Playing, Playing);

    /// <summary>Objetos de jogador com o estado (+0x04) entre min e max: 2 = conta logada (seleção de personagem), 3 = jogando.</summary>
    static IEnumerable<(int Index, byte[] Obj)> Objects(Target t, int min, int max)
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
            int state = BitConverter.ToInt32(obj, OffConnected);
            if (state < min || state > max) continue;
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
            int ext = BitConverter.ToInt32(obj, OffExtInventory);
            bool known = t.Seen.TryGetValue(idx, out var prev) && prev.Name == name;
            if (ResetToSelectEnabled && known && prev.Level >= 50 && level <= 10)
                Log(Trigger(t, idx, name, $"/reset detectado (nível {prev.Level} → {level})"));
            // o GameServer soma a faixa mas não avisa o cliente: ela só aparece ao reentrar (dono, 28/09/2026)
            else if (BackpackEnabled && known && ext > prev.ExtInventory && prev.ExtInventory >= 0 && ext <= 2)
                Log(Trigger(t, idx, name, $"Magic Backpack usado (faixas extras {prev.ExtInventory} → {ext})"));
            t.Seen[idx] = (name, level, ext);
        }
        foreach (var gone in t.Seen.Keys.Where(k => !alive.Contains(k)).ToList()) t.Seen.Remove(gone);
    }

    // ---------------- reset pelo site sem deslogar ----------------
    // pedido → (quando mandou para a seleção, quando viu o personagem fora do jogo)
    static readonly Dictionary<int, (DateTime Sent, DateTime? Left)> requests = new();
    static DateTime nextBeat = DateTime.MinValue;

    /// <summary>
    /// Pedidos de reset feitos pelo site com a conta online (dbo.MuChila_ResetPedido). Personagem jogando → vai para a seleção
    /// de personagem na hora (Trigger). Fora do jogo há 2 s (o servidor já gravou) → chama a procedure com @IgnorarOnline=1.
    /// Só age quando TODOS os GameServers/Castle Siege rodando estão com a tabela de objetos lida (senão não dá para ter certeza
    /// de que o personagem está fora do jogo). Pedido com mais de 3 minutos expira.
    /// </summary>
    static List<string> ProcessResetRequests(List<Target> targets)
    {
        var log = new List<string>();
        if (DateTime.Now >= nextBeat)
        {
            nextBeat = DateTime.Now.AddSeconds(5);
            Db.Execute("MERGE dbo.MuChila_VigiaStatus AS d USING (SELECT 1 AS Id) AS s ON d.Id = s.Id " +
                       "WHEN MATCHED THEN UPDATE SET Batida = GETDATE() WHEN NOT MATCHED THEN INSERT (Id, Batida) VALUES (1, GETDATE());");
            WriteLiveStatus(targets);
        }
        var pend = Db.Query("SELECT Id, Personagem, Tipo, Coins, MaxStat, ISNULL(Valor, 0) AS Valor, Criado FROM dbo.MuChila_ResetPedido WHERE Status = 'pendente' ORDER BY Id");
        if (pend.Rows.Count == 0) { requests.Clear(); return log; }

        int running = new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess }.Sum(n => Process.GetProcessesByName(n).Length);
        var ready = targets.Where(t => t.Table != 0 && !t.Process.HasExited).ToList();
        if (ready.Count < running) return log;   // algum servidor ainda não foi lido: espera
        var playing = new Dictionary<string, (Target T, int Idx)>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in ready) foreach (var (idx, obj) in Players(t)) if (ReadName(obj) is { } n) playing[n] = (t, idx);

        foreach (System.Data.DataRow r in pend.Rows)
        {
            int id = (int)r["Id"]; string name = (string)r["Personagem"], kind = (string)r["Tipo"];
            if ((DateTime)r["Criado"] < DateTime.Now.AddMinutes(-3))
            {
                FinishRequest(id, "expirado", null, "O personagem não saiu do jogo a tempo. Peça de novo.");
                requests.Remove(id); log.Add($"{name}: pedido {id} ({kind}) expirou"); continue;
            }
            if (playing.TryGetValue(name, out var p))
            {
                if (!requests.TryGetValue(id, out var st) || (st.Left == null && DateTime.Now - st.Sent > TimeSpan.FromSeconds(8)))
                {   // manda (ou manda de novo, se não saiu em 8 s) para a seleção de personagem
                    log.Add(Trigger(p.T, p.Idx, name, $"{kind} pedido pelo site"));
                    requests[id] = (DateTime.Now, null);
                }
                else if (st.Left != null) requests[id] = (st.Sent, null);   // entrou de novo antes do reset: espera sair outra vez
                continue;
            }
            if (requests.TryGetValue(id, out var a))
            {
                if (a.Left == null) { requests[id] = (a.Sent, DateTime.Now); continue; }
                if (DateTime.Now - a.Left.Value < TimeSpan.FromSeconds(2)) continue;   // tempo para o servidor gravar o personagem
            }
            int res = RunReset(name, kind, (int)r["Coins"], (int)r["MaxStat"], Convert.ToInt64(r["Valor"]));
            FinishRequest(id, res == 0 ? "feito" : "erro", res, null);
            requests.Remove(id);
            log.Add($"{name}: {kind} aplicado (retorno {res})");
        }
        return log;
    }

    /// <summary>
    /// Nível e master level AO VIVO de quem está jogando (dbo.MuChila_PersonagemAoVivo): o banco só recebe o nível quando o
    /// servidor salva o personagem, então o site usa esta tabela para habilitar os resets na hora. Master level em +0x61C.
    /// </summary>
    static void WriteLiveStatus(List<Target> targets)
    {
        var rows = new List<(string Name, int Level, int Master)>();
        foreach (var t in targets.Where(t => t.Table != 0 && !t.Process.HasExited))
            foreach (var (_, obj) in Players(t))
            {
                int level = BitConverter.ToInt16(obj, OffLevel), master = BitConverter.ToInt16(obj, OffMasterLevel);
                if (ReadName(obj) is { } n && level is >= 1 and <= 400 && master is >= 0 and <= 600) rows.Add((n, level, master));
            }
        var sql = new StringBuilder("DELETE dbo.MuChila_PersonagemAoVivo;");
        var pars = new List<(string, object)>();
        for (int i = 0; i < rows.Count; i++)
        {
            sql.Append($" INSERT dbo.MuChila_PersonagemAoVivo (Name, cLevel, MasterLevel) VALUES (@n{i}, @l{i}, @m{i});");
            pars.Add(($"@n{i}", rows[i].Name)); pars.Add(($"@l{i}", rows[i].Level)); pars.Add(($"@m{i}", rows[i].Master));
        }
        Db.Execute(sql.ToString(), pars.ToArray());
    }

    /// <summary>
    /// Resets do site e do painel (reset, master, supreme) e ajustes do painel (nivel, exp, mlevel: dbo.MuChila_AjustarPersonagem
    /// com o valor do pedido). Público: o painel usa o mesmo caminho quando o personagem está fora do jogo e o vigia parado.
    /// </summary>
    public static int RunReset(string name, string kind, int coins, int maxStat, long value = 0, bool ignoreOnline = true)
    {
        string sql = kind switch
        {
            "reset" => "DECLARE @r int; EXEC @r = dbo.MuChila_Reset @Name = @n, @IgnorarOnline = @i; SELECT @r AS r",
            "master" => "DECLARE @r int; EXEC @r = dbo.MuChila_MasterReset @Name = @n, @Coins = @c, @IgnorarOnline = @i; SELECT @r AS r",
            "supreme" => "DECLARE @r int; EXEC @r = dbo.MuChila_SupremeReset @Name = @n, @Coins = @c, @MaxStat = @m, @IgnorarOnline = @i; SELECT @r AS r",
            "nivel" or "exp" or "mlevel" => "DECLARE @r int; EXEC @r = dbo.MuChila_AjustarPersonagem @Name = @n, @Tipo = @t, @Valor = @v, @IgnorarOnline = @i; SELECT @r AS r",
            _ => throw new InvalidOperationException($"tipo de pedido desconhecido: {kind}"),
        };
        var t = Db.Query(sql, ("@n", name), ("@c", coins), ("@m", maxStat), ("@t", kind), ("@v", value), ("@i", ignoreOnline));
        return t.Rows.Count > 0 ? Convert.ToInt32(t.Rows[^1]["r"]) : -1;
    }

    static void FinishRequest(int id, string status, int? result, string? message) =>
        Db.Execute("UPDATE dbo.MuChila_ResetPedido SET Status = @s, Resultado = @r, Mensagem = @m, Atualizado = GETDATE() WHERE Id = @id",
            ("@s", status), ("@r", (object?)result ?? DBNull.Value), ("@m", (object?)message ?? DBNull.Value), ("@id", id));

    /// <summary>
    /// Marca a saída do jogador, como as opções do menu do jogo: closeType 1 = seleção de personagem (continua logado),
    /// 2 = seleção de servidor (a conta sai: o servidor salva o personagem e fecha a conta). Confere que o objeto ainda é o
    /// mesmo pelo nome ou, com account, pela conta (na seleção de personagem o nome pode não estar preenchido).
    /// </summary>
    static string Trigger(Target t, int idx, string name, string why, int closeType = CharacterSelect, string? account = null)
    {
        uint ptr = BitConverter.ToUInt32(Read(t, t.Table + (uint)idx * 4, 4) ?? new byte[4]);
        var head = ptr == 0 ? null : Read(t, ptr, ObjHead);
        bool same = head != null && BitConverter.ToInt32(head, OffIndex) == idx &&
                    (account != null ? string.Equals(ReadAccount(head), account, StringComparison.OrdinalIgnoreCase) : ReadName(head) == name);
        if (!same) return $"{name}: {why}, mas o personagem saiu antes; nada feito";
        if ((sbyte)head![OffCloseCount] > 0) return $"{name}: {why}; já estava saindo";
        // o tipo primeiro e a contagem por último: o servidor só olha o tipo quando a contagem é maior que zero
        bool ok = Write(t, ptr + OffCloseType, new byte[] { (byte)closeType })
               && Write(t, ptr + OffEnableDel, BitConverter.GetBytes(1))
               && Write(t, ptr + OffCloseCount, new byte[] { 1 });   // 1 = troca no próximo segundo do servidor, sem contagem na tela (o menu do jogo usa 6 = 5 s)
        var where = closeType == ServerSelect ? "seleção de servidor (conta deslogada)" : "seleção de personagem";
        return ok ? $"{name} ({t.Process.ProcessName}): {why}; indo para a {where} agora"
                  : $"{name}: {why}; falha ao gravar na memória do {t.Process.ProcessName} (erro {Marshal.GetLastWin32Error()})";
    }

    /// <summary>
    /// Desloga a conta pelo GameServer (como "Trocar servidor" no menu do jogo): o servidor salva o personagem e fecha a conta,
    /// sem derrubar a conexão e sem pedir administrador; só esta conta sai. Procura em todos os GameServers e no Castle Siege,
    /// jogando ou na seleção de personagem. Null = a conta não foi achada (versão desconhecida do GameServer, ou já saiu).
    /// </summary>
    public static string? LogoutAccount(string account, string why)
    {
        foreach (var name in new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess })
            foreach (var p in Process.GetProcessesByName(name))
            {
                var t = new Target { Process = p };
                try
                {
                    Attach(t);
                    if (t.Table == 0) continue;
                    foreach (var (idx, obj) in Objects(t, LoggedIn, Playing))
                        if (string.Equals(ReadAccount(obj), account, StringComparison.OrdinalIgnoreCase))
                            return Trigger(t, idx, ReadName(obj) ?? account, why, ServerSelect, account);
                }
                finally { Close(t); }
            }
        return null;
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

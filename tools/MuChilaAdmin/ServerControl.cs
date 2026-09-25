using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace MuChilaAdmin;

/// <summary>Processos do servidor, comandos de menu (Reload/Disconnect) e o launcher "Ligar Servidor".</summary>
public static class ServerControl
{
    // MUCHILA_ROOT permite testar contra uma copia da pasta do servidor
    public static readonly string ServerRoot = Environment.GetEnvironmentVariable("MUCHILA_ROOT") ?? @"C:\MuServer";
    public static readonly string LauncherPath = Path.Combine(ServerRoot, @"2 - Ligar Servidor\Ligar Servidor.exe");
    public static readonly string MuEditorPath = Path.Combine(ServerRoot, @"1 - MuEditor\MuEditor.exe");

    public static readonly (string Display, string Process)[] Servers =
    {
        ("ConnectServer", "ConnectServer"),
        ("DataServer", "DataServer"),
        ("DataServer BattleCore", "DataServer BattleCore"),
        ("JoinServer", "JoinServer"),
        ("Castle Siege Server", "Castle Siege Server"),
        ("GameServer", "Game Server S14"),
    };

    public const string GameServerProcess = "Game Server S14";
    public const string CastleSiegeProcess = "Castle Siege Server";

    // IDs dos itens de menu dos executaveis MuDevs (lidos das janelas dos servidores)
    public static readonly Dictionary<string, int> ReloadIds = new()
    {
        ["CashShop"] = 32776, ["ChaosMix"] = 32777, ["Character"] = 32778, ["Command"] = 32779,
        ["Common (inclui mensagens)"] = 32780, ["Custom"] = 32781, ["Event"] = 32782, ["EventItemBag"] = 32783,
        ["Hack"] = 32784, ["Item"] = 32785, ["Monster"] = 32786, ["Move"] = 32787,
        ["Quest"] = 32788, ["Shop"] = 32789, ["Skill"] = 32790, ["Util (GMs, avisos)"] = 32791,
    };
    const int AllUserDisconnect = 32772;
    const uint WM_COMMAND = 0x0111;

    public static Process? Find(string processName) => Process.GetProcessesByName(processName).FirstOrDefault();

    /// <summary>Envia um comando de menu para o GameServer e o Castle Siege (a pasta Data e compartilhada).</summary>
    public static List<string> SendToGameServers(int menuId)
    {
        var result = new List<string>();
        foreach (var name in new[] { GameServerProcess, CastleSiegeProcess })
        {
            var p = Find(name);
            var hwnd = p == null ? IntPtr.Zero : FindMenuWindow(p.Id);
            if (hwnd == IntPtr.Zero) { result.Add($"{name}: nao esta rodando"); continue; }
            PostMessage(hwnd, WM_COMMAND, (IntPtr)menuId, IntPtr.Zero);
            result.Add($"{name}: comando enviado");
        }
        return result;
    }

    public static List<string> Reload(string item) => SendToGameServers(ReloadIds[item]);
    public static List<string> DisconnectAll() => SendToGameServers(AllUserDisconnect);

    /// <summary>Titulo da janela do GameServer, ex.: "(PlayerCount : 2/100) (MonsterCount : 9023/10000)".</summary>
    public static (string Players, string Monsters) GameServerCounts()
    {
        var p = Find(GameServerProcess);
        if (p == null) return ("-", "-");
        var title = WindowTitle(FindMenuWindow(p.Id));
        var players = Regex.Match(title, @"PlayerCount : ([^)]+)").Groups[1].Value;
        var monsters = Regex.Match(title, @"MonsterCount : ([^)]+)").Groups[1].Value;
        return (players == "" ? "-" : players, monsters == "" ? "-" : monsters);
    }

    /// <summary>Aciona "Start all" ou "Stop all" na barra do launcher (o botao alterna entre os dois).</summary>
    public static string LauncherToggle(bool start)
    {
        var launcher = Find("Ligar Servidor");
        if (launcher == null)
        {
            if (!start) return "O launcher nao esta aberto.";
            launcher = Process.Start(new ProcessStartInfo(LauncherPath) { WorkingDirectory = Path.GetDirectoryName(LauncherPath)! })!;
            Thread.Sleep(3000);
            launcher.Refresh();
        }
        ShowWindow(launcher.MainWindowHandle, 9);   // restaura: minimizado, a barra nao aparece para o UI Automation
        Thread.Sleep(800);

        var window = AutomationElement.RootElement.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, launcher.Id));
        var toolbar = window?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "TSM"));
        if (toolbar == null) return "Barra do launcher nao encontrada.";

        var wanted = start ? "Start all" : "Stop all";
        var button = toolbar.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, wanted));
        if (button == null) return start ? "O launcher ja esta em modo iniciado (botao 'Stop all' ativo)." : "O launcher ja esta parado.";
        ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        return $"'{wanted}' acionado no launcher.";
    }

    // ---------------- logout forcado de um jogador ----------------
    // O MuDevs FREE nao tem comando para desconectar um jogador so ("UsersOnline" nao abre nada).
    // Derrubar a conexao TCP dele tem o mesmo efeito do jogo fechar: o servidor salva o personagem e faz o logout.

    public static bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Outras contas online com o mesmo IP (tambem cairiam, porque a conexao e identificada pelo IP).</summary>
    public static string[] OnlineSameIp(string account) =>
        Db.Query(@"SELECT s2.memb___id FROM MEMB_STAT s1 JOIN MEMB_STAT s2 ON s2.IP = s1.IP AND s2.memb___id <> s1.memb___id
                   WHERE s1.memb___id = @a AND s1.ConnectStat = 1 AND s2.ConnectStat = 1", ("@a", account))
          .Rows.Cast<System.Data.DataRow>().Select(r => (string)r[0]).ToArray();

    /// <summary>Forca o logout da conta derrubando as conexoes dela com o servidor em que esta. Espera o servidor registrar a saida.</summary>
    public static string ForceLogout(string account)
    {
        var t = Db.Query("SELECT IP, ServerName FROM MEMB_STAT WHERE memb___id = @a AND ConnectStat = 1", ("@a", account));
        if (t.Rows.Count == 0) return $"{account} já está offline.";
        var ip = (string)t.Rows[0]["IP"];
        var server = t.Rows[0]["ServerName"] as string ?? "";
        var proc = Find(server.Contains("CastleSiege", StringComparison.OrdinalIgnoreCase) ? CastleSiegeProcess : GameServerProcess)
                   ?? throw new InvalidOperationException($"O servidor em que {account} está ({server}) não está rodando.");

        int closed; uint error;
        if (IsElevated) (closed, error) = DropConnections(proc.Id, ip);
        else
        {
            // So esta parte precisa de administrador: roda o proprio painel elevado com --derrubar (o Windows pede permissao)
            var result = Path.Combine(Path.GetTempPath(), $"muchila-derrubar-{Guid.NewGuid():N}.txt");
            var psi = new ProcessStartInfo(Environment.ProcessPath!, $"--derrubar {proc.Id} {ip} \"{result}\"")
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            try { using var p = Process.Start(psi)!; p.WaitForExit(20000); }
            catch (System.ComponentModel.Win32Exception) { return "Logout forçado cancelado: a permissão de administrador não foi dada."; }
            var parts = File.Exists(result) ? File.ReadAllText(result).Split(' ') : new[] { "0", "0" };
            try { File.Delete(result); } catch { }
            closed = int.Parse(parts[0]); error = uint.Parse(parts[1]);
        }
        if (closed == 0)
            return error != 0 ? $"Não consegui derrubar a conexão de {account} (erro {error})." : $"Nenhuma conexão de {account} ({ip}) encontrada no servidor.";

        for (int i = 0; i < 20 && Db.IsOnline(account); i++) Thread.Sleep(1000);
        return Db.IsOnline(account)
            ? $"Conexão de {account} derrubada ({ip}), mas o servidor ainda não registrou a saída. Confira em alguns segundos."
            : $"{account}: logout forçado ({closed} conexão(ões) de {ip}); o servidor salvou o personagem.";
    }

    /// <summary>Derruba as conexoes estabelecidas do processo vindas do IP. Retorna (quantas fechou, ultimo erro). Precisa de administrador.</summary>
    public static (int Closed, uint Error) DropConnections(int pid, string ip)
    {
        int closed = 0; uint error = 0;
        foreach (var c in Connections(pid).Where(c => new System.Net.IPAddress(c.RemoteAddr).ToString() == ip))
        {
            var row = new MIB_TCPROW { State = 12 /* DELETE_TCB */, LocalAddr = c.LocalAddr, LocalPort = c.LocalPort, RemoteAddr = c.RemoteAddr, RemotePort = c.RemotePort };
            var r = SetTcpEntry(ref row);
            if (r == 0) closed++; else error = r;
        }
        return (closed, error);
    }

    static List<MIB_TCPROW_OWNER_PID> Connections(int pid)
    {
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2 /* AF_INET */, 4 /* TCP_TABLE_OWNER_PID_CONNECTIONS */, 0);
        var buf = Marshal.AllocHGlobal(size);
        try
        {
            var list = new List<MIB_TCPROW_OWNER_PID>();
            if (GetExtendedTcpTable(buf, ref size, false, 2, 4, 0) != 0) return list;
            int n = Marshal.ReadInt32(buf), rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < n; i++)
            {
                var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(buf + 4 + i * rowSize);
                if (r.OwningPid == pid && r.State == 5 /* ESTABLISHED */) list.Add(r);
            }
            return list;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    [StructLayout(LayoutKind.Sequential)] struct MIB_TCPROW { public uint State, LocalAddr, LocalPort, RemoteAddr, RemotePort; }
    [StructLayout(LayoutKind.Sequential)] struct MIB_TCPROW_OWNER_PID { public uint State, LocalAddr, LocalPort, RemoteAddr, RemotePort, OwningPid; }
    [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int af, int tableClass, uint reserved);
    [DllImport("iphlpapi.dll")] static extern uint SetTcpEntry(ref MIB_TCPROW row);

    public static void Open(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = true });
    }

    // --- Win32 ---
    delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int cmd);

    /// <summary>A janela aberta pelo launcher nao e a "janela principal" do processo; procura a que tem menu.</summary>
    static IntPtr FindMenuWindow(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var p);
            if (p == pid && GetMenu(h) != IntPtr.Zero) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    static string WindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "";
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}

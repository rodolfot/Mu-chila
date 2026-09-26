using System.IO;
using System.Text;

namespace MuChilaAdmin;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // "MuChilaAdmin.exe --teste [arquivo]": verifica banco, servidores e agendas sem abrir a janela
        if (args.Length > 0 && args[0] == "--teste")
            return SelfTest(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "muchila-admin-teste.txt"));

        // "--derrubar <pid> <ip> <arquivo>": usado pelo proprio painel, elevado, para o logout forcado (grava "fechadas erro")
        if (args.Length == 4 && args[0] == "--derrubar")
        {
            var (closed, error) = ServerControl.DropConnections(int.Parse(args[1]), args[2]);
            File.WriteAllText(args[3], $"{closed} {error}");
            return closed > 0 ? 0 : 1;
        }

        // "--forcar-logout <conta> <arquivo>": o mesmo que o botao, sem abrir a janela
        if (args.Length == 3 && args[0] == "--forcar-logout")
        {
            try { File.WriteAllText(args[2], ServerControl.ForceLogout(args[1])); return 0; }
            catch (Exception ex) { File.WriteAllText(args[2], "FALHA: " + ex.Message); return 1; }
        }

        // "MuChilaAdmin.exe --zerar-master <conta> <personagem> <arquivo>": o mesmo que o botao, sem abrir a janela
        if (args.Length == 4 && args[0] == "--zerar-master")
        {
            try { File.WriteAllText(args[3], Accounts.ClearMasterSkills(args[1], args[2])); return 0; }
            catch (Exception ex) { File.WriteAllText(args[3], "FALHA: " + ex.Message); return 1; }
        }

        // "--vigia-reset": laço do vigia do /reset (sem janela; uma instância só)
        if (args.Length == 1 && args[0] == "--vigia-reset")
            return ResetWatcher.Run();

        // "--vigia-sondar <arquivo>": só leitura, mostra o que o vigia enxerga nos GameServers
        if (args.Length == 2 && args[0] == "--vigia-sondar")
        {
            try { File.WriteAllText(args[1], ResetWatcher.Probe()); return 0; }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex.Message); return 1; }
        }

        // "--selecao <personagem> <arquivo>": leva o personagem para a seleção de personagem (o mesmo que o botão)
        if (args.Length == 3 && args[0] == "--selecao")
        {
            try { File.WriteAllText(args[2], ResetWatcher.SendToCharacterSelect(args[1])); return 0; }
            catch (Exception ex) { File.WriteAllText(args[2], "FALHA: " + ex.Message); return 1; }
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    static int SelfTest(string output)
    {
        var sb = new StringBuilder();
        int failures = 0;
        void Check(string name, Func<string> test)
        {
            try { sb.AppendLine($"OK    {name}: {test()}"); }
            catch (Exception ex) { failures++; sb.AppendLine($"FALHA {name}: {ex.Message}"); }
        }

        Check("Banco (contas)", () => $"{Accounts.List().Rows.Count} contas");
        Check("Banco (online)", () => $"{Accounts.Online().Rows.Count} online");
        Check("Servidores", () => string.Join(", ", ServerControl.Servers.Select(s => $"{s.Display}={(ServerControl.Find(s.Process, s.Folder) != null ? "rodando" : "parado")}")));
        Check("GameServers", ServerControl.GameServerCounts);
        Check("Agendas de eventos", () => $"{EventScheduler.Pending().Count} disparo(s) registrados");
        Check("Vigia do /reset", () => ResetWatcher.IsRunning() ? "rodando" : "parado");
        Check("MuEditor", () => File.Exists(ServerControl.MuEditorPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.MuEditorPath));
        Check("Launcher", () => File.Exists(ServerControl.LauncherPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.LauncherPath));

        File.WriteAllText(output, sb.ToString());
        return failures;
    }
}

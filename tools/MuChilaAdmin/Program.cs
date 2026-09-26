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

        // "--agendar-bonus <tipos: 0=EXP,1=master,2=drop, ex. 0,1> <multiplicador> <minutos> <começa-em-minutos> <arquivo>":
        // só grava o BonusManager.dat e as mensagens (não recarrega nada). Usado para testar contra a cópia de testes (MUCHILA_ROOT).
        if (args.Length == 6 && args[0] == "--agendar-bonus")
        {
            try
            {
                var types = args[1].Split(',').Select(int.Parse).ToList();
                var start = DateTime.Now.AddMinutes(int.Parse(args[4]));
                var msg = BonusScheduler.Schedule(types, decimal.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), int.Parse(args[3]), start);
                File.WriteAllText(args[5], msg + Environment.NewLine + string.Join(Environment.NewLine, BonusScheduler.List().Select(x => $"{x.Slot} {x.Start:dd/MM HH:mm}-{x.End:HH:mm} {x.Description} [{x.State}]")));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[5], "ERRO: " + ex.Message); return 1; }
        }

        // "--testar-lojas <arquivo>": lê todas as lojas, simula o encaixe 8×15 e regrava cada uma sem mudar nada, conferindo
        // que os itens voltam iguais. Só roda contra uma cópia (MUCHILA_ROOT), nunca contra C:\MuServer.
        if (args.Length == 2 && args[0] == "--testar-lojas")
        {
            var sb = new StringBuilder();
            if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(args[1], "ERRO: use MUCHILA_ROOT apontando para uma cópia de testes"); return 1; }
            int falhas = 0;
            foreach (var s in Shops.List())
            {
                var items = Shops.Load(s);
                var (fora, used) = Shops.Fit(items);
                Shops.Save(s, items);
                var again = Shops.Load(s);
                bool same = again.Count == items.Count && again.Zip(items).All(p => System.Text.Json.JsonSerializer.Serialize(p.First) == System.Text.Json.JsonSerializer.Serialize(p.Second));
                if (!same) falhas++;
                sb.AppendLine($"{(same ? "OK   " : "FALHA")} {s.Index:000} {s.Name}: {items.Count} itens, {used}/{Shops.GridWidth * Shops.GridHeight} espaços{(fora.Count > 0 ? $", não cabem: {string.Join(", ", fora.Select(i => Shops.Name(i.Section, i.Type)))}" : "")}");
            }
            File.WriteAllText(args[1], sb.ToString());
            return falhas;
        }

        // "--listar-itens <conta> <personagem | bau> <arquivo>": só leitura, lista o inventário ou o baú decodificado
        if (args.Length == 4 && args[0] == "--listar-itens")
        {
            var lista = args[2] == "bau" ? Items.Vault(args[1]) : Items.Inventory(args[2]);
            File.WriteAllLines(args[3], lista.Select(i => $"{i.Slot,3} {i.Place,-28} [{i.Section},{i.Type}] {i.Name} +{i.Level}{(i.Skill ? " skill" : "")}{(i.Luck ? " sorte" : "")}{(i.Option > 0 ? $" +{i.Option * 4}" : "")}{(i.Excellent > 0 ? $" exc{i.Excellent:X2}" : "")} dur{i.Durability}"));
            return 0;
        }

        // "--remover-item <conta> <personagem | bau> <posição> <arquivo>": o mesmo que o botão (conta fora do jogo, backup antes)
        if (args.Length == 5 && args[0] == "--remover-item")
        {
            try
            {
                var lista = args[2] == "bau" ? Items.Vault(args[1]) : Items.Inventory(args[2]);
                var item = lista.FirstOrDefault(i => i.Slot == int.Parse(args[3])) ?? throw new InvalidOperationException($"Posição {args[3]} vazia.");
                File.WriteAllText(args[4], Items.Remove(args[1], args[2] == "bau" ? null : args[2], item));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[4], "FALHA: " + ex.Message); return 1; }
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
        Check("Bônus", () => $"{BonusScheduler.List().Count} bônus deste programa no BonusManager.dat");
        Check("Vigia do /reset", () => ResetWatcher.IsRunning() ? "rodando" : "parado");
        Check("MuEditor", () => File.Exists(ServerControl.MuEditorPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.MuEditorPath));
        Check("Launcher", () => File.Exists(ServerControl.LauncherPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.LauncherPath));

        File.WriteAllText(output, sb.ToString());
        return failures;
    }
}

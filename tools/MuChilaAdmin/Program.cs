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

        // "--testar-cashshop <preço> <arquivo>": grava um preço novo em um pacote de teste da loja de cash e regrava,
        // conferindo o formato. Só roda contra uma cópia (MUCHILA_ROOT), nunca contra C:\MuServer.
        if (args.Length == 3 && args[0] == "--testar-cashshop")
        {
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(args[2], "ERRO: use MUCHILA_ROOT apontando para uma cópia de testes"); return 1; }
                var lista = CashShop.List();
                var antes = lista.Select(p => (p.Category, p.Main, p.Price)).ToList();
                var alvo = lista.FirstOrDefault(p => p.HasClient) ?? lista.First();
                int original = alvo.Price;
                alvo.Price = int.Parse(args[1]);
                var msg = CashShop.Save(lista);
                var depois = CashShop.List();
                var mudou = depois.First(p => p.Category == alvo.Category && p.Main == alvo.Main).Price;
                bool resto = depois.Where(p => !(p.Category == alvo.Category && p.Main == alvo.Main))
                    .All(p => antes.Any(a => a.Category == p.Category && a.Main == p.Main && a.Price == p.Price));
                File.WriteAllText(args[2], $"{msg}\r\npacotes: {lista.Count} -> {depois.Count}\r\nalvo {alvo.Name} [{alvo.Category},{alvo.Main}] {original} -> {mudou} (esperado {args[1]})\r\ndemais preços intactos: {resto}");
                return mudou == int.Parse(args[1]) && depois.Count == lista.Count && resto ? 0 : 1;
            }
            catch (Exception ex) { File.WriteAllText(args[2], "FALHA: " + ex.Message); return 1; }
        }

        // "--testar-monstros <arquivo>": carrega, regrava e recarrega da cópia dada, conferindo que os spawns voltam
        // iguais e que NPC/SPOT/EVENT continuam lá. Passe uma CÓPIA de um mapa, nunca o arquivo real.
        if (args.Length == 2 && args[0] == "--testar-monstros")
        {
            try
            {
                var antes = Monsters.Load(args[1]);
                var doc0 = System.Xml.Linq.XDocument.Load(args[1]);
                int npc0 = doc0.Root!.Element("NPC")?.Elements("Config").Count() ?? 0;
                Monsters.Save(args[1], antes);
                var depois = Monsters.Load(args[1]);
                var doc1 = System.Xml.Linq.XDocument.Load(args[1]);
                int npc1 = doc1.Root!.Element("NPC")?.Elements("Config").Count() ?? 0;
                bool preservados = npc1 == npc0 && doc1.Root!.Element("MONSTER") != null && doc1.Root!.Element("EVENT") != null;
                bool ok = depois.Count == antes.Count && depois.Sum(s => s.Quantity) == antes.Sum(s => s.Quantity) && preservados;
                var res = $"{(ok ? "OK" : "FALHA")}: spawns {antes.Count}->{depois.Count}, monstros {antes.Sum(s => s.Quantity)}->{depois.Sum(s => s.Quantity)}, NPCs {npc0}->{npc1}, MONSTER/EVENT preservados={preservados}";
                Console.WriteLine(res); File.WriteAllText(args[1] + ".resultado.txt", res);
                return ok ? 0 : 1;
            }
            catch (Exception ex) { Console.WriteLine("FALHA: " + ex.Message); return 1; }
        }

        // "--testar-drops <saida>": roda contra MUCHILA_ROOT (use uma CÓPIA de Data\Item\ItemDrop.txt e Data\Monster\Monster.txt).
        // Regravar sem mudar tem de sair idêntico; depois muda uma chance, cria e remove regra, troca taxas de um monstro e confere.
        if (args.Length == 2 && args[0] == "--testar-drops")
        {
            int f = TestDrops(args[1]);
            return f;
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

    static int TestDrops(string output)
    {
        var sb = new StringBuilder();
        int failures = 0;
        void Check(string name, bool ok, string extra = "") { if (!ok) failures++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {name} {extra}"); }
        try
        {
            if (!ServerControl.ServerRoot.Contains("teste", StringComparison.OrdinalIgnoreCase) && !ServerControl.ServerRoot.Contains("Temp", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"MUCHILA_ROOT ({ServerControl.ServerRoot}) não parece uma cópia de teste; nada foi feito.");
            static string[] Lines(string f) => File.ReadAllText(f, System.Text.Encoding.Latin1).Split("\r\n");
            static int Diff(string[] a, string[] b) => a.GroupBy(x => x).Sum(g => Math.Max(0, g.Count() - b.Count(y => y == g.Key)));

            // ItemDrop.txt
            var orig = File.ReadAllBytes(Drops.DropFile);
            var rules = Drops.Load();
            Drops.Save(rules);
            Check("ItemDrop regravado sem mudança sai idêntico", File.ReadAllBytes(Drops.DropFile).SequenceEqual(orig), $"({rules.Count} regras)");

            var antes = Lines(Drops.DropFile);
            rules = Drops.Load();
            int rate0 = rules[0].Rate, total = rules.Count;
            rules[0].Rate = rate0 + 1;
            var removida = rules[1];
            rules.Remove(removida);
            rules.Add(new DropRule { Item = 14 * 512 + 13, Monster = "354", Rate = Drops.ParsePercent("0,5"), Comment = "Jewel of Bless - teste" });
            Drops.Save(rules);
            var depois = Drops.Load();
            var linhas = Lines(Drops.DropFile);
            Check("quantidade de regras (1 removida, 1 nova)", depois.Count == total, $"{total} -> {depois.Count}");
            Check("chance alterada gravada", depois[0].Rate == rate0 + 1);
            var nova = depois.FirstOrDefault(r => r.Item == 14 * 512 + 13 && r.Monster == "354");
            Check("regra nova lida de volta (0,5% = 5000)", nova != null && nova.Rate == 5000 && nova.Comment == "Jewel of Bless - teste");
            Check("regra nova antes do \"end\" e arquivo termina em end+CRLF", linhas.Length >= 2 && linhas[^2].Trim() == "end" && linhas[^1] == "" && nova != null && Array.FindIndex(linhas, l => l.Contains("Jewel of Bless - teste")) < linhas.Length - 2);
            Check("só 2 linhas mudaram de cada lado (alterada + removida / alterada + nova)", Diff(antes, linhas) == 2 && Diff(linhas, antes) == 2, $"saíram {Diff(antes, linhas)}, entraram {Diff(linhas, antes)}");
            Check("Percent/ParsePercent", Drops.Percent(10000) == "1" && Drops.ParsePercent("1%") == 10000 && Drops.ParsePercent("0.1") == 1000);

            // Monster.txt
            var m0 = File.ReadAllText(Drops.MonsterFile, System.Text.Encoding.Latin1).Split("\r\n");
            var rates = Drops.MonsterRates();
            var bh = rates.First(r => r.Index == 354);
            Drops.SaveMonsterRates(new[] { bh with { ItemRate = 999, MaxItemLevel = 12 } });
            var m1 = File.ReadAllText(Drops.MonsterFile, System.Text.Encoding.Latin1).Split("\r\n");
            var bh2 = Drops.MonsterRates().First(r => r.Index == 354);
            Check("taxas do Blade Hunter gravadas", bh2.ItemRate == 999 && bh2.MaxItemLevel == 12 && bh2.MoneyRate == bh.MoneyRate, $"{bh.ItemRate}/{bh.MoneyRate}/{bh.MaxItemLevel} -> {bh2.ItemRate}/{bh2.MoneyRate}/{bh2.MaxItemLevel}");
            int li = Array.FindIndex(m0, l => l.StartsWith("354 "));
            Check("só a linha do Blade Hunter mudou", m0.Length == m1.Length && Enumerable.Range(0, m0.Length).Count(i => m0[i] != m1[i]) == 1 && m0[li] != m1[li]);
            Check("colunas seguintes continuam alinhadas", m0[li].Length == m1[li].Length, $"({m0[li].Length} -> {m1[li].Length} caracteres)");
            Check("demais monstros iguais", rates.Where(r => r.Index != 354).SequenceEqual(Drops.MonsterRates().Where(r => r.Index != 354)));
        }
        catch (Exception ex) { failures++; sb.AppendLine("FALHA inesperada: " + ex.Message); }
        sb.AppendLine($"{failures} falha(s)");
        File.WriteAllText(output, sb.ToString());
        return failures;
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

        Check("Janela do painel", () =>
        {
            using var f = new MainForm();
            if (f.SafeErrors.Count > 0) throw new InvalidOperationException("erro ao montar a janela: " + string.Join("; ", f.SafeErrors));
            return $"{f.Controls.OfType<TabControl>().Single().TabCount} abas";
        });
        Check("Banco (contas)", () => $"{Accounts.List().Rows.Count} contas");
        Check("Banco (online)", () => $"{Accounts.Online().Rows.Count} online");
        Check("Servidores", () => string.Join(", ", ServerControl.Servers.Select(s => $"{s.Display}={(ServerControl.Find(s.Process, s.Folder) != null ? "rodando" : "parado")}")));
        Check("GameServers", ServerControl.GameServerCounts);
        Check("Agendas de eventos", () => $"{EventScheduler.Pending().Count} disparo(s) registrados");
        Check("Bônus", () => $"{BonusScheduler.List().Count} bônus deste programa no BonusManager.dat");
        Check("Loja de Cash", () => { var l = CashShop.List(); return $"{l.Count} pacotes ({l.Count(p => p.HasClient)} com tela no cliente){(CashShop.ClientAvailable ? "" : "; cliente não encontrado")}"; });
        Check("Monstros (respawn)", () => { var m = Monsters.Maps(); return $"{m.Count} mapas, {m.Sum(x => x.Total)} monstros no total"; });
        Check("Comandos", () => $"{Commands.All.Count} comandos catalogados");
        Check("Drops", () => $"{Drops.Load().Count} regras de item, {Drops.MonsterRates().Count} monstros com taxas");
        Check("Invasão no ar", () => ServerControl.InvasionEnd(DateTime.Now) is { } fim ? $"até {fim:HH:mm:ss}" : "nenhuma");
        Check("Vigia do /reset", () => ResetWatcher.IsRunning() ? "rodando" : "parado");
        Check("MuEditor", () => File.Exists(ServerControl.MuEditorPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.MuEditorPath));
        Check("Launcher", () => File.Exists(ServerControl.LauncherPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.LauncherPath));

        File.WriteAllText(output, sb.ToString());
        return failures;
    }
}

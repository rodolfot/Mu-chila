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

        // "MuChilaAdmin.exe --dar-pontos <conta> <personagem> <atributo|master> <quantidade> <arquivo>": o mesmo que o botao "Dar pontos"
        if (args.Length == 6 && args[0] == "--dar-pontos")
        {
            try
            {
                int kind = args[3] switch { "atributo" => 0, "master" => 1, _ => throw new ArgumentException("tipo deve ser atributo ou master") };
                File.WriteAllText(args[5], Accounts.GivePoints(args[1], args[2], kind, int.Parse(args[4]))); return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[5], "FALHA: " + ex.Message); return 1; }
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

        // "--testar-drops-visao <saida> <foto.png>": visão "Por monstro / por item" da aba Drops, com os dados reais; só simula
        // na memória (não salva nada) e grava uma foto da aba
        if (args.Length == 3 && args[0] == "--testar-drops-visao")
        {
            try
            {
                Application.EnableVisualStyles();
                using var f = new MainForm();
                var r = f.TestDropsView(args[2]);
                File.WriteAllText(args[1], r);
                return r.Contains("FALHA") ? 1 : 0;
            }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex); return 1; }
        }

        // "--testar-passe <saida>": dá, soma e tira passe de uma conta descartável (passeteste), criada e apagada pelo teste
        if (args.Length == 2 && args[0] == "--testar-passe")
            return TestPass(args[1]);

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

        // "--testar-avisos <saida>": contra MUCHILA_ROOT (CÓPIA de Data\Util\Notice.txt): regravar sem mudar sai idêntico; o
        // "enviar agora" entra no topo com RepeatTime 1; tirar os "enviar agora" devolve o arquivo original.
        if (args.Length == 2 && args[0] == "--testar-avisos")
        {
            var sb = new StringBuilder(); int f = 0;
            void C(string n, bool ok, string x = "") { if (!ok) f++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {n} {x}"); }
            try
            {
                if (!ServerControl.ServerRoot.Contains("Temp", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("MUCHILA_ROOT não é uma cópia de teste.");
                var orig = File.ReadAllBytes(Notices.File_);
                var l = Notices.Load(); Notices.Save(l);
                C("regravar sem mudança sai idêntico", File.ReadAllBytes(Notices.File_).SequenceEqual(orig), $"({l.Count} aviso(s))");
                Notices.AddOneShot("Teste: manutenção às 22h, não saia no meio de evento!");
                var l2 = Notices.Load();
                C("\"enviar agora\" é o primeiro aviso, com 1 s", l2.Count == l.Count + 1 && l2[0].IsOneShot && l2[0].RepeatTime == 1 && l2[0].Message.StartsWith("Teste: manutenção"));
                C("avisos antigos continuam iguais", l2.Skip(1).Select(n => n.Raw).SequenceEqual(l.Select(n => n.Raw)));
                C("tirar o \"enviar agora\" devolve o original", Notices.RemoveOneShots(TimeSpan.Zero) == 1 && File.ReadAllBytes(Notices.File_).SequenceEqual(orig));
                bool recusou = false; try { Notices.Clean("oi 😀"); } catch (InvalidOperationException) { recusou = true; }
                C("recusa emoji e aceita acento", recusou && Notices.Clean("  ação\r\ncoração  ") == "ação coração");
            }
            catch (Exception ex) { f++; sb.AppendLine("FALHA inesperada: " + ex.Message); }
            sb.AppendLine($"{f} falha(s)"); File.WriteAllText(args[1], sb.ToString());
            return f;
        }

        // "--testar-bonus <saida>": contra MUCHILA_ROOT (CÓPIA com GameServer*\DATA\GameServerInfo - Common.dat, Data\Util\Notice.txt
        // e MuChilaAdmin\): liga, soma, cancela e termina bônus sem recarregar servidor, conferindo arquivos e avisos.
        if (args.Length == 2 && args[0] == "--testar-bonus")
        {
            var sb = new StringBuilder(); int f = 0;
            void C(string n, bool ok, string x = "") { if (!ok) f++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {n} {x}"); }
            try
            {
                if (!ServerControl.ServerRoot.Contains("Temp", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("MUCHILA_ROOT não é uma cópia de teste.");
                var gs = Path.Combine(ServerControl.ServerRoot, @"GameServer\DATA\GameServerInfo - Common.dat");
                var vip = Path.Combine(ServerControl.ServerRoot, @"GameServerVIP\DATA\GameServerInfo - Common.dat");
                var notice = Path.Combine(ServerControl.ServerRoot, @"Data\Util\Notice.txt");
                var o1 = File.ReadAllBytes(gs); var o2 = File.ReadAllBytes(vip); var on = File.ReadAllBytes(notice);
                int V(string file, string key) => int.Parse(System.Text.RegularExpressions.Regex.Match(File.ReadAllText(file), $@"(?m)^\s*{key}\s*=\s*(\d+)").Groups[1].Value);
                int exp0 = V(gs, "AddExperienceRate_AL0"), exp3 = V(gs, "AddExperienceRate_AL3"), m0 = V(gs, "AddMasterExperienceRate_AL0"), d0 = V(gs, "ItemDropRate_AL0");
                var t0 = DateTime.Now;

                var b1 = TimedBonuses.Schedule(new[] { 0, 2 }, 2m, 10, t0);
                TimedBonuses.Tick(t0.AddSeconds(1), reload: false);
                C("EXP e drop x2 nos dois GameServers, master igual", V(gs, "AddExperienceRate_AL0") == exp0 * 2 && V(gs, "AddExperienceRate_AL3") == exp3 * 2 && V(vip, "AddExperienceRate_AL0") == exp0 * 2
                  && V(gs, "ItemDropRate_AL0") == d0 * 2 && V(gs, "AddMasterExperienceRate_AL0") == m0, $"EXP {exp0}->{V(gs, "AddExperienceRate_AL0")}");
                var n1 = Notices.Load();
                C("aviso \"começou\" no topo e lembrete a cada 5 min", n1[0].IsOneShot && n1[0].Message.StartsWith("Começou: EXP + Drop x2") && n1.Any(n => n.Comment == $"{TimedBonuses.NoticeTag} {b1.Id}" && n.RepeatTime == 300));
                var antes = File.ReadAllBytes(gs); TimedBonuses.Tick(t0.AddSeconds(2), reload: false);
                C("rodar de novo não muda nada", File.ReadAllBytes(gs).SequenceEqual(antes) && Notices.Load().Count == n1.Count);

                var b2 = TimedBonuses.Schedule(new[] { 0 }, 1.5m, 10, t0);
                TimedBonuses.Tick(t0.AddSeconds(3), reload: false);
                C("dois bônus somam (x2 + x1,5 = x2,5)", V(gs, "AddExperienceRate_AL0") == (int)Math.Round(exp0 * 2.5m, MidpointRounding.AwayFromZero), $"{V(gs, "AddExperienceRate_AL0")}");

                var txt = File.ReadAllText(gs, System.Text.Encoding.Latin1);
                File.WriteAllText(gs, System.Text.RegularExpressions.Regex.Replace(txt, @"(?m)^(\s*AddExperienceRate_AL1\s*=\s*)\d+", "${1}999"), System.Text.Encoding.Latin1);
                var log = TimedBonuses.Tick(t0.AddSeconds(4), reload: false);
                C("taxa mudada à mão vira a base", log.Any(l => l.Contains("mudado à mão")) && V(gs, "AddExperienceRate_AL1") == (int)Math.Round(999 * 2.5m, MidpointRounding.AwayFromZero), $"{V(gs, "AddExperienceRate_AL1")}");

                TimedBonuses.Cancel(b1.Id);
                TimedBonuses.Tick(t0.AddSeconds(5), reload: false);
                var n2 = Notices.Load();
                C("cancelar o ativo: fica só o x1,5 e o lembrete dele", V(gs, "AddExperienceRate_AL0") == (int)Math.Round(exp0 * 1.5m, MidpointRounding.AwayFromZero) && V(gs, "ItemDropRate_AL0") == d0
                  && !n2.Any(n => n.Comment == $"{TimedBonuses.NoticeTag} {b1.Id}") && n2.Any(n => n.Comment == $"{TimedBonuses.NoticeTag} {b2.Id}") && n2.Any(n => n.IsOneShot && n.Message.Contains("encerrado")));

                TimedBonuses.Tick(t0.AddMinutes(11), reload: false);
                Notices.RemoveOneShots(TimeSpan.FromDays(-1));   // o teste simula horários à frente do relógio
                var esperado1 = System.Text.RegularExpressions.Regex.Replace(System.Text.Encoding.Latin1.GetString(o1), @"(?m)^(\s*AddExperienceRate_AL1\s*=\s*)\d+", "${1}999");
                C("no fim tudo volta (menos a taxa mudada à mão)", File.ReadAllText(gs, System.Text.Encoding.Latin1) == esperado1 && File.ReadAllBytes(vip).SequenceEqual(o2));
                C("avisos do bônus saem do Notice.txt", File.ReadAllBytes(notice).SequenceEqual(on));
                C("bônus terminados saem da lista", TimedBonuses.CleanupFinished(t0.AddMinutes(12)) == 2 && TimedBonuses.List().Count == 0);
            }
            catch (Exception ex) { f++; sb.AppendLine("FALHA inesperada: " + ex); }
            sb.AppendLine($"{f} falha(s)"); File.WriteAllText(args[1], sb.ToString());
            return f;
        }

        // "--cash-esconder <saida> <categoria:main> [...]": esconde pacotes da loja de cash (servidor e cliente), como o botão da
        // aba Loja de Cash; a linha original fica guardada para poder voltar. Não recarrega (use Reload CashShop depois).
        if (args.Length >= 3 && args[0] == "--cash-esconder")
        {
            try
            {
                var alvo = args.Skip(2).Select(a => a.Split(':')).Select(p => (int.Parse(p[0]), int.Parse(p[1]))).ToHashSet();
                var lista = CashShop.List();
                var achados = lista.Where(p => alvo.Contains((p.Category, p.Main))).ToList();
                if (achados.Count != alvo.Count) throw new InvalidOperationException($"achei {achados.Count} de {alvo.Count} pacotes");
                foreach (var p in achados) p.Hidden = true;
                File.WriteAllText(args[1], "OK: " + CashShop.Save(lista) + Environment.NewLine + string.Join(Environment.NewLine, achados.Select(p => $"escondido: {p.Category},{p.Main} {p.Name} (preço {p.Price})")));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex.Message); return 1; }
        }

        // "--criar-item <seção> <índice base> <nome> <saida>": cria um item novo a partir do base (use MUCHILA_ROOT/MUCHILA_CLIENTE
        // de CÓPIAS para testar; sem eles, mexe no servidor e no cliente de verdade, como o botão do painel).
        if (args.Length == 5 && args[0] == "--criar-item")
        {
            try { var i = NewItems.Create(int.Parse(args[1]), int.Parse(args[2]), args[3], new Dictionary<string, int>()); File.WriteAllText(args[4], $"OK: {i.Name} = {i.Section},{i.Index}"); return 0; }
            catch (Exception ex) { File.WriteAllText(args[4], "FALHA: " + ex.Message); return 1; }
        }

        // "--testar-itens-novos <saida>": contra MUCHILA_ROOT (cópia com Data\Item\Item.txt e MuChilaAdmin\) e MUCHILA_CLIENTE
        // (cópia com Data\Local\{Eng,Por}\item_*.bmd, itemtooltip_*.bmd e Data\Local\ItemTRSData.bmd): cria e remove itens.
        if (args.Length == 2 && args[0] == "--testar-itens-novos")
        {
            var sb = new StringBuilder(); int f = 0;
            void C(string n, bool ok, string x = "") { if (!ok) f++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {n} {x}"); }
            try
            {
                if (!ServerControl.ServerRoot.Contains("Temp", StringComparison.OrdinalIgnoreCase) || !NewItems.ClientDir.Contains("Temp", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("MUCHILA_ROOT e MUCHILA_CLIENTE precisam ser cópias de teste.");
                var arquivos = Directory.GetFiles(NewItems.ClientDir, "*.bmd", SearchOption.AllDirectories).Append(Path.Combine(ServerControl.ServerRoot, @"Data\Item\Item.txt")).ToList();
                var orig = arquivos.ToDictionary(a => a, File.ReadAllBytes);
                C("cliente: regravar sem mudança sai idêntico (itens, tooltips, TRS)", NewItems.RoundTripIdentical());

                var i1 = NewItems.Create(0, 0, "Espada do Teste", new Dictionary<string, int> { ["DamageMin"] = 77, ["DamageMax"] = 99, ["ReqStrength"] = 123 });
                var s1 = NewItems.ServerItems().FirstOrDefault(i => i.Section == 0 && i.Index == i1.Index);
                C("servidor: linha nova com nome e atributos", s1 != null && s1.Name == "Espada do Teste" && s1.Values["DamageMin"] == "77" && s1.Values["DamageMax"] == "99" && s1.Values["ReqStrength"] == "123" && s1.Values["ReqDexterity"] == "40", $"(índice {i1.Index})");
                C("servidor: o resto igual ao item base", s1 != null && s1.Values["Width"] == "1" && s1.Values["Height"] == "2" && s1.Values["AttackSpeed"] == "50");
                C("cliente: arquivos novos abrem com checksum certo e o item está lá", NewItems.RoundTripIdentical() && NewItems.NextFreeIndex(0) == i1.Index + 1);
                C("catálogo do painel (lojas/drops) enxerga o item novo", Shops.Name(0, i1.Index) == "Espada do Teste");

                var i2 = NewItems.Create(7, 0, "Elmo do Teste", new Dictionary<string, int> { ["Defense"] = 55 });
                C("segunda seção (elmo)", NewItems.ServerItems().Any(i => i.Section == 7 && i.Index == i2.Index && i.Values["Defense"] == "55") && NewItems.List().Count == 2);

                NewItems.Remove(7, i2.Index); NewItems.Remove(0, i1.Index);
                var dif = arquivos.Where(a => !File.ReadAllBytes(a).SequenceEqual(orig[a])).Select(Path.GetFileName).ToList();
                C("remover os dois devolve todos os arquivos iguais aos originais", dif.Count == 0, string.Join(", ", dif));
                C("lista de itens novos vazia", NewItems.List().Count == 0);
                bool recusou = false; try { NewItems.Remove(0, 0); } catch (InvalidOperationException) { recusou = true; }
                C("não deixa remover item do kit", recusou);
            }
            catch (Exception ex) { f++; sb.AppendLine("FALHA inesperada: " + ex); }
            sb.AppendLine($"{f} falha(s)"); File.WriteAllText(args[1], sb.ToString());
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

    static int TestPass(string output)
    {
        var sb = new StringBuilder();
        int failures = 0;
        void Check(string name, bool ok, string extra = "") { if (!ok) failures++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {name} {extra}"); }
        const string acc = "passeteste";
        void Limpar() => Db.Execute("DELETE dbo.MuChila_PasseMapasLog WHERE Conta = @a; DELETE dbo.MuChila_PasseMapas WHERE Conta = @a; DELETE dbo.MEMB_INFO WHERE memb___id = @a", ("@a", acc));
        try
        {
            Limpar();
            Db.Execute("INSERT dbo.MEMB_INFO (memb___id, memb__pwd, memb_name, sno__numb, mail_addr, bloc_code, ctl1_code, appl_days) " +
                       "VALUES (@a, 'teste1', 'teste', '1', 'teste@exemplo.com', '0', '0', GETDATE())", ("@a", acc));
            var r1 = PassMaps.Give(acc, 5, 0);
            var e1 = PassMaps.Expiry(acc);
            Check("5 minutos a partir de agora", e1 != null && Math.Abs((e1.Value - DateTime.Now.AddMinutes(5)).TotalSeconds) < 5, r1);
            var r2 = PassMaps.Give(acc, 1, 2);
            var e2 = PassMaps.Expiry(acc);
            Check("+1 dia soma ao vencimento", e1 != null && e2 != null && Math.Abs((e2.Value - e1.Value.AddDays(1)).TotalSeconds) < 2, r2);
            Check("aparece como ativo na lista", PassMaps.Passes().Select($"Conta = '{acc}' AND Falta <> 'vencido'").Length == 1);
            var r3 = PassMaps.Remove(acc);
            Check("tirar vence agora", PassMaps.Expiry(acc) is { } e3 && e3 <= DateTime.Now.AddSeconds(1), r3);
            Check("tirar de novo avisa que não tem passe", PassMaps.Remove(acc).Contains("não tem passe ativo"));
            Check("histórico com as 3 ações", PassMaps.History(500).Select($"Conta = '{acc}'").Length == 3);
            try { PassMaps.Give("naoexiste__", 1, 2); Check("conta inexistente é recusada", false); }
            catch (InvalidOperationException ex) { Check("conta inexistente é recusada", true, ex.Message); }
            Check("lista de mapas (12 acima do 400)", PassMaps.Maps().Rows.Count == 12, $"({PassMaps.Maps().Rows.Count})");
        }
        catch (Exception ex) { failures++; sb.AppendLine("FALHA inesperada: " + ex); }
        finally { try { Limpar(); sb.AppendLine("limpeza: conta passeteste, passe e histórico apagados"); } catch (Exception ex) { sb.AppendLine("FALHA na limpeza: " + ex.Message); } }
        sb.AppendLine($"resultado: {failures} falha(s)");
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
        Check("Bônus", () => { var n = DateTime.Now; var l = TimedBonuses.List(); return $"{l.Count} bônus na lista, {l.Count(b => b.ActiveAt(n))} ativo(s)"; });
        Check("Avisos", () => $"{Notices.Load().Count} aviso(s) no Notice.txt");
        Check("Loja de Cash", () => { var l = CashShop.List(); return $"{l.Count} pacotes ({l.Count(p => p.HasClient)} com tela no cliente){(CashShop.ClientAvailable ? "" : "; cliente não encontrado")}"; });
        Check("Monstros (respawn)", () => { var m = Monsters.Maps(); return $"{m.Count} mapas, {m.Sum(x => x.Total)} monstros no total"; });
        Check("Comandos", () => $"{Commands.All.Count} comandos catalogados");
        Check("Drops", () => $"{Drops.Load().Count} regras de item, {Drops.MonsterRates().Count} monstros com taxas");
        Check("Invasão no ar", () => ServerControl.InvasionEnd(DateTime.Now) is { } fim ? $"até {fim:HH:mm:ss}" : "nenhuma");
        Check("Vigia do /reset", () => ResetWatcher.IsRunning() ? "rodando" : "parado");
        Check("Passe dos Mapas", () => $"{PassMaps.Maps().Rows.Count} mapas, {PassMaps.Passes().Select("Falta <> 'vencido'").Length} conta(s) com passe ativo, cobrança {(PassMaps.Enforced ? "ligada" : "desligada")}");
        Check("MuEditor", () => File.Exists(ServerControl.MuEditorPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.MuEditorPath));
        Check("Launcher", () => File.Exists(ServerControl.LauncherPath) ? "encontrado" : throw new FileNotFoundException(ServerControl.LauncherPath));

        File.WriteAllText(output, sb.ToString());
        return failures;
    }
}

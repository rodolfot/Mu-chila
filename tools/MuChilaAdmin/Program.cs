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

        // "--invasao <índice> <segundos> <saida>": faz a invasão começar daqui a N segundos em todos os GameServers de
        // MUCHILA_ROOT (o mesmo que o "Disparar evento", só a parte da memória). "--invasao-ler <índice> <saida>": só lê o estado.
        if ((args.Length == 4 && args[0] == "--invasao") || (args.Length == 3 && args[0] == "--invasao-ler"))
        {
            try
            {
                int idx = int.Parse(args[1]);
                var linhas = args[0] == "--invasao"
                    ? ResetWatcher.InvasionStartAt(idx, DateTime.Now.AddSeconds(int.Parse(args[2])))
                    : ResetWatcher.InvasionRead(idx).Select(s => s == null ? "(não achado)" : $"{s.Server}: estado {s.State}, alvo {s.Target:dd/MM HH:mm:ss}").ToList();
                File.WriteAllLines(args[^1], linhas);
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[^1], "FALHA: " + ex.Message); return 1; }
        }

        // "--testar-opcoes-visao <saida> <foto.png>": aba "Taxas e opções" com os dados reais; só simula na tela e tira fotos
        if (args.Length == 3 && args[0] == "--testar-opcoes-visao")
        {
            try
            {
                Application.EnableVisualStyles();
                using var f = new MainForm();
                var r = f.TestSettingsView(args[2]);
                File.WriteAllText(args[1], r);
                return r.Contains("FALHA") ? 1 : 0;
            }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex); return 1; }
        }

        // "--avisos-reiniciar <saida>": reinicia o rodízio de avisos (Notice.txt) em todos os GameServers e no Castle Siege,
        // sem mandar nada na hora (o 1º aviso sai depois do intervalo dele). Serve para conferir a rotina achada (#31).
        if ((args.Length == 2 || args.Length == 3) && args[0] == "--avisos-reiniciar")
        {
            int linha = args.Length == 3 ? int.Parse(args[2]) - 1 : 0;   // [linha] = 1ª, 2ª... (padrão 1ª)
            try { File.WriteAllText(args[1], string.Join(Environment.NewLine, ResetWatcher.NoticeRestart(linha, false, null))); return 0; }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex); return 1; }
        }

        // "--testar-passe <saida>": dá, soma e tira passe de uma conta descartável (passeteste), criada e apagada pelo teste
        if (args.Length == 2 && args[0] == "--testar-passe")
            return TestPass(args[1]);

        // "--testar-lojaitens <saida>": loja de itens do site numa CÓPIA (MUCHILA_ROOT): grava sem mudar nada e confere que tudo
        // volta igual, muda preços e confere, recusa item inexistente e preço negativo, e devolve o arquivo original (byte a byte)
        if (args.Length == 2 && args[0] == "--testar-lojaitens")
            return TestLojaItens(args[1]);

        // "--testar-lojaitens-visao <saida> <foto.png>": aba "Loja de itens" com o arquivo real; só simula na tela e tira fotos
        if (args.Length == 3 && args[0] == "--testar-lojaitens-visao")
        {
            try
            {
                Application.EnableVisualStyles();
                using var f = new MainForm();
                var r = f.TestLojaItensView(args[2]);
                File.WriteAllText(args[1], r);
                return r.Contains("FALHA") ? 1 : 0;
            }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex); return 1; }
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
                var divergentes = lista.Where(p => p.Options.Count > 0 && p.Price != p.Options[0].Price).Select(p => (p.Category, p.Main)).ToHashSet();
                var alvo = lista.FirstOrDefault(p => p.HasClient && p.Options.Count <= 1) ?? lista.First();   // com várias opções o preço vem das opções
                int original = alvo.Price;
                alvo.Price = int.Parse(args[1]);
                var msg = CashShop.Save(lista);
                var depois = CashShop.List();
                var mudou = depois.First(p => p.Category == alvo.Category && p.Main == alvo.Main).Price;
                // pacote cuja vitrine já divergia da 1ª opção (ex.: Panda Ring (C) com 0 em 29/09) volta a mostrar o preço cobrado
                bool resto = depois.Where(p => !(p.Category == alvo.Category && p.Main == alvo.Main) && !divergentes.Contains((p.Category, p.Main)))
                    .All(p => antes.Any(a => a.Category == p.Category && a.Main == p.Main && a.Price == p.Price));
                File.WriteAllText(args[2], $"{msg}\r\npacotes: {lista.Count} -> {depois.Count}\r\nalvo {alvo.Name} [{alvo.Category},{alvo.Main}] {original} -> {mudou} (esperado {args[1]})\r\ndemais preços intactos: {resto}");
                return mudou == int.Parse(args[1]) && depois.Count == lista.Count && resto ? 0 : 1;
            }
            catch (Exception ex) { File.WriteAllText(args[2], "FALHA: " + ex.Message); return 1; }
        }

        // "--testar-opcoes <arquivo>": aba "Taxas e opções" numa CÓPIA (MUCHILA_ROOT): lê os 7 arquivos, grava uma opção por
        // plano e uma única em todas as pastas, confere que só aquelas linhas mudaram, recusa as travadas e desfaz (byte a byte).
        if (args.Length == 2 && args[0] == "--testar-opcoes")
        {
            var log = new List<string>(); int falhas = 0;
            void Check(bool ok, string what) { log.Add((ok ? "ok     " : "FALHA  ") + what); if (!ok) falhas++; }
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(args[1], "ERRO: use MUCHILA_ROOT apontando para uma cópia de testes"); return 1; }
                var folders = ServerSettings.ServerFolders();
                Check(folders.Count >= 3, $"{folders.Count} pastas GameServer*");
                foreach (var f in ServerSettings.Files)
                {
                    var rows = ServerSettings.Load(f.Name);
                    Check(rows.Count > 0, $"{f.Name}: {rows.Count} linhas ({rows.Count(r => r.PerPlan)} por plano, {rows.Count(r => r.LockReason != null)} travadas)");
                }
                var main = ServerSettings.Main();
                Check(main.Count > 30 && main.Any(r => r.File == ServerSettings.ItemDropFile), $"Principais: {main.Count} linhas, {main.Count(r => r.File == ServerSettings.ItemDropFile)} de drop de joias");
                string Snap(string file) => string.Join("\n---\n", folders.Select(d => File.ReadAllText(Path.Combine(d, "DATA", $"GameServerInfo - {file}.dat"), System.Text.Encoding.Latin1)));
                var common = ServerSettings.Load("Common");
                var antes = Snap("Common");
                var drop = common.First(r => r.BaseKey == "ItemDropRate"); var tempo = common.First(r => r.BaseKey == "ItemDropTime");
                var ch = new List<ServerSettings.Change> { new("Common", "ItemDropRate_AL2", "77", drop), new("Common", "ItemDropTime", "31", tempo) };
                log.Add(ServerSettings.Save("Common", ch));
                var depois = ServerSettings.Load("Common");
                Check(depois.First(r => r.BaseKey == "ItemDropRate").Values[2] == "77" && depois.First(r => r.BaseKey == "ItemDropTime").Values[0] == "31", "valores novos lidos de volta");
                int linhas = 0;
                foreach (var d in folders)
                {
                    var p = Path.Combine(d, "DATA", "GameServerInfo - Common.dat");
                    var a = File.ReadAllLines(Directory.GetFiles(Path.GetDirectoryName(p)!, "GameServerInfo - Common.dat.bak-*").OrderBy(x => x).Last(), System.Text.Encoding.Latin1);
                    var b = File.ReadAllLines(p, System.Text.Encoding.Latin1);
                    linhas += a.Zip(b).Count(x => x.First != x.Second);
                    Check(a.Length == b.Length, $"{Path.GetFileName(d)}: mesmo número de linhas");
                }
                Check(linhas == 2 * folders.Count, $"só 2 linhas mudaram em cada pasta ({linhas} no total)");
                string Erro(Action a) { try { a(); return ""; } catch (InvalidOperationException ex) { return ex.Message; } }
                var nome = common.First(r => r.BaseKey == "ServerName");
                Check(Erro(() => ServerSettings.Save("Common", new[] { new ServerSettings.Change("Common", "ServerName", "x", nome) })).Contains("não pode"), "recusa opção travada (ServerName)");
                Check(Erro(() => ServerSettings.Save("Common", new[] { new ServerSettings.Change("Common", "ItemDropTime", "abc", tempo) })).Contains("números"), "recusa texto em opção numérica");
                ServerSettings.Save("Common", new[] { new ServerSettings.Change("Common", "ItemDropRate_AL2", drop.Values[2], depois.First(r => r.BaseKey == "ItemDropRate")),
                                                     new ServerSettings.Change("Common", "ItemDropTime", tempo.Values[0], depois.First(r => r.BaseKey == "ItemDropTime")) });
                Check(Snap("Common") == antes, "desfeito: arquivos iguais aos de antes, byte a byte");
            }
            catch (Exception ex) { log.Add("FALHA  exceção: " + ex); falhas++; }
            log.Add(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: {falhas} falha(s)");
            File.WriteAllLines(args[1], log);
            return falhas == 0 ? 0 : 1;
        }

        // "--testar-opcoes-aplicar <arquivo>": o ciclo inteiro do botão (grava, Reload, confere na memória) no GameServer de
        // testes (MUCHILA_ROOT=C:\MuServerTeste, ligado): ItemDropRate_AL1 deve valer na hora e WriteChatLog só ao reiniciar.
        // Desfaz no fim.
        if (args.Length == 2 && args[0] == "--testar-opcoes-aplicar")
        {
            var log = new List<string>();
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(args[1], "ERRO: use MUCHILA_ROOT apontando para o servidor de testes"); return 1; }
                var common = ServerSettings.Load("Common");
                var drop = common.First(r => r.BaseKey == "ItemDropRate"); var chat = common.First(r => r.BaseKey == "WriteChatLog");
                int d1 = int.Parse(drop.Values[1]); int c0 = int.Parse(chat.Values[0]);
                log.Add("== aplicando");
                ServerSettings.SaveAndApply(new[] { new ServerSettings.Change("Common", "ItemDropRate_AL1", (d1 + 1).ToString(), drop), new ServerSettings.Change("Common", "WriteChatLog", (1 - c0).ToString(), chat) },
                                            Array.Empty<(ServerSettings.Row, string)>(), log.Add);
                common = ServerSettings.Load("Common");
                log.Add("== desfazendo");
                ServerSettings.SaveAndApply(new[] { new ServerSettings.Change("Common", "ItemDropRate_AL1", d1.ToString(), common.First(r => r.BaseKey == "ItemDropRate")),
                                                    new ServerSettings.Change("Common", "WriteChatLog", c0.ToString(), common.First(r => r.BaseKey == "WriteChatLog")) },
                                            Array.Empty<(ServerSettings.Row, string)>(), log.Add);
            }
            catch (Exception ex) { log.Add("FALHA: " + ex); }
            File.WriteAllLines(args[1], log);
            return 0;
        }

        // "--conferir-memoria <arquivo>": só LÊ a memória dos GameServers reais e compara cada opção numérica dos 7 arquivos com
        // o valor do arquivo (mostra quantas o painel consegue conferir e quais diferem: ou só valem ao reiniciar, ou o
        // servidor converte o valor).
        if (args.Length == 2 && args[0] == "--conferir-memoria")
        {
            var log = new List<string>();
            try
            {
                foreach (var f in ServerSettings.Files)
                {
                    var rows = ServerSettings.Load(f.Name).Where(r => r.IsNumeric && r.LockReason == null).ToList();
                    var keys = rows.SelectMany(r => Enumerable.Range(0, r.PerPlan ? 4 : 1).Select(i => (Key: r.KeyOf(i), Value: int.Parse(r.Values[i])))).ToList();
                    var mem = ResetWatcher.ConfigRead(keys.Select(k => k.Key).ToList());
                    foreach (var (server, v) in mem)
                    {
                        var achadas = keys.Where(k => v.GetValueOrDefault(k.Key) != null).ToList();
                        var diferentes = achadas.Where(k => v[k.Key] != k.Value).ToList();
                        log.Add($"{f.Name} @ {server}: {keys.Count} opções, {achadas.Count} achadas na memória, {achadas.Count - diferentes.Count} iguais ao arquivo, {diferentes.Count} diferentes");
                        foreach (var d in diferentes.Take(15)) log.Add($"      {d.Key}: arquivo {d.Value}, memória {v[d.Key]}");
                    }
                }
            }
            catch (Exception ex) { log.Add("FALHA: " + ex); }
            File.WriteAllLines(args[1], log);
            return 0;
        }

        // "--testar-exp-dinamica <arquivo>": EXP dinâmica (#37) numa CÓPIA (MUCHILA_ROOT com Data\Util\ExperienceTable.txt):
        // regravar igual não muda nada, editar/adicionar faixa grava e relê, sobreposição é recusada, e desfaz no fim.
        if (args.Length == 2 && args[0] == "--testar-exp-dinamica")
        {
            var log = new List<string>(); int falhas = 0;
            void Check(bool ok, string what) { log.Add((ok ? "ok     " : "FALHA  ") + what); if (!ok) falhas++; }
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(args[1], "ERRO: use MUCHILA_ROOT apontando para uma cópia de testes"); return 1; }
                var orig = File.ReadAllBytes(DynamicExp.FilePath);
                var (b0, fora) = DynamicExp.Load();
                Check(b0.Count > 0, $"leu {b0.Count} faixas ({string.Join("; ", b0)}), {fora} fora do bloco");
                Check(DynamicExp.Save(b0).Contains("nada mudou") && File.ReadAllBytes(DynamicExp.FilePath).SequenceEqual(orig), "regravar sem mudança não mexe no arquivo");
                var b1 = DynamicExp.Load().Bands; b1[1].Rate = 90; b1.RemoveAt(b1.Count - 1);
                b1.Add(new DynamicExp.Band { LevelMin = 400, LevelMax = 400, Rate = 50 });
                log.Add(DynamicExp.Save(b1));
                var b2 = DynamicExp.Load().Bands;
                Check(b2.Count == b0.Count && b2[1].Rate == 90 && b2[^1].Rate == 50 && b2.Select(b => b.LevelMin).SequenceEqual(b2.Select(b => b.LevelMin).OrderBy(x => x)), "editar e trocar faixa: grava, relê e fica em ordem de nível");
                var txt = File.ReadAllText(DynamicExp.FilePath, System.Text.Encoding.Latin1);
                Check(txt.EndsWith("\r\nend") && txt.Contains("51\t100\t0\t600\t0\t10000\t0\t10000\t90\r\n"), "formato: TAB entre colunas, CRLF, termina em end");
                string Erro(Action a) { try { a(); return ""; } catch (InvalidOperationException ex) { return ex.Message; } }
                var b3 = DynamicExp.Load().Bands; b3.Add(new DynamicExp.Band { LevelMin = 40, LevelMax = 60, Rate = 5 });
                Check(Erro(() => DynamicExp.Save(b3)).Contains("sobrepõem"), "recusa faixa sobreposta (40–60 com 1–50 e 51–100)");
                var b4 = DynamicExp.Load().Bands; b4[0].LevelMin = 60;
                Check(Erro(() => DynamicExp.Save(b4)).Contains("menor ou igual"), "recusa \"de\" maior que \"até\"");
                Check(DynamicExp.Gaps(new[] { new DynamicExp.Band { LevelMin = 1, LevelMax = 100 }, new DynamicExp.Band { LevelMin = 201, LevelMax = 399 } }) == "101–200, 400", "mostra os níveis sem faixa");
                DynamicExp.Save(b0);
                Check(DynamicExp.Load().Bands.Select(b => b.ToString()).SequenceEqual(b0.Select(b => b.ToString())), "voltar às faixas originais");
                File.WriteAllBytes(DynamicExp.FilePath, orig);
            }
            catch (Exception ex) { log.Add("FALHA  exceção: " + ex); falhas++; }
            log.Add(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: {falhas} falha(s)");
            File.WriteAllLines(args[1], log);
            return falhas == 0 ? 0 : 1;
        }

        // "--testar-exp-visao <saida> <foto.png>": aba "EXP dinâmica" com os dados reais; só simula na tela e tira foto
        if (args.Length == 3 && args[0] == "--testar-exp-visao")
        {
            try
            {
                Application.EnableVisualStyles();
                using var f = new MainForm();
                var r = f.TestDynamicExpView(args[2]);
                File.WriteAllText(args[1], r);
                return r.Contains("FALHA") ? 1 : 0;
            }
            catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex); return 1; }
        }

        // "--colocar-item <conta> <personagem | bau> <seção> <índice> <nível> <quantidade> <arquivo>": o mesmo que o botão
        // "Colocar item..." da aba Itens e baú (conta fora do jogo, backup antes).
        if (args.Length == 8 && args[0] == "--colocar-item")
        {
            try
            {
                File.WriteAllText(args[7], Items.Place(args[1], args[2] == "bau" ? null : args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]),
                                                        false, false, 0, 0, int.Parse(args[6])));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[7], "FALHA: " + ex.Message); return 1; }
        }

        // "--testar-cashshop-precos <arquivo>": preço por OPÇÃO (o que o jogo mostra e cobra), em cópias (MUCHILA_ROOT e
        // MUCHILA_CLIENTE): Panda Ring (C) 150/700 sem mexer no (P), vitrine e descrição juntas, produto próprio quando era
        // compartilhado, pacote de opção única pela coluna Preço, e 2ª edição no mesmo produto (sem nova cópia).
        if (args.Length == 2 && args[0] == "--testar-cashshop-precos")
        {
            var log = new List<string>(); int falhas = 0;
            void Check(bool ok, string what) { log.Add((ok ? "ok     " : "FALHA  ") + what); if (!ok) falhas++; }
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null || Environment.GetEnvironmentVariable("MUCHILA_CLIENTE") == null)
                { File.WriteAllText(args[1], "ERRO: use MUCHILA_ROOT e MUCHILA_CLIENTE apontando para cópias de testes"); return 1; }
                var t0 = CashShop.TestLines();
                var l = CashShop.List();
                var pc = l.First(p => p.Name == "Panda Ring" && p.CoinIndex == 508); var pp = l.First(p => p.Name == "[Panda Ring]" && p.CoinIndex == 509);
                Check(pc.Options.Count == 2 && pc.Options.All(o => o.Shared) && pp.Options.Select(o => o.Main).SequenceEqual(pc.Options.Select(o => o.Main)),
                      $"Panda Ring: 2 opções ({CashOptionsTextForTest(pc)}), as mesmas do [Panda Ring] da aba (P)");
                var pOld = pp.Options.Select(o => o.Price).ToList();
                pc.Options[0].Price = 150; pc.Options[1].Price = 700;
                log.Add(CashShop.Save(l));
                var t1 = CashShop.TestLines(); var l1 = CashShop.List();
                var pc1 = l1.First(p => p.Category == pc.Category && p.Main == pc.Main); var pp1 = l1.First(p => p.Category == pp.Category && p.Main == pp.Main);
                Check(pc1.Options.Select(o => o.Price).SequenceEqual(new[] { 150, 700 }) && pc1.Options.All(o => !o.Shared), $"(C) agora {CashOptionsTextForTest(pc1)}, produto próprio");
                Check(pp1.Options.Select(o => o.Price).SequenceEqual(pOld) && pp1.Options.All(o => !o.Shared), $"(P) continua {CashOptionsTextForTest(pp1)}");
                var sp = t1.SrvPkg.Single(c => c[0] == $"{pc.Category}" && c[2] == $"{pc.Main}"); var cp = t1.CliPkg.Single(c => c[0] == $"{pc.Category}" && c[2] == $"{pc.Main}");
                Check(sp[5] == "150" && cp[5] == "150", $"vitrine (preço do pacote) = 1ª opção: servidor {sp[5]}, cliente {cp[5]}");
                Check(cp[6].Contains("150 W Coin - 1Day") && cp[6].Contains("700 W Coin - 7Day"), $"descrição: {cp[6]}");
                Check(t1.CliPkg.Single(c => c[0] == $"{pp.Category}" && c[2] == $"{pp.Main}")[6].Contains("100 W Coin - 1Day"), "descrição do (P) não mudou");
                Check(cp[23] == string.Concat(pc1.Options.Select(o => o.Main + "|")) && pc1.Options.All(o => t1.CliProd.Count(r => r[6] == $"{o.Main}" && r[5] == $"{o.Price}") >= 1),
                      $"cliente aponta para os produtos novos ({cp[23]}) com o preço novo");
                Check(t1.SrvProd.Count == t0.SrvProd.Count + 2 && t1.CliProd.Count == t0.CliProd.Count + 6, $"produtos: servidor +{t1.SrvProd.Count - t0.SrvProd.Count}, cliente +{t1.CliProd.Count - t0.CliProd.Count} linhas");

                // pacote de uma opção, pela coluna Preço (como a grade faz)
                var um = l1.First(p => p.Options.Count == 1 && !p.Hidden && p.HasClient && p.CoinIndex == 508);
                int umOld = um.Options[0].Price; um.Price = umOld + 11;
                log.Add(CashShop.Save(l1));
                var l2 = CashShop.List(); var um2 = l2.First(p => p.Category == um.Category && p.Main == um.Main);
                Check(um2.Options[0].Price == umOld + 11 && um2.Price == umOld + 11, $"{um.Name}: opção única {umOld} -> {um2.Options[0].Price} (pacote {um2.Price})");

                // 2ª edição do Panda (C): produto já é só dele -> muda no lugar
                var t2 = CashShop.TestLines();
                l2.First(p => p.Category == pc.Category && p.Main == pc.Main).Options[1].Price = 800;
                CashShop.Save(l2);
                var t3 = CashShop.TestLines(); var pc3 = CashShop.List().First(p => p.Category == pc.Category && p.Main == pc.Main);
                Check(t3.SrvProd.Count == t2.SrvProd.Count && pc3.Options[1].Price == 800 && t3.CliPkg.Single(c => c[0] == $"{pc.Category}" && c[2] == $"{pc.Main}")[6].Contains("800 W Coin - 7Day"),
                      "2ª edição muda no mesmo produto (sem nova cópia) e na descrição");
            }
            catch (Exception ex) { log.Add("FALHA  exceção: " + ex); falhas++; }
            log.Add(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: {falhas} falha(s)");
            File.WriteAllLines(args[1], log);
            return falhas == 0 ? 0 : 1;
        }

        // "--cash-adicionar <aba> <seção> <índice> <qtd> <dias> <preço> <nome> <arquivo>" e "--cash-apagar <aba> <main> <arquivo>":
        // o mesmo que os botões da aba Loja de Cash, para validar no servidor de testes. Só com MUCHILA_ROOT (cópia).
        if ((args.Length == 9 && args[0] == "--cash-adicionar") || (args.Length == 4 && args[0] == "--cash-apagar"))
        {
            var saida = args[^1];
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(saida, "ERRO: use MUCHILA_ROOT apontando para uma cópia de testes"); return 1; }
                File.WriteAllText(saida, args[0] == "--cash-apagar"
                    ? CashShop.RemoveAdded(int.Parse(args[1]), int.Parse(args[2]))
                    : CashShop.Add(new(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]), 0, false, false, 0, 0, int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]), args[7], "")));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(saida, "FALHA: " + ex.Message); return 1; }
        }

        // "--testar-cashshop-adicionar <arquivo>": cria pacotes (por quantidade, por prazo, Goblin), confere as 4 peças de
        // cada um, os erros esperados, muda o preço, esconde, apaga e confere que os arquivos voltaram byte a byte.
        // Só em cópias: MUCHILA_ROOT (servidor) e MUCHILA_CLIENTE (cliente).
        if (args.Length == 2 && args[0] == "--testar-cashshop-adicionar")
        {
            var log = new List<string>(); int falhas = 0;
            void Check(bool ok, string what) { log.Add((ok ? "ok     " : "FALHA  ") + what); if (!ok) falhas++; }
            try
            {
                if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null || Environment.GetEnvironmentVariable("MUCHILA_CLIENTE") == null)
                { File.WriteAllText(args[1], "ERRO: use MUCHILA_ROOT e MUCHILA_CLIENTE apontando para cópias de testes"); return 1; }
                CashShop.Save(CashShop.List());   // normaliza (o painel já regravou os arquivos reais assim)
                var files = CashShop.TestFiles;
                var before = files.ToDictionary(f => f, f => File.Exists(f) ? File.ReadAllText(f, System.Text.Encoding.Latin1) : "");
                var cats = CashShop.Categories();
                Check(cats.Count > 0, $"abas: {string.Join(" | ", cats)}");
                var cC = cats.First(c => c.CoinIndex == 508); var cP = cats.First(c => c.CoinIndex == 509); var cG = cats.First(c => c.CoinIndex == 0);
                int n0 = CashShop.List().Count;

                log.Add(CashShop.Add(new(cC.Id, 14, 13, 0, false, false, 0, 0, 10, 0, 123, "Teste Joia x10", "linha 1\nlinha 2 com acentuação @|#")));
                log.Add(CashShop.Add(new(cP.Id, 13, 44, 0, false, false, 0, 0, 1, 7, 77, "Teste Selo 7 dias", "")));
                log.Add(CashShop.Add(new(cG.Id, 0, 0, 9, true, true, 4, 5, 1, 0, 5, "Teste Kris", "espada")));
                string Erro(Action a) { try { a(); return ""; } catch (InvalidOperationException ex) { return ex.Message; } }
                Check(Erro(() => CashShop.Add(new(cC.Id, 0, 0, 0, false, false, 0, 0, 5, 0, 10, "x", ""))).Contains("não empilha"), "recusa quantidade em item que não empilha");
                Check(Erro(() => CashShop.Add(new(cC.Id, 14, 13, 0, false, false, 0, 0, 5, 3, 10, "x", ""))).Contains("prazo"), "recusa prazo com quantidade > 1");
                Check(Erro(() => CashShop.Add(new(cC.Id, 14, 13, 0, false, false, 0, 0, 999, 0, 10, "x", ""))).Contains("empilha até"), "recusa acima do empilhamento");

                var added = CashShop.AddedList();
                var list = CashShop.List();
                Check(added.Count == 3 && list.Count == n0 + 3, $"3 pacotes novos (lista {n0} -> {list.Count})");
                Check(added.Select(a => a.Main).Distinct().Count() == 3 && added.Select(a => a.ProductMain).Distinct().Count() == 3, "números novos distintos");
                var t = CashShop.TestLines();
                foreach (var (a, i) in added.Select((a, i) => (a, i)))
                {
                    var p = list.First(x => x.Category == a.Category && x.Main == a.Main);
                    Check(p.Added && p.HasClient && p.Price == new[] { 123, 77, 5 }[i], $"{a.Name}: na lista com tela e preço {p.Price}");
                    Check(t.SrvPkg.Count(c => c[0] == $"{a.Category}" && c[2] == $"{a.Main}") == 1, $"{a.Name}: 1 linha de pacote no servidor");
                    var sp = t.SrvProd.Single(c => c[0] == $"{a.ProductBase}" && c[1] == $"{a.ProductMain}");
                    var cp = t.CliPkg.Single(c => c[0] == $"{a.Category}" && c[2] == $"{a.Main}");
                    var cr = t.CliProd.Where(c => c[0] == $"{a.ProductBase}" && c[6] == $"{a.ProductMain}").ToList();
                    var sk = t.SrvPkg.Single(c => c[0] == $"{a.Category}" && c[2] == $"{a.Main}");
                    Check(sk[7] == $"{a.ProductBase}" && sk[17] == $"{a.ProductMain}" && sk[5] == sp[2], $"{a.Name}: pacote do servidor aponta para o produto (preço {sk[5]}/{sp[2]}, moeda {sk[4]})");
                    Check(cp[19] == $"{a.ProductBase}|" && cp[23] == $"{a.ProductMain}|" && cp[5] == sk[5] && cp[25] == sk[4], $"{a.Name}: pacote do cliente casa ({cp[3]} | {cp[6]})");
                    Check(cr.Count >= 1 && cr.All(r => r[5] == sp[2] && r[13] == sp[3]), $"{a.Name}: {cr.Count} linha(s) de produto no cliente, 1ª = {cr.FirstOrDefault()?[2]} {cr.FirstOrDefault()?[3]}");
                    log.Add($"         servidor produto: item {sp[3]} nível {sp[4]} skill {sp[5]} sorte {sp[6]} opção {sp[7]} exc {sp[8]} qtd {sp[17]} prazo {sp[18]}");
                }
                Check(!t.CliPkg.Any(c => c[3].Contains('ç') || c[6].Contains('@')), "sem acento/separador nos textos do cliente");
                Check(t.CliPkg.First(c => c[2] == $"{added[1].Main}")[3].StartsWith('['), "W Coin (P) ganha o [ ] do kit");

                // preço: muda o 1º no painel -> produto acompanha
                var l2 = CashShop.List(); l2.First(p => p.Main == added[0].Main).Price = 150; CashShop.Save(l2);
                t = CashShop.TestLines();
                Check(t.SrvProd.Single(c => c[1] == $"{added[0].ProductMain}")[2] == "150" && t.CliProd.Where(c => c[6] == $"{added[0].ProductMain}").All(c => c[5] == "150")
                      && t.SrvPkg.Single(c => c[2] == $"{added[0].Main}")[5] == "150", "mudar o preço muda pacote e produto (servidor e cliente)");
                // esconder o 2º e apagar escondido
                var l3 = CashShop.List(); l3.First(p => p.Main == added[1].Main).Hidden = true; CashShop.Save(l3);
                Check(!CashShop.TestLines().SrvPkg.Any(c => c[2] == $"{added[1].Main}") && CashShop.List().First(p => p.Main == added[1].Main).Hidden, "esconder um adicionado");
                Check(Erro(() => CashShop.RemoveAdded(list.First(p => !p.Added).Category, list.First(p => !p.Added).Main)).Contains("kit"), "recusa apagar pacote do kit");
                foreach (var a in added) log.Add(CashShop.RemoveAdded(a.Category, a.Main));
                Check(CashShop.List().Count == n0 && CashShop.AddedList().Count == 0, "apagados (lista voltou ao tamanho original)");
                foreach (var f in files)
                {
                    var now = File.Exists(f) ? File.ReadAllText(f, System.Text.Encoding.Latin1) : "";
                    Check(now == before[f], $"{Path.GetFileName(f)} voltou igual");
                }
            }
            catch (Exception ex) { log.Add("FALHA  exceção: " + ex); falhas++; }
            log.Add(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: {falhas} falha(s)");
            File.WriteAllLines(args[1], log);
            return falhas == 0 ? 0 : 1;
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

                // painel e vigia no mesmo arquivo (29/09/2026): cada um só manda na sua parte
                var painel = Notices.Load().Where(n => !n.IsManaged).ToList();       // o painel abre a aba Avisos
                Notices.AddOneShot("Aviso do vigia no meio da edição");               // o vigia mexe no arquivo
                var v = Notices.Load(); v.Add(new Notice { Message = "Bônus ativo: teste", RepeatTime = 300, Comment = $"{TimedBonuses.NoticeTag} teste" }); Notices.Save(v);
                var editar = painel[0]; var textoAntigo = editar.Message; int antigos = painel.Count(n => n.Message == textoAntigo);
                editar.Message = "Mensagem editada no painel";
                painel.Add(new Notice { Message = "Aviso novo do painel", RepeatTime = 120 });
                Notices.Save(painel, userList: true);
                var l3 = Notices.Load(); var lista3 = l3.Where(n => !n.IsManaged).Select(n => n.Message).ToList();
                C("salvar a lista depois que o vigia mexeu: grava a edição e o aviso novo", lista3.Contains("Mensagem editada no painel") && lista3.Contains("Aviso novo do painel") && lista3.Count(m => m == textoAntigo) == antigos - 1);
                C("o que o vigia pôs continua lá", l3.Any(n => n.IsOneShot && n.Message.StartsWith("Aviso do vigia")) && l3.Any(n => n.Comment == $"{TimedBonuses.NoticeTag} teste"));
                C("aviso editado fica no mesmo lugar da lista", lista3.IndexOf("Mensagem editada no painel") == 0 && lista3[^1] == "Aviso novo do painel");
                var longa = "Com VIP você ganha até 80% mais drop e exp. Venha fazer parte da Familia Mu Chila, teste!";
                var p4 = Notices.Load().Where(n => !n.IsManaged).ToList(); p4.Add(new Notice { Message = longa, RepeatTime = 455 }); Notices.Save(p4, userList: true);
                var linhaLonga = File.ReadAllLines(Notices.File_, System.Text.Encoding.Latin1).FirstOrDefault(x => x.Contains("teste!\"")) ?? "";
                C("mensagem longa (89 caracteres): gravada com espaço e lida de volta", Notices.Load().Any(n => n.Message == longa && n.RepeatTime == 455) && linhaLonga.Contains("teste!\" "), $"({longa.Length} caracteres)");
                File.WriteAllText(Notices.File_, File.ReadAllText(Notices.File_, System.Text.Encoding.Latin1).Replace("\"Mensagem editada no painel\"", "\"Mensagem editada no painel\"").Replace("Chila, teste!\" ", "Chila, teste!\""), System.Text.Encoding.Latin1);
                C("linha antiga grudada (\"...\"0) também é lida", Notices.Load().Any(n => n.Message == longa));
                l3 = Notices.Load();
                var semVigia = l3.Where(n => !n.IsManaged).ToList();
                Notices.Save(Notices.Load().Where(n => !n.IsManaged).ToList());   // o vigia grava a parte dele (sem mudança) com a lista do painel velha
                C("gravar a parte do vigia não desfaz a lista do painel", Notices.Load().Where(n => !n.IsManaged).Select(n => n.Message).SequenceEqual(semVigia.Select(n => n.Message)));
                File.WriteAllBytes(Notices.File_, orig);
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

    static int TestLojaItens(string output)
    {
        var sb = new StringBuilder();
        int failures = 0;
        void Check(string name, bool ok, string extra = "") { if (!ok) failures++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {name} {extra}"); }
        if (Environment.GetEnvironmentVariable("MUCHILA_ROOT") == null) { File.WriteAllText(output, "ERRO: use MUCHILA_ROOT apontando para uma cópia de testes"); return 1; }
        var arquivo = LojaItens.Arquivo;
        var originais = LojaItens.Arquivos.ToDictionary(f => f, File.ReadAllBytes);
        string Resumo(LojaItens.Config c) => System.Text.Json.JsonSerializer.Serialize(new
        {
            c.Ativo, c.NivelMax, c.ExcMax, c.Sorte, c.Skill, c.Nivel, c.Adicional, c.Excelente,
            Cats = c.Categorias.Select(x => new { x.Id, x.Nome, x.Grupo, x.Icone, x.Ativo, Itens = x.Itens.Select(i => new { i.Secao, i.Tipo, i.Preco, i.NivelMax, i.Exc, i.Ativo, i.Destaque }) }),
        });
        try
        {
            var antes = LojaItens.Carregar();
            Check("leu o catálogo", antes.Categorias.Count > 0 && antes.Categorias.Sum(c => c.Itens.Count) > 0, $"({antes.Categorias.Count} categorias, {antes.Categorias.Sum(c => c.Itens.Count)} itens)");
            Check("o arquivo de hoje passa na validação", LojaItens.Validar(antes).Count == 0, string.Join(" | ", LojaItens.Validar(antes)));
            sb.AppendLine("     " + LojaItens.Salvar(antes));
            var relido = LojaItens.Carregar();
            Check("gravar sem mudar nada mantém catálogo e preços", Resumo(relido) == Resumo(antes));
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(arquivo))!.AsObject();
            Check("mantém o que o painel não conhece (_comentario)", json["_comentario"] != null);

            relido.Excelente[6] = 777; relido.Sorte = 33; relido.Categorias[0].Itens[0].Preco = 99; relido.Categorias[0].Itens[0].Destaque = true; relido.Categorias[0].Itens[0].Exc = "nenhuma";
            LojaItens.Salvar(relido);
            var mudou = LojaItens.Carregar();
            Check("preços e item alterados voltam como gravados", mudou.Excelente[6] == 777 && mudou.Sorte == 33 && mudou.Categorias[0].Itens[0].Preco == 99
                && mudou.Categorias[0].Itens[0].Destaque && mudou.Categorias[0].Itens[0].Exc == "nenhuma");
            Check("conta do preço igual à do site (40 + nível 15 + adicional 7 + sorte + 6 exc)",
                mudou.Preco(40, 15, 7, true, false, 6) == 40 + mudou.Nivel[15] + mudou.Adicional[7] + 33 + 777);

            var ruim = LojaItens.Carregar();
            ruim.Categorias[0].Itens.Add(new LojaItens.Item { Secao = 7, Tipo = 499, Preco = 10 });
            ruim.Sorte = -1;
            var erros = LojaItens.Validar(ruim);
            Check("recusa item que não existe e preço negativo", erros.Any(e => e.Contains("7,499")) && erros.Any(e => e.Contains("negativ")), string.Join(" | ", erros));
            bool recusou = false;
            try { LojaItens.Salvar(ruim); } catch (InvalidOperationException) { recusou = true; }
            Check("Salvar não grava nada inválido", recusou && LojaItens.Carregar().Sorte == 33);
        }
        catch (Exception ex) { failures++; sb.AppendLine("FALHA " + ex); }
        finally
        {
            foreach (var (f, bytes) in originais)
            {
                File.WriteAllBytes(f, bytes);
                foreach (var bak in Directory.GetFiles(Path.GetDirectoryName(f)!, Path.GetFileName(f) + ".bak-*")) File.Delete(bak);
            }
        }
        Check($"arquivo(s) original(is) devolvido(s) byte a byte ({originais.Count})", originais.All(o => File.ReadAllBytes(o.Key).SequenceEqual(o.Value)));
        sb.AppendLine($"resultado: {failures} falha(s)");
        File.WriteAllText(output, sb.ToString());
        return failures == 0 ? 0 : 1;
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

    static string CashOptionsTextForTest(CashShop.Package p) => string.Join(" · ", p.Options.Select(o => $"{o.Label}: {o.Price}"));
}

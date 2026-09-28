// Mu Chila - Launcher do cliente (issue #23).
// Instala e atualiza o cliente. Ao abrir: baixa a lista de arquivos do servidor (manifest.txt), confere tamanho e
// SHA-1 de cada arquivo, baixa só o que mudou ou está faltando, se atualiza sozinho e abre o jogo.
// Pasta do jogo: a do próprio launcher se tiver main.exe ao lado (quem já usava assim continua igual); senão a salva
// em %APPDATA%\MuChila\launcher.ini; senão (primeira vez) pergunta onde instalar, baixa tudo para lá, se copia para a
// pasta e cria o atalho "Mu Chila" na área de trabalho. O botão "Pasta do jogo..." troca a pasta depois.
// Escrito em C# 5 para compilar com o csc do .NET Framework 4.x (já vem no Windows 10/11; o jogador não instala nada).
// Compilar: tools\Publicar-Launcher.ps1 (ou csc /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll).
//
// Servidor (padrão): http://26.139.39.123/arquivos/launcher/  — pode trocar com launcher.ini (ao lado do launcher ou
// na pasta do jogo) contendo ServerUrl=...
// Manifesto: 1ª linha "MUCHILA-MANIFEST 1", 2ª "versao=<texto>", depois "sha1<TAB>tamanho<TAB>caminho/relativo".
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Mu Chila Launcher")]
[assembly: System.Reflection.AssemblyProduct("Mu Chila")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]

namespace MuChilaLauncher
{
    class Entry
    {
        public string Hash;
        public long Size;
        public string Path;   // relativo, com '/'
    }

    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // modo silencioso para teste/diagnóstico: MuChilaLauncher.exe --verificar <arquivo-de-saida> [pasta-do-jogo]
            // (sem pasta: a do launcher). Instala/atualiza sem perguntar nada e sem mexer na pasta salva.
            if ((args.Length == 2 || args.Length == 3) && args[0] == "--verificar")
            {
                string pasta = args.Length == 3 ? Path.GetFullPath(args[2]) : AppDomain.CurrentDomain.BaseDirectory;
                try { File.WriteAllText(args[1], "OK: " + new LauncherForm(pasta).Core(null, false)); return 0; }
                catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex.Message); return 1; }
            }
            // teste do atalho: MuChilaLauncher.exe --testar-atalho <alvo.exe> <atalho.lnk>
            if (args.Length == 3 && args[0] == "--testar-atalho")
            {
                try { LauncherForm.CriarAtalho(args[1], args[2]); return 0; }
                catch { return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
            return 0;
        }
    }

    class LauncherForm : Form
    {
        const string DefaultUrl = "http://26.139.39.123/arquivos/launcher/";
        const string SelfName = "MuChilaLauncher.exe";
        const string CacheName = "launcher-cache.txt";

        readonly string exeDir;   // onde o launcher está rodando
        string root;              // pasta do jogo (a do main.exe); null = ainda não escolhida
        string baseUrl;
        readonly Label lblTitle = new Label();
        readonly Label lblStatus = new Label();
        readonly Label lblPasta = new Label();
        readonly ProgressBar bar = new ProgressBar();
        readonly Button btnPlay = new Button();
        readonly Button btnCheck = new Button();
        readonly Button btnPasta = new Button();
        BackgroundWorker worker;
        bool selfUpdating;
        bool posEscolha;          // a pasta acabou de ser escolhida: ao terminar, o launcher se copia para ela (e cria o atalho)
        bool criarAtalho;

        public LauncherForm() : this(DescobrirPasta(AppDomain.CurrentDomain.BaseDirectory)) { }

        public LauncherForm(string pastaDoJogo)
        {
            exeDir = AppDomain.CurrentDomain.BaseDirectory;
            DefinirPasta(pastaDoJogo);

            Text = "Mu Chila - Launcher";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 192);
            Font = new Font("Segoe UI", 9f);

            lblTitle.Text = "Mu Chila";
            lblTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblTitle.SetBounds(16, 10, 420, 34);

            lblStatus.Text = "Preparando...";
            lblStatus.SetBounds(16, 50, 428, 36);

            bar.SetBounds(16, 90, 428, 20);

            btnCheck.Text = "Verificar de novo";
            btnCheck.SetBounds(16, 124, 140, 30);
            btnCheck.Enabled = false;
            btnCheck.Click += delegate { StartCheck(); };

            btnPasta.Text = "Pasta do jogo...";
            btnPasta.SetBounds(164, 124, 140, 30);
            btnPasta.Enabled = false;
            btnPasta.Click += delegate { if (EscolherPasta(false)) StartCheck(); };

            btnPlay.Text = "Jogar";
            btnPlay.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnPlay.SetBounds(324, 124, 120, 30);
            btnPlay.Enabled = false;
            btnPlay.Click += delegate { Play(); };

            lblPasta.ForeColor = Color.DimGray;
            lblPasta.AutoEllipsis = true;
            lblPasta.SetBounds(16, 164, 428, 20);

            Controls.AddRange(new Control[] { lblTitle, lblStatus, bar, btnCheck, btnPasta, btnPlay, lblPasta });
            Shown += delegate
            {
                if (root == null && !EscolherPasta(true)) { Close(); return; }
                StartCheck();
            };
        }

        void DefinirPasta(string pasta)
        {
            root = pasta;
            string u = LerIni(Path.Combine(exeDir, "launcher.ini"), "ServerUrl");
            if (u == null && pasta != null) u = LerIni(Path.Combine(pasta, "launcher.ini"), "ServerUrl");
            baseUrl = u == null ? DefaultUrl : (u.EndsWith("/") ? u : u + "/");
        }

        // ---------------- pasta do jogo ----------------
        static string ConfigPath
        {
            get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuChila"), "launcher.ini"); }
        }

        /// <summary>Pasta do jogo já conhecida, ou null se é a primeira vez.</summary>
        static string DescobrirPasta(string exeDir)
        {
            if (File.Exists(Path.Combine(exeDir, "main.exe"))) return exeDir;
            string salva = LerIni(ConfigPath, "Pasta");
            if (salva != null && Directory.Exists(salva)) return salva;
            return null;
        }

        static string LerIni(string arquivo, string chave)
        {
            try
            {
                if (File.Exists(arquivo))
                    foreach (string l in File.ReadAllLines(arquivo, Encoding.UTF8))
                    {
                        string t = l.Trim();
                        if (t.StartsWith(chave + "=", StringComparison.OrdinalIgnoreCase))
                        {
                            string v = t.Substring(chave.Length + 1).Trim();
                            if (v.Length > 0) return v;
                        }
                    }
            }
            catch { }
            return null;
        }

        static void SalvarPasta(string pasta)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, "Pasta=" + pasta + "\r\n", new UTF8Encoding(false));
        }

        /// <summary>Pasta escolhida no "Procurar": ela mesma se já tem o jogo ou está vazia; senão uma subpasta "Mu Chila".</summary>
        static string PastaDoJogo(string escolhida)
        {
            try
            {
                if (File.Exists(Path.Combine(escolhida, "main.exe"))) return escolhida;
                if (Directory.GetFileSystemEntries(escolhida).Length == 0) return escolhida;
            }
            catch { }
            return Path.Combine(escolhida, "Mu Chila");
        }

        /// <summary>null se dá para instalar na pasta; senão o motivo.</summary>
        static string ValidarPasta(string pasta)
        {
            if (pasta.Length == 0) return "Escolha uma pasta.";
            string full;
            try { full = Path.GetFullPath(pasta).TrimEnd('\\'); }
            catch { return "Esse caminho não é válido."; }
            if (!Path.IsPathRooted(pasta) || full.Length <= 2)
                return "Escolha uma pasta dentro do disco, por exemplo C:\\Jogos\\Mu Chila.";
            foreach (Environment.SpecialFolder sf in new Environment.SpecialFolder[] {
                Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows })
            {
                string b = Environment.GetFolderPath(sf).TrimEnd('\\');
                if (b.Length > 0 && (full + "\\").StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase))
                    return "Não instale dentro de \"" + b + "\": o jogo grava arquivos na própria pasta e o Windows bloqueia isso ali.\r\n"
                         + "Use, por exemplo, C:\\Jogos\\Mu Chila.";
            }
            try
            {
                Directory.CreateDirectory(full);
                string t = Path.Combine(full, "muchila-teste-gravacao.tmp");
                File.WriteAllText(t, "ok");
                File.Delete(t);
            }
            catch (Exception ex) { return "Não consigo gravar nessa pasta: " + ex.Message; }
            return null;
        }

        /// <summary>Janela "onde instalar". true = pasta escolhida (e já salva).</summary>
        bool EscolherPasta(bool primeiraVez)
        {
            string sugestao = root ?? Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Jogos\\Mu Chila");
            using (Form f = new Form())
            {
                f.Text = primeiraVez ? "Mu Chila - Instalação" : "Mu Chila - Pasta do jogo";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.StartPosition = FormStartPosition.CenterScreen;
                f.ClientSize = new Size(460, 196);
                f.Font = Font;

                Label l1 = new Label();
                l1.Text = primeiraVez ? "Onde você quer instalar o Mu Chila?" : "Pasta do jogo (onde fica o main.exe):";
                l1.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                l1.SetBounds(16, 14, 428, 22);
                TextBox txt = new TextBox();
                txt.Text = sugestao;
                txt.SetBounds(16, 45, 330, 24);
                Button procurar = new Button();
                procurar.Text = "Procurar...";
                procurar.SetBounds(354, 43, 90, 27);
                Label l2 = new Label();
                l2.Text = "O jogo inteiro é baixado nessa pasta. Se você já tem o cliente do Mu Chila, escolha a pasta dele "
                        + "(a do main.exe): só o que estiver faltando ou desatualizado é baixado.";
                l2.ForeColor = Color.DimGray;
                l2.SetBounds(16, 78, 428, 46);
                CheckBox chk = new CheckBox();
                chk.Text = "Criar atalho \"Mu Chila\" na área de trabalho";
                chk.Checked = true;
                chk.SetBounds(16, 126, 428, 22);
                Button ok = new Button();
                ok.Text = primeiraVez ? "Instalar" : "OK";
                ok.SetBounds(248, 156, 95, 28);
                Button cancelar = new Button();
                cancelar.Text = "Cancelar";
                cancelar.DialogResult = DialogResult.Cancel;
                cancelar.SetBounds(349, 156, 95, 28);
                f.AcceptButton = ok;
                f.CancelButton = cancelar;

                procurar.Click += delegate
                {
                    using (FolderBrowserDialog fb = new FolderBrowserDialog())
                    {
                        fb.Description = "Escolha onde instalar. Numa pasta com outros arquivos, o jogo vai para uma subpasta \"Mu Chila\".";
                        fb.ShowNewFolderButton = true;
                        try { if (Directory.Exists(txt.Text)) fb.SelectedPath = txt.Text; }
                        catch { }
                        if (fb.ShowDialog(f) == DialogResult.OK) txt.Text = PastaDoJogo(fb.SelectedPath);
                    }
                };
                ok.Click += delegate
                {
                    string erro = ValidarPasta(txt.Text.Trim());
                    if (erro != null) { MessageBox.Show(f, erro, "Mu Chila", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.AddRange(new Control[] { l1, txt, procurar, l2, chk, ok, cancelar });

                if (f.ShowDialog(this) != DialogResult.OK) return false;
                string pasta = Path.GetFullPath(txt.Text.Trim());
                DefinirPasta(pasta);
                SalvarPasta(pasta);
                posEscolha = true;
                criarAtalho = chk.Checked;
                return true;
            }
        }

        /// <summary>Depois de instalar/escolher a pasta: o launcher passa a morar nela, e o atalho aponta para lá.</summary>
        void DepoisDeEscolher()
        {
            posEscolha = false;
            string destino = Path.Combine(root, SelfName);
            try
            {
                string atual = Path.GetFullPath(Application.ExecutablePath);
                if (!string.Equals(atual, Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(atual, destino, true);
                    string ini = Path.Combine(exeDir, "launcher.ini");
                    if (File.Exists(ini) && !File.Exists(Path.Combine(root, "launcher.ini"))) File.Copy(ini, Path.Combine(root, "launcher.ini"));
                }
                if (criarAtalho)
                    CriarAtalho(destino, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Mu Chila.lnk"));
            }
            catch (Exception ex) { lblStatus.Text += "\r\n(Não consegui copiar o launcher / criar o atalho: " + ex.Message + ")"; }
        }

        /// <summary>Atalho .lnk pelo WScript.Shell (por reflexão: o csc do Framework compila sem referência COM).</summary>
        public static void CriarAtalho(string alvo, string lnk)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { alvo });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(alvo) });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Mu Chila: atualiza e abre o jogo" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.FinalReleaseComObject(sc);
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }

        // ---------------- verificação ----------------
        void StartCheck()
        {
            btnPlay.Enabled = false;
            btnCheck.Enabled = false;
            btnPasta.Enabled = false;
            lblPasta.Text = "Pasta do jogo: " + root;
            bar.Value = 0;
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += DoWork;
            worker.ProgressChanged += delegate(object s, ProgressChangedEventArgs e)
            {
                bar.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                if (e.UserState != null) lblStatus.Text = (string)e.UserState;
            };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                if (selfUpdating) { Close(); return; }
                bool temJogo = File.Exists(Path.Combine(root, "main.exe"));
                if (e.Error != null)
                    lblStatus.Text = "Não foi possível atualizar: " + e.Error.Message
                                   + (temJogo ? "\r\nVocê ainda pode jogar com os arquivos atuais." : "\r\nClique em \"Verificar de novo\" para continuar.");
                else if (e.Result != null)
                    lblStatus.Text = (string)e.Result;
                if (e.Error == null && posEscolha) DepoisDeEscolher();
                btnPlay.Enabled = temJogo;
                btnCheck.Enabled = true;
                btnPasta.Enabled = true;
                bar.Value = e.Error == null ? 100 : 0;
            };
            worker.RunWorkerAsync();
        }

        void DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = Core((BackgroundWorker)sender, true);
        }

        static void Report(BackgroundWorker w, int pct, string msg) { if (w != null) w.ReportProgress(pct, msg); }

        /// <summary>Verifica e atualiza. w = null no modo silencioso. Devolve a mensagem final.</summary>
        public string Core(BackgroundWorker w, bool autoAtualizar)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2, para quando o site tiver HTTPS

            Report(w, 0, "Buscando a lista de arquivos do servidor...");
            string versao;
            List<Entry> entries = ReadManifest(out versao);

            // 1) o próprio launcher primeiro: se mudou, baixa e reinicia com a versão nova
            string selfPath = Application.ExecutablePath;
            foreach (Entry en in entries)
            {
                if (!autoAtualizar || !string.Equals(en.Path, SelfName, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(selfPath) && string.Equals(Sha1(selfPath), en.Hash, StringComparison.OrdinalIgnoreCase)) break;
                Report(w, 0, "Atualizando o launcher...");
                string novo = selfPath + ".new";
                Download(en, novo, w, 0, en.Size);
                string bat = Path.Combine(Path.GetDirectoryName(selfPath), "launcher-update.bat");
                File.WriteAllText(bat,
                    "@echo off\r\nping 127.0.0.1 -n 3 > nul\r\n" +
                    "move /y \"" + novo + "\" \"" + selfPath + "\" > nul\r\n" +
                    "start \"\" \"" + selfPath + "\"\r\ndel \"%~f0\"\r\n", Encoding.Default);
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.CreateNoWindow = true;
                Process.Start(psi);
                selfUpdating = true;
                return "Atualizando o launcher...";
            }

            // 2) confere os arquivos (usa o cache de tamanho+data para não recalcular o hash de tudo toda vez)
            Dictionary<string, string[]> cache = ReadCache();
            Dictionary<string, string[]> novoCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            List<Entry> baixar = new List<Entry>();
            int arquivosDoJogo = 0;
            long total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry en = entries[i];
                if (string.Equals(en.Path, SelfName, StringComparison.OrdinalIgnoreCase)) continue;
                arquivosDoJogo++;
                if (i % 200 == 0) Report(w, i * 100 / Math.Max(1, entries.Count), "Verificando arquivos (" + i + " de " + entries.Count + ")...");
                string local = LocalPath(en.Path);
                FileInfo fi = new FileInfo(local);
                string hash = null;
                if (fi.Exists && fi.Length == en.Size)
                {
                    string ticks = fi.LastWriteTimeUtc.Ticks.ToString();
                    string[] c;
                    if (cache.TryGetValue(en.Path, out c) && c[0] == fi.Length.ToString() && c[1] == ticks) hash = c[2];
                    else hash = Sha1(local);
                    novoCache[en.Path] = new string[] { fi.Length.ToString(), ticks, hash };
                }
                if (hash == null || !string.Equals(hash, en.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    baixar.Add(en);
                    total += en.Size;
                }
            }
            WriteCache(novoCache);

            if (baixar.Count == 0) return "Tudo atualizado (" + versao + "). Bom jogo!";

            // instalação nova baixa vários GB: confere o espaço antes (com 200 MB de folga)
            DriveInfo disco = null;
            try { disco = new DriveInfo(Path.GetPathRoot(root)); }
            catch (ArgumentException) { }   // pasta de rede: não confere
            if (disco != null && disco.IsReady && disco.AvailableFreeSpace < total + 200L * 1048576)
                throw new Exception("falta espaço em " + disco.Name + ": precisa de " + Mb(total) + " e há " + Mb(disco.AvailableFreeSpace) + " livres.");

            // 3) baixa o que mudou
            long feito = 0;
            for (int i = 0; i < baixar.Count; i++)
            {
                Entry en = baixar[i];
                string local = LocalPath(en.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(local));
                string tmp = local + ".download";
                Download(en, tmp, w, feito, total, i + 1, baixar.Count);
                if (!string.Equals(Sha1(tmp), en.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tmp);
                    throw new Exception("o arquivo " + en.Path + " chegou corrompido; tente de novo.");
                }
                try
                {
                    if (File.Exists(local)) File.Delete(local);
                    File.Move(tmp, local);
                }
                catch (IOException)
                {
                    throw new Exception("não consegui trocar " + en.Path + ". Feche o jogo e clique em \"Verificar de novo\".");
                }
                feito += en.Size;
                FileInfo fi = new FileInfo(local);
                novoCache[en.Path] = new string[] { fi.Length.ToString(), fi.LastWriteTimeUtc.Ticks.ToString(), en.Hash };
            }
            WriteCache(novoCache);
            return (baixar.Count == arquivosDoJogo ? "Jogo instalado! " : "Atualizado! ")
                 + baixar.Count + " arquivo(s) baixado(s) (" + versao + "). Bom jogo!";
        }

        List<Entry> ReadManifest(out string versao)
        {
            string texto;
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.CacheControl] = "no-cache";
                texto = wc.DownloadString(baseUrl + "manifest.txt?t=" + DateTime.UtcNow.Ticks);
            }
            string[] linhas = texto.Replace("\r\n", "\n").Split('\n');
            if (linhas.Length < 2 || !linhas[0].StartsWith("MUCHILA-MANIFEST")) throw new Exception("lista de arquivos inválida no servidor");
            versao = linhas[1].StartsWith("versao=") ? linhas[1].Substring(7) : "?";
            List<Entry> lista = new List<Entry>();
            for (int i = 2; i < linhas.Length; i++)
            {
                if (linhas[i].Length == 0) continue;
                string[] c = linhas[i].Split('\t');
                if (c.Length != 3) continue;
                string p = c[2];
                // segurança: só caminhos relativos dentro da pasta do cliente
                if (p.Contains("..") || p.Contains(":") || p.StartsWith("/") || p.StartsWith("\\")) continue;
                Entry en = new Entry();
                en.Hash = c[0];
                en.Size = long.Parse(c[1]);
                en.Path = p;
                lista.Add(en);
            }
            return lista;
        }

        string LocalPath(string rel) { return Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)); }

        string Url(string rel)
        {
            string[] partes = rel.Split('/');
            for (int i = 0; i < partes.Length; i++) partes[i] = Uri.EscapeDataString(partes[i]);
            return baseUrl + "files/" + string.Join("/", partes);
        }

        void Download(Entry en, string destino, BackgroundWorker w, long feito, long total) { Download(en, destino, w, feito, total, 1, 1); }

        void Download(Entry en, string destino, BackgroundWorker w, long feito, long total, int n, int de)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Url(en.Path));
            req.Timeout = 30000;
            req.ReadWriteTimeout = 60000;
            using (WebResponse resp = req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (FileStream f = new FileStream(destino, FileMode.Create, FileAccess.Write))
            {
                byte[] buf = new byte[81920];
                long lido = 0;
                int r;
                int ultimo = -1;
                while ((r = s.Read(buf, 0, buf.Length)) > 0)
                {
                    f.Write(buf, 0, r);
                    lido += r;
                    int pct = (int)((feito + lido) * 100 / Math.Max(1, total));
                    if (pct != ultimo)
                    {
                        ultimo = pct;
                        Report(w, pct, "Baixando " + n + " de " + de + ": " + en.Path + "  (" + Mb(feito + lido) + " de " + Mb(total) + ")");
                    }
                }
            }
        }

        static string Mb(long b) { return (b / 1048576.0).ToString("0.0") + " MB"; }

        static string Sha1(string path)
        {
            using (SHA1 sha = SHA1.Create())
            using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] h = sha.ComputeHash(f);
                StringBuilder sb = new StringBuilder(40);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        Dictionary<string, string[]> ReadCache()
        {
            Dictionary<string, string[]> d = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string f = Path.Combine(root, CacheName);
                if (File.Exists(f))
                    foreach (string l in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        string[] c = l.Split('\t');
                        if (c.Length == 4) d[c[0]] = new string[] { c[1], c[2], c[3] };
                    }
            }
            catch { }
            return d;
        }

        void WriteCache(Dictionary<string, string[]> d)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string[]> kv in d)
                    sb.Append(kv.Key).Append('\t').Append(kv.Value[0]).Append('\t').Append(kv.Value[1]).Append('\t').Append(kv.Value[2]).Append("\r\n");
                File.WriteAllText(Path.Combine(root, CacheName), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        void Play()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(root, "main.exe"));
                psi.WorkingDirectory = root;
                Process.Start(psi);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não consegui abrir o jogo: " + ex.Message, "Mu Chila", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

// Mu Chila - Launcher do cliente (issue #23).
// Fica na pasta do cliente (ao lado do main.exe). Ao abrir: baixa a lista de arquivos do servidor (manifest.txt),
// confere tamanho e SHA-1 de cada arquivo, baixa só o que mudou ou está faltando, se atualiza sozinho e abre o jogo.
// Escrito em C# 5 para compilar com o csc do .NET Framework 4.x (já vem no Windows 10/11; o jogador não instala nada).
// Compilar: tools\Publicar-Launcher.ps1 (ou csc /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll).
//
// Servidor (padrão): http://26.139.39.123/arquivos/launcher/  — pode trocar criando launcher.ini com ServerUrl=...
// Manifesto: 1ª linha "MUCHILA-MANIFEST 1", 2ª "versao=<texto>", depois "sha1<TAB>tamanho<TAB>caminho/relativo".
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Mu Chila Launcher")]
[assembly: System.Reflection.AssemblyProduct("Mu Chila")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

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
            // modo silencioso para teste/diagnóstico: MuChilaLauncher.exe --verificar <arquivo-de-saida>
            if (args.Length == 2 && args[0] == "--verificar")
            {
                try { File.WriteAllText(args[1], "OK: " + new LauncherForm().Core(null, false)); return 0; }
                catch (Exception ex) { File.WriteAllText(args[1], "FALHA: " + ex.Message); return 1; }
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

        readonly string root;
        readonly string baseUrl;
        readonly Label lblTitle = new Label();
        readonly Label lblStatus = new Label();
        readonly ProgressBar bar = new ProgressBar();
        readonly Button btnPlay = new Button();
        readonly Button btnCheck = new Button();
        BackgroundWorker worker;
        bool selfUpdating;

        public LauncherForm()
        {
            root = AppDomain.CurrentDomain.BaseDirectory;
            baseUrl = ReadUrl(root);

            Text = "Mu Chila - Launcher";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 170);
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

            btnPlay.Text = "Jogar";
            btnPlay.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnPlay.SetBounds(324, 124, 120, 30);
            btnPlay.Enabled = false;
            btnPlay.Click += delegate { Play(); };

            Controls.AddRange(new Control[] { lblTitle, lblStatus, bar, btnCheck, btnPlay });
            Shown += delegate { StartCheck(); };
        }

        static string ReadUrl(string dir)
        {
            try
            {
                string ini = Path.Combine(dir, "launcher.ini");
                if (File.Exists(ini))
                    foreach (string l in File.ReadAllLines(ini))
                    {
                        string t = l.Trim();
                        if (t.StartsWith("ServerUrl=", StringComparison.OrdinalIgnoreCase))
                        {
                            string u = t.Substring(10).Trim();
                            if (u.Length > 0) return u.EndsWith("/") ? u : u + "/";
                        }
                    }
            }
            catch { }
            return DefaultUrl;
        }

        void StartCheck()
        {
            btnPlay.Enabled = false;
            btnCheck.Enabled = false;
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
                if (e.Error != null)
                    lblStatus.Text = "Não foi possível atualizar: " + e.Error.Message + "\r\nVocê ainda pode jogar com os arquivos atuais.";
                else if (e.Result != null)
                    lblStatus.Text = (string)e.Result;
                btnPlay.Enabled = File.Exists(Path.Combine(root, "main.exe"));
                btnCheck.Enabled = true;
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
            string selfPath = Path.Combine(root, SelfName);
            foreach (Entry en in entries)
            {
                if (!autoAtualizar || !string.Equals(en.Path, SelfName, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(selfPath) && string.Equals(Sha1(selfPath), en.Hash, StringComparison.OrdinalIgnoreCase)) break;
                Report(w, 0, "Atualizando o launcher...");
                string novo = selfPath + ".new";
                Download(en, novo, w, 0, en.Size);
                string bat = Path.Combine(root, "launcher-update.bat");
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
            long total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry en = entries[i];
                if (string.Equals(en.Path, SelfName, StringComparison.OrdinalIgnoreCase)) continue;
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
            return "Atualizado! " + baixar.Count + " arquivo(s) baixado(s) (" + versao + "). Bom jogo!";
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

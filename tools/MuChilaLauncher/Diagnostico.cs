// Mu Chila - Launcher: requisitos do Windows e diagnóstico (pedido do dono, 30/09/2026: "no PC do meu amigo o mouse vira
// loading e o jogo não abre"; logs que comprovem que o problema não é o jogo + conferir/instalar os programas obrigatórios).
//
// Programas obrigatórios (medido nas importações das DLLs do cliente S14, não é chute):
//   - Visual C++ 2013 x86 (msvcr120.dll/msvcp120.dll): o Main.dll precisa. O main.exe carrega o Main.dll com LoadLibrary;
//     sem o VC++ 2013 o carregamento falha e o main.exe fecha sem mostrar nada (o "loading" do mouse e mais nada).
//   - DirectX End-User Runtime (junho/2010) (d3dx9_43.dll/d3dcompiler_43.dll): libGLESv2.dll e awesomium.dll precisam.
// Os dois não vêm com o Windows 10/11. Instalação: baixa primeiro do servidor do launcher (<ServerUrl>/requisitos/, espelhado
// pelo Publicar-Launcher.ps1), senão da Microsoft; roda em silêncio pedindo administrador (UAC). Se não der, mostra os links.
//
// Logs (pasta do jogo\Logs\launcher, ou %APPDATA%\MuChila\logs):
//   - launcher.log: uma linha por evento (abriu, atualizou, requisitos, instalou, abriu o jogo, como o jogo terminou);
//   - diagnostico-<data>.txt: computador, placa de vídeo, antivírus, programas obrigatórios, DLLs que faltam para o que o
//     jogo carrega ao abrir (main.exe, Main.dll e as DLLs da pasta que eles pedem), arquivos do jogo x servidor (SHA-1), como o jogo terminou (código de saída) e travamentos do main.exe
//     no Log de Eventos do Windows, com uma conclusão no fim. Gerado quando o jogo fecha sozinho e no botão DIAGNÓSTICO.
// Linha de comando (teste): MuChilaLauncher.exe --diagnostico <saida.txt> [pasta-do-jogo]  (código 0 = nada faltando, 2 = falta algo)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MuChilaLauncher
{
    class Requisito
    {
        public string Nome, Motivo, Arquivo, UrlDownload, UrlPagina, Argumentos;
        public string[] Dlls;
        public bool Extrair;   // DirectX: o pacote é um extrator; o instalador de verdade é o DXSETUP.exe dentro dele

        /// <summary>DLLs deste requisito que não estão na pasta de sistema de 32 bits (o jogo é 32 bits).</summary>
        public List<string> Faltando()
        {
            List<string> f = new List<string>();
            foreach (string d in Dlls) if (!File.Exists(Path.Combine(Requisitos.Sistema32bits, d))) f.Add(d);
            return f;
        }

        public bool Instalado { get { return Faltando().Count == 0; } }
    }

    static class Requisitos
    {
        public static string Sistema32bits { get { return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86); } }

        public static List<Requisito> Todos()
        {
            List<Requisito> l = new List<Requisito>();
            Requisito vc = new Requisito();
            vc.Nome = "Microsoft Visual C++ 2013 (x86)";
            vc.Motivo = "O Main.dll do jogo precisa dele. Sem ele o jogo fecha sozinho logo ao abrir, sem mensagem.";
            vc.Dlls = new string[] { "msvcr120.dll", "msvcp120.dll" };
            vc.Arquivo = "vcredist_x86_2013.exe";
            vc.UrlDownload = "https://download.visualstudio.microsoft.com/download/pr/10912113/5da66ddebb0ad32ebd4b922fd82e8e25/vcredist_x86.exe";   // = aka.ms/highdpimfc2013x86enu (12.0.40664)
            vc.UrlPagina = "https://learn.microsoft.com/pt-br/cpp/windows/latest-supported-vc-redist";
            vc.Argumentos = "/install /quiet /norestart";
            l.Add(vc);
            Requisito dx = new Requisito();
            dx.Nome = "DirectX End-User Runtime (junho de 2010)";
            dx.Motivo = "Gráficos e navegador do jogo (libGLESv2.dll, awesomium.dll). O Windows 10/11 não traz o d3dx9_43.";
            dx.Dlls = new string[] { "d3dx9_43.dll", "d3dcompiler_43.dll" };
            dx.Arquivo = "directx_Jun2010_redist.exe";
            dx.UrlDownload = "https://download.microsoft.com/download/8/4/A/84A35BF1-DAFE-4AE8-82AF-AD2AE20B6B14/directx_Jun2010_redist.exe";
            dx.UrlPagina = "https://www.microsoft.com/pt-br/download/details.aspx?id=8109";
            dx.Extrair = true;
            l.Add(dx);
            return l;
        }

        public static List<Requisito> Faltando()
        {
            return Todos().FindAll(delegate(Requisito r) { return !r.Instalado; });
        }

        /// <summary>Baixa e instala. null = instalado; senão o motivo da falha (para mostrar junto com os links).</summary>
        public static string Instalar(Requisito r, string baseUrl, Action<int, string> progresso)
        {
            string pasta = Path.Combine(Path.GetTempPath(), "MuChila");
            Directory.CreateDirectory(pasta);
            string arq = Path.Combine(pasta, r.Arquivo);
            StringBuilder erros = new StringBuilder();
            if (!InstaladorValido(arq))
                foreach (string url in new string[] { baseUrl + "requisitos/" + r.Arquivo, r.UrlDownload })
                {
                    try
                    {
                        string tmp = arq + ".download";
                        Diagnostico.Baixar(url, tmp, delegate(long feito, long total)
                        {
                            progresso(total > 0 ? (int)(feito * 100 / total) : 0, "Baixando " + r.Nome + "  (" + (feito / 1048576.0).ToString("0.0") + " MB"
                                + (total > 0 ? " de " + (total / 1048576.0).ToString("0.0") + " MB" : "") + ")");
                        });
                        if (!InstaladorValido(tmp)) { File.Delete(tmp); throw new Exception("o arquivo recebido não é um instalador"); }
                        if (File.Exists(arq)) File.Delete(arq);
                        File.Move(tmp, arq);
                        break;
                    }
                    catch (Exception ex) { erros.Append(" [" + url + ": " + ex.Message + "]"); }
                }
            if (!InstaladorValido(arq)) return "não consegui baixar o instalador." + erros;

            progresso(100, "Instalando " + r.Nome + "... (se o Windows perguntar, clique em Sim)");
            ProcessStartInfo psi;
            if (r.Extrair)
            {
                string dir = Path.Combine(pasta, "dx");
                string bat = Path.Combine(pasta, "instalar-directx.bat");
                File.WriteAllText(bat, "@echo off\r\n\"" + arq + "\" /Q /C /T:\"" + dir + "\"\r\n\"" + dir + "\\DXSETUP.exe\" /silent\r\nexit /b %errorlevel%\r\n", Encoding.Default);
                psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
            }
            else psi = new ProcessStartInfo(arq, r.Argumentos);
            psi.UseShellExecute = true;
            psi.Verb = "runas";   // os dois instalam na pasta do Windows: precisam de administrador
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            int codigo;
            try
            {
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    codigo = p.ExitCode;
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) return "a instalação precisa de permissão de administrador e ela foi negada.";
                return "não consegui rodar o instalador: " + ex.Message;
            }
            if (!r.Instalado)
                return "o instalador terminou (código " + codigo + ") mas continuam faltando: " + string.Join(", ", r.Faltando().ToArray()) + ".";
            return null;
        }

        static bool InstaladorValido(string arq)
        {
            try
            {
                FileInfo fi = new FileInfo(arq);
                if (!fi.Exists || fi.Length < 1048576) return false;
                using (FileStream f = fi.OpenRead()) return f.ReadByte() == 'M' && f.ReadByte() == 'Z';
            }
            catch { return false; }
        }
    }

    /// <summary>Lê as DLLs que um .exe/.dll do Windows importa (tabela normal e "delay load").</summary>
    static class Pe
    {
        public class Importacao { public string Dll; public bool Atrasada; }

        /// <summary>null se não for um executável válido. maquina: 0x14c = 32 bits, 0x8664 = 64 bits.</summary>
        public static List<Importacao> Importacoes(string arquivo, out int maquina)
        {
            maquina = 0;
            try
            {
                byte[] b = File.ReadAllBytes(arquivo);
                if (b.Length < 0x40 || b[0] != 'M' || b[1] != 'Z') return null;
                int pe = BitConverter.ToInt32(b, 0x3c);
                if (pe <= 0 || pe + 24 > b.Length || BitConverter.ToUInt32(b, pe) != 0x4550) return null;
                maquina = BitConverter.ToUInt16(b, pe + 4);
                int nsec = BitConverter.ToUInt16(b, pe + 6);
                int opt = pe + 24;
                bool x64 = BitConverter.ToUInt16(b, opt) == 0x20b;
                long imageBase = x64 ? BitConverter.ToInt64(b, opt + 24) : BitConverter.ToUInt32(b, opt + 28);
                int dd = opt + (x64 ? 112 : 96);
                int secoes = opt + BitConverter.ToUInt16(b, pe + 20);
                List<Importacao> r = new List<Importacao>();
                for (int tipo = 0; tipo < 2; tipo++)
                {
                    int rva = BitConverter.ToInt32(b, dd + (tipo == 0 ? 1 : 13) * 8);
                    if (rva == 0) continue;
                    int o = Offset(b, secoes, nsec, rva);
                    for (int n = 0; o >= 0 && o + 32 <= b.Length && n < 500; n++, o += tipo == 0 ? 20 : 32)
                    {
                        long nome;
                        if (tipo == 0) nome = BitConverter.ToUInt32(b, o + 12);
                        else
                        {
                            nome = BitConverter.ToUInt32(b, o + 4);
                            if ((BitConverter.ToUInt32(b, o) & 1) == 0 && nome != 0) nome -= imageBase;   // formato antigo: endereço, não RVA
                        }
                        if (nome == 0) break;
                        int no = Offset(b, secoes, nsec, (int)nome);
                        if (no < 0) break;
                        int fim = no;
                        while (fim < b.Length && b[fim] != 0) fim++;
                        Importacao im = new Importacao();
                        im.Dll = Encoding.ASCII.GetString(b, no, fim - no);
                        im.Atrasada = tipo == 1;
                        r.Add(im);
                    }
                }
                return r;
            }
            catch { return null; }
        }

        static int Offset(byte[] b, int secoes, int nsec, int rva)
        {
            for (int i = 0; i < nsec; i++)
            {
                int s = secoes + i * 40;
                int va = BitConverter.ToInt32(b, s + 12);
                int tam = Math.Max(BitConverter.ToInt32(b, s + 8), BitConverter.ToInt32(b, s + 16));
                if (rva >= va && rva < va + tam) return rva - va + BitConverter.ToInt32(b, s + 20);
            }
            return -1;
        }
    }

    /// <summary>Como o jogo terminou depois do JOGAR (acompanhado por 30 s).</summary>
    class Abertura
    {
        public DateTime Inicio;
        public int Pid;
        public bool Saiu, JanelaApareceu, OutroMainRodando;
        public int Codigo;
        public double Segundos;

        public bool Falhou { get { return Saiu && !OutroMainRodando && (!JanelaApareceu || Codigo != 0); } }
    }

    static class Diagnostico
    {
        public static string Versao { get { return typeof(Diagnostico).Assembly.GetName().Version.ToString(); } }

        public static string PastaLogs(string root)
        {
            if (root != null)
                try
                {
                    string p = Path.Combine(Path.Combine(root, "Logs"), "launcher");
                    Directory.CreateDirectory(p);
                    return p;
                }
                catch { }
            string a = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuChila"), "logs");
            Directory.CreateDirectory(a);
            return a;
        }

        static readonly object trava = new object();

        /// <summary>Uma linha com data/hora em launcher.log (passa de 512 KB: vira launcher.old.log).</summary>
        public static void Log(string root, string msg)
        {
            try
            {
                lock (trava)
                {
                    string f = Path.Combine(PastaLogs(root), "launcher.log");
                    FileInfo fi = new FileInfo(f);
                    if (fi.Exists && fi.Length > 512 * 1024)
                    {
                        string velho = Path.Combine(fi.DirectoryName, "launcher.old.log");
                        if (File.Exists(velho)) File.Delete(velho);
                        File.Move(f, velho);
                    }
                    File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n", new UTF8Encoding(false));
                }
            }
            catch { }
        }

        /// <summary>Grava um relatório (guarda os 20 mais novos). Devolve o caminho.</summary>
        public static string Salvar(string root, string texto)
        {
            string pasta = PastaLogs(root);
            string f = Path.Combine(pasta, "diagnostico-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            File.WriteAllText(f, texto, new UTF8Encoding(true));   // com BOM: o Bloco de Notas antigo mostra os acentos certo
            try
            {
                string[] velhos = Directory.GetFiles(pasta, "diagnostico-*.txt");
                Array.Sort(velhos);
                for (int i = 0; i < velhos.Length - 20; i++) File.Delete(velhos[i]);
            }
            catch { }
            return f;
        }

        /// <summary>Erro não tratado no launcher: grava em %APPDATA%\MuChila\launcher-erros.log e avisa.</summary>
        public static void ErroFatal(Exception ex)
        {
            string f = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuChila"), "launcher-erros.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                File.AppendAllText(f, "==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  launcher " + Versao + "  " + Environment.OSVersion + "\r\n" + ex + "\r\n\r\n", new UTF8Encoding(false));
            }
            catch { }
            try { MessageBox.Show("O launcher teve um erro inesperado: " + (ex == null ? "?" : ex.Message) + "\r\n\r\nDetalhes salvos em:\r\n" + f, "Mu Chila", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
        }

        // ---------------- rede ----------------
        public static void Baixar(string url, string destino, Action<long, long> progresso)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 20000;
            req.ReadWriteTimeout = 60000;
            req.UserAgent = "MuChilaLauncher/" + Versao;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (FileStream f = new FileStream(destino, FileMode.Create, FileAccess.Write))
            {
                long total = resp.ContentLength, feito = 0;
                byte[] buf = new byte[81920];
                int r, ultimo = -1;
                while ((r = s.Read(buf, 0, buf.Length)) > 0)
                {
                    f.Write(buf, 0, r);
                    feito += r;
                    int pct = total > 0 ? (int)(feito * 100 / total) : (int)(feito / 1048576);
                    if (pct != ultimo) { ultimo = pct; progresso(feito, total); }
                }
            }
        }

        static string BaixarTexto(string url, int timeout)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeout;
            req.ReadWriteTimeout = timeout;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }

        // ---------------- o relatório ----------------
        /// <summary>Relatório completo (demora alguns segundos: WMI, SHA-1 dos arquivos principais e o manifesto do servidor).</summary>
        public static string Relatorio(string root, string baseUrl, string motivo, Abertura abertura)
        {
            StringBuilder sb = new StringBuilder();
            List<string> problemasPc = new List<string>(), problemasArquivos = new List<string>(), avisos = new List<string>();
            sb.AppendLine("==================== MU CHILA - DIAGNÓSTICO ====================");
            sb.AppendLine("Gerado em:   " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            sb.AppendLine("Motivo:      " + motivo);
            sb.AppendLine("Launcher:    " + Versao + "  (" + Application.ExecutablePath + ")");
            sb.AppendLine("Pasta:       " + (root ?? "(nenhuma)"));
            sb.AppendLine();
            string manifesto = null, erroManifesto = null;
            try { manifesto = BaixarTexto(baseUrl + "manifest.txt?t=" + DateTime.UtcNow.Ticks, 10000); }
            catch (Exception ex) { erroManifesto = ex.Message; }
            string[] linhasManifesto = manifesto == null ? new string[0] : manifesto.Replace("\r\n", "\n").Split('\n');
            Dictionary<string, string> doServidor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // arquivos da pasta principal -> SHA-1
            foreach (string l in linhasManifesto)
            {
                string[] c = l.Split('\t');
                if (c.Length == 3 && !c[2].Contains("/") && !c[2].Equals("MuChilaLauncher.exe", StringComparison.OrdinalIgnoreCase)) doServidor[c[2]] = c[0];
            }

            // [1] computador
            sb.AppendLine("[1] COMPUTADOR");
            foreach (Dictionary<string, object> o in Wmi(null, "SELECT Caption, Version, OSArchitecture, TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
            {
                sb.AppendLine("    Windows:        " + Txt(o, "Caption") + "  versão " + Txt(o, "Version") + "  " + Txt(o, "OSArchitecture"));
                long totalKb = Num(o, "TotalVisibleMemorySize"), livreKb = Num(o, "FreePhysicalMemory");
                sb.AppendLine("    Memória:        " + (totalKb / 1048576.0).ToString("0.0") + " GB (" + (livreKb / 1048576.0).ToString("0.0") + " GB livres)");
                if (totalKb > 0 && totalKb < 2 * 1048576) avisos.Add("pouca memória RAM (" + (totalKb / 1048576.0).ToString("0.0") + " GB).");
            }
            foreach (Dictionary<string, object> o in Wmi(null, "SELECT Name FROM Win32_Processor"))
                sb.AppendLine("    Processador:    " + Txt(o, "Name").Trim());
            sb.AppendLine("    Windows 64 bits: " + (Environment.Is64BitOperatingSystem ? "sim" : "não") + "   Launcher em 64 bits: " + (Environment.Is64BitProcess ? "sim" : "não"));
            bool admin = false;
            try { admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { }
            sb.AppendLine("    Usuário:        " + Environment.UserName + (admin ? " (rodando como administrador)" : " (sem administrador)"));
            sb.AppendLine("    Tela principal: " + Screen.PrimaryScreen.Bounds.Width + "x" + Screen.PrimaryScreen.Bounds.Height);
            bool temVideo = false;
            foreach (Dictionary<string, object> o in Wmi(null, "SELECT Name, DriverVersion, DriverDate, AdapterRAM, Status FROM Win32_VideoController"))
            {
                temVideo = true;
                string nome = Txt(o, "Name");
                string data = "";
                try { if (o["DriverDate"] != null) data = ManagementDateTimeConverter.ToDateTime(o["DriverDate"].ToString()).ToString("dd/MM/yyyy"); } catch { }
                sb.AppendLine("    Placa de vídeo: " + nome + "  (driver " + Txt(o, "DriverVersion") + (data.Length > 0 ? " de " + data : "") + ", status " + Txt(o, "Status") + ")");
                string n = nome.ToLowerInvariant();
                if ((n.Contains("microsoft") && (n.Contains("basic") || n.Contains("básico"))) || n.Contains("standard vga") || n.Contains("vga padrão"))
                    problemasPc.Add("a placa de vídeo está sem driver (\"" + nome + "\"): o jogo usa OpenGL e não abre assim. Instale o driver do fabricante (NVIDIA, AMD ou Intel).");
            }
            if (!temVideo) sb.AppendLine("    Placa de vídeo: (não consegui ler)");
            List<Dictionary<string, object>> avs = Wmi(@"root\SecurityCenter2", "SELECT displayName, productState FROM AntiVirusProduct");
            if (avs.Count == 0) sb.AppendLine("    Antivírus:      (não consegui ler)");
            foreach (Dictionary<string, object> o in avs)
            {
                long st = Num(o, "productState");
                bool ligado = ((st >> 12) & 0xF) == 1;
                sb.AppendLine("    Antivírus:      " + Txt(o, "displayName") + (ligado ? " (ligado)" : " (desligado)"));
            }
            try
            {
                DriveInfo d = new DriveInfo(Path.GetPathRoot(root ?? Environment.SystemDirectory));
                sb.AppendLine("    Disco " + d.Name.TrimEnd('\\') + "        " +(d.AvailableFreeSpace / 1073741824.0).ToString("0.0") + " GB livres");
                if (d.AvailableFreeSpace < 500L * 1048576) avisos.Add("pouco espaço livre no disco " + d.Name + ".");
            }
            catch { }
            sb.AppendLine();

            // [2] programas obrigatórios
            sb.AppendLine("[2] PROGRAMAS OBRIGATÓRIOS  (conferidos em " + Requisitos.Sistema32bits + ")");
            foreach (Requisito r in Requisitos.Todos())
            {
                List<string> f = r.Faltando();
                sb.AppendLine("    " + (f.Count == 0 ? "[ OK  ] " : "[FALTA] ") + r.Nome + (f.Count == 0 ? "" : "  -> não encontrados: " + string.Join(", ", f.ToArray())));
                if (f.Count > 0) problemasPc.Add(r.Nome + " não está instalado. " + r.Motivo + " Download: " + r.UrlDownload);
            }
            sb.AppendLine();

            // [3] o que o jogo carrega ao abrir: parte do main.exe e do Main.dll (que o main.exe carrega) e segue as DLLs da pasta
            // que eles pedem. O resto da pasta (GameGuard np*.dll, DSETUP, MFSvc2, WZRegPII...) não é carregado e fica de fora.
            sb.AppendLine("[3] DLLs QUE O JOGO CARREGA AO ABRIR  (main.exe + Main.dll e o que eles pedem; pasta do jogo + " + Requisitos.Sistema32bits + ")");
            if (root == null || !File.Exists(Path.Combine(root, "main.exe")))
            {
                sb.AppendLine("    main.exe não encontrado na pasta do jogo.");
                problemasArquivos.Add("main.exe não está na pasta do jogo (antivírus pode ter apagado). Clique em VERIFICAR INTEGRIDADE.");
            }
            else
            {
                Dictionary<string, List<string>> faltam = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, bool> atrasada = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                int dlls = 0;
                List<string> conferidos = new List<string>();
                Queue<string> fila = new Queue<string>();
                fila.Enqueue("main.exe");
                fila.Enqueue("Main.dll");
                while (fila.Count > 0)
                {
                    string nome = fila.Dequeue();
                    if (conferidos.Exists(delegate(string c) { return c.Equals(nome, StringComparison.OrdinalIgnoreCase); })) continue;
                    string a = Path.Combine(root, nome);
                    if (!File.Exists(a))
                    {
                        if (nome == "Main.dll") { sb.AppendLine("    FALTA Main.dll (o main.exe carrega ao abrir)"); problemasArquivos.Add("Main.dll não está na pasta do jogo (antivírus pode ter apagado). Clique em VERIFICAR INTEGRIDADE."); }
                        continue;
                    }
                    conferidos.Add(nome);
                    int maquina;
                    List<Pe.Importacao> imps = Pe.Importacoes(a, out maquina);
                    if (imps == null)
                    {
                        sb.AppendLine("    " + nome + ": NÃO É UM EXECUTÁVEL VÁLIDO (arquivo corrompido ou trocado)");
                        problemasArquivos.Add(nome + " está corrompido. Clique em VERIFICAR INTEGRIDADE.");
                        continue;
                    }
                    if (maquina != 0x14c) sb.AppendLine("    " + nome + ": atenção, não é 32 bits (máquina 0x" + maquina.ToString("X") + ")");
                    foreach (Pe.Importacao im in imps)
                    {
                        string dll = im.Dll;
                        if (dll.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) || dll.StartsWith("ext-ms-", StringComparison.OrdinalIgnoreCase)) continue;
                        dlls++;
                        if (File.Exists(Path.Combine(root, dll))) { fila.Enqueue(dll); continue; }   // DLL do próprio jogo: confere as dela também
                        if (File.Exists(Path.Combine(Requisitos.Sistema32bits, dll))
                            || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), dll))) continue;
                        if (!faltam.ContainsKey(dll)) { faltam[dll] = new List<string>(); atrasada[dll] = true; }
                        if (!faltam[dll].Contains(nome)) faltam[dll].Add(nome);
                        if (!im.Atrasada) atrasada[dll] = false;
                    }
                }
                sb.AppendLine("    Conferidos: " + string.Join(", ", conferidos.ToArray()) + "  (" + dlls + " dependências)");
                if (faltam.Count == 0) sb.AppendLine("    Todas as DLLs necessárias foram encontradas.");
                foreach (KeyValuePair<string, List<string>> kv in faltam)
                {
                    bool at = atrasada[kv.Key];
                    sb.AppendLine("    FALTA " + kv.Key + "  <- pedida por " + string.Join(", ", kv.Value.ToArray()) + (at ? "  (só carregada quando usada)" : ""));
                    bool deRequisito = false;
                    foreach (Requisito r in Requisitos.Todos()) if (Array.IndexOf(r.Dlls, kv.Key.ToLowerInvariant()) >= 0) deRequisito = true;
                    if (deRequisito || doServidor.ContainsKey(kv.Key)) continue;   // já está no [2] / é arquivo do jogo (vai para o [4])
                    if (at) avisos.Add(kv.Key + " não existe neste Windows (usada só em parte do jogo, por " + string.Join(", ", kv.Value.ToArray()) + ").");
                    else problemasPc.Add(kv.Key + " não existe neste Windows e " + string.Join(", ", kv.Value.ToArray()) + " precisa dela para abrir.");
                }
            }
            sb.AppendLine();

            // [4] arquivos do jogo x servidor
            sb.AppendLine("[4] ARQUIVOS DO JOGO x SERVIDOR  (SHA-1 dos arquivos da pasta principal)");
            int iguais = 0;
            if (root != null)
            {
                if (manifesto == null) sb.AppendLine("    Não consegui baixar a lista do servidor (" + erroManifesto + "); comparação não feita.");
                else
                {
                    if (linhasManifesto.Length > 1 && linhasManifesto[1].StartsWith("versao=")) sb.AppendLine("    Versão do servidor: " + linhasManifesto[1].Substring(7));
                    List<string> diferentes = new List<string>();
                    foreach (KeyValuePair<string, string> kv in doServidor)
                    {
                        string local = Path.Combine(root, kv.Key);
                        if (!File.Exists(local)) { diferentes.Add(kv.Key + " (FALTANDO)"); continue; }
                        string h;
                        try { h = Sha1(local); } catch (Exception ex) { diferentes.Add(kv.Key + " (não consegui ler: " + ex.Message + ")"); continue; }
                        if (string.Equals(h, kv.Value, StringComparison.OrdinalIgnoreCase)) iguais++;
                        else diferentes.Add(kv.Key + " (DIFERENTE do servidor)");
                    }
                    sb.AppendLine("    " + iguais + " arquivo(s) idêntico(s) aos do servidor (os mesmos que funcionam nos outros computadores).");
                    for (int i = 0; i < diferentes.Count && i < 10; i++) sb.AppendLine("    " + diferentes[i]);
                    if (diferentes.Count > 10) sb.AppendLine("    ... e mais " + (diferentes.Count - 10) + ". VERIFICAR INTEGRIDADE baixa de novo todos eles.");
                    if (diferentes.Count > 0) problemasArquivos.Add(diferentes.Count + " arquivo(s) faltando ou diferentes do servidor (ver [4]: "
                        + string.Join(", ", diferentes.GetRange(0, Math.Min(5, diferentes.Count)).ToArray()) + (diferentes.Count > 5 ? "..." : "")
                        + "). Normalmente é o antivírus apagando/bloqueando. Clique em VERIFICAR INTEGRIDADE.");
                }
                foreach (string k in new string[] { "main.exe", "Main.dll" })
                {
                    string p = Path.Combine(root, k);
                    if (File.Exists(p)) try { sb.AppendLine("    " + k + ": " + new FileInfo(p).Length + " bytes, SHA-1 " + Sha1(p)); } catch { }
                }
            }
            sb.AppendLine();

            // [5] pasta do jogo
            sb.AppendLine("[5] PASTA DO JOGO");
            if (root != null)
            {
                Encoding ansi = Encoding.Default;
                if (ansi.GetString(ansi.GetBytes(root)) != root)
                    problemasPc.Add("o caminho da pasta do jogo tem caracteres que o jogo (programa antigo) não entende: " + root + ". Instale em C:\\Jogos\\Mu Chila.");
                if (root.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0)
                    avisos.Add("a pasta do jogo está dentro do OneDrive: arquivos podem ficar \"só na nuvem\". Prefira C:\\Jogos\\Mu Chila.");
                try
                {
                    string t = Path.Combine(root, "muchila-teste-gravacao.tmp");
                    File.WriteAllText(t, "ok"); File.Delete(t);
                    sb.AppendLine("    Gravação na pasta: OK");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("    Gravação na pasta: FALHOU (" + ex.Message + ")");
                    problemasPc.Add("não dá para gravar na pasta do jogo (" + ex.Message + "). Instale em C:\\Jogos\\Mu Chila.");
                }
                string compat = Compatibilidade(Path.Combine(root, "main.exe"));
                sb.AppendLine("    Modo de compatibilidade do main.exe: " + (compat ?? "nenhum"));
                // "Executar como administrador" (RUNASADMIN) não atrapalha; modo de compatibilidade com Windows antigo, sim
                if (compat != null && compat.Replace("~", "").Replace("RUNASADMIN", "").Replace("HIGHDPIAWARE", "").Trim().Length > 0) avisos.Add("o main.exe está com modo de compatibilidade (" + compat + "); se o jogo não abrir, desmarque em Propriedades > Compatibilidade.");
            }
            sb.AppendLine();

            // [6] como o jogo terminou
            if (abertura != null)
            {
                sb.AppendLine("[6] ABERTURA DO JOGO");
                sb.AppendLine("    " + ResumoAbertura(abertura));
                if (abertura.Falhou) sb.AppendLine("    Significado do código: " + SignificadoCodigo(abertura.Codigo));
                sb.AppendLine();
            }

            // [7] Log de Eventos do Windows
            DateTime desde = abertura != null ? abertura.Inicio.AddMinutes(-1) : DateTime.Now.AddDays(-7);
            sb.AppendLine("[7] ERROS DO main.exe NO LOG DE EVENTOS DO WINDOWS  (desde " + desde.ToString("dd/MM HH:mm") + ")");
            sb.Append(EventosDoWindows(desde));
            sb.AppendLine();

            // [8] conclusão
            sb.AppendLine("[8] CONCLUSÃO");
            if (problemasPc.Count == 0 && problemasArquivos.Count == 0)
            {
                if (abertura != null && abertura.Falhou)
                    sb.AppendLine("    Nenhuma falta encontrada, mas o jogo fechou sozinho. Veja o código em [6] e os eventos em [7]. Tente: desligar o\r\n"
                                + "    antivírus ou colocar a pasta do jogo nas exceções dele, atualizar o driver de vídeo e abrir o launcher como administrador.");
                else sb.AppendLine("    Nenhum problema encontrado neste computador.");
            }
            else
            {
                if (problemasPc.Count > 0)
                {
                    sb.AppendLine("    O PROBLEMA É DESTE COMPUTADOR, NÃO DO JOGO"
                        + (iguais > 0 && problemasArquivos.Count == 0 ? " (os arquivos do jogo são idênticos aos do servidor, ver [4])" : "") + ":");
                    foreach (string p in problemasPc) sb.AppendLine("     - " + p);
                }
                if (problemasArquivos.Count > 0)
                {
                    sb.AppendLine("    Arquivos do jogo neste computador:");
                    foreach (string p in problemasArquivos) sb.AppendLine("     - " + p);
                }
            }
            foreach (string a in avisos) sb.AppendLine("    Aviso: " + a);
            sb.AppendLine("================================================================");
            return sb.ToString();
        }

        public static string ResumoAbertura(Abertura a)
        {
            if (!a.Saiu) return "Jogo aberto às " + a.Inicio.ToString("HH:mm:ss") + " (PID " + a.Pid + ") e continua rodando" + (a.JanelaApareceu ? " com a janela na tela." : ".");
            if (a.OutroMainRodando) return "O main.exe (PID " + a.Pid + ") passou o jogo para outro processo main.exe, que continua rodando. Normal.";
            return "O jogo (PID " + a.Pid + ") FECHOU SOZINHO " + a.Segundos.ToString("0.0") + " s depois de abrir, código de saída " + Codigo(a.Codigo)
                 + (a.JanelaApareceu ? " (a janela chegou a aparecer)." : " (sem nunca mostrar a janela).");
        }

        static string Codigo(int c) { return c + " (0x" + ((uint)c).ToString("X8") + ")"; }

        public static string SignificadoCodigo(int c)
        {
            switch ((uint)c)
            {
                case 0: return "o próprio jogo decidiu fechar. Acontece quando ele não consegue carregar o Main.dll (falta o Visual C++ 2013, ver [2]/[3]) ou algum arquivo dele.";
                case 0xC0000135: return "DLL não encontrada (falta um programa obrigatório ou arquivo do jogo; ver [2] e [3]).";
                case 0xC000007B: return "DLL do tipo errado (64 bits no lugar da de 32) ou corrompida.";
                case 0xC0000142: return "uma DLL não conseguiu iniciar (Visual C++/DirectX corrompido: reinstale).";
                case 0xC0000005: return "acesso inválido à memória (driver de vídeo, antivírus ou programa que se injeta no jogo, como overlay/gravador de tela).";
                case 0xC0000409: return "erro crítico dentro do jogo ou de uma DLL (driver de vídeo, antivírus, overlay).";
                case 0xC0000022: return "acesso negado (antivírus ou permissão da pasta).";
                case 0xC000001D: return "o processador não suporta uma instrução usada.";
                case 0xC0000417: return "erro do Visual C++ (parâmetro inválido).";
                default: return "código não catalogado; veja os eventos do Windows em [7].";
            }
        }

        static string EventosDoWindows(DateTime desde)
        {
            StringBuilder sb = new StringBuilder();
            int achados = 0;
            try
            {
                using (EventLog log = new EventLog("Application"))
                {
                    EventLogEntryCollection en = log.Entries;
                    int n = en.Count;
                    for (int i = n - 1; i >= 0 && i >= n - 5000 && achados < 8; i--)
                    {
                        EventLogEntry e = en[i];
                        if (e.TimeGenerated < desde) break;
                        string src = e.Source;
                        if (src != "Application Error" && src != "Windows Error Reporting" && src != "SideBySide" && src != "Application Hang") continue;
                        string msg = e.Message ?? "";
                        if (msg.IndexOf("main.exe", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        achados++;
                        sb.AppendLine("    --- " + e.TimeGenerated.ToString("dd/MM/yyyy HH:mm:ss") + "  " + src + " (evento " + e.InstanceId + ")");
                        string[] linhas = msg.Replace("\r\n", "\n").Split('\n');
                        for (int j = 0; j < linhas.Length && j < 14; j++) if (linhas[j].Trim().Length > 0) sb.AppendLine("        " + linhas[j].Trim());
                    }
                }
            }
            catch (Exception ex) { return "    Não consegui ler o Log de Eventos: " + ex.Message + "\r\n"; }
            if (achados == 0) sb.AppendLine("    Nenhum erro do main.exe registrado.");
            return sb.ToString();
        }

        static string Compatibilidade(string exe)
        {
            foreach (RegistryKey raiz in new RegistryKey[] { Registry.CurrentUser, Registry.LocalMachine })
                try
                {
                    using (RegistryKey k = raiz.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
                        if (k != null)
                            foreach (string nome in k.GetValueNames())
                                if (string.Equals(nome, exe, StringComparison.OrdinalIgnoreCase)) return Convert.ToString(k.GetValue(nome)).Trim();
                }
                catch { }
            return null;
        }

        static List<Dictionary<string, object>> Wmi(string escopo, string consulta)
        {
            List<Dictionary<string, object>> r = new List<Dictionary<string, object>>();
            try
            {
                using (ManagementObjectSearcher s = escopo == null ? new ManagementObjectSearcher(consulta) : new ManagementObjectSearcher(escopo, consulta))
                {
                    s.Options.Timeout = TimeSpan.FromSeconds(10);
                    foreach (ManagementBaseObject o in s.Get())
                        using (o)
                        {
                            Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            foreach (PropertyData p in o.Properties) d[p.Name] = p.Value;
                            r.Add(d);
                        }
                }
            }
            catch { }
            return r;
        }

        static string Txt(Dictionary<string, object> o, string k) { object v; return o.TryGetValue(k, out v) && v != null ? v.ToString() : "?"; }

        static long Num(Dictionary<string, object> o, string k)
        {
            object v;
            if (!o.TryGetValue(k, out v) || v == null) return 0;
            try { return Convert.ToInt64(v); } catch { return 0; }
        }

        static string Sha1(string path)
        {
            using (System.Security.Cryptography.SHA1 sha = System.Security.Cryptography.SHA1.Create())
            using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                StringBuilder sb = new StringBuilder(40);
                foreach (byte b in sha.ComputeHash(f)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    // ------------------------------------------------------------------ na janela do launcher
    partial class LauncherForm
    {
        enum EscolhaReq { Fechar, Instalar, Jogar }

        /// <summary>
        /// Janela dos programas obrigatórios. falhou = a instalação automática não deu certo (mostra o motivo e os links).
        /// antesDeJogar = oferece "Jogar mesmo assim".
        /// </summary>
        EscolhaReq DialogoRequisitos(string titulo, string texto, Dictionary<string, string> falhas, bool antesDeJogar)
        {
            List<Requisito> todos = Requisitos.Todos();
            bool falta = false;
            foreach (Requisito r in todos) if (!r.Instalado) falta = true;
            using (Form f = new Form())
            {
                f.Text = "Mu Chila - Programas obrigatórios";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false; f.MinimizeBox = false;
                f.StartPosition = FormStartPosition.CenterScreen;
                f.Font = new Font("Segoe UI", 9f);
                f.BackColor = Color.White;
                int y = 14, largura = 660;

                Label l1 = new Label();
                l1.Text = titulo; l1.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                l1.SetBounds(16, y, largura - 32, 24); f.Controls.Add(l1); y += 30;
                Label l2 = new Label();
                l2.Text = texto; l2.ForeColor = Color.DimGray;
                l2.SetBounds(16, y, largura - 32, 36); f.Controls.Add(l2); y += 42;

                foreach (Requisito r in todos)
                {
                    bool ok = r.Instalado;
                    Label st = new Label();
                    st.Text = (ok ? "✔  " : "✖  ") + r.Nome + (ok ? " — instalado" : " — FALTANDO");
                    st.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                    st.ForeColor = ok ? Color.FromArgb(20, 130, 50) : Color.FromArgb(200, 40, 40);
                    st.SetBounds(16, y, largura - 32, 22); f.Controls.Add(st); y += 23;
                    Label mot = new Label();
                    mot.Text = r.Motivo; mot.ForeColor = Color.DimGray;
                    mot.SetBounds(36, y, largura - 52, 32); f.Controls.Add(mot); y += 34;
                    string erro;
                    if (!ok && falhas != null && falhas.TryGetValue(r.Nome, out erro))
                    {
                        Label le = new Label();
                        le.Text = "Não consegui instalar sozinho: " + erro; le.ForeColor = Color.FromArgb(160, 60, 0);
                        le.SetBounds(36, y, largura - 52, 32); f.Controls.Add(le); y += 34;
                        foreach (string[] lk in new string[][] { new string[] { "Baixar o instalador: ", r.UrlDownload }, new string[] { "Página oficial da Microsoft: ", r.UrlPagina } })
                        {
                            LinkLabel ll = new LinkLabel();
                            ll.Text = lk[0] + lk[1];
                            ll.LinkArea = new LinkArea(lk[0].Length, lk[1].Length);
                            string url = lk[1];
                            ll.LinkClicked += delegate { try { Process.Start(url); } catch { Clipboard.SetText(url); } };
                            ll.SetBounds(36, y, largura - 52, 20); f.Controls.Add(ll); y += 21;
                        }
                        y += 6;
                    }
                }
                if (falhas != null && falta)
                {
                    y += 4;
                    Label dica = new Label();
                    dica.Text = "Baixe e instale os que estão faltando pelos links acima (aceite tudo, é da Microsoft) e depois abra o launcher de novo.";
                    dica.SetBounds(16, y, largura - 32, 34); f.Controls.Add(dica); y += 38;
                }

                y += 6;
                int bx = largura - 16;
                Button fechar = new Button(); fechar.Text = "Fechar"; fechar.DialogResult = DialogResult.Cancel;
                fechar.SetBounds(bx -= 100, y, 100, 30); f.Controls.Add(fechar); f.CancelButton = fechar;
                if (antesDeJogar && falta)
                {
                    Button jogar = new Button(); jogar.Text = "Jogar mesmo assim"; jogar.DialogResult = DialogResult.Ignore;
                    jogar.SetBounds(bx -= 150, y, 142, 30); f.Controls.Add(jogar);
                }
                if (falta)
                {
                    Button inst = new Button(); inst.Text = falhas != null ? "Tentar instalar de novo" : "Instalar automaticamente"; inst.DialogResult = DialogResult.Yes;
                    inst.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    inst.SetBounds(bx -= 190, y, 182, 30); f.Controls.Add(inst); f.AcceptButton = inst;
                }
                f.ClientSize = new Size(largura, y + 44);

                DialogResult res = f.ShowDialog(this);
                return res == DialogResult.Yes ? EscolhaReq.Instalar : res == DialogResult.Ignore ? EscolhaReq.Jogar : EscolhaReq.Fechar;
            }
        }

        /// <summary>Confere os programas obrigatórios; se falta algum, mostra a janela. true = pode seguir para o jogo.</summary>
        bool ConferirRequisitos(bool antesDeJogar, string titulo)
        {
            List<Requisito> faltam = Requisitos.Faltando();
            if (faltam.Count == 0) return true;
            List<string> nomes = faltam.ConvertAll(delegate(Requisito r) { return r.Nome; });
            Diagnostico.Log(root, "Requisitos faltando: " + string.Join(", ", nomes.ToArray()));
            EscolhaReq e = DialogoRequisitos(titulo ?? "Faltam programas do Windows para o jogo abrir",
                "O jogo precisa destes programas gratuitos da Microsoft. Sem eles, o jogo pode fechar sozinho logo ao abrir. "
                + "O launcher baixa e instala para você (o Windows vai pedir permissão).", null, antesDeJogar);
            if (e == EscolhaReq.Instalar) { InstalarRequisitos(antesDeJogar); return false; }
            if (e == EscolhaReq.Jogar) { Diagnostico.Log(root, "Jogador escolheu jogar sem instalar os requisitos."); return true; }
            return false;
        }

        void InstalarRequisitos(bool jogarDepois)
        {
            if (verificando) return;
            verificando = true;
            progresso = 0;
            status = "Instalando os programas obrigatórios...";
            Invalidate();
            string url = baseUrl;
            BackgroundWorker w = new BackgroundWorker();
            w.WorkerReportsProgress = true;
            w.DoWork += delegate(object s, DoWorkEventArgs e)
            {
                Dictionary<string, string> falhas = new Dictionary<string, string>();
                foreach (Requisito r in Requisitos.Faltando())
                {
                    Diagnostico.Log(root, "Instalando " + r.Nome + "...");
                    string erro;
                    try { erro = Requisitos.Instalar(r, url, delegate(int pct, string msg) { w.ReportProgress(pct, msg); }); }
                    catch (Exception ex) { erro = ex.Message; }
                    Diagnostico.Log(root, r.Nome + ": " + (erro == null ? "instalado." : "FALHOU: " + erro));
                    if (erro != null) falhas[r.Nome] = erro;
                }
                e.Result = falhas;
            };
            w.ProgressChanged += delegate(object s, ProgressChangedEventArgs e)
            {
                progresso = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                if (e.UserState != null) status = (string)e.UserState;
                Invalidate();
            };
            w.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                verificando = false;
                Dictionary<string, string> falhas = e.Error != null ? new Dictionary<string, string>() : (Dictionary<string, string>)e.Result;
                if (e.Error != null) foreach (Requisito r in Requisitos.Faltando()) falhas[r.Nome] = e.Error.Message;
                if (Requisitos.Faltando().Count == 0)
                {
                    progresso = 100;
                    status = "Programas obrigatórios instalados!" + (jogarDepois ? " Abrindo o jogo..." : " Clique em JOGAR.");
                    Invalidate();
                    if (jogarDepois) Play();
                    return;
                }
                progresso = 0;
                status = "Não consegui instalar tudo sozinho. Veja os links na janela.";
                Invalidate();
                if (DialogoRequisitos("Não consegui instalar tudo automaticamente",
                        "Instale manualmente pelos links abaixo (são downloads oficiais e gratuitos da Microsoft).", falhas, false) == EscolhaReq.Instalar)
                    InstalarRequisitos(jogarDepois);
            };
            w.RunWorkerAsync();
        }

        /// <summary>Botão DIAGNÓSTICO: gera o relatório, abre no Bloco de Notas e confere os requisitos.</summary>
        void RodarDiagnostico()
        {
            if (verificando) return;
            verificando = true;
            progresso = 30;
            status = "Gerando o diagnóstico do computador...";
            Invalidate();
            string pasta = root, url = baseUrl;
            BackgroundWorker w = new BackgroundWorker();
            w.DoWork += delegate(object s, DoWorkEventArgs e) { e.Result = Diagnostico.Salvar(pasta, Diagnostico.Relatorio(pasta, url, "pedido pelo botão DIAGNÓSTICO", ultimaAbertura)); };
            w.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                verificando = false;
                progresso = 100;
                if (e.Error != null) status = "Não consegui gerar o diagnóstico: " + e.Error.Message;
                else
                {
                    string arq = (string)e.Result;
                    status = "Diagnóstico salvo em " + arq;
                    Diagnostico.Log(root, "Diagnóstico gerado: " + arq);
                    try { Process.Start("notepad.exe", "\"" + arq + "\""); } catch { }
                }
                Invalidate();
                ConferirRequisitos(false, null);
            };
            w.RunWorkerAsync();
        }

        Abertura ultimaAbertura;

        /// <summary>Acompanha o jogo por 30 s depois do JOGAR: se ele fechar sozinho, grava o diagnóstico e avisa.</summary>
        void AcompanharJogo(Process p, DateTime inicio)
        {
            Abertura a = new Abertura();
            a.Inicio = inicio;
            try { a.Pid = p.Id; } catch { }
            string pasta = root, url = baseUrl;
            Thread t = new Thread(delegate()
            {
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    while (sw.Elapsed.TotalSeconds < 30)
                    {
                        if (p.WaitForExit(500)) break;
                        try { p.Refresh(); if (p.MainWindowHandle != IntPtr.Zero) a.JanelaApareceu = true; } catch { }
                    }
                    if (p.HasExited)
                    {
                        a.Saiu = true;
                        a.Codigo = p.ExitCode;
                        try { a.Segundos = (p.ExitTime - p.StartTime).TotalSeconds; } catch { a.Segundos = sw.Elapsed.TotalSeconds; }
                        a.OutroMainRodando = OutroMainRodando(pasta, a.Pid);
                    }
                }
                catch { }
                Diagnostico.Log(pasta, Diagnostico.ResumoAbertura(a));
                string relatorio = null;
                if (a.Falhou)
                    try { relatorio = Diagnostico.Salvar(pasta, Diagnostico.Relatorio(pasta, url, "o jogo fechou sozinho depois do JOGAR", a)); }
                    catch (Exception ex) { Diagnostico.Log(pasta, "Não consegui gravar o diagnóstico: " + ex.Message); }
                try { BeginInvoke((MethodInvoker)delegate { JogoAcompanhado(a, relatorio); }); } catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        static bool OutroMainRodando(string pasta, int pid)
        {
            foreach (Process o in Process.GetProcessesByName("main"))
                using (o)
                    try
                    {
                        if (o.Id != pid && !o.HasExited && string.Equals(Path.GetDirectoryName(o.MainModule.FileName).TrimEnd('\\'), pasta.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { }
            return false;
        }

        void JogoAcompanhado(Abertura a, string relatorio)
        {
            ultimaAbertura = a;
            if (!a.Falhou)
            {
                if (config.FecharAoJogar) Close();
                else { status = "Jogo aberto. Bom jogo!"; Invalidate(); }
                return;
            }
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            status = "O jogo fechou sozinho (código 0x" + ((uint)a.Codigo).ToString("X8") + "). " + (relatorio != null ? "Diagnóstico salvo." : "");
            Invalidate();
            if (Requisitos.Faltando().Count > 0)
            {
                ConferirRequisitos(true, "O jogo fechou sozinho: faltam programas do Windows");
                return;
            }
            string msg = "O jogo fechou sozinho " + a.Segundos.ToString("0.0") + " s depois de abrir.\r\n\r\n"
                       + Diagnostico.SignificadoCodigo(a.Codigo) + "\r\n\r\n"
                       + (relatorio != null ? "Salvei um diagnóstico completo em:\r\n" + relatorio + "\r\n\r\nQuer abrir o diagnóstico agora? (mande esse arquivo para o administrador do servidor)" : "");
            if (MessageBox.Show(this, msg, "Mu Chila", relatorio != null ? MessageBoxButtons.YesNo : MessageBoxButtons.OK, MessageBoxIcon.Warning) == DialogResult.Yes)
                try { Process.Start("notepad.exe", "\"" + relatorio + "\""); } catch { }
        }
    }
}

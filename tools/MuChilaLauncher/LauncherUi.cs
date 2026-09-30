// Mu Chila - Launcher: a janela (pedido do dono, 28/09/2026: "bem bonito e inovador", com configurações do jogo e
// verificação de integridade). Tudo desenhado à mão (GDI+): arte oficial do MU (duas telas de carregamento do próprio
// cliente, em arte1.jpg/arte2.jpg embutidas no .exe) alternando com transição, título dourado, status do servidor ao vivo,
// barra de progresso, botão JOGAR e um painel de configurações que desliza da direita.
// C# 5 (csc do .NET Framework): sem interpolação de string, sem "?.", sem membros com "=>".
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MuChilaLauncher
{
    static class Tema
    {
        public static readonly Color Ouro = Color.FromArgb(232, 176, 74);
        public static readonly Color OuroClaro = Color.FromArgb(255, 226, 158);
        public static readonly Color OuroEscuro = Color.FromArgb(140, 90, 22);
        public static readonly Color Texto = Color.FromArgb(242, 238, 230);
        public static readonly Color Apagado = Color.FromArgb(168, 160, 148);
        public static readonly Color Painel = Color.FromArgb(19, 18, 23);
        public static readonly Color Campo = Color.FromArgb(33, 31, 39);
        public static readonly Color Borda = Color.FromArgb(70, 60, 44);
        public static readonly Color Verde = Color.FromArgb(76, 217, 100);
        public static readonly Color Vermelho = Color.FromArgb(255, 92, 96);

        public static GraphicsPath Arredondado(RectangleF r, float raio)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Qualidade(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        /// <summary>Texto com espaçamento entre letras (títulos em caixa alta).</summary>
        public static void TextoEspacado(Graphics g, string s, Font f, Color c, float x, float y, float espaco)
        {
            using (SolidBrush b = new SolidBrush(c))
                foreach (char ch in s)
                {
                    string t = ch.ToString();
                    g.DrawString(t, f, b, x, y, StringFormat.GenericTypographic);
                    x += (ch == ' ' ? f.Size * 0.35f : g.MeasureString(t, f, PointF.Empty, StringFormat.GenericTypographic).Width) + espaco;
                }
        }

        public static float LarguraEspacada(Graphics g, string s, Font f, float espaco)
        {
            float w = 0;
            foreach (char ch in s) w += (ch == ' ' ? f.Size * 0.35f : g.MeasureString(ch.ToString(), f, PointF.Empty, StringFormat.GenericTypographic).Width) + espaco;
            return w;
        }
    }

    // ------------------------------------------------------------------ controles do painel de configurações
    class ControleDesenhado : Control
    {
        protected bool sobre, apertado;
        public ControleDesenhado()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Tema.Painel;
            ForeColor = Tema.Texto;
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { sobre = false; apertado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { apertado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { apertado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    class BotaoPlano : ControleDesenhado
    {
        public bool Primario;
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Tema.Qualidade(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Tema.Arredondado(r, 7))
            {
                if (Primario)
                {
                    Color a = Enabled ? (sobre ? Tema.OuroClaro : Color.FromArgb(246, 204, 120)) : Color.FromArgb(90, 84, 74);
                    Color b = Enabled ? (apertado ? Tema.OuroEscuro : Tema.Ouro) : Color.FromArgb(60, 56, 50);
                    using (LinearGradientBrush br = new LinearGradientBrush(r, a, b, 90f)) g.FillPath(br, p);
                }
                else
                {
                    using (SolidBrush br = new SolidBrush(sobre && Enabled ? Color.FromArgb(40, 232, 176, 74) : Tema.Campo)) g.FillPath(br, p);
                    using (Pen pen = new Pen(Enabled ? Color.FromArgb(sobre ? 220 : 120, Tema.Ouro) : Tema.Borda)) g.DrawPath(pen, p);
                }
            }
            Color cor = Primario ? Color.FromArgb(46, 28, 6) : (Enabled ? Tema.Texto : Tema.Apagado);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, cor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Interruptor liga/desliga.</summary>
    class Chave : ControleDesenhado
    {
        bool ligado;
        public event EventHandler Mudou;
        public bool Ligado { get { return ligado; } set { ligado = value; Invalidate(); } }
        public Chave() { Size = new Size(46, 24); }
        protected override void OnClick(EventArgs e) { Ligado = !ligado; if (Mudou != null) Mudou(this, e); base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Tema.Qualidade(g);
            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (GraphicsPath p = Tema.Arredondado(r, r.Height / 2))
            {
                using (SolidBrush b = new SolidBrush(ligado ? Tema.Ouro : Tema.Campo)) g.FillPath(b, p);
                using (Pen pen = new Pen(ligado ? Tema.OuroClaro : Tema.Borda)) g.DrawPath(pen, p);
            }
            float d = r.Height - 6;
            float x = ligado ? r.Right - d - 3 : r.X + 3;
            using (SolidBrush b = new SolidBrush(ligado ? Color.FromArgb(46, 28, 6) : Tema.Apagado)) g.FillEllipse(b, x, r.Y + 3, d, d);
        }
    }

    /// <summary>Escolha entre poucas opções, lado a lado (idioma).</summary>
    class Segmentos : ControleDesenhado
    {
        public string[] Opcoes = new string[0];
        int selecionado;
        public event EventHandler Mudou;
        public int Selecionado { get { return selecionado; } set { selecionado = value; Invalidate(); } }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (Opcoes.Length == 0) return;
            int i = Math.Min(Opcoes.Length - 1, e.X * Opcoes.Length / Math.Max(1, Width));
            if (i != selecionado) { Selecionado = i; if (Mudou != null) Mudou(this, e); }
            base.OnMouseClick(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Tema.Qualidade(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Tema.Arredondado(r, 7))
            {
                using (SolidBrush b = new SolidBrush(Tema.Campo)) g.FillPath(b, p);
                using (Pen pen = new Pen(Tema.Borda)) g.DrawPath(pen, p);
            }
            float w = r.Width / Math.Max(1, Opcoes.Length);
            for (int i = 0; i < Opcoes.Length; i++)
            {
                RectangleF s = new RectangleF(r.X + i * w + 3, r.Y + 3, w - 6, r.Height - 6);
                if (i == selecionado)
                    using (GraphicsPath p = Tema.Arredondado(s, 5))
                    using (LinearGradientBrush b = new LinearGradientBrush(s, Color.FromArgb(246, 204, 120), Tema.Ouro, 90f)) g.FillPath(b, p);
                TextRenderer.DrawText(g, Opcoes[i], Font, Rectangle.Round(s), i == selecionado ? Color.FromArgb(46, 28, 6) : Tema.Texto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>Barra deslizante de Min a Max (volume).</summary>
    class Deslizante : ControleDesenhado
    {
        public int Min = 0, Max = 10;
        int valor;
        bool arrastando;
        public event EventHandler Mudou;
        public int Valor { get { return valor; } set { valor = Math.Max(Min, Math.Min(Max, value)); Invalidate(); } }
        public Deslizante() { Height = 26; }
        void Posicionar(int x)
        {
            float t = (x - 10f) / Math.Max(1f, Width - 20f);
            int v = Min + (int)Math.Round(Math.Max(0, Math.Min(1, t)) * (Max - Min));
            if (v != valor) { Valor = v; if (Mudou != null) Mudou(this, EventArgs.Empty); }
        }
        protected override void OnMouseDown(MouseEventArgs e) { arrastando = true; Posicionar(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (arrastando) Posicionar(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { arrastando = false; base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Tema.Qualidade(g);
            float y = Height / 2f, x0 = 10, x1 = Width - 10;
            float xv = x0 + (x1 - x0) * (valor - Min) / Math.Max(1f, Max - Min);
            using (Pen p = new Pen(Tema.Campo, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x0, y, x1, y);
            if (xv > x0) using (Pen p = new Pen(Tema.Ouro, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(p, x0, y, xv, y);
            using (SolidBrush b = new SolidBrush(sobre || arrastando ? Tema.OuroClaro : Tema.Texto)) g.FillEllipse(b, xv - 8, y - 8, 16, 16);
        }
    }

    // ------------------------------------------------------------------ a janela
    partial class LauncherForm
    {
        const int W = 1000, H = 600, Porta = 44405;

        /// <summary>
        /// Resoluções do jogo: o cliente guarda o ÍNDICE desta tabela em DisplayDeviceModeIndex (medido no cliente S14:
        /// 0 = 800x600 ... 10 = 1440x900; índice fora da tabela vira 800x600).
        /// </summary>
        static readonly int[,] Resolucoes = { { 800, 600 }, { 1024, 768 }, { 1152, 864 }, { 1280, 720 }, { 1280, 800 }, { 1280, 960 },
                                              { 1440, 1080 }, { 1600, 900 }, { 1680, 1050 }, { 1920, 1080 }, { 1440, 900 } };

        /// <summary>Configurações do jogo (registro HKCU\Software\Webzen\Mu\Config, lido pelo main.exe) e do launcher.</summary>
        class ConfigJogo
        {
            const string Chave = @"Software\Webzen\Mu\Config";
            public int Resolucao = 9, Volume = 5;
            public bool TelaCheia = true, FecharAoJogar = true;
            public string Idioma = "Por";

            public static ConfigJogo Ler()
            {
                ConfigJogo c = new ConfigJogo();
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Chave))
                        if (k != null)
                        {
                            c.Resolucao = Convert.ToInt32(k.GetValue("DisplayDeviceModeIndex", c.Resolucao));
                            c.TelaCheia = Convert.ToInt32(k.GetValue("FullScreenMode", 1)) != 0;
                            c.Volume = Convert.ToInt32(k.GetValue("VolumeLevel", c.Volume));
                            string l = k.GetValue("LangSelection", c.Idioma) as string;
                            if (l == "Eng" || l == "Por") c.Idioma = l;
                        }
                }
                catch { }
                if (c.Resolucao < 0 || c.Resolucao >= Resolucoes.GetLength(0)) c.Resolucao = 0;
                c.Volume = Math.Max(0, Math.Min(10, c.Volume));
                c.FecharAoJogar = LerIni(ConfigPath, "FecharAoJogar") != "0";
                return c;
            }

            public void Gravar()
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Chave))
                {
                    k.SetValue("DisplayDeviceModeIndex", Resolucao, RegistryValueKind.DWord);
                    k.SetValue("FullScreenMode", TelaCheia ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("VolumeLevel", Volume, RegistryValueKind.DWord);
                    k.SetValue("LangSelection", Idioma, RegistryValueKind.String);
                }
                SalvarIni("FecharAoJogar", FecharAoJogar ? "1" : "0");
            }
        }

        ConfigJogo config;
        string status = "Preparando...";
        string versaoCliente;
        int progresso;
        bool verificando, integridadeAtual, modoFoto;
        int servidor = -1;   // -1 conferindo, 0 offline, 1 online
        string hostServidor;

        Bitmap[] artes = new Bitmap[0];
        int arte;            // arte na tela
        int proxima = -1;    // arte entrando (transição)
        float fade;          // 0..1 da transição
        DateTime proximaTroca = DateTime.Now.AddSeconds(9);
        readonly System.Windows.Forms.Timer relogio = new System.Windows.Forms.Timer();

        readonly Dictionary<string, Rectangle> areas = new Dictionary<string, Rectangle>();
        string sobreArea, apertadaArea;
        Panel painel;
        Chave chkTela, chkFechar;
        Segmentos segIdioma;
        Deslizante volume;
        Label lblVolume, lblPastaCfg;
        ComboBox cmbRes;
        int painelDestino = W;   // Left alvo da animação do painel

        static readonly Font FonteTitulo = new Font("Georgia", 52f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font FonteMarca = new Font("Georgia", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font FonteSub = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Pixel);
        static readonly Font FonteNav = new Font("Segoe UI Semibold", 12f, GraphicsUnit.Pixel);
        static readonly Font FonteTexto = new Font("Segoe UI", 14f, GraphicsUnit.Pixel);
        static readonly Font FontePequena = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
        static readonly Font FonteJogar = new Font("Georgia", 26f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font FonteBotao = new Font("Segoe UI Semibold", 12.5f, GraphicsUnit.Pixel);

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* CS_DROPSHADOW */ return cp; }
        }

        void MontarJanela()
        {
            config = ConfigJogo.Ler();
            try { hostServidor = new Uri(baseUrl).Host; } catch { hostServidor = "26.139.39.123"; }

            Text = "Mu Chila";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(W, H);
            BackColor = Color.Black;
            Font = new Font("Segoe UI", 9f);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            using (GraphicsPath p = Tema.Arredondado(new RectangleF(0, 0, W, H), 14)) Region = new Region(p);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            artes = new Bitmap[] { CarregarArte("arte1.jpg"), CarregarArte("arte2.jpg") };
            artes = Array.FindAll(artes, delegate(Bitmap b) { return b != null; });

            MontarPainel();

            relogio.Interval = 30;
            relogio.Tick += delegate { Animar(); };
            relogio.Start();

            Shown += delegate
            {
                if (modoFoto) return;
                Diagnostico.Log(root, "Launcher " + Diagnostico.Versao + " aberto (" + Environment.OSVersion + ", " + (Environment.Is64BitOperatingSystem ? "64" : "32") + " bits).");
                if (root == null && !EscolherPasta(true)) { Close(); return; }
                StartCheck(false);
                Thread t = new Thread(VigiarServidor);
                t.IsBackground = true;
                t.Start();
            };
            FormClosed += delegate { relogio.Stop(); };
        }

        /// <summary>Arte embutida (recurso), redimensionada para cobrir a janela.</summary>
        static Bitmap CarregarArte(string nome)
        {
            try
            {
                using (Stream s = typeof(LauncherForm).Assembly.GetManifestResourceStream(nome))
                {
                    if (s == null) return null;
                    using (Image img = Image.FromStream(s))
                    {
                        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(b))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            float esc = Math.Max((float)W / img.Width, (float)H / img.Height);
                            float w = img.Width * esc, h = img.Height * esc;
                            g.DrawImage(img, (W - w) / 2f, (H - h) / 2f, w, h);
                        }
                        return b;
                    }
                }
            }
            catch { return null; }
        }

        // ---------------- animação e servidor ----------------
        void Animar()
        {
            bool mudou = false;
            if (painel != null && painel.Left != painelDestino)
            {
                int d = painelDestino - painel.Left;
                painel.Left += Math.Abs(d) < 6 ? d : d / 3;
                if (painel.Left >= W) painel.Visible = false;
            }
            if (artes.Length > 1)
            {
                if (proxima < 0 && DateTime.Now >= proximaTroca) { proxima = (arte + 1) % artes.Length; fade = 0; }
                if (proxima >= 0)
                {
                    fade += 0.03f;
                    if (fade >= 1) { arte = proxima; proxima = -1; fade = 0; proximaTroca = DateTime.Now.AddSeconds(9); }
                    mudou = true;
                }
            }
            if (verificando) mudou = true;   // brilho correndo na barra
            if (mudou) Invalidate();
        }

        /// <summary>Confere a cada 20 s se o ConnectServer responde (o mesmo endereço do site/launcher, porta 44405).</summary>
        void VigiarServidor()
        {
            while (!IsDisposed)
            {
                int r = 0;
                try
                {
                    using (TcpClient c = new TcpClient())
                    {
                        IAsyncResult ar = c.BeginConnect(hostServidor, Porta, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(2500) && c.Connected) r = 1;
                    }
                }
                catch { r = 0; }
                try { BeginInvoke((MethodInvoker)delegate { servidor = r; Invalidate(); }); } catch { return; }
                Thread.Sleep(20000);
            }
        }

        // ---------------- verificação ----------------
        void StartCheck(bool integridade)
        {
            if (verificando) return;
            verificando = true;
            integridadeAtual = integridade;
            progresso = 0;
            status = integridade ? "Conferindo a integridade de todos os arquivos..." : "Procurando atualizações...";
            Invalidate();
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object s, DoWorkEventArgs e) { e.Result = Core((BackgroundWorker)s, true, integridade); };
            worker.ProgressChanged += delegate(object s, ProgressChangedEventArgs e)
            {
                progresso = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                if (e.UserState != null) status = (string)e.UserState;
                Invalidate();
            };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                verificando = false;
                if (selfUpdating) { Close(); return; }
                bool temJogo = TemJogo();
                if (e.Error != null)
                    status = "Não foi possível atualizar: " + e.Error.Message
                           + (temJogo ? " Você ainda pode jogar com os arquivos atuais." : " Clique em \"Verificar integridade\" para tentar de novo.");
                else if (e.Result != null)
                    status = (string)e.Result;
                if (e.Error == null)
                    try
                    {
                        if (GarantirAtalho(root, Path.GetFullPath(Application.ExecutablePath), ConfigPath, AtalhoPadrao))
                            status += "  Atalho \"Mu Chila\" criado na área de trabalho.";
                    }
                    catch (Exception ex) { status += "  (Não consegui criar o atalho: " + ex.Message + ")"; }
                progresso = e.Error == null ? 100 : 0;
                Diagnostico.Log(root, (integridade ? "Integridade: " : "Atualização: ") + (e.Error != null ? "ERRO " + e.Error.Message : status));
                AtualizarPainel();
                Invalidate();
                // jogo pronto: já avisa (e oferece instalar) se faltar Visual C++ 2013 ou DirectX neste computador
                if (temJogo) ConferirRequisitos(false, null);
            };
            worker.RunWorkerAsync();
        }

        bool TemJogo() { return root != null && File.Exists(Path.Combine(root, "main.exe")); }

        // ---------------- desenho ----------------
        protected override void OnPaint(PaintEventArgs e)
        {
            // um erro dentro do desenho deixaria a janela com o "X vermelho" do WinForms para sempre: melhor seguir sem o enfeite
            try { Desenhar(e.Graphics); }
            catch (Exception ex)
            {
                e.Graphics.ResetClip();
                TextRenderer.DrawText(e.Graphics, "Erro ao desenhar a janela: " + ex.Message, Font, new Point(20, H - 30), Color.White);
            }
        }

        void Desenhar(Graphics g)
        {
            Tema.Qualidade(g);
            areas.Clear();

            // 1) arte (com transição) e véus para o texto ler bem
            if (artes.Length > 0)
            {
                g.DrawImageUnscaled(artes[arte], 0, 0);
                if (proxima >= 0)
                {
                    ColorMatrix m = new ColorMatrix();
                    m.Matrix33 = fade;
                    using (ImageAttributes ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(m);
                        g.DrawImage(artes[proxima], new Rectangle(0, 0, W, H), 0, 0, W, H, GraphicsUnit.Pixel, ia);
                    }
                }
            }
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(235, 8, 7, 10), Color.FromArgb(0, 8, 7, 10), 0f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Color.FromArgb(235, 8, 7, 10), Color.FromArgb(170, 8, 7, 10), Color.FromArgb(0, 8, 7, 10), Color.FromArgb(0, 8, 7, 10) };
                cb.Positions = new float[] { 0f, 0.38f, 0.72f, 1f };   // a lista tem de ir de 0 a 1
                b.InterpolationColors = cb;
                g.FillRectangle(b, 0, 0, W, H);
            }
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, H - 190, W, 190), Color.FromArgb(0, 6, 5, 8), Color.FromArgb(245, 6, 5, 8), 90f))
                g.FillRectangle(b, 0, H - 190, W, 190);
            using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, W, 90), Color.FromArgb(200, 6, 5, 8), Color.FromArgb(0, 6, 5, 8), 90f))
                g.FillRectangle(b, 0, 0, W, 90);

            DesenharTopo(g);
            DesenharTitulo(g);
            DesenharRodape(g);

            // moldura fina dourada
            using (GraphicsPath p = Tema.Arredondado(new RectangleF(0.5f, 0.5f, W - 1.5f, H - 1.5f), 14))
            using (Pen pen = new Pen(Color.FromArgb(90, Tema.Ouro))) g.DrawPath(pen, p);
        }

        void DesenharTopo(Graphics g)
        {
            // marca: losango dourado + MU CHILA
            PointF[] los = { new PointF(34, 22), new PointF(44, 32), new PointF(34, 42), new PointF(24, 32) };
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(24, 22, 20, 20), Tema.OuroClaro, Tema.OuroEscuro, 90f)) g.FillPolygon(b, los);
            Tema.TextoEspacado(g, "MU CHILA", FonteMarca, Tema.OuroClaro, 54, 23, 2.2f);

            // navegação
            float x = 210;
            foreach (string[] item in new string[][] { new string[] { "inicio", "INÍCIO" }, new string[] { "config", "CONFIGURAÇÕES" }, new string[] { "site", "SITE" } })
            {
                float w = Tema.LarguraEspacada(g, item[1], FonteNav, 1.6f);
                Rectangle r = new Rectangle((int)x - 6, 14, (int)w + 12, 34);
                areas[item[0]] = r;
                bool ativo = item[0] == "config" ? painel.Visible : item[0] == "inicio" && !painel.Visible;
                Color c = ativo ? Tema.OuroClaro : (sobreArea == item[0] ? Tema.Texto : Tema.Apagado);
                Tema.TextoEspacado(g, item[1], FonteNav, c, x, 24, 1.6f);
                if (ativo)
                    using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, 43, w, 2), Tema.Ouro, Color.FromArgb(0, Tema.Ouro), 0f))
                        g.FillRectangle(b, x, 43, w, 2);
                x += w + 34;
            }

            // status do servidor
            string st = servidor < 0 ? "CONFERINDO SERVIDOR" : servidor == 1 ? "SERVIDOR ONLINE" : "SERVIDOR OFFLINE";
            Color cs = servidor < 0 ? Tema.Apagado : servidor == 1 ? Tema.Verde : Tema.Vermelho;
            float ws = Tema.LarguraEspacada(g, st, FonteNav, 1.2f);
            RectangleF pill = new RectangleF(W - 110 - ws - 34, 18, ws + 34, 26);
            using (GraphicsPath p = Tema.Arredondado(pill, 13))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 12, 11, 15))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(110, cs))) g.DrawPath(pen, p);
            }
            float pulso = servidor == 1 ? (float)(0.5 + 0.5 * Math.Sin(Environment.TickCount / 300.0)) : 0;
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(60 + 90 * pulso), cs))) g.FillEllipse(b, pill.X + 9, pill.Y + 7, 12, 12);
            using (SolidBrush b = new SolidBrush(cs)) g.FillEllipse(b, pill.X + 12, pill.Y + 10, 6, 6);
            Tema.TextoEspacado(g, st, FonteNav, Tema.Texto, pill.X + 26, pill.Y + 5, 1.2f);

            // minimizar e fechar
            Rectangle rmin = new Rectangle(W - 86, 16, 30, 30), rfec = new Rectangle(W - 48, 16, 30, 30);
            areas["min"] = rmin; areas["fechar"] = rfec;
            if (sobreArea == "min") using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillEllipse(b, rmin);
            if (sobreArea == "fechar") using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 200, 50, 50))) g.FillEllipse(b, rfec);
            using (Pen p = new Pen(Tema.Texto, 1.6f))
            {
                g.DrawLine(p, rmin.X + 10, rmin.Y + 16, rmin.Right - 10, rmin.Y + 16);
                g.DrawLine(p, rfec.X + 10, rfec.Y + 10, rfec.Right - 10, rfec.Bottom - 10);
                g.DrawLine(p, rfec.Right - 10, rfec.Y + 10, rfec.X + 10, rfec.Bottom - 10);
            }
        }

        void DesenharTitulo(Graphics g)
        {
            float x = 58, y = 168;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddString("MU CHILA", FonteTitulo.FontFamily, (int)FontStyle.Bold, FonteTitulo.Size, new PointF(x, y), StringFormat.GenericTypographic);
                // sombra e brilho
                for (int i = 6; i >= 1; i--)
                    using (Pen pen = new Pen(Color.FromArgb(14, 0, 0, 0), i * 2.4f) { LineJoin = LineJoin.Round })
                    {
                        using (Matrix mt = new Matrix()) { mt.Translate(0, 3); p.Transform(mt); }
                        g.DrawPath(pen, p);
                        using (Matrix mt = new Matrix()) { mt.Translate(0, -3); p.Transform(mt); }
                    }
                using (Pen pen = new Pen(Color.FromArgb(55, Tema.Ouro), 7) { LineJoin = LineJoin.Round }) g.DrawPath(pen, p);
                RectangleF br = p.GetBounds();
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(br.X, br.Y - 2, br.Width, br.Height + 4), Tema.OuroClaro, Tema.OuroEscuro, 90f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Colors = new Color[] { Color.FromArgb(255, 244, 214), Tema.OuroClaro, Tema.Ouro, Tema.OuroEscuro };
                    cb.Positions = new float[] { 0f, 0.35f, 0.6f, 1f };
                    b.InterpolationColors = cb;
                    g.FillPath(b, p);
                }
                using (Pen pen = new Pen(Color.FromArgb(160, 90, 55, 10), 1.2f)) g.DrawPath(pen, p);
            }
            // linha e subtítulo
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, y + 74, 300, 2), Tema.Ouro, Color.FromArgb(0, Tema.Ouro), 0f))
                g.FillRectangle(b, x + 2, y + 74, 300, 2);
            Tema.TextoEspacado(g, "SEASON 14  ·  SERVIDOR BRASILEIRO", FonteSub, Tema.Texto, x + 2, y + 86, 3f);
            using (SolidBrush b = new SolidBrush(Tema.Apagado))
                g.DrawString("O continente de MU espera por você. Reúna sua party,\nsuba de nível e escreva seu nome na história.", FonteTexto, b, x + 2, y + 122);
        }

        void DesenharRodape(Graphics g)
        {
            int x = 58, yStatus = H - 128;
            using (SolidBrush b = new SolidBrush(Tema.Texto))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(status, FonteTexto, b, new RectangleF(x, yStatus, 590, 22), sf);
            string info = (versaoCliente != null ? "Cliente " + versaoCliente + "   ·   " : "") + "Pasta: " + (root ?? "(ainda não escolhida)");
            using (SolidBrush b = new SolidBrush(Tema.Apagado))
            using (StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(info, FontePequena, b, new RectangleF(x, yStatus + 24, 590, 18), sf);

            // barra de progresso
            RectangleF trilho = new RectangleF(x, yStatus + 52, 560, 8);
            using (GraphicsPath p = Tema.Arredondado(trilho, 4)) using (SolidBrush b = new SolidBrush(Color.FromArgb(160, 40, 38, 46))) g.FillPath(b, p);
            float w = trilho.Width * progresso / 100f;
            if (w > 2)
            {
                RectangleF cheio = new RectangleF(trilho.X, trilho.Y, w, trilho.Height);
                using (GraphicsPath p = Tema.Arredondado(cheio, 4))
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(cheio, Tema.OuroEscuro, Tema.OuroClaro, 0f)) g.FillPath(b, p);
                    if (verificando)
                    {   // brilho correndo sobre a parte cheia
                        float t = (Environment.TickCount % 1600) / 1600f;
                        float bx = cheio.X - 80 + (cheio.Width + 160) * t;
                        g.SetClip(p);
                        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(bx, cheio.Y, 80, cheio.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                        {
                            ColorBlend cb = new ColorBlend();
                            cb.Colors = new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(150, 255, 250, 230), Color.FromArgb(0, 255, 255, 255) };
                            cb.Positions = new float[] { 0f, 0.5f, 1f };
                            b.InterpolationColors = cb;
                            g.FillRectangle(b, bx, cheio.Y, 80, cheio.Height);
                        }
                        g.ResetClip();
                    }
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, Tema.Ouro))) g.FillEllipse(b, trilho.X + w - 7, trilho.Y - 3, 14, 14);
            }
            TextRenderer.DrawText(g, progresso + "%", FontePequena, new Point((int)trilho.Right + 12, (int)trilho.Y - 5), Tema.Apagado);

            // botões secundários
            BotaoFantasma(g, "integridade", "VERIFICAR INTEGRIDADE", new Rectangle(x, H - 50, 214, 32), !verificando && root != null);
            BotaoFantasma(g, "config2", "CONFIGURAÇÕES", new Rectangle(x + 226, H - 50, 170, 32), true);
            BotaoFantasma(g, "diagnostico", "DIAGNÓSTICO", new Rectangle(x + 408, H - 50, 152, 32), !verificando);

            // JOGAR
            bool pode = !verificando && TemJogo();
            Rectangle rj = new Rectangle(W - 58 - 250, H - 120, 250, 78);
            areas["jogar"] = rj;
            bool hov = pode && sobreArea == "jogar", aperta = pode && apertadaArea == "jogar";
            if (pode)
                for (int i = 4; i >= 1; i--)
                    using (GraphicsPath p = Tema.Arredondado(new RectangleF(rj.X - i * 3, rj.Y - i * 3, rj.Width + i * 6, rj.Height + i * 6), 12 + i * 3))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(hov ? 20 : 11, Tema.Ouro))) g.FillPath(b, p);
            using (GraphicsPath p = Tema.Arredondado(rj, 12))
            {
                Color a = pode ? (hov ? Color.FromArgb(255, 238, 188) : Tema.OuroClaro) : Color.FromArgb(80, 76, 70);
                Color c = pode ? (aperta ? Tema.OuroEscuro : Tema.Ouro) : Color.FromArgb(52, 49, 45);
                using (LinearGradientBrush b = new LinearGradientBrush(rj, a, c, 90f)) g.FillPath(b, p);
                using (Pen pen = new Pen(pode ? Color.FromArgb(255, 246, 220) : Color.FromArgb(90, 86, 80), 1.2f)) g.DrawPath(pen, p);
                using (GraphicsPath brilho = Tema.Arredondado(new RectangleF(rj.X + 3, rj.Y + 3, rj.Width - 6, rj.Height / 2f - 3), 10))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(pode ? 45 : 15, 255, 255, 255))) g.FillPath(b, brilho);
            }
            string tj = verificando ? "AGUARDE" : TemJogo() ? "JOGAR" : "INSTALAR";
            if (!verificando && !TemJogo()) areas["instalar"] = rj;
            float wj = Tema.LarguraEspacada(g, tj, FonteJogar, 4f);
            Tema.TextoEspacado(g, tj, FonteJogar, pode ? Color.FromArgb(52, 30, 4) : Color.FromArgb(150, 145, 138), rj.X + (rj.Width - wj) / 2f, rj.Y + 22, 4f);

            // bolinhas das artes
            for (int i = 0; i < artes.Length; i++)
            {
                Rectangle rd = new Rectangle(rj.X + rj.Width / 2 - artes.Length * 10 + i * 20 + 4, rj.Bottom + 14, 12, 12);
                areas["arte" + i] = new Rectangle(rd.X - 4, rd.Y - 4, 20, 20);
                bool atual = (proxima >= 0 ? proxima : arte) == i;
                using (SolidBrush b = new SolidBrush(atual ? Tema.Ouro : Color.FromArgb(sobreArea == "arte" + i ? 160 : 90, 200, 190, 170)))
                    g.FillEllipse(b, atual ? rd : Rectangle.Inflate(rd, -2, -2));
            }
        }

        void BotaoFantasma(Graphics g, string id, string texto, Rectangle r, bool habilitado)
        {
            if (habilitado) areas[id] = r;
            bool hov = habilitado && sobreArea == id;
            using (GraphicsPath p = Tema.Arredondado(r, 7))
            {
                using (SolidBrush b = new SolidBrush(hov ? Color.FromArgb(60, Tema.Ouro) : Color.FromArgb(120, 14, 13, 18))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(habilitado ? (hov ? 230 : 130) : 50, Tema.Ouro))) g.DrawPath(pen, p);
            }
            float w = Tema.LarguraEspacada(g, texto, FonteBotao, 1.4f);
            Tema.TextoEspacado(g, texto, FonteBotao, habilitado ? Tema.Texto : Color.FromArgb(110, 104, 96), r.X + (r.Width - w) / 2f, r.Y + 8, 1.4f);
        }

        // ---------------- mouse ----------------
        string AreaEm(Point p)
        {
            foreach (KeyValuePair<string, Rectangle> kv in areas) if (kv.Value.Contains(p)) return kv.Key;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            string a = AreaEm(e.Location);
            if (a != sobreArea) { sobreArea = a; Cursor = a != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { sobreArea = null; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            string a = AreaEm(e.Location);
            if (a == null && e.Button == MouseButtons.Left)
            {   // arrastar a janela por qualquer parte vazia
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 2, 0);
                return;
            }
            apertadaArea = a; Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            string a = AreaEm(e.Location);
            string ap = apertadaArea;
            apertadaArea = null; Invalidate();
            if (a != null && a == ap) Clique(a);
            base.OnMouseUp(e);
        }

        void Clique(string a)
        {
            if (a == "fechar") Close();
            else if (a == "min") WindowState = FormWindowState.Minimized;
            else if (a == "jogar") { if (!verificando && TemJogo()) Play(); }
            else if (a == "instalar") StartCheck(false);
            else if (a == "integridade") StartCheck(true);
            else if (a == "diagnostico") RodarDiagnostico();
            else if (a == "config" || a == "config2") AbrirConfig(!painel.Visible || painelDestino >= W);
            else if (a == "inicio") AbrirConfig(false);
            else if (a == "site") AbrirSite();
            else if (a.StartsWith("arte")) { int i = int.Parse(a.Substring(4)); if (i != arte && proxima < 0) { proxima = i; fade = 0; } }
        }

        void AbrirSite()
        {
            try { Process.Start("http://" + hostServidor + "/"); }
            catch (Exception ex) { status = "Não consegui abrir o site: " + ex.Message; Invalidate(); }
        }

        // ---------------- painel de configurações ----------------
        void MontarPainel()
        {
            painel = new Panel();
            painel.SetBounds(W, 66, 420, H - 66 - 16);
            painel.BackColor = Tema.Painel;
            painel.Visible = false;
            Font fonteCfg = new Font("Georgia", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
            painel.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics; Tema.Qualidade(g);
                Tema.TextoEspacado(g, "CONFIGURAÇÕES", fonteCfg, Tema.OuroClaro, 26, 20, 2f);
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(26, 50, 200, 2), Tema.Ouro, Color.FromArgb(0, Tema.Ouro), 0f)) g.FillRectangle(b, 26, 50, 200, 2);
                Tema.TextoEspacado(g, "JOGO", FonteNav, Tema.Ouro, 26, 66, 2f);
                Tema.TextoEspacado(g, "LAUNCHER", FonteNav, Tema.Ouro, 26, 290, 2f);
                using (Pen p = new Pen(Color.FromArgb(80, Tema.Ouro))) g.DrawLine(p, 0, 0, 0, painel.Height);
            };
            using (GraphicsPath p = Tema.Arredondado(new RectangleF(0, 0, 420, painel.Height), 12)) painel.Region = new Region(p);

            int lx = 26, cx = 170, cw = 222;

            painel.Controls.Add(Rotulo("Resolução", lx, 92));
            cmbRes = new ComboBox();
            cmbRes.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRes.FlatStyle = FlatStyle.Flat;
            cmbRes.DrawMode = DrawMode.OwnerDrawFixed;
            cmbRes.ItemHeight = 24;
            cmbRes.BackColor = Tema.Campo;
            cmbRes.ForeColor = Tema.Texto;
            cmbRes.Font = new Font("Segoe UI", 10f);
            cmbRes.SetBounds(cx, 88, cw, 30);
            Rectangle tela = Screen.PrimaryScreen.Bounds;
            List<int> ordem = new List<int>();
            for (int i = 0; i < Resolucoes.GetLength(0); i++) ordem.Add(i);
            ordem.Sort(delegate(int a, int b)
            {
                int c = Resolucoes[a, 0].CompareTo(Resolucoes[b, 0]);
                return c != 0 ? c : Resolucoes[a, 1].CompareTo(Resolucoes[b, 1]);
            });
            foreach (int i in ordem)
            {
                bool grande = Resolucoes[i, 0] > tela.Width || Resolucoes[i, 1] > tela.Height;
                cmbRes.Items.Add(new ItemRes(i, Resolucoes[i, 0] + " x " + Resolucoes[i, 1] + (grande ? "   (maior que a tela)" : "")));
            }
            cmbRes.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
                using (SolidBrush b = new SolidBrush(sel ? Color.FromArgb(90, Tema.Ouro) : Tema.Campo)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, cmbRes.Items[e.Index].ToString(), cmbRes.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                    Tema.Texto, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
            painel.Controls.Add(cmbRes);

            painel.Controls.Add(Rotulo("Tela cheia", lx, 138));
            chkTela = new Chave(); chkTela.Location = new Point(cx, 136);
            painel.Controls.Add(chkTela);
            Label dicaTela = Rotulo("desligada = janela", cx + 58, 139); dicaTela.ForeColor = Tema.Apagado; dicaTela.Font = new Font("Segoe UI", 9f);
            painel.Controls.Add(dicaTela);

            painel.Controls.Add(Rotulo("Idioma", lx, 184));
            segIdioma = new Segmentos(); segIdioma.Opcoes = new string[] { "Português", "English" }; segIdioma.Font = new Font("Segoe UI", 9.5f);
            segIdioma.SetBounds(cx, 178, cw, 32);
            painel.Controls.Add(segIdioma);

            painel.Controls.Add(Rotulo("Volume", lx, 232));
            volume = new Deslizante(); volume.SetBounds(cx - 8, 228, cw - 30, 26);
            lblVolume = Rotulo("5", cx + cw - 30, 232); lblVolume.ForeColor = Tema.OuroClaro;
            volume.Mudou += delegate { lblVolume.Text = volume.Valor.ToString(); };
            painel.Controls.Add(volume); painel.Controls.Add(lblVolume);

            painel.Controls.Add(Rotulo("Pasta do jogo", lx, 318));
            lblPastaCfg = Rotulo("", lx, 342); lblPastaCfg.AutoSize = false; lblPastaCfg.SetBounds(lx, 342, 260, 20);
            lblPastaCfg.ForeColor = Tema.Apagado; lblPastaCfg.AutoEllipsis = true; lblPastaCfg.Font = new Font("Segoe UI", 9f);
            painel.Controls.Add(lblPastaCfg);
            BotaoPlano bPasta = new BotaoPlano(); bPasta.Text = "Alterar..."; bPasta.Font = new Font("Segoe UI", 9f); bPasta.SetBounds(296, 336, 96, 30);
            bPasta.Click += delegate { if (!verificando && EscolherPasta(false)) { AtualizarPainel(); StartCheck(false); } };
            painel.Controls.Add(bPasta);

            painel.Controls.Add(Rotulo("Fechar o launcher ao abrir o jogo", lx, 386));
            chkFechar = new Chave(); chkFechar.Location = new Point(346, 384);
            painel.Controls.Add(chkFechar);

            Label nota = Rotulo("Resolução, tela cheia, idioma e volume valem na próxima vez que o jogo abrir.", lx, 430);
            nota.AutoSize = false; nota.SetBounds(lx, 418, 370, 34); nota.ForeColor = Tema.Apagado; nota.Font = new Font("Segoe UI", 9f);
            painel.Controls.Add(nota);

            BotaoPlano salvar = new BotaoPlano(); salvar.Primario = true; salvar.Text = "SALVAR"; salvar.Font = new Font("Segoe UI Semibold", 10f);
            salvar.SetBounds(lx, painel.Height - 58, 180, 38);
            salvar.Click += delegate { SalvarConfig(); };
            BotaoPlano fechar = new BotaoPlano(); fechar.Text = "FECHAR"; fechar.Font = new Font("Segoe UI Semibold", 10f);
            fechar.SetBounds(lx + 192, painel.Height - 58, 176, 38);
            fechar.Click += delegate { AbrirConfig(false); };
            painel.Controls.Add(salvar); painel.Controls.Add(fechar);

            Controls.Add(painel);
            AtualizarPainel();
        }

        static Label Rotulo(string texto, int x, int y)
        {
            Label l = new Label();
            l.Text = texto; l.AutoSize = true; l.Location = new Point(x, y);
            l.ForeColor = Tema.Texto; l.BackColor = Tema.Painel; l.Font = new Font("Segoe UI", 10f);
            return l;
        }

        class ItemRes
        {
            public readonly int Indice; readonly string texto;
            public ItemRes(int i, string t) { Indice = i; texto = t; }
            public override string ToString() { return texto; }
        }

        /// <summary>Põe no painel o que está salvo (registro do jogo e launcher.ini).</summary>
        void AtualizarPainel()
        {
            if (painel == null) return;
            foreach (object o in cmbRes.Items) if (((ItemRes)o).Indice == config.Resolucao) cmbRes.SelectedItem = o;
            if (cmbRes.SelectedIndex < 0 && cmbRes.Items.Count > 0) cmbRes.SelectedIndex = 0;
            chkTela.Ligado = config.TelaCheia;
            segIdioma.Selecionado = config.Idioma == "Eng" ? 1 : 0;
            volume.Valor = config.Volume; lblVolume.Text = config.Volume.ToString();
            chkFechar.Ligado = config.FecharAoJogar;
            lblPastaCfg.Text = root ?? "(ainda não escolhida)";
        }

        void SalvarConfig()
        {
            if (cmbRes.SelectedItem != null) config.Resolucao = ((ItemRes)cmbRes.SelectedItem).Indice;
            config.TelaCheia = chkTela.Ligado;
            config.Idioma = segIdioma.Selecionado == 1 ? "Eng" : "Por";
            config.Volume = volume.Valor;
            config.FecharAoJogar = chkFechar.Ligado;
            try
            {
                config.Gravar();
                status = "Configurações salvas: " + Resolucoes[config.Resolucao, 0] + "x" + Resolucoes[config.Resolucao, 1]
                       + (config.TelaCheia ? ", tela cheia" : ", janela") + ", " + (config.Idioma == "Eng" ? "English" : "Português") + ".";
                AbrirConfig(false);
            }
            catch (Exception ex) { status = "Não consegui salvar as configurações: " + ex.Message; }
            Invalidate();
        }

        void AbrirConfig(bool abrir)
        {
            if (abrir)
            {
                AtualizarPainel();
                painel.Left = W; painel.Visible = true; painel.BringToFront();
                painelDestino = W - 420 - 16;
            }
            else painelDestino = W;
            Invalidate();
        }

        // ---------------- foto para teste visual (sem rede) ----------------
        public static void Foto(string png, string pasta)
        {
            LauncherForm f = new LauncherForm(pasta ?? AppDomain.CurrentDomain.BaseDirectory);
            f.modoFoto = true;
            f.status = "Tudo atualizado. Bom jogo!";
            f.progresso = 100;
            f.servidor = 1;
            f.versaoCliente = "2026-09-28 19:51";
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-3000, 0);
            f.ShowInTaskbar = false;
            f.Show();
            Application.DoEvents();
            Salvar(f, png);
            f.AbrirConfig(true);
            f.painel.Left = f.painelDestino;
            Application.DoEvents();
            Salvar(f, Path.ChangeExtension(png, null) + "-config.png");
            f.AbrirConfig(false); f.painel.Visible = false;
            f.verificando = true; f.progresso = 62; f.status = "Baixando 12 de 40: Data/Local/Por/item_por.bmd  (410.2 MB de 661.0 MB)";
            f.Invalidate(); Application.DoEvents();
            Salvar(f, Path.ChangeExtension(png, null) + "-baixando.png");
            f.Close();
        }

        static void Salvar(Form f, string png)
        {
            using (Bitmap b = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                b.Save(png, ImageFormat.Png);
            }
        }
    }
}

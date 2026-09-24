using System.Data;

namespace MuChilaAdmin;

public sealed class MainForm : Form
{
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 110 };
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 5000 };

    // Servidor
    readonly DataGridView gridServers = Grid();
    readonly DataGridView gridOnline = Grid();
    readonly Label lblCounts = new() { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    readonly ComboBox cmbReload = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    // Eventos
    readonly ListBox lstEvents = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly NumericUpDown numMinutes = new() { Minimum = 1, Maximum = 1440, Value = 1, Width = 70 };
    readonly DataGridView gridPending = Grid();

    // VIP e contas
    readonly DataGridView gridAccounts = Grid();
    readonly ComboBox cmbLevel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    readonly NumericUpDown numDays = new() { Minimum = 1, Maximum = 3650, Value = 30, Width = 70 };

    public MainForm()
    {
        Text = "Mu Chila Admin";
        Width = 1000; Height = 720; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(ServerTab());
        tabs.TabPages.Add(EventsTab());
        tabs.TabPages.Add(AccountsTab());
        Controls.Add(tabs);
        Controls.Add(log);

        refresh.Tick += (_, _) => Safe(RefreshServer, quiet: true);
        Shown += (_, _) => { Safe(RefreshServer); Safe(RefreshEvents); Safe(RefreshAccounts); refresh.Start(); };
    }

    // ---------------- Servidor ----------------
    TabPage ServerTab()
    {
        var page = new TabPage("Servidor");
        var bar = Bar(
            Btn("Atualizar", () => RefreshServer()),
            Btn("Iniciar todos (launcher)", () => Log(ServerControl.LauncherToggle(start: true))),
            Btn("Parar todos (launcher)", () => { if (Confirm("Parar todos os servidores? Quem estiver jogando sera desconectado.")) Log(ServerControl.LauncherToggle(start: false)); }),
            Btn("Desconectar todos os jogadores", () => { if (Confirm("Desconectar todos os jogadores (os personagens sao salvos)?")) LogAll(ServerControl.DisconnectAll()); }),
            Btn("Abrir MuEditor", () => ServerControl.Open(ServerControl.MuEditorPath)),
            Btn("Abrir launcher", () => ServerControl.Open(ServerControl.LauncherPath)));

        cmbReload.Items.AddRange(ServerControl.ReloadIds.Keys.Cast<object>().ToArray());
        cmbReload.SelectedIndex = 0;
        var reloadBar = Bar(new Label { Text = "Recarregar sem reiniciar:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, cmbReload,
            Btn("Recarregar (GameServer + Castle Siege)", () => LogAll(ServerControl.Reload((string)cmbReload.SelectedItem!))), lblCounts);

        page.Controls.Add(Split(Titled("Processos", gridServers), Titled("Jogadores online", gridOnline), 0.5));
        page.Controls.Add(reloadBar);
        page.Controls.Add(bar);
        return page;
    }

    void RefreshServer()
    {
        var t = new DataTable();
        t.Columns.Add("Servidor"); t.Columns.Add("Status"); t.Columns.Add("Desde");
        foreach (var (display, process) in ServerControl.Servers)
        {
            var p = ServerControl.Find(process);
            t.Rows.Add(display, p == null ? "parado" : "rodando", p == null ? "" : p.StartTime.ToString("dd/MM HH:mm"));
        }
        gridServers.DataSource = t;
        gridOnline.DataSource = Accounts.Online();
        var (players, monsters) = ServerControl.GameServerCounts();
        lblCounts.Text = $"   Jogadores: {players}   Monstros: {monsters}";
    }

    // ---------------- Eventos ----------------
    TabPage EventsTab()
    {
        var page = new TabPage("Eventos");
        foreach (var ev in EventScheduler.Events) lstEvents.Items.Add(ev.Name);
        lstEvents.SelectedIndexChanged += (_, _) =>
        {
            if (lstEvents.SelectedIndex >= 0) numMinutes.Value = EventScheduler.Events[lstEvents.SelectedIndex].LeadMinutes;
        };
        lstEvents.SelectedIndex = 0;

        var bar = Bar(new Label { Text = "Começar daqui a", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, numMinutes,
            new Label { Text = "minuto(s)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
            Btn("Disparar evento", TriggerEvent),
            Btn("Limpar disparos já executados", () =>
            {
                int n = EventScheduler.CleanupPast(TimeSpan.FromMinutes(30));
                if (n > 0) LogAll(ServerControl.Reload("Event"));
                Log($"{n} disparo(s) antigo(s) removido(s).");
                RefreshEvents();
            }));

        var help = new Label
        {
            Dock = DockStyle.Top, Height = 52, Padding = new Padding(6),
            Text = "O servidor não tem comando para iniciar evento na hora: o programa agenda uma execução única no arquivo do evento " +
                   "e recarrega os GameServers. Eventos com sala (Blood Castle, Devil Square, Chaos Castle, Illusion Temple) avisam " +
                   "5 minutos antes para os jogadores entrarem, por isso o padrão é 6 minutos.",
        };
        page.Controls.Add(Split(Titled("Evento", lstEvents), Titled("Disparos agendados por este programa", gridPending), 0.35));
        page.Controls.Add(bar);
        page.Controls.Add(help);
        return page;
    }

    void TriggerEvent()
    {
        if (lstEvents.SelectedIndex < 0) return;
        var ev = EventScheduler.Events[lstEvents.SelectedIndex];
        // Segundo 0 do minuto alvo: o agendador do servidor compara minuto a minuto
        var when = DateTime.Now.AddMinutes((double)numMinutes.Value);
        when = new DateTime(when.Year, when.Month, when.Day, when.Hour, when.Minute, 0);
        Log(EventScheduler.Schedule(ev, when));
        LogAll(ServerControl.Reload("Event"));
        RefreshEvents();
    }

    void RefreshEvents()
    {
        var t = new DataTable();
        t.Columns.Add("Quando"); t.Columns.Add("Evento"); t.Columns.Add("Situação");
        foreach (var (when, name, _) in EventScheduler.Pending())
            t.Rows.Add(when.ToString("dd/MM HH:mm"), name, when > DateTime.Now ? "pendente" : "já executado");
        gridPending.DataSource = t;
    }

    // ---------------- VIP e contas ----------------
    TabPage AccountsTab()
    {
        var page = new TabPage("VIP e contas");
        cmbLevel.Items.AddRange(Accounts.Levels);
        cmbLevel.SelectedIndex = 1;
        var bar = Bar(
            Btn("Atualizar", () => RefreshAccounts()),
            new Label { Text = "Nível:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, cmbLevel,
            new Label { Text = "por", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, numDays,
            new Label { Text = "dia(s)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) },
            Btn("Aplicar VIP", () => ForSelectedAccount(a => { Accounts.SetVip(a, cmbLevel.SelectedIndex, (int)numDays.Value); return $"{a}: {cmbLevel.SelectedItem} por {numDays.Value} dia(s)"; })),
            Btn("Remover VIP", () => ForSelectedAccount(a => { Accounts.SetVip(a, 0, 0); return $"{a}: VIP removido"; })),
            Btn("Banir", () => ForSelectedAccount(a => Confirm($"Banir a conta {a}?") ? Do(() => Accounts.SetBanned(a, true), $"{a}: banida") : null)),
            Btn("Desbanir", () => ForSelectedAccount(a => { Accounts.SetBanned(a, false); return $"{a}: desbanida"; })));

        var note = new Label
        {
            Dock = DockStyle.Top, Height = 52, Padding = new Padding(6),
            Text = "VIP e ban valem no próximo login da conta. Benefícios de cada nível (experiência, drop, pontos...) ficam nas linhas *_AL1/_AL2/_AL3 " +
                   "do GameServerInfo - Common.dat. Personagens, inventário e baú: use o MuEditor (aba Servidor).",
        };
        page.Controls.Add(Titled("Contas", gridAccounts));
        page.Controls.Add(bar);
        page.Controls.Add(note);
        return page;
    }

    void RefreshAccounts()
    {
        gridAccounts.DataSource = Accounts.List();
        gridAccounts.Columns["Personagens"]!.FillWeight = 350;
    }

    void ForSelectedAccount(Func<string, string?> action)
    {
        if (gridAccounts.CurrentRow?.Cells["Conta"].Value is not string account) { Log("Selecione uma conta na lista."); return; }
        var msg = action(account);
        if (msg != null) Log(msg);
        RefreshAccounts();
    }

    // ---------------- utilidades ----------------
    static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };

    static FlowLayoutPanel Bar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4), WrapContents = true };
        bar.Controls.AddRange(controls);
        return bar;
    }

    Button Btn(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += (_, _) => Safe(action);
        return b;
    }

    /// <summary>Divisao lado a lado; a proporcao e reaplicada ao redimensionar (SplitterDistance fixo nao escala com a janela).</summary>
    static SplitContainer Split(Control left, Control right, double ratio)
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(right);
        split.SizeChanged += (_, _) => { if (split.Width > 100) split.SplitterDistance = (int)(split.Width * ratio); };
        return split;
    }

    static GroupBox Titled(string title, Control content)
    {
        var box = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(6) };
        box.Controls.Add(content);
        return box;
    }

    static string Do(Action a, string msg) { a(); return msg; }

    static bool Confirm(string text) =>
        MessageBox.Show(text, "Mu Chila Admin", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    void Log(string text) => log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    void LogAll(IEnumerable<string> lines) { foreach (var l in lines) Log(l); }

    void Safe(Action action, bool quiet = false)
    {
        try { action(); }
        catch (Exception ex) { if (!quiet) Log("ERRO: " + ex.Message); }
    }
}

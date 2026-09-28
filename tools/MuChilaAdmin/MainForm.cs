using System.Data;
using System.IO;

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

    // Bônus
    readonly CheckBox chkExp = new() { Text = "EXP", Checked = true, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
    readonly CheckBox chkMaster = new() { Text = "EXP master", Checked = true, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
    readonly CheckBox chkDrop = new() { Text = "Drop", AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
    readonly NumericUpDown numMult = new() { Minimum = 1.1m, Maximum = 10, Increment = 0.5m, DecimalPlaces = 1, Value = 2, Width = 60 };
    readonly NumericUpDown numBonusMinutes = new() { Minimum = 1, Maximum = 10080, Value = 60, Width = 70 };
    readonly NumericUpDown numBonusStart = new() { Minimum = 1, Maximum = 10080, Value = 1, Width = 70 };
    readonly DataGridView gridBonus = Grid();

    // Lojas
    readonly ListBox lstShops = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly DataGridView gridShop = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };
    readonly Label lblShop = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    List<ShopInfo> shops = new();
    DataTable shopTable = new();
    int shopShown = -1;
    bool shopDirty;

    // Loja de Cash
    readonly DataGridView gridCash = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.CellSelect,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };
    readonly TextBox txtCashFind = new() { Width = 220, PlaceholderText = "filtrar por nome..." };
    readonly Label lblCash = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    List<CashShop.Package> cashAll = new();
    bool cashDirty;

    // Comandos (consulta)
    readonly DataGridView gridCmds = Grid();
    readonly TextBox txtCmdFind = new() { Width = 240, PlaceholderText = "filtrar comando ou descrição..." };

    // Monstros (respawn + densidade)
    readonly DataGridView gridMaps = Grid();
    readonly DataGridView gridMon = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };
    readonly Panel pnlDensidade = new() { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
    readonly Label lblMon = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    List<MapDensity> mapas = new();
    DataTable monTable = new();
    string? monFile;
    bool monDirty;

    // Drops (ItemDrop.txt + taxas do Monster.txt)
    readonly DataGridView gridDrops = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.CellSelect,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };
    readonly DataGridView gridRates = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.CellSelect,
        RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
    };
    readonly TextBox txtDropFind = new() { Width = 220, PlaceholderText = "filtrar item, monstro, mapa..." };
    readonly TextBox txtRateFind = new() { Width = 220, PlaceholderText = "filtrar monstro..." };
    readonly Label lblDrops = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    readonly Label lblRates = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    DataTable dropTable = new(), rateTable = new();
    readonly Dictionary<DataRow, DropRule> dropRows = new();
    Dictionary<int, string> monsterNames = new(), mapNames = new();
    bool dropDirty, rateDirty, dropLoading;
    System.Windows.Forms.Timer? monsterReloadTimer;

    // Resets (valores do site)
    readonly NumericUpDown numMasterCred = new() { Minimum = 0, Maximum = 1000000, Width = 100 };
    readonly NumericUpDown numSupremeCred = new() { Minimum = 0, Maximum = 1000000, Width = 100 };
    readonly NumericUpDown numMaxStat = new() { Minimum = 0, Maximum = 65535, Width = 100 };
    readonly CheckBox chkResetsAtivo = new() { Text = "Resets pelo site ativos", AutoSize = true };

    // Itens e baú
    readonly ComboBox cmbItemAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    readonly ComboBox cmbItemPlace = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    readonly DataGridView gridItems = Grid();
    readonly DataGridView gridGifts = Grid();
    List<ItemView> itemsShown = new();
    const string VaultOption = "(baú da conta)";

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
        tabs.TabPages.Add(BonusTab());
        tabs.TabPages.Add(ShopsTab());
        tabs.TabPages.Add(CashShopTab());
        tabs.TabPages.Add(ResetsTab());
        tabs.TabPages.Add(MonstersTab());
        tabs.TabPages.Add(DropsTab());
        tabs.TabPages.Add(CommandsTab());
        tabs.TabPages.Add(ItemsTab());
        tabs.TabPages.Add(AccountsTab());
        Controls.Add(tabs);
        Controls.Add(log);

        refresh.Tick += (_, _) => Safe(RefreshServer, quiet: true);
        Shown += (_, _) => { Safe(StartResetWatcher); Safe(RefreshServer); Safe(RefreshEvents); Safe(RefreshBonus); Safe(RefreshAccounts); refresh.Start(); };
    }

    /// <summary>O vigia roda em processo próprio (continua depois de fechar o painel); só liga se não estiver rodando.</summary>
    void StartResetWatcher()
    {
        if (ResetWatcher.IsRunning()) return;
        System.Diagnostics.Process.Start(Application.ExecutablePath, "--vigia-reset");
        Log("Vigia ligado: corrige a checagem de ataques do GameServer (issue #12) sempre que ele liga; registro em vigia.log.");
    }

    // ---------------- Servidor ----------------
    TabPage ServerTab()
    {
        var page = new TabPage("Servidor");
        var bar = Bar(
            Btn("Atualizar", () => RefreshServer()),
            Btn("Iniciar todos (launcher)", () => Log(ServerControl.LauncherToggle(start: true))),
            Btn("Parar todos (launcher)", () => { if (Confirm("Parar todos os servidores? Quem estiver jogando sera desconectado.")) Log(ServerControl.LauncherToggle(start: false)); }),
            Btn("Desconectar jogador selecionado", () =>
            {
                if (gridOnline.CurrentRow?.Cells["Conta"].Value is not string a) { Log("Selecione um jogador na lista \"Jogadores online\"."); return; }
                if (Confirm($"Forçar o logout de {a}? O jogador é desconectado e o personagem é salvo.{SameIpWarning(a)}")) RunForceLogout(a);
            }),
            Btn("Levar à seleção de personagem", () =>
            {
                if (gridOnline.CurrentRow?.Cells["Personagem"].Value is not string c) { Log("Selecione um jogador na lista \"Jogadores online\"."); return; }
                if (Confirm($"Levar {c} para a seleção de personagem? É o mesmo que o jogador escolher \"Trocar personagem\", mas na hora: o personagem é salvo e o jogo volta para a seleção, sem desconectar."))
                    Log(ResetWatcher.SendToCharacterSelect(c));
            }),
            Btn("Desconectar todos os jogadores", () => { if (Confirm("Desconectar todos os jogadores (os personagens sao salvos)?")) LogAll(ServerControl.DisconnectAll()); }),
            Btn("Publicar atualização do cliente", PublishClientUpdate),
            Btn("Abrir MuEditor", () => ServerControl.Open(ServerControl.MuEditorPath)),
            Btn("Abrir launcher", () => ServerControl.Open(ServerControl.LauncherPath)));

        cmbReload.Items.AddRange(ServerControl.ReloadIds.Keys.Cast<object>().ToArray());
        cmbReload.SelectedIndex = 0;
        var reloadBar = Bar(new Label { Text = "Recarregar sem reiniciar:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, cmbReload,
            Btn("Recarregar (GameServers + Castle Siege)", () =>
            {
                if ((string)cmbReload.SelectedItem! == "Monster") ReloadMonstersSafe();   // só GameServers e fora da invasão
                else LogAll(ServerControl.Reload((string)cmbReload.SelectedItem!));
            }), lblCounts);

        page.Controls.Add(Split(Titled("Processos", gridServers), Titled("Jogadores online", gridOnline), 0.5));
        page.Controls.Add(reloadBar);
        page.Controls.Add(bar);
        return page;
    }

    void RefreshServer()
    {
        var t = new DataTable();
        t.Columns.Add("Servidor"); t.Columns.Add("Status"); t.Columns.Add("Desde");
        foreach (var (display, process, folder) in ServerControl.Servers)
        {
            var p = ServerControl.Find(process, folder);
            t.Rows.Add(display, p == null ? "parado" : "rodando", p == null ? "" : p.StartTime.ToString("dd/MM HH:mm"));
        }
        gridServers.DataSource = t;
        gridOnline.DataSource = Accounts.Online();
        lblCounts.Text = $"   {ServerControl.GameServerCounts()}   Vigia: {(ResetWatcher.IsRunning() ? "ligado" : "DESLIGADO")}";
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

    // ---------------- Bônus ----------------
    TabPage BonusTab()
    {
        var page = new TabPage("Bônus");
        Label L(string s) => new() { Text = s, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        var bar = Bar(chkExp, chkMaster, chkDrop, L("  x"), numMult, L("  por"), numBonusMinutes, L("minuto(s),  começando daqui a"), numBonusStart, L("minuto(s)"),
            Btn("Agendar bônus", ScheduleBonus));
        var bar2 = Bar(
            Btn("Cancelar bônus selecionado", () =>
            {
                if (gridBonus.CurrentRow?.Cells["Vaga"].Value is not int slot) { Log("Selecione um bônus na lista."); return; }
                Log(BonusScheduler.Cancel(slot));
                LogAll(ServerControl.Reload("Event"));
                RefreshBonus();
            }),
            Btn("Limpar bônus terminados", () =>
            {
                int n = BonusScheduler.CleanupFinished();
                if (n > 0) LogAll(ServerControl.Reload("Event"));
                Log($"{n} bônus terminado(s) removido(s).");
                RefreshBonus();
            }),
            Btn("Atualizar", RefreshBonus));
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 70, Padding = new Padding(6),
            Text = "Exemplo: EXP e EXP master x2 por 60 minutos = experiência em dobro por uma hora para todos. O bônus multiplica a taxa de cada plano " +
                   "(Free, Vipzinho, Vip e Vipzão continuam na mesma proporção), vale nos dois GameServers e no Castle Siege, e o jogo avisa todos " +
                   "os jogadores no início e no fim. Até 7 bônus ao mesmo tempo; \"Limpar bônus terminados\" libera as vagas.",
        };
        page.Controls.Add(Titled("Bônus agendados por este programa", gridBonus));
        page.Controls.Add(bar2);
        page.Controls.Add(bar);
        page.Controls.Add(help);
        return page;
    }

    void ScheduleBonus()
    {
        var types = new List<int>();
        if (chkExp.Checked) types.Add(0);
        if (chkMaster.Checked) types.Add(1);
        if (chkDrop.Checked) types.Add(2);
        // o servidor confere a agenda minuto a minuto: começa no minuto cheio, com pelo menos 30 s de folga para a recarga
        var start = DateTime.Now.AddMinutes((double)numBonusStart.Value);
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0);
        if (start < DateTime.Now.AddSeconds(30)) start = start.AddMinutes(1);
        Log(BonusScheduler.Schedule(types, numMult.Value, (int)numBonusMinutes.Value, start));
        LogAll(ServerControl.Reload("Common (inclui mensagens)"));   // mensagens de início e fim
        LogAll(ServerControl.Reload("Event"));                       // agenda do bônus
        RefreshBonus();
    }

    void RefreshBonus()
    {
        var t = new DataTable();
        t.Columns.Add("Vaga", typeof(int)); t.Columns.Add("Início"); t.Columns.Add("Fim"); t.Columns.Add("Bônus"); t.Columns.Add("Situação");
        foreach (var b in BonusScheduler.List())
            t.Rows.Add(b.Slot, b.Start.ToString("dd/MM HH:mm"), b.End.ToString("dd/MM HH:mm"), b.Description, b.State);
        gridBonus.DataSource = t;
        if (gridBonus.Columns["Bônus"] is { } c) c.FillWeight = 300;
    }

    // ---------------- Lojas ----------------
    static readonly string[] ShopCols = { "Seção", "Tipo", "Nível", "Dur", "Skill", "Sorte", "Opção", "Excelente" };

    TabPage ShopsTab()
    {
        var page = new TabPage("Lojas");
        shops = Shops.List();
        foreach (var s in shops) lstShops.Items.Add($"{s.Index:000} - {s.Name} (NPC {s.MonsterClass})");
        lstShops.SelectedIndexChanged += (_, _) => Safe(ShowShop);
        gridShop.CellValueChanged += (_, _) => { if (shopShown >= 0) { shopDirty = true; UpdateShopStatus(); } };
        gridShop.DataError += (_, e) => { Log("Valor inválido: use só números."); e.Cancel = true; };

        var bar = Bar(
            Btn("Adicionar item...", AddShopItem),
            Btn("Remover", () => { if (gridShop.CurrentRow is { } r) { shopTable.Rows.RemoveAt(r.Index); shopDirty = true; UpdateShopStatus(); } }),
            Btn("Subir", () => MoveShopRow(-1)),
            Btn("Descer", () => MoveShopRow(+1)),
            Btn("Salvar e aplicar", SaveShop),
            Btn("Descartar alterações", () => { shopDirty = false; ShowShop(force: true); }),
            lblShop);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 54, Padding = new Padding(6),
            Text = "Nível 0-15; Dur = durabilidade (em poções, a quantidade do pacote); Skill e Sorte 0 ou 1; Opção 0-7 (+4 a +28); Excelente 0-63. " +
                   "A janela da loja tem 8×15 espaços e os itens entram na ordem da lista: o que não couber não aparece no jogo. " +
                   "\"Salvar e aplicar\" faz backup do arquivo e recarrega as lojas nos GameServers, sem reiniciar.",
        };
        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(gridShop);
        right.Controls.Add(bar);
        page.Controls.Add(Split(Titled("NPC", lstShops), Titled("Itens à venda", right), 0.28));
        page.Controls.Add(help);
        // só depois de a janela existir (antes disso a grade não gera as colunas)
        Shown += (_, _) => { if (lstShops.Items.Count > 0 && lstShops.SelectedIndex < 0) lstShops.SelectedIndex = 0; };
        return page;
    }

    void ShowShop() => ShowShop(force: false);

    void ShowShop(bool force)
    {
        int sel = lstShops.SelectedIndex;
        if (sel < 0 || (sel == shopShown && !force)) return;
        if (shopDirty && sel != shopShown)
        {
            if (!Confirm("A loja atual tem alterações não salvas. Descartar?")) { lstShops.SelectedIndex = shopShown; return; }
            shopDirty = false;
        }
        var t = new DataTable();
        t.Columns.Add("Item");
        foreach (var c in ShopCols) t.Columns.Add(c, typeof(int));
        foreach (var it in Shops.Load(shops[sel]))
            t.Rows.Add(Shops.Name(it.Section, it.Type), it.Section, it.Type, it.Level, it.Dur, it.Skill, it.Luck, it.Option, it.Excellent);
        shopShown = -1;   // evita marcar como alterado durante a carga
        gridShop.DataSource = shopTable = t;
        gridShop.Columns["Item"]!.ReadOnly = true; gridShop.Columns["Item"]!.FillWeight = 260;
        gridShop.Columns["Seção"]!.ReadOnly = true; gridShop.Columns["Tipo"]!.ReadOnly = true;
        shopShown = sel; shopDirty = false;
        UpdateShopStatus();
    }

    List<ShopItem> ShopItemsFromGrid() => shopTable.Rows.Cast<DataRow>().Select(r => new ShopItem
    {
        Section = (int)r["Seção"], Type = (int)r["Tipo"], Level = (int)r["Nível"], Dur = (int)r["Dur"],
        Skill = (int)r["Skill"], Luck = (int)r["Sorte"], Option = (int)r["Opção"], Excellent = (int)r["Excelente"],
    }).ToList();

    void UpdateShopStatus()
    {
        var (fora, used) = Shops.Fit(ShopItemsFromGrid());
        lblShop.ForeColor = fora.Count > 0 ? Color.Firebrick : SystemColors.ControlText;
        lblShop.Text = $"   {shopTable.Rows.Count} itens, {used} de {Shops.GridWidth * Shops.GridHeight} espaços" +
                       (fora.Count > 0 ? $" — NÃO CABEM: {string.Join(", ", fora.Select(i => Shops.Name(i.Section, i.Type)))}" : "") +
                       (shopDirty ? "   (não salvo)" : "");
    }

    void MoveShopRow(int delta)
    {
        if (gridShop.CurrentRow is not { } cur) return;
        int i = cur.Index, j = i + delta;
        if (j < 0 || j >= shopTable.Rows.Count) return;
        var values = shopTable.Rows[i].ItemArray;
        shopTable.Rows.RemoveAt(i);
        var row = shopTable.NewRow(); row.ItemArray = values;
        shopTable.Rows.InsertAt(row, j);
        gridShop.CurrentCell = gridShop.Rows[j].Cells[0];
        shopDirty = true; UpdateShopStatus();
    }

    void AddShopItem()
    {
        if (shopShown < 0) return;
        using var dlg = new Form { Text = "Adicionar item à loja", Width = 520, Height = 480, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var search = new TextBox { Left = 12, Top = 12, Width = 480, PlaceholderText = "Digite parte do nome (ex.: Jewel, Healing, Dragon)..." };
        var list = new ListBox { Left = 12, Top = 42, Width = 480, Height = 300 };
        var all = Shops.Catalog().OrderBy(d => d.Section).ThenBy(d => d.Type).ToList();
        void Filter()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var d in all.Where(d => search.Text.Length == 0 || d.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).Take(400))
                list.Items.Add(d);
            list.EndUpdate();
        }
        list.Format += (_, e) => { if (e.ListItem is ItemDef d) e.Value = $"{d.Name}   [{d.Section},{d.Type}]  {d.Width}×{d.Height}"; };
        search.TextChanged += (_, _) => Filter();
        NumericUpDown N(int left, int max, int value) => new() { Left = left, Top = 355, Width = 55, Minimum = 0, Maximum = max, Value = value };
        Label L(int left, string text) => new() { Left = left, Top = 358, AutoSize = true, Text = text };
        var level = N(55, 15, 0); var dur = N(145, 255, 0); var opt = N(235, 7, 0);
        var skill = new CheckBox { Left = 305, Top = 356, Text = "Skill", AutoSize = true };
        var luck = new CheckBox { Left = 365, Top = 356, Text = "Sorte", AutoSize = true };
        var ok = new Button { Text = "Adicionar", DialogResult = DialogResult.OK, Left = 336, Top = 395, Width = 75 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 417, Top = 395, Width = 75 };
        dlg.Controls.AddRange(new Control[] { search, list, L(12, "Nível"), level, L(110, "Dur"), dur, L(195, "Opção"), opt, skill, luck, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        Filter();
        if (dlg.ShowDialog(this) != DialogResult.OK || list.SelectedItem is not ItemDef item) return;
        shopTable.Rows.Add(item.Name, item.Section, item.Type, (int)level.Value, (int)dur.Value, skill.Checked ? 1 : 0, luck.Checked ? 1 : 0, (int)opt.Value, 0);
        shopDirty = true; UpdateShopStatus();
        gridShop.CurrentCell = gridShop.Rows[^1].Cells[0];
    }

    void SaveShop()
    {
        if (shopShown < 0) return;
        gridShop.EndEdit();
        var items = ShopItemsFromGrid();
        var (fora, _) = Shops.Fit(items);
        if (fora.Count > 0 && !Confirm($"{fora.Count} item(ns) não cabem na janela da loja e não vão aparecer no jogo:\n{string.Join("\n", fora.Select(i => Shops.Name(i.Section, i.Type)))}\n\nSalvar assim mesmo?")) return;
        Shops.Save(shops[shopShown], items);
        Log($"Loja {shops[shopShown].Name} salva ({items.Count} itens; backup .bak-* ao lado do arquivo).");
        LogAll(ServerControl.Reload("Shop"));
        shopDirty = false; UpdateShopStatus();
    }

    // ---------------- Loja de Cash ----------------
    TabPage CashShopTab()
    {
        var page = new TabPage("Loja de Cash");
        gridCash.CellValueChanged += (_, e) => { if (e.RowIndex >= 0 && cashAll.Count > 0) { cashDirty = true; UpdateCashStatus(); } };
        gridCash.CurrentCellDirtyStateChanged += (_, _) => { if (gridCash.IsCurrentCellDirty && gridCash.CurrentCell is DataGridViewCheckBoxCell) gridCash.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        gridCash.DataError += (_, e) => { Log("Preço inválido: use só números."); e.Cancel = true; };
        txtCashFind.TextChanged += (_, _) => Safe(() => FillCash(txtCashFind.Text));

        var bar = Bar(
            new Label { Text = "Loja de Cash (tecla X no jogo):", AutoSize = true, Padding = new Padding(0, 6, 6, 0) }, txtCashFind,
            Btn("Salvar e aplicar", SaveCash),
            Btn("Recarregar da pasta", () => { cashDirty = false; LoadCash(); }),
            Btn("Publicar p/ launcher", GenerateCashPatch),
            lblCash);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(6),
            Text = "O preço é em W Coin/Goblin (a moeda de cada pacote). Quem cobra é o servidor; o cliente só mostra. " +
                   "\"Na loja\" desmarcado esconde o pacote (dá para voltar depois). \"Salvar e aplicar\" faz backup, grava servidor + cliente e " +
                   "recarrega a loja nos GameServers. Depois use \"Publicar p/ launcher\" para os amigos receberem os nomes/preços novos.",
        };
        page.Controls.Add(gridCash);
        page.Controls.Add(help);
        page.Controls.Add(bar);
        Shown += (_, _) => Safe(LoadCash);
        return page;
    }

    void LoadCash()
    {
        cashAll = CashShop.List();
        FillCash(txtCashFind.Text);
        cashDirty = false;
        UpdateCashStatus();
    }

    void FillCash(string filter)
    {
        var t = new DataTable();
        t.Columns.Add("Pacote"); t.Columns.Add("Moeda"); t.Columns.Add("Preço", typeof(int)); t.Columns.Add("Na loja", typeof(bool));
        t.Columns.Add("cat", typeof(int)); t.Columns.Add("main", typeof(int));
        foreach (var p in cashAll)
        {
            if (filter.Length > 0 && !(p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))) continue;
            t.Rows.Add(p.HasClient ? p.Name : $"(pacote {p.Category},{p.Main} — sem tela no cliente)", p.CoinLabel, p.Price, !p.Hidden, p.Category, p.Main);
        }
        gridCash.DataSource = t;
        foreach (var c in new[] { "Pacote", "Moeda", "cat", "main" }) gridCash.Columns[c]!.ReadOnly = true;
        gridCash.Columns["cat"]!.Visible = gridCash.Columns["main"]!.Visible = false;
        gridCash.Columns["Pacote"]!.FillWeight = 260; gridCash.Columns["Moeda"]!.FillWeight = 90;
        gridCash.Columns["Preço"]!.FillWeight = 70; gridCash.Columns["Na loja"]!.FillWeight = 60;
    }

    void UpdateCashStatus()
    {
        int na = cashAll.Count(p => !p.Hidden);
        lblCash.Text = $"   {cashAll.Count} pacotes ({na} na loja)" +
                       (CashShop.ClientAvailable ? "" : "   — cliente não encontrado: só o preço do servidor será gravado") +
                       (cashDirty ? "   (não salvo)" : "");
    }

    /// <summary>Passa os preços/estado da grade de volta para a lista cashAll (casando por categoria+main das colunas ocultas).</summary>
    void ApplyCashGrid()
    {
        if (gridCash.DataSource is not DataTable t) return;
        var byKey = cashAll.ToDictionary(p => (p.Category, p.Main));
        foreach (DataRow r in t.Rows)
        {
            if (r["cat"] is not int cat || r["main"] is not int main) continue;
            if (!byKey.TryGetValue((cat, main), out var p)) continue;
            p.Price = r["Preço"] is int pr ? pr : p.Price;
            p.Hidden = r["Na loja"] is bool b && !b;
        }
    }

    void SaveCash()
    {
        gridCash.EndEdit();
        ApplyCashGrid();
        var somenteServidor = !CashShop.ClientAvailable;
        if (somenteServidor && !Confirm($"O cliente não foi encontrado em:\n{CashShop.ClientDir}\n\nVou gravar só no servidor (o jogo cobra o preço novo, mas a tela do cliente mostra o antigo). Continuar?")) return;
        Log(CashShop.Save(cashAll));
        LogAll(ServerControl.Reload("CashShop"));
        if (!somenteServidor) Log("Cliente atualizado. Use \"Publicar p/ launcher\" para enviar os arquivos novos aos amigos.");
        cashDirty = false; UpdateCashStatus();
    }

    void GenerateCashPatch() => PublishClientUpdate();

    /// <summary>
    /// Publica a pasta do cliente do repositório para o Launcher (tools\Publicar-Launcher.ps1): espelha os arquivos em
    /// Cliente para amigos\launcher e regrava o manifesto. Quem abre o jogo pelo MuChilaLauncher.exe recebe as mudanças.
    /// </summary>
    void PublishClientUpdate()
    {
        const string script = @"C:\Projetos\MuServer-Season14\tools\Publicar-Launcher.ps1";
        if (!File.Exists(script)) { Log($"Script não encontrado: {script}"); return; }
        if (!Confirm("Publicar a atualização do cliente para o Launcher?\n\nCopia as mudanças da pasta do cliente do repositório (loja de cash, item.bmd, serverlist...) e gera a lista nova. Quem abrir o jogo pelo MuChilaLauncher.exe recebe na hora.")) return;
        Log("Publicando a atualização do cliente em segundo plano (Publicar-Launcher.ps1)...");
        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -NoExit -File \"{script}\"") { UseShellExecute = true };
        System.Diagnostics.Process.Start(psi);
    }

    // ---------------- Resets ----------------
    TabPage ResetsTab()
    {
        var page = new TabPage("Resets");
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 96, Padding = new Padding(8), AutoSize = false,
            Text = "Valores dos resets feitos pelo site (área do jogador):\r\n"
                 + "• Master Reset (Master Level 600) e Supreme Reset (nível 400 + Master 600 + atributos no máximo) dão créditos do site.\r\n"
                 + "• O Reset normal continua sendo /reset no jogo.\r\n"
                 + "• \"Atributo máximo\" é o valor que cada atributo precisa ter para liberar o Supreme (o servidor usa 65000).\r\n"
                 + "Salvar aqui vale na hora para o site; o cron do master reset usa o novo valor no próximo minuto.",
        };
        Label L(string t) => new() { Text = t, AutoSize = true, Padding = new Padding(0, 6, 6, 0) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        grid.Controls.Add(L("Créditos por Master Reset:"), 0, 0); grid.Controls.Add(numMasterCred, 1, 0);
        grid.Controls.Add(L("Créditos por Supreme Reset:"), 0, 1); grid.Controls.Add(numSupremeCred, 1, 1);
        grid.Controls.Add(L("Atributo máximo (libera o Supreme):"), 0, 2); grid.Controls.Add(numMaxStat, 1, 2);
        grid.Controls.Add(new Label { Width = 1 }, 0, 3); grid.Controls.Add(chkResetsAtivo, 1, 3);

        var bar = Bar(Btn("Salvar", SaveResetConfig), Btn("Recarregar", LoadResetConfig));
        page.Controls.Add(grid);
        page.Controls.Add(bar);
        page.Controls.Add(help);
        Shown += (_, _) => Safe(LoadResetConfig);
        return page;
    }

    void LoadResetConfig()
    {
        var v = ResetConfig.Carregar();
        numMasterCred.Value = Math.Min(numMasterCred.Maximum, v.MasterCreditos);
        numSupremeCred.Value = Math.Min(numSupremeCred.Maximum, v.SupremeCreditos);
        numMaxStat.Value = Math.Min(numMaxStat.Maximum, v.MaxStat);
        chkResetsAtivo.Checked = v.Ativo;
    }

    void SaveResetConfig()
    {
        Log(ResetConfig.Salvar(new ResetConfig.Valores
        {
            Ativo = chkResetsAtivo.Checked,
            MasterCreditos = (int)numMasterCred.Value,
            SupremeCreditos = (int)numSupremeCred.Value,
            MaxStat = (int)numMaxStat.Value,
        }));
    }

    // ---------------- Drops (ItemDrop.txt + drop comum/zen do Monster.txt) ----------------
    static readonly string[] DropOptCols = { "Op0", "Op1", "Op2", "Op3", "Op4", "Op5", "Op6" };
    record Choice(string Value, string Text);

    TabPage DropsTab()
    {
        var page = new TabPage("Drops");
        var inner = new TabControl { Dock = DockStyle.Fill };

        // 1) regras de item (ItemDrop.txt)
        var p1 = new TabPage("Itens que os monstros dropam");
        gridDrops.CellValueChanged += (_, e) => Safe(() => DropCellChanged(e));
        gridDrops.DataError += (_, e) => { Log("Valor inválido."); e.Cancel = true; };
        txtDropFind.TextChanged += (_, _) => Safe(FilterDrops);
        var bar1 = Bar(txtDropFind,
            Btn("Adicionar regra...", AddDropRule),
            Btn("Escolher monstro...", () => PickForDrop(monstro: true)),
            Btn("Escolher mapa...", () => PickForDrop(monstro: false)),
            Btn("Remover", RemoveDropRule),
            Btn("Salvar e aplicar", SaveDrops),
            Btn("Descartar", () => { if (!dropDirty || Confirm("Descartar as alterações nos drops?")) LoadDrops(); }),
            lblDrops);
        var help1 = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(6),
            Text = "Cada linha: o item cai dos monstros que se encaixam em Mapa, Monstro e Nível mín/máx do monstro (\"*\" = qualquer), com a Chance em % " +
                   "(1% = 1 em cada 100 monstros desses). Nível do item 0-15; Grade e Op0-Op6 como no cabeçalho do ItemDrop.txt (\"*\" = sorteado). " +
                   "\"Salvar e aplicar\" faz backup do arquivo e recarrega os itens nos servidores, sem reiniciar.",
        };
        p1.Controls.Add(gridDrops); p1.Controls.Add(help1); p1.Controls.Add(bar1);

        // 2) drop comum e zen por monstro (Monster.txt)
        var p2 = new TabPage("Drop comum e zen por monstro");
        gridRates.CellValueChanged += (_, e) => { if (!dropLoading && e.RowIndex >= 0) { rateDirty = true; UpdateRateStatus(); } };
        gridRates.DataError += (_, e) => { Log("Valor inválido: use só números."); e.Cancel = true; };
        txtRateFind.TextChanged += (_, _) => Safe(() => rateTable.DefaultView.RowFilter = txtRateFind.Text.Length == 0 ? "" : $"Monstro LIKE '%{LikeEsc(txtRateFind.Text)}%'");
        var bar2 = Bar(txtRateFind,
            Btn("Salvar e aplicar", SaveRates),
            Btn("Descartar", () => { if (!rateDirty || Confirm("Descartar as alterações nas taxas?")) LoadRates(); }),
            lblRates);
        var help2 = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(6),
            Text = "Drop comum = chance de o monstro soltar um item comum sorteado, até o \"Nível máx. do item\"; Zen = chance de soltar zen " +
                   "(o valor do zen segue a taxa de zen de cada plano). Números do kit: maior = mais vezes. \"Salvar e aplicar\" faz backup do Monster.txt " +
                   "e recarrega os monstros nos GameServers assim que não houver invasão no ar (a dourada fica 9 de cada 10 minutos).",
        };
        p2.Controls.Add(gridRates); p2.Controls.Add(help2); p2.Controls.Add(bar2);

        inner.TabPages.Add(p1); inner.TabPages.Add(p2);
        page.Controls.Add(inner);
        Shown += (_, _) => { Safe(LoadDrops); Safe(LoadRates); };
        FormClosing += (_, e) =>
        {
            if (monsterReloadTimer != null && !Confirm("Há uma recarga de monstros esperando a invasão acabar. Se fechar agora, ela não acontece. Fechar mesmo assim?")) e.Cancel = true;
            else if ((dropDirty || rateDirty) && !Confirm("Há alterações nos drops não salvas. Fechar mesmo assim?")) e.Cancel = true;
        };
        return page;
    }

    static string LikeEsc(string s) => string.Concat(s.Select(ch => ch is '*' or '%' or '[' or ']' ? $"[{ch}]" : ch == '\'' ? "''" : ch.ToString()));
    string MapName(string v) => v == "*" ? "(todos)" : int.TryParse(v, out var n) && mapNames.TryGetValue(n, out var s) ? s : "(?)";
    string MonName(string v) => v == "*" ? "(todos)" : int.TryParse(v, out var n) && monsterNames.TryGetValue(n, out var s) ? s : "(não existe)";

    void LoadDrops()
    {
        monsterNames = Drops.MonsterNames();
        mapNames = Monsters.Maps().GroupBy(m => m.Code).ToDictionary(g => g.Key, g => g.First().Name);
        var t = new DataTable();
        foreach (var c in new[] { "Item", "Código", "Nível", "Grade" }.Concat(DropOptCols)
                     .Concat(new[] { "Duração", "Mapa", "Nome do mapa", "Monstro", "Nome do monstro", "Nível mín", "Nível máx", "Chance %", "Comentário" }))
            t.Columns.Add(c);
        dropRows.Clear();
        foreach (var r in Drops.Load())
        {
            var row = t.NewRow();
            row["Item"] = Shops.Name(r.Section, r.Type); row["Código"] = $"{r.Section},{r.Type}";
            row["Nível"] = r.Level; row["Grade"] = r.Grade;
            for (int i = 0; i < DropOptCols.Length; i++) row[DropOptCols[i]] = r.Options[i];
            row["Duração"] = r.Duration; row["Mapa"] = r.Map; row["Nome do mapa"] = MapName(r.Map);
            row["Monstro"] = r.Monster; row["Nome do monstro"] = MonName(r.Monster);
            row["Nível mín"] = r.LevelMin; row["Nível máx"] = r.LevelMax;
            row["Chance %"] = Drops.Percent(r.Rate).Replace('.', ','); row["Comentário"] = r.Comment;
            t.Rows.Add(row); dropRows[row] = r;
        }
        dropLoading = true;
        gridDrops.DataSource = dropTable = t;
        foreach (var c in new[] { "Item", "Código", "Nome do mapa", "Nome do monstro" }) gridDrops.Columns[c]!.ReadOnly = true;
        var pesos = new Dictionary<string, float> { ["Item"] = 170, ["Código"] = 45, ["Nome do mapa"] = 90, ["Nome do monstro"] = 110, ["Chance %"] = 50, ["Comentário"] = 150, ["Duração"] = 45, ["Monstro"] = 45 };
        foreach (DataGridViewColumn c in gridDrops.Columns) c.FillWeight = pesos.TryGetValue(c.Name, out var w) ? w : 35;
        dropLoading = false; dropDirty = false;
        FilterDrops(); UpdateDropStatus();
    }

    void FilterDrops()
    {
        var f = LikeEsc(txtDropFind.Text.Trim());
        dropTable.DefaultView.RowFilter = f.Length == 0 ? "" :
            $"[Item] LIKE '%{f}%' OR [Nome do mapa] LIKE '%{f}%' OR [Nome do monstro] LIKE '%{f}%' OR [Comentário] LIKE '%{f}%' OR [Código] LIKE '%{f}%'";
    }

    void UpdateDropStatus() => lblDrops.Text = $"   {dropTable.Rows.Count} regras" + (dropDirty ? "   (não salvo)" : "");
    void UpdateRateStatus() => lblRates.Text = $"   {rateTable.Rows.Count} monstros" + (rateDirty ? "   (não salvo)" : "");

    void DropCellChanged(DataGridViewCellEventArgs e)
    {
        if (dropLoading || e.RowIndex < 0 || gridDrops.Rows[e.RowIndex].DataBoundItem is not DataRowView drv) return;
        var col = gridDrops.Columns[e.ColumnIndex].Name;
        dropLoading = true;
        try
        {
            if (col == "Mapa") drv.Row["Nome do mapa"] = MapName((drv.Row["Mapa"] as string ?? "").Trim());
            if (col == "Monstro") drv.Row["Nome do monstro"] = MonName((drv.Row["Monstro"] as string ?? "").Trim());
        }
        finally { dropLoading = false; }
        dropDirty = true; UpdateDropStatus();
    }

    T? Pick<T>(string title, IReadOnlyList<T> all, Func<T, string> text) where T : class
    {
        using var dlg = new Form { Text = title, Width = 540, Height = 470, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var search = new TextBox { Left = 12, Top = 12, Width = 500, PlaceholderText = "Digite parte do nome..." };
        var list = new ListBox { Left = 12, Top = 42, Width = 500, Height = 340 };
        void Filter()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var x in all.Where(x => search.Text.Length == 0 || text(x).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).Take(500)) list.Items.Add(x);
            list.EndUpdate();
        }
        list.Format += (_, e) => { if (e.ListItem is T x) e.Value = text(x); };
        list.DoubleClick += (_, _) => { if (list.SelectedItem != null) dlg.DialogResult = DialogResult.OK; };
        search.TextChanged += (_, _) => Filter();
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 356, Top = 392, Width = 75 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 437, Top = 392, Width = 75 };
        dlg.Controls.AddRange(new Control[] { search, list, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        Filter();
        return dlg.ShowDialog(this) == DialogResult.OK ? list.SelectedItem as T : null;
    }

    void AddDropRule()
    {
        var item = Pick("Nova regra de drop: escolha o item", Shops.Catalog().OrderBy(d => d.Section).ThenBy(d => d.Type).ToList(), d => $"{d.Name}   [{d.Section},{d.Type}]");
        if (item == null) return;
        var r = new DropRule { Item = item.Section * 512 + item.Type, Rate = 1000, Comment = item.Name + " - Mu Chila" };
        txtDropFind.Text = "";
        var row = dropTable.NewRow();
        dropLoading = true;
        row["Item"] = item.Name; row["Código"] = $"{item.Section},{item.Type}"; row["Nível"] = r.Level; row["Grade"] = r.Grade;
        for (int i = 0; i < DropOptCols.Length; i++) row[DropOptCols[i]] = r.Options[i];
        row["Duração"] = r.Duration; row["Mapa"] = r.Map; row["Nome do mapa"] = MapName(r.Map); row["Monstro"] = r.Monster; row["Nome do monstro"] = MonName(r.Monster);
        row["Nível mín"] = r.LevelMin; row["Nível máx"] = r.LevelMax; row["Chance %"] = Drops.Percent(r.Rate).Replace('.', ','); row["Comentário"] = r.Comment;
        dropTable.Rows.Add(row); dropRows[row] = r;
        dropLoading = false;
        dropDirty = true; UpdateDropStatus();
        gridDrops.CurrentCell = gridDrops.Rows[^1].Cells["Monstro"];
        Log($"Regra nova para {item.Name} (0,1%, qualquer monstro). Escolha o monstro ou o mapa e a chance, e salve.");
    }

    void PickForDrop(bool monstro)
    {
        if (gridDrops.CurrentRow?.DataBoundItem is not DataRowView drv) { Log("Clique numa regra primeiro."); return; }
        var opcoes = new List<Choice> { new("*", "(todos)") };
        opcoes.AddRange(monstro
            ? monsterNames.OrderBy(k => k.Key).Select(k => new Choice(k.Key.ToString(), $"{k.Key} - {k.Value}"))
            : mapNames.OrderBy(k => k.Key).Select(k => new Choice(k.Key.ToString(), $"{k.Key:000} - {k.Value}")));
        var c = Pick(monstro ? "Escolha o monstro" : "Escolha o mapa", opcoes, x => x.Text);
        if (c == null) return;
        dropLoading = true;
        if (monstro) { drv.Row["Monstro"] = c.Value; drv.Row["Nome do monstro"] = MonName(c.Value); }
        else { drv.Row["Mapa"] = c.Value; drv.Row["Nome do mapa"] = MapName(c.Value); }
        dropLoading = false;
        dropDirty = true; UpdateDropStatus();
    }

    void RemoveDropRule()
    {
        if (gridDrops.CurrentRow?.DataBoundItem is not DataRowView drv) return;
        dropRows.Remove(drv.Row); dropTable.Rows.Remove(drv.Row);
        dropDirty = true; UpdateDropStatus();
    }

    void SaveDrops()
    {
        gridDrops.EndEdit();
        var rules = new List<DropRule>();
        foreach (DataRow row in dropTable.Rows)
        {
            var r = dropRows[row];
            string S(string c) => (row[c] as string ?? "").Trim();
            string V(string c, int min, int max) => Drops.Valid(S(c), min, max) ? S(c) : throw new InvalidOperationException($"{row["Item"]}: \"{c}\" inválido ({S(c)}). Use \"*\" ou um número de {min} a {max}.");
            r.Level = V("Nível", 0, 15); r.Grade = V("Grade", 0, 255);
            for (int i = 0; i < DropOptCols.Length; i++) r.Options[i] = V(DropOptCols[i], 0, 255);
            r.Duration = V("Duração", 0, int.MaxValue); r.Map = V("Mapa", 0, 255); r.Monster = V("Monstro", 0, 65535);
            r.LevelMin = V("Nível mín", 0, 1000); r.LevelMax = V("Nível máx", 0, 1000);
            r.Rate = Drops.ParsePercent(S("Chance %"));
            r.Comment = row["Comentário"] as string ?? "";
            rules.Add(r);
        }
        var backup = Drops.Save(rules);
        Log($"Drops salvos ({rules.Count} regras; backup {Path.GetFileName(backup)}).");
        LogAll(ServerControl.Reload("Item"));
        LoadDrops();
    }

    void LoadRates()
    {
        var t = new DataTable();
        t.Columns.Add("Índice", typeof(int)); t.Columns.Add("Monstro"); t.Columns.Add("Nível", typeof(int));
        t.Columns.Add("Drop comum", typeof(int)); t.Columns.Add("Zen", typeof(int)); t.Columns.Add("Nível máx. do item", typeof(int));
        foreach (var m in Drops.MonsterRates()) t.Rows.Add(m.Index, m.Name, m.Level, m.ItemRate, m.MoneyRate, m.MaxItemLevel);
        t.AcceptChanges();
        dropLoading = true;
        gridRates.DataSource = rateTable = t;
        foreach (var c in new[] { "Índice", "Monstro", "Nível" }) gridRates.Columns[c]!.ReadOnly = true;
        gridRates.Columns["Monstro"]!.FillWeight = 200;
        dropLoading = false; rateDirty = false;
        UpdateRateStatus();
    }

    void SaveRates()
    {
        gridRates.EndEdit();
        if (rateTable.GetChanges(DataRowState.Modified) is not { } mudou) { Log("Nenhuma taxa mudou."); return; }
        var list = new List<MonsterRate>();
        foreach (DataRow r in mudou.Rows)
        {
            var m = new MonsterRate((int)r["Índice"], (string)r["Monstro"], (int)r["Nível"], (int)r["Drop comum"], (int)r["Zen"], (int)r["Nível máx. do item"]);
            if (m.ItemRate < 0 || m.MoneyRate < 0 || m.MaxItemLevel is < 0 or > 15)
                throw new InvalidOperationException($"{m.Name}: taxas não podem ser negativas e o nível máximo do item vai de 0 a 15.");
            list.Add(m);
        }
        var (n, backup) = Drops.SaveMonsterRates(list);
        Log($"Taxas de {n} monstro(s) salvas (backup {Path.GetFileName(backup)}).");
        ReloadMonstersSafe();
        LoadRates();
    }

    /// <summary>Reload Monster só nos GameServers e fora da invasão: com uma no ar, agenda para quando ela acabar (o painel precisa ficar aberto).</summary>
    void ReloadMonstersSafe()
    {
        var fim = ServerControl.InvasionEnd(DateTime.Now);
        if (fim == null)
        {
            monsterReloadTimer?.Dispose(); monsterReloadTimer = null;
            LogAll(ServerControl.ReloadMonstersNow());
            Log("Monstros recarregados nos GameServers. O Castle Siege (mapas 31 e 41) pega a mudança quando reiniciar.");
            return;
        }
        if (monsterReloadTimer != null) { Log($"A recarga dos monstros já está agendada para depois da invasão (até {fim:HH:mm:ss})."); return; }
        Log($"Invasão no ar até {fim:HH:mm:ss}: os monstros recarregam sozinhos depois dela. Deixe o painel aberto até lá.");
        monsterReloadTimer = new System.Windows.Forms.Timer { Interval = (int)Math.Clamp((fim.Value - DateTime.Now).TotalMilliseconds + 1000, 1000, int.MaxValue) };
        monsterReloadTimer.Tick += (_, _) => { monsterReloadTimer?.Dispose(); monsterReloadTimer = null; Safe(ReloadMonstersSafe); };
        monsterReloadTimer.Start();
    }

    // ---------------- Comandos (consulta) ----------------
    TabPage CommandsTab()
    {
        var page = new TabPage("Comandos");
        txtCmdFind.TextChanged += (_, _) => Safe(() => FillCommands(txtCmdFind.Text));
        var bar = Bar(new Label { Text = "Comandos do jogo (consulta):", AutoSize = true, Padding = new Padding(0, 6, 6, 0) }, txtCmdFind);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 40, Padding = new Padding(6),
            Text = "A sintaxe fica no executável (o kit só liga/desliga). Comandos de GM exigem conta nível 32. "
                 + "Os de atributo viram /f /a /v /e /c quando os comandos curtos do vigia estão ligados.",
        };
        gridCmds.Dock = DockStyle.Fill;
        page.Controls.Add(gridCmds);
        page.Controls.Add(help);
        page.Controls.Add(bar);
        // só depois de a janela existir: antes disso a grade não gera as colunas e Columns["..."] vem nulo
        Shown += (_, _) => Safe(() => FillCommands(""));
        return page;
    }

    void FillCommands(string filtro)
    {
        var t = new DataTable();
        t.Columns.Add("Comando"); t.Columns.Add("Grupo"); t.Columns.Add("O que faz");
        foreach (var c in Commands.All)
            if (filtro.Length == 0 || c.Cmd.Contains(filtro, StringComparison.OrdinalIgnoreCase) || c.Descricao.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                t.Rows.Add(c.Cmd, c.Grupo, c.Descricao);
        gridCmds.DataSource = t;
        gridCmds.Columns["Comando"]!.FillWeight = 60; gridCmds.Columns["Grupo"]!.FillWeight = 45; gridCmds.Columns["O que faz"]!.FillWeight = 230;
    }

    // ---------------- Monstros (respawn + densidade) ----------------
    static readonly string[] MonCols = { "Classe", "Monstro", "IniX", "IniY", "FimX", "FimY", "Qtd" };

    TabPage MonstersTab()
    {
        var page = new TabPage("Monstros");
        gridMaps.SelectionChanged += (_, _) => Safe(ShowMap);
        gridMon.CellValueChanged += (_, e) => { if (e.RowIndex >= 0 && monFile != null) { monDirty = true; pnlDensidade.Invalidate(); UpdateMonStatus(); } };
        gridMon.DataError += (_, e) => { Log("Valor inválido: use só números."); e.Cancel = true; };
        pnlDensidade.Paint += (_, e) => DrawDensidade(e.Graphics, pnlDensidade.ClientSize);

        var bar = Bar(
            Btn("Adicionar spawn", AddMonster),
            Btn("Remover", () => { if (gridMon.CurrentRow is { } r) { monTable.Rows.RemoveAt(r.Index); monDirty = true; pnlDensidade.Invalidate(); UpdateMonStatus(); } }),
            Btn("Salvar e recarregar", SaveMonsters),
            Btn("Descartar", () => { monDirty = false; ShowMap(force: true); }),
            lblMon);
        var direita = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        direita.Panel1.Controls.Add(Titled("Densidade (área de spawn no mapa 256×256)", pnlDensidade));
        var baixo = new Panel { Dock = DockStyle.Fill }; baixo.Controls.Add(gridMon); baixo.Controls.Add(bar);
        direita.Panel2.Controls.Add(Titled("Spawns deste mapa (edite a quantidade e a área)", baixo));
        direita.SizeChanged += (_, _) => { if (direita.Height > 100) direita.SplitterDistance = (int)(direita.Height * 0.42); };

        page.Controls.Add(Split(Titled("Mapas (densidade)", gridMaps), direita, 0.34));
        Shown += (_, _) => Safe(LoadMaps);
        return page;
    }

    void LoadMaps()
    {
        mapas = Monsters.Maps();
        var t = new DataTable();
        t.Columns.Add("Mapa"); t.Columns.Add("Monstros", typeof(int)); t.Columns.Add("Spawns", typeof(int)); t.Columns.Add("Code", typeof(int));
        foreach (var m in mapas) t.Rows.Add($"{m.Code:000} {m.Name}", m.Total, m.Pontos, m.Code);
        gridMaps.DataSource = t;
        gridMaps.Columns["Code"]!.Visible = false;
        gridMaps.Columns["Mapa"]!.FillWeight = 130; gridMaps.Columns["Monstros"]!.FillWeight = 55; gridMaps.Columns["Spawns"]!.FillWeight = 45;
        int total = mapas.Sum(m => m.Total);
        Log($"Densidade carregada: {mapas.Count} mapas, {total} monstros no total (limite ~9078).");
    }

    void ShowMap() => ShowMap(force: false);
    void ShowMap(bool force)
    {
        if (gridMaps.CurrentRow?.Cells["Code"].Value is not int code) return;
        var m = mapas.FirstOrDefault(x => x.Code == code);
        if (m == null) return;
        if (!force && monFile == m.File) return;
        if (monDirty && monFile != m.File && !Confirm("O mapa atual tem alterações não salvas. Descartar?")) return;
        monDirty = false;
        var t = new DataTable();
        t.Columns.Add(MonCols[0], typeof(int)); t.Columns.Add(MonCols[1]); for (int i = 2; i < MonCols.Length; i++) t.Columns.Add(MonCols[i], typeof(int));
        foreach (var s in Monsters.Load(m.File)) t.Rows.Add(s.Class, s.Name, s.BeginX, s.BeginY, s.EndX, s.EndY, s.Quantity);
        monFile = null;   // evita marcar dirty durante a carga
        gridMon.DataSource = monTable = t;
        gridMon.Columns["Monstro"]!.ReadOnly = true; gridMon.Columns["Monstro"]!.FillWeight = 160;
        monFile = m.File;
        pnlDensidade.Invalidate(); UpdateMonStatus();
    }

    List<MonsterSpawn> MonFromGrid() => monTable.Rows.Cast<DataRow>().Select(r => new MonsterSpawn
    {
        Class = (int)r["Classe"], Name = (string)r["Monstro"], BeginX = (int)r["IniX"], BeginY = (int)r["IniY"],
        EndX = (int)r["FimX"], EndY = (int)r["FimY"], Quantity = (int)r["Qtd"],
    }).ToList();

    void UpdateMonStatus()
    {
        if (monFile == null) return;
        int total = MonFromGrid().Sum(s => s.Quantity);
        lblMon.Text = $"   {monTable.Rows.Count} spawns, {total} monstros neste mapa" + (monDirty ? "   (não salvo)" : "");
    }

    void AddMonster()
    {
        if (monFile == null) return;
        monTable.Rows.Add(0, "Classe 0", 100, 100, 110, 110, 10);
        monDirty = true; pnlDensidade.Invalidate(); UpdateMonStatus();
        gridMon.CurrentCell = gridMon.Rows[^1].Cells[0];
    }

    void SaveMonsters()
    {
        if (monFile == null) return;
        gridMon.EndEdit();
        Monsters.Save(monFile, MonFromGrid());
        Log($"Respawn salvo ({monTable.Rows.Count} spawns; backup .bak-* ao lado do arquivo).");
        ReloadMonstersSafe();
        monDirty = false; UpdateMonStatus();
        LoadMaps();   // atualiza a densidade
    }

    /// <summary>Desenha as áreas de spawn do mapa selecionado num quadro 256×256; sobreposições ficam mais escuras (densidade).</summary>
    void DrawDensidade(Graphics g, Size size)
    {
        g.Clear(Color.White);
        if (monFile == null || monTable.Rows.Count == 0) return;
        int lado = Math.Min(size.Width, size.Height) - 2;
        if (lado < 20) return;
        float esc = lado / (float)Monsters.MapTiles;
        using var borda = new Pen(Color.Gainsboro);
        g.DrawRectangle(borda, 0, 0, lado, lado);
        using var fill = new SolidBrush(Color.FromArgb(40, 30, 90, 200));   // translúcido: sobreposição escurece
        foreach (DataRow r in monTable.Rows)
        {
            int x1 = (int)r["IniX"], y1 = (int)r["IniY"], x2 = (int)r["FimX"], y2 = (int)r["FimY"], q = (int)r["Qtd"];
            int lx = Math.Min(x1, x2), ly = Math.Min(y1, y2), w = Math.Abs(x2 - x1) + 1, h = Math.Abs(y2 - y1) + 1;
            var rect = new RectangleF(lx * esc, ly * esc, Math.Max(2, w * esc), Math.Max(2, h * esc));
            g.FillRectangle(fill, rect);   // mais spawns no mesmo lugar = azul mais forte
            if (q >= 15) g.FillRectangle(fill, rect);   // farm (muitos): reforça a cor
        }
    }

    // ---------------- Itens e baú ----------------
    TabPage ItemsTab()
    {
        var page = new TabPage("Itens e baú");
        cmbItemAccount.DropDown += (_, _) => Safe(() =>
        {
            var sel = cmbItemAccount.SelectedItem;
            cmbItemAccount.Items.Clear();
            foreach (DataRow r in Accounts.List().Rows) cmbItemAccount.Items.Add((string)r["Conta"]);
            if (sel != null) cmbItemAccount.SelectedItem = sel;
        });
        cmbItemAccount.SelectedIndexChanged += (_, _) => Safe(() =>
        {
            cmbItemPlace.Items.Clear();
            if (cmbItemAccount.SelectedItem is not string a) return;
            foreach (var c in Accounts.Characters(a)) cmbItemPlace.Items.Add(c);
            cmbItemPlace.Items.Add(VaultOption);
            cmbItemPlace.SelectedIndex = 0;
        });
        cmbItemPlace.SelectedIndexChanged += (_, _) => Safe(ShowItems);
        Label L(string s) => new() { Text = s, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        var bar = Bar(L("Conta:"), cmbItemAccount, L("Personagem:"), cmbItemPlace,
            Btn("Atualizar", ShowItems),
            Btn("Remover item selecionado", RemoveItem),
            Btn("Dar item (Gremory Case)...", GiveItem),
            Btn("Cancelar presente selecionado", () =>
            {
                if (cmbItemAccount.SelectedItem is not string a || gridGifts.CurrentRow?.Cells["Codigo"].Value is not int code) { Log("Selecione um presente."); return; }
                if (!Confirm("Cancelar este presente? O jogador não vai recebê-lo.")) return;
                Items.CancelGift(a, code); Log("Presente cancelado."); ShowItems();
            }));
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 54, Padding = new Padding(6),
            Text = "Remover só funciona com a conta fora do jogo (o servidor regravaria o item ao sair) e salva antes o inventário/baú inteiro em " +
                   "C:\\MuServer\\DB\\backup-itens-*.csv. Presentes vão para a Gremory Case do jogador: ele recebe ao entrar e puxa para o inventário. " +
                   "Não use o MuEditor para salvar inventários do S14: ele é da Season 8 e pode apagar o inventário expandido e o baú estendido.",
        };
        page.Controls.Add(Split(Titled("Itens", gridItems), Titled("Presentes pendentes na Gremory Case", gridGifts), 0.68));
        page.Controls.Add(bar);
        page.Controls.Add(help);
        return page;
    }

    void ShowItems()
    {
        if (cmbItemAccount.SelectedItem is not string account || cmbItemPlace.SelectedItem is not string place) return;
        itemsShown = place == VaultOption ? Items.Vault(account) : Items.Inventory(place);
        var t = new DataTable();
        foreach (var c in new[] { "Posição", "Onde", "Item", "Nível", "Skill", "Sorte", "Opção", "Excelente", "Set", "Dur" }) t.Columns.Add(c);
        foreach (var i in itemsShown)
            t.Rows.Add(i.Slot, i.Place, i.Name, $"+{i.Level}", i.Skill ? "sim" : "", i.Luck ? "sim" : "", i.Option > 0 ? $"+{i.Option * 4}" : "",
                       i.Excellent == 0 ? "" : $"{System.Numerics.BitOperations.PopCount((uint)i.Excellent)} opção(ões)", i.SetOption > 0 ? "ancient" : "", i.Durability);
        gridItems.DataSource = t;
        if (gridItems.Columns["Item"] is { } col) col.FillWeight = 250;
        gridGifts.DataSource = Items.Gifts(account);
    }

    void RemoveItem()
    {
        if (cmbItemAccount.SelectedItem is not string account || cmbItemPlace.SelectedItem is not string place) return;
        if (gridItems.CurrentRow == null) { Log("Selecione um item."); return; }
        var item = itemsShown[gridItems.CurrentRow.Index];
        if (!Confirm($"Remover {item.Name} +{item.Level} ({item.Place}, posição {item.Slot}) de {(place == VaultOption ? "baú de " + account : place)}?\n\nO inventário/baú inteiro é salvo antes num arquivo de backup.")) return;
        Log(Items.Remove(account, place == VaultOption ? null : place, item));
        ShowItems();
    }

    void GiveItem()
    {
        if (cmbItemAccount.SelectedItem is not string account || cmbItemPlace.SelectedItem is not string place) { Log("Escolha a conta e o personagem."); return; }
        if (Items.RewardSource() == null) { Log("A Gremory Case ainda não foi calibrada (falta um /gremgif de exemplo no jogo); presentes pelo painel ficam desligados até lá."); return; }
        using var dlg = new Form { Text = "Dar item pela Gremory Case", Width = 520, Height = 500, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var search = new TextBox { Left = 12, Top = 12, Width = 480, PlaceholderText = "Digite parte do nome do item..." };
        var list = new ListBox { Left = 12, Top = 42, Width = 480, Height = 290 };
        var all = Shops.Catalog().OrderBy(d => d.Section).ThenBy(d => d.Type).ToList();
        void Filter()
        {
            list.BeginUpdate(); list.Items.Clear();
            foreach (var d in all.Where(d => search.Text.Length == 0 || d.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).Take(400)) list.Items.Add(d);
            list.EndUpdate();
        }
        list.Format += (_, e) => { if (e.ListItem is ItemDef d) e.Value = $"{d.Name}   [{d.Section},{d.Type}]"; };
        search.TextChanged += (_, _) => Filter();
        NumericUpDown N(int left, int top, int min, int max, int value) => new() { Left = left, Top = top, Width = 55, Minimum = min, Maximum = max, Value = value };
        Label L(int left, int top, string text) => new() { Left = left, Top = top + 3, AutoSize = true, Text = text };
        var level = N(55, 345, 0, 15, 0); var opt = N(150, 345, 0, 7, 0); var exc = N(255, 345, 0, 63, 0); var days = N(385, 345, 1, 3650, 30);
        var skill = new CheckBox { Left = 12, Top = 378, Text = "Skill", AutoSize = true };
        var luck = new CheckBox { Left = 75, Top = 378, Text = "Sorte", AutoSize = true };
        var toAccount = new CheckBox { Left = 145, Top = 378, Text = "Para a conta (qualquer personagem pega)", AutoSize = true, Checked = place == VaultOption, Enabled = place != VaultOption };
        var ok = new Button { Text = "Dar", DialogResult = DialogResult.OK, Left = 336, Top = 415, Width = 75 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 417, Top = 415, Width = 75 };
        dlg.Controls.AddRange(new Control[] { search, list, L(12, 345, "Nível"), level, L(110, 345, "Opção"), opt, L(210, 345, "Exc (0-63)"), exc,
                                              L(320, 345, "Dias p/ pegar"), days, skill, luck, toAccount, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        Filter();
        if (dlg.ShowDialog(this) != DialogResult.OK || list.SelectedItem is not ItemDef item) return;
        Log(Items.Gift(account, toAccount.Checked ? null : place, item.Section, item.Type, (int)level.Value, skill.Checked, luck.Checked, (int)opt.Value, (int)exc.Value, (int)days.Value));
        ShowItems();
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
            Btn("Aplicar VIP", () => ForSelectedAccount(a =>
            {
                Accounts.SetVip(a, cmbLevel.SelectedIndex, (int)numDays.Value);
                Log($"{a}: {cmbLevel.SelectedItem} por {numDays.Value} dia(s)");
                OfferLogout(a, "O novo VIP só vale no próximo login.");
                return null;
            })),
            Btn("Remover VIP", () => ForSelectedAccount(a =>
            {
                Accounts.SetVip(a, 0, 0);
                Log($"{a}: VIP removido");
                OfferLogout(a, "A remoção do VIP só vale no próximo login.");
                return null;
            })),
            Btn("Banir", () => ForSelectedAccount(a =>
            {
                if (!Confirm($"Banir a conta {a}?")) return null;
                Accounts.SetBanned(a, true);
                Log($"{a}: banida");
                OfferLogout(a, "O ban impede o próximo login, mas não tira quem já está jogando.");
                return null;
            })),
            Btn("Desbanir", () => ForSelectedAccount(a => { Accounts.SetBanned(a, false); return $"{a}: desbanida"; })),
            Btn("Zerar habilidades master", () => ForSelectedAccount(ClearMasterSkills)));

        var note = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(6),
            Text = "VIP e ban valem no próximo login da conta. Benefícios de cada nível (experiência, drop, pontos...) ficam nas linhas *_AL1/_AL2/_AL3 " +
                   "do GameServerInfo - Common.dat. Personagens, inventário e baú: use o MuEditor (aba Servidor). " +
                   "\"Zerar habilidades master\": para quem mostra \"Suces de Atq\" negativo. Com a conta online, o painel oferece forçar o logout.",
        };
        page.Controls.Add(Titled("Contas", gridAccounts));
        page.Controls.Add(bar);
        page.Controls.Add(note);
        return page;
    }

    /// <summary>Se a conta estiver online, pergunta se deve forcar o logout (para a alteracao valer ja) e faz, se confirmado.</summary>
    void OfferLogout(string account, string why)
    {
        if (!Db.IsOnline(account)) return;
        if (Confirm($"A conta {account} está online. {why}\n\nForçar o logout agora? O jogador é desconectado e o personagem é salvo.{SameIpWarning(account)}"))
            RunForceLogout(account);
    }

    static string SameIpWarning(string account)
    {
        var others = ServerControl.OnlineSameIp(account);
        return others.Length == 0 ? "" : $"\n\nAtenção: {string.Join(", ", others)} usa(m) o mesmo IP e também será(ão) desconectada(s).";
    }

    void RunForceLogout(string account)
    {
        Cursor = Cursors.WaitCursor;
        try { Log(ServerControl.ForceLogout(account)); }
        finally { Cursor = Cursors.Default; }
        Safe(RefreshServer, quiet: true);
    }

    string? ClearMasterSkills(string account)
    {
        if (Db.IsOnline(account))
        {
            // O servidor grava o personagem ao sair: a conta precisa sair ANTES de mexer, senao a alteracao e desfeita
            if (!Confirm($"A conta {account} está online. Para zerar as habilidades master, o jogador precisa sair do jogo antes.\n\n" +
                         $"Forçar o logout e continuar?{SameIpWarning(account)}"))
                return null;
            RunForceLogout(account);
            Thread.Sleep(3000);   // margem para o DataServer terminar de gravar o personagem
            if (Db.IsOnline(account)) { Log($"{account} ainda aparece online; nada foi alterado. Tente de novo em alguns segundos."); return null; }
        }
        var character = PickCharacter(account);
        if (character == null) return null;
        if (!Confirm($"Zerar as habilidades master de {character}?\n\nA árvore master é apagada, os pontos voltam a ficar livres (1 por Master Level) e os poderes master " +
                     "saem da lista de habilidades (os melhorados voltam a ser a habilidade normal, ex.: Twisting Slash Improved → Twisting Slash). " +
                     "Use quando a janela (C) do personagem mostrar \"Suces de Atq\" negativo."))
            return null;
        return Accounts.ClearMasterSkills(account, character);
    }

    static string? PickCharacter(string account)
    {
        var names = Accounts.Characters(account);
        if (names.Length == 0) { MessageBox.Show($"A conta {account} não tem personagens.", "Mu Chila Admin"); return null; }
        using var dlg = new Form
        {
            Text = $"Personagem da conta {account}", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(320, 90), Font = new Font("Segoe UI", 9),
        };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 12, Top = 12, Width = 296 };
        combo.Items.AddRange(names.Cast<object>().ToArray());
        combo.SelectedIndex = 0;
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 152, Top = 52, Width = 75 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 233, Top = 52, Width = 75 };
        dlg.Controls.AddRange(new Control[] { combo, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        return dlg.ShowDialog() == DialogResult.OK ? (string)combo.SelectedItem! : null;
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

    /// <summary>Erros engolidos pelo Safe (o autoteste confere que a janela abre sem nenhum).</summary>
    internal readonly List<string> SafeErrors = new();

    void Safe(Action action, bool quiet = false)
    {
        try { action(); }
        catch (Exception ex)
        {
            SafeErrors.Add(ex.Message);
            if (!quiet) Log("ERRO: " + ex.Message);
            // detalhe completo (onde aconteceu) para diagnóstico, ao lado do executável
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "painel-erros.log"), $"{DateTime.Now:s} {ex}{Environment.NewLine}{Environment.NewLine}"); } catch { }
        }
    }
}

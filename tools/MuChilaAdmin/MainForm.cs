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
            Btn("Abrir MuEditor", () => ServerControl.Open(ServerControl.MuEditorPath)),
            Btn("Abrir launcher", () => ServerControl.Open(ServerControl.LauncherPath)));

        cmbReload.Items.AddRange(ServerControl.ReloadIds.Keys.Cast<object>().ToArray());
        cmbReload.SelectedIndex = 0;
        var reloadBar = Bar(new Label { Text = "Recarregar sem reiniciar:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, cmbReload,
            Btn("Recarregar (GameServers + Castle Siege)", () => LogAll(ServerControl.Reload((string)cmbReload.SelectedItem!))), lblCounts);

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
        if (lstShops.Items.Count > 0) lstShops.SelectedIndex = 0;
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
            Btn("Gerar patch p/ amigos", GenerateCashPatch),
            lblCash);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(6),
            Text = "O preço é em W Coin/Goblin (a moeda de cada pacote). Quem cobra é o servidor; o cliente só mostra. " +
                   "\"Na loja\" desmarcado esconde o pacote (dá para voltar depois). \"Salvar e aplicar\" faz backup, grava servidor + cliente e " +
                   "recarrega a loja nos GameServers. Depois use \"Gerar patch\" para os amigos verem os nomes/preços novos.",
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
        if (!somenteServidor) Log("Cliente atualizado. Use \"Gerar patch p/ amigos\" para empacotar os arquivos novos.");
        cashDirty = false; UpdateCashStatus();
    }

    void GenerateCashPatch()
    {
        if (!CashShop.ClientAvailable) { Log($"Cliente não encontrado em {CashShop.ClientDir}; nada a empacotar."); return; }
        var script = Path.Combine(ServerControl.ServerRoot, @"..\Projetos\MuServer-Season14\tools\atualiza_zips2.ps1");
        var repoScript = @"C:\Projetos\MuServer-Season14\tools\atualiza_zips2.ps1";
        var used = File.Exists(repoScript) ? repoScript : script;
        if (!File.Exists(used)) { Log($"Script de patch não encontrado ({used}). Gere o patch pelo tools\\atualiza_zips2.ps1."); return; }
        if (!Confirm("Gerar o patch e o cliente completo dos amigos com os arquivos da loja novos? Pode levar alguns minutos (o cliente completo é grande).")) return;
        const string sub = @"Data\InGameShopScript\512.2011.006\";
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{used}\" -Arquivos \"{sub}IBSPackage.txt\",\"{sub}IBSProduct.txt\",\"{sub}IBSCategory.txt\"";
        Log("Gerando patch dos amigos em segundo plano (atualiza_zips2.ps1)...");
        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", args) { UseShellExecute = true };
        System.Diagnostics.Process.Start(psi);
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

    void Safe(Action action, bool quiet = false)
    {
        try { action(); }
        catch (Exception ex) { if (!quiet) Log("ERRO: " + ex.Message); }
    }
}

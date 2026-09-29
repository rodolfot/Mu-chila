namespace MuChilaAdmin;

/// <summary>
/// Aba "Taxas e opções" (29/09/2026): edita as opções dos GameServers (os 7 "GameServerInfo - X.dat") e a chance de drop
/// das joias (ItemDrop.txt). "Principais" junta as de economia num lugar só; as outras visões mostram cada arquivo inteiro.
/// Grava igual nos 5 GameServers, dá o Reload do arquivo e confere na memória se valeu na hora (ServerSettings.SaveAndApply).
/// </summary>
public sealed partial class MainForm
{
    readonly DataGridView gridSet = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false, BackgroundColor = SystemColors.Window,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
    };
    readonly ComboBox cmbSetView = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    readonly TextBox txtSetFind = new() { Width = 200, PlaceholderText = "filtrar por nome..." };
    readonly Label lblSet = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    List<ServerSettings.Row> setRows = new();
    readonly Dictionary<(string Id, int Plan), string> setEdits = new();
    int setViewShown = -1;
    bool setFilling, setBusy;
    Button? btnSetSave;

    TabPage SettingsTab()
    {
        var page = new TabPage("Taxas e opções");
        cmbSetView.Items.Add("Principais (economia: EXP, drop, zen, joias, Chaos Machine...)");
        foreach (var f in ServerSettings.Files) cmbSetView.Items.Add("Todas: " + f.Label);
        cmbSetView.SelectedIndexChanged += (_, _) => Safe(() =>
        {
            if (cmbSetView.SelectedIndex == setViewShown) return;
            if (setEdits.Count > 0 && !Confirm("Há mudanças não salvas nesta lista. Descartar?")) { cmbSetView.SelectedIndex = setViewShown; return; }
            LoadSettings();
        });
        txtSetFind.TextChanged += (_, _) => Safe(FillSettings);
        foreach (var (name, header, weight, ro) in new[]
        {
            ("sec", "Seção", 90, true), ("opt", "Opção", 200, true), ("v0", "Free / valor", 60, false), ("v1", "Vip1", 45, false),
            ("v2", "Vip2", 45, false), ("v3", "Vipzão", 45, false), ("obs", "Obs", 230, true),
        })
            gridSet.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = weight, ReadOnly = ro, SortMode = DataGridViewColumnSortMode.NotSortable });
        gridSet.CellValueChanged += (_, e) => Safe(() => SetCellChanged(e.RowIndex, e.ColumnIndex));
        btnSetSave = Btn("Salvar e aplicar", SaveSettings);
        var bar = Bar(new Label { Text = "Mostrar:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, cmbSetView, txtSetFind, btnSetSave,
                      Btn("Recarregar da pasta", () => { if (setEdits.Count == 0 || Confirm("Descartar as mudanças não salvas?")) LoadSettings(); }), lblSet);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 62, Padding = new Padding(6),
            Text = "Clique na célula e digite o valor novo (fica amarelo). \"Salvar e aplicar\" grava igual nos 5 GameServers (com backup), dá o Reload do " +
                   "arquivo e confere na memória se valeu na hora ou só depois de reiniciar. Linhas cinza não se mexem por aqui (diferentes em cada " +
                   "servidor, ou identidade/conexão). Colunas Free/Vip1/Vip2/Vipzão = por plano; opção única usa só a 1ª coluna.",
        };
        page.Controls.Add(gridSet);
        page.Controls.Add(help);
        page.Controls.Add(bar);
        Shown += (_, _) => Safe(() => { cmbSetView.SelectedIndex = 0; });
        return page;
    }

    void LoadSettings()
    {
        setEdits.Clear();
        setViewShown = cmbSetView.SelectedIndex;
        setRows = setViewShown <= 0 ? ServerSettings.Main() : ServerSettings.Load(ServerSettings.Files[setViewShown - 1].Name);
        FillSettings();
    }

    void FillSettings()
    {
        setFilling = true;
        try
        {
            gridSet.Rows.Clear();
            var filter = txtSetFind.Text.Trim();
            bool bonus = TimedBonuses.List().Any(b => b.ActiveAt(DateTime.Now));
            foreach (var r in setRows)
            {
                if (filter.Length > 0 && !(r.Label.Contains(filter, StringComparison.OrdinalIgnoreCase) || r.BaseKey.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                           || ServerSettings.SectionLabel(r.Section).Contains(filter, StringComparison.OrdinalIgnoreCase))) continue;
                var lockReason = r.LockReason ?? (bonus && ServerSettings.BonusKeys.Contains(r.BaseKey) ? "bônus por tempo ativo: o arquivo está com o valor do bônus; mude depois que ele acabar" : null);
                var obs = lockReason ?? string.Join(" ", new[] { r.Help, r.Label != r.BaseKey && r.File != ServerSettings.ItemDropFile ? $"[{r.BaseKey}]" : "" }.Where(s => s.Length > 0));
                var vals = Enumerable.Range(0, 4).Select(i => setEdits.TryGetValue((r.Id, i), out var ed) ? ed : r.PerPlan || i == 0 ? r.Values[i] : "").ToArray();
                int n = gridSet.Rows.Add(ServerSettings.SectionLabel(r.Section), r.Label, vals[0], vals[1], vals[2], vals[3], obs);
                var gr = gridSet.Rows[n]; gr.Tag = r;
                if (lockReason != null) { gr.ReadOnly = true; gr.DefaultCellStyle.ForeColor = SystemColors.GrayText; }
                else if (!r.PerPlan) for (int c = 3; c <= 5; c++) { gr.Cells[c].ReadOnly = true; gr.Cells[c].Style.BackColor = SystemColors.Control; }
                for (int i = 0; i < 4; i++) if (setEdits.ContainsKey((r.Id, i))) gr.Cells[2 + i].Style.BackColor = Color.LightYellow;
            }
        }
        finally { setFilling = false; }
        UpdateSetStatus();
    }

    void SetCellChanged(int row, int col)
    {
        if (setFilling || row < 0 || col < 2 || col > 5 || gridSet.Rows[row].Tag is not ServerSettings.Row r) return;
        int plan = col - 2;
        var val = gridSet.Rows[row].Cells[col].Value?.ToString()?.Trim() ?? "";
        if (val == r.Values[plan]) setEdits.Remove((r.Id, plan));
        else setEdits[(r.Id, plan)] = val;
        gridSet.Rows[row].Cells[col].Style.BackColor = setEdits.ContainsKey((r.Id, plan)) ? Color.LightYellow : Color.Empty;
        UpdateSetStatus();
    }

    void UpdateSetStatus() =>
        lblSet.Text = $"   {setRows.Count} opções" + (setEdits.Count > 0 ? $"   ({setEdits.Count} mudança(s) não salva(s))" : "") + (setBusy ? "   — aplicando..." : "");

    void SaveSettings()
    {
        gridSet.EndEdit();
        if (setBusy) { Log("Ainda aplicando a gravação anterior; espere terminar."); return; }
        if (setEdits.Count == 0) { Log("Taxas e opções: nada mudou."); return; }
        var byId = setRows.ToDictionary(r => r.Id);
        var changes = new List<ServerSettings.Change>(); var jewels = new List<(ServerSettings.Row, string)>(); var lines = new List<string>();
        foreach (var ((id, plan), val) in setEdits)
        {
            var r = byId[id];
            if (r.File == ServerSettings.ItemDropFile) { Drops.ParsePercent(val); jewels.Add((r, val)); lines.Add($"{r.Label}: {r.Values[0]}% → {val}%"); }
            else { changes.Add(new ServerSettings.Change(r.File, r.KeyOf(plan), val, r)); lines.Add($"{r.Label}{(r.PerPlan ? $" ({ServerSettings.Plans[plan]})" : "")}: {r.Values[plan]} → {val}"); }
        }
        ServerSettings.Validate(changes);
        if (!Confirm($"Gravar {lines.Count} mudança(s) nos 5 GameServers e recarregar?\n\n{string.Join("\n", lines.Take(25))}{(lines.Count > 25 ? "\n..." : "")}")) return;
        setBusy = true; btnSetSave!.Enabled = false; UpdateSetStatus();
        Log($"Taxas e opções: gravando {lines.Count} mudança(s)...");
        Task.Run(() =>
        {
            try { ServerSettings.SaveAndApply(changes, jewels, msg => BeginInvoke(() => Log(msg))); }
            catch (Exception ex) { BeginInvoke(() => Log("ERRO: " + ex.Message)); }
            finally { BeginInvoke(() => { setBusy = false; btnSetSave.Enabled = true; Safe(LoadSettings); }); }
        });
    }
}

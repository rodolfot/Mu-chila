using System.Data;

namespace MuChilaAdmin;

/// <summary>
/// Aba "EXP dinâmica" (issue #37): edita as faixas do Data\Util\ExperienceTable.txt (DynamicExp.cs). Mostra, ao lado de cada
/// faixa, a EXP efetiva de cada plano (taxa do plano × % da faixa) com as taxas atuais do Common.dat.
/// </summary>
public sealed partial class MainForm
{
    readonly DataGridView gridExp = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false, BackgroundColor = SystemColors.Window,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
    };
    readonly Label lblExp = new() { AutoSize = true, Padding = new Padding(0, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    DataTable expTable = new();
    bool expDirty, expFilling;
    int[] expPlanRates = { 100, 100, 100, 100 };

    static readonly (string Col, string Header, int Weight)[] ExpCols =
    {
        ("lmin", "Nível de", 55), ("lmax", "Nível até", 55), ("rate", "EXP %", 55),
        ("p0", "Free", 55), ("p1", "Vip1", 55), ("p2", "Vip2", 55), ("p3", "Vipzão", 55),
        ("mmin", "Master de", 55), ("mmax", "Master até", 55), ("rmin", "Resets de", 55), ("rmax", "Resets até", 55), ("mrmin", "M.resets de", 60), ("mrmax", "M.resets até", 60),
    };

    TabPage DynamicExpTab()
    {
        var page = new TabPage("EXP dinâmica");
        gridExp.DataError += (_, e) => { Log("Valor inválido: use só números inteiros."); e.Cancel = true; };
        gridExp.CellValueChanged += (_, e) => Safe(() =>
        {
            if (expFilling || e.RowIndex < 0) return;
            expDirty = true; UpdateExpComputed(e.RowIndex); UpdateExpStatus();
        });
        var bar = Bar(
            Btn("Adicionar faixa", () =>
            {
                int de = expTable.Rows.Count > 0 && expTable.Rows[^1]["lmax"] is int m ? Math.Min(400, m + 1) : 1;
                AddExpRow(new DynamicExp.Band { LevelMin = de, LevelMax = Math.Max(de, 400), Rate = 100 });
                expDirty = true; UpdateExpStatus();
                gridExp.CurrentCell = gridExp.Rows[^1].Cells["lmin"];
            }),
            Btn("Remover faixa", () =>
            {
                if (gridExp.CurrentRow?.DataBoundItem is not DataRowView v) { Log("Selecione uma faixa."); return; }
                expTable.Rows.Remove(v.Row); expDirty = true; UpdateExpStatus();
            }),
            Btn("Salvar e aplicar", SaveDynamicExp),
            Btn("Recarregar da pasta", () => { if (!expDirty || Confirm("Descartar as mudanças não salvas?")) LoadDynamicExp(); }),
            Btn("Voltar à curva padrão (100% → 10%)", () =>
            {
                if (!Confirm("Trocar a lista pela curva de 28/09 (100% até o nível 50, caindo até 10% nos 351–399, 100% no 400)? Só grava ao clicar em \"Salvar e aplicar\".")) return;
                FillDynamicExp(DynamicExp.Default()); expDirty = true; UpdateExpStatus();
            }),
            lblExp);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 62, Padding = new Padding(6),
            Text = "A EXP de cada jogador = taxa do plano (\"EXP (x)\" da aba Taxas e opções) × \"EXP %\" da faixa do nível dele. 100% = neutro; níveis sem faixa ficam em 100%. " +
                   "As colunas Free/Vip1/Vip2/Vipzão mostram a EXP efetiva. Master/resets: a faixa só vale para quem está dentro deles (o padrão 0–600 e 0–10000 vale para todos). " +
                   "\"Salvar e aplicar\" faz backup do ExperienceTable.txt e dá Reload Util nos servidores.",
        };
        page.Controls.Add(gridExp); page.Controls.Add(help); page.Controls.Add(bar);
        Shown += (_, _) => Safe(LoadDynamicExp);
        return page;
    }

    void LoadDynamicExp()
    {
        var rates = ServerSettings.Load("Common").FirstOrDefault(r => r.BaseKey == "AddExperienceRate");
        if (rates != null) expPlanRates = rates.Values.Select(v => int.TryParse(v, out var x) ? x : 100).ToArray();
        var (bands, outside) = DynamicExp.Load();
        FillDynamicExp(bands);
        expDirty = false; UpdateExpStatus();
        if (outside > 0) Log($"EXP dinâmica: o ExperienceTable.txt tem {outside} faixa(s) fora do bloco do painel (postas à mão); o servidor também usa, e o painel não mexe nelas.");
    }

    void FillDynamicExp(IEnumerable<DynamicExp.Band> bands)
    {
        expFilling = true;
        try
        {
            expTable = new DataTable();
            foreach (var (c, _, _) in ExpCols) expTable.Columns.Add(c, c.StartsWith('p') ? typeof(string) : typeof(int));
            foreach (var b in bands) AddExpRow(b, fill: false);
            gridExp.DataSource = expTable;
            foreach (var (c, h, w) in ExpCols)
            {
                var col = gridExp.Columns[c]!; col.HeaderText = h; col.FillWeight = w; col.SortMode = DataGridViewColumnSortMode.NotSortable;
                if (c.StartsWith('p')) { col.ReadOnly = true; col.DefaultCellStyle.BackColor = SystemColors.Control; }
                if (c is "lmin" or "lmax" or "rate") col.DefaultCellStyle.Font = new Font(gridExp.Font, FontStyle.Bold);
            }
            for (int i = 0; i < expTable.Rows.Count; i++) UpdateExpComputed(i);
        }
        finally { expFilling = false; }
    }

    void AddExpRow(DynamicExp.Band b, bool fill = true)
    {
        var r = expTable.NewRow();
        r["lmin"] = b.LevelMin; r["lmax"] = b.LevelMax; r["rate"] = b.Rate; r["mmin"] = b.MasterMin; r["mmax"] = b.MasterMax;
        r["rmin"] = b.ResetMin; r["rmax"] = b.ResetMax; r["mrmin"] = b.MResetMin; r["mrmax"] = b.MResetMax;
        expTable.Rows.Add(r);
        if (fill) UpdateExpComputed(expTable.Rows.Count - 1);
    }

    void UpdateExpComputed(int row)
    {
        if (row < 0 || row >= expTable.Rows.Count) return;
        var r = expTable.Rows[row];
        bool was = expFilling; expFilling = true;
        try { for (int p = 0; p < 4; p++) r[$"p{p}"] = r["rate"] is int pct ? $"{expPlanRates[p] * pct / 100.0:0.#}x" : ""; }
        finally { expFilling = was; }
    }

    List<DynamicExp.Band> ExpBandsFromGrid()
    {
        gridExp.EndEdit();
        int V(DataRow r, string c, string nome) => r[c] is int v ? v : throw new InvalidOperationException($"Preencha \"{nome}\" em todas as faixas.");
        return expTable.Rows.Cast<DataRow>().Select(r => new DynamicExp.Band
        {
            LevelMin = V(r, "lmin", "Nível de"), LevelMax = V(r, "lmax", "Nível até"), Rate = V(r, "rate", "EXP %"),
            MasterMin = V(r, "mmin", "Master de"), MasterMax = V(r, "mmax", "Master até"), ResetMin = V(r, "rmin", "Resets de"),
            ResetMax = V(r, "rmax", "Resets até"), MResetMin = V(r, "mrmin", "M.resets de"), MResetMax = V(r, "mrmax", "M.resets até"),
        }).ToList();
    }

    void UpdateExpStatus()
    {
        string lacunas = "";
        try { lacunas = DynamicExp.Gaps(ExpBandsFromGridQuiet()); } catch { }
        lblExp.Text = $"   {expTable.Rows.Count} faixa(s); EXP dos planos: {string.Join(" / ", expPlanRates.Select(x => x + "x"))}" +
                      (lacunas.Length > 0 ? $"; níveis sem faixa (100%): {lacunas}" : "") + (expDirty ? "   (não salvo)" : "");
    }

    List<DynamicExp.Band> ExpBandsFromGridQuiet() => expTable.Rows.Cast<DataRow>()
        .Where(r => r["lmin"] is int && r["lmax"] is int).Select(r => new DynamicExp.Band { LevelMin = (int)r["lmin"], LevelMax = (int)r["lmax"] }).ToList();

    /// <summary>Teste (--testar-exp-visao): abre a aba com os dados reais, simula uma edição SÓ NA TELA e tira uma foto.</summary>
    internal string TestDynamicExpView(string png)
    {
        var sb = new System.Text.StringBuilder(); int fails = 0;
        void Check(string name, bool ok, string extra = "") { if (!ok) fails++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {name} {extra}"); }
        Width = 1400; Height = 800; StartPosition = FormStartPosition.Manual; Left = -3000; Top = 0; ShowInTaskbar = false;
        Show(); Application.DoEvents();
        var tabs = Controls.OfType<TabControl>().First();
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "EXP dinâmica");
        LoadDynamicExp(); Application.DoEvents();
        Check("carregou as faixas", expTable.Rows.Count > 0, $"({expTable.Rows.Count} faixas; planos {string.Join("/", expPlanRates)})");
        gridExp.Rows[1].Cells["rate"].Value = 90; Application.DoEvents();   // como o usuário: pela célula da grade
        var r = expTable.Rows[1];
        Check("mudar o % recalcula a EXP dos planos", (string)r["p0"] == $"{expPlanRates[0] * 90 / 100.0:0.#}x" && expDirty, $"(Free {r["p0"]}, Vipzão {r["p3"]})");
        using (var bmp = new Bitmap(Width, Height)) { DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height)); bmp.Save(png); }
        sb.AppendLine($"foto: {png}");
        Hide(); LoadDynamicExp();
        Check("nada foi salvo", !expDirty);
        sb.AppendLine($"resultado: {fails} falha(s)");
        return sb.ToString();
    }

    void SaveDynamicExp()
    {
        var bands = ExpBandsFromGrid();
        var err = DynamicExp.Validate(bands);
        if (err.Count > 0) { foreach (var e in err) Log("EXP dinâmica: " + e); MessageBox.Show(string.Join("\n", err), "Mu Chila Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var lista = string.Join("\n", bands.OrderBy(b => b.LevelMin).Select(b => $"níveis {b.LevelMin}–{b.LevelMax}: {b.Rate}%  (Free {expPlanRates[0] * b.Rate / 100.0:0.#}x, Vipzão {expPlanRates[3] * b.Rate / 100.0:0.#}x)"));
        if (!Confirm($"Gravar a EXP dinâmica e aplicar nos servidores?\n\n{lista}")) return;
        Log(DynamicExp.Save(bands));
        LogAll(ServerControl.Reload("Util (GMs, avisos)"));
        LoadDynamicExp();
    }
}

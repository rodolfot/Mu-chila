using System.Data;
using System.IO;

namespace MuChilaAdmin;

/// <summary>
/// Aba Drops → "Por monstro / por item" (pedido do dono, 28/09/2026: a grade do arquivo era confusa). Escolhe um monstro e vê
/// tudo o que ele dropa (inclusive as regras que valem para vários monstros: por mapa, por faixa de nível ou para qualquer
/// monstro), com a chance em % e em "1 em N", mais o drop comum e o zen dele; ou escolhe um item e vê de onde ele cai.
/// Edita as MESMAS linhas das abas avançadas (dropTable/rateTable), então salvar em qualquer uma grava tudo sem duplicar.
/// </summary>
public sealed partial class MainForm
{
    readonly RadioButton rbDvMonster = new() { Text = "Por monstro", Checked = true, AutoSize = true, Padding = new Padding(4, 4, 0, 0) };
    readonly RadioButton rbDvItem = new() { Text = "Por item", AutoSize = true, Padding = new Padding(4, 4, 0, 0) };
    readonly TextBox txtDvFind = new() { Width = 220, PlaceholderText = "buscar..." };
    readonly DataGridView gridDvList = Grid(), gridDvRules = Grid();
    readonly Label lblDvTitle = new() { Dock = DockStyle.Top, Height = 46, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(4, 6, 4, 0) };
    readonly NumericUpDown numDvItemRate = new() { Maximum = 1_000_000, Width = 80 }, numDvMoneyRate = new() { Maximum = 1_000_000, Width = 80 },
                           numDvMaxLevel = new() { Maximum = 15, Width = 50 };
    readonly FlowLayoutPanel pnlDvRates = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(2), WrapContents = true };
    readonly Label lblDvStatus = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    readonly ItemPicture picDv = new();
    readonly Button btnDvAdd = new() { AutoSize = true }, btnDvOther = new() { AutoSize = true, Text = "Outro item (que ainda não cai)...", Visible = false };
    Dictionary<int, HashSet<int>> dvMonsterMaps = new();
    List<MonsterRate> dvMonsters = new();
    bool dvLoading;

    TabPage DropsSimplePage()
    {
        var page = new TabPage("Por monstro / por item");
        rbDvMonster.CheckedChanged += (_, _) => { if (rbDvMonster.Checked) Safe(DvShowList); };
        rbDvItem.CheckedChanged += (_, _) => { if (rbDvItem.Checked) Safe(DvShowList); };
        txtDvFind.TextChanged += (_, _) => Safe(DvFilter);
        gridDvList.SelectionChanged += (_, _) => Safe(DvShowSelected, quiet: true);
        gridDvRules.SelectionChanged += (_, _) => Safe(() =>
        {
            if (DvCurrentRule() is { } r && (r["Código"] as string ?? "").Split(',') is [var s, var t]) picDv.Show(int.Parse(s), int.Parse(t), r["Item"] as string);
            else picDv.Show(null, null);
        }, quiet: true);
        gridDvRules.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Safe(DvChangeChance); };
        foreach (var n in new[] { numDvItemRate, numDvMoneyRate, numDvMaxLevel }) n.ValueChanged += (_, _) => Safe(DvRateChanged);
        btnDvAdd.Click += (_, _) => Safe(() => DvAdd(false));
        btnDvOther.Click += (_, _) => Safe(() => DvAdd(true));

        var bar = Bar(rbDvMonster, rbDvItem, txtDvFind,
            Btn("Salvar e aplicar", DvSave),
            Btn("Descartar", () => { if ((!dropDirty && !RatesChanged()) || Confirm("Descartar as alterações nos drops?")) { LoadDrops(); LoadRates(); DvLoad(); } }),
            lblDvStatus);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 44, Padding = new Padding(6),
            Text = "Escolha um monstro para ver tudo o que ele dropa, ou um item para ver de onde ele cai. Chance = por monstro morto (1% = 1 em 100). " +
                   "Duplo clique numa linha muda a chance. Regras que valem para vários monstros avisam antes de mudar. \"Salvar e aplicar\" grava e recarrega nos servidores.",
        };

        pnlDvRates.Controls.AddRange(new Control[]
        {
            new Label { Text = "Drop comum deste monstro:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, numDvItemRate,
            new Label { Text = "Zen:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, numDvMoneyRate,
            new Label { Text = "Item comum até +", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, numDvMaxLevel,
            new Label { Text = "(números do kit: maior = mais vezes)", AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(8, 6, 0, 0) },
        });
        var ruleBar = Bar(btnDvAdd, btnDvOther, Btn("Mudar chance...", DvChangeChance), Btn("Remover", DvRemove));
        var direita = new Panel { Dock = DockStyle.Fill };
        direita.Controls.Add(gridDvRules); direita.Controls.Add(picDv); direita.Controls.Add(ruleBar); direita.Controls.Add(pnlDvRates); direita.Controls.Add(lblDvTitle);

        page.Controls.Add(Split(Titled("Escolha", gridDvList), direita, 0.33));
        page.Controls.Add(help);
        page.Controls.Add(bar);
        return page;
    }

    // ---------------- dados ----------------

    /// <summary>Lê onde cada monstro nasce (arquivos de spawn) e monta a lista. Chamar depois de LoadDrops e LoadRates.</summary>
    void DvLoad()
    {
        dvMonsters = Drops.MonsterRates();
        dvMonsterMaps = new();
        foreach (var m in Monsters.Maps())
            foreach (var s in Monsters.Load(m.File))
            {
                if (!dvMonsterMaps.TryGetValue(s.Class, out var set)) dvMonsterMaps[s.Class] = set = new();
                set.Add(m.Code);
            }
        DvShowList();
    }

    IEnumerable<DataRow> RuleRows => dropTable.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted && r.RowState != DataRowState.Detached);
    static string Cell(DataRow r, string c) => (r[c] as string ?? "").Trim();

    bool Applies(DataRow r, MonsterRate m)
    {
        string mon = Cell(r, "Monstro"), map = Cell(r, "Mapa"), lmin = Cell(r, "Nível mín"), lmax = Cell(r, "Nível máx");
        if (mon != "*" && (!int.TryParse(mon, out var c) || c != m.Index)) return false;
        if (map != "*" && (!int.TryParse(map, out var mp) || !dvMonsterMaps.TryGetValue(m.Index, out var maps) || !maps.Contains(mp))) return false;
        if (lmin != "*" && int.TryParse(lmin, out var a) && m.Level < a) return false;
        if (lmax != "*" && int.TryParse(lmax, out var b) && m.Level > b) return false;
        return true;
    }

    int Reach(DataRow r) => dvMonsters.Count(m => Applies(r, m));

    /// <summary>Para quem a regra vale, em português ("só Blade Hunter", "monstros de Kalima de nível 12–150", "qualquer monstro").</summary>
    string Scope(DataRow r)
    {
        string mon = Cell(r, "Monstro"), map = Cell(r, "Mapa"), lmin = Cell(r, "Nível mín"), lmax = Cell(r, "Nível máx");
        var mapTxt = map == "*" ? "" : $" em {MapName(map)}";
        if (mon != "*") return $"só {MonName(mon)}{mapTxt}";
        var lvl = (lmin, lmax) switch
        {
            ("*", "*") => "",
            (_, "*") => $" de nível {lmin} ou mais",
            ("*", _) => $" até o nível {lmax}",
            _ => $" de nível {lmin}–{lmax}",
        };
        return map == "*" && lvl == "" ? "qualquer monstro" : $"monstros{mapTxt}{lvl}";
    }

    static int RateOf(DataRow r) { try { return Drops.ParsePercent(Cell(r, "Chance %")); } catch (FormatException) { return -1; } }
    static string OneIn(int rate) => rate <= 0 ? "nunca" : $"1 em {Math.Max(1, Math.Round((double)Drops.RateBase / rate)):N0}";
    static string Pct(int rate) => rate < 0 ? "?" : Drops.Percent(rate).Replace('.', ',') + "%";

    // ---------------- lista da esquerda ----------------

    void DvShowList()
    {
        if (dvMonsters.Count == 0 && dropTable.Columns.Count == 0) return;
        var t = new DataTable();
        btnDvOther.Visible = rbDvItem.Checked;
        if (rbDvMonster.Checked)
        {
            btnDvAdd.Text = "Adicionar item a este monstro...";
            t.Columns.Add("Monstro"); t.Columns.Add("Nível", typeof(int)); t.Columns.Add("Onde nasce"); t.Columns.Add("Itens", typeof(int)); t.Columns.Add("_id", typeof(int));
            var rules = RuleRows.ToList();
            // primeiro os que nascem em mapas (por nível); os de evento/invasão (sem spawn) ficam no fim
            foreach (var m in dvMonsters.OrderBy(m => dvMonsterMaps.ContainsKey(m.Index) ? 0 : 1).ThenBy(m => m.Level).ThenBy(m => m.Name))
            {
                var onde = dvMonsterMaps.TryGetValue(m.Index, out var maps) ? string.Join(", ", maps.OrderBy(x => x).Select(x => MapName(x.ToString())).Distinct().Take(3)) + (maps.Count > 3 ? "..." : "") : "(evento/invasão)";
                t.Rows.Add(m.Name, m.Level, onde, rules.Count(r => Applies(r, m)), m.Index);
            }
        }
        else
        {
            btnDvAdd.Text = "Fazer este item cair em...";
            t.Columns.Add("Item"); t.Columns.Add("Código"); t.Columns.Add("Regras", typeof(int));
            foreach (var g in RuleRows.GroupBy(r => Cell(r, "Código")).OrderBy(g => g.First()["Item"] as string))
                t.Rows.Add(g.First()["Item"], g.Key, g.Count());
        }
        dvLoading = true;
        gridDvList.DataSource = t;
        if (gridDvList.Columns["_id"] is { } id) id.Visible = false;
        if (gridDvList.Columns["Onde nasce"] is { } onde2) onde2.FillWeight = 150;
        dvLoading = false;
        DvFilter();
        DvStatus();
    }

    void DvFilter()
    {
        if (gridDvList.DataSource is not DataTable t) return;
        var f = LikeEsc(txtDvFind.Text.Trim());
        t.DefaultView.RowFilter = f.Length == 0 ? "" : rbDvMonster.Checked ? $"[Monstro] LIKE '%{f}%' OR [Onde nasce] LIKE '%{f}%'" : $"[Item] LIKE '%{f}%' OR [Código] LIKE '%{f}%'";
    }

    bool RatesChanged() => rateTable.GetChanges(DataRowState.Modified) != null;
    void DvStatus() => lblDvStatus.Text = (dropDirty || RatesChanged()) ? "   (alterações não salvas)" : "";

    MonsterRate? DvCurrentMonster() =>
        rbDvMonster.Checked && gridDvList.CurrentRow?.DataBoundItem is DataRowView v && v["_id"] is int id ? dvMonsters.FirstOrDefault(m => m.Index == id) : null;

    (int Section, int Type, string Name)? DvCurrentItem() =>
        rbDvItem.Checked && gridDvList.CurrentRow?.DataBoundItem is DataRowView v && (v["Código"] as string ?? "").Split(',') is [var s, var t]
            ? (int.Parse(s), int.Parse(t), v["Item"] as string ?? "") : null;

    DataRow? DvCurrentRule() => gridDvRules.CurrentRow?.DataBoundItem is DataRowView v && v["_regra"] is DataRow r && r.RowState != DataRowState.Detached ? r : null;

    // ---------------- lado direito ----------------

    void DvShowSelected()
    {
        if (dvLoading) return;
        var t = new DataTable();
        t.Columns.Add("Item"); t.Columns.Add("Chance"); t.Columns.Add("1 em"); t.Columns.Add("Vale para"); t.Columns.Add("Alcance"); t.Columns.Add("_regra", typeof(object));
        if (DvCurrentMonster() is { } m)
        {
            var onde = dvMonsterMaps.TryGetValue(m.Index, out var maps) ? string.Join(", ", maps.OrderBy(x => x).Select(x => MapName(x.ToString())).Distinct()) : "não nasce em nenhum mapa (evento/invasão)";
            lblDvTitle.Text = $"{m.Name} (nível {m.Level})\n{onde}";
            pnlDvRates.Visible = true;
            dvLoading = true;
            var rr = RateRow(m.Index);
            numDvItemRate.Value = Math.Clamp(Convert.ToDecimal(rr?["Drop comum"] ?? m.ItemRate), 0, numDvItemRate.Maximum);
            numDvMoneyRate.Value = Math.Clamp(Convert.ToDecimal(rr?["Zen"] ?? m.MoneyRate), 0, numDvMoneyRate.Maximum);
            numDvMaxLevel.Value = Math.Clamp(Convert.ToDecimal(rr?["Nível máx. do item"] ?? m.MaxItemLevel), 0, 15);
            dvLoading = false;
            foreach (var r in RuleRows.Where(r => Applies(r, m)).OrderByDescending(RateOf))
            {
                int reach = Reach(r);
                t.Rows.Add(r["Item"], Pct(RateOf(r)), OneIn(RateOf(r)), Scope(r), reach <= 1 ? "só este" : $"{reach} monstros", r);
            }
        }
        else if (DvCurrentItem() is { } it)
        {
            lblDvTitle.Text = $"{it.Name}  [{it.Section},{it.Type}]\nOnde cai:";
            pnlDvRates.Visible = false;
            picDv.Show(it.Section, it.Type, it.Name);
            foreach (var r in RuleRows.Where(r => Cell(r, "Código") == $"{it.Section},{it.Type}").OrderByDescending(RateOf))
            {
                int reach = Reach(r);
                t.Rows.Add(r["Item"], Pct(RateOf(r)), OneIn(RateOf(r)), Scope(r), reach == 1 ? "1 monstro" : $"{reach} monstros", r);
            }
        }
        else { lblDvTitle.Text = ""; pnlDvRates.Visible = false; }
        gridDvRules.DataSource = t;
        gridDvRules.Columns["_regra"]!.Visible = false;
        gridDvRules.Columns["Item"]!.FillWeight = 150; gridDvRules.Columns["Vale para"]!.FillWeight = 170;
        gridDvRules.Columns["Chance"]!.FillWeight = 55; gridDvRules.Columns["1 em"]!.FillWeight = 70; gridDvRules.Columns["Alcance"]!.FillWeight = 70;
    }

    DataRow? RateRow(int monster) => rateTable.Rows.Cast<DataRow>().FirstOrDefault(r => (int)r["Índice"] == monster);

    void DvRateChanged()
    {
        if (dvLoading || DvCurrentMonster() is not { } m || RateRow(m.Index) is not { } rr) return;
        rr["Drop comum"] = (int)numDvItemRate.Value; rr["Zen"] = (int)numDvMoneyRate.Value; rr["Nível máx. do item"] = (int)numDvMaxLevel.Value;
        rateDirty = true; UpdateRateStatus(); DvStatus();
    }

    // ---------------- ações ----------------

    /// <summary>Pergunta a chance em %, mostrando "1 em N" ao lado. Devolve a chance em 1.000.000 ou null.</summary>
    int? AskChance(string title, int current)
    {
        using var dlg = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(400, 110), Font = new Font("Segoe UI", 9),
        };
        var num = new NumericUpDown { Left = 110, Top = 14, Width = 110, DecimalPlaces = 4, Minimum = 0.0001m, Maximum = 100, Increment = 0.1m,
                                      Value = Math.Clamp((decimal)current * 100 / Drops.RateBase, 0.0001m, 100) };
        var equiv = new Label { Left = 230, Top = 17, AutoSize = true, ForeColor = SystemColors.GrayText };
        void Upd() => equiv.Text = "= " + OneIn((int)Math.Round(num.Value * Drops.RateBase / 100)) + " monstros";
        num.ValueChanged += (_, _) => Upd(); Upd();
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 232, Top = 72, Width = 75 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 313, Top = 72, Width = 75 };
        dlg.Controls.AddRange(new Control[] { new Label { Text = "Chance (%):", Left = 12, Top = 17, AutoSize = true }, num, equiv, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        return dlg.ShowDialog(this) == DialogResult.OK ? (int)Math.Round(num.Value * Drops.RateBase / 100) : null;
    }

    /// <summary>Linha nova na tabela de regras (a mesma da aba avançada).</summary>
    DataRow AddRuleRow(DropRule r, string itemName)
    {
        var row = dropTable.NewRow();
        dropLoading = true;
        row["Item"] = itemName; row["Código"] = $"{r.Section},{r.Type}"; row["Nível"] = r.Level; row["Grade"] = r.Grade;
        for (int i = 0; i < DropOptCols.Length; i++) row[DropOptCols[i]] = r.Options[i];
        row["Duração"] = r.Duration; row["Mapa"] = r.Map; row["Nome do mapa"] = MapName(r.Map); row["Monstro"] = r.Monster; row["Nome do monstro"] = MonName(r.Monster);
        row["Nível mín"] = r.LevelMin; row["Nível máx"] = r.LevelMax; row["Chance %"] = Drops.Percent(r.Rate).Replace('.', ','); row["Comentário"] = r.Comment;
        dropTable.Rows.Add(row); dropRows[row] = r;
        dropLoading = false;
        dropDirty = true; UpdateDropStatus(); DvStatus();
        return row;
    }

    void DvAdd(bool otherItem)
    {
        if (!otherItem && DvCurrentMonster() is { } m)
        {
            var item = Pick($"Item que {m.Name} vai dropar", Shops.Catalog().OrderBy(d => d.Section).ThenBy(d => d.Type).ToList(),
                            d => $"{d.Name}   [{d.Section},{d.Type}]", d => (d.Section, d.Type, d.Name));
            if (item == null || AskChance($"{item.Name} em {m.Name}", 10_000) is not int rate) return;
            AddRuleRow(new DropRule { Item = item.Section * 512 + item.Type, Monster = m.Index.ToString(), Rate = rate, Comment = $"{item.Name} - Mu Chila" }, item.Name);
            Log($"{m.Name} passa a dropar {item.Name} ({Pct(rate)}, {OneIn(rate)}). Clique em \"Salvar e aplicar\".");
            DvShowList(); DvSelect(m.Index.ToString());
            return;
        }
        (int Section, int Type, string Name)? it = otherItem ? null : DvCurrentItem();
        if (it == null)
        {   // "Outro item" ou nenhum escolhido: escolhe no catálogo (inclusive item que ainda não cai de nada)
            var d = Pick("Item que vai passar a cair", Shops.Catalog().OrderBy(d => d.Section).ThenBy(d => d.Type).ToList(),
                         d => $"{d.Name}   [{d.Section},{d.Type}]", d => (d.Section, d.Type, d.Name));
            if (d == null) return;
            it = (d.Section, d.Type, d.Name);
        }
        var (sec, typ, name) = it.Value;
        var alvo = Pick($"{name}: de onde vai cair?", new List<Choice>
        {
            new("monstro", "De um monstro"), new("mapa", "De todos os monstros de um mapa"),
            new("nivel", "De monstros de uma faixa de nível"), new("todos", "De qualquer monstro"),
        }, c => c.Text);
        if (alvo == null) return;
        var rule = new DropRule { Item = sec * 512 + typ, Comment = $"{name} - Mu Chila" };
        switch (alvo.Value)
        {
            case "monstro":
                var mon = Pick("Escolha o monstro", dvMonsters.OrderBy(x => x.Level).ToList(), x => $"{x.Name} (nível {x.Level})");
                if (mon == null) return;
                rule.Monster = mon.Index.ToString();
                break;
            case "mapa":
                var map = Pick("Escolha o mapa", mapNames.OrderBy(k => k.Key).Select(k => new Choice(k.Key.ToString(), $"{k.Key:000} - {k.Value}")).ToList(), x => x.Text);
                if (map == null) return;
                rule.Map = map.Value;
                break;
            case "nivel":
                if (AskLevels() is not (int min, int max)) return;
                rule.LevelMin = min.ToString(); rule.LevelMax = max.ToString();
                break;
        }
        if (AskChance($"{name}: chance", alvo.Value == "todos" ? 100 : 10_000) is not int r2) return;
        rule.Rate = r2;
        var row = AddRuleRow(rule, name);
        Log($"{name} passa a cair: {Scope(row)} ({Pct(r2)}, {OneIn(r2)}; alcança {Reach(row)} monstro(s)). Clique em \"Salvar e aplicar\".");
        DvShowList(); DvSelect($"{sec},{typ}");
    }

    (int, int)? AskLevels()
    {
        using var dlg = new Form
        {
            Text = "Faixa de nível dos monstros", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(300, 100), Font = new Font("Segoe UI", 9),
        };
        var a = new NumericUpDown { Left = 60, Top = 14, Width = 70, Maximum = 1000, Value = 1 };
        var b = new NumericUpDown { Left = 180, Top = 14, Width = 70, Maximum = 1000, Value = 150 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 132, Top = 60, Width = 75 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 213, Top = 60, Width = 75 };
        dlg.Controls.AddRange(new Control[] { new Label { Text = "De", Left = 12, Top = 17, AutoSize = true }, a,
                                              new Label { Text = "até", Left = 145, Top = 17, AutoSize = true }, b, ok, cancel });
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        if (dlg.ShowDialog(this) != DialogResult.OK) return null;
        return a.Value <= b.Value ? ((int)a.Value, (int)b.Value) : ((int)b.Value, (int)a.Value);
    }

    /// <summary>Aviso quando a regra vale para outros monstros além do escolhido.</summary>
    bool ConfirmShared(DataRow r, string what)
    {
        int reach = Reach(r);
        if (reach <= 1) return true;
        return Confirm($"Esta regra de {r["Item"]} vale para {reach} monstros ({Scope(r)}), não só para este.\n\n{what} para todos eles?\n\n" +
                       "(Para mexer só em um monstro, adicione uma regra própria nele.)");
    }

    void DvChangeChance()
    {
        if (DvCurrentRule() is not { } r) { Log("Escolha uma linha à direita."); return; }
        int old = RateOf(r);
        if (AskChance($"{r["Item"]}: {Scope(r)}", Math.Max(old, 1)) is not int rate || rate == old) return;
        if (!ConfirmShared(r, $"Mudar a chance de {Pct(old)} para {Pct(rate)}")) return;
        dropLoading = true;
        r["Chance %"] = Drops.Percent(rate).Replace('.', ',');
        dropLoading = false;
        dropDirty = true; UpdateDropStatus(); DvStatus();
        Log($"{r["Item"]} ({Scope(r)}): chance {Pct(old)} → {Pct(rate)} ({OneIn(rate)}). Clique em \"Salvar e aplicar\".");
        DvShowSelected();
    }

    void DvRemove()
    {
        if (DvCurrentRule() is not { } r) { Log("Escolha uma linha à direita."); return; }
        if (!ConfirmShared(r, "Tirar o drop") || Reach(r) <= 1 && !Confirm($"Tirar {r["Item"]} ({Scope(r)})?")) return;
        var desc = $"{r["Item"]} ({Scope(r)})";
        dropRows.Remove(r); dropTable.Rows.Remove(r);
        dropDirty = true; UpdateDropStatus(); DvStatus();
        Log($"{desc}: removido. Clique em \"Salvar e aplicar\".");
        var sel = rbDvMonster.Checked ? (gridDvList.CurrentRow?.DataBoundItem as DataRowView)?["_id"]?.ToString() : (gridDvList.CurrentRow?.DataBoundItem as DataRowView)?["Código"] as string;
        DvShowList(); if (sel != null) DvSelect(sel);
    }

    /// <summary>Volta a selecionar o monstro (pelo índice) ou o item (pelo código) na lista da esquerda.</summary>
    void DvSelect(string key)
    {
        var col = rbDvMonster.Checked ? "_id" : "Código";
        foreach (DataGridViewRow gr in gridDvList.Rows)
            if (gr.DataBoundItem is DataRowView v && v[col]?.ToString() == key)
            {
                gridDvList.CurrentCell = gr.Cells.Cast<DataGridViewCell>().First(c => c.Visible);
                break;
            }
        DvShowSelected();
    }

    /// <summary>
    /// Teste (--testar-drops-visao): carrega os drops de verdade, confere a visão por monstro e por item, simula adicionar,
    /// mudar a chance e remover SÓ NA MEMÓRIA (nada é salvo) e grava uma foto da aba em png.
    /// </summary>
    internal string TestDropsView(string png)
    {
        var sb = new System.Text.StringBuilder();
        int fails = 0;
        void Check(string name, bool ok, string extra = "") { if (!ok) fails++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {name} {extra}"); }
        LoadDrops(); LoadRates(); DvLoad();
        int total = RuleRows.Count();
        Check("carregou", dvMonsters.Count > 0 && total > 0, $"({dvMonsters.Count} monstros, {total} regras, {dvMonsterMaps.Count} monstros com mapa de spawn)");

        var ranking = dvMonsters.Select(m => (M: m, N: RuleRows.Count(r => Applies(r, m)))).OrderByDescending(x => x.N).ToList();
        var top = ranking[0].M;
        sb.AppendLine($"      monstro com mais drops: {top.Name} (nível {top.Level}), {ranking[0].N} regras:");
        foreach (var r in RuleRows.Where(r => Applies(r, top)).OrderByDescending(RateOf).Take(8))
            sb.AppendLine($"        {r["Item"]}: {Pct(RateOf(r))} ({OneIn(RateOf(r))}) - {Scope(r)} - {Reach(r)} monstro(s)");
        var global = RuleRows.FirstOrDefault(r => Cell(r, "Monstro") == "*" && Cell(r, "Mapa") == "*" && Cell(r, "Nível mín") == "*" && Cell(r, "Nível máx") == "*");
        if (global != null) Check("regra de qualquer monstro vale para todos", Reach(global) == dvMonsters.Count, $"({global["Item"]}: {Reach(global)} de {dvMonsters.Count})");
        var byLevel = RuleRows.FirstOrDefault(r => Cell(r, "Monstro") == "*" && Cell(r, "Mapa") == "*" && Cell(r, "Nível mín") != "*");
        if (byLevel != null && int.TryParse(Cell(byLevel, "Nível mín"), out var lmin) && int.TryParse(Cell(byLevel, "Nível máx"), out var lmax))
            Check("regra por faixa de nível", Reach(byLevel) == dvMonsters.Count(m => m.Level >= lmin && m.Level <= lmax), $"({byLevel["Item"]}, {Scope(byLevel)}: {Reach(byLevel)} monstros)");
        var single = RuleRows.FirstOrDefault(r => Cell(r, "Monstro") != "*");
        if (single != null) Check("regra de um monstro só", Reach(single) <= 1 && Scope(single).StartsWith("só "), $"({single["Item"]}: {Scope(single)})");

        // mostra o monstro na tela (seleciona na lista)
        rbDvMonster.Checked = true; DvShowList(); DvSelect(top.Index.ToString());
        Check("lado direito mostra as regras do monstro", gridDvRules.Rows.Count == ranking[0].N, $"({gridDvRules.Rows.Count} linhas)");

        // simulação só na memória
        var novo = AddRuleRow(new DropRule { Item = 14 * 512 + 13, Monster = top.Index.ToString(), Rate = 5000, Comment = "Jewel of Bless - teste" }, "Jewel of Bless");
        Check("adicionar: regra nova só para o monstro", Reach(novo) == 1 && RuleRows.Count() == total + 1 && dropDirty, $"({Scope(novo)}, {Pct(RateOf(novo))} = {OneIn(RateOf(novo))})");
        novo["Chance %"] = Drops.Percent(20000).Replace('.', ',');
        Check("mudar chance (2% = 1 em 50)", RateOf(novo) == 20000 && OneIn(20000) == "1 em 50");
        dropRows.Remove(novo); dropTable.Rows.Remove(novo);
        Check("remover", RuleRows.Count() == total);

        // foto da aba
        Width = 1400; Height = 900; StartPosition = FormStartPosition.Manual; Left = -3000; Top = 0; ShowInTaskbar = false;
        Show();
        Application.DoEvents();   // deixa o carregamento da abertura da janela terminar antes de escolher
        var tabs = Controls.OfType<TabControl>().First();
        tabs.SelectedTab = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Drops");
        rbDvMonster.Checked = true; DvShowList(); DvSelect(top.Index.ToString());
        Application.DoEvents();
        void Foto(string f) { using var bmp = new Bitmap(Width, Height); DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height)); bmp.Save(f); sb.AppendLine($"foto: {f}"); }
        Foto(png);
        rbDvItem.Checked = true; DvSelect("12,15");   // Jewel of Chaos
        Application.DoEvents();
        Foto(Path.ChangeExtension(png, null) + "-item.png");
        Hide();
        LoadDrops(); LoadRates();   // descarta a simulação
        Check("nada foi salvo", !dropDirty);
        sb.AppendLine($"resultado: {fails} falha(s)");
        return sb.ToString();
    }

    void DvSave()
    {
        if (!dropDirty && !RatesChanged()) { Log("Nada para salvar nos drops."); return; }
        var sel = rbDvMonster.Checked ? (gridDvList.CurrentRow?.DataBoundItem as DataRowView)?["_id"]?.ToString() : (gridDvList.CurrentRow?.DataBoundItem as DataRowView)?["Código"] as string;
        if (dropDirty) SaveDrops();
        if (RatesChanged()) SaveRates();
        DvLoad();
        if (sel != null) DvSelect(sel);
    }
}

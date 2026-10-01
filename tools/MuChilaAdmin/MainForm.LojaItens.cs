using System.Data;
using System.IO;

namespace MuChilaAdmin;

/// <summary>
/// Aba "Loja de itens" (30/09/2026): catálogo e preços da loja de itens do site (LojaItens.cs → muchila.lojaitens.json).
/// Subabas: Catálogo (categorias e itens, com a foto do item), Preços (tabelas das opções, com exemplo calculado) e
/// Vendas (últimas compras do banco). "Salvar" grava o arquivo; o site usa na hora, sem reiniciar nada.
/// </summary>
public sealed partial class MainForm
{
    LojaItens.Config lojaCfg = new();
    bool lojaDirty, lojaFilling;
    readonly CheckBox chkLojaAtiva = new() { Text = "Loja aberta no site", AutoSize = true, Padding = new Padding(0, 5, 10, 0) };
    readonly NumericUpDown numLojaNivelMax = new() { Minimum = 0, Maximum = 15, Width = 50 };
    readonly NumericUpDown numLojaExcMax = new() { Minimum = 0, Maximum = 6, Width = 50 };
    readonly Label lblLoja = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    readonly ListBox lstLojaCats = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly TextBox txtLojaCatNome = new() { Dock = DockStyle.Fill };
    readonly TextBox txtLojaCatId = new() { Dock = DockStyle.Fill };
    readonly ComboBox cmbLojaCatGrupo = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    readonly ComboBox cmbLojaCatIcone = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox chkLojaCatAtiva = new() { Text = "Categoria visível no site", AutoSize = true };
    readonly DataGridView gridLojaItens = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, BackgroundColor = SystemColors.Window,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EditMode = DataGridViewEditMode.EditOnEnter,
    };
    readonly ItemPicture picLoja = new(150);
    readonly DataGridView gridLojaNivel = PriceGrid(), gridLojaAdicional = PriceGrid(), gridLojaExc = PriceGrid();
    readonly NumericUpDown numLojaSorte = new() { Maximum = 100000, Width = 80 }, numLojaSkill = new() { Maximum = 100000, Width = 80 };
    readonly Label lblLojaExemplo = new() { Dock = DockStyle.Fill, Padding = new Padding(6), Font = new Font("Segoe UI", 10) };
    readonly DataGridView gridLojaVendas = Grid();
    readonly Label lblLojaVendas = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    const string ExcAuto = "automático";

    static DataGridView PriceGrid() => new()
    {
        Dock = DockStyle.Top, Height = 54, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window, ScrollBars = ScrollBars.None,
        SelectionMode = DataGridViewSelectionMode.CellSelect, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
    };

    TabPage LojaItensTab()
    {
        var page = new TabPage("Loja de itens");
        var inner = new TabControl { Dock = DockStyle.Fill };
        inner.TabPages.Add(LojaCatalogoPage());
        inner.TabPages.Add(LojaPrecosPage());
        var vendas = LojaVendasPage();
        inner.TabPages.Add(vendas);
        inner.SelectedIndexChanged += (_, _) => { if (inner.SelectedTab == vendas) Safe(LoadLojaVendas); };

        chkLojaAtiva.CheckedChanged += (_, _) => LojaMudou(() => lojaCfg.Ativo = chkLojaAtiva.Checked);
        numLojaNivelMax.ValueChanged += (_, _) => LojaMudou(() => lojaCfg.NivelMax = (int)numLojaNivelMax.Value);
        numLojaExcMax.ValueChanged += (_, _) => LojaMudou(() => lojaCfg.ExcMax = (int)numLojaExcMax.Value);
        var bar = Bar(chkLojaAtiva, new Label { Text = "Nível máximo +", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, numLojaNivelMax,
            new Label { Text = "Excelentes no máximo", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }, numLojaExcMax,
            Btn("Salvar", SaveLoja),
            Btn("Recarregar do arquivo", () => { if (!lojaDirty || Confirm("Descartar as mudanças não salvas?")) LoadLoja(); }),
            lblLoja);
        var help = new Label
        {
            Dock = DockStyle.Top, Height = 48, Padding = new Padding(6),
            Text = "Loja de itens do site (Painel do jogador → Loja de itens): o jogador monta o item, paga com Cash (WCoinC) e recebe no baú, fora do jogo. " +
                   "Preço = preço do item + nível + adicional + sorte + skill + excelentes (aba Preços). Nível máx. vazio = o da loja; Excelentes \"automático\" = " +
                   "o site escolhe pela seção. \"Salvar\" grava o muchila.lojaitens.json e o site usa na hora.",
        };
        page.Controls.Add(inner); page.Controls.Add(help); page.Controls.Add(bar);
        Shown += (_, _) => Safe(LoadLoja);
        return page;
    }

    // ---------------- catálogo ----------------
    TabPage LojaCatalogoPage()
    {
        var page = new TabPage("Catálogo");
        cmbLojaCatGrupo.Items.AddRange(new object[] { "Defesa", "Ataque", "Especiais" });
        cmbLojaCatIcone.Items.AddRange(LojaItens.Icones);

        var props = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 150, ColumnCount = 2, Padding = new Padding(2) };
        props.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
        props.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Linha(string rotulo, Control c) { props.Controls.Add(new Label { Text = rotulo, AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); props.Controls.Add(c); }
        Linha("Nome", txtLojaCatNome); Linha("Id", txtLojaCatId); Linha("Grupo", cmbLojaCatGrupo); Linha("Ícone", cmbLojaCatIcone);
        props.Controls.Add(new Label()); props.Controls.Add(chkLojaCatAtiva);
        txtLojaCatNome.TextChanged += (_, _) => LojaCatMudou(c => c.Nome = txtLojaCatNome.Text);
        txtLojaCatId.TextChanged += (_, _) => LojaCatMudou(c => c.Id = txtLojaCatId.Text.Trim());
        cmbLojaCatGrupo.TextChanged += (_, _) => LojaCatMudou(c => c.Grupo = cmbLojaCatGrupo.Text.Trim());
        cmbLojaCatIcone.SelectedIndexChanged += (_, _) => LojaCatMudou(c => c.Icone = cmbLojaCatIcone.Text);
        chkLojaCatAtiva.CheckedChanged += (_, _) => LojaCatMudou(c => c.Ativo = chkLojaCatAtiva.Checked);
        lstLojaCats.SelectedIndexChanged += (_, _) => { if (!lojaFilling) Safe(FillLojaCategoria); };   // não refaz os campos enquanto o nome é digitado

        var catBar = Bar(
            Btn("Nova", () =>
            {
                int n = lojaCfg.Categorias.Count + 1;
                var c = new LojaItens.Categoria { Id = $"categoria{n}", Nome = $"Nova categoria {n}", Grupo = "Especiais", Icone = "item" };
                lojaCfg.Categorias.Add(c); lojaDirty = true; FillLojaCategorias(); lstLojaCats.SelectedItem = c; txtLojaCatNome.Focus(); UpdateLojaStatus();
            }),
            Btn("Remover", () =>
            {
                if (lstLojaCats.SelectedItem is not LojaItens.Categoria c) return;
                if (c.Itens.Count > 0 && !Confirm($"Remover a categoria \"{c.Nome}\" com {c.Itens.Count} item(ns)? (Só grava ao clicar em Salvar.)")) return;
                lojaCfg.Categorias.Remove(c); lojaDirty = true; FillLojaCategorias(); UpdateLojaStatus();
            }),
            Btn("▲", () => MoveLojaCategoria(-1)), Btn("▼", () => MoveLojaCategoria(1)));
        var esquerda = new Panel { Dock = DockStyle.Fill };
        esquerda.Controls.Add(lstLojaCats); esquerda.Controls.Add(catBar); esquerda.Controls.Add(props);

        gridLojaItens.Columns.Add(new DataGridViewTextBoxColumn { Name = "secao", HeaderText = "Seção", ReadOnly = true, FillWeight = 40 });
        gridLojaItens.Columns.Add(new DataGridViewTextBoxColumn { Name = "tipo", HeaderText = "Tipo", ReadOnly = true, FillWeight = 40 });
        gridLojaItens.Columns.Add(new DataGridViewTextBoxColumn { Name = "nome", HeaderText = "Item", ReadOnly = true, FillWeight = 160 });
        gridLojaItens.Columns.Add(new DataGridViewTextBoxColumn { Name = "preco", HeaderText = "Preço (Cash)", FillWeight = 70, ValueType = typeof(int) });
        gridLojaItens.Columns.Add(new DataGridViewTextBoxColumn { Name = "nivel", HeaderText = "Nível máx.", FillWeight = 60, ToolTipText = "Vazio = o nível máximo da loja (barra de cima)" });
        var exc = new DataGridViewComboBoxColumn { Name = "exc", HeaderText = "Excelentes", FillWeight = 80, FlatStyle = FlatStyle.Flat };
        exc.Items.AddRange(ExcAuto, "arma", "defesa", "nenhuma");
        gridLojaItens.Columns.Add(exc);
        gridLojaItens.Columns.Add(new DataGridViewCheckBoxColumn { Name = "destaque", HeaderText = "Destaque", FillWeight = 50 });
        gridLojaItens.Columns.Add(new DataGridViewCheckBoxColumn { Name = "ativo", HeaderText = "À venda", FillWeight = 50 });
        gridLojaItens.Columns["preco"]!.DefaultCellStyle.Font = new Font(gridLojaItens.Font, FontStyle.Bold);
        gridLojaItens.DataError += (_, e) => { Log("Valor inválido: use números inteiros."); e.Cancel = true; };
        gridLojaItens.CurrentCellDirtyStateChanged += (_, _) => { if (gridLojaItens.IsCurrentCellDirty && gridLojaItens.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell) gridLojaItens.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        gridLojaItens.CellValueChanged += (_, e) => Safe(() => LojaItemMudou(e.RowIndex));
        gridLojaItens.SelectionChanged += (_, _) =>
        {
            if (gridLojaItens.CurrentRow?.Tag is LojaItens.Item it) picLoja.Show(it.Secao, it.Tipo, it.Nome); else picLoja.Show(null, null);
        };

        var itensBar = Bar(
            Btn("Adicionar itens...", AddLojaItens),
            Btn("Remover selecionados", () =>
            {
                if (lstLojaCats.SelectedItem is not LojaItens.Categoria c) return;
                var sel = gridLojaItens.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<LojaItens.Item>().ToList();
                if (sel.Count == 0) { Log("Selecione os itens na lista."); return; }
                c.Itens.RemoveAll(sel.Contains); lojaDirty = true; FillLojaCategoria(); FillLojaCategorias(keepSelection: true); UpdateLojaStatus();
            }),
            Btn("Preço dos selecionados...", () =>
            {
                var sel = gridLojaItens.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<LojaItens.Item>().ToList();
                if (sel.Count == 0) { Log("Selecione os itens na lista."); return; }
                var v = AskNumber($"Preço base (Cash) para {sel.Count} item(ns):", sel[0].Preco);
                if (v == null) return;
                foreach (var it in sel) it.Preco = v.Value;
                lojaDirty = true; FillLojaCategoria(); UpdateLojaStatus();
            }));
        var direita = new Panel { Dock = DockStyle.Fill };
        direita.Controls.Add(gridLojaItens); direita.Controls.Add(picLoja); direita.Controls.Add(itensBar);
        page.Controls.Add(Split(Titled("Categorias", esquerda), Titled("Itens da categoria", direita), 0.27));
        return page;
    }

    void LoadLoja()
    {
        lojaCfg = LojaItens.Carregar();
        lojaFilling = true;
        try
        {
            chkLojaAtiva.Checked = lojaCfg.Ativo;
            numLojaNivelMax.Value = lojaCfg.NivelMax;
            numLojaExcMax.Value = lojaCfg.ExcMax;
            numLojaSorte.Value = lojaCfg.Sorte;
            numLojaSkill.Value = lojaCfg.Skill;
            FillPriceGrid(gridLojaNivel, lojaCfg.Nivel, i => $"+{i}");
            FillPriceGrid(gridLojaAdicional, lojaCfg.Adicional, i => $"+{i * 4}");
            FillPriceGrid(gridLojaExc, lojaCfg.Excelente, i => i == 0 ? "nenhuma" : $"{i}");
        }
        finally { lojaFilling = false; }
        FillLojaCategorias();
        lojaDirty = false; UpdateLojaStatus(); UpdateLojaExemplo();
    }

    void FillLojaCategorias(bool keepSelection = false)
    {
        var antes = lstLojaCats.SelectedItem;
        lojaFilling = true;
        try
        {
            lstLojaCats.Items.Clear();
            foreach (var c in lojaCfg.Categorias) lstLojaCats.Items.Add(c);
        }
        finally { lojaFilling = false; }
        if (keepSelection && antes != null && lstLojaCats.Items.Contains(antes)) lstLojaCats.SelectedItem = antes;
        else if (lstLojaCats.Items.Count > 0) lstLojaCats.SelectedIndex = 0;
        else FillLojaCategoria();
    }

    void FillLojaCategoria()
    {
        var c = lstLojaCats.SelectedItem as LojaItens.Categoria;
        lojaFilling = true;
        try
        {
            txtLojaCatNome.Text = c?.Nome ?? ""; txtLojaCatId.Text = c?.Id ?? ""; cmbLojaCatGrupo.Text = c?.Grupo ?? "";
            cmbLojaCatIcone.SelectedItem = c?.Icone; chkLojaCatAtiva.Checked = c?.Ativo ?? false;
            foreach (Control x in new Control[] { txtLojaCatNome, txtLojaCatId, cmbLojaCatGrupo, cmbLojaCatIcone, chkLojaCatAtiva }) x.Enabled = c != null;
            gridLojaItens.Rows.Clear();
            foreach (var it in c?.Itens ?? new())
            {
                int i = gridLojaItens.Rows.Add(it.Secao, it.Tipo, it.Nome, it.Preco, it.NivelMax?.ToString() ?? "", it.Exc == "" ? ExcAuto : it.Exc, it.Destaque, it.Ativo);
                gridLojaItens.Rows[i].Tag = it;
                if (!it.Ativo) gridLojaItens.Rows[i].DefaultCellStyle.ForeColor = SystemColors.GrayText;
            }
        }
        finally { lojaFilling = false; }
    }

    void LojaItemMudou(int row)
    {
        if (lojaFilling || row < 0 || gridLojaItens.Rows[row].Tag is not LojaItens.Item it) return;
        var r = gridLojaItens.Rows[row];
        it.Preco = r.Cells["preco"].Value is int p ? Math.Max(0, p) : it.Preco;
        var nivel = Convert.ToString(r.Cells["nivel"].Value)?.Trim() ?? "";
        if (nivel == "") it.NivelMax = null;
        else if (int.TryParse(nivel, out var n) && n is >= 0 and <= 15) it.NivelMax = n;
        else Log($"{it.Nome}: nível máximo vai de 0 a 15 (vazio = o padrão da loja).");
        var exc = Convert.ToString(r.Cells["exc"].Value) ?? ExcAuto;
        it.Exc = exc == ExcAuto ? "" : exc;
        it.Destaque = r.Cells["destaque"].Value is true;
        it.Ativo = r.Cells["ativo"].Value is true;
        r.DefaultCellStyle.ForeColor = it.Ativo ? SystemColors.ControlText : SystemColors.GrayText;
        lojaDirty = true; UpdateLojaStatus();
    }

    void LojaCatMudou(Action<LojaItens.Categoria> mudar)
    {
        if (lojaFilling || lstLojaCats.SelectedItem is not LojaItens.Categoria c) return;
        mudar(c);
        lojaFilling = true;
        try { lstLojaCats.Items[lstLojaCats.SelectedIndex] = c; }   // atualiza o texto da lista
        finally { lojaFilling = false; }
        lojaDirty = true; UpdateLojaStatus();
    }

    void LojaMudou(Action mudar)
    {
        if (lojaFilling) return;
        mudar(); lojaDirty = true; UpdateLojaStatus(); UpdateLojaExemplo();
    }

    void MoveLojaCategoria(int passo)
    {
        int i = lstLojaCats.SelectedIndex, j = i + passo;
        if (i < 0 || j < 0 || j >= lojaCfg.Categorias.Count) return;
        (lojaCfg.Categorias[i], lojaCfg.Categorias[j]) = (lojaCfg.Categorias[j], lojaCfg.Categorias[i]);
        lojaDirty = true; FillLojaCategorias(); lstLojaCats.SelectedIndex = j; UpdateLojaStatus();
    }

    /// <summary>Janela para escolher itens do Item.txt (busca por nome, filtro por seção, foto) e o preço base deles.</summary>
    void AddLojaItens()
    {
        if (lstLojaCats.SelectedItem is not LojaItens.Categoria cat) { Log("Escolha (ou crie) uma categoria primeiro."); return; }
        var ja = cat.Itens.Select(i => (i.Secao, i.Tipo)).ToHashSet();
        using var f = new Form { Text = $"Adicionar itens em \"{cat.Nome}\"", Width = 760, Height = 560, StartPosition = FormStartPosition.CenterParent, Font = Font };
        var busca = new TextBox { Width = 260, PlaceholderText = "buscar pelo nome..." };
        var secao = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        string[] nomes = { "0 Espadas", "1 Machados", "2 Maças e cetros", "3 Lanças", "4 Arcos e bestas", "5 Cajados", "6 Escudos", "7 Elmos", "8 Armaduras",
                           "9 Calças", "10 Luvas", "11 Botas", "12 Asas e outros", "13 Anéis, pingentes e outros" };
        secao.Items.Add("todas as seções"); secao.Items.AddRange(nomes);
        int sugestao = cat.Itens.Count > 0 ? cat.Itens[0].Secao : -1;
        secao.SelectedIndex = sugestao is >= 0 and <= 13 ? sugestao + 1 : 0;
        var preco = new NumericUpDown { Maximum = 100000, Value = cat.Itens.Count > 0 ? cat.Itens[0].Preco : 50, Width = 80 };
        var lista = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false, Font = new Font("Consolas", 9) };
        var foto = new ItemPicture(150);
        var ok = new Button { Text = "Adicionar", DialogResult = DialogResult.OK, AutoSize = true };
        var catalogo = Shops.Catalog().Where(d => d.Section <= 13).OrderBy(d => d.Section).ThenBy(d => d.Type).ToList();
        void Filtrar()
        {
            var q = busca.Text.Trim();
            lista.BeginUpdate(); lista.Items.Clear();
            foreach (var d in catalogo)
            {
                if (secao.SelectedIndex > 0 && d.Section != secao.SelectedIndex - 1) continue;
                if (q.Length > 0 && !d.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                lista.Items.Add(new ItemEscolha(d, ja.Contains((d.Section, d.Type))));
            }
            lista.EndUpdate();
        }
        busca.TextChanged += (_, _) => Filtrar();
        secao.SelectedIndexChanged += (_, _) => Filtrar();
        lista.SelectedIndexChanged += (_, _) => { if (lista.SelectedItem is ItemEscolha e) foto.Show(e.Def.Section, e.Def.Type, e.Def.Name); };
        var topo = Bar(new Label { Text = "Seção", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, secao, busca,
            new Label { Text = "Preço base (Cash)", AutoSize = true, Padding = new Padding(10, 5, 0, 0) }, preco, ok);
        var dica = new Label { Dock = DockStyle.Bottom, Height = 22, Text = "Ctrl/Shift + clique escolhem vários. Itens com * já estão nesta categoria.", Padding = new Padding(4) };
        f.Controls.Add(lista); f.Controls.Add(foto); f.Controls.Add(dica); f.Controls.Add(topo);
        f.AcceptButton = ok;
        Filtrar();
        if (f.ShowDialog(this) != DialogResult.OK) return;
        int add = 0;
        foreach (var e in lista.SelectedItems.OfType<ItemEscolha>())
        {
            if (ja.Contains((e.Def.Section, e.Def.Type))) continue;
            cat.Itens.Add(new LojaItens.Item { Secao = e.Def.Section, Tipo = e.Def.Type, Preco = (int)preco.Value, NivelMax = e.Def.Section == 13 ? 4 : null });
            add++;
        }
        if (add == 0) { Log("Nenhum item novo escolhido."); return; }
        lojaDirty = true; FillLojaCategoria(); FillLojaCategorias(keepSelection: true); UpdateLojaStatus();
        Log($"Loja de itens: {add} item(ns) adicionado(s) em \"{cat.Nome}\" (falta Salvar).");
    }

    sealed record ItemEscolha(ItemDef Def, bool JaTem)
    {
        public override string ToString() => $"{(JaTem ? "*" : " ")} {Def.Section,2},{Def.Type,-4} {Def.Name}  ({Def.Width}x{Def.Height})";
    }

    int? AskNumber(string texto, int valor)
    {
        using var f = new Form { Text = "Mu Chila Admin", Width = 360, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, Font = Font };
        var num = new NumericUpDown { Maximum = 100000, Value = Math.Clamp(valor, 0, 100000), Left = 14, Top = 40, Width = 100 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 240, Top = 70, Width = 80 };
        f.Controls.Add(new Label { Text = texto, Left = 14, Top = 14, AutoSize = true }); f.Controls.Add(num); f.Controls.Add(ok);
        f.AcceptButton = ok;
        return f.ShowDialog(this) == DialogResult.OK ? (int)num.Value : null;
    }

    // ---------------- preços ----------------
    TabPage LojaPrecosPage()
    {
        var page = new TabPage("Preços");
        foreach (var g in new[] { gridLojaNivel, gridLojaAdicional, gridLojaExc })
        {
            g.DataError += (_, e) => { Log("Valor inválido: use números inteiros."); e.Cancel = true; };
            g.CellValueChanged += (_, e) => Safe(() => LojaMudou(() => ReadPriceGrids()));
        }
        numLojaSorte.ValueChanged += (_, _) => LojaMudou(() => lojaCfg.Sorte = (int)numLojaSorte.Value);
        numLojaSkill.ValueChanged += (_, _) => LojaMudou(() => lojaCfg.Skill = (int)numLojaSkill.Value);
        var corpo = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(6) };
        Control Titulo(string t) => new Label { Text = t, Dock = DockStyle.Top, Height = 26, Padding = new Padding(0, 8, 0, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        var extras = Bar(new Label { Text = "Sorte", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, numLojaSorte,
            new Label { Text = "Skill", AutoSize = true, Padding = new Padding(16, 5, 0, 0) }, numLojaSkill,
            new Label { Text = "Cash (somados quando o jogador liga a opção)", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        // Dock = Top empilha de baixo para cima: a ordem de inclusão é a inversa da tela
        foreach (var c in new Control[] { lblLojaExemplo, extras, Titulo("Sorte e skill"), gridLojaExc, Titulo("Excelentes: preço pela QUANTIDADE de opções escolhidas (0 a 6)"),
                                          gridLojaAdicional, Titulo("Adicional (Cash por opção; anéis e pingentes: +1% a +7% de vida)"), gridLojaNivel, Titulo("Nível do item (Cash para cada nível; não soma os anteriores)") })
            corpo.Controls.Add(c);
        lblLojaExemplo.Dock = DockStyle.Top; lblLojaExemplo.Height = 90;
        page.Controls.Add(corpo);
        return page;
    }

    void FillPriceGrid(DataGridView g, int[] valores, Func<int, string> titulo)
    {
        g.Columns.Clear(); g.Rows.Clear();
        for (int i = 0; i < valores.Length; i++) g.Columns.Add(new DataGridViewTextBoxColumn { Name = "c" + i, HeaderText = titulo(i), ValueType = typeof(int), SortMode = DataGridViewColumnSortMode.NotSortable });
        g.Rows.Add(valores.Cast<object>().ToArray());
    }

    void ReadPriceGrids()
    {
        int[] Ler(DataGridView g) => g.Rows.Count == 0 ? Array.Empty<int>() : g.Rows[0].Cells.Cast<DataGridViewCell>().Select(c => c.Value is int v ? Math.Max(0, v) : 0).ToArray();
        lojaCfg.Nivel = Ler(gridLojaNivel); lojaCfg.Adicional = Ler(gridLojaAdicional); lojaCfg.Excelente = Ler(gridLojaExc);
    }

    void UpdateLojaExemplo()
    {
        if (lojaCfg.Nivel.Length != 16 || lojaCfg.Adicional.Length != 8 || lojaCfg.Excelente.Length != 7) return;
        int nMax = lojaCfg.NivelMax, eMax = lojaCfg.ExcMax;
        string Ex(int b, int n, int a, bool so, bool sk, int e) =>
            $"{lojaCfg.Preco(b, Math.Min(n, nMax), a, so, sk, Math.Min(e, eMax)):N0} Cash".Replace(',', '.');
        lblLojaExemplo.Text = "Exemplos com um item de preço base 40 (como o Dragon Helm):" + Environment.NewLine +
            $"  sem nada: {Ex(40, 0, 0, false, false, 0)}     +9 +16 com sorte e 2 excelentes: {Ex(40, 9, 4, true, false, 2)}" + Environment.NewLine +
            $"  +13 +24 com sorte e 4 excelentes: {Ex(40, 13, 6, true, false, 4)}     completo (+{nMax} +28, sorte, skill, {eMax} excelentes): {Ex(40, 15, 7, true, true, 6)}";
    }

    // ---------------- vendas ----------------
    TabPage LojaVendasPage()
    {
        var page = new TabPage("Vendas");
        page.Controls.Add(gridLojaVendas);
        page.Controls.Add(Bar(Btn("Atualizar", LoadLojaVendas), lblLojaVendas));
        return page;
    }

    void LoadLojaVendas()
    {
        gridLojaVendas.DataSource = LojaItens.Compras();
        var (dia, semana, mes) = LojaItens.Totais();
        lblLojaVendas.Text = $"Cash gasto na loja de itens: {dia:N0} nas últimas 24 h · {semana:N0} em 7 dias · {mes:N0} em 30 dias".Replace(',', '.');
    }

    void UpdateLojaStatus()
    {
        int itens = lojaCfg.Categorias.Where(c => c.Ativo).Sum(c => c.Itens.Count(i => i.Ativo));
        lblLoja.Text = $"   {(lojaCfg.Ativo ? "aberta" : "FECHADA")} · {lojaCfg.Categorias.Count} categorias · {itens} itens à venda" + (lojaDirty ? "   (não salvo)" : "");
        lblLoja.ForeColor = lojaDirty ? Color.DarkOrange : SystemColors.ControlText;
    }

    void SaveLoja()
    {
        gridLojaItens.EndEdit(); ReadPriceGrids();
        var erros = LojaItens.Validar(lojaCfg);
        if (erros.Count > 0) { foreach (var e in erros) Log("Loja de itens: " + e); MessageBox.Show(string.Join("\n", erros.Take(15)), "Mu Chila Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!Confirm($"Gravar a loja de itens? O site passa a usar na hora.\n\n{lblLoja.Text.Trim()}\n{lblLojaExemplo.Text}")) return;
        Log(LojaItens.Salvar(lojaCfg));
        LoadLoja();
    }

    /// <summary>Teste (--testar-lojaitens-visao): abre a aba com o arquivo real, simula edições SÓ NA TELA, tira fotos e não salva.</summary>
    internal string TestLojaItensView(string png)
    {
        var sb = new System.Text.StringBuilder(); int fails = 0;
        void Check(string name, bool ok, string extra = "") { if (!ok) fails++; sb.AppendLine($"{(ok ? "OK   " : "FALHA")} {name} {extra}"); }
        Width = 1400; Height = 860; StartPosition = FormStartPosition.Manual; Left = -3000; Top = 0; ShowInTaskbar = false;
        Show(); Application.DoEvents();
        var tabs = Controls.OfType<TabControl>().First();
        var page = tabs.TabPages.Cast<TabPage>().First(p => p.Text == "Loja de itens");
        tabs.SelectedTab = page;
        LoadLoja(); Application.DoEvents();
        Check("carregou o catálogo", lojaCfg.Categorias.Count > 0 && lstLojaCats.Items.Count == lojaCfg.Categorias.Count, $"({lojaCfg.Categorias.Count} categorias)");
        lstLojaCats.SelectedIndex = 0; Application.DoEvents();
        var cat = (LojaItens.Categoria)lstLojaCats.SelectedItem!;
        Check("itens da categoria na grade", gridLojaItens.Rows.Count == cat.Itens.Count && cat.Itens.Count > 0, $"({cat.Nome}: {cat.Itens.Count})");
        var primeiro = cat.Itens[0]; int precoAntes = primeiro.Preco;
        gridLojaItens.Rows[0].Cells["preco"].Value = precoAntes + 5; Application.DoEvents();
        Check("editar o preço na grade muda o item e marca \"não salvo\"", primeiro.Preco == precoAntes + 5 && lojaDirty && lblLoja.Text.Contains("não salvo"));
        gridLojaNivel.Rows[0].Cells["c15"].Value = 999; Application.DoEvents();
        Check("tabela de nível muda o exemplo", lojaCfg.Nivel[15] == 999 && lblLojaExemplo.Text.Contains("completo"));
        Check("validação aceita o catálogo", LojaItens.Validar(lojaCfg).Count == 0, string.Join(" | ", LojaItens.Validar(lojaCfg)));
        using (var bmp = new Bitmap(Width, Height)) { DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height)); bmp.Save(png); }
        var inner = page.Controls.OfType<TabControl>().First();
        inner.SelectedIndex = 1; Application.DoEvents();
        var png2 = Path.ChangeExtension(png, null) + "-precos.png";
        using (var bmp = new Bitmap(Width, Height)) { DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height)); bmp.Save(png2); }
        sb.AppendLine($"fotos: {png} e {png2}");
        Hide(); LoadLoja();
        Check("nada foi salvo (recarregado igual ao arquivo)", !lojaDirty && lojaCfg.Categorias[0].Itens[0].Preco == precoAntes);
        sb.AppendLine($"resultado: {fails} falha(s)");
        return sb.ToString();
    }
}

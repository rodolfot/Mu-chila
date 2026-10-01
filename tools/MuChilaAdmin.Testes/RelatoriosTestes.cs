using System.Data;
using System.Text;
using MuChilaAdmin.Core.Painel.Relatorios;

namespace MuChilaAdmin.Testes;

public class RelatoriosTestes
{
    static Relatorio Exemplo(int linhas = 3) => new()
    {
        Titulo = "Contas", Subtitulo = "teste",
        Colunas = { new("Conta"), new("Cash", Numero: true), new("Observação") },
        Linhas = Enumerable.Range(1, linhas).Select(i => new[] { $"conta{i}", (i * 1000).ToString("N0"), "ação; com \"aspas\"" }).ToList(),
    };

    [Fact]
    public void Csv_para_o_Excel_em_portugues()
    {
        var bytes = Csv.Gerar(Exemplo());
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);   // BOM: acentos certos no Excel
        var texto = Encoding.UTF8.GetString(bytes[3..]);
        var linhas = texto.Split("\r\n");
        Assert.Equal("Conta;Cash;Observação", linhas[0]);
        Assert.Equal("conta1;1.000;\"ação; com \"\"aspas\"\"\"", linhas[1]);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "'=HYPERLINK(\"x\")")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("@SOMA(A1)", "'@SOMA(A1)")]
    [InlineData("-10", "-10")]          // número negativo continua número
    [InlineData("-1.234,50", "-1.234,50")]
    [InlineData("normal", "normal")]
    public void Csv_nao_deixa_formula_do_jogador_rodar(string valor, string esperado)
    {
        var campo = Csv.Campo(valor);
        Assert.Equal(esperado, campo.Trim('"').Replace("\"\"", "\""));
    }

    [Fact]
    public void Pdf_valido_com_varias_paginas()
    {
        var pdf = Pdf.Gerar(Exemplo(200));
        var texto = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.", texto);
        Assert.EndsWith("%%EOF\n", texto.Replace("\r\n", "\n"));
        var paginas = System.Text.RegularExpressions.Regex.Matches(texto, @"/Type\s*/Page[^s]").Count;
        Assert.True(paginas >= 4, $"esperava várias páginas, veio {paginas}");
        Assert.Contains("/Count " + paginas, texto);
    }

    [Fact]
    public void Pdf_escreve_acentos_em_WinAnsi()
    {
        Assert.Equal(new byte[] { 0xE7, 0xE3, 0x6F }, Pdf.WinAnsi("ção"));
    }

    [Fact]
    public void Relatorio_de_tabela_formata_e_esconde_colunas_internas()
    {
        var t = new DataTable();
        t.Columns.Add("Conta"); t.Columns.Add("Cash", typeof(int)); t.Columns.Add("Criada", typeof(DateTime)); t.Columns.Add("_ctl");
        t.Rows.Add("mario", 12345, new DateTime(2026, 10, 1, 14, 30, 0), "x");
        var r = Relatorio.DeTabela("Contas", t);
        Assert.Equal(new[] { "Conta", "Cash", "Criada" }, r.Colunas.Select(c => c.Titulo));
        Assert.True(r.Colunas[1].Numero);
        Assert.Equal(new[] { "mario", "12.345", "01/10/2026 14:30" }, r.Linhas[0]);
    }
}

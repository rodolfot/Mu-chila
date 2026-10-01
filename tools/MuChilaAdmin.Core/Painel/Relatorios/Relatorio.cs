using System.Data;
using System.Globalization;

namespace MuChilaAdmin.Core.Painel.Relatorios;

/// <summary>Coluna de relatório. Numero = alinhada à direita; Peso = largura relativa no PDF (0 = calcula pelo conteúdo).</summary>
public sealed record ColunaRelatorio(string Titulo, bool Numero = false, float Peso = 0);

/// <summary>Uma tabela pronta para virar CSV ou PDF.</summary>
public sealed class Relatorio
{
    public string Titulo { get; init; } = "";
    public string? Subtitulo { get; init; }
    public List<ColunaRelatorio> Colunas { get; init; } = new();
    public List<string[]> Linhas { get; init; } = new();
    public DateTime Gerado { get; init; } = DateTime.Now;
    public string? GeradoPor { get; set; }
    /// <summary>Linha de total no fim (opcional; mesma quantidade de colunas).</summary>
    public string[]? Rodape { get; init; }

    static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Texto de um valor do banco para o relatório (datas e números no formato brasileiro).</summary>
    public static string Texto(object? v) => v switch
    {
        null or DBNull => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy") : d.ToString("dd/MM/yyyy HH:mm"),
        decimal m => m.ToString("N2", Br),
        double x => x.ToString("N2", Br),
        float x => x.ToString("N2", Br),
        int or long or short or byte => Convert.ToInt64(v).ToString("N0", Br),
        bool b => b ? "sim" : "não",
        byte[] bytes => Convert.ToHexString(bytes),
        _ => v.ToString() ?? "",
    };

    public static bool EhNumero(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(decimal) || t == typeof(double) || t == typeof(float);

    /// <summary>Monta a partir de um DataTable (colunas que começam com "_" ficam de fora).</summary>
    public static Relatorio DeTabela(string titulo, DataTable t, string? subtitulo = null)
    {
        var cols = t.Columns.Cast<DataColumn>().Where(c => !c.ColumnName.StartsWith('_')).ToList();
        return new Relatorio
        {
            Titulo = titulo, Subtitulo = subtitulo,
            Colunas = cols.Select(c => new ColunaRelatorio(c.ColumnName, EhNumero(c.DataType))).ToList(),
            Linhas = t.Rows.Cast<DataRow>().Select(r => cols.Select(c => Texto(r[c])).ToArray()).ToList(),
        };
    }
}

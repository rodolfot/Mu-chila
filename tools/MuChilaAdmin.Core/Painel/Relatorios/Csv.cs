using System.Text;

namespace MuChilaAdmin.Core.Painel.Relatorios;

/// <summary>
/// CSV para o Excel em português: UTF-8 com BOM (acentos certos), separador ";" e CRLF. Texto que começa com = + - @
/// (e não é número) ganha um apóstrofo na frente, para o Excel não executar como fórmula (nomes de conta e mensagens
/// vêm de jogadores).
/// </summary>
public static class Csv
{
    public static byte[] Gerar(Relatorio r)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(';', r.Colunas.Select(c => Campo(c.Titulo)))).Append("\r\n");
        foreach (var l in r.Linhas) sb.Append(string.Join(';', l.Select(Campo))).Append("\r\n");
        if (r.Rodape != null) sb.Append(string.Join(';', r.Rodape.Select(Campo))).Append("\r\n");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public static string Campo(string? v)
    {
        v ??= "";
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(v, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), out _))
            v = "'" + v;
        bool aspas = v.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 || v != v.Trim();
        return aspas ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }
}

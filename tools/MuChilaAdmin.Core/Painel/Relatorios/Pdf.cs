using System.Globalization;
using System.IO;
using System.Text;

namespace MuChilaAdmin.Core.Painel.Relatorios;

/// <summary>
/// PDF de tabela sem biblioteca externa: fontes padrão do PDF (Helvetica e Helvetica-Bold, que todo leitor tem, sem
/// embutir) com WinAnsiEncoding, que cobre os acentos do português. Cabeçalho da tabela repetido em cada página, linhas
/// zebradas, números à direita, texto longo cortado com "…", rodapé "Página X de Y". Retrato até 5 colunas; paisagem acima.
/// As larguras vêm das métricas oficiais (AFM) da Helvetica, para medir o texto e montar as colunas.
/// </summary>
public static class Pdf
{
    const float Margem = 36, FonteTabela = 8f, AlturaLinha = 14f, AlturaCabecalho = 18f, Folga = 4f;

    public static byte[] Gerar(Relatorio r)
    {
        int n = Math.Max(1, r.Colunas.Count);
        var naturais = new float[n];
        for (int c = 0; c < n; c++)
        {
            float w = Largura(c < r.Colunas.Count ? r.Colunas[c].Titulo : "", FonteTabela, negrito: true);
            foreach (var l in r.Linhas.Take(2000)) if (c < l.Length) w = Math.Max(w, Largura(l[c], FonteTabela, false));
            if (r.Rodape != null && c < r.Rodape.Length) w = Math.Max(w, Largura(r.Rodape[c], FonteTabela, true));
            naturais[c] = w + 2 * Folga;
        }
        bool paisagem = n > 5 || naturais.Sum() > 595 - 2 * Margem;
        float W = paisagem ? 842 : 595, H = paisagem ? 595 : 842, util = W - 2 * Margem;
        var larguras = Distribuir(naturais, r.Colunas, util);

        float topoTabela = H - Margem - 58;
        float fimUtil = Margem + 22;
        int porPagina = Math.Max(1, (int)((topoTabela - AlturaCabecalho - fimUtil) / AlturaLinha));
        int linhasTotais = r.Linhas.Count + (r.Rodape != null ? 1 : 0);
        int paginas = Math.Max(1, (int)Math.Ceiling(linhasTotais / (double)porPagina));

        var conteudos = new List<string>();
        for (int p = 0; p < paginas; p++)
        {
            var s = new StringBuilder();
            void Cor(float g, bool preenchimento = true) => s.Append(Num(g)).Append(' ').Append(Num(g)).Append(' ').Append(Num(g)).Append(preenchimento ? " rg\n" : " RG\n");
            void Rgb(float a, float b, float c, bool preenchimento = true) => s.Append($"{Num(a)} {Num(b)} {Num(c)} {(preenchimento ? "rg" : "RG")}\n");
            void Texto(string t, float x, float y, float tam, bool negrito) =>
                s.Append("BT /").Append(negrito ? "F2 " : "F1 ").Append(Num(tam)).Append(" Tf ").Append(Num(x)).Append(' ').Append(Num(y)).Append(" Td (").Append(Escapar(t)).Append(") Tj ET\n");

            // cabeçalho da página
            Rgb(0.07f, 0.09f, 0.13f); Texto(Cortar(r.Titulo, util, 15, true), Margem, H - Margem - 14, 15, true);
            Cor(0.38f);
            if (!string.IsNullOrWhiteSpace(r.Subtitulo)) Texto(Cortar(r.Subtitulo!, util, 9, false), Margem, H - Margem - 30, 9, false);
            var meta = $"Mu Chila Admin · gerado em {r.Gerado:dd/MM/yyyy HH:mm}{(r.GeradoPor != null ? $" por {r.GeradoPor}" : "")} · {r.Linhas.Count:N0} linha(s)".Replace(',', '.');
            Texto(Cortar(meta, util, 8, false), Margem, H - Margem - 43, 8, false);

            // cabeçalho da tabela
            float y = topoTabela;
            Rgb(0.12f, 0.16f, 0.24f); s.Append($"{Num(Margem)} {Num(y - AlturaCabecalho)} {Num(util)} {Num(AlturaCabecalho)} re f\n");
            Cor(1f);
            float x = Margem;
            for (int c = 0; c < n; c++)
            {
                var t = Cortar(c < r.Colunas.Count ? r.Colunas[c].Titulo : "", larguras[c] - 2 * Folga, FonteTabela, true);
                bool num = c < r.Colunas.Count && r.Colunas[c].Numero;
                float tx = num ? x + larguras[c] - Folga - Largura(t, FonteTabela, true) : x + Folga;
                Texto(t, tx, y - AlturaCabecalho / 2 - FonteTabela * 0.35f, FonteTabela, true);
                x += larguras[c];
            }
            y -= AlturaCabecalho;

            // linhas
            int ini = p * porPagina, fim = Math.Min(linhasTotais, ini + porPagina);
            for (int i = ini; i < fim; i++)
            {
                bool rodape = r.Rodape != null && i == r.Linhas.Count;
                var linha = rodape ? r.Rodape! : r.Linhas[i];
                if (rodape) { Cor(0.86f); s.Append($"{Num(Margem)} {Num(y - AlturaLinha)} {Num(util)} {Num(AlturaLinha)} re f\n"); }
                else if (i % 2 == 1) { Rgb(0.95f, 0.96f, 0.97f); s.Append($"{Num(Margem)} {Num(y - AlturaLinha)} {Num(util)} {Num(AlturaLinha)} re f\n"); }
                Rgb(0.07f, 0.09f, 0.13f);
                x = Margem;
                for (int c = 0; c < n; c++)
                {
                    var t = Cortar(c < linha.Length ? linha[c] ?? "" : "", larguras[c] - 2 * Folga, FonteTabela, rodape);
                    bool num = c < r.Colunas.Count && r.Colunas[c].Numero;
                    float tx = num ? x + larguras[c] - Folga - Largura(t, FonteTabela, rodape) : x + Folga;
                    Texto(t, tx, y - AlturaLinha / 2 - FonteTabela * 0.35f, FonteTabela, rodape);
                    x += larguras[c];
                }
                y -= AlturaLinha;
            }
            // borda da tabela e linha final
            Cor(0.8f, preenchimento: false);
            s.Append("0.5 w\n").Append($"{Num(Margem)} {Num(y)} {Num(util)} {Num(topoTabela - y)} re S\n");

            // rodapé
            Cor(0.45f);
            Texto(Cortar($"Mu Chila Admin — {r.Titulo}", util * 0.7f, 7.5f, false), Margem, Margem - 4, 7.5f, false);
            var pag = $"Página {p + 1} de {paginas}";
            Texto(pag, W - Margem - Largura(pag, 7.5f, false), Margem - 4, 7.5f, false);
            conteudos.Add(s.ToString());
        }
        return Montar(conteudos, W, H, r.Titulo, r.Gerado);
    }

    /// <summary>Larguras finais: cabe no espaço útil, sem coluna engolindo as outras.</summary>
    static float[] Distribuir(float[] naturais, List<ColunaRelatorio> colunas, float util)
    {
        int n = naturais.Length;
        var w = new float[n];
        for (int c = 0; c < n; c++)
        {
            float peso = c < colunas.Count ? colunas[c].Peso : 0;
            w[c] = peso > 0 ? peso : Math.Min(naturais[c], util * 0.45f);
            w[c] = Math.Max(w[c], 28);
        }
        float soma = w.Sum();
        if (soma > util)
        {
            // encolhe primeiro as colunas largas (texto), preservando as estreitas (números, datas)
            float excesso = soma - util;
            var largas = Enumerable.Range(0, n).Where(c => w[c] > util / n).ToList();
            float somaLargas = largas.Sum(c => w[c]);
            foreach (var c in largas) w[c] = Math.Max(28, w[c] - excesso * w[c] / somaLargas);
            soma = w.Sum();
            if (soma > util) for (int c = 0; c < n; c++) w[c] *= util / soma;
        }
        else if (soma < util)
            for (int c = 0; c < n; c++) w[c] += (util - soma) * w[c] / soma;
        return w;
    }

    static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Corta o texto para caber na largura, terminando com "…".</summary>
    static string Cortar(string t, float max, float tam, bool negrito)
    {
        t = (t ?? "").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        if (Largura(t, tam, negrito) <= max) return t;
        float rot = Largura("…", tam, negrito);
        int lo = 0, hi = t.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Largura(t[..mid], tam, negrito) + rot <= max) lo = mid; else hi = mid - 1;
        }
        return lo <= 0 ? "…" : t[..lo].TrimEnd() + "…";
    }

    public static float Largura(string t, float tam, bool negrito)
    {
        var tabela = negrito ? Negrito : Regular;
        int soma = 0;
        foreach (var b in WinAnsi(t)) soma += b >= 32 ? tabela[b - 32] : 556;
        return soma * tam / 1000f;
    }

    /// <summary>Texto → bytes WinAnsi (Windows-1252). Fora dela vira "?".</summary>
    public static byte[] WinAnsi(string t)
    {
        var bytes = new byte[t.Length];
        for (int i = 0; i < t.Length; i++)
        {
            char ch = t[i];
            bytes[i] = ch switch
            {
                < (char)0x80 => (byte)ch,
                >= (char)0xA0 and <= (char)0xFF => (byte)ch,
                '€' => 0x80, '‚' => 0x82, 'ƒ' => 0x83, '„' => 0x84, '…' => 0x85, '†' => 0x86, '‡' => 0x87, 'ˆ' => 0x88, '‰' => 0x89, 'Š' => 0x8A,
                '‹' => 0x8B, 'Œ' => 0x8C, 'Ž' => 0x8E, '‘' => 0x91, '’' => 0x92, '“' => 0x93, '”' => 0x94, '•' => 0x95, '–' => 0x96,
                '—' => 0x97, '˜' => 0x98, '™' => 0x99, 'š' => 0x9A, '›' => 0x9B, 'œ' => 0x9C, 'ž' => 0x9E, 'Ÿ' => 0x9F, '→' => (byte)'>',
                _ => (byte)'?',
            };
        }
        return bytes;
    }

    static string Escapar(string t)
    {
        var sb = new StringBuilder();
        foreach (var b in WinAnsi(t))
        {
            if (b is (byte)'(' or (byte)')' or (byte)'\\') sb.Append('\\').Append((char)b);
            else if (b < 32 || b > 126) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
            else sb.Append((char)b);
        }
        return sb.ToString();
    }

    /// <summary>Texto do dicionário Info (título): UTF-16BE com BOM, em hexa (aceita qualquer caractere).</summary>
    static string TextoUnicode(string t) => "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(t)) + ">";

    static byte[] Montar(List<string> conteudos, float w, float h, string titulo, DateTime gerado)
    {
        var ms = new MemoryStream();
        var offsets = new List<long>();
        void Escrever(string s) { var b = Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); }
        void Objeto(int num, string corpo) { offsets.Add(ms.Position); Escrever($"{num} 0 obj\n{corpo}\nendobj\n"); }

        Escrever("%PDF-1.4\n");
        ms.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });   // marca de arquivo binário
        int paginas = conteudos.Count;
        var kids = string.Join(" ", Enumerable.Range(0, paginas).Select(i => $"{6 + 2 * i} 0 R"));
        Objeto(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Objeto(2, $"<< /Type /Pages /Kids [{kids}] /Count {paginas} >>");
        Objeto(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Objeto(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var fuso = TimeZoneInfo.Local.GetUtcOffset(gerado);
        var data = $"D:{gerado:yyyyMMddHHmmss}{(fuso < TimeSpan.Zero ? "-" : "+")}{Math.Abs(fuso.Hours):00}'{Math.Abs(fuso.Minutes):00}'";
        Objeto(5, $"<< /Title {TextoUnicode(titulo)} /Producer (Mu Chila Admin) /Creator (Mu Chila Admin) /CreationDate ({data}) >>");
        for (int i = 0; i < paginas; i++)
        {
            var bytes = Encoding.ASCII.GetBytes(conteudos[i]);
            Objeto(6 + 2 * i, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(w)} {Num(h)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {7 + 2 * i} 0 R >>");
            offsets.Add(ms.Position);
            Escrever($"{7 + 2 * i} 0 obj\n<< /Length {bytes.Length} >>\nstream\n");
            ms.Write(bytes, 0, bytes.Length);
            Escrever("\nendstream\nendobj\n");
        }
        long xref = ms.Position;
        int total = offsets.Count + 1;
        Escrever($"xref\n0 {total}\n0000000000 65535 f \n");
        foreach (var o in offsets) Escrever($"{o:0000000000} 00000 n \n");
        Escrever($"trailer\n<< /Size {total} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    // ---------------- métricas da Helvetica (AFM, 1/1000 do corpo), códigos 32 a 255 em WinAnsi ----------------
    static readonly short[] Regular = Tabela(
        // 32-126
        "278 278 355 556 556 889 667 191 333 333 389 584 278 333 278 278 556 556 556 556 556 556 556 556 556 556 278 278 584 584 584 556 " +
        "1015 667 667 722 722 667 611 778 722 278 500 667 556 833 722 778 667 778 722 667 611 722 667 944 667 667 611 278 278 278 469 556 " +
        "333 556 556 500 556 556 278 556 556 222 222 500 222 833 556 556 556 556 333 500 278 556 500 722 500 500 500 334 260 334 584 " +
        // 127-159
        "350 556 350 222 556 333 1000 556 556 333 1000 667 333 1000 350 611 350 350 222 222 333 333 350 556 1000 333 1000 500 333 944 350 500 667 " +
        // 160-191
        "278 333 556 556 556 556 260 556 333 737 370 556 584 333 737 333 400 584 333 333 333 556 537 278 333 333 365 556 834 834 834 611 " +
        // 192-255
        "667 667 667 667 667 667 1000 722 667 667 667 667 278 278 278 278 722 722 778 778 778 778 778 584 778 722 722 722 722 667 667 611 " +
        "556 556 556 556 556 556 889 500 556 556 556 556 278 278 278 278 556 556 556 556 556 556 556 584 611 556 556 556 556 500 556 500");

    static readonly short[] Negrito = Tabela(
        "278 333 474 556 556 889 722 238 333 333 389 584 278 333 278 278 556 556 556 556 556 556 556 556 556 556 333 333 584 584 584 611 " +
        "975 722 722 722 722 667 611 778 722 278 556 722 611 833 722 778 667 778 722 667 611 722 667 944 667 667 611 333 278 333 584 556 " +
        "333 556 611 556 611 556 333 611 611 278 278 556 278 889 611 611 611 611 389 556 333 611 556 778 556 556 500 389 280 389 584 " +
        "350 556 350 278 556 500 1000 556 556 333 1000 667 333 1000 350 611 350 350 278 278 500 500 350 556 1000 333 1000 556 333 944 350 500 667 " +
        "278 333 556 556 556 556 280 556 333 737 370 556 584 333 737 333 400 584 333 333 333 611 556 278 333 333 365 556 834 834 834 611 " +
        "722 722 722 722 722 722 1000 722 667 667 667 667 278 278 278 278 722 722 778 778 778 778 778 584 778 722 722 722 722 667 667 611 " +
        "556 556 556 556 556 556 889 556 556 556 556 556 278 278 278 278 611 611 611 611 611 611 611 584 611 611 611 611 611 556 611 556");

    static short[] Tabela(string valores)
    {
        var v = valores.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToArray();
        if (v.Length != 224) throw new InvalidOperationException($"Tabela de métricas com {v.Length} valores (esperado 224).");
        return v;
    }
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MuChilaAdmin.Core;

/// <summary>
/// Valores dos resets do site (créditos por Master/Supreme e o atributo máximo que libera o Supreme),
/// guardados em Site\...\includes\config\muchila.resets.json. Grava na cópia ativa (www) e na do repositório
/// (muchila\www), para sobreviver a um reinstalar do site. As procedures/UI do site leem esse arquivo.
/// </summary>
public static class ResetConfig
{
    static string Www => Path.Combine(ServerControl.ServerRoot, @"Site\www\includes\config\muchila.resets.json");
    static string Overlay => Path.Combine(ServerControl.ServerRoot, @"Site\muchila\www\includes\config\muchila.resets.json");

    public sealed class Valores
    {
        public bool Ativo = true;
        public int MasterCreditos;
        public int SupremeCreditos;
        public int MaxStat = 65000;
    }

    public static Valores Carregar()
    {
        var f = File.Exists(Www) ? Www : Overlay;
        var v = new Valores();
        if (!File.Exists(f)) return v;
        var o = JsonNode.Parse(File.ReadAllText(f)) as JsonObject;
        if (o == null) return v;
        v.Ativo = (o["ativo"]?.GetValue<bool>()) ?? true;
        v.MasterCreditos = LerInt(o, "master_creditos");
        v.SupremeCreditos = LerInt(o, "supreme_creditos");
        v.MaxStat = LerInt(o, "max_stat", 65000);
        return v;
    }

    static int LerInt(JsonObject o, string k, int padrao = 0)
    {
        try { return o[k]?.GetValue<int>() ?? padrao; } catch { return padrao; }
    }

    /// <summary>Grava os valores nos dois arquivos (o que existir), preservando o resto do JSON (ex.: o comentário).</summary>
    public static string Salvar(Valores v)
    {
        if (v.MasterCreditos < 0 || v.SupremeCreditos < 0 || v.MaxStat < 0)
            throw new InvalidOperationException("Os valores não podem ser negativos.");
        int gravados = 0;
        foreach (var f in new[] { Www, Overlay })
        {
            if (!File.Exists(f)) continue;
            var o = (JsonNode.Parse(File.ReadAllText(f)) as JsonObject) ?? new JsonObject();
            o["ativo"] = v.Ativo;
            o["master_creditos"] = v.MasterCreditos;
            o["supreme_creditos"] = v.SupremeCreditos;
            o["max_stat"] = v.MaxStat;
            File.WriteAllText(f, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            gravados++;
        }
        if (gravados == 0) throw new FileNotFoundException("Não achei o muchila.resets.json (o site está instalado?).");
        return $"Valores dos resets salvos ({gravados} arquivo(s)). O site passa a usar na hora; o cron do master reset usa o novo valor no próximo minuto.";
    }
}

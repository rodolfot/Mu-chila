using System.Diagnostics;
using System.IO;
using System.Text;

namespace MuChilaAdmin.Core;

/// <summary>
/// Passe dos Mapas Exclusivos (decisão do dono, 28/09/2026): os mapas de dbo.MuChila_PasseMapasLista (acima do nível 400)
/// só com passe ativo na conta (dbo.MuChila_PasseMapas, vindo do ticket da loja de cash ou da loja do site). O GameServer não
/// tem como barrar a entrada, então o vigia confere a cada segundo: personagem num desses mapas sem passe vai para a seleção
/// de personagem (Trigger) e, 2 s depois de sair do jogo (o servidor já gravou), é posto em Lorencia no banco. Se reentrar
/// antes disso, volta a ser mandado para a seleção. Liga com o arquivo vigia-passe-mapas.ligado.
///
/// Magic Backpack (vigia-mochila.ligado): a faixa nova do inventário só aparece ao reentrar; o Poll manda para a seleção
/// assim que vê o contador subir.
/// </summary>
public static partial class ResetWatcher
{
    public static readonly string PassSwitchPath = Path.Combine(AppContext.BaseDirectory, "vigia-passe-mapas.ligado");
    static bool PassEnabled => File.Exists(PassSwitchPath);
    public static readonly string BackpackSwitchPath = Path.Combine(AppContext.BaseDirectory, "vigia-mochila.ligado");
    static bool BackpackEnabled => File.Exists(BackpackSwitchPath);

    // Lorencia, área do gate 17 (Gate.txt): mapa 0, X 133-143, Y 118-125
    const int SafeMap = 0, SafeX1 = 133, SafeX2 = 143, SafeY1 = 118, SafeY2 = 125;

    static readonly Dictionary<int, string> passMaps = new();
    static HashSet<string> passAccounts = new(StringComparer.OrdinalIgnoreCase);
    static bool passAccountsOk;
    static DateTime nextPassMaps = DateTime.MinValue, nextPassAccounts = DateTime.MinValue;
    // personagem mandado para a seleção → (quando mandou, quando viu fora do jogo, conta, mapa)
    static readonly Dictionary<string, (DateTime Sent, DateTime? Left, string Account, int Map)> expelled = new(StringComparer.OrdinalIgnoreCase);

    static string? ReadAccount(byte[] obj)
    {
        int end = Array.IndexOf(obj, (byte)0, OffAccount, 11);
        if (end <= OffAccount) return null;
        var s = Encoding.ASCII.GetString(obj, OffAccount, end - OffAccount);
        return s.All(c => c > ' ' && c < 127) ? s : null;
    }

    static string MapName(int map) => passMaps.TryGetValue(map, out var n) ? n : $"mapa {map}";

    static List<string> ProcessMapPass(List<Target> targets)
    {
        var log = new List<string>();
        if (!PassEnabled) { expelled.Clear(); return log; }
        if (DateTime.Now >= nextPassMaps)
        {
            nextPassMaps = DateTime.Now.AddMinutes(1);
            var t = Db.Query("SELECT Mapa, Nome FROM dbo.MuChila_PasseMapasLista");
            passMaps.Clear();
            foreach (System.Data.DataRow r in t.Rows) passMaps[(int)r["Mapa"]] = (string)r["Nome"];
        }
        if (passMaps.Count == 0) return log;
        if (DateTime.Now >= nextPassAccounts)
        {
            nextPassAccounts = DateTime.Now.AddSeconds(5);
            passAccountsOk = false;   // se a consulta falhar, ninguém é tirado (melhor deixar passar do que tirar quem pagou)
            var t = Db.Query("SELECT Conta FROM dbo.MuChila_PasseMapas WHERE Expira > GETDATE()");
            passAccounts = new HashSet<string>(t.Rows.Cast<System.Data.DataRow>().Select(r => ((string)r["Conta"]).Trim()), StringComparer.OrdinalIgnoreCase);
            passAccountsOk = true;
        }
        if (!passAccountsOk) return log;

        int running = new[] { ServerControl.GameServerProcess, ServerControl.CastleSiegeProcess }.Sum(n => Process.GetProcessesByName(n).Length);
        var ready = targets.Where(t => t.Table != 0 && !t.Process.HasExited).ToList();
        var playing = new Dictionary<string, (Target T, int Idx, int Map, string Account)>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in ready)
            foreach (var (idx, obj) in Players(t))
                if (ReadName(obj) is { } n && ReadAccount(obj) is { } a) playing[n] = (t, idx, obj[OffMap], a);

        foreach (var (name, p) in playing)
        {
            if (!passMaps.ContainsKey(p.Map) || passAccounts.Contains(p.Account)) { expelled.Remove(name); continue; }
            // num mapa do passe sem passe: manda para a seleção (de novo se reentrou ou se não saiu em 8 s)
            if (!expelled.TryGetValue(name, out var e) || e.Left != null || DateTime.Now - e.Sent > TimeSpan.FromSeconds(8))
            {
                log.Add(Trigger(p.T, p.Idx, name, $"sem Passe dos Mapas em {MapName(p.Map)} (conta {p.Account})"));
                expelled[name] = (DateTime.Now, null, p.Account, p.Map);
            }
        }

        if (ready.Count < running) return log;   // algum servidor ainda não foi lido: não dá para ter certeza de quem saiu
        foreach (var (name, e) in expelled.ToList())
        {
            if (playing.ContainsKey(name)) continue;
            if (e.Left == null) { expelled[name] = (e.Sent, DateTime.Now, e.Account, e.Map); continue; }
            if (DateTime.Now - e.Left.Value < TimeSpan.FromSeconds(2)) continue;   // tempo para o servidor gravar o personagem
            int x = Random.Shared.Next(SafeX1, SafeX2 + 1), y = Random.Shared.Next(SafeY1, SafeY2 + 1);
            int n = Db.Execute(
                "UPDATE dbo.[Character] SET MapNumber = @m, MapPosX = @x, MapPosY = @y WHERE Name = @n " +
                "AND MapNumber IN (SELECT Mapa FROM dbo.MuChila_PasseMapasLista) " +
                "AND NOT EXISTS (SELECT 1 FROM dbo.MuChila_PasseMapas WHERE Conta = @c AND Expira > GETDATE())",
                ("@m", SafeMap), ("@x", x), ("@y", y), ("@n", name), ("@c", e.Account));
            log.Add(n > 0 ? $"{name}: fora do jogo; posto em Lorencia ({x}, {y}), saindo de {MapName(e.Map)}"
                          : $"{name}: fora do jogo; já não estava num mapa do passe (ou a conta ganhou passe)");
            expelled.Remove(name);
        }
        return log;
    }
}

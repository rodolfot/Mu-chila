# Reagrupa os spawns de monstros em grupos de 5+ proximos.
# -Apply grava os arquivos (com backup); sem -Apply so simula e mostra o resumo.
param([switch]$Apply, [int]$GroupSize = 5, [int]$BoxHalf = 2, [int]$MinWalkable = 12)
. (Join-Path $PSScriptRoot 'spawn_lib.ps1')

Add-Type -TypeDefinition @"
using System; using System.Collections.Generic;
public class SpawnGrid {
    int[] sum = new int[257 * 257];   // soma acumulada 2D de tiles livres
    bool[] walk = new bool[256 * 256];
    public SpawnGrid(byte[] att) {
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++) walk[y * 256 + x] = (att[3 + y * 256 + x] & 0x0D) == 0;
        for (int y = 1; y <= 256; y++) for (int x = 1; x <= 256; x++)
            sum[y * 257 + x] = (walk[(y - 1) * 256 + x - 1] ? 1 : 0) + sum[(y - 1) * 257 + x] + sum[y * 257 + x - 1] - sum[(y - 1) * 257 + x - 1];
    }
    public int Box(int cx, int cy, int h) {
        int x1 = Math.Max(0, cx - h), y1 = Math.Max(0, cy - h), x2 = Math.Min(255, cx + h), y2 = Math.Min(255, cy + h);
        return sum[(y2 + 1) * 257 + x2 + 1] - sum[y1 * 257 + x2 + 1] - sum[(y2 + 1) * 257 + x1] + sum[y1 * 257 + x1];
    }
    // Tiles livres dentro dos retangulos cuja caixa (2h+1)^2 tem pelo menos minWalk tiles livres
    public List<int[]> Candidates(List<int[]> rects, int h, int minWalk) {
        var seen = new HashSet<int>(); var list = new List<int[]>();
        foreach (var r in rects)
            for (int y = Math.Max(0, r[1]); y <= Math.Min(255, r[3]); y++)
                for (int x = Math.Max(0, r[0]); x <= Math.Min(255, r[2]); x++)
                    if (walk[y * 256 + x] && Box(x, y, h) >= minWalk && seen.Add(y * 256 + x)) list.Add(new int[] { x, y });
        return list;
    }
    // Amostragem do ponto mais distante: centros espalhados pela area original
    public static List<int[]> Centers(List<int[]> cand, int k, int seed) {
        var rnd = new Random(seed); var res = new List<int[]>();
        var dist = new long[cand.Count];
        var first = cand[rnd.Next(cand.Count)]; res.Add(first);
        for (int i = 0; i < cand.Count; i++) dist[i] = Sq(cand[i], first);
        while (res.Count < k) {
            int best = -1; long bd = 0;
            for (int i = 0; i < cand.Count; i++) if (dist[i] > bd) { bd = dist[i]; best = i; }
            if (best < 0) break;
            var c = cand[best]; res.Add(c);
            for (int i = 0; i < cand.Count; i++) { long d = Sq(cand[i], c); if (d < dist[i]) dist[i] = d; }
        }
        return res;
    }
    static long Sq(int[] a, int[] b) { long dx = a[0] - b[0], dy = a[1] - b[1]; return dx * dx + dy * dy; }
}
"@

$info = Get-MonsterInfo
$terrainAlias = @{ 25 = 24; 26 = 24; 27 = 24; 28 = 24; 29 = 24; 124 = 123; 125 = 123; 126 = 123; 127 = 123 }
$skipMaps = @(34, 42)   # Crywolf e Refugio de Balgass: spawns ligados a eventos
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$report = @()

foreach ($file in Get-ChildItem -LiteralPath $MsbDir -File | Sort-Object Name) {
    $map = [int]$file.Name.Substring(0, 3)
    $rows = @(Get-MapSpawns $file)
    if (-not $rows -or $skipMaps -contains $map) { continue }
    $tmap = if ($terrainAlias.ContainsKey($map)) { $terrainAlias[$map] } else { $map }
    $att = Get-Terrain $tmap
    if (-not $att) { $report += [pscustomobject]@{ Map = $map; Nome = $file.BaseName; Antes = 0; Depois = 0; Grupos = 0; Falhas = 0; Intocados = 'sem terreno' }; continue }
    $grid = New-Object SpawnGrid (, $att)

    $newConfigs = New-Object System.Collections.Generic.List[string]
    $removeRaw = New-Object System.Collections.Generic.HashSet[string]
    $before = 0; $after = 0; $groups = 0; $kept = @(); $fails = 0

    foreach ($g in $rows | Group-Object Class) {
        $cls = [int]$g.Name
        $n = [int]($g.Group | Measure-Object Q -Sum).Sum
        $mi = $info[$cls]
        $before += $n
        # Chefes/raros (1-2 no mapa), armadilhas/estatuas (MoveRange 0) e respawn longo (>= 10 min) ficam como estao
        if ($n -le 2 -or -not $mi -or $mi.MoveRange -eq 0 -or $mi.Regen -ge 600) {
            $after += $n; $kept += $(if ($mi) { $mi.Name } else { "#$cls" }); continue
        }
        $rects = New-Object 'System.Collections.Generic.List[int[]]'
        foreach ($r in $g.Group) {
            if ($r.X1 -eq $r.X2 -and $r.Y1 -eq $r.Y2) { $rects.Add([int[]]@(($r.X1 - 3), ($r.Y1 - 3), ($r.X2 + 3), ($r.Y2 + 3))) }
            else { $rects.Add([int[]]@($r.X1, $r.Y1, $r.X2, $r.Y2)) }
        }
        $cand = $grid.Candidates($rects, $BoxHalf, $MinWalkable)
        if ($cand.Count -eq 0) { $after += $n; $kept += "$($mi.Name) (sem chao)"; $fails++; continue }

        $k = [Math]::Max(1, [Math]::Floor($n / $GroupSize))
        $total = [Math]::Max($n, $GroupSize)
        $centers = [SpawnGrid]::Centers($cand, $k, (20260923 + $map * 1000 + $cls))
        $k = $centers.Count
        $element = ($g.Group | Select-Object -First 1).Element
        for ($i = 0; $i -lt $k; $i++) {
            $q = [Math]::Floor($total / $k) + $(if ($i -lt ($total % $k)) { 1 } else { 0 })
            $c = $centers[$i]
            $newConfigs.Add(('		<Config Class="{0}" Range="3" BeginPosX="{1}" BeginPosY="{2}" EndPosX="{3}" EndPosY="{4}" Direction="-1" Quantity="{5}" Element="{6}" /> <!-- "{7}" (Mu Chila: grupo) -->' -f `
                $cls, [Math]::Max(0, $c[0] - $BoxHalf), [Math]::Max(0, $c[1] - $BoxHalf), [Math]::Min(255, $c[0] + $BoxHalf), [Math]::Min(255, $c[1] + $BoxHalf), $q, $element, $mi.Name))
        }
        foreach ($r in $g.Group) { [void]$removeRaw.Add($r.Raw) }
        $after += $total; $groups += $k
    }

    $report += [pscustomobject]@{ Map = $map; Nome = $file.BaseName.Substring(6); Antes = $before; Depois = $after; Grupos = $groups; Falhas = $fails; Intocados = ($kept -join ', ') }

    if ($Apply -and $newConfigs.Count -gt 0) {
        $xml = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
        Copy-Item -LiteralPath $file.FullName "$($file.FullName).bak-$stamp"
        $nl = "`r`n"
        # Remove as linhas antigas dos monstros agrupados, apenas dentro de SPOT e MONSTER
        foreach ($sec in 'SPOT', 'MONSTER') {
            $xml = [regex]::Replace($xml, "(?s)(<$sec>)(.*?)(</$sec>)", {
                param($m)
                $body = $m.Groups[2].Value
                # $raw ja e a tag inteira "<Config ... />" (valor completo do match em Get-MapSpawns)
                foreach ($raw in $removeRaw) { $body = [regex]::Replace($body, '(?m)^[ \t]*' + [regex]::Escape($raw) + '[^\r\n]*\r?\n', '') }
                $m.Groups[1].Value + $body + $m.Groups[3].Value
            })
        }
        # Novos grupos entram no fim da secao SPOT
        $xml = [regex]::Replace($xml, '(?s)(<SPOT>.*?)(\r?\n)([ \t]*</SPOT>)', { param($m) $m.Groups[1].Value + $nl + ($newConfigs -join $nl) + $m.Groups[2].Value + $m.Groups[3].Value })
        [IO.File]::WriteAllText($file.FullName, $xml, (New-Object Text.UTF8Encoding($false)))
    }
}

$report | Format-Table Map, Nome, Antes, Depois, Grupos, Falhas, Intocados -AutoSize -Wrap | Out-String -Width 220
"TOTAL antes: {0} | depois: {1} | diferenca: {2:+#;-#;0} | grupos: {3}" -f ($report | Measure-Object Antes -Sum).Sum, ($report | Measure-Object Depois -Sum).Sum, (($report | Measure-Object Depois -Sum).Sum - ($report | Measure-Object Antes -Sum).Sum), ($report | Measure-Object Grupos -Sum).Sum
if ($Apply) { "Arquivos gravados. Backups: *.bak-$stamp" }

# Leitura dos MonsterSetBase (SPOT e MONSTER), do Monster.txt e do terreno, compartilhada pelos scripts de agrupamento.
$Global:MsbDir = 'C:\MuServer\Data\Monster\MonsterSetBase'
$Global:TerrainDir = 'C:\MuServer\Data\Terrain'

function Get-MonsterInfo {
    $info = @{}
    foreach ($l in Get-Content 'C:\MuServer\Data\Monster\Monster.txt' -Encoding Latin1) {
        if ($l -match '^\s*(\d+)\s+\d+\s+"([^"]*)"\s+(.*)$') {
            $v = $Matches[3].Trim() -split '\s+'
            # depois do nome: Level MaxLife MaxMana DmgMin DmgMax Def MagDef AtkRate DefRate MoveRange ... RegenTime (indice 15)
            $info[[int]$Matches[1]] = [pscustomobject]@{ Name = $Matches[2]; Level = [int]$v[0]; Life = [int]$v[1]; MoveRange = [int]$v[9]; Regen = [int]$v[15] }
        }
    }
    $info
}

function Get-Terrain([int]$map) {
    $f = Join-Path $Global:TerrainDir ("Terrain{0}.att" -f ($map + 1))
    if (-not (Test-Path $f)) { return $null }
    $b = [IO.File]::ReadAllBytes($f)
    if ($b.Length -ne 65539) { return $null }
    , $b
}

# Chao livre para monstro: sem zona segura (0x01), sem bloqueio (0x04) e sem vazio (0x08)
function Test-Walkable($terrain, [int]$x, [int]$y) {
    if ($x -lt 0 -or $y -lt 0 -or $x -gt 255 -or $y -gt 255) { return $false }
    ($terrain[3 + $y * 256 + $x] -band 0x0D) -eq 0
}

function Get-MapSpawns([IO.FileInfo]$file) {
    $xml = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    $rows = @()
    foreach ($sec in 'SPOT', 'MONSTER') {
        $m = [regex]::Match($xml, "(?s)<$sec>(.*?)</$sec>")
        if (-not $m.Success) { continue }
        foreach ($c in [regex]::Matches($m.Groups[1].Value, '<Config ([^>]*)/>')) {
            $a = @{}
            foreach ($kv in [regex]::Matches($c.Groups[1].Value, '(\w+)="(-?\d+)"')) { $a[$kv.Groups[1].Value] = [int]$kv.Groups[2].Value }
            if ($a.ContainsKey('BeginPosX')) {
                $rows += [pscustomobject]@{ Sec = $sec; Class = $a.Class; Range = $a.Range; X1 = $a.BeginPosX; Y1 = $a.BeginPosY; X2 = $a.EndPosX; Y2 = $a.EndPosY; Q = $a.Quantity; Element = $a.Element; Raw = $c.Value }
            }
            elseif ($a.ContainsKey('PositionX')) {
                $rows += [pscustomobject]@{ Sec = $sec; Class = $a.Class; Range = $a.Range; X1 = $a.PositionX; Y1 = $a.PositionY; X2 = $a.PositionX; Y2 = $a.PositionY; Q = 1; Element = $a.Element; Raw = $c.Value }
            }
        }
    }
    $rows
}

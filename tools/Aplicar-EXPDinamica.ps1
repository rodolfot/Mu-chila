# EXP dinâmica (issues #15 e #24 do Mário, aprovada pelo dono em 28/09/2026): "100x que vai afunilando até 10x".
# Grava a curva na Data\Util\ExperienceTable.txt e recarrega o Util nos GameServers e no Castle Siege.
# O teste do #24 provou que a taxa da tabela é uma PORCENTAGEM da taxa do plano (AddExperienceRate_AL0..3):
# EXP final = taxa do plano × tabela / 100. Então a curva vale para todos, e cada plano mantém a sua vantagem por cima.
# Nível 400 fica em 100 (neutro): lá não há EXP normal, e assim a tabela não mexe na EXP master se também valer para ela.
# Colunas: MinLevel MaxLevel MinMasterLevel MaxMasterLevel MinReset MaxReset MinMasterReset MaxMasterReset ExperienceRate
#
#   .\Aplicar-EXPDinamica.ps1            # aplica (tira a linha de teste do #24, se ainda estiver lá)
#   .\Aplicar-EXPDinamica.ps1 -Desfazer  # tira a curva (EXP volta a ser só a taxa do plano)
#   -Raiz C:\MuServerTeste               # o mesmo no servidor de testes (recarrega só ele)
param([switch]$Desfazer, [string]$Raiz = 'C:\MuServer')
$ErrorActionPreference = 'Stop'
$curva = @(   # nível mínimo, máximo, % da taxa do plano
    @(1, 50, 100), @(51, 100, 85), @(101, 150, 70), @(151, 200, 55), @(201, 250, 40),
    @(251, 300, 30), @(301, 350, 20), @(351, 399, 10), @(400, 400, 100)
)
$f = Join-Path $Raiz 'Data\Util\ExperienceTable.txt'
$enc = [Text.Encoding]::GetEncoding(1252)
$inicio = '// Mu Chila - EXP dinamica (inicio)'; $fim = '// Mu Chila - EXP dinamica (fim)'
$t = [IO.File]::ReadAllText($f, $enc)

# tira a linha de teste do #24 e uma curva anterior
$t = [regex]::Replace($t, "\r\n// Mu Chila - TESTE \(issue do Mario\)[^\r\n]*\r\n1\t10\t0\t0\t0\t10000\t0\t10000\t10", '')
$t = [regex]::Replace($t, "\r\n" + [regex]::Escape($inicio) + "[\s\S]*?" + [regex]::Escape($fim), '')
if (-not $Desfazer) {
    $linhas = @($inicio + ': % da taxa do plano por faixa de nivel (100 = neutro)')
    foreach ($c in $curva) { $linhas += "$($c[0])`t$($c[1])`t0`t600`t0`t10000`t0`t10000`t$($c[2])" }
    $linhas += $fim
    if ($t -notmatch "\r\nend\s*$") { throw "ExperienceTable.txt sem a linha 'end' no fim" }
    $t = $t -replace "\r\nend\s*$", ("`r`n" + ($linhas -join "`r`n") + "`r`nend")
}
Copy-Item $f "$f.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"
[IO.File]::WriteAllText($f, $t, $enc)
"Arquivo: " + ($(if ($Desfazer) { 'curva RETIRADA' } else { 'curva APLICADA' }))
Get-Content $f -Encoding 1252 | Select-Object -Last ($(if ($Desfazer) { 3 } else { $curva.Count + 3 }))

$tools = $PSScriptRoot
if ($Raiz -eq 'C:\MuServer') {
    & (Join-Path $tools 'Recarregar-Servidor.ps1') -Servidor GameServer -Item Util | Out-Null
    & (Join-Path $tools 'Recarregar-Servidor.ps1') -Servidor CastleSiege -Item Util | Out-Null
    $pastas = 'GameServer', 'GameServerNonPvP', 'GameServerVIP', 'GameServerCS'
} else {
    & (Join-Path $tools 'Recarregar-Teste.ps1') -Item Util | Out-Null
    $pastas = @('GameServerTeste')
}
Start-Sleep 3
foreach ($d in $pastas) {
    $log = Get-ChildItem (Join-Path $Raiz "$d\LOG") -Filter *.txt | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    "$d : " + ((Get-Content $log.FullName -Tail 10 | Where-Object { $_ -match 'Util loaded' } | Select-Object -Last 1) -join '')
}

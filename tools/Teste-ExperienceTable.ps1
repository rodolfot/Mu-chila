# Teste da EXP dinamica (issue do Mario): liga/desliga UMA linha na Data\Util\ExperienceTable.txt que vale so para
# personagens de nivel 1 a 10 (taxa 10). Serve para descobrir se a taxa da tabela MULTIPLICA a taxa do plano
# (AddExperienceRate_AL: Free 100, Vipzao 2000) ou SUBSTITUI. Faz backup e recarrega o Util nos servidores.
#
#   .\Teste-ExperienceTable.ps1 -Ligar      # poe a linha de teste e recarrega
#   .\Teste-ExperienceTable.ps1 -Desligar   # tira a linha de teste e recarrega (volta ao normal)
#   -Raiz C:\MuServerTeste                   # o mesmo no servidor de testes (recarrega so ele)
param([switch]$Ligar, [switch]$Desligar, [string]$Raiz = 'C:\MuServer')
$ErrorActionPreference = 'Stop'
if ($Ligar -eq $Desligar) { throw 'Use -Ligar ou -Desligar.' }
$f = Join-Path $Raiz 'Data\Util\ExperienceTable.txt'
$enc = [Text.Encoding]::GetEncoding(1252)
$marca = '// Mu Chila - TESTE (issue do Mario)'
$linha = "1`t10`t0`t0`t0`t10000`t0`t10000`t10"

$t = [IO.File]::ReadAllText($f, $enc)
$tem = $t.Contains($marca)
if ($Ligar -and $tem)       { 'A linha de teste ja esta ligada.'; return }
if ($Desligar -and -not $tem) { 'A linha de teste ja esta desligada.'; return }

Copy-Item $f "$f.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"
if ($Ligar) {
    $t = $t -replace "\r\nend\s*$", "`r`n$marca`: nivel 1-10 com taxa 10, para ver se multiplica ou substitui a taxa do VIP`r`n$linha`r`nend"
} else {
    $t = [regex]::Replace($t, "\r\n" + [regex]::Escape($marca) + "[^\r\n]*\r\n" + [regex]::Escape($linha), '')
}
[IO.File]::WriteAllText($f, $t, $enc)
"Arquivo: " + ($(if ($Ligar) { 'linha de teste LIGADA' } else { 'linha de teste DESLIGADA' }))
Get-Content $f -Tail 3

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

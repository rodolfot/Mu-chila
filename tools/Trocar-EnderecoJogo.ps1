# Troca o endereco que o CLIENTE usa para achar os servidores do jogo, nos arquivos ServerList.dat (ConnectServer)
# e MapServerInfo.dat (Data). Serve para sair do IP do Radmin (26.139.39.123) para um dominio/IP publico e voltar.
# Faz backup antes. NAO aplica sozinho no servidor rodando: depois avisa o que recarregar/reiniciar.
#
#   .\Trocar-EnderecoJogo.ps1                          # mostra o(s) endereco(s) atual(is)
#   .\Trocar-EnderecoJogo.ps1 -Para jogo.seudominio    # troca para o dominio publico
#   .\Trocar-EnderecoJogo.ps1 -Radmin                  # volta para o IP do Radmin (26.139.39.123)
param(
    [string]$Para,
    [switch]$Radmin,
    [string]$IpRadmin = '26.139.39.123',
    [string]$Raiz = 'C:\MuServer'
)
$ErrorActionPreference = 'Stop'
$enc = [Text.Encoding]::GetEncoding(1252)
$arqs = @(
    (Join-Path $Raiz 'ConnectServer\ServerList.dat'),
    (Join-Path $Raiz 'Data\MapServerInfo.dat')
)
foreach ($a in $arqs) { if (-not (Test-Path $a)) { throw "nao achei $a" } }

# endereços atuais (o cliente usa o que esta em ServerList "ADDR" e MapServerInfo Saddr)
$rx = [regex]'(?<![\d.])(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(?![\d.])|(?<=["\sS])([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})'
$atuais = @{}
foreach ($a in $arqs) {
    foreach ($l in [IO.File]::ReadAllLines($a, $enc)) {
        $t = ($l -replace '//.*$', '').Trim(); if (-not $t) { continue }
        foreach ($m in $rx.Matches($t)) { $v = $m.Value; if ($v -and $v -notmatch '^55|^0$') { $atuais[$v] = $true } }
    }
}
$listaAtual = $atuais.Keys | Where-Object { $_ -match '\d+\.\d+\.\d+\.\d+' -or $_ -match '[a-zA-Z]' } | Sort-Object -Unique
Write-Host ("Endereco(s) atual(is) nos arquivos do jogo: " + ($listaAtual -join ', '))

$destino = if ($Radmin) { $IpRadmin } elseif ($Para) { $Para } else { $null }
if (-not $destino) { Write-Host "(Nada trocado. Use -Para <dominio> ou -Radmin.)"; return }

# de qual endereco sair: se houver exatamente um "de jogo" (o do Radmin ou o publico atual), troca ele
$de = $listaAtual | Where-Object { $_ -ne $destino }
if ($de.Count -ne 1) { throw "Nao consegui decidir o endereco de origem automaticamente (achei: $($listaAtual -join ', ')). Edite a mao ou me diga o -De." }
$de = $de[0]
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
foreach ($a in $arqs) {
    Copy-Item $a "$a.bak-$stamp"
    $txt = [IO.File]::ReadAllText($a, $enc).Replace($de, $destino)
    [IO.File]::WriteAllText($a, $txt, $enc)
}
Write-Host "OK  troquei '$de' -> '$destino' em ServerList.dat e MapServerInfo.dat (backup .bak-$stamp)." -ForegroundColor Green
Write-Host "Aplicar no servidor em execucao:"
Write-Host "  - ConnectServer: .\Recarregar-Servidor.ps1 -Servidor ConnectServer -Item ServerList"
Write-Host "  - MapServerInfo so vale reiniciando os GameServers e o Castle Siege (troca de IP dos mapas)."
Write-Host "  - Atualize o cliente/patch dos amigos com o endereco novo."

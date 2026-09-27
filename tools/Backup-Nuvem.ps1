# Copia o backup mais novo de cada banco para uma pasta sincronizada com a nuvem (OneDrive, Google Drive...).
# Assim, se o PC morrer, as contas dos jogadores nao se perdem. Rodar DEPOIS do Backup-Banco.ps1 (ou junto).
# Descubra sua pasta de nuvem (ex.: "$env:USERPROFILE\OneDrive\MuChilaBackup") e passe em -Destino.
param(
    [string]$Origem = 'D:\MuServerBackup\DB',
    [Parameter(Mandatory)][string]$Destino,   # ex.: C:\Users\t4rt1\OneDrive\MuChilaBackup
    [string[]]$Bancos = @('MuOnlineS14', 'MuOnlineS4', 'BattleCore'),
    [int]$Manter = 7
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Destino | Out-Null
$log = Join-Path $Destino 'backup-nuvem.log'
function Log($m) { $l = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; Add-Content $log $l -Encoding utf8; Write-Host $l }

foreach ($b in $Bancos) {
    $novo = Get-ChildItem $Origem -Filter "$b-*.bak" -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $novo) { Log "AVISO: nenhum backup de $b em $Origem"; continue }
    Copy-Item $novo.FullName (Join-Path $Destino $novo.Name) -Force
    Log "copiado $($novo.Name) -> nuvem"
    # limpeza na nuvem: guarda so os $Manter mais novos de cada banco
    Get-ChildItem $Destino -Filter "$b-*.bak" | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Manter | ForEach-Object { Remove-Item $_.FullName -Force; Log "apagado (antigo): $($_.Name)" }
}
Log "backup na nuvem concluido"

# Renova o ciclo do Castle Siege quando a data de termino ja passou, para o servidor do cerco nao se recusar a ligar.
# O Castle Siege e um evento ciclico de 7 dias; ele guarda o inicio/fim do ciclo em MuOnlineS14.dbo.MuCastle_DATA e,
# ao ligar, recusa iniciar se o fim ja passou (erro 0x81). Este script so mexe se o fim JA PASSOU (nao cancela o cerco
# do dia): define inicio = hoje e fim = hoje + $DiasCiclo. Se o Castle Siege estiver parado, ele o religa.
# -Simular mostra o que faria sem gravar nada. -Force renova mesmo que ainda nao tenha vencido (use com cuidado).
param(
    [switch]$Simular,
    [switch]$Force,
    [int]$DiasCiclo = 7,
    [string]$Log = 'D:\MuServerBackup\castle-siege.log'
)
$ErrorActionPreference = 'Stop'
function Log($m) {
    $linha = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"
    try { New-Item -ItemType Directory -Force -Path (Split-Path $Log) | Out-Null; Add-Content -Path $Log -Value $linha -Encoding utf8 } catch {}
    Write-Host $linha
}

# le as datas atuais (formato aaaa-mm-dd)
$linha = (sqlcmd -S .\MUONLINE -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT CONVERT(varchar(10),SIEGE_START_DATE,120)+'|'+CONVERT(varchar(10),SIEGE_END_DATE,120)+'|'+CONVERT(varchar(10),GETDATE(),120) FROM MuOnlineS14.dbo.MuCastle_DATA" | Where-Object { $_ -match '\d{4}-\d{2}-\d{2}' } | Select-Object -First 1).Trim()
if (-not $linha) { Log 'FALHA: nao consegui ler MuCastle_DATA'; exit 1 }
$ini, $fim, $hoje = $linha -split '\|'
# o servidor recusa ligar quando fim <= hoje (checagem iEVENT_END_DATE_NUM <= iTODAY_DATE_NUM); usamos o mesmo criterio
$vencido = [datetime]$fim -le [datetime]$hoje

if (-not $vencido -and -not $Force) {
    Log "ok: ciclo valido (inicio $ini, fim $fim, hoje $hoje); nada a fazer"
    exit 0
}

$motivo = if ($Force) { 'forcado' } else { "vencido (fim $fim < hoje $hoje)" }
if ($Simular) { Log "SIMULACAO: renovaria o ciclo ($motivo) para inicio=$hoje fim=hoje+$DiasCiclo"; exit 0 }

$sql = "UPDATE MuOnlineS14.dbo.MuCastle_DATA SET SIEGE_START_DATE = CAST(GETDATE() AS date), SIEGE_END_DATE = DATEADD(day, $DiasCiclo, CAST(GETDATE() AS date))"
sqlcmd -S .\MUONLINE -E -C -b -Q $sql | Out-Null
if ($LASTEXITCODE -ne 0) { Log "FALHA: UPDATE retornou $LASTEXITCODE"; exit 1 }
Log "ciclo renovado ($motivo): inicio=hoje fim=hoje+$DiasCiclo"

# religa o Castle Siege se estiver parado (o cerco precisa dele no ar)
if (-not (Get-Process 'Castle Siege Server' -ErrorAction SilentlyContinue)) {
    $exe = 'C:\MuServer\GameServerCS\Castle Siege Server.exe'
    if (Test-Path $exe) { Start-Process $exe -WorkingDirectory (Split-Path $exe); Log 'Castle Siege estava parado; religado' }
    else { Log "Castle Siege parado e nao achei $exe; ligue pelo launcher" }
}

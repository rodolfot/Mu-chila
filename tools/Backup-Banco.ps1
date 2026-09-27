# Backup diario dos bancos do Mu Chila (SQL Server Express nao tem SQL Agent, entao usamos o Agendador do Windows).
# Faz um .bak de cada banco do jogo em D:\MuServerBackup\DB, guarda os ultimos $Manter dias e apaga os mais antigos.
# Registrar a tarefa: veja Registrar-BackupBanco.ps1. Restaurar: RESTORE DATABASE <nome> FROM DISK='...bak' WITH REPLACE.
param(
    [string]$Destino = 'D:\MuServerBackup\DB',
    [string[]]$Bancos = @('MuOnlineS14', 'MuOnlineS4', 'BattleCore'),
    [int]$Manter = 14
)
$ErrorActionPreference = 'Stop'
$data = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $Destino 'backup.log'
New-Item -ItemType Directory -Force -Path $Destino | Out-Null

# o servico do SQL (NT Service\MSSQL$MUONLINE) e quem grava o arquivo: garante que ele pode escrever na pasta
$conta = (Get-CimInstance Win32_Service -Filter "Name='MSSQL`$MUONLINE'").StartName
icacls $Destino /grant "${conta}:(OI)(CI)M" /Q | Out-Null

function Log($m) { $linha = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; Add-Content -Path $log -Value $linha -Encoding utf8; Write-Host $linha }

$falhas = 0
foreach ($b in $Bancos) {
    $arq = Join-Path $Destino "$b-$data.bak"
    try {
        $sql = "BACKUP DATABASE [$b] TO DISK = N'$arq' WITH INIT, FORMAT, NAME = N'$b backup diario', STATS = 25"
        sqlcmd -S .\MUONLINE -E -C -b -Q $sql | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $arq)) { throw "sqlcmd retornou $LASTEXITCODE" }
        $mb = [int]((Get-Item $arq).Length / 1MB)
        Log "OK    $b -> $(Split-Path $arq -Leaf) (${mb} MB)"
    }
    catch { $falhas++; Log "FALHA $b : $($_.Exception.Message)" }
}

# limpeza: guarda so os $Manter mais novos de cada banco
foreach ($b in $Bancos) {
    $antigos = Get-ChildItem $Destino -Filter "$b-*.bak" | Sort-Object LastWriteTime -Descending | Select-Object -Skip $Manter
    foreach ($f in $antigos) { Remove-Item $f.FullName -Force; Log "apagado (antigo): $($f.Name)" }
}

if ($falhas -gt 0) { Log "TERMINOU COM $falhas FALHA(S)"; exit 1 }
Log "backup diario concluido ($($Bancos.Count) bancos)"

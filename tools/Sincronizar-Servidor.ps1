# Copia o servidor em uso (C:\MuServer) para a pasta versionada do repositorio,
# sem logs, backups temporarios e pacotes .zip. Rode antes de fazer commit de alteracoes do servidor.
param(
    [string]$Origem = 'C:\MuServer',
    [string]$Destino = (Join-Path $PSScriptRoot '..\3 - MuServer Mu Chila (servidor configurado)')
)

robocopy $Origem $Destino /MIR /COPY:DAT /R:1 /W:1 /NFL /NDL /NP `
    /XD LOG LOG_ACCOUNT CONNECT_LOG HACK_LOG Logs 'Cliente para amigos' `
    /XF '*.bak-*' '*.zip' 'desktop.ini' 'backup-caixas-*.csv'

# robocopy: 0 a 7 = sucesso, 8 ou mais = falha
if ($LASTEXITCODE -ge 8) { throw "robocopy falhou (codigo $LASTEXITCODE)" }
Write-Host "Servidor sincronizado em $Destino"

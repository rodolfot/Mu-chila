# Registra (ou atualiza) a tarefa do Agendador do Windows que roda o Backup-Banco.ps1 todo dia.
# Rodar UMA vez, como administrador. A tarefa roda como SYSTEM (nao precisa de senha e roda mesmo sem login).
param([string]$Hora = '05:00')
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Backup-Banco.ps1'
if (-not (Test-Path $script)) { throw "nao achei $script" }

$acao = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`""
$gatilho = New-ScheduledTaskTrigger -Daily -At $Hora
$conta = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$cfg = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 1)

Register-ScheduledTask -TaskName 'Backup diario do banco' -TaskPath '\Mu Chila\' `
    -Action $acao -Trigger $gatilho -Principal $conta -Settings $cfg `
    -Description 'Backup diario dos bancos do Mu Chila em D:\MuServerBackup\DB (guarda 14 dias).' -Force | Out-Null
"Tarefa 'Mu Chila\Backup diario do banco' registrada para as $Hora."

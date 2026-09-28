# Publica o Mu Chila Admin (painel + vigia) em C:\MuServer\MuChilaAdmin, liga o vigia com o Windows e confere.
# Rodar com o PAINEL FECHADO (o vigia em execução, oficial ou de teste, é fechado pelo próprio script).
# Chaves do vigia (arquivos .ligado na pasta do painel): reset → seleção (#7), comandos curtos (/f /a /v /e /c),
# Passe dos Mapas e Magic Backpack. Para desligar uma: apague o arquivo (o vigia confere a cada volta).
# Uso: .\Publicar-Painel.ps1
$ErrorActionPreference = 'Stop'
$dest = 'C:\MuServer\MuChilaAdmin'; $exe = Join-Path $dest 'MuChilaAdmin.exe'
$proj = Join-Path $PSScriptRoot 'MuChilaAdmin\MuChilaAdmin.csproj'

$abertos = @(Get-CimInstance Win32_Process -Filter "Name='MuChilaAdmin.exe'")
$paineis = @($abertos | Where-Object { $_.CommandLine -notmatch '--vigia-reset' })
if ($paineis) { throw "O painel Mu Chila Admin está aberto (pid $($paineis.ProcessId -join ', ')). Feche e rode de novo." }
foreach ($v in $abertos) { Stop-Process -Id $v.ProcessId -Force; "vigia fechado (pid $($v.ProcessId)): $($v.ExecutablePath)" }
Start-Sleep 2

dotnet publish $proj -c Release -r win-x64 --self-contained false -o $dest -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou' }
"publicado: $exe ($((Get-Item $exe).LastWriteTime))"

foreach ($chave in 'vigia-reset-selecao.ligado', 'vigia-comandos-curtos.ligado', 'vigia-passe-mapas.ligado', 'vigia-mochila.ligado') {
    $f = Join-Path $dest $chave
    if (-not (Test-Path $f)) { Set-Content -Path $f -Value "ligado em $(Get-Date -f 'dd/MM/yyyy HH:mm')"; "chave criada: $chave" }
}

# atalho na pasta Inicializar do usuário: o vigia liga junto com o Windows
$atalho = Join-Path ([Environment]::GetFolderPath('Startup')) 'Mu Chila - Vigia.lnk'
$ws = New-Object -ComObject WScript.Shell; $l = $ws.CreateShortcut($atalho)
$l.TargetPath = $exe; $l.Arguments = '--vigia-reset'; $l.WorkingDirectory = $dest
$l.Description = 'Mu Chila: vigia dos GameServers (ataques, resets, passe dos mapas, avisos e bônus)'; $l.Save()
"atalho: $atalho"

# liga pelo atalho via explorer: se fosse filho deste terminal, morreria junto com ele
Start-Process explorer.exe -ArgumentList "`"$atalho`""
Start-Sleep 6
$r = Join-Path $env:TEMP 'vigia-sonda.txt'; Start-Process $exe -ArgumentList '--vigia-sondar', $r -Wait
Get-Content $r
"--- vigia.log ---"; Get-Content (Join-Path $dest 'vigia.log') -Tail 6

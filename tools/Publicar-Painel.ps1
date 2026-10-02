# Publica o Mu Chila Admin (painel web + vigia) em C:\MuServer\MuChilaAdmin, liga os dois com o Windows e confere.
#   1. confere o .NET: SDK 10 (para publicar) e o runtime do ASP.NET Core 10 (para o painel rodar);
#   2. fecha o painel e o vigia que estiverem rodando (o painel web volta sozinho no fim);
#   3. publica (dotnet publish) e cria as chaves do vigia que faltarem;
#   4. atalhos na pasta Inicializar do Windows: "Mu Chila - Painel" (--web) e "Mu Chila - Vigia" (--vigia-reset);
#   5. se a configuração abre o painel para a rede do Radmin, cria a regra do firewall (porta do painel, só da rede 26.x);
#   6. liga o painel e o vigia e confere (http://localhost:5170/api/saude e a sonda do vigia).
# Chaves do vigia (arquivos .ligado na pasta do painel): reset → seleção (#7), comandos curtos (/f /a /v /e /c),
# Passe dos Mapas, Magic Backpack e classe inicial (#35: a 1ª classe aparece com o nome certo no jogo). Para desligar uma:
# apague o arquivo (o vigia confere a cada volta; comandos curtos e classe inicial só voltam ao original reiniciando o GameServer).
# A configuração (banco, pastas, porta, endereços) fica em %ProgramData%\MuChilaAdmin\muchila-admin.json; sem o arquivo
# valem os padrões deste PC. Exemplo comentado: tools\MuChilaAdmin\muchila-admin.exemplo.json. Guia: docs\MU-ADMIN.md.
# Uso: .\Publicar-Painel.ps1          (a regra do firewall precisa do PowerShell como administrador)
$ErrorActionPreference = 'Stop'
$dest = 'C:\MuServer\MuChilaAdmin'; $exe = Join-Path $dest 'MuChilaAdmin.exe'
$proj = Join-Path $PSScriptRoot 'MuChilaAdmin\MuChilaAdmin.csproj'
function Passo($t) { Write-Host "`n== $t" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1. .NET
Passo '1. .NET'
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { throw 'O .NET não está instalado. Instale o SDK 10 (x64): https://dotnet.microsoft.com/download/dotnet/10.0' }
if (-not (@(& $dotnet --list-sdks) -match '^10\.')) { throw 'Falta o .NET SDK 10 (x64) para publicar: https://dotnet.microsoft.com/download/dotnet/10.0' }
$aspnet = @(& $dotnet --list-runtimes) -match '^Microsoft\.AspNetCore\.App 10\.'
if (-not $aspnet) {
    throw "Falta o runtime do ASP.NET Core 10, que o painel web usa. Instale com:`n  winget install Microsoft.DotNet.AspNetCore.10`n" +
          "ou baixe 'ASP.NET Core Runtime 10 (Hosting Bundle ou x64)' em https://dotnet.microsoft.com/download/dotnet/10.0"
}
"ok: $($aspnet[-1])"

# ---------------------------------------------------------------- 2. fechar o que estiver rodando
Passo '2. Fechando o painel e o vigia'
foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name='MuChilaAdmin.exe'")) {
    Stop-Process -Id $p.ProcessId -Force
    $papel = if ($p.CommandLine -match '--vigia-reset') { 'vigia' } else { 'painel' }
    "$papel fechado (pid $($p.ProcessId)): $($p.ExecutablePath)"
}
Start-Sleep 2

# ---------------------------------------------------------------- 3. publicar
Passo "3. Publicando em $dest"
dotnet publish $proj -c Release -r win-x64 --self-contained false -o $dest -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou' }
"publicado: $exe ($((Get-Item $exe).LastWriteTime))"
foreach ($chave in 'vigia-reset-selecao.ligado', 'vigia-comandos-curtos.ligado', 'vigia-passe-mapas.ligado', 'vigia-mochila.ligado', 'vigia-classe-inicial.ligado') {
    $f = Join-Path $dest $chave
    if (-not (Test-Path $f)) { Set-Content -Path $f -Value "ligado em $(Get-Date -f 'dd/MM/yyyy HH:mm')"; "chave criada: $chave" }
}

# ---------------------------------------------------------------- 4. atalhos de inicialização
Passo '4. Atalhos (ligam junto com o Windows)'
$ws = New-Object -ComObject WScript.Shell
function Atalho($nome, $argumentos, $descricao) {
    $caminho = Join-Path ([Environment]::GetFolderPath('Startup')) "$nome.lnk"
    $l = $ws.CreateShortcut($caminho)
    $l.TargetPath = $exe; $l.Arguments = $argumentos; $l.WorkingDirectory = $dest; $l.Description = $descricao; $l.Save()
    Write-Host "atalho: $caminho"
    $caminho
}
$atalhoVigia = Atalho 'Mu Chila - Vigia' '--vigia-reset' 'Mu Chila: vigia dos GameServers (ataques, resets, passe dos mapas, avisos e bônus)'
$atalhoPainel = Atalho 'Mu Chila - Painel' '--web' 'Mu Chila Admin: painel web (http://localhost:5170), ícone na bandeja'
# atalho na área de trabalho: abre o painel no navegador (liga o painel se estiver desligado)
$l = $ws.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Mu Chila Admin.lnk'))
$l.TargetPath = $exe; $l.WorkingDirectory = $dest; $l.Description = 'Abrir o painel Mu Chila Admin'; $l.IconLocation = "$exe,0"; $l.Save()

# ---------------------------------------------------------------- 5. firewall (só se o painel abre para a rede do Radmin)
Passo '5. Firewall'
$cfgArq = Join-Path $env:ProgramData 'MuChilaAdmin\muchila-admin.json'
$porta = 5170; $enderecos = @('localhost')
if (Test-Path $cfgArq) {
    try {
        # o painel aceita as linhas de comentário "//" do arquivo (o modelo tem várias); o ConvertFrom-Json do PowerShell 5.1 não
        $texto = (Get-Content $cfgArq -Raw -Encoding UTF8) -replace '(?m)^\s*//.*$', ''
        $cfg = $texto | ConvertFrom-Json
        if ($cfg.Web.Porta) { $porta = [int]$cfg.Web.Porta }
        if ($cfg.Web.Enderecos) { $enderecos = @($cfg.Web.Enderecos) }
    }
    catch { Write-Warning "não consegui ler $cfgArq ($($_.Exception.Message)): confira o arquivo. A regra do firewall não foi mexida." }
}
$regra = 'Mu Chila Admin (painel web, Radmin)'
if (@($enderecos | Where-Object { $_ -notin 'localhost', '127.0.0.1', '::1' }).Count -eq 0) {
    "o painel só atende este PC (Enderecos = localhost): sem regra no firewall"
    if (Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue) { "atenção: a regra '$regra' existe de antes; apague-a se não usa mais o painel pela rede" }
} else {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $cmd = "New-NetFirewallRule -DisplayName '$regra' -Direction Inbound -Protocol TCP -LocalPort $porta -RemoteAddress 26.0.0.0/8 -Action Allow -Profile Any"
    if ($admin) {
        Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue | Remove-NetFirewallRule
        Invoke-Expression $cmd | Out-Null
        "regra '$regra': porta $porta liberada só para a rede do Radmin (26.0.0.0/8)"
    } else {
        "atenção: rode como administrador para criar a regra do firewall, ou rode este comando num PowerShell de administrador:`n  $cmd"
    }
}

# ---------------------------------------------------------------- 6. ligar e conferir
Passo '6. Ligando e conferindo'
# pelos atalhos via explorer: se fossem filhos deste terminal, morreriam junto com ele
Start-Process explorer.exe -ArgumentList "`"$atalhoPainel`""
Start-Process explorer.exe -ArgumentList "`"$atalhoVigia`""
$saude = $null
for ($i = 0; $i -lt 30 -and -not $saude; $i++) {
    Start-Sleep 1
    try { $saude = Invoke-RestMethod "http://localhost:$porta/api/saude" -TimeoutSec 2 } catch { }
}
if ($saude) { "painel no ar: http://localhost:$porta (versão $($saude.versao))" }
else { Write-Warning "o painel não respondeu em http://localhost:$porta/api/saude. Veja $dest\painel-erros.log" }
$r = Join-Path $env:TEMP 'vigia-sonda.txt'; Start-Process $exe -ArgumentList '--vigia-sondar', $r -Wait
Get-Content $r
"--- vigia.log ---"; Get-Content (Join-Path $dest 'vigia.log') -Tail 6 -ErrorAction SilentlyContinue
"`nPrimeira vez? Abra http://localhost:$porta NESTE PC e crie o administrador do painel (a tela de primeiro acesso só abre aqui)."

# Prepara o firewall do Windows para o Mu Chila. Por PADRAO so MOSTRA o que faria (nao mexe em nada).
# Para aplicar de verdade, rode com -Aplicar (de preferencia logado localmente, nao por Area de Trabalho Remota).
#
# Ideia: liberar tudo pela rede do Radmin (para os jogadores e o admin de hoje nao caírem) e, na internet,
# abrir SO as portas do jogo (se -Jogo) e NUNCA o SQL/RDP/DataServer/JoinServer/site.
# O site fica seguro pelo tunel da Cloudflare (o PC conecta de dentro para fora; nao precisa abrir a porta 80).
#
#   .\Firewall-Servidor.ps1                 # so mostra o plano
#   .\Firewall-Servidor.ps1 -Jogo           # mostra o plano incluindo as portas publicas do jogo (Caminho A)
#   .\Firewall-Servidor.ps1 -Aplicar -Jogo  # aplica: liga o firewall, cria as regras (jogo publico)
#   .\Firewall-Servidor.ps1 -Aplicar        # aplica sem expor o jogo (Caminho B: jogo passa pela ponte/VPS)
param(
    [switch]$Aplicar,
    [switch]$Jogo,
    [string]$RadminInterface = 'Radmin VPN'
)
$ErrorActionPreference = 'Stop'
$prefixo = 'Mu Chila -'

# portas que o CLIENTE precisa alcancar quando o jogo for publico (Caminho A)
$jogoTCP = 44405, 55901, 55902, 55903, 55904, 55919   # ConnectServer + GameServers + Castle Siege
$jogoUDP = 55557                                       # contagem de jogadores na lista de servidores
# portas que NUNCA devem ficar abertas para a internet (ficam so na rede local/Radmin)
$nuncaExpor = @{ 'SQL Server' = 1433; 'Area de Trabalho Remota (RDP)' = 3389; 'DataServer' = 55960; 'JoinServer' = 55970; 'Site HTTP' = 80 }

Write-Host "=== Plano do firewall ===" -ForegroundColor Cyan
Write-Host "Radmin ($RadminInterface): LIBERAR TUDO (jogadores e admin de hoje continuam funcionando)."
Write-Host "Internet/LAN: bloquear entrada por padrao."
if ($Jogo) {
    Write-Host "Jogo PUBLICO (Caminho A) - abrir para a internet:"
    Write-Host ("  TCP: " + ($jogoTCP -join ', '))
    Write-Host ("  UDP: " + ($jogoUDP -join ', '))
} else {
    Write-Host "Jogo NAO exposto (Caminho B: passa pela ponte/VPS, ou segue so no Radmin)."
}
Write-Host ("Nunca exposto para a internet: " + (($nuncaExpor.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '))
Write-Host "Site: pela Cloudflare Tunnel (nao abre a porta 80)."

if (-not $Aplicar) {
    Write-Host "`n(Somente demonstracao. Rode com -Aplicar para valer.)" -ForegroundColor Yellow
    return
}

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrator')) {
    throw 'Precisa rodar como administrador para mexer no firewall.'
}

# 1) regra que libera TUDO pela interface do Radmin (os jogadores de hoje entram por ai)
if (Get-NetAdapter -Name $RadminInterface -EA SilentlyContinue) {
    Remove-NetFirewallRule -DisplayName "$prefixo Radmin (liberar tudo)" -EA SilentlyContinue
    New-NetFirewallRule -DisplayName "$prefixo Radmin (liberar tudo)" -Direction Inbound -Action Allow `
        -InterfaceAlias $RadminInterface -Profile Any | Out-Null
    Write-Host "OK  liberado tudo na interface $RadminInterface"
} else {
    Write-Warning "Interface '$RadminInterface' nao encontrada; confira o nome com Get-NetAdapter."
}

# 2) regras do jogo publico (so com -Jogo) para todas as interfaces
Remove-NetFirewallRule -DisplayName "$prefixo Jogo (TCP)" -EA SilentlyContinue
Remove-NetFirewallRule -DisplayName "$prefixo Jogo (UDP)" -EA SilentlyContinue
if ($Jogo) {
    New-NetFirewallRule -DisplayName "$prefixo Jogo (TCP)" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $jogoTCP -Profile Any | Out-Null
    New-NetFirewallRule -DisplayName "$prefixo Jogo (UDP)" -Direction Inbound -Action Allow -Protocol UDP -LocalPort $jogoUDP -Profile Any | Out-Null
    Write-Host "OK  portas do jogo abertas (TCP $($jogoTCP -join ',') / UDP $($jogoUDP -join ','))"
}

# 3) bloqueios explicitos do que nunca pode ir para a internet (reforco; o padrao ja bloqueia entrada)
foreach ($item in $nuncaExpor.GetEnumerator()) {
    $nome = "$prefixo Bloquear $($item.Key) na internet"
    Remove-NetFirewallRule -DisplayName $nome -EA SilentlyContinue
    New-NetFirewallRule -DisplayName $nome -Direction Inbound -Action Block -Protocol TCP -LocalPort $item.Value -Profile Public | Out-Null
}
Write-Host "OK  bloqueios de SQL/RDP/DataServer/JoinServer/Site no perfil Publico"

# 4) liga o firewall nos tres perfis, com entrada bloqueada por padrao e saida liberada
Set-NetFirewallProfile -Profile Domain, Private, Public -Enabled True -DefaultInboundAction Block -DefaultOutboundAction Allow
Write-Host "OK  firewall LIGADO (entrada bloqueada por padrao, saida liberada)" -ForegroundColor Green
Write-Host "Confira que voce ainda alcanca o servidor pelo Radmin e que os jogadores entram."

# DDNS: mantem um registro A na Cloudflare apontando para o IP publico atual da sua casa (que muda de vez em quando).
# Sem isso, quando a operadora troca seu IP, o dominio para de achar o servidor. Rode por uma tarefa a cada 15 min.
#
# Segredos NAO ficam no repositorio: crie C:\MuServer\Site\config-local\cloudflare-ddns.json com:
#   { "token": "TOKEN_DA_API", "zona": "seudominio.com.br", "registros": ["jogo.seudominio.com.br"] }
# O token e um "API Token" da Cloudflare com permissao Zone.DNS:Edit so na sua zona.
param(
    [string]$Config = 'C:\MuServer\Site\config-local\cloudflare-ddns.json',
    [string]$Log = 'D:\MuServerBackup\ddns.log'
)
$ErrorActionPreference = 'Stop'
function Log($m) { $l = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; try { New-Item -ItemType Directory -Force (Split-Path $Log) | Out-Null; Add-Content $Log $l -Encoding utf8 } catch {}; Write-Host $l }

if (-not (Test-Path $Config)) { Log "FALHA: falta o arquivo de config $Config (veja o cabecalho do script)"; exit 1 }
$cfg = Get-Content $Config -Raw | ConvertFrom-Json
$hdr = @{ Authorization = "Bearer $($cfg.token)"; 'Content-Type' = 'application/json' }

$ip = (Invoke-RestMethod 'https://api.ipify.org?format=json' -TimeoutSec 15).ip
if ($ip -notmatch '^\d{1,3}(\.\d{1,3}){3}$') { Log "FALHA: nao consegui descobrir o IP publico"; exit 1 }

$zonaId = (Invoke-RestMethod "https://api.cloudflare.com/client/v4/zones?name=$($cfg.zona)" -Headers $hdr).result[0].id
if (-not $zonaId) { Log "FALHA: zona $($cfg.zona) nao encontrada na conta"; exit 1 }

$mudou = 0
foreach ($nome in $cfg.registros) {
    $reg = (Invoke-RestMethod "https://api.cloudflare.com/client/v4/zones/$zonaId/dns_records?type=A&name=$nome" -Headers $hdr).result[0]
    $corpo = @{ type = 'A'; name = $nome; content = $ip; ttl = 120; proxied = $false } | ConvertTo-Json
    if (-not $reg) {
        Invoke-RestMethod "https://api.cloudflare.com/client/v4/zones/$zonaId/dns_records" -Method Post -Headers $hdr -Body $corpo | Out-Null
        Log "criado $nome -> $ip"; $mudou++
    } elseif ($reg.content -ne $ip) {
        Invoke-RestMethod "https://api.cloudflare.com/client/v4/zones/$zonaId/dns_records/$($reg.id)" -Method Put -Headers $hdr -Body $corpo | Out-Null
        Log "atualizado $nome : $($reg.content) -> $ip"; $mudou++
    }
}
if ($mudou -eq 0) { Log "ok: IP $ip sem mudanca" }

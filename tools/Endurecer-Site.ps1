# Endurece o Apache/PHP do site do Mu Chila (o que ainda faltava). Idempotente e com backup.
# Por PADRAO so mostra o que faria. Com -Aplicar, edita, valida com "httpd -t" e reinicia o Apache se a config estiver ok.
#
# O que muda:
#  1) httpd.conf: desliga a LISTAGEM DE DIRETORIO (Options Indexes -> -Indexes) na pasta www.
#  2) httpd.conf: cabecalhos de seguranca (X-Content-Type-Options, X-Frame-Options, Referrer-Policy) via mod_headers.
#     (HSTS fica comentado; ligar so quando o site estiver 100% em HTTPS.)
#  3) php.ini: session.use_strict_mode = 1.
# Ja estavam ok (nao mexe): ServerTokens Prod, ServerSignature Off, TraceEnable Off, expose_php Off,
#   display_errors Off, log_errors On, session.cookie_httponly = 1.
#
#   .\Endurecer-Site.ps1            # mostra o que faria
#   .\Endurecer-Site.ps1 -Aplicar   # aplica, valida e reinicia o Apache
param([switch]$Aplicar, [string]$Raiz = 'C:\MuServer\Site')
$ErrorActionPreference = 'Stop'
$httpdConf = Join-Path $Raiz 'apache\conf\httpd.conf'
$phpIni = Join-Path $Raiz 'php\php.ini'
$httpdExe = Join-Path $Raiz 'apache\bin\httpd.exe'
$marca = '# --- Mu Chila: cabecalhos de seguranca ---'

$plano = @()
$conf = Get-Content $httpdConf -Raw
if ($conf -match 'Options Indexes FollowSymLinks') { $plano += 'httpd.conf: desligar listagem de diretorio (Options -Indexes)' }
if ($conf -notmatch [regex]::Escape($marca)) { $plano += 'httpd.conf: adicionar cabecalhos de seguranca (mod_headers)' }
$ini = Get-Content $phpIni -Raw
if ($ini -match '(?m)^\s*session\.use_strict_mode\s*=\s*0') { $plano += 'php.ini: session.use_strict_mode = 1' }

if ($plano.Count -eq 0) { Write-Host 'Nada a fazer: o site ja esta endurecido.' -ForegroundColor Green; return }
Write-Host '=== O que sera feito ==='; $plano | ForEach-Object { Write-Host "  - $_" }
if (-not $Aplicar) { Write-Host "`n(Somente demonstracao. Rode com -Aplicar para valer.)" -ForegroundColor Yellow; return }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Copy-Item $httpdConf "$httpdConf.bak-$stamp"; Copy-Item $phpIni "$phpIni.bak-$stamp"

# 1) desliga listagem de diretorio
$conf = $conf -replace 'Options Indexes FollowSymLinks', 'Options -Indexes +FollowSymLinks'
# 2) cabecalhos de seguranca (uma vez so)
if ($conf -notmatch [regex]::Escape($marca)) {
    $bloco = @"

$marca
<IfModule headers_module>
    Header always set X-Content-Type-Options "nosniff"
    Header always set X-Frame-Options "SAMEORIGIN"
    Header always set Referrer-Policy "strict-origin-when-cross-origin"
    # HSTS: ligar SO quando o site estiver 100% em HTTPS (senao trava o acesso por http)
    # Header always set Strict-Transport-Security "max-age=31536000; includeSubDomains"
</IfModule>
# --- fim ---
"@
    $conf = $conf.TrimEnd() + "`r`n" + $bloco + "`r`n"
}
[IO.File]::WriteAllText($httpdConf, $conf, (New-Object Text.UTF8Encoding($false)))

# 3) php.ini
$ini = $ini -replace '(?m)^\s*session\.use_strict_mode\s*=\s*0', 'session.use_strict_mode = 1'
[IO.File]::WriteAllText($phpIni, $ini, (New-Object Text.UTF8Encoding($false)))

# valida a config do Apache antes de reiniciar
$teste = & $httpdExe -t -f $httpdConf 2>&1
if ($LASTEXITCODE -ne 0) {
    Copy-Item "$httpdConf.bak-$stamp" $httpdConf -Force; Copy-Item "$phpIni.bak-$stamp" $phpIni -Force
    throw "httpd -t reprovou a config; desfiz tudo. Saida: $teste"
}
Write-Host "OK  config validada: $teste"

# reinicia o Apache do site. O site liga como PROCESSO (nao como servico), entao "-k restart" nao serve:
# paramos o(s) httpd de C:\MuServer\Site e subimos de novo (igual ao "Ligar Site.bat").
$p = @(Get-Process httpd -EA SilentlyContinue | Where-Object { $_.Path -like "$Raiz\*" })
if ($p.Count -gt 0) {
    $p | Stop-Process -Force
    Start-Sleep 2
    Start-Process $httpdExe -WorkingDirectory (Join-Path $Raiz 'apache') -WindowStyle Hidden
    Start-Sleep 2
    if (Get-Process httpd -EA SilentlyContinue | Where-Object { $_.Path -like "$Raiz\*" }) { Write-Host 'OK  Apache reiniciado com a config endurecida' -ForegroundColor Green }
    else { throw 'Apache nao voltou apos o restart; veja logs\apache-erro.log' }
} else { Write-Host 'Apache nao estava rodando; as mudancas valem quando ligar o site.' }

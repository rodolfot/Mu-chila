# Prepara um PC de desenvolvimento para rodar e testar o Mu Chila Admin (painel web) SEM mexer no servidor de verdade:
#   1. confere o .NET SDK 10 (necessário para compilar) e o SQL Server;
#   2. restaura o banco do kit (DB\MuOnlineS14.bak do repositório) no SQL Server deste PC e aplica os scripts que o painel
#      e o site usam (resets, passe dos mapas, pedidos, mercado, loja de itens, Contate-nos, Zen);
#   3. copia a pasta do servidor do repositório para <Pasta>\MuServer e as partes do cliente que o painel edita para
#      <Pasta>\Cliente (o painel grava arquivos: lojas, drops, loja de cash... nada disso suja o repositório);
#   4. grava a configuração de desenvolvimento em %ProgramData%\MuChilaAdmin\muchila-admin.json;
#   5. baixa o Tailwind CSS (tools\MuChilaAdmin\.ferramentas\tailwindcss.exe) para o build gerar o CSS;
#   6. compila o painel e cria o usuário "dev" (administrador) com uma senha temporária.
# Depois: dotnet run --project tools\MuChilaAdmin  (ou o .exe em tools\MuChilaAdmin\bin\Debug\net10.0-windows) e abra
# http://localhost:5170. Para voltar a usar o banco/pastas de produção neste PC, apague o muchila-admin.json.
# Uso: .\Preparar-Ambiente-Dev.ps1 [-Instancia .\SQLEXPRESS] [-Pasta C:\MuChilaDev] [-RecriarBanco] [-SemCopia] [-Php C:\caminho\do\php]
# -Php: pasta de um PHP 8 para Windows (a do servidor, Site\php) para testar a página Notícias, que passa pelo PHP do site.
param(
    [string]$Instancia = '',
    [string]$Pasta = 'C:\MuChilaDev',
    [int]$Porta = 5170,
    [switch]$RecriarBanco,
    [switch]$SemCopia,
    [string]$Php = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$servidorRepo = Join-Path $repo '3 - MuServer Mu Chila (servidor configurado)'
$clienteRepo = Join-Path $repo '2 - Cliente Season 14 Full'
$projeto = Join-Path $PSScriptRoot 'MuChilaAdmin\MuChilaAdmin.csproj'
$utf8 = New-Object Text.UTF8Encoding($false)
function Passo($t) { Write-Host "`n== $t" -ForegroundColor Cyan }
function Ok($t) { Write-Host "   ok: $t" -ForegroundColor Green }
function Aviso($t) { Write-Host "   atenção: $t" -ForegroundColor Yellow }

# ---------------------------------------------------------------- 1. ferramentas
Passo '1. Ferramentas'
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { throw 'O .NET SDK 10 não está instalado. Baixe em https://dotnet.microsoft.com/download/dotnet/10.0 (SDK x64) e rode de novo.' }
$sdks = @(& $dotnet --list-sdks)
if (-not ($sdks -match '^10\.')) { throw "O .NET SDK 10 não foi encontrado (achei: $($sdks -join ', ')). Baixe em https://dotnet.microsoft.com/download/dotnet/10.0" }
Ok ".NET SDK: $(($sdks -match '^10\.')[0])"
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'O sqlcmd não foi encontrado. Instale o SQL Server (Developer ou Express) com as ferramentas de linha de comando.' }
if ($Instancia -eq '') {
    $Instancia = if (Get-Service 'MSSQL$MUONLINE' -ErrorAction SilentlyContinue) { '.\MUONLINE' } elseif (Get-Service 'MSSQLSERVER' -ErrorAction SilentlyContinue) { '.' } elseif (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue) { '.\SQLEXPRESS' } else { throw 'Nenhum SQL Server encontrado neste PC. Instale o SQL Server Developer/Express ou informe -Instancia.' }
}
$versao = sqlcmd -S $Instancia -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('ProductVersion') AS varchar(20))"
if ($LASTEXITCODE -ne 0) { throw "Não consegui conectar no SQL Server $Instancia com o usuário do Windows." }
Ok "SQL Server $Instancia ($($versao.Trim()))"

# ---------------------------------------------------------------- 2. banco
Passo '2. Banco MuOnlineS14 (cópia do kit, só para desenvolvimento)'
function Sql([string]$arquivo, [string]$cp = '65001') {
    sqlcmd -S $Instancia -E -C -d MuOnlineS14 -I -b -f $cp -i $arquivo | Out-Null
    if ($LASTEXITCODE -ne 0) { Aviso "falhou: $(Split-Path $arquivo -Leaf)" } else { Ok (Split-Path $arquivo -Leaf) }
}
$existe = (sqlcmd -S $Instancia -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'MuOnlineS14'").Trim() -eq '1'
if ($existe -and $RecriarBanco) {
    sqlcmd -S $Instancia -E -C -b -Q "ALTER DATABASE [MuOnlineS14] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [MuOnlineS14];" | Out-Null
    $existe = $false; Ok 'banco antigo apagado (-RecriarBanco)'
}
if (-not $existe) {
    $bak = Join-Path $servidorRepo 'DB\MuOnlineS14.bak'
    # o serviço do SQL é quem lê o .bak: copia para <Pasta>\bak e dá leitura à conta do serviço (como o Backup-Banco.ps1)
    $pastaDados = (sqlcmd -S $Instancia -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))").Trim()
    $pastaBak = Join-Path $Pasta 'bak'
    New-Item -ItemType Directory -Force $pastaBak | Out-Null
    $nomeServico = if ($Instancia -match '\\(.+)$') { 'MSSQL$' + $Matches[1] } else { 'MSSQLSERVER' }
    $conta = (Get-CimInstance Win32_Service -Filter "Name='$nomeServico'").StartName
    if ($conta) { icacls $pastaBak /grant "${conta}:(OI)(CI)RX" /Q | Out-Null }
    $copia = Join-Path $pastaBak 'MuOnlineS14-kit.bak'
    Copy-Item $bak $copia -Force
    $sql = "RESTORE DATABASE [MuOnlineS14] FROM DISK = N'$copia' WITH MOVE N'MuOnline_Data' TO N'$($pastaDados.TrimEnd('\'))\MuOnlineS14.mdf', MOVE N'MuOnline_Log' TO N'$($pastaDados.TrimEnd('\'))\MuOnlineS14_1.ldf', RECOVERY, REPLACE"
    sqlcmd -S $Instancia -E -C -b -Q $sql | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'A restauração do banco do kit falhou (veja a mensagem acima).' }
    Remove-Item $copia -ErrorAction SilentlyContinue
    sqlcmd -S $Instancia -E -C -b -Q "ALTER DATABASE [MuOnlineS14] SET COMPATIBILITY_LEVEL = 140" | Out-Null
    Ok 'banco do kit restaurado'
    $querys = Join-Path $servidorRepo 'DB\1 - Querys'
    foreach ($f in 'EffectList.txt', 'GiftNewbies.txt', 'RuudMoney.txt', 'TotalPKCount.txt', 'Golden Archer.txt') { Sql (Join-Path $querys $f) '1252' }
    Get-ChildItem (Join-Path $querys 'Hunting') -File | ForEach-Object { Sql $_.FullName '1252' }
    Get-ChildItem (Join-Path $querys 'Correcoes (Gremory e RestoreItem)') -Filter *.sql | ForEach-Object { Sql $_.FullName '1252' }
    # o MuChila-Resets.sql registra os créditos na tabela de configuração do WebEngine (o site não existe neste PC): cria uma vazia
    sqlcmd -S $Instancia -E -C -d MuOnlineS14 -b -Q "IF OBJECT_ID('dbo.WEBENGINE_CREDITS_CONFIG') IS NULL CREATE TABLE dbo.WEBENGINE_CREDITS_CONFIG (config_id INT IDENTITY(1,1) PRIMARY KEY, config_title VARCHAR(100), config_database VARCHAR(100), config_table VARCHAR(100), config_credits_col VARCHAR(100), config_user_col VARCHAR(100), config_user_col_id VARCHAR(100), config_checkonline TINYINT, config_display TINYINT)" | Out-Null
    Sql (Join-Path $querys 'MuChila-Resets.sql')
    Sql (Join-Path $querys 'MuChila-PasseMapas.sql')
    $siteSql = Join-Path $servidorRepo 'Site\muchila\sql'
    foreach ($f in 'MUCHILA_PEDIDOS.sql', 'MUCHILA_MERCADO.sql', 'MUCHILA_LOJAITENS.sql', 'MUCHILA_SITE.sql') { Sql (Join-Path $siteSql $f) }
    sqlcmd -S $Instancia -E -C -d MuOnlineS14 -b -Q "UPDATE dbo.MuCastle_DATA SET SIEGE_START_DATE = CAST(GETDATE() AS date), SIEGE_END_DATE = DATEADD(day, 7, CAST(GETDATE() AS date))" | Out-Null
} else { Ok 'o banco MuOnlineS14 já existe (use -RecriarBanco para restaurar de novo)' }
# contador de séries dos itens: no servidor de verdade o GameServer cria a linha ao ligar; aqui ele não liga (colocar item
# pelo painel e a loja de itens do site precisam do WZ_GetItemSerial)
sqlcmd -S $Instancia -E -C -d MuOnlineS14 -b -Q "IF NOT EXISTS (SELECT 1 FROM dbo.GameServerInfo) INSERT INTO dbo.GameServerInfo (Number, ItemCount, ZenCount, AceItemCount) VALUES (0, 900000000, 0, 0)" | Out-Null

# ---------------------------------------------------------------- 3. pastas
Passo "3. Cópias da pasta do servidor e do cliente em $Pasta"
if (-not $SemCopia) {
    $q = @('/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1')
    # php: o PHP do site não vai para o repositório (.gitignore); a cópia mantém o que já estiver lá
    robocopy $servidorRepo (Join-Path $Pasta 'MuServer') /MIR /XD MuChilaAdmin LOG Logs php @q | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'robocopy da pasta do servidor falhou' }
    if ($Php -ne '') {
        if (-not (Test-Path (Join-Path $Php 'php.exe'))) { throw "Não achei php.exe em $Php" }
        robocopy $Php (Join-Path $Pasta 'MuServer\Site\php') /MIR @q | Out-Null
        if ($LASTEXITCODE -ge 8) { throw 'robocopy do PHP falhou' }
        Ok "PHP do site copiado de $Php (a página Notícias usa)"
    }
    foreach ($sub in 'Data\Local', 'Data\InGameShopScript') {
        robocopy (Join-Path $clienteRepo $sub) (Join-Path $Pasta "Cliente\$sub") /MIR @q | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy de $sub falhou" }
    }
    Ok "servidor em $Pasta\MuServer, cliente (partes que o painel edita) em $Pasta\Cliente"
} else { Ok 'cópias mantidas (-SemCopia)' }

# ---------------------------------------------------------------- 4. configuração
Passo '4. Configuração de desenvolvimento'
$cfgDir = Join-Path $env:ProgramData 'MuChilaAdmin'
New-Item -ItemType Directory -Force $cfgDir | Out-Null
$cfg = [ordered]@{
    Banco            = "Server=$Instancia;Database=MuOnlineS14;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=5"
    PastaServidor    = (Join-Path $Pasta 'MuServer')
    PastaCliente     = (Join-Path $Pasta 'Cliente')
    PastaRepositorio = $repo
    Ambiente         = 'desenvolvimento'
    Web              = [ordered]@{ Porta = $Porta; Enderecos = @('localhost'); MinutosSessao = 480; AbrirNavegador = $true }
}
$arquivoCfg = Join-Path $cfgDir 'muchila-admin.json'
[IO.File]::WriteAllText($arquivoCfg, ($cfg | ConvertTo-Json -Depth 4), $utf8)
Ok $arquivoCfg

# ---------------------------------------------------------------- 5. Tailwind
Passo '5. Tailwind CSS (gera o CSS do painel no build)'
$tw = Join-Path $PSScriptRoot 'MuChilaAdmin\.ferramentas\tailwindcss.exe'
if (-not (Test-Path $tw)) {
    New-Item -ItemType Directory -Force (Split-Path $tw) | Out-Null
    Invoke-WebRequest 'https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-windows-x64.exe' -OutFile $tw -UseBasicParsing
}
Ok "$((& $tw --help 2>&1 | Select-Object -First 1))"

# ---------------------------------------------------------------- 6. build e usuário
Passo '6. Compilar e criar o usuário dev'
& $dotnet build $projeto -c Debug -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'O build falhou.' }
$exe = Join-Path $PSScriptRoot 'MuChilaAdmin\bin\Debug\net10.0-windows\MuChilaAdmin.exe'
$saida = Join-Path $env:TEMP 'muchila-usuario-dev.txt'
$existeDev = (sqlcmd -S $Instancia -E -C -d MuOnlineS14 -h -1 -W -Q "SET NOCOUNT ON; IF OBJECT_ID('dbo.MUCHILA_ADMIN_USUARIOS') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM dbo.MUCHILA_ADMIN_USUARIOS WHERE usuario = 'dev'").Trim()
$p = Start-Process $exe -ArgumentList $(if ($existeDev -eq '1') { @('--redefinir-senha', 'dev', $saida) } else { @('--criar-usuario', 'dev', 'admin', $saida) }) -Wait -PassThru
Get-Content $saida -Encoding UTF8; Remove-Item $saida -ErrorAction SilentlyContinue
if ($p.ExitCode -ne 0) { throw 'Não consegui criar o usuário dev.' }

Write-Host "`nPronto. Rode:  dotnet run --project `"$projeto`"   e abra http://localhost:$Porta (usuário dev, senha acima)." -ForegroundColor Green

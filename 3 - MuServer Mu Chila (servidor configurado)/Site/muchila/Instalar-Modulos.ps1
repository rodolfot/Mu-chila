# Instala o código do Mu Chila dentro do WebEngine (C:\MuServer\Site\www). Pode rodar de novo a qualquer momento
# (depois de atualizar o WebEngine ou de mudar algo em muchila\www): cada ajuste confere se já foi feito.
#   1. copia muchila\www\* para www (loja, painel de pedidos, aviso do Mercado Pago, configurações)
#   2. class.database.php e webengine.php: conexão com instância nomeada sem porta (.\MUONLINE) e certificado local aceito
#   3. menu do jogador (usercp.json): item "Loja: VIP e Cash"; a doação por PayPal (não usada) sai do menu
#   4. idioma português: texto do item do menu
#   5. painel admin: grupo "Mu Chila" com "Pedidos da loja"
#   6. desliga módulos que não funcionam aqui (reset do site, comprar zen, votar, esqueci a senha) e troca o link de senha no login
#   7. downloads: cliente, patch e LEIA-ME de C:\MuServer\Cliente para amigos (Apache /arquivos/) e cache da página
param([string]$Site = 'C:\MuServer\Site')
$ErrorActionPreference = 'Stop'
$www = Join-Path $Site 'www'
$utf8 = New-Object Text.UTF8Encoding($false)
if (-not (Test-Path (Join-Path $www 'includes\webengine.php'))) { throw "WebEngine não encontrado em $www" }

function Ajustar([string]$arquivo, [string]$marca, [scriptblock]$mudanca, [string]$descricao) {
    $caminho = Join-Path $www $arquivo
    $texto = [IO.File]::ReadAllText($caminho, $utf8)
    if ($texto.Contains($marca)) { "ja feito: $descricao"; return }
    $novo = & $mudanca $texto
    if ($novo -eq $texto -or -not $novo.Contains($marca)) { throw "Não consegui aplicar: $descricao ($arquivo mudou de formato?)" }
    Copy-Item $caminho "$caminho.original" -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText($caminho, $novo, $utf8)
    "aplicado: $descricao"
}

# 1. arquivos do Mu Chila
$origem = Join-Path $PSScriptRoot 'www'
Get-ChildItem $origem -Recurse -File | ForEach-Object {
    $destino = Join-Path $www $_.FullName.Substring($origem.Length + 1)
    New-Item -ItemType Directory (Split-Path $destino) -Force | Out-Null
    Copy-Item $_.FullName $destino -Force
}
"copiado: arquivos de $origem"

# 2. conexão
Ajustar 'includes\classes\class.database.php' 'Mu Chila' {
    param($t)
    $t.Replace('$pdo_connect = "sqlsrv:Server=".$SQLHOST.",".$SQLPORT.";Database=".$SQLDB."";',
        '$pdo_connect = "sqlsrv:Server=".$SQLHOST.(strlen((string)$SQLPORT) ? ",".$SQLPORT : "").";Database=".$SQLDB.";TrustServerCertificate=1"; // Mu Chila: .\MUONLINE sem porta')
} 'conexão com instância nomeada'
Ajustar 'includes\webengine.php' 'Mu Chila: porta opcional' {
    param($t)
    $t.Replace("	if(!check_value(`$config['SQL_DB_PORT'])) throw new Exception('The database port configuration is required to connect to your database.');",
        "	// Mu Chila: porta opcional (a instância nomeada .\MUONLINE conecta sem porta)")
} 'porta do banco opcional'

# 3. menu do jogador
$menu = Join-Path $www 'includes\config\usercp.json'
$itens = [Collections.Generic.List[object]](Get-Content $menu -Raw | ConvertFrom-Json)
if (-not ($itens | Where-Object link -eq 'usercp/loja')) {
    $itens.Insert(0, [pscustomobject]@{ active = $true; type = 'internal'; phrase = 'usercp_menu_txt_muchila_loja'; link = 'usercp/loja'
                                        icon = 'donate.png'; visibility = 'user'; newtab = $false; order = 5 })
    "aplicado: item Loja no menu do jogador"
} else { "ja feito: item Loja no menu do jogador" }
foreach ($i in $itens | Where-Object link -eq 'donation') { $i.active = $false }
[IO.File]::WriteAllText($menu, ($itens | ConvertTo-Json -Depth 4), $utf8)

# 3b. menu do topo (navbar.json): "Doação" (PayPal, não usado) vira "Loja"
$topo = Join-Path $www 'includes\config\navbar.json'
$itensTopo = Get-Content $topo -Raw | ConvertFrom-Json
foreach ($i in $itensTopo | Where-Object link -eq 'donation') { $i.link = 'usercp/loja'; $i.phrase = 'menu_txt_muchila_loja'; "aplicado: Doação -> Loja no menu do topo" }
[IO.File]::WriteAllText($topo, ($itensTopo | ConvertTo-Json -Depth 4), $utf8)

# 3c. fuso horário do site (o WebEngine vem em UTC; o servidor do jogo usa o horário de Brasília)
Ajustar 'includes\config\timezone.php' 'America/Sao_Paulo' {
    param($t) $t.Replace("date_default_timezone_set('UTC');", "date_default_timezone_set('America/Sao_Paulo'); // Mu Chila")
} 'fuso horário de São Paulo'

# 3d. quadro de eventos: a agenda de exemplo (fixa, em UTC) vira a agenda real do servidor (includes\muchila\eventos.php)
Ajustar 'api\events.php' 'muchila/eventos.php' {
    param($t)
    $i = $t.IndexOf("date_default_timezone_set('UTC');"); $f = $t.IndexOf('// DO NOT EDIT BELOW THIS LINE')
    if ($i -lt 0 -or $f -lt $i) { return $t }
    $t.Substring(0, $i) + "date_default_timezone_set('America/Sao_Paulo');`n`n// Mu Chila: agenda lida dos arquivos de evento do servidor (C:\MuServer\Data\Event)`n" +
        "`$eventTimes = require(__DIR__ . '/../includes/muchila/eventos.php');`n`n" + $t.Substring($f).Replace('date("D g:i A", strtotime($nextTime))', 'date("d/m H:i", strtotime($nextTime))')
} 'agenda real no quadro de eventos'

# 4. idioma
$frases = [ordered]@{ usercp_menu_txt_muchila_loja = 'Loja: VIP e Cash'; menu_txt_muchila_loja = 'Loja' }
foreach ($idioma in 'pt', 'en') {
    foreach ($chave in $frases.Keys) {
        $linha = "`$lang['$chave'] = '$($frases[$chave])'; // Mu Chila"
        Ajustar "includes\languages\$idioma\language.php" "`$lang['$chave']" { param($t) $t.TrimEnd() + "`n$linha`n" }.GetNewClosure() "texto $chave ($idioma)"
    }
}

# 4b. banco: tabela/colunas da loja (script reaplicável) e tarefa "Mu Chila - Zen do VIP" no agendador (MD5 = md5_file do WebEngine)
sqlcmd -S .\MUONLINE -d MuOnlineS14 -E -C -I -b -f 65001 -i (Join-Path $PSScriptRoot 'sql\MUCHILA_PEDIDOS.sql') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falha ao aplicar sql\MUCHILA_PEDIDOS.sql' }
$md5 = (Get-FileHash (Join-Path $www 'includes\cron\muchila_zen.php') -Algorithm MD5).Hash.ToLower()
$r = sqlcmd -S .\MUONLINE -d MuOnlineS14 -E -C -I -b -h -1 -W -Q @"
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM WEBENGINE_CRON WHERE cron_file_run = 'muchila_zen.php')
BEGIN UPDATE WEBENGINE_CRON SET cron_file_md5 = '$md5' WHERE cron_file_run = 'muchila_zen.php' AND cron_file_md5 <> '$md5'; SELECT 'ja feito: tarefa Zen do VIP' + CASE WHEN @@ROWCOUNT > 0 THEN ' (MD5 atualizado)' ELSE '' END; END
ELSE BEGIN INSERT INTO WEBENGINE_CRON (cron_name, cron_description, cron_file_run, cron_run_time, cron_last_run, cron_status, cron_protected, cron_file_md5)
    VALUES ('Mu Chila - Zen do VIP', 'Entrega o Zen de bonus do VIP no bau das contas que ja sairam do jogo', 'muchila_zen.php', '60', NULL, 1, 0, '$md5');
    SELECT 'aplicado: tarefa Zen do VIP no agendador'; END
"@
if ($LASTEXITCODE -ne 0) { throw 'Falha ao registrar a tarefa do Zen' }
$r

# 5. painel admin
Ajustar 'admincp\index.php' 'muchila_pedidos' {
    param($t)
    $t.Replace('$admincpSidebar = array(', "`$admincpSidebar = array(`n`tarray(`"Mu Chila`", array(`n`t`t`"muchila_pedidos`" => `"Pedidos da loja`",`n`t), `"fa-shopping-cart`"),")
} 'menu do painel admin'

# 6. módulos que não funcionam neste servidor ficam desligados (26/09/2026):
#    - usercp.reset: o jogo tem /reset e reset automático com outras regras (Command.dat + ResetTable.txt); o do site daria outro resultado
#    - usercp.buyzen e usercp.vote: dependem do sistema de créditos do WebEngine, que não está configurado (a moeda do Mu Chila é o cash)
#    - forgotpassword: manda e-mail, e este PC não tem servidor de e-mail (senha esquecida: pedir ao administrador)
foreach ($modulo in 'usercp.reset', 'usercp.buyzen', 'usercp.vote', 'forgotpassword') {
    $arq = Join-Path $www "includes\config\modules\$modulo.xml"
    $xml = [IO.File]::ReadAllText($arq, $utf8)
    if ($xml -match '<active>0</active>') { "ja feito: $modulo desligado"; continue }
    [IO.File]::WriteAllText($arq, ($xml -replace '<active>1</active>', '<active>0</active>'), $utf8)
    "aplicado: $modulo desligado"
}
Ajustar 'modules\login.php' 'Mu Chila: sem e-mail' {
    param($t)
    $t.Replace("echo '<span id=`"helpBlock`" class=`"help-block`"><a href=`"'.__BASE_URL__.'forgotpassword/`">'.lang('login_txt_4',true).'</a></span>';",
        "echo '<span id=`"helpBlock`" class=`"help-block`">Esqueceu a senha? Peça ao administrador do servidor.</span>'; // Mu Chila: sem e-mail para recuperar senha")
} 'login: senha esquecida -> pedir ao administrador'

# 7. downloads (título e descrição: até 100 caracteres, limite da tabela): cliente, patch e LEIA-ME da pasta "Cliente para amigos" (servida pelo Apache em /arquivos/, ver conf\httpd.conf).
#    Cadastra ou atualiza pelo endereço do arquivo, com o tamanho atual, e regrava o cache que a página de downloads lê.
$pastaArquivos = 'C:\MuServer\Cliente para amigos'
$downloads = @(
    @{ arquivo = 'Cliente Season 14 - Radmin.zip'; tipo = 1; titulo = 'Cliente Season 14 (completo)'
       descricao = 'Extraia em C:\Jogos e abra 2 - Cliente Season 14 Full\main.exe. Precisa do Radmin VPN.' },
    @{ arquivo = 'Patch Mu Chila - para quem ja tem o cliente.zip'; tipo = 2; titulo = 'Patch Mu Chila'
       descricao = 'Para quem já tem o cliente: extraia na pasta do cliente, substituindo os arquivos.' },
    @{ arquivo = 'LEIA-ME - Como jogar.txt'; tipo = 3; titulo = 'LEIA-ME: como jogar'
       descricao = 'Radmin VPN, instalação, idioma português, resolução e comandos úteis.' }
)
$sql = New-Object Text.StringBuilder
[void]$sql.AppendLine('SET NOCOUNT ON;')
foreach ($d in $downloads) {
    $caminho = Join-Path $pastaArquivos $d.arquivo
    if (-not (Test-Path $caminho)) { "aviso: $($d.arquivo) não existe; download não cadastrado"; continue }
    $mb = [math]::Max(0.01, [math]::Round((Get-Item $caminho).Length / 1MB, 2)).ToString([Globalization.CultureInfo]::InvariantCulture)   # a página mostra MB com 2 casas
    $link = '/arquivos/' + [uri]::EscapeDataString($d.arquivo)
    $q = { param($s) $s.Replace("'", "''") }
    [void]$sql.AppendLine("IF EXISTS (SELECT 1 FROM WEBENGINE_DOWNLOADS WHERE download_link = '$(& $q $link)') UPDATE WEBENGINE_DOWNLOADS SET download_title = '$(& $q $d.titulo)', download_description = '$(& $q $d.descricao)', download_size = $mb, download_type = $($d.tipo) WHERE download_link = '$(& $q $link)';")
    [void]$sql.AppendLine("ELSE INSERT INTO WEBENGINE_DOWNLOADS (download_title, download_description, download_link, download_size, download_type) VALUES ('$(& $q $d.titulo)', '$(& $q $d.descricao)', '$(& $q $link)', $mb, $($d.tipo));")
}
[void]$sql.AppendLine('SELECT download_id, download_title, download_description, download_link, download_size, download_type FROM WEBENGINE_DOWNLOADS ORDER BY download_type, download_id FOR JSON PATH;')
$tmpSql = Join-Path $env:TEMP 'muchila-downloads.sql'
[IO.File]::WriteAllText($tmpSql, $sql.ToString(), $utf8)
$json = (sqlcmd -S .\MUONLINE -d MuOnlineS14 -E -C -I -b -y 0 -f 65001 -i $tmpSql) -join ''
if ($LASTEXITCODE -ne 0) { throw 'Falha ao cadastrar os downloads' }
Remove-Item $tmpSql
[IO.File]::WriteAllText((Join-Path $www 'includes\cache\downloads.cache'), $(if ($json) { $json } else { '[]' }), $utf8)
"aplicado: downloads ($((($json | ConvertFrom-Json) | Measure-Object).Count) arquivos) e cache da página"

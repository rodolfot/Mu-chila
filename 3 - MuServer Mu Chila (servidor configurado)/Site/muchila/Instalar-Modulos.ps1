# Instala o código do Mu Chila dentro do WebEngine (C:\MuServer\Site\www). Pode rodar de novo a qualquer momento
# (depois de atualizar o WebEngine ou de mudar algo em muchila\www): cada ajuste confere se já foi feito.
#   1. copia muchila\www\* para www (loja, painel de pedidos, aviso do Mercado Pago, configurações)
#   2. class.database.php e webengine.php: conexão com instância nomeada sem porta (.\MUONLINE) e certificado local aceito
#   3. menu do jogador (usercp.json): item "Loja: VIP e Cash"; a doação por PayPal (não usada) sai do menu
#   4. idioma português: texto do item do menu
#   5. painel admin: grupo "Mu Chila" com "Pedidos da loja"
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

# 5. painel admin
Ajustar 'admincp\index.php' 'muchila_pedidos' {
    param($t)
    $t.Replace('$admincpSidebar = array(', "`$admincpSidebar = array(`n`tarray(`"Mu Chila`", array(`n`t`t`"muchila_pedidos`" => `"Pedidos da loja`",`n`t), `"fa-shopping-cart`"),")
} 'menu do painel admin'

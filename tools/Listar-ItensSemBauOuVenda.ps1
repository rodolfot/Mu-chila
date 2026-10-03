# Lista os itens que o jogador não consegue guardar no baú ou vender ao NPC, e de onde ele obtém cada um.
#
# Duas travas, e as duas precisam liberar:
#   - cliente: 7 permissões por item no Data\Local\{Eng,Por,Spn}\item_*.bmd (registros de 672 bytes, XOR FC CF AB
#     recomeçando em cada registro, permissões nos bytes 661-667). Ordem: largar, trocar, loja pessoal, baú,
#     vender ao NPC, item valioso, consertar (ver docs\TAREFA-ITENS-BAU-E-VENDA.md);
#   - servidor: AllowDrop/AllowSell/AllowTrade/AllowVault no Data\Item\ItemMove.txt (item fora da lista = liberado).
# Fontes de obtenção: DropItem do Item.txt, ItemDrop.txt, lojas de NPC do ShopManager, caixas do EventItemBagManager,
# pacotes da loja de cash e a loja de itens do site. Não cobre Chaos Machine, missões, Gremory nem o que já está no banco.
#
# Uso: .\tools\Listar-ItensSemBauOuVenda.ps1 [-Saida <arquivo.csv>]
param(
    [string]$Repo = (Join-Path $PSScriptRoot '..'),
    [string]$Saida = (Join-Path $PSScriptRoot '..\docs\itens-sem-bau-ou-venda.csv')
)
$ErrorActionPreference = 'Stop'
$srv = Join-Path $Repo '3 - MuServer Mu Chila (servidor configurado)\Data'
$cli = Join-Path $Repo '2 - Cliente Season 14 Full\Data\Local'

function LerBmd([string]$arq) {
    $b = [IO.File]::ReadAllBytes($arq); $n = [BitConverter]::ToInt32($b, 0); $x = [byte[]](0xFC, 0xCF, 0xAB); $h = @{}
    for ($r = 0; $r -lt $n; $r++) {
        $o = 4 + $r * 672
        $cod = ($b[$o] -bxor $x[0]) -bor (($b[$o + 1] -bxor $x[1]) -shl 8)
        $h[$cod] = -join (0..6 | ForEach-Object { $b[$o + 661 + $_] -bxor $x[(661 + $_) % 3] })
    }
    $h
}
$bmd = @{}
foreach ($l in 'Eng', 'Por', 'Spn') { $bmd[$l] = LerBmd (Join-Path $cli "$l\item_$($l.ToLower()).bmd") }

# servidor: Item.txt (nome e DropItem) e ItemMove.txt
$itens = @{}; $sec = -1
foreach ($ln in [IO.File]::ReadAllLines((Join-Path $srv 'Item\Item.txt'), [Text.Encoding]::GetEncoding(1252))) {
    $t = $ln.Trim()
    if ($t -match '^(\d+)$') { $sec = [int]$Matches[1]; continue }
    if ($t -match '^(\d+)\s+\S+\s+\S+\s+\S+\s+\S+\s+\S+\s+\S+\s+(\d+)\s+"([^"]*)"') { $itens[$sec * 512 + [int]$Matches[1]] = @{ Nome = $Matches[3]; Drop = [int]$Matches[2] } }
}
$move = @{}
foreach ($ln in Get-Content (Join-Path $srv 'Item\ItemMove.txt')) {
    if ($ln -match '^\s*(\d+)\s+(\d)\s+(\d)\s+(\d)\s+(\d)') { $move[[int]$Matches[1]] = @{ Drop = $Matches[2]; Sell = $Matches[3]; Trade = $Matches[4]; Vault = $Matches[5] } }
}

# fontes de obtenção
$fontes = @{}
function Fonte([int]$cod, [string]$f) { if (-not $fontes.ContainsKey($cod)) { $fontes[$cod] = New-Object System.Collections.Generic.HashSet[string] }; [void]$fontes[$cod].Add($f) }
foreach ($k in $itens.Keys) { if ($itens[$k].Drop -eq 1) { Fonte $k 'drop de monstro' } }
foreach ($ln in Get-Content (Join-Path $srv 'Item\ItemDrop.txt')) {
    $c = ($ln -replace '//.*', '').Trim() -split '\s+'
    if ($c.Count -ge 16 -and $c[0] -match '^\d+$' -and $c[15] -match '^\d+$' -and [int]$c[15] -gt 0) { Fonte ([int]$c[0]) 'ItemDrop.txt' }
}
foreach ($ln in Get-Content (Join-Path $srv 'ShopManager.txt')) {
    if ($ln -notmatch '^\s*(\d+)\s+\d+\s*//\s*(.*)$') { continue }
    $loja = $Matches[2].Trim()
    $arq = Get-ChildItem -LiteralPath (Join-Path $srv 'Shop') -Filter ('{0:D3} - *.txt' -f [int]$Matches[1]) | Select-Object -First 1
    if (-not $arq) { continue }
    foreach ($s in Get-Content -LiteralPath $arq.FullName) {
        $c = ($s -replace '//.*', '').Trim() -split '\s+'
        if ($c.Count -ge 2 -and $c[0] -match '^\d+$' -and $c[1] -match '^\d+$') { Fonte ([int]$c[0] * 512 + [int]$c[1]) "loja NPC ($loja)" }
    }
}
foreach ($ln in Get-Content (Join-Path $srv 'EventItemBagManager.txt')) {
    if ($ln -notmatch '^\s*(\d+)\s+\S+\s+\S+\s+\S+\s+\S+\s*//\s*(.*)$') { continue }
    $bag = $Matches[2].Trim()
    $arq = Get-ChildItem -LiteralPath (Join-Path $srv 'EventItemBag') -Filter ('{0:D3} - *.txt' -f [int]$Matches[1]) | Select-Object -First 1
    if (-not $arq) { continue }
    $bloco = -1
    foreach ($s in Get-Content -LiteralPath $arq.FullName) {
        $t = ($s -replace '//.*', '').Trim()
        if ($t -match '^\d+$') { $bloco = [int]$t; continue }
        if ($t -eq 'end') { $bloco = -1; continue }
        $c = $t -split '\s+'
        if ($bloco -ge 1 -and $c.Count -ge 2 -and $c[0] -match '^\d+$' -and $c[1] -match '^\d+$' -and [int]$c[0] -le 20) { Fonte ([int]$c[0] * 512 + [int]$c[1]) "caixa/evento ($bag)" }
    }
}
# loja de cash: produto = (BaseIndex, MainIndex); o pacote lista ProductBaseIndex1..10 (colunas 7-16) e ProductMainIndex1..10
# (17-26). Nos combos o MainIndex vem 0: vale qualquer produto daquele BaseIndex.
$prod = @{}
foreach ($ln in Get-Content (Join-Path $srv 'CashShop\CashShopProduct.txt')) { $c = ($ln -replace '//.*', '').Trim() -split '\s+'; if ($c.Count -ge 4 -and $c[0] -match '^\d+$') { $prod["$($c[0]),$($c[1])"] = [int]$c[3] } }
foreach ($ln in Get-Content (Join-Path $srv 'CashShop\CashShopPackage.txt')) {
    $c = ($ln -replace '//.*', '').Trim() -split '\s+'
    if ($c.Count -lt 27 -or $c[0] -notmatch '^\d+$') { continue }
    Fonte ([int]$c[3]) 'loja de cash'
    for ($i = 7; $i -le 16; $i++) {
        if ([int]$c[$i] -le 0) { continue }
        $k = "$($c[$i]),$($c[$i + 10])"
        if ($prod.ContainsKey($k)) { Fonte $prod[$k] 'loja de cash' }
        else { foreach ($t in @($prod.Keys | Where-Object { $_.StartsWith("$($c[$i]),") })) { Fonte $prod[$t] 'loja de cash' } }
    }
}
$site = Get-Content (Join-Path $srv '..\Site\www\includes\config\muchila.lojaitens.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($cat in $site.categorias) { foreach ($it in $cat.itens) { Fonte ([int]$it.secao * 512 + [int]$it.tipo) 'loja do site (entrega no baú)' } }

# cruzamento: entra quem não vai ao baú OU não vende ao NPC
$sn = { param([bool]$v) if ($v) { 'Sim' } else { 'Não' } }
$livre = @{ Drop = '1'; Sell = '1'; Trade = '1'; Vault = '1' }
$diferentes = 0
$res = foreach ($cod in ($itens.Keys | Sort-Object)) {
    $p = $bmd['Eng'][$cod]
    if (-not $p) { continue }   # o cliente não conhece o item: ele não existe no jogo
    if ($p -ne $bmd['Por'][$cod] -or $p -ne $bmd['Spn'][$cod]) { $diferentes++ }
    $m = if ($move.ContainsKey($cod)) { $move[$cod] } else { $livre }
    $bau = $p[3] -eq '1' -and $m.Vault -eq '1'
    $vende = $p[4] -eq '1' -and $m.Sell -eq '1'
    if ($bau -and $vende) { continue }
    $travas = @()
    if ($p[3] -ne '1') { $travas += 'baú no cliente' }
    if ($m.Vault -ne '1') { $travas += 'baú no servidor' }
    if ($p[4] -ne '1') { $travas += 'venda no cliente' }
    if ($m.Sell -ne '1') { $travas += 'venda no servidor' }
    $nome = $itens[$cod].Nome
    $obs = switch ($true) {
        ($cod -eq 6676) { "Vale para todos os níveis: +1 Warrior's Ring e +2 Champion's Ring, que personagens antigos ainda têm (issue #11). No cliente o baú está liberado: o jogador arrasta e o servidor recusa"; break }
        ($cod -ge 6727 -and $cod -le 6731) { 'Ícone da aposta do Moss Merchant (provavelmente vira outro item na compra)'; break }
        ($cod -eq 7331) { 'Feito para ser usado: libera o baú estendido'; break }
        ($cod -in 7598, 7621) { 'Item de missão'; break }
        ($cod -in 7259, 7337, 7449, 7655) { 'Cartão de personagem'; break }
        ($p -eq '0000110') { 'Muun'; break }
        ($nome -match 'Bound') { 'Preso ao personagem (Bound)'; break }
        ($p -eq '0000000') { 'Tudo bloqueado no cliente: não sai do inventário'; break }
        default { '' }
    }
    [pscustomobject]@{
        'Código' = $cod; 'Seção' = [Math]::Floor($cod / 512); 'Índice' = $cod % 512; 'Nome' = $nome
        'Guarda no baú' = & $sn $bau; 'Vende ao NPC' = & $sn $vende
        'Larga no chão' = & $sn ($p[0] -eq '1' -and $m.Drop -eq '1'); 'Troca' = & $sn ($p[1] -eq '1' -and $m.Trade -eq '1'); 'Loja pessoal' = & $sn ($p[2] -eq '1')
        'Onde está a trava' = $travas -join ', '
        'Jogador consegue obter' = & $sn $fontes.ContainsKey($cod)
        'Como obtém' = if ($fontes.ContainsKey($cod)) { ($fontes[$cod] | Sort-Object) -join ' | ' } else { '' }
        'Observação' = $obs
        # com espaços para o Excel não transformar "0001000" no número 1000
        'Permissões no cliente' = ($p.ToCharArray() -join ' ')
    }
}
$res = $res | Sort-Object @{ Expression = { $_.'Jogador consegue obter' -eq 'Sim' }; Descending = $true }, 'Código'
$res | Export-Csv $Saida -Delimiter ';' -NoTypeInformation -Encoding UTF8
"itens no servidor: $($itens.Count); no cliente: $($bmd['Eng'].Count); permissões diferentes entre os idiomas: $diferentes"
"na lista: $($res.Count) | não guardam no baú: $(@($res | Where-Object { $_.'Guarda no baú' -eq 'Não' }).Count) | não vendem: $(@($res | Where-Object { $_.'Vende ao NPC' -eq 'Não' }).Count) | o jogador consegue obter: $(@($res | Where-Object { $_.'Jogador consegue obter' -eq 'Sim' }).Count)"
"gravado em $((Resolve-Path $Saida).Path)"

# Loja de cash (decisão do dono, 28/09/2026):
#  1) Cartões de classe à venda SÓ na loja de cash, 500 WCoin (C): cria os pacotes de Rage Fighter, Grow Lancer e Rune Mage
#     clonando o do Summoner (13,126 / produto 31,53), no servidor e no cliente.
#  2) Chaos Card sai dos pacotes combinados ("Level up" normal/Master em C e P e "Gamble package"), sem apagar nada:
#     as linhas originais vão para Data\CashShop\muchila-cash-chaoscard-original.txt. Para voltar: -Restaurar.
#     (Os 2 pacotes avulsos de Chaos Card, 27,141 e 27,81, são escondidos à parte pelo painel/--cash-esconder.)
# Uso: .\Loja-CartoesClasse-ChaosCard.ps1 [-Restaurar] [-Servidor C:\MuServer\Data\CashShop] [-Cliente <...>\512.2011.006]
param(
    [switch]$Restaurar,
    [string]$Servidor = 'C:\MuServer\Data\CashShop',
    [string]$Cliente = (Join-Path $PSScriptRoot '..\2 - Cliente Season 14 Full\Data\InGameShopScript\512.2011.006')
)
$ErrorActionPreference = 'Stop'
$enc = [Text.Encoding]::Latin1; $stamp = '.bak-' + (Get-Date -f 'yyyyMMdd-HHmmss')
$fPac = Join-Path $Servidor 'CashShopPackage.txt'; $fProd = Join-Path $Servidor 'CashShopProduct.txt'
$cPac = Join-Path $Cliente 'IBSPackage.txt'; $cProd = Join-Path $Cliente 'IBSProduct.txt'
$fOrig = Join-Path $Servidor 'muchila-cash-chaoscard-original.txt'
function Ler($f) { $t = $enc.GetString([IO.File]::ReadAllBytes($f)); $nl = if ($t.Contains("`r`n")) { "`r`n" } else { "`n" }; @{ L = [Collections.Generic.List[string]]($t -split "`r?`n"); NL = $nl } }
function Gravar($f, $d) { Copy-Item $f ($f + $stamp); [IO.File]::WriteAllBytes($f, $enc.GetBytes(($d.L -join $d.NL))) }
# troca tokens (por posição) de uma linha separada por espaços/TABs, mantendo os separadores
function Tokens([string]$linha, [hashtable]$novos) {
    $ms = [regex]::Matches($linha, '\S+')
    for ($i = $ms.Count - 1; $i -ge 0; $i--) { if ($novos.ContainsKey($i)) { $m = $ms[$i]; $linha = $linha.Substring(0, $m.Index) + $novos[$i] + $linha.Substring($m.Index + $m.Length) } }
    $linha
}
function Chave([string]$l) { $c = $l.Trim() -split '\s+'; if ($c.Count -eq 27 -and $c[0] -match '^\d+$') { "$($c[0]),$($c[2])" } }
function ChaveCli([string]$l) { $c = $l.Split('@'); if ($c.Count -ge 26 -and $c[0] -match '^\d+$') { "$($c[0]),$($c[2])" } }

$pac = Ler $fPac; $prod = Ler $fProd; $cp = Ler $cPac; $cpr = Ler $cProd
$pacotesChaos = '13,145', '13,143', '27,146', '27,144', '27,164'

if ($Restaurar) {
    if (-not (Test-Path $fOrig)) { throw "Não achei $fOrig (nada a restaurar)." }
    foreach ($l in [IO.File]::ReadAllLines($fOrig, $enc)) {
        $tipo, $chave, $orig = $l.Split("`t", 3)
        $lista = if ($tipo -eq 'S') { $pac } else { $cp }
        $i = 0..($lista.L.Count - 1) | Where-Object { ($(if ($tipo -eq 'S') { Chave $lista.L[$_] } else { ChaveCli $lista.L[$_] })) -eq $chave } | Select-Object -First 1
        if ($null -eq $i) { throw "pacote $chave não encontrado para restaurar" }
        $lista.L[$i] = $orig
    }
    Gravar $fPac $pac; Gravar $cPac $cp; Remove-Item $fOrig
    'Chaos Card devolvido aos pacotes combinados. Para os avulsos (27,141 e 27,81): aba Loja de Cash → mostrar. Depois: Reload CashShop e publicar o cliente.'
    return
}

# ---------- 1) cartões de classe ----------
$iPac = $pac.L.FindIndex({ param($l) (Chave $l) -eq '13,126' }); if ($iPac -lt 0) { throw 'pacote do Summoner (13,126) não encontrado' }
$iProd = $prod.L.FindIndex({ param($l) $l -match '^31\s+53\s' }); if ($iProd -lt 0) { throw 'produto do Summoner (31,53) não encontrado' }
$iCp = $cp.L.FindIndex({ param($l) (ChaveCli $l) -eq '13,126' }); if ($iCp -lt 0) { throw 'pacote do Summoner não encontrado no cliente' }
$cprSum = @($cpr.L | Where-Object { $_ -match '^31@' })
if ($cprSum.Count -eq 0) { throw 'produto do Summoner não encontrado no cliente' }
$usados = @($pac.L | ForEach-Object { $c = $_.Trim() -split '\s+'; if ($c.Count -eq 27 -and $c[0] -match '^\d+$') { [int]$c[2] } })
$cartoes = @(
    @{ Nome = 'RageFighter Character Card'; Item = 7337; Classe = 'Rage Fighter' },
    @{ Nome = 'Grow Lancer Character Card'; Item = 7449; Classe = 'Grow Lancer' },
    @{ Nome = 'Rune Mage Character Card'; Item = 7655; Classe = 'Rune Mage' }
)
$feitos = 0
foreach ($k in $cartoes) {
    if ($pac.L | Where-Object { ($_.Trim() -split '\s+')[3] -eq "$($k.Item)" -and (Chave $_) -like '13,*' }) { "$($k.Nome): já está na loja"; continue }
    $main = (($usados + 265) | Measure-Object -Maximum).Maximum + 1; $usados += $main
    $base13 = (@($pac.L | ForEach-Object { $c = $_.Trim() -split '\s+'; if ($c.Count -eq 27 -and $c[0] -eq '13') { [int]$c[1] } }) | Measure-Object -Maximum).Maximum + 1
    $pb = (@($prod.L | ForEach-Object { if ($_ -match '^(\d+)\s') { [int]$Matches[1] } }) | Measure-Object -Maximum).Maximum + 1
    $pm = (@($prod.L | ForEach-Object { $c = $_.Trim() -split '\s+'; if ($c.Count -ge 19 -and $c[0] -match '^\d+$') { [int]$c[1] } }) | Measure-Object -Maximum).Maximum + 1
    $ord = (@($cp.L | ForEach-Object { $c = $_.Split('@'); if ($c.Count -ge 26 -and $c[0] -eq '13') { [int]$c[1] } }) | Measure-Object -Maximum).Maximum + 1
    # servidor
    $pac.L.Insert($iPac + 1, (Tokens $pac.L[$iPac] @{ 1 = "$base13"; 2 = "$main"; 3 = "$($k.Item)"; 7 = "$pb"; 17 = "$pm" }))
    $prod.L.Insert($iProd + 1, (Tokens $prod.L[$iProd] @{ 0 = "$pb"; 1 = "$pm"; 3 = "$($k.Item)" }))
    # cliente
    $c = $cp.L[$iCp].Split('@')
    $c[1] = "$ord"; $c[2] = "$main"; $c[3] = $k.Nome; $c[6] = "Libera criar personagens $($k.Classe) na conta.#Basta 1 cartão por conta."; $c[19] = "$pb|"; $c[20] = "$($k.Item)"; $c[23] = "$pm|"
    $cp.L.Insert($iCp + 1, ($c -join '@'))
    $iCpr = $cpr.L.IndexOf($cprSum[-1])
    foreach ($r in [Linq.Enumerable]::Reverse([string[]]$cprSum)) {
        $x = $r.Split('@'); $x[0] = "$pb"; $x[1] = $k.Nome; $x[6] = "$pm"; $x[13] = "$($k.Item)"
        $cpr.L.Insert($iCpr + 1, ($x -join '@'))
    }
    "$($k.Nome): pacote 13,$main (produto $pb,$pm), 500 WCoin (C)"; $feitos++
}

# ---------- 2) Chaos Card fora dos pacotes combinados ----------
$tirar = @{ '13,145' = 96, 99, 100; '13,143' = 96, 99, 100; '27,146' = 96, 99, 100; '27,144' = 96, 99, 100; '27,164' = 32, 117 }
$orig = [Collections.Generic.List[string]]::new()
foreach ($chave in $pacotesChaos) {
    $i = $pac.L.FindIndex({ param($l) (Chave $l) -eq $chave }); $j = $cp.L.FindIndex({ param($l) (ChaveCli $l) -eq $chave })
    if ($i -lt 0 -or $j -lt 0) { throw "pacote $chave não encontrado" }
    $c = $pac.L[$i].Trim() -split '\s+'
    $bases = @($c[7..16] | ForEach-Object { [int]$_ }); $mains = @($c[17..26] | ForEach-Object { [int]$_ })
    if (-not ($bases | Where-Object { $tirar[$chave] -contains $_ })) { "${chave}: já sem Chaos Card"; continue }
    $orig.Add("S`t$chave`t$($pac.L[$i])"); $orig.Add("C`t$chave`t$($cp.L[$j])")
    $nb = @(); $nm = @()
    for ($n = 0; $n -lt 10; $n++) { if ($bases[$n] -ne 0 -and $tirar[$chave] -notcontains $bases[$n]) { $nb += $bases[$n]; $nm += $mains[$n] } }
    while ($nb.Count -lt 10) { $nb += 0; $nm += 0 }
    $novos = @{}; for ($n = 0; $n -lt 10; $n++) { $novos[7 + $n] = "$($nb[$n])"; $novos[17 + $n] = "$($nm[$n])" }
    $pac.L[$i] = Tokens $pac.L[$i] $novos
    $x = $cp.L[$j].Split('@')
    $x[19] = (($x[19].Split('|') | Where-Object { $_ -ne '' -and $tirar[$chave] -notcontains [int]$_ }) -join '|') + '|'
    $x[6] = $x[6] -replace '\+Chaos card Gold\(3EA\)', '' -replace ' ?Chaos Card Mini\(1EA\),', '' -replace ' ?Chaos Card Gold\(1EA\),', ''
    $cp.L[$j] = $x -join '@'
    "${chave}: Chaos Card retirado (produtos $($tirar[$chave] -join ', '))"
}
if ($orig.Count) { [IO.File]::AppendAllLines($fOrig, $orig, $enc) }
Gravar $fPac $pac; Gravar $fProd $prod; Gravar $cPac $cp; Gravar $cProd $cpr
"Pronto. Depois: esconder os avulsos 27,141 e 27,81, Reload CashShop e publicar o cliente."

# Loja de cash: "Gold Channel Ticket" (item 13,124) vira o Passe dos Mapas Exclusivos (decisão do dono, 28/09/2026).
#  - Usar o ticket guardado na loja soma a duração ao passe da conta (procedure WZ_SetAccountLevel ajustada em
#    DB\1 - Querys\MuChila-PasseMapas.sql). As 4 durações já existiam no kit: 1 dia, 3 dias e o pacote "7 ou 30 dias".
#  - Preços: bem caros, perto do VIP do site (VIP R$35-50; o pacote de 500 cash custa R$45 no site).
#  - Nomes e descrições em português (sem acento: a fonte da loja do cliente não tem).
#  - Linhas originais guardadas em Data\CashShop\muchila-cash-passe-original.txt; -Restaurar volta tudo.
#  - Os mesmos pacotes em WCoin P (categoria 32) são escondidos à parte (painel, aba Loja de Cash, ou --cash-esconder),
#    porque WCoin P se ganha em evento e o dono quer o passe só por WCoin comprado ou dinheiro no site.
# Uso: .\Loja-PasseMapas.ps1 [-Preco1 100] [-Preco3 200] [-Preco7 300] [-Preco30 500] [-Restaurar] [-Servidor ...] [-Cliente ...]
# Depois: Reload CashShop nos GameServers e publicar o cliente (tools\Publicar-Launcher.ps1).
param(
    [int]$Preco1 = 100, [int]$Preco3 = 200, [int]$Preco7 = 300, [int]$Preco30 = 500,
    [switch]$Restaurar,
    [string]$Servidor = 'C:\MuServer\Data\CashShop',
    [string]$Cliente = (Join-Path $PSScriptRoot '..\2 - Cliente Season 14 Full\Data\InGameShopScript\512.2011.006')
)
$ErrorActionPreference = 'Stop'
$enc = [Text.Encoding]::Latin1; $stamp = '.bak-' + (Get-Date -f 'yyyyMMdd-HHmmss')
$fPac = Join-Path $Servidor 'CashShopPackage.txt'; $fProd = Join-Path $Servidor 'CashShopProduct.txt'
$cPac = Join-Path $Cliente 'IBSPackage.txt'; $cProd = Join-Path $Cliente 'IBSProduct.txt'
$fOrig = Join-Path $Servidor 'muchila-cash-passe-original.txt'
function Ler($f) { $t = $enc.GetString([IO.File]::ReadAllBytes($f)); $nl = if ($t.Contains("`r`n")) { "`r`n" } else { "`n" }; @{ F = $f; L = [Collections.Generic.List[string]]($t -split "`r?`n"); NL = $nl } }
function Gravar($d) { Copy-Item $d.F ($d.F + $stamp); [IO.File]::WriteAllBytes($d.F, $enc.GetBytes(($d.L -join $d.NL))) }
# troca tokens (por posição) de uma linha separada por espaços/TABs, mantendo os separadores
function Tokens([string]$linha, [hashtable]$novos) {
    $ms = [regex]::Matches($linha, '\S+')
    for ($i = $ms.Count - 1; $i -ge 0; $i--) { if ($novos.ContainsKey($i)) { $m = $ms[$i]; $linha = $linha.Substring(0, $m.Index) + $novos[$i] + $linha.Substring($m.Index + $m.Length) } }
    $linha
}
function Campos([string]$linha, [hashtable]$novos) { $c = $linha.Split('@'); foreach ($k in $novos.Keys) { $c[$k] = $novos[$k] }; $c -join '@' }

$arq = [ordered]@{ SP = (Ler $fPac); SR = (Ler $fProd); CP = (Ler $cPac); CR = (Ler $cProd) }

if ($Restaurar) {
    if (-not (Test-Path $fOrig)) { throw "Não achei $fOrig (nada a restaurar)." }
    $mudou = @{}
    foreach ($l in [IO.File]::ReadAllLines($fOrig, $enc)) {
        $qual, $num, $orig = $l.Split("`t", 3)
        $arq[$qual].L[[int]$num] = $orig; $mudou[$qual] = $true
    }
    foreach ($k in $mudou.Keys) { Gravar $arq[$k] }
    Remove-Item $fOrig
    'Tickets devolvidos ao original do kit (nomes e preços). Depois: Reload CashShop e publicar o cliente.'
    'Atenção: a procedure do banco continua tratando o ticket como passe (para voltar a ela, veja o fim de MuChila-PasseMapas.sql).'
    return
}
if (Test-Path $fOrig) { throw "Já aplicado (existe $fOrig). Para mudar os preços: -Restaurar e aplique de novo." }

$mapas = 'Nixies Lake, Deep Dungeon, Swamp of Darkness e Kubera Mine'
# produto (base,main) -> dias e preço
$produtos = @(
    @{ Base = 154; Main = 214; Dias = 1;  Preco = $Preco1 },
    @{ Base = 111; Main = 170; Dias = 3;  Preco = $Preco3 },
    @{ Base = 129; Main = 188; Dias = 7;  Preco = $Preco7 },
    @{ Base = 129; Main = 189; Dias = 30; Preco = $Preco30 }
)
function Dias($d) { if ($d -eq 1) { '1 dia' } else { "$d dias" } }
# pacote (main no cliente/servidor) -> produtos que oferece
$pacotes = @(
    @{ Mains = 193, 194; Nome = 'Passe Mapas 400+ (1 dia)';  Prod = @($produtos[0]) },
    @{ Mains = 156, 157; Nome = 'Passe Mapas 400+ (3 dias)'; Prod = @($produtos[1]) },
    @{ Mains = 167, 168; Nome = 'Passe Mapas 400+ (7 ou 30 dias)'; Prod = @($produtos[2], $produtos[3]) }
)
$orig = [Collections.Generic.List[string]]::new()
function Trocar($qual, [int]$i, [string]$nova) { $d = $arq[$qual]; if ($d.L[$i] -ne $nova) { $orig.Add("$qual`t$i`t$($d.L[$i])"); $d.L[$i] = $nova } }
$feitos = 0

# ---------- servidor: CashShopProduct (CoinValue = 3º campo) ----------
foreach ($p in $produtos) {
    $i = $arq.SR.L.FindIndex({ param($l) $l -match "^$($p.Base)\s+$($p.Main)\s" })
    if ($i -lt 0) { throw "produto $($p.Base),$($p.Main) não encontrado no CashShopProduct.txt" }
    $c = $arq.SR.L[$i].Trim() -split '\s+'
    if ($c[3] -ne '6780' -or [int]$c[18] -ne $p.Dias * 86400) { throw "produto $($p.Base),$($p.Main) não é o ticket de $(Dias $p.Dias) (item $($c[3]), duração $($c[18]))" }
    Trocar 'SR' $i (Tokens $arq.SR.L[$i] @{ 2 = "$($p.Preco)" }); $feitos++
}
# ---------- servidor: CashShopPackage (CoinValue = 6º campo; o do pacote é o da 1ª opção) ----------
foreach ($k in $pacotes) {
    foreach ($m in $k.Mains) {
        $i = $arq.SP.L.FindIndex({ param($l) $c = $l.Trim() -split '\s+'; $c.Count -eq 27 -and $c[2] -eq "$m" -and $c[3] -eq '6780' })
        if ($i -lt 0) { throw "pacote $m (ticket) não encontrado no CashShopPackage.txt" }
        Trocar 'SP' $i (Tokens $arq.SP.L[$i] @{ 5 = "$($k.Prod[0].Preco)" }); $feitos++
    }
}
# ---------- cliente: IBSProduct ([1] nome, [5] preço; 5 linhas por produto) ----------
foreach ($p in $produtos) {
    $n = 0
    for ($i = 0; $i -lt $arq.CR.L.Count; $i++) {
        $c = $arq.CR.L[$i].Split('@')
        if ($c.Count -lt 14 -or $c[0] -ne "$($p.Base)" -or $c[6] -ne "$($p.Main)") { continue }
        if ($c[13] -ne '6780') { throw "linha $($i + 1) do IBSProduct.txt não é o ticket" }
        Trocar 'CR' $i (Campos $arq.CR.L[$i] @{ 1 = "Passe Mapas 400+ ($(Dias $p.Dias))"; 5 = "$($p.Preco)" }); $n++
    }
    if ($n -eq 0) { throw "produto $($p.Main) não encontrado no IBSProduct.txt do cliente" }
    $feitos++
}
# ---------- cliente: IBSPackage ([3] nome, [5] preço, [6] descrição com # = quebra de linha) ----------
foreach ($k in $pacotes) {
    $precos = ($k.Prod | ForEach-Object { "$(Dias $_.Dias) - $($_.Preco) W Coin" }) -join '#'
    $desc = "Libera os mapas acima do nivel 400#($mapas)#$precos#Use o ticket guardado na loja para ativar"
    foreach ($m in $k.Mains) {
        $i = $arq.CP.L.FindIndex({ param($l) $c = $l.Split('@'); $c.Count -ge 26 -and $c[2] -eq "$m" -and $c[20] -eq '6780' })
        if ($i -lt 0) { throw "pacote $m (ticket) não encontrado no IBSPackage.txt do cliente" }
        Trocar 'CP' $i (Campos $arq.CP.L[$i] @{ 3 = $k.Nome; 5 = "$($k.Prod[0].Preco)"; 6 = $desc }); $feitos++
    }
}

foreach ($k in 'SP', 'SR', 'CP', 'CR') { if ($orig | Where-Object { $_.StartsWith("$k`t") }) { Gravar $arq[$k] } }
[IO.File]::WriteAllLines($fOrig, $orig, $enc)
"Passe dos Mapas na loja de cash: $feitos ajuste(s), $($orig.Count) linha(s) trocada(s); originais em $fOrig."
"Preços (WCoin): 1 dia $Preco1, 3 dias $Preco3, 7 dias $Preco7, 30 dias $Preco30."
'Falta: esconder os pacotes em WCoin P (32:194, 32:157, 32:168), Reload CashShop e publicar o cliente.'

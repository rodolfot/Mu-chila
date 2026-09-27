# Teste do mercado pelo site (como um jogador): cadastra vendedor e comprador, liga o vendedor (modo de teste),
# anuncia um item do baú, compra, simula o pagamento e confere a entrega. Procura erros de PHP na tela e no log.
# Apaga as contas de teste no fim. Uso: .\teste-mercado-site.ps1
param([string]$Base = 'http://localhost')
$ErrorActionPreference = 'Stop'
$V = 'mkwvend'; $C = 'mkwcomp'; $JOIA = '0D0001000000000000E000FFFFFFFFFF'; $ok = 0; $falhas = 0
function Confere($desc, $cond, $extra = '') { if ($cond) { $script:ok++; "OK    $desc" } else { $script:falhas++; "FALHA $desc  $extra" } }
function Sql($q) { sqlcmd -S .\MUONLINE -d MuOnlineS14 -E -C -I -h -1 -W -Q "SET NOCOUNT ON; $q" }
function Texto($r) { ([Net.WebUtility]::HtmlDecode([regex]::Replace([regex]::Replace($r.Content, '(?s)<script.*?</script>', ''), '<[^>]+>', ' ')) -replace '\s+', ' ') }
function Csrf($r) { [regex]::Match($r.Content, 'name="csrf" value="([0-9a-f]+)"').Groups[1].Value }
function SemErro($r) { $r.StatusCode -eq 200 -and $r.Content -notmatch 'Fatal error|Parse error|Warning:|Notice:|Deprecated:|Uncaught' }
function Limpar {
    $in = "'$V','$C'"
    Sql "DELETE FROM MUCHILA_MERCADO_PEDIDOS WHERE comprador IN ($in) OR vendedor IN ($in); DELETE FROM MUCHILA_MERCADO_ANUNCIOS WHERE vendedor IN ($in);
         DELETE FROM MUCHILA_MERCADO_VENDEDORES WHERE conta IN ($in); DELETE FROM warehouse WHERE AccountID IN ($in); DELETE FROM AccountCharacter WHERE Id IN ($in);
         DELETE FROM MEMB_STAT WHERE memb___id IN ($in); DELETE FROM MEMB_INFO WHERE memb___id IN ($in)" | Out-Null
}
function Cadastrar($conta) {
    $null = Invoke-WebRequest "$Base/register" -SessionVariable s -UseBasicParsing
    $null = Invoke-WebRequest "$Base/register" -WebSession $s -UseBasicParsing -Method Post -Body @{
        webengineRegister_user = $conta; webengineRegister_pwd = 'teste123'; webengineRegister_pwdc = 'teste123'
        webengineRegister_email = "$conta@exemplo.com"; webengineRegister_submit = 'submit' }
    $s
}

$log = 'C:\MuServer\Site\www\includes\logs\php_errors.log'
$linhasAntes = if (Test-Path $log) { (Get-Content $log).Count } else { 0 }
Limpar
try {
    $sv = Cadastrar $V; $sc = Cadastrar $C
    Sql "INSERT INTO warehouse (AccountID, Items, Money, EndUseDate, DbVersion, pw) VALUES ('$V', CONVERT(varbinary(3840), '$JOIA' + REPLICATE('FF', 3824), 2), 0, GETDATE(), 3, 0)" | Out-Null

    # vendedor: ligar a conta (modo de teste)
    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=conta" -WebSession $sv -UseBasicParsing
    Confere 'aba "Minha conta de vendedor" abre sem erro' (SemErro $r)
    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=conta" -WebSession $sv -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); conectar = '1' }
    Confere 'vendedor liga a conta (modo de teste)' ((SemErro $r) -and (Texto $r) -match 'est. ligada') (Texto $r).Substring(0, 200)

    # vendedor: anunciar a joia
    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=vender" -WebSession $sv -UseBasicParsing
    Confere 'aba "Vender" lista o item do baú' ((SemErro $r) -and (Texto $r) -match 'Jewel of Bless')
    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=vender" -WebSession $sv -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); posicao = '0'; hex = $JOIA; valor = '12,50'; anunciar_item = '1' }
    Confere 'anúncio criado e aparece em "Meus anúncios" à venda' ((SemErro $r) -and (Texto $r) -match 'Jewel of Bless' -and (Texto $r) -match 'venda')
    Confere 'joia saiu do baú do vendedor' ((Sql "SELECT CHARINDEX('$JOIA', CONVERT(varchar(7680), Items, 2)) FROM warehouse WHERE AccountID = '$V'").Trim() -eq '0')

    # comprador: achar e comprar
    $r = Invoke-WebRequest "$Base/usercp/mercado" -WebSession $sc -UseBasicParsing
    $id = [regex]::Match($r.Content, 'name="comprar" value="(\d+)"').Groups[1].Value
    Confere 'aba "Comprar" mostra o anúncio com o preço' ((SemErro $r) -and $id -ne '' -and (Texto $r) -match '12,50')
    $r = Invoke-WebRequest "$Base/usercp/mercado" -WebSession $sc -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); comprar = $id }
    $pedido = [regex]::Match($r.Content, 'name="pedido" value="(\d+)"').Groups[1].Value
    Confere 'compra abre o PIX de teste aguardando pagamento' ((SemErro $r) -and (Texto $r) -match 'PIX-DE-TESTE' -and (Texto $r) -match 'Aguardando pagamento')
    $r = Invoke-WebRequest "$Base/usercp/mercado?pedido=$pedido" -WebSession $sc -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); pedido = $pedido; simular = 'aprovar' }
    Confere 'pagamento simulado entrega a compra' ((SemErro $r) -and (Texto $r) -match 'Entregue')
    Confere 'joia está no baú do comprador' ((Sql "SELECT CHARINDEX('$JOIA', CONVERT(varchar(7680), Items, 2)) FROM warehouse WHERE AccountID = '$C'").Trim() -eq '1')

    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=compras" -WebSession $sc -UseBasicParsing
    Confere 'aba "Minhas compras" mostra entregue' ((SemErro $r) -and (Texto $r) -match 'Entregue')
    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=meus" -WebSession $sv -UseBasicParsing
    Confere 'vendedor vê o anúncio como vendido' ((SemErro $r) -and (Texto $r) -match 'Vendido')
    $r = Invoke-WebRequest "$Base/usercp/mercado?aba=comprar" -WebSession $sc -UseBasicParsing
    Confere 'anúncio vendido saiu da vitrine' ((SemErro $r) -and $r.Content -notmatch "name=`"comprar`" value=`"$id`"")

    # CSRF falso não faz nada
    $r = Invoke-WebRequest "$Base/usercp/mercado" -WebSession $sc -UseBasicParsing -Method Post -Body @{ csrf = 'falso'; comprar = $id }
    Confere 'formulário sem o token da sessão é recusado' ((Texto $r) -match 'Sess.o expirada')

    # retorno do OAuth sem autorização volta para a aba da conta
    $res = (curl.exe -s -o NUL -w '%{http_code} %{redirect_url}' "$Base/api/muchila-mercado-oauth.php?state=abc&code=x")
    Confere 'retorno do OAuth inválido redireciona para a conta de vendedor' ($res -match '^302 .*usercp/mercado') $res
} catch {
    Confere 'sem erro inesperado' $false $_.Exception.Message
} finally {
    Limpar
}
$novos = if (Test-Path $log) { Get-Content $log | Select-Object -Skip $linhasAntes | Where-Object { $_ -match 'mercado|Mercado|MuChilaItens' } } else { @() }
Confere 'nenhum erro de PHP do mercado no log' (-not $novos) ($novos -join ' | ')
""; "$ok ok, $falhas falha(s)"

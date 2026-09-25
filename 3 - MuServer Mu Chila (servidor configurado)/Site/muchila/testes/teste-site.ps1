# Teste de ponta a ponta do site (como um navegador): cadastro, loja (modo de teste), rankings e painel admin.
# Usa a conta descartável sitetest1 e apaga tudo no final.
# Uso: .\teste-site.ps1 -Admin <conta admin do site> -AdminSenha <senha>
param([string]$Base = 'http://localhost', [Parameter(Mandatory)][string]$Admin, [Parameter(Mandatory)][string]$AdminSenha)
$ErrorActionPreference = 'Stop'
$conta = 'sitetest1'; $ok = 0; $falhas = 0
function Confere($desc, $cond, $extra = '') { if ($cond) { $script:ok++; "OK    $desc" } else { $script:falhas++; "FALHA $desc  $extra" } }
function Sql($q) { sqlcmd -S .\MUONLINE -d MuOnlineS14 -E -C -I -h -1 -W -Q "SET NOCOUNT ON; $q" }
function Texto($r) { ([Net.WebUtility]::HtmlDecode([regex]::Replace([regex]::Replace($r.Content, '(?s)<script.*?</script>', ''), '<[^>]+>', ' ')) -replace '\s+', ' ') }
function Csrf($r) { [regex]::Match($r.Content, 'name="csrf" value="([0-9a-f]+)"').Groups[1].Value }

try {
    # cadastro (com login automático)
    $r = Invoke-WebRequest "$Base/register" -SessionVariable s -UseBasicParsing
    $r = Invoke-WebRequest "$Base/register" -WebSession $s -UseBasicParsing -Method Post -Body @{
        webengineRegister_user = $conta; webengineRegister_pwd = 'teste123'; webengineRegister_pwdc = 'teste123'
        webengineRegister_email = 'sitetest1@exemplo.com'; webengineRegister_submit = 'submit' }
    $linha = Sql "SELECT memb___id + '|' + memb__pwd + '|' + ISNULL(mail_addr,'') + '|' + bloc_code FROM MEMB_INFO WHERE memb___id = '$conta'"
    Confere 'cadastro gravou a conta no MEMB_INFO (senha em texto, desbloqueada)' ($linha -eq "$conta|teste123|sitetest1@exemplo.com|0") $linha

    # loja
    $r = Invoke-WebRequest "$Base/usercp/loja" -WebSession $s -UseBasicParsing
    $t = Texto $r
    Confere 'loja abre logado, em modo de teste, com os pacotes' ($t -match 'MODO DE TESTE' -and $t -match 'VIP 1 - 30 dias' -and $t -match '1\.000 Cash') ($t.Substring(0, [Math]::Min(300, $t.Length)))
    $r = Invoke-WebRequest "$Base/usercp/loja" -WebSession $s -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); comprar = 'cash-1000' }
    $pedido = [regex]::Match($r.BaseResponse.RequestMessage.RequestUri.Query, 'pedido=(\d+)').Groups[1].Value
    $t = Texto $r
    Confere 'comprar leva à página do pedido com PIX de teste' ($pedido -and $t -match 'PIX-DE-TESTE-NAO-PAGUE' -and $t -match 'Aguardando pagamento') "pedido=$pedido"
    $r = Invoke-WebRequest "$Base/usercp/loja?pedido=$pedido" -WebSession $s -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); pedido = $pedido; simular = 'aprovar' }
    $t = Texto $r
    Confere 'simular aprovação entrega o cash e mostra a confirmação' ($t -match 'Pagamento confirmado' -and (Sql "SELECT WCoinC FROM CashShopData WHERE AccountID = '$conta'") -eq '1000')
    $r = Invoke-WebRequest "$Base/usercp/loja" -WebSession $s -UseBasicParsing
    $r = Invoke-WebRequest "$Base/usercp/loja" -WebSession $s -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); comprar = 'vip1-30' }
    $pv = [regex]::Match($r.BaseResponse.RequestMessage.RequestUri.Query, 'pedido=(\d+)').Groups[1].Value
    $r = Invoke-WebRequest "$Base/usercp/loja?pedido=$pv" -WebSession $s -UseBasicParsing -Method Post -Body @{ csrf = (Csrf $r); pedido = $pv; simular = 'aprovar' }
    Confere 'VIP 1 entregue pelo site' ((Sql "SELECT CAST(AccountLevel AS varchar) + '|' + CAST(DATEDIFF(day, GETDATE(), AccountExpireDate) AS varchar) FROM MEMB_INFO WHERE memb___id = '$conta'") -in '1|30', '1|29')
    $r = Invoke-WebRequest "$Base/usercp/loja" -WebSession $s -UseBasicParsing -Method Post -Body @{ csrf = 'falso'; comprar = 'cash-1000' }
    Confere 'POST sem o token da sessão é recusado' ((Texto $r) -match 'Sessão expirada')
    $r = Invoke-WebRequest "$Base/usercp/loja?pedido=1" -WebSession $s -UseBasicParsing
    Confere 'pedido de outra conta não abre' ((Texto $r) -match 'Pedido não encontrado' -or $pedido -eq '1')

    # rankings
    $r = Invoke-WebRequest "$Base/rankings/resets" -UseBasicParsing
    Confere 'ranking de resets mostra a mamaeupo' ((Texto $r) -match 'mamaeupo')

    # painel admin
    $r = Invoke-WebRequest "$Base/login" -SessionVariable a -UseBasicParsing
    $r = Invoke-WebRequest "$Base/login" -WebSession $a -UseBasicParsing -Method Post -Body @{ webengineLogin_user = $Admin; webengineLogin_pwd = $AdminSenha; webengineLogin_submit = 'submit' }
    $r = Invoke-WebRequest "$Base/admincp/index.php?module=muchila_pedidos" -WebSession $a -UseBasicParsing
    $t = Texto $r
    Confere 'painel admin lista os pedidos da loja' ($t -match 'Loja: pedidos de VIP e cash' -and $t -match $conta -and $t -match 'Pago e entregue') ($t.Substring(0, [Math]::Min(300, $t.Length)))
    $r2 = Invoke-WebRequest "$Base/admincp/index.php?module=muchila_pedidos" -WebSession $s -UseBasicParsing
    Confere 'conta comum não entra no painel admin' ((Texto $r2) -notmatch 'Loja: pedidos de VIP e cash')
} finally {
    Sql "DELETE FROM MUCHILA_PEDIDOS WHERE conta = '$conta'; DELETE FROM CashShopData WHERE AccountID = '$conta'; DELETE FROM WEBENGINE_ACCOUNT_COUNTRY WHERE account = '$conta'; IF OBJECT_ID('VI_CURR_INFO') IS NOT NULL DELETE FROM VI_CURR_INFO WHERE memb___id = '$conta'; DELETE FROM MEMB_INFO WHERE memb___id = '$conta'; SELECT 'limpeza: ' + CAST((SELECT COUNT(*) FROM MEMB_INFO WHERE memb___id = '$conta') AS varchar) + ' conta(s) de teste restante(s)'"
}
"resultado: $ok ok, $falhas falha(s)"


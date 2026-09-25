<?php
// Teste temporário do núcleo da loja (provedor simulado) com a conta descartável "lojateste". Apagar depois.
require 'C:/MuServer/Site/muchila/www/includes/muchila/MuChilaLoja.php';

$ok = 0; $falhas = 0;
function confere(string $desc, bool $cond, string $extra = '') { global $ok, $falhas; $cond ? $ok++ : $falhas++; echo ($cond ? 'OK    ' : 'FALHA ') . $desc . ($extra !== '' ? "  [$extra]" : '') . PHP_EOL; }

$loja = new MuChilaLoja();
$db = $loja->db;
$conta = 'lojateste';
$db->prepare("IF NOT EXISTS (SELECT 1 FROM MEMB_INFO WHERE memb___id = ?) INSERT INTO MEMB_INFO (memb___id, memb__pwd, memb_name, sno__numb, mail_addr, bloc_code, ctl1_code, appl_days) VALUES (?, 'teste1', 'teste', '1', 'teste@exemplo.com', '0', '0', GETDATE())")->execute([$conta, $conta]);

try {
    confere('modo de teste (provedor simulado)', $loja->modoTeste());
    confere('pacotes carregados', count($loja->pacotes) === 6, implode(',', array_keys($loja->pacotes)));

    // 1) VIP 1: pedido, pagamento simulado, entrega
    $p = $loja->criarPedido($conta, 'vip1-30', '127.0.0.1');
    confere('pedido VIP criado pendente com PIX de teste', $p['status'] === 'pendente' && str_starts_with($p['pix_copia_cola'], 'PIX-DE-TESTE') && str_starts_with($p['provedor_id'], 'SIM-'));
    $r = $loja->simular((int)$p['id'], $conta, true);
    $c = $loja->conta($conta);
    confere('VIP 1 entregue por 30 dias', $loja->pedido((int)$p['id'])['status'] === 'entregue' && (int)$c['vip_nivel'] === 1 && $c['vip_ativo'] == 1
        && abs(strtotime($c['vip_expira']) - strtotime('+30 days')) < 180, $r . ' | expira ' . $c['vip_expira']);

    // 2) o mesmo aviso de novo não entrega de novo
    $expira1 = $c['vip_expira'];
    $r2 = $loja->processarAviso($p['provedor_id'], 'repetido');
    confere('aviso repetido não entrega duas vezes', $loja->conta($conta)['vip_expira'] === $expira1, $r2);

    // 3) renovar VIP 1 soma 30 dias ao vencimento
    $p2 = $loja->criarPedido($conta, 'vip1-30', '127.0.0.1');
    $loja->simular((int)$p2['id'], $conta, true);
    $c = $loja->conta($conta);
    confere('renovação soma aos dias restantes (~60 dias)', abs(strtotime($c['vip_expira']) - strtotime('+60 days')) < 180, $c['vip_expira']);

    // 3b) Zen de bônus do VIP no baú (a conta de teste nunca abriu o baú e está fora do jogo)
    $zenBau = function() use ($db, $conta) { $s = $db->prepare("SELECT Money FROM warehouse WHERE AccountID = ?"); $s->execute([$conta]); return $s->fetchColumn(); };
    $s = $db->prepare("SELECT DATALENGTH(Items) AS n, CASE WHEN Items = CAST(REPLICATE(CAST(CHAR(255) AS varchar(max)), 3840) AS varbinary(3840)) THEN 1 ELSE 0 END AS vazio FROM warehouse WHERE AccountID = ?");
    $s->execute([$conta]); $bau = $s->fetch();
    confere('Zen dos 2 VIP 1 no baú criado vazio (2 x 200.000.000)', (int)$zenBau() === 400000000 && (int)$bau['n'] === 3840 && (int)$bau['vazio'] === 1, 'baú=' . $zenBau());
    // no jogo: o Zen espera a conta sair
    $db->prepare("DELETE FROM MEMB_STAT WHERE memb___id = ?; INSERT INTO MEMB_STAT (memb___id, ConnectStat, ServerName, IP, ConnectTM, DisConnectTM) VALUES (?, 1, 'Teste', '127.0.0.1', GETDATE(), GETDATE())")->execute([$conta, $conta]);
    $pz = $loja->criarPedido($conta, 'vip1-30', '127.0.0.1');
    $loja->simular((int)$pz['id'], $conta, true);
    $pz = $loja->pedido((int)$pz['id']);
    confere('comprado no jogo: VIP entregue, Zen aguardando', $pz['status'] === 'entregue' && $pz['zen_entregue_em'] === null && (int)$zenBau() === 400000000);
    confere('tarefa não entrega Zen com a conta no jogo', count($loja->entregarZenPendente($conta)) === 0 && (int)$zenBau() === 400000000);
    $db->prepare("UPDATE MEMB_STAT SET ConnectStat = 0, DisConnectTM = DATEADD(minute, -1, GETDATE()) WHERE memb___id = ?")->execute([$conta]);
    $feitos = $loja->entregarZenPendente($conta);
    confere('depois de sair do jogo, a tarefa entrega o Zen uma vez', count($feitos) === 1 && (int)$zenBau() === 600000000 && count($loja->entregarZenPendente($conta)) === 0, implode(' | ', $feitos));
    // limite de 2 bilhões
    $db->prepare("UPDATE warehouse SET Money = 1900000000 WHERE AccountID = ?")->execute([$conta]);
    $pl = $loja->criarPedido($conta, 'vip1-30', '127.0.0.1');
    $loja->simular((int)$pl['id'], $conta, true);
    confere('Zen para no limite de 2.000.000.000', (int)$zenBau() === 2000000000 && str_contains((string)$loja->pedido((int)$pl['id'])['obs'], 'limite'), (string)$loja->pedido((int)$pl['id'])['obs']);

    // 4) outro nível com VIP ativo é recusado
    try { $loja->criarPedido($conta, 'vip2-30', '127.0.0.1'); confere('VIP 2 com VIP 1 ativo é recusado', false); }
    catch (Exception $e) { confere('VIP 2 com VIP 1 ativo é recusado', true, $e->getMessage()); }

    // 5) cash: conta sem linha no CashShopData
    $db->prepare("DELETE FROM CashShopData WHERE AccountID = ?")->execute([$conta]);
    $p3 = $loja->criarPedido($conta, 'cash-1000', '127.0.0.1');
    $loja->simular((int)$p3['id'], $conta, true);
    confere('cash entregue criando a linha (1000)', (int)$loja->conta($conta)['cash'] === 1000);
    $p4 = $loja->criarPedido($conta, 'cash-5000', '127.0.0.1');
    $loja->simular((int)$p4['id'], $conta, true);
    confere('cash somado (1000 + 5500)', (int)$loja->conta($conta)['cash'] === 6500);

    // 6) pagamento recusado cancela e não entrega
    $p5 = $loja->criarPedido($conta, 'cash-1000', '127.0.0.1');
    $r5 = $loja->simular((int)$p5['id'], $conta, false);
    confere('recusado vira cancelado sem cash', $loja->pedido((int)$p5['id'])['status'] === 'cancelado' && (int)$loja->conta($conta)['cash'] === 6500, $r5);

    // 7) limite de pendentes
    for ($i = 0; $i < 3; $i++) $loja->criarPedido($conta, 'cash-1000', '127.0.0.1');
    try { $loja->criarPedido($conta, 'cash-1000', '127.0.0.1'); confere('4º pedido pendente é recusado', false); }
    catch (Exception $e) { confere('4º pedido pendente é recusado', true, $e->getMessage()); }

    // 8) pedido de outra conta não aparece
    confere('pedido de outra conta não é visível', $loja->pedido((int)$p['id'], 'outraconta') === null);

    // 9) expiração
    $db->prepare("UPDATE MUCHILA_PEDIDOS SET expira = DATEADD(minute, -1, GETDATE()) WHERE conta = ? AND status = 'pendente'")->execute([$conta]);
    $lista = $loja->pedidosDaConta($conta);
    confere('pendentes vencidos viram expirados', count(array_filter($lista, fn($x) => $x['status'] === 'pendente')) === 0 && count(array_filter($lista, fn($x) => $x['status'] === 'expirado')) === 3);

    // 10) pagamento que chega depois de expirar ainda é entregue
    $exp = array_values(array_filter($lista, fn($x) => $x['status'] === 'expirado'))[0];
    $db->prepare("UPDATE MUCHILA_PEDIDOS SET simulado_status = 'aprovado' WHERE id = ?")->execute([$exp['id']]);
    $loja->processarAviso($exp['provedor_id'], 'atrasado');
    confere('pago depois de expirar é entregue', $loja->pedido((int)$exp['id'])['status'] === 'entregue' && (int)$loja->conta($conta)['cash'] === 7500);
} finally {
    $db->prepare("DELETE FROM MUCHILA_PEDIDOS WHERE conta = ?")->execute([$conta]);
    $db->prepare("DELETE FROM CashShopData WHERE AccountID = ?")->execute([$conta]);
    $db->prepare("DELETE FROM warehouse WHERE AccountID = ?")->execute([$conta]);
    $db->prepare("DELETE FROM MEMB_STAT WHERE memb___id = ?")->execute([$conta]);
    $db->prepare("DELETE FROM MEMB_INFO WHERE memb___id = ?")->execute([$conta]);
    echo "limpeza: conta $conta, pedidos, cash, baú e status apagados" . PHP_EOL;
}
echo "resultado: $ok ok, $falhas falha(s)" . PHP_EOL;

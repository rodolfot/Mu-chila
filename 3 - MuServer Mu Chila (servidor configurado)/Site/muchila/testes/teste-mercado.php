<?php
// Teste de ponta a ponta do mercado entre jogadores (provedor simulado), com contas descartáveis criadas e apagadas aqui.
// Rodar: C:\MuServer\Site\php\php.exe C:\MuServer\Site\muchila\testes\teste-mercado.php
require 'C:/MuServer/Site/muchila/www/includes/muchila/MuChilaMercado.php';

$ok = 0; $falhas = 0;
function confere(string $desc, bool $cond, string $extra = '') { global $ok, $falhas; $cond ? $ok++ : $falhas++; echo ($cond ? 'OK    ' : 'FALHA ') . $desc . ($extra !== '' ? "  [$extra]" : '') . PHP_EOL; }
function erro(callable $f): string { try { $f(); return ''; } catch (Exception $e) { return $e->getMessage(); } }

$m = new MuChilaMercado();
$db = $m->db;
[$V, $C, $C2, $P] = ['mktvend', 'mktcomp', 'mktcomp2', 'mkttest'];
$JOIA = '0D0001000000000000E000FFFFFFFFFF';   // Jewel of Bless (14,13), 1x1
$ARMA = '0548320000000000000000FFFFFFFFFF';   // item 0,5 +9 (espada)

$limpar = function() use ($db, $V, $C, $C2, $P) {
    $contas = [$V, $C, $C2];
    $in = implode(',', array_fill(0, 3, '?'));
    $db->prepare("DELETE FROM MUCHILA_MERCADO_PEDIDOS WHERE comprador IN ($in) OR vendedor IN ($in)")->execute(array_merge($contas, $contas));
    $db->prepare("DELETE FROM MUCHILA_MERCADO_ANUNCIOS WHERE vendedor IN ($in)")->execute($contas);
    $db->prepare("DELETE FROM MUCHILA_MERCADO_VENDEDORES WHERE conta IN ($in)")->execute($contas);
    $db->prepare("DELETE FROM Character WHERE Name = ?")->execute([$P]);
    foreach (['AccountCharacter' => 'Id', 'warehouse' => 'AccountID', 'MEMB_STAT' => 'memb___id', 'MEMB_INFO' => 'memb___id'] as $t => $k)
        $db->prepare("DELETE FROM $t WHERE $k IN ($in)")->execute($contas);
};
$bau = function(string $conta) use ($db) {
    $st = $db->prepare("SELECT CONVERT(varchar(7680), Items, 2) FROM warehouse WHERE AccountID = ?"); $st->execute([$conta]);
    $h = $st->fetchColumn(); return $h === false ? null : strtoupper($h);
};
$qtd = function(?string $bauHex, string $item) { return $bauHex === null ? 0 : count(array_filter(str_split($bauHex, 32), fn($x) => $x === $item)); };
$online = function(string $conta, bool $sim) use ($db) {
    $db->prepare("DELETE FROM MEMB_STAT WHERE memb___id = ?")->execute([$conta]);
    // datas calculadas no SQL (texto de data depende do idioma do servidor)
    $db->prepare("INSERT INTO MEMB_STAT (memb___id, ConnectStat, ServerName, IP, ConnectTM, DisConnectTM, OnlineHours)
        VALUES (?, ?, 'teste', '127.0.0.1', GETDATE(), DATEADD(minute, CAST(? AS int), GETDATE()), 0)")
       ->execute([$conta, $sim ? 1 : 0, $sim ? 0 : -60]);
};

$limpar();
try {
    // ---------------------------------------------------------------- preparo
    foreach ([$V, $C, $C2] as $c)
        $db->prepare("INSERT INTO MEMB_INFO (memb___id, memb__pwd, memb_name, sno__numb, mail_addr, bloc_code, ctl1_code, appl_days) VALUES (?, 'teste1', 'teste', '1', 'teste@exemplo.com', '0', '0', GETDATE())")->execute([$c]);
    $vazio = str_repeat('FF', 3840);
    $bauV = substr_replace(substr_replace($vazio, $JOIA, 0, 32), $ARMA, 32, 32);   // joia na posição 0, arma na 1
    $db->prepare("INSERT INTO warehouse (AccountID, Items, Money, EndUseDate, DbVersion, pw) VALUES (?, CONVERT(varbinary(3840), ?, 2), 0, GETDATE(), 3, 0)")->execute([$V, $bauV]);
    // personagem de teste: cópia do Quest8 com o inventário vazio, na conta do vendedor
    $db->exec("SELECT * INTO #c FROM Character WHERE Name = 'Quest8'");
    $db->prepare("UPDATE #c SET Name = ?, AccountID = ?, CtlCode = 0, Inventory = CAST(REPLICATE(CAST(CHAR(255) AS varchar(max)), DATALENGTH(Inventory)) AS varbinary(max))")->execute([$P, $V]);
    $db->exec("INSERT INTO Character SELECT * FROM #c; DROP TABLE #c;");
    $db->prepare("INSERT INTO AccountCharacter (Id, GameID1, MoveCnt, ExtClass, ExtWarehouse) VALUES (?, ?, 0, 0, 0)")->execute([$V, $P]);

    confere('modo de teste (simulado) e mercado aberto', $m->modoTeste() && $m->ativa());

    // ---------------------------------------------------------------- vendedor precisa ligar o Mercado Pago
    $e = erro(fn() => $m->anunciarItem($V, 0, $JOIA, 50));
    confere('sem ligar o Mercado Pago não anuncia', str_contains($e, 'Mercado Pago'), $e);
    $m->conectarSimulado($V);
    confere('vendedor ligado (simulado)', $m->vendedorLigado($V));

    // ---------------------------------------------------------------- anunciar item (custódia)
    $itens = $m->itensDoBau($V);
    confere('baú do vendedor lista 2 itens', count($itens) === 2, implode(' / ', array_column($itens, 'descricao')));
    $e = erro(fn() => $m->anunciarItem($V, 0, $ARMA, 50));
    confere('hex diferente do da posição é recusado', str_contains($e, 'mudou'), $e);
    $e = erro(fn() => $m->anunciarItem($V, 0, $JOIA, 0.5));
    confere('preço abaixo do mínimo é recusado', str_contains($e, 'Preço'), $e);
    $anJoia = $m->anunciarItem($V, 0, $JOIA, 25);
    confere('joia anunciada e SAIU do baú do vendedor', $qtd($bau($V), $JOIA) === 0 && $m->anuncio($anJoia)['item_hex'] === $JOIA);

    // ---------------------------------------------------------------- compra disputada
    $e = erro(fn() => $m->comprar($V, $anJoia, '127.0.0.1'));
    confere('vendedor não compra o próprio anúncio', str_contains($e, 'próprio'), $e);
    $ped = $m->comprar($C, $anJoia, '127.0.0.1');
    confere('compra gera pedido pendente com PIX de teste e taxa de 10%', $ped['status'] === 'pendente' && str_starts_with($ped['pix_copia_cola'], 'PIX-DE-TESTE') && (float)$ped['taxa'] === 2.5, 'taxa ' . $ped['taxa']);
    confere('anúncio fica reservado', $m->anuncio($anJoia)['status'] === 'reservado');
    $e = erro(fn() => $m->comprar($C2, $anJoia, '127.0.0.1'));
    confere('segundo comprador não consegue o mesmo anúncio', str_contains($e, 'não está mais à venda'), $e);

    // ---------------------------------------------------------------- pagamento aprovado -> entrega
    $r = $m->simular((int)$ped['id'], $C, true);
    $pf = $m->pedido((int)$ped['id']);
    confere('pagamento aprovado entrega no baú do comprador', $pf['status'] === 'entregue' && $qtd($bau($C), $JOIA) === 1, $r);
    confere('anúncio vendido para o comprador', $m->anuncio($anJoia)['status'] === 'vendido' && $m->anuncio($anJoia)['comprador'] === $C);
    $r2 = $m->processarAviso($pf['provedor_id'], 'repetido');
    confere('aviso repetido não entrega de novo (1 joia só)', $qtd($bau($C), $JOIA) === 1 && $qtd($bau($V), $JOIA) === 0, $r2);

    // ---------------------------------------------------------------- cancelar anúncio devolve o item
    $anArma = $m->anunciarItem($V, 1, $ARMA, 80);
    confere('arma anunciada saiu do baú', $qtd($bau($V), $ARMA) === 0);
    $r = $m->cancelarAnuncio($V, $anArma);
    confere('cancelar devolve a arma ao baú do vendedor', $qtd($bau($V), $ARMA) === 1 && $m->anuncio($anArma)['status'] === 'cancelado', $r);
    $e = erro(fn() => $m->cancelarAnuncio($V, $anArma));
    confere('cancelar de novo não duplica', $qtd($bau($V), $ARMA) === 1 && $e !== '', $e);

    // ---------------------------------------------------------------- recusado e PIX vencido liberam o anúncio
    $posArma = intdiv(strpos($bau($V), $ARMA), 32);
    $anArma2 = $m->anunciarItem($V, $posArma, $ARMA, 80);
    $pr = $m->comprar($C2, $anArma2, '127.0.0.1');
    $m->simular((int)$pr['id'], $C2, false);
    confere('pagamento recusado: pedido cancelado e anúncio volta à venda', $m->pedido((int)$pr['id'])['status'] === 'cancelado' && $m->anuncio($anArma2)['status'] === 'ativo');
    $pr2 = $m->comprar($C2, $anArma2, '127.0.0.1');
    $db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET expira = DATEADD(minute, -1, GETDATE()) WHERE id = ?")->execute([$pr2['id']]);
    $m->expirarVencidos();
    confere('PIX vencido: pedido expirado e anúncio volta à venda', $m->pedido((int)$pr2['id'])['status'] === 'expirado' && $m->anuncio($anArma2)['status'] === 'ativo');

    // pago DEPOIS de vencer, mas outro comprador já levou: não entrega em dobro, fica para reembolso
    $pr3 = $m->comprar($C, $anArma2, '127.0.0.1');
    $m->simular((int)$pr3['id'], $C, true);
    $db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET simulado_status = 'aprovado' WHERE id = ?")->execute([$pr2['id']]);
    $r = $m->processarAviso($m->pedido((int)$pr2['id'])['provedor_id'], 'atrasado');
    confere('pagamento atrasado de anúncio já vendido vira "reembolsar" (1 arma só)', $m->pedido((int)$pr2['id'])['status'] === 'falhou'
        && $qtd($bau($C), $ARMA) === 1 && $qtd($bau($C2), $ARMA) === 0 && $qtd($bau($V), $ARMA) === 0, $r);

    // ---------------------------------------------------------------- personagem
    $e = erro(fn() => $m->anunciarPersonagem($V, 'Quest8', 100));
    confere('não anuncia personagem de outra conta', str_contains($e, 'não encontrado'), $e);
    $anP = $m->anunciarPersonagem($V, $P, 100);
    $st = $db->prepare("SELECT AccountID FROM Character WHERE Name = ?"); $st->execute([$P]);
    $ac = $db->prepare("SELECT GameID1 FROM AccountCharacter WHERE Id = ?"); $ac->execute([$V]);
    confere('personagem anunciado vai para a custódia e sai da vaga', $st->fetchColumn() === MuChilaMercado::CUSTODIA && $ac->fetchColumn() === null);

    // comprador online: pago, mas a entrega espera ele sair
    $online($C, true);
    $pp = $m->comprar($C, $anP, '127.0.0.1');
    $r = $m->simular((int)$pp['id'], $C, true);
    confere('comprador online: fica "pago" aguardando sair do jogo', $m->pedido((int)$pp['id'])['status'] === 'pago', $r);
    $online($C, false);
    $rs = $m->entregarPendentes();
    $st->execute([$P]);
    $vaga = $db->prepare("SELECT CASE WHEN ? IN (GameID1,GameID2,GameID3,GameID4,GameID5,GameID6,GameID7,GameID8) THEN 1 ELSE 0 END FROM AccountCharacter WHERE Id = ?");
    $vaga->execute([$P, $C]);
    confere('ao sair do jogo, o personagem vai para a conta do comprador', $m->pedido((int)$pp['id'])['status'] === 'entregue' && $st->fetchColumn() === $C && (int)$vaga->fetchColumn() === 1, implode(' / ', $rs));
    confere('personagem sumiu das listas do vendedor', count($m->personagensDaConta($V)) === 0);
} catch (Throwable $t) {
    confere('sem erro inesperado', false, $t->getMessage() . ' @' . $t->getLine());
} finally {
    $limpar();
}
$st = $db->prepare("SELECT COUNT(*) FROM Character WHERE Name = ?"); $st->execute([$P]);
confere('limpeza: nada de teste sobrou', (int)$st->fetchColumn() === 0);
echo PHP_EOL . "$ok ok, $falhas falha(s)" . PHP_EOL;
exit($falhas > 0 ? 1 : 0);

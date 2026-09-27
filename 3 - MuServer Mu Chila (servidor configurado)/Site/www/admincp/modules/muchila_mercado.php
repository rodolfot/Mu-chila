<?php
/**
 * Mu Chila - mercado entre jogadores (AdminCP): compras e anúncios. Ações: entregar de novo (pago, aguardando) e
 * cancelar compra não paga. Compras "com problema" (ex.: pago depois de vencer, com o anúncio já vendido a outro)
 * precisam de REEMBOLSO no Mercado Pago. Núcleo: includes/muchila/MuChilaMercado.php.
 */
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMercado.php');
$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
if(empty($_SESSION['muchila_admin_csrf'])) $_SESSION['muchila_admin_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_admin_csrf'];
echo '<h1 class="page-header">Mercado entre jogadores</h1>';
try {
	$m = new MuChilaMercado();
	echo '<p>Provedor: <strong>'.$h($m->cfg['provedor']).'</strong>'.($m->modoTeste() ? ' (modo de teste: nenhum pagamento é real)' : ' (PIX com split: o dinheiro vai direto ao vendedor)')
		.' &nbsp;|&nbsp; Taxa da loja: <strong>'.$h($m->taxaPct()).'%</strong> &nbsp;|&nbsp; Mercado '.($m->ativa() ? 'aberto' : '<strong>fechado</strong>')
		.'. Config: <code>includes/config/modules/usercp.mercado.xml</code>.</p>';
	if($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['acao'], $_POST['pedido'])) {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada; recarregue a página.');
		$id = (int)$_POST['pedido'];
		if($_POST['acao'] === 'entregar') message('info', $h($m->entregar($id, 'admin:' . $_SESSION['username'])));
		if($_POST['acao'] === 'cancelar') message('info', $h($m->cancelarPedido($id, 'Cancelado por admin:' . $_SESSION['username'])));
	}
	$mes = $m->db->query("SELECT COUNT(*) AS n, ISNULL(SUM(taxa), 0) AS taxa, ISNULL(SUM(valor), 0) AS total FROM MUCHILA_MERCADO_PEDIDOS
		WHERE status = 'entregue' AND provedor <> 'simulado' AND pago_em >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)")->fetch();
	$abertos = (int)$m->db->query("SELECT COUNT(*) FROM MUCHILA_MERCADO_ANUNCIOS WHERE status IN ('ativo', 'reservado')")->fetchColumn();
	echo '<p>Neste mês (sem testes): '.(int)$mes['n'].' venda(s), '.MuChilaLoja::real($mes['total']).' movimentados, <strong>'.MuChilaLoja::real($mes['taxa'])
		.'</strong> de taxa da loja. Anúncios abertos: '.$abertos.'.</p>';

	$filtros = ['' => 'Todas', 'pendente' => 'Aguardando', 'pago' => 'Pagas, aguardando entrega', 'falhou' => 'Com problema', 'entregue' => 'Entregues', 'expirado' => 'Expiradas', 'cancelado' => 'Canceladas'];
	$filtro = isset($_GET['status'], $filtros[$_GET['status']]) ? $_GET['status'] : '';
	echo '<p>';
	foreach($filtros as $k => $t) echo '<a class="btn btn-xs '.($k === $filtro ? 'btn-primary' : 'btn-default').'" href="'.admincp_base('muchila_mercado'.($k !== '' ? '&status='.$k : '')).'">'.$t.'</a> ';
	echo '</p>';

	$lista = $m->pedidos($filtro !== '' ? $filtro : null);
	if(!$lista) { message('info', 'Nenhuma compra.'); return; }
	echo '<table class="table table-condensed table-hover"><thead><tr><th>#</th><th>Criada</th><th>Comprador</th><th>Vendedor</th><th>O quê</th>'
		.'<th>Valor / taxa</th><th>Situação</th><th>Observações</th><th></th></tr></thead><tbody>';
	foreach($lista as $p) {
		echo '<tr'.($p['status'] === 'falhou' ? ' class="danger"' : '').'><td>'.(int)$p['id'].'</td><td>'.MuChilaLoja::data($p['criado']).'</td>'
			.'<td>'.$h($p['comprador']).'</td><td>'.$h($p['vendedor']).'</td><td>'.$h($p['descricao']).'</td>'
			.'<td>'.MuChilaLoja::real($p['valor']).'<br><small>taxa '.MuChilaLoja::real($p['taxa']).'</small></td>'
			.'<td>'.$h(MuChilaMercado::statusPedido($p['status'])).'</td><td><small>'.$h($p['obs']).'</small></td><td style="white-space:nowrap">';
		$f = '<form method="post" style="display:inline"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">';
		if($p['status'] === 'pago') echo $f.'<button name="acao" value="entregar" class="btn btn-xs btn-success">Entregar agora</button></form> ';
		if(in_array($p['status'], ['pendente', 'expirado'], true))
			echo $f.'<button name="acao" value="cancelar" class="btn btn-xs btn-default" onclick="return confirm(\'Cancelar a compra '.(int)$p['id'].'?\')">Cancelar</button></form>';
		echo '</td></tr>';
	}
	echo '</tbody></table>';
} catch(Exception $ex) {
	message('error', $h($ex->getMessage()));
}

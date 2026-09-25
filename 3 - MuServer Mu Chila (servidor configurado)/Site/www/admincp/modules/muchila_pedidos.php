<?php
/**
 * Mu Chila - pedidos da loja (AdminCP). Ações: entregar manualmente (pagamento conferido fora do sistema, ou pedido
 * "com problema" depois de corrigir a causa) e cancelar. Núcleo: includes/muchila/MuChilaLoja.php.
 */
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLoja.php');
$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
if(empty($_SESSION['muchila_admin_csrf'])) $_SESSION['muchila_admin_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_admin_csrf'];

echo '<h1 class="page-header">Loja: pedidos de VIP e cash</h1>';
try {
	$loja = new MuChilaLoja();
	echo '<p>Provedor: <strong>'.$h($loja->cfg['provedor']).'</strong>'.($loja->modoTeste() ? ' (modo de teste: nenhum pagamento é real)' : '')
		.' &nbsp; | &nbsp; Loja '.($loja->ativa() ? 'aberta' : '<strong>fechada</strong>').'. Pacotes e preços: <code>includes/config/muchila.pacotes.json</code>.</p>';

	if($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['acao'], $_POST['pedido'])) {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada; recarregue a página.');
		$id = (int)$_POST['pedido'];
		$quem = 'admin:' . $_SESSION['username'];
		if($_POST['acao'] === 'entregar') message('info', $h($loja->entregar($id, $quem)));
		if($_POST['acao'] === 'cancelar') message('info', $h($loja->cancelar($id, 'Cancelado por ' . $quem)));
	}

	$st = $loja->db->query("SELECT COUNT(*) AS n, ISNULL(SUM(valor), 0) AS total FROM MUCHILA_PEDIDOS
		WHERE status = 'entregue' AND provedor <> 'simulado' AND entregue_em >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)");
	$mes = $st->fetch();
	echo '<p>Recebido neste mês (sem os testes): <strong>'.MuChilaLoja::real($mes['total']).'</strong> em '.(int)$mes['n'].' pedido(s).</p>';

	$filtros = ['' => 'Todos', 'pendente' => 'Aguardando', 'falhou' => 'Com problema', 'entregue' => 'Entregues', 'expirado' => 'Expirados', 'cancelado' => 'Cancelados'];
	$filtro = isset($_GET['status'], $filtros[$_GET['status']]) ? $_GET['status'] : '';
	echo '<p>';
	foreach($filtros as $k => $t) echo '<a class="btn btn-xs '.($k === $filtro ? 'btn-primary' : 'btn-default').'" href="'.admincp_base('muchila_pedidos'.($k !== '' ? '&status='.$k : '')).'">'.$t.'</a> ';
	echo '</p>';

	$pedidos = $loja->pedidos($filtro !== '' ? $filtro : null);
	if(!$pedidos) { message('info', 'Nenhum pedido.'); return; }
	echo '<table class="table table-condensed table-hover"><thead><tr><th>#</th><th>Criado</th><th>Conta</th><th>Pacote</th><th>Valor</th>'
		.'<th>Provedor</th><th>Situação</th><th>Entrega</th><th>Observações</th><th></th></tr></thead><tbody>';
	foreach($pedidos as $p) {
		$aberto = in_array($p['status'], ['pendente', 'expirado', 'falhou'], true);
		echo '<tr'.($p['status'] === 'falhou' ? ' class="danger"' : '').'>';
		echo '<td>'.(int)$p['id'].'</td><td>'.MuChilaLoja::data($p['criado']).'</td>';
		echo '<td><a href="'.admincp_base('searchaccount').'">'.$h($p['conta']).'</a></td><td>'.$h($p['descricao']).'</td><td>'.MuChilaLoja::real($p['valor']).'</td>';
		echo '<td>'.$h($p['provedor']).($p['provedor_id'] ? '<br><small>'.$h($p['provedor_id']).'</small>' : '').'</td>';
		echo '<td>'.$h(MuChilaLoja::statusTexto($p['status'])).'</td>';
		$zen = (int)$p['zen'] > 0 && $p['status'] === 'entregue'
			? '<br><small>Zen '.number_format((int)$p['zen'], 0, ',', '.').': '.($p['zen_entregue_em'] ? 'no baú' : '<strong>aguardando sair do jogo</strong>').'</small>' : '';
		echo '<td>'.($p['entregue_em'] ? MuChilaLoja::data($p['entregue_em']).'<br><small>'.$h($p['entregue_por']).'</small>' : '').$zen.'</td>';
		echo '<td><small>'.$h($p['obs']).'</small></td><td style="white-space:nowrap">';
		if($aberto) {
			echo '<form method="post" style="display:inline" onsubmit="return confirm(\'Entregar o pedido '.(int)$p['id'].' ('.$h($p['descricao']).') para '.$h($p['conta']).'? Use só se o pagamento foi conferido.\')">'
				.'<input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">'
				.'<button name="acao" value="entregar" class="btn btn-xs btn-success">Entregar</button></form> ';
			echo '<form method="post" style="display:inline" onsubmit="return confirm(\'Cancelar o pedido '.(int)$p['id'].'?\')">'
				.'<input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">'
				.'<button name="acao" value="cancelar" class="btn btn-xs btn-default">Cancelar</button></form>';
		}
		echo '</td></tr>';
	}
	echo '</tbody></table>';
} catch(Exception $ex) {
	message('error', $h($ex->getMessage()));
}

<?php
/**
 * Mu Chila - Loja (área do jogador): comprar VIP e cash por PIX.
 * Núcleo e regras: includes/muchila/MuChilaLoja.php. Pacotes: includes/config/muchila.pacotes.json.
 */

if(!isLoggedIn()) redirect(1,'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLoja.php');

$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
$conta = $_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];

$erro = null;
try {
	$loja = new MuChilaLoja();

	// POST antes de qualquer saída: depois de comprar, redireciona para o pedido (atualizar a página não compra de novo)
	if($_SERVER['REQUEST_METHOD'] === 'POST') {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra a loja de novo.');
		if(isset($_POST['comprar'])) {
			$pedido = $loja->criarPedido($conta, (string)$_POST['comprar'], (string)($_SERVER['REMOTE_ADDR'] ?? ''));
			redirect(1, 'usercp/loja?pedido=' . (int)$pedido['id']);
		}
		if(isset($_POST['simular'], $_POST['pedido'])) {
			$_SESSION['muchila_msg'] = $loja->simular((int)$_POST['pedido'], $conta, $_POST['simular'] === 'aprovar');
			redirect(1, 'usercp/loja?pedido=' . (int)$_POST['pedido']);
		}
	}
} catch(Exception $ex) {
	$erro = $ex->getMessage();
}

echo '<div class="page-title"><span>Loja: VIP e Cash</span></div>';
if($erro) message('error', $h($erro));
if(!isset($loja)) return;
if(!$loja->ativa()) { message('warning', 'A loja está fechada no momento.'); return; }
if(!empty($_SESSION['muchila_msg'])) { message('info', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }

$teste = $loja->modoTeste();
if($teste) message('warning', '<strong>MODO DE TESTE.</strong> Nenhum pagamento é real: o PIX mostrado é falso e o pagamento é simulado pelos botões de teste.');

// ------------------------------------------------------------------ um pedido
if(isset($_GET['pedido'])) {
	$p = $loja->pedido((int)$_GET['pedido'], $conta);
	if(!$p) { message('error', 'Pedido não encontrado.'); echo '<p><a href="'.__BASE_URL__.'usercp/loja">Voltar para a loja</a></p>'; return; }

	$cores = ['pendente' => 'warning', 'entregue' => 'success', 'cancelado' => 'default', 'expirado' => 'default', 'falhou' => 'danger'];
	echo '<div class="panel panel-general"><div class="panel-body">';
	echo '<h4>Pedido #'.(int)$p['id'].': '.$h($p['descricao']).'</h4>';
	echo '<p>Valor: <strong>'.MuChilaLoja::real($p['valor']).'</strong> &nbsp; Situação: <span class="label label-'.($cores[$p['status']] ?? 'default').'">'.$h(MuChilaLoja::statusTexto($p['status'])).'</span></p>';

	if($p['status'] === 'pendente') {
		echo '<p>Pague com PIX até <strong>'.MuChilaLoja::data($p['expira']).'</strong>. A página se atualiza sozinha quando o pagamento for confirmado.</p>';
		if(!empty($p['qr_base64'])) {
			echo '<p class="text-center"><img alt="QR Code PIX" style="max-width:240px" src="data:image/png;base64,'.$h($p['qr_base64']).'"></p>';
		} else {
			echo '<div class="text-center" style="border:2px dashed #999;padding:30px;margin:10px auto;max-width:240px;color:#999">QR Code do PIX<br>'.($teste ? '(não existe no modo de teste)' : '').'</div>';
		}
		echo '<p>PIX copia e cola:</p>';
		echo '<textarea id="pix" class="form-control" rows="3" readonly>'.$h($p['pix_copia_cola']).'</textarea>';
		echo '<p style="margin-top:8px"><button type="button" class="btn btn-default" onclick="navigator.clipboard.writeText(document.getElementById(\'pix\').value);this.innerText=\'Copiado!\'">Copiar código</button></p>';

		if($teste) {
			echo '<form method="post" style="margin-top:15px"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">';
			echo '<button name="simular" value="aprovar" class="btn btn-success">Simular pagamento aprovado (teste)</button> ';
			echo '<button name="simular" value="recusar" class="btn btn-danger">Simular pagamento recusado (teste)</button>';
			echo '</form>';
		}
		echo '<script>setTimeout(function(){ location.reload(); }, 15000);</script>';
	} elseif($p['status'] === 'entregue') {
		if($p['tipo'] === 'vip') {
			$c = $loja->conta($conta);
			message('success', 'Pagamento confirmado! VIP '.(int)$c['vip_nivel'].' ativo até <strong>'.MuChilaLoja::data($c['vip_expira']).'</strong>. Se você estiver jogando, saia e entre de novo para o VIP valer.');
		} else {
			message('success', 'Pagamento confirmado! '.number_format((int)$p['cash'], 0, ',', '.').' de cash creditados. Abra a Cash Shop no jogo (se o saldo não aparecer, saia e entre de novo).');
		}
	} elseif($p['status'] === 'falhou') {
		message('error', 'Houve um problema com este pedido. A administração já foi avisada pelo painel; se o pagamento foi feito, ele será entregue.');
	}
	echo '</div></div>';
	echo '<p><a href="'.__BASE_URL__.'usercp/loja">&laquo; Voltar para a loja</a></p>';
	return;
}

// ------------------------------------------------------------------ vitrine
$c = $loja->conta($conta);
echo '<div class="panel panel-general"><div class="panel-body">';
echo '<p><strong>Sua conta:</strong> '.$h($conta).' &nbsp; | &nbsp; <strong>VIP:</strong> '
	.($c['vip_ativo'] ? 'VIP '.(int)$c['vip_nivel'].' até '.MuChilaLoja::data($c['vip_expira']) : 'nenhum')
	.' &nbsp; | &nbsp; <strong>Cash:</strong> '.number_format((int)$c['cash'], 0, ',', '.').'</p>';
echo '</div></div>';

$grupos = ['vip' => 'VIP', 'cash' => 'Cash'];
foreach($grupos as $tipo => $titulo) {
	$lista = array_filter($loja->pacotes, function($p) use ($tipo) { return $p['tipo'] === $tipo; });
	if(!$lista) continue;
	echo '<h4>'.$titulo.'</h4><div class="row">';
	foreach($lista as $p) {
		$bloqueado = $tipo === 'vip' && $c['vip_ativo'] && (int)$c['vip_nivel'] !== (int)$p['vip_nivel'];
		echo '<div class="col-xs-12 col-sm-4"><div class="panel panel-general"><div class="panel-body text-center">';
		echo '<p><strong>'.$h($p['nome']).'</strong></p><p style="font-size:18px">'.MuChilaLoja::real($p['valor']).'</p>';
		if($bloqueado) {
			echo '<p><small>Disponível quando o seu VIP '.(int)$c['vip_nivel'].' acabar.</small></p>';
		} else {
			echo '<form method="post"><input type="hidden" name="csrf" value="'.$h($csrf).'">';
			echo '<button name="comprar" value="'.$h($p['id']).'" class="btn btn-primary">'.($tipo === 'vip' && $c['vip_ativo'] ? 'Renovar' : 'Comprar').'</button></form>';
		}
		echo '</div></div></div>';
	}
	echo '</div>';
}

$pedidos = $loja->pedidosDaConta($conta);
if($pedidos) {
	echo '<h4>Meus pedidos</h4><table class="table table-condensed"><thead><tr><th>#</th><th>Data</th><th>Pacote</th><th>Valor</th><th>Situação</th><th></th></tr></thead><tbody>';
	foreach($pedidos as $p) {
		echo '<tr><td>'.(int)$p['id'].'</td><td>'.MuChilaLoja::data($p['criado']).'</td><td>'.$h($p['descricao']).'</td><td>'.MuChilaLoja::real($p['valor']).'</td>'
			.'<td>'.$h(MuChilaLoja::statusTexto($p['status'])).'</td><td><a href="'.__BASE_URL__.'usercp/loja?pedido='.(int)$p['id'].'">ver</a></td></tr>';
	}
	echo '</tbody></table>';
}

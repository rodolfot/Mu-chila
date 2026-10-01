<?php
/**
 * Mu Chila - Loja (área do jogador): comprar VIP, cash e Passe dos Mapas por PIX.
 * Núcleo e regras: includes/muchila/MuChilaLoja.php. Pacotes: includes/config/muchila.pacotes.json.
 * Visual do tema "muchila" (30/09/2026): componentes de includes/muchila/MuChilaUI.php; a lógica não mudou.
 */

if(!isLoggedIn()) redirect(1,'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLoja.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');

$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
$ui = 'MuChilaUI';
$conta = $_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];

$erro = null;
try {
	$loja = new MuChilaLoja();
	$loja->entregarZenPendente($conta);   // Zen de VIP comprado enquanto estava no jogo (a tarefa do minuto também faz)

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

echo $ui::titulo('Loja', 'VIP, Cash e Passe dos Mapas, pagos na hora por PIX.', 'coin',
	'<a class="mc-btn mc-btn--sm" href="' . __BASE_URL__ . 'usercp/lojaitens">' . $ui::icone('chest') . 'Loja de itens</a>');
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!isset($loja)) return;
if(!$loja->ativa()) { echo $ui::aviso('atencao', 'Loja fechada', 'A loja está fechada no momento.', 'lock'); return; }
if(!empty($_SESSION['muchila_msg'])) { echo $ui::aviso('info', '', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }

$teste = $loja->modoTeste();
if($teste) echo $ui::aviso('atencao', 'Modo de teste', 'Nenhum pagamento é real: o PIX mostrado é falso e o pagamento é simulado pelos botões de teste.');

$statusTom = ['pendente' => 'ouro', 'entregue' => 'verde', 'cancelado' => 'cinza', 'expirado' => 'cinza', 'falhou' => 'rubi'];

// ------------------------------------------------------------------ um pedido
if(isset($_GET['pedido'])) {
	$p = $loja->pedido((int)$_GET['pedido'], $conta);
	if(!$p) { echo $ui::aviso('perigo', 'Pedido não encontrado', '<a href="'.__BASE_URL__.'usercp/loja">Voltar para a loja</a>'); return; }

	echo '<a class="mc-back" href="'.__BASE_URL__.'usercp/loja">' . $ui::icone('arrow-left') . 'Voltar para a loja</a>';
	echo '<div class="mc-card">';
	echo '<div class="mc-card__head"><h2 class="mc-card__title">' . $ui::icone('cart') . 'Pedido #'.(int)$p['id'].': '.$h($p['descricao']).'</h2>'
		. $ui::etiqueta(MuChilaLoja::statusTexto($p['status']), $statusTom[$p['status']] ?? 'cinza') . '</div>';
	// etapas: pedido criado -> pago -> entregue
	$passo = $p['status'] === 'entregue' ? 3 : ($p['status'] === 'pendente' ? 1 : 0);
	echo '<div class="mc-steps" aria-hidden="true">';
	for($i = 1; $i <= 3; $i++) echo '<span class="'.($passo >= $i ? 'is-done' : '').'"></span>';
	echo '</div>';

	if($p['status'] === 'pendente') {
		echo '<div class="mc-pix">';
		if(!empty($p['qr_base64'])) echo '<div class="mc-pix__qr"><img alt="QR Code PIX" src="data:image/png;base64,'.$h($p['qr_base64']).'"></div>';
		else echo '<div class="mc-pix__qr mc-pix__qr--empty">' . $ui::icone('qr') . '<br>QR Code do PIX' . ($teste ? '<br><small>(não existe no modo de teste)</small>' : '') . '</div>';
		echo '<div>';
		echo '<p style="margin-top:0">Valor: <strong style="font-size:22px;color:var(--gold-2)">'.MuChilaLoja::real($p['valor']).'</strong></p>';
		echo '<p>Pague com PIX até <strong>'.MuChilaLoja::data($p['expira']).'</strong>. A página se atualiza sozinha quando o pagamento for confirmado.</p>';
		echo '<label for="pix">PIX copia e cola</label><textarea id="pix" class="form-control" rows="3" readonly>'.$h($p['pix_copia_cola']).'</textarea>';
		echo '<p style="margin-top:10px"><button type="button" class="mc-btn" data-mc-copy="#pix">' . $ui::icone('copy') . 'Copiar código</button></p>';
		if($teste) {
			echo '<form method="post" style="margin-top:15px;display:flex;flex-wrap:wrap;gap:8px"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">';
			echo '<button name="simular" value="aprovar" class="btn btn-success">Simular pagamento aprovado (teste)</button>';
			echo '<button name="simular" value="recusar" class="btn btn-danger">Simular pagamento recusado (teste)</button>';
			echo '</form>';
		}
		echo '</div></div>';
		echo '<script>setTimeout(function(){ location.reload(); }, 15000);</script>';
	} elseif($p['status'] === 'entregue') {
		if($p['tipo'] === 'vip') {
			$c = $loja->conta($conta);
			$zen = (int)$p['zen'];
			$zenTexto = $zen <= 0 ? '' : ($p['zen_entregue_em']
				? ' '.number_format($zen, 0, ',', '.').' de Zen foram colocados no seu baú.'
				: ' O bônus de '.number_format($zen, 0, ',', '.').' de Zen vai para o seu baú assim que você sair do jogo (até 1 minuto depois).');
			echo $ui::aviso('sucesso', 'Pagamento confirmado!', 'VIP '.(int)$c['vip_nivel'].' ativo até <strong>'.MuChilaLoja::data($c['vip_expira']).'</strong>.'.$zenTexto.' Se você estiver jogando, saia e entre de novo para o VIP valer.', 'crown');
		} elseif($p['tipo'] === 'passe') {
			$c = $loja->conta($conta);
			echo $ui::aviso('sucesso', 'Pagamento confirmado!', 'Passe dos Mapas ativo até <strong>'.MuChilaLoja::data($c['passe_expira']).'</strong>. Já vale, não precisa sair do jogo.', 'key');
		} else {
			echo $ui::aviso('sucesso', 'Pagamento confirmado!', number_format((int)$p['cash'], 0, ',', '.').' de cash creditados. Use na <a href="'.__BASE_URL__.'usercp/lojaitens">loja de itens</a> do site ou na Cash Shop do jogo (se o saldo não aparecer no jogo, saia e entre de novo).', 'coin');
		}
	} elseif($p['status'] === 'falhou') {
		echo $ui::aviso('perigo', 'Problema no pedido', 'Houve um problema com este pedido. A administração já foi avisada pelo painel; se o pagamento foi feito, ele será entregue.');
	} else {
		echo '<p>Valor: <strong>'.MuChilaLoja::real($p['valor']).'</strong>.</p>';
	}
	echo '</div>';
	return;
}

// ------------------------------------------------------------------ vitrine
$c = $loja->conta($conta);
echo '<div class="mc-grid mc-grid--3" style="margin-bottom:8px">';
echo $ui::carteira((int)$c['cash']);
echo $ui::estatistica('VIP', $c['vip_ativo'] ? 'VIP '.(int)$c['vip_nivel'] : 'Nenhum', 'crown', $c['vip_ativo'] ? 'ouro' : 'arcano', $c['vip_ativo'] ? 'até '.MuChilaLoja::data($c['vip_expira']) : 'Free');
echo $ui::estatistica('Passe dos Mapas', $c['passe_ativo'] ? 'Ativo' : 'Nenhum', 'key', $c['passe_ativo'] ? 'verde' : 'rubi', $c['passe_ativo'] ? 'até '.MuChilaLoja::data($c['passe_expira']) : 'mapas acima do nível 400');
echo '</div>';

$grupos = ['vip' => ['VIP', 'crown', 'Mais EXP, drop e vantagens no jogo'], 'cash' => ['Cash', 'coin', 'Para a loja de itens do site e a Cash Shop do jogo'],
           'passe' => ['Passe dos Mapas', 'key', 'Acesso aos mapas acima do nível 400']];
foreach($grupos as $tipo => [$titulo, $icone, $sub]) {
	$lista = array_filter($loja->pacotes, function($p) use ($tipo) { return $p['tipo'] === $tipo; });
	if(!$lista) continue;
	echo '<h2 class="mc-section-title" id="'.$tipo.'">' . $ui::icone($icone) . $titulo . ' <small>' . $sub . '</small></h2>';
	if($tipo === 'passe') {
		$mapasPasse = [];
		try { $mapasPasse = $loja->db->query("SELECT Nome FROM MuChila_PasseMapasLista ORDER BY Mapa")->fetchAll(PDO::FETCH_COLUMN); } catch(Exception $ex) {}
		echo '<p>Sem o passe, quem entra nesses mapas volta para a seleção de personagem e depois aparece em Lorencia'
			.($mapasPasse ? ': <strong>'.$h(implode(', ', $mapasPasse)).'</strong>' : '').'. Vale para a conta toda (todos os personagens e servidores). '
			.'Comprar de novo soma os dias. No jogo, o mesmo passe sai pelo <em>Gold Channel Ticket</em> da Cash Shop (use o ticket guardado na loja).</p>';
	}
	$valores = array_column($lista, 'valor');
	$maior = $valores ? max($valores) : 0;
	echo '<div class="mc-packs">';
	foreach($lista as $p) {
		$bloqueado = $tipo === 'vip' && $c['vip_ativo'] && (int)$c['vip_nivel'] !== (int)$p['vip_nivel'];
		$destaque = (float)$p['valor'] === (float)$maior && count($lista) > 1;
		echo '<div class="mc-pack'.($destaque ? ' mc-pack--featured' : '').'">';
		if($destaque) echo '<span class="mc-pack__ribbon">' . $ui::etiqueta($tipo === 'cash' ? 'Mais Cash' : ($tipo === 'vip' ? 'O melhor' : 'Mais dias'), 'ouro') . '</span>';
		echo '<span class="mc-pack__icon">' . $ui::icone($icone) . '</span>';
		echo '<span class="mc-pack__name">'.$h($p['nome']).'</span>';
		echo '<span class="mc-pack__price">'.MuChilaLoja::real($p['valor']).'</span>';
		$nota = '';
		if($tipo === 'cash' && (int)$p['cash'] > 0) $nota = 'R$ '.number_format((float)$p['valor'] / (int)$p['cash'] * 100, 2, ',', '.').' a cada 100 Cash';
		if(!empty($p['zen'])) $nota = '+ '.number_format((int)$p['zen'], 0, ',', '.').' de Zen no baú';
		echo '<span class="mc-pack__note">'.$nota.'</span>';
		if($bloqueado) {
			echo '<p class="small" style="margin:12px 0 0">Disponível quando o seu VIP '.(int)$c['vip_nivel'].' acabar.</p>';
		} else {
			$renovar = $tipo === 'vip' && $c['vip_ativo'] || $tipo === 'passe' && $c['passe_ativo'];
			echo '<form method="post"><input type="hidden" name="csrf" value="'.$h($csrf).'">';
			echo '<button name="comprar" value="'.$h($p['id']).'" class="mc-btn '.($destaque ? 'mc-btn--gold mc-btn--shine' : '').' mc-btn--block">'
				. $ui::icone('qr') . ($renovar ? 'Renovar' : 'Comprar com PIX').'</button></form>';
		}
		echo '</div>';
	}
	echo '</div>';
}

$pedidos = $loja->pedidosDaConta($conta);
if($pedidos) {
	echo '<h2 class="mc-section-title">' . $ui::icone('history') . 'Meus pedidos</h2>';
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>#</th><th>Data</th><th>Pacote</th><th>Valor</th><th>Situação</th><th></th></tr></thead><tbody>';
	foreach($pedidos as $p) {
		echo '<tr><td>'.(int)$p['id'].'</td><td>'.MuChilaLoja::data($p['criado']).'</td><td>'.$h($p['descricao']).'</td><td>'.MuChilaLoja::real($p['valor']).'</td>'
			.'<td>'.$ui::etiqueta(MuChilaLoja::statusTexto($p['status']), $statusTom[$p['status']] ?? 'cinza').'</td><td><a href="'.__BASE_URL__.'usercp/loja?pedido='.(int)$p['id'].'">ver</a></td></tr>';
	}
	echo '</tbody></table></div>';
}

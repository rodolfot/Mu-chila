<?php
/**
 * Mu Chila - Mercado entre jogadores (área do jogador): comprar e vender itens do baú e personagens por PIX.
 * Núcleo e regras: includes/muchila/MuChilaMercado.php. Config: includes/config/modules/usercp.mercado.xml.
 */
if(!isLoggedIn()) redirect(1,'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMercado.php');

$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
$conta = $_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];
$abas = ['comprar' => 'Comprar', 'vender' => 'Vender', 'meus' => 'Meus anúncios', 'compras' => 'Minhas compras', 'conta' => 'Minha conta de vendedor'];
$aba = isset($_GET['aba'], $abas[$_GET['aba']]) ? $_GET['aba'] : 'comprar';
$url = function(array $q = []) { return __BASE_URL__ . 'usercp/mercado' . ($q ? '?' . http_build_query($q) : ''); };

$erro = null;
try {
	$m = new MuChilaMercado();
	$m->entregarPendentes($conta);   // o que ficou pago enquanto a conta estava no jogo

	if($_SERVER['REQUEST_METHOD'] === 'POST') {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra o mercado de novo.');
		$ip = (string)($_SERVER['REMOTE_ADDR'] ?? '');
		if(isset($_POST['comprar'])) {
			$p = $m->comprar($conta, (int)$_POST['comprar'], $ip);
			redirect(1, 'usercp/mercado?pedido=' . (int)$p['id']);
		}
		if(isset($_POST['simular'], $_POST['pedido'])) {
			$_SESSION['muchila_msg'] = $m->simular((int)$_POST['pedido'], $conta, $_POST['simular'] === 'aprovar');
			redirect(1, 'usercp/mercado?pedido=' . (int)$_POST['pedido']);
		}
		if(isset($_POST['anunciar_item'])) {
			$id = $m->anunciarItem($conta, (int)$_POST['posicao'], (string)$_POST['hex'], (string)$_POST['valor']);
			$_SESSION['muchila_msg'] = "Item anunciado (anúncio #$id). Ele saiu do seu baú e fica guardado pela loja até vender ou você cancelar.";
			redirect(1, 'usercp/mercado?aba=meus');
		}
		if(isset($_POST['anunciar_personagem'])) {
			$id = $m->anunciarPersonagem($conta, (string)$_POST['nome'], (string)$_POST['valor']);
			$_SESSION['muchila_msg'] = "Personagem anunciado (anúncio #$id). Ele saiu da sua conta e fica guardado pela loja até vender ou você cancelar.";
			redirect(1, 'usercp/mercado?aba=meus');
		}
		if(isset($_POST['cancelar'])) {
			$_SESSION['muchila_msg'] = $m->cancelarAnuncio($conta, (int)$_POST['cancelar']);
			redirect(1, 'usercp/mercado?aba=meus');
		}
		if(isset($_POST['conectar'])) {
			if($m->modoTeste()) { $m->conectarSimulado($conta); $_SESSION['muchila_msg'] = 'Conta de vendedor ligada (modo de teste).'; redirect(1, 'usercp/mercado?aba=conta'); }
			header('Location: ' . $m->urlAutorizacao($conta)); exit;
		}
		if(isset($_POST['desligar'])) {
			$m->desligarVendedor($conta);
			$_SESSION['muchila_msg'] = 'Conta do Mercado Pago desligada.';
			redirect(1, 'usercp/mercado?aba=conta');
		}
	}
} catch(Exception $ex) {
	$erro = $ex->getMessage();
}

require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
echo MuChilaUI::titulo('Mercado entre jogadores', 'Compre e venda itens do baú e personagens com outros jogadores, por PIX.', 'market');
if($erro) message('error', $h($erro));
if(!isset($m)) return;
if(!$m->ativa()) { message('warning', 'O mercado está fechado no momento.'); return; }
if(!empty($_SESSION['muchila_msg'])) { message('info', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }
if($m->modoTeste()) message('warning', '<strong>MODO DE TESTE.</strong> Nenhum pagamento é real: o PIX é falso e o pagamento é simulado pelos botões de teste.');

// ------------------------------------------------------------------ uma compra (PIX)
if(isset($_GET['pedido'])) {
	$p = $m->pedido((int)$_GET['pedido'], $conta);
	if(!$p) { message('error', 'Compra não encontrada.'); echo '<p><a href="'.$url(['aba' => 'compras']).'">Voltar</a></p>'; return; }
	echo '<div class="panel panel-general"><div class="panel-body">';
	echo '<h4>Compra #'.(int)$p['id'].': '.$h($p['descricao']).'</h4>';
	echo '<p>Valor: <strong>'.MuChilaLoja::real($p['valor']).'</strong> (vendedor: '.$h($p['vendedor']).') &nbsp; Situação: <strong>'.$h(MuChilaMercado::statusPedido($p['status'])).'</strong></p>';
	if($p['status'] === 'pendente') {
		echo '<p>Pague com PIX até <strong>'.MuChilaLoja::data($p['expira']).'</strong>. Enquanto isso o anúncio fica reservado para você. A página se atualiza sozinha.</p>';
		if(!empty($p['qr_base64'])) echo '<p class="text-center"><img alt="QR Code PIX" style="max-width:240px" src="data:image/png;base64,'.$h($p['qr_base64']).'"></p>';
		echo '<p>PIX copia e cola:</p><textarea id="pix" class="form-control" rows="3" readonly>'.$h($p['pix_copia_cola']).'</textarea>';
		echo '<p style="margin-top:8px"><button type="button" class="btn btn-default" onclick="navigator.clipboard.writeText(document.getElementById(\'pix\').value);this.innerText=\'Copiado!\'">Copiar código</button></p>';
		if($m->modoTeste()) {
			echo '<form method="post" style="margin-top:15px"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">'
				.'<button name="simular" value="aprovar" class="btn btn-success">Simular pagamento aprovado (teste)</button> '
				.'<button name="simular" value="recusar" class="btn btn-danger">Simular pagamento recusado (teste)</button></form>';
		}
		echo '<script>setTimeout(function(){ location.reload(); }, 15000);</script>';
	} elseif($p['status'] === 'pago') {
		message('info', 'Pagamento confirmado! A entrega acontece assim que você estiver <strong>fora do jogo</strong> (e, para item, com espaço no baú; para personagem, com vaga livre na conta). Saia do jogo e espere até 1 minuto.');
	} elseif($p['status'] === 'entregue') {
		message('success', 'Entregue! Itens vão para o seu <strong>baú</strong>; personagens aparecem na sua lista de personagens.');
	}
	echo '</div></div><p><a href="'.$url(['aba' => 'compras']).'">&laquo; Minhas compras</a></p>';
	return;
}

// ------------------------------------------------------------------ abas
$iconesAba = ['comprar' => 'cart', 'vender' => 'coin', 'meus' => 'scroll', 'compras' => 'history', 'conta' => 'user'];
echo '<nav class="mc-tabs" aria-label="Seções do mercado">';
foreach($abas as $k => $t) echo '<a class="'.($k === $aba ? 'is-active' : '').'" href="'.$url(['aba' => $k]).'"'.($k === $aba ? ' aria-current="page"' : '').'>'.MuChilaUI::icone($iconesAba[$k]).$t.'</a>';
echo '</nav>';
$form = function($extra) use ($h, $csrf) { return '<form method="post" style="display:inline"><input type="hidden" name="csrf" value="'.$h($csrf).'">'.$extra; };
$taxa = rtrim(rtrim(number_format($m->taxaPct(), 2, ',', ''), '0'), ',');

if($aba === 'comprar') {
	$tipo = (string)($_GET['tipo'] ?? '');
	$busca = trim((string)($_GET['busca'] ?? ''));
	echo '<form method="get" class="mc-toolbar" action="'.$url().'">'
		.'<select name="tipo" class="form-control"><option value="">Tudo</option><option value="item"'.($tipo === 'item' ? ' selected' : '').'>Itens</option>'
		.'<option value="personagem"'.($tipo === 'personagem' ? ' selected' : '').'>Personagens</option></select> '
		.'<input name="busca" class="form-control" placeholder="Buscar..." value="'.$h($busca).'"> <button class="btn btn-default">Filtrar</button></form>';
	$lista = $m->anunciosAtivos($tipo, $busca);
	if(!$lista) { message('info', 'Nenhum anúncio à venda no momento.'); return; }
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>Tipo</th><th>Anúncio</th><th>Vendedor</th><th>Preço</th><th></th></tr></thead><tbody>';
	foreach($lista as $a) {
		echo '<tr><td>'.($a['tipo'] === 'item' ? 'Item' : 'Personagem').'</td><td><strong>'.$h($a['titulo']).'</strong><br><small>'.$h($a['detalhes']).'</small></td>'
			.'<td>'.$h($a['vendedor']).'</td><td>'.MuChilaLoja::real($a['valor']).'</td><td>';
		if(strcasecmp($a['vendedor'], $conta) !== 0)
			echo $form('<button name="comprar" value="'.(int)$a['id'].'" class="btn btn-xs btn-primary" onclick="return confirm(\'Comprar '.$h($a['titulo']).' por '.MuChilaLoja::real($a['valor']).'? Vai gerar um PIX.\')">Comprar</button></form>');
		else echo '<small>seu anúncio</small>';
		echo '</td></tr>';
	}
	echo '</tbody></table></div>';
	echo '<p><small>Itens chegam no seu <strong>baú</strong> e personagens na sua lista, com você <strong>fora do jogo</strong>. O pagamento vai direto para o vendedor pelo Mercado Pago.</small></p>';
}

if($aba === 'vender') {
	if(!$m->vendedorLigado($conta)) { message('warning', 'Para vender, primeiro ligue a sua conta do Mercado Pago na aba <a href="'.$url(['aba' => 'conta']).'">Minha conta de vendedor</a>. É para lá que o dinheiro vai.'); return; }
	message('info', "Para anunciar, esteja <strong>fora do jogo</strong>. O item sai do seu baú (ou o personagem sai da sua conta) na hora e fica guardado pela loja até vender ou você cancelar. A loja fica com <strong>$taxa%</strong> de cada venda; o resto cai direto na sua conta do Mercado Pago.");
	$minimo = MuChilaLoja::real($m->cfg['valor_minimo']);
	echo '<h4>Itens do seu baú</h4>';
	$itens = $m->itensDoBau($conta);
	if(!$itens) echo '<p>Seu baú está vazio (coloque o item no baú dentro do jogo e volte aqui).</p>';
	else {
		echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>Item</th><th>Preço (R$)</th><th></th></tr></thead><tbody>';
		foreach($itens as $it) {
			echo '<tr><td>'.$h($it['descricao']).'</td><td colspan="2">';
			if($it['bloqueio']) echo '<small class="text-muted">'.$h($it['bloqueio']).'</small>';
			else echo $form('<input type="hidden" name="posicao" value="'.(int)$it['posicao'].'"><input type="hidden" name="hex" value="'.$h($it['hex']).'">'
				.'<input name="valor" class="form-control input-sm" style="width:110px;display:inline" placeholder="mín. '.$h($minimo).'" required> '
				.'<button name="anunciar_item" value="1" class="btn btn-xs btn-primary" onclick="return confirm(\'Anunciar '.$h($it['nome']).'? Ele sai do seu baú agora.\')">Anunciar</button></form>');
			echo '</td></tr>';
		}
		echo '</tbody></table></div>';
	}
	echo '<h4>Seus personagens</h4>';
	$chars = $m->personagensDaConta($conta);
	if(!$chars) echo '<p>Nenhum personagem.</p>';
	else {
		echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>Personagem</th><th>Preço (R$)</th></tr></thead><tbody>';
		foreach($chars as $c) {
			echo '<tr><td><strong>'.$h($c['Name']).'</strong><br><small>'.$h($c['descricao']).'</small></td><td>';
			if($c['bloqueio']) echo '<small class="text-muted">'.$h($c['bloqueio']).'</small>';
			else echo $form('<input type="hidden" name="nome" value="'.$h($c['Name']).'">'
				.'<input name="valor" class="form-control input-sm" style="width:110px;display:inline" placeholder="mín. '.$h($minimo).'" required> '
				.'<button name="anunciar_personagem" value="1" class="btn btn-xs btn-primary" onclick="return confirm(\'Anunciar o personagem '.$h($c['Name']).'? Ele sai da sua conta agora (com o inventário dele).\')">Anunciar</button></form>');
			echo '</td></tr>';
		}
		echo '</tbody></table></div>';
	}
}

if($aba === 'meus') {
	$lista = $m->anunciosDoVendedor($conta);
	if(!$lista) { message('info', 'Você ainda não anunciou nada.'); return; }
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>#</th><th>Anúncio</th><th>Preço</th><th>Situação</th><th></th></tr></thead><tbody>';
	foreach($lista as $a) {
		echo '<tr><td>'.(int)$a['id'].'</td><td><strong>'.$h($a['titulo']).'</strong><br><small>'.$h($a['detalhes']).'</small></td><td>'.MuChilaLoja::real($a['valor']).'</td>'
			.'<td>'.$h(MuChilaMercado::statusAnuncio($a['status'])).($a['comprador'] ? '<br><small>para '.$h($a['comprador']).'</small>' : '').'</td><td>';
		if($a['status'] === 'ativo')
			echo $form('<button name="cancelar" value="'.(int)$a['id'].'" class="btn btn-xs btn-default" onclick="return confirm(\'Cancelar o anúncio? O '.($a['tipo'] === 'item' ? 'item volta para o seu baú' : 'personagem volta para a sua conta').' (esteja fora do jogo).\')">Cancelar</button></form>');
		echo '</td></tr>';
	}
	echo '</tbody></table></div>';
}

if($aba === 'compras') {
	$lista = $m->pedidosDoComprador($conta);
	if(!$lista) { message('info', 'Você ainda não comprou nada.'); return; }
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>#</th><th>Data</th><th>Compra</th><th>Valor</th><th>Situação</th><th></th></tr></thead><tbody>';
	foreach($lista as $p)
		echo '<tr><td>'.(int)$p['id'].'</td><td>'.MuChilaLoja::data($p['criado']).'</td><td>'.$h($p['descricao']).'</td><td>'.MuChilaLoja::real($p['valor']).'</td>'
			.'<td>'.$h(MuChilaMercado::statusPedido($p['status'])).'</td><td><a href="'.$url(['pedido' => (int)$p['id']]).'">ver</a></td></tr>';
	echo '</tbody></table></div>';
}

if($aba === 'conta') {
	$v = $m->vendedor($conta);
	echo '<div class="panel panel-general"><div class="panel-body">';
	if($m->vendedorLigado($conta)) {
		echo '<p>Sua conta de vendedor está <strong>ligada</strong>'.((int)($v['simulado'] ?? 0) === 1 ? ' (modo de teste)' : ' ao Mercado Pago (usuário '.$h($v['mp_user_id']).')')
			.' desde '.MuChilaLoja::data($v['conectado_em']).'.</p>';
		echo "<p>Quando alguém compra um anúncio seu, o PIX é gerado em nome da <strong>sua</strong> conta do Mercado Pago: você recebe o valor menos <strong>$taxa%</strong> da loja, direto no Mercado Pago.</p>";
		echo $form('<button name="desligar" value="1" class="btn btn-default" onclick="return confirm(\'Desligar a conta do Mercado Pago? Você não poderá vender até ligar de novo.\')">Desligar</button></form>');
	} else {
		echo "<p>Para vender, ligue a sua conta do <strong>Mercado Pago</strong>. Você autoriza a loja do Mu Chila a gerar cobranças em seu nome; o dinheiro das vendas cai direto na sua conta, e a loja fica com <strong>$taxa%</strong>.</p>";
		echo '<p><small>A loja nunca vê a sua senha: a autorização é feita no próprio site do Mercado Pago e pode ser desligada quando quiser.</small></p>';
		echo $form('<button name="conectar" value="1" class="btn btn-primary">'.($m->modoTeste() ? 'Ligar (modo de teste)' : 'Ligar minha conta do Mercado Pago').'</button></form>');
	}
	echo '</div></div>';
}

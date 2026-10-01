<?php
/**
 * Mu Chila - Mercado entre jogadores (área do jogador): comprar e vender itens do baú e personagens por PIX.
 * Núcleo e regras: includes/muchila/MuChilaMercado.php. Config: includes/config/modules/usercp.mercado.xml.
 * Visual (01/10/2026): anúncios em cartões com foto e quantidade (Jewel of Soul x50), páginas de 20, venda separada em
 * "Vender itens" e "Vender personagem", preço numa janela só (com o valor líquido depois da taxa). A lógica não mudou.
 */
if(!isLoggedIn()) redirect(1,'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMercado.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');

$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
$ui = 'MuChilaUI';
$conta = $_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];
$abas = ['comprar' => ['Comprar', 'cart'], 'itens' => ['Vender itens', 'chest'], 'personagens' => ['Vender personagem', 'user'],
         'meus' => ['Meus anúncios', 'scroll'], 'compras' => ['Minhas compras', 'history'], 'conta' => ['Conta de vendedor', 'key']];
$aba = (string)($_GET['aba'] ?? 'comprar');
if($aba === 'vender') $aba = 'itens';   // endereço antigo
if(!isset($abas[$aba])) $aba = 'comprar';
$url = function(array $q = []) { return __BASE_URL__ . 'usercp/mercado' . ($q ? '?' . http_build_query($q) : ''); };
$porPagina = 20;

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
} catch(Throwable $ex) {
	$erro = MuChilaUI::erro($ex);   // issue #38: erro do banco nunca aparece na tela
}

echo $ui::titulo('Mercado entre jogadores', 'Compre e venda itens do baú e personagens com outros jogadores, por PIX.', 'market');
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!isset($m)) return;
if(!$m->ativa()) { echo $ui::aviso('atencao', 'Mercado fechado', 'O mercado está fechado no momento.', 'lock'); return; }
if(!empty($_SESSION['muchila_msg'])) { echo $ui::aviso('info', '', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }
if($m->modoTeste()) echo $ui::aviso('atencao', 'Modo de teste', 'Nenhum pagamento é real: o PIX é falso e o pagamento é simulado pelos botões de teste.');

$statusTom = ['pendente' => 'ouro', 'pago' => 'arcano', 'entregue' => 'verde', 'cancelado' => 'cinza', 'expirado' => 'cinza', 'falhou' => 'rubi',
              'ativo' => 'verde', 'reservado' => 'ouro', 'vendido' => 'arcano'];
$imagem = function(int $indice) { return __BASE_URL__ . 'api/muchila-item-imagem.php?s=' . intdiv($indice, 512) . '&t=' . ($indice % 512); };
$foto = function(?string $src, string $icone) use ($ui) {
	return '<span class="mc-item__frame">' . ($src ? '<img src="' . $src . '" alt="" loading="lazy" onerror="this.hidden=true;this.nextElementSibling.hidden=false"><span hidden>' . $ui::icone($icone) . '</span>' : $ui::icone($icone)) . '</span>';
};
/** Item do anúncio decodificado (foto, quantidade e descrição completa); null se não der. */
$itemDoAnuncio = function(array $a) use ($m) {
	if(($a['tipo'] ?? '') !== 'item' || empty($a['item_hex'])) return null;
	$it = $m->itens->decodificar(hex2bin(str_pad((string)$a['item_hex'], 32, '0')));
	return $it ?: null;
};
$paginacao = function(int $total, int $pagina, array $q) use ($url, $porPagina, $ui) {
	$n = (int)ceil($total / $porPagina);
	if($n <= 1) return '';
	$s = '<nav class="mc-pages" aria-label="Páginas">';
	if($pagina > 1) $s .= '<a class="mc-btn mc-btn--sm" href="' . $url($q + ['p' => $pagina - 1]) . '">' . $ui::icone('arrow-left') . 'Anterior</a>';
	$s .= '<span>Página ' . $pagina . ' de ' . $n . '</span>';
	if($pagina < $n) $s .= '<a class="mc-btn mc-btn--sm" href="' . $url($q + ['p' => $pagina + 1]) . '">Próxima' . $ui::icone('arrow-right') . '</a>';
	return $s . '</nav>';
};
$pagina = max(1, (int)($_GET['p'] ?? 1));

// ------------------------------------------------------------------ uma compra (PIX)
if(isset($_GET['pedido'])) {
	$p = $m->pedido((int)$_GET['pedido'], $conta);
	if(!$p) { echo $ui::aviso('perigo', 'Compra não encontrada', '<a href="'.$url(['aba' => 'compras']).'">Voltar para as suas compras</a>'); return; }
	echo '<a class="mc-back" href="'.$url(['aba' => 'compras']).'">' . $ui::icone('arrow-left') . 'Minhas compras</a>';
	echo '<div class="mc-card"><div class="mc-card__head"><h2 class="mc-card__title">' . $ui::icone('cart') . 'Compra #'.(int)$p['id'].': '.$h($p['descricao']).'</h2>'
		. $ui::etiqueta(MuChilaMercado::statusPedido($p['status']), $statusTom[$p['status']] ?? 'cinza') . '</div>';
	echo '<p style="margin-top:0">Valor: <strong style="font-size:20px;color:var(--gold-2)">'.MuChilaLoja::real($p['valor']).'</strong> · vendedor <strong>'.$h($p['vendedor']).'</strong></p>';
	if($p['status'] === 'pendente') {
		echo '<div class="mc-pix">';
		if(!empty($p['qr_base64'])) echo '<div class="mc-pix__qr"><img alt="QR Code PIX" src="data:image/png;base64,'.$h($p['qr_base64']).'"></div>';
		else echo '<div class="mc-pix__qr mc-pix__qr--empty">' . $ui::icone('qr') . '<br>QR Code do PIX</div>';
		echo '<div><p>Pague com PIX até <strong>'.MuChilaLoja::data($p['expira']).'</strong>. Enquanto isso o anúncio fica reservado para você. A página se atualiza sozinha.</p>';
		echo '<label for="pix">PIX copia e cola</label><textarea id="pix" class="form-control" rows="3" readonly>'.$h($p['pix_copia_cola']).'</textarea>';
		echo '<p style="margin-top:10px"><button type="button" class="mc-btn" data-mc-copy="#pix">' . $ui::icone('copy') . 'Copiar código</button></p>';
		if($m->modoTeste()) {
			echo '<form method="post" style="margin-top:15px;display:flex;flex-wrap:wrap;gap:8px"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="pedido" value="'.(int)$p['id'].'">'
				.'<button name="simular" value="aprovar" class="btn btn-success">Simular pagamento aprovado (teste)</button>'
				.'<button name="simular" value="recusar" class="btn btn-danger">Simular pagamento recusado (teste)</button></form>';
		}
		echo '</div></div><script>setTimeout(function(){ location.reload(); }, 15000);</script>';
	} elseif($p['status'] === 'pago') {
		echo $ui::aviso('info', 'Pagamento confirmado!', 'A entrega acontece assim que você estiver <strong>fora do jogo</strong> (e, para item, com espaço no baú; para personagem, com vaga livre na conta). Saia do jogo e espere até 1 minuto.');
	} elseif($p['status'] === 'entregue') {
		echo $ui::aviso('sucesso', 'Entregue!', 'Itens vão para o seu <strong>baú</strong>; personagens aparecem na sua lista de personagens.', 'gift');
	}
	echo '</div>';
	return;
}

// ------------------------------------------------------------------ abas
echo '<nav class="mc-tabs mc-tabs--grid" aria-label="Seções do mercado">';
foreach($abas as $k => [$t, $ic]) echo '<a class="'.($k === $aba ? 'is-active' : '').'" href="'.$url(['aba' => $k]).'"'.($k === $aba ? ' aria-current="page"' : '').'>'.$ui::icone($ic).$t.'</a>';
echo '</nav>';
$form = function($extra) use ($h, $csrf) { return '<form method="post"><input type="hidden" name="csrf" value="'.$h($csrf).'">'.$extra; };
$taxa = rtrim(rtrim(number_format($m->taxaPct(), 2, ',', ''), '0'), ',');
$minimo = (float)$m->cfg['valor_minimo'];
$maximo = (float)$m->cfg['valor_maximo'];

if($aba === 'comprar') {
	$tipo = (string)($_GET['tipo'] ?? '');
	$busca = trim((string)($_GET['busca'] ?? ''));
	echo '<form method="get" class="mc-toolbar" action="'.$url().'"><input type="hidden" name="aba" value="comprar">'
		.'<label class="mc-field"><span class="mc-sr">Buscar</span>'.$ui::icone('search').'<input name="busca" class="mc-input" placeholder="Buscar item ou personagem..." value="'.$h($busca).'"></label>'
		.'<select name="tipo" class="mc-input" aria-label="Tipo"><option value="">Itens e personagens</option><option value="item"'.($tipo === 'item' ? ' selected' : '').'>Só itens</option>'
		.'<option value="personagem"'.($tipo === 'personagem' ? ' selected' : '').'>Só personagens</option></select>'
		.'<button class="mc-btn">'.$ui::icone('filter').'Filtrar</button></form>';
	$lista = $m->anunciosAtivos($tipo, $busca);
	if(!$lista) { echo $ui::vazio($busca !== '' || $tipo !== '' ? 'Nenhum anúncio com esse filtro.' : 'Nenhum anúncio à venda no momento.', 'market'); return; }
	$total = count($lista);
	echo '<p class="mc-count">'.$total.' anúncio'.($total === 1 ? '' : 's').' à venda</p><div class="mc-items mc-items--offers">';
	foreach(array_slice($lista, ($pagina - 1) * $porPagina, $porPagina) as $a) {
		$it = $itemDoAnuncio($a);
		$titulo = $it ? $it['nome'] : (string)$a['titulo'];
		$detalhe = $it ? MuChilaItens::descrever($it) : (string)$a['detalhes'];
		echo '<article class="mc-item mc-offer">';
		if($it && $it['quantidade'] > 1) echo '<span class="mc-item__ribbon">'.$ui::etiqueta('x'.$it['quantidade'], 'ouro').'</span>';
		echo $foto($a['tipo'] === 'item' ? $imagem((int)$a['item_index']) : null, $a['tipo'] === 'item' ? 'item' : 'user');
		echo '<span class="mc-item__name">'.$h($titulo).'</span><span class="mc-offer__detail">'.$h($detalhe).'</span>';
		echo '<span class="mc-item__meta">'.($a['tipo'] === 'item' ? '' : $ui::etiqueta('Personagem', 'arcano')).$ui::etiqueta('por '.$a['vendedor']).'</span>';
		echo '<span class="mc-item__price"><span>preço</span><strong>'.MuChilaLoja::real($a['valor']).'</strong></span>';
		if(strcasecmp($a['vendedor'], $conta) !== 0)
			echo $form('<button name="comprar" value="'.(int)$a['id'].'" class="mc-btn mc-btn--gold mc-btn--sm mc-btn--block"'.$ui::confirma('Comprar '.$titulo.($it && $it['quantidade'] > 1 ? ' x'.$it['quantidade'] : '').' por '.MuChilaLoja::real($a['valor']).'? Vai gerar um PIX.').'>'.$ui::icone('qr').'Comprar</button></form>');
		else echo '<span class="mc-offer__mine">seu anúncio</span>';
		echo '</article>';
	}
	echo '</div>'.$paginacao($total, $pagina, ['aba' => 'comprar', 'tipo' => $tipo, 'busca' => $busca]);
	echo '<p class="small" style="margin-top:16px">Itens chegam no seu <strong>baú</strong> e personagens na sua lista, com você <strong>fora do jogo</strong>. O pagamento vai direto para o vendedor pelo Mercado Pago.</p>';
}

if($aba === 'itens' || $aba === 'personagens') {
	if(!$m->vendedorLigado($conta)) { echo $ui::aviso('atencao', 'Ligue a sua conta de vendedor', 'Para vender, primeiro ligue a sua conta do Mercado Pago na aba <a href="'.$url(['aba' => 'conta']).'">Conta de vendedor</a>. É para lá que o dinheiro vai.', 'key'); return; }
	echo $ui::aviso('info', 'Antes de anunciar', 'Esteja <strong>fora do jogo</strong>. '.($aba === 'itens' ? 'O item sai do seu baú' : 'O personagem sai da sua conta')
		.' na hora e fica guardado pela loja até vender ou você cancelar. A loja fica com <strong>'.$taxa.'%</strong> de cada venda; o resto cai direto na sua conta do Mercado Pago.');
}

if($aba === 'itens') {
	$itens = $m->itensDoBau($conta);
	if(!$itens) { echo $ui::vazio('Seu baú está vazio. Coloque o item no baú dentro do jogo e volte aqui.', 'chest'); }
	else {
		$total = count($itens);
		echo '<div class="mc-toolbar"><label class="mc-field"><span class="mc-sr">Buscar no baú</span>'.$ui::icone('search').'<input class="mc-input" placeholder="Buscar no seu baú..." data-mc-filtro="#mcBau"></label></div>';
		echo '<p class="mc-count">'.$total.' ite'.($total === 1 ? 'm' : 'ns').' no baú</p><div class="mc-items mc-items--offers" id="mcBau">';
		foreach($itens as $it) {
			$desc = MuChilaItens::descrever($it);
			echo '<article class="mc-item mc-offer'.($it['bloqueio'] ? ' is-blocked' : '').'" data-nome="'.$h(strtolower($desc)).'">';
			if($it['quantidade'] > 1) echo '<span class="mc-item__ribbon">'.$ui::etiqueta('x'.$it['quantidade'], 'ouro').'</span>';
			echo $foto($imagem((int)$it['indice']), 'item');
			echo '<span class="mc-item__name">'.$h($it['nome']).'</span><span class="mc-offer__detail">'.$h($desc).'</span>';
			echo '<span class="mc-item__meta">'.$ui::etiqueta($it['posicao'] < MuChilaItens::POR_PAGINA ? 'baú' : 'baú estendido').'</span>';
			if($it['bloqueio']) echo '<span class="mc-offer__blocked">'.$h($it['bloqueio']).'</span>';
			else echo '<button type="button" class="mc-btn mc-btn--gold mc-btn--sm mc-btn--block" data-mc-vender="item" data-nome="'.$h($desc).'" data-posicao="'.(int)$it['posicao'].'" data-hex="'.$h($it['hex']).'">'.$ui::icone('coin').'Anunciar</button>';
			echo '</article>';
		}
		echo '</div>';
	}
}

if($aba === 'personagens') {
	$chars = $m->personagensDaConta($conta);
	if(!$chars) echo $ui::vazio('Nenhum personagem nesta conta.', 'user');
	else {
		echo '<div class="mc-items mc-items--offers">';
		foreach($chars as $c) {
			$avatar = function_exists('getPlayerClassAvatar') ? getPlayerClassAvatar((int)$c['Class'], false) : null;
			echo '<article class="mc-item mc-offer'.($c['bloqueio'] ? ' is-blocked' : '').'">';
			echo $foto($avatar, 'user');
			echo '<span class="mc-item__name">'.$h($c['Name']).'</span><span class="mc-offer__detail">'.$h($c['descricao']).'</span>';
			if($c['bloqueio']) echo '<span class="mc-offer__blocked">'.$h($c['bloqueio']).'</span>';
			else echo '<button type="button" class="mc-btn mc-btn--gold mc-btn--sm mc-btn--block" data-mc-vender="personagem" data-nome="'.$h($c['Name']).'">'.$ui::icone('coin').'Anunciar</button>';
			echo '</article>';
		}
		echo '</div>';
	}
}

if($aba === 'itens' || $aba === 'personagens') {
	// janela única de preço (preenchida pelo botão "Anunciar" de cada cartão)
	echo '<div class="mc-modal" id="mcVender" role="dialog" aria-modal="true" aria-labelledby="mcVenderTitulo"><div class="mc-modal__backdrop" data-mc-close></div><div class="mc-modal__box">';
	echo '<form method="post"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="posicao"><input type="hidden" name="hex"><input type="hidden" name="nome">';
	echo '<div class="mc-modal__head"><h3 id="mcVenderTitulo">Anunciar</h3><button type="button" class="mc-modal__close" data-mc-close aria-label="Fechar">'.$ui::icone('close').'</button></div>';
	echo '<div class="mc-modal__body"><p style="margin-top:0"><strong id="mcVenderNome"></strong></p>';
	echo '<label for="mcVenderValor">Preço (R$)</label><input id="mcVenderValor" name="valor" class="mc-input" inputmode="decimal" required data-mc-focus placeholder="ex.: '.number_format(max($minimo, 10), 2, ',', '.').'">';
	echo '<p class="small" id="mcVenderLiquido" style="margin:8px 0 0">Mínimo '.MuChilaLoja::real($minimo).', máximo '.MuChilaLoja::real($maximo).'. A loja fica com '.$taxa.'%.</p>';
	echo $ui::aviso('atencao', '', ($aba === 'itens' ? 'O item sai do seu baú' : 'O personagem sai da sua conta (com o inventário dele)').' agora e só volta se você cancelar o anúncio.');
	echo '</div><div class="mc-modal__foot"><button type="button" class="mc-btn mc-btn--ghost" data-mc-close>Voltar</button>'
		.'<button type="submit" name="'.($aba === 'itens' ? 'anunciar_item' : 'anunciar_personagem').'" value="1" class="mc-btn mc-btn--gold">'.$ui::icone('check').'Anunciar</button></div>';
	echo '</form></div></div>';
	echo '<script>(function(){var j=document.getElementById("mcVender"),f=j.querySelector("form"),taxa='.json_encode($m->taxaPct()).',v=f.valor,liq=document.getElementById("mcVenderLiquido"),txt=liq.textContent;'
		.'document.addEventListener("click",function(e){var b=e.target.closest&&e.target.closest("[data-mc-vender]");if(!b)return;'
		.'f.posicao.value=b.dataset.posicao||"";f.hex.value=b.dataset.hex||"";f.nome.value=b.dataset.nome||"";document.getElementById("mcVenderNome").textContent=b.dataset.nome;v.value="";liq.textContent=txt;window.mcJanela.abrir(j);});'
		.'v.addEventListener("input",function(){var n=parseFloat(v.value.replace(/\./g,"").replace(",","."));liq.textContent=isNaN(n)?txt:"Você recebe "+(n*(1-taxa/100)).toLocaleString("pt-BR",{style:"currency",currency:"BRL"})+" (a loja fica com "+String(taxa).replace(".",",")+"%).";});})();</script>';
}

if($aba === 'meus') {
	$lista = $m->anunciosDoVendedor($conta);
	if(!$lista) { echo $ui::vazio('Você ainda não anunciou nada.', 'scroll', '<a class="mc-btn mc-btn--sm" href="'.$url(['aba' => 'itens']).'">Vender um item</a>'); return; }
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>#</th><th>Anúncio</th><th>Preço</th><th>Situação</th><th></th></tr></thead><tbody>';
	foreach($lista as $a) {
		$it = $itemDoAnuncio($a);
		echo '<tr><td>'.(int)$a['id'].'</td><td><strong>'.$h($it ? $it['nome'] : $a['titulo']).'</strong><br><small>'.$h($it ? MuChilaItens::descrever($it) : $a['detalhes']).'</small></td><td>'.MuChilaLoja::real($a['valor']).'</td>'
			.'<td>'.$ui::etiqueta(MuChilaMercado::statusAnuncio($a['status']), $statusTom[$a['status']] ?? 'cinza').($a['comprador'] ? '<br><small>para '.$h($a['comprador']).'</small>' : '').'</td><td>';
		if($a['status'] === 'ativo')
			echo $form('<button name="cancelar" value="'.(int)$a['id'].'" class="mc-btn mc-btn--sm"'.$ui::confirma('Cancelar o anúncio? O '.($a['tipo'] === 'item' ? 'item volta para o seu baú' : 'personagem volta para a sua conta').' (esteja fora do jogo).').'>Cancelar</button></form>');
		echo '</td></tr>';
	}
	echo '</tbody></table></div>';
}

if($aba === 'compras') {
	$lista = $m->pedidosDoComprador($conta);
	if(!$lista) { echo $ui::vazio('Você ainda não comprou nada.', 'history', '<a class="mc-btn mc-btn--sm" href="'.$url().'">Ver os anúncios</a>'); return; }
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>#</th><th>Data</th><th>Compra</th><th>Valor</th><th>Situação</th><th></th></tr></thead><tbody>';
	foreach($lista as $p)
		echo '<tr><td>'.(int)$p['id'].'</td><td>'.MuChilaLoja::data($p['criado']).'</td><td>'.$h($p['descricao']).'</td><td>'.MuChilaLoja::real($p['valor']).'</td>'
			.'<td>'.$ui::etiqueta(MuChilaMercado::statusPedido($p['status']), $statusTom[$p['status']] ?? 'cinza').'</td><td><a href="'.$url(['pedido' => (int)$p['id']]).'">ver</a></td></tr>';
	echo '</tbody></table></div>';
}

if($aba === 'conta') {
	$v = $m->vendedor($conta);
	echo '<div class="mc-card">';
	if($m->vendedorLigado($conta)) {
		echo '<p style="margin-top:0">Sua conta de vendedor está <strong>ligada</strong>'.((int)($v['simulado'] ?? 0) === 1 ? ' (modo de teste)' : ' ao Mercado Pago (usuário '.$h($v['mp_user_id']).')')
			.' desde '.MuChilaLoja::data($v['conectado_em']).'.</p>';
		echo "<p>Quando alguém compra um anúncio seu, o PIX é gerado em nome da <strong>sua</strong> conta do Mercado Pago: você recebe o valor menos <strong>$taxa%</strong> da loja, direto no Mercado Pago.</p>";
		echo $form('<button name="desligar" value="1" class="mc-btn"'.$ui::confirma('Desligar a conta do Mercado Pago? Você não poderá vender até ligar de novo.').'>Desligar</button></form>');
	} else {
		echo "<p style=\"margin-top:0\">Para vender, ligue a sua conta do <strong>Mercado Pago</strong>. Você autoriza a loja do Mu Chila a gerar cobranças em seu nome; o dinheiro das vendas cai direto na sua conta, e a loja fica com <strong>$taxa%</strong>.</p>";
		echo '<p class="small">A loja nunca vê a sua senha: a autorização é feita no próprio site do Mercado Pago e pode ser desligada quando quiser.</p>';
		echo $form('<button name="conectar" value="1" class="mc-btn mc-btn--gold">'.($m->modoTeste() ? 'Ligar (modo de teste)' : 'Ligar minha conta do Mercado Pago').'</button></form>');
	}
	echo '</div>';
}

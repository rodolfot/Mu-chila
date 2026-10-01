<?php
/**
 * Mu Chila - Loja de itens (área do jogador, 30/09/2026): monta o item e paga com Cash; o item vai para o baú.
 * Núcleo e regras: includes/muchila/MuChilaLojaItens.php. Catálogo e preços: includes/config/muchila.lojaitens.json.
 * Duas telas: vitrine (sem ?item) e montagem do item (?item=<seção>-<tipo>). Comportamento: templates/muchila/js/lojaitens.js.
 */
if(!isLoggedIn()) redirect(1, 'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLojaItens.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');

$h = function($v) { return MuChilaUI::h($v); };
$ui = 'MuChilaUI';
$conta = (string)$_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];
$url = function(array $q = []) { return __BASE_URL__ . 'usercp/lojaitens' . ($q ? '?' . http_build_query($q) : ''); };
$imagem = function(array $it) { return __BASE_URL__ . 'api/muchila-item-imagem.php?s=' . (int)$it['secao'] . '&t=' . (int)$it['tipo']; };

$erro = null;
try {
	$loja = new MuChilaLojaItens();
	if($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['comprar'])) {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra a loja de novo.');
		$itemId = (string)($_POST['item'] ?? '');
		$r = $loja->comprar($conta, $itemId, $_POST, (int)($_POST['preco'] ?? -1), (string)($_SERVER['REMOTE_ADDR'] ?? ''));
		$_SESSION['muchila_lojaitens_ok'] = $r;
		redirect(1, 'usercp/lojaitens?item=' . rawurlencode($itemId));
	}
} catch(Exception $ex) {
	$erro = $ex->getMessage();
}

$saldo = isset($loja) ? (function() use ($loja, $conta) { try { return $loja->saldo($conta); } catch(Exception $e) { return 0; } })() : 0;
echo $ui::titulo('Loja de itens', 'Monte o seu item do jeito que quiser e receba direto no baú.', 'chest', $ui::carteira($saldo, '', true));
if(!isset($loja)) { echo $ui::aviso('perigo', 'Loja indisponível', $h($erro)); return; }
if(!$loja->ativa()) { echo $ui::aviso('atencao', 'Loja fechada', 'A loja de itens está fechada no momento. Volte mais tarde.', 'lock'); return; }
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!empty($_SESSION['muchila_lojaitens_ok'])) {
	$ok = $_SESSION['muchila_lojaitens_ok'];
	unset($_SESSION['muchila_lojaitens_ok']);
	echo $ui::aviso('sucesso', 'Compra concluída!', '<strong>' . $h($ok['descricao']) . '</strong> está no seu ' . $h($ok['pagina']) . '. Foram cobrados '
		. $h(MuChilaLojaItens::cash((int)$ok['preco'])) . '; seu saldo agora é ' . $h(MuChilaLojaItens::cash((int)$ok['saldo'])) . '. Entre no jogo e abra o baú.', 'gift');
	echo '<script>document.addEventListener("DOMContentLoaded",function(){window.mcToast&&mcToast(' . json_encode('Item entregue no baú: ' . $ok['descricao']) . ',"gift")});</script>';
}

$catalogo = $loja->catalogo();
if(!$catalogo) { echo $ui::vazio('Nenhum item à venda no momento.', 'chest'); return; }
$classesTodas = count(MuChilaLojaItens::CLASSES);
$textoClasses = function(array $it) use ($classesTodas) { return count($it['classes']) >= 9 ? 'Todas as classes' : implode(', ', $it['classes']); };

// ====================================================================== montagem do item
if(isset($_GET['item'])) {
	$item = $loja->item((string)$_GET['item']);
	if(!$item) { echo $ui::aviso('atencao', 'Item não encontrado', 'Esse item não está mais à venda. <a href="' . $url() . '">Voltar para a loja</a>.'); return; }
	$categoria = null;
	foreach($catalogo as $c) if($c['id'] === $item['categoria']) $categoria = $c;
	// depois de um erro, a tela volta com a escolha que o jogador tinha feito
	$antes = $erro && ($_POST['item'] ?? '') === $item['id'] ? $_POST : [];
	try { $esc = $loja->escolha($item, $antes); } catch(Exception $e) { $esc = $loja->escolha($item, []); }
	$excAntes = $esc['exc'];
	$passo = $item['adicional_passo'];
	$sufixo = $passo === 1 ? '%' : '';
	$dados = [
		'precos' => $loja->tabelaParaTela()['precos'], 'saldo' => $saldo,
		'item' => ['id' => $item['id'], 'nome' => $item['nome'], 'preco' => $item['preco'], 'nivel_max' => $item['nivel_max'], 'adicional' => $item['adicional'],
		           'passo' => $passo, 'sufixo' => $sufixo, 'sorte' => $item['sorte'], 'skill' => $item['tem_skill'], 'exc_max' => $item['exc_max']],
	];

	echo '<a class="mc-back" href="' . $url() . ($categoria ? '#' . $h($categoria['id']) : '') . '">' . $ui::icone('arrow-left') . 'Voltar para ' . $h($categoria['nome'] ?? 'a loja') . '</a>';
	echo $ui::aviso('atencao', 'Antes de comprar', '<ul>'
		. '<li><strong>Saia do jogo</strong> e espere uns 30 segundos: o item vai direto para o seu <strong>baú</strong>, e com o jogo aberto a compra é recusada.</li>'
		. '<li>O baú precisa de um espaço livre de <strong>' . $item['largura'] . 'x' . $item['altura'] . '</strong> (o baú estendido também vale, se estiver liberado).</li>'
		. '<li>Confira tudo antes de confirmar: <strong>itens comprados não são devolvidos nem trocados</strong>.</li></ul>', 'alert');
	echo '<noscript>' . $ui::aviso('perigo', 'JavaScript desligado', 'Ligue o JavaScript do navegador para montar o item e ver o preço.') . '</noscript>';

	echo '<div class="mc-detail">';
	// ---------------- vitrine do item
	echo '<div class="mc-showcase">';
		echo '<div class="mc-show" id="mcShow" data-raridade="comum">';
			echo '<div class="mc-show__stage" id="mcStage" title="Clique para ampliar">';
				echo '<img src="' . $imagem($item) . '" alt="' . $h($item['nome']) . '" onerror="this.hidden=true;this.nextElementSibling.hidden=false">';
				echo '<span hidden>' . $ui::icone($categoria['icone'] ?? 'item') . '</span>';
			echo '</div>';
			echo '<div class="mc-show__name"><h2 id="mcNome">' . $h($item['nome']) . '</h2><span class="mc-show__rarity" id="mcRaridade">Comum</span></div>';
			echo '<p class="mc-show__note">*foto meramente ilustrativa</p>';
		echo '</div>';
		echo '<div class="mc-specs">'
			. '<div class="mc-spec"><span>Tamanho</span><strong>' . $item['largura'] . ' x ' . $item['altura'] . '</strong></div>'
			. '<div class="mc-spec"><span>Nível mínimo</span><strong>' . ($item['nivel_req'] ?: '—') . '</strong></div>'
			. '<div class="mc-spec"><span>Durabilidade</span><strong>' . (int)$item['durabilidade'] . '</strong></div></div>';
		if($item['classes']) {
			echo '<div class="mc-classes">';
			if(count($item['classes']) >= 9) echo $ui::etiqueta('Todas as classes', 'ouro');
			else foreach($item['classes'] as $cl) echo $ui::etiqueta($cl, 'arcano');
			echo '</div>';
		}
	echo '</div>';

	// ---------------- montagem
	echo '<form class="mc-config" method="post" id="mcForm" action="' . $url(['item' => $item['id']]) . '" autocomplete="off">';
	echo '<input type="hidden" name="csrf" value="' . $h($csrf) . '"><input type="hidden" name="item" value="' . $h($item['id']) . '">';
	echo '<input type="hidden" name="preco" id="mcPreco" value="' . $loja->preco($item, $esc) . '"><input type="hidden" name="comprar" value="1">';

	echo '<section class="mc-block"><div class="mc-block__head"><h3>' . $ui::icone('zap') . 'Nível</h3><small>de +0 a +' . $item['nivel_max'] . '</small></div>';
	echo '<div class="mc-stepper"><div class="mc-stepper__row">'
		. '<button type="button" class="mc-stepper__btn" data-passo="-1" aria-label="Diminuir o nível">' . $ui::icone('minus') . '</button>'
		. '<output class="mc-stepper__value" id="mcNivelTxt" for="mcNivel">+' . $esc['nivel'] . '</output>'
		. '<button type="button" class="mc-stepper__btn" data-passo="1" aria-label="Aumentar o nível">' . $ui::icone('plus') . '</button></div>'
		. '<input type="range" class="mc-range" id="mcNivel" name="nivel" min="0" max="' . $item['nivel_max'] . '" step="1" value="' . $esc['nivel'] . '" aria-label="Nível">'
		. '<div class="mc-ticks"><span>+0</span><span>+' . intdiv($item['nivel_max'], 3) . '</span><span>+' . intdiv($item['nivel_max'] * 2, 3) . '</span><span>+' . $item['nivel_max'] . '</span></div></div>';
	echo '</section>';

	if($item['adicional']) {
		echo '<section class="mc-block"><div class="mc-block__head"><h3>' . $ui::icone('plus') . 'Adicional</h3><small>' . ($passo === 1 ? 'recuperação de vida' : 'de +0 a +28') . '</small></div>';
		echo '<div class="mc-segment" style="--n:8" role="radiogroup" aria-label="Adicional">';
		for($o = 0; $o <= 7; $o++)
			echo '<label><input type="radio" name="adicional" value="' . $o . '"' . ($esc['adicional'] === $o ? ' checked' : '') . '><span>+' . ($o * $passo) . $sufixo . '</span></label>';
		echo '</div></section>';
	}

	if($item['sorte'] || $item['tem_skill']) {
		echo '<section class="mc-block"><div class="mc-block__head"><h3>' . $ui::icone('star') . 'Opções</h3></div><div class="mc-toggles">';
		if($item['sorte']) echo '<label class="mc-switch"><span class="mc-switch__text"><strong>Sorte</strong><small>+5% de dano crítico e +25% na Jewel of Soul</small></span>'
			. '<input type="checkbox" name="sorte" value="1"' . ($esc['sorte'] ? ' checked' : '') . '><span class="mc-switch__track"></span></label>';
		if($item['tem_skill']) echo '<label class="mc-switch"><span class="mc-switch__text"><strong>Skill</strong><small>libera o golpe especial da arma</small></span>'
			. '<input type="checkbox" name="skill" value="1"' . ($esc['skill'] ? ' checked' : '') . '><span class="mc-switch__track"></span></label>';
		echo '</div></section>';
	}

	if($item['exc_max'] > 0) {
		echo '<section class="mc-block"><div class="mc-block__head"><h3>' . $ui::icone('sparkle') . 'Opções excelentes <small id="mcExcConta">0/' . $item['exc_max'] . '</small></h3>'
			. '<span><button type="button" class="mc-link-btn" data-exc="todas">Marcar todas</button><button type="button" class="mc-link-btn" data-exc="nenhuma">Limpar</button></span></div>';
		echo '<div class="mc-excs">';
		foreach($item['exc_nomes'] as $bit => $nome)
			echo '<label class="mc-exc' . ($excAntes & (1 << $bit) ? ' is-on' : '') . '"><input type="checkbox" name="exc[]" value="' . $bit . '"' . ($excAntes & (1 << $bit) ? ' checked' : '') . '>'
				. '<span class="mc-exc__box">' . $ui::icone('check') . '</span><span>' . $h($nome) . '</span></label>';
		echo '</div></section>';
	}

	echo '<section class="mc-summary" aria-live="polite">';
	echo '<ul class="mc-summary__lines" id="mcLinhas"></ul>';
	echo '<div class="mc-summary__total"><span>Preço total</span><strong id="mcTotal">' . number_format($loja->preco($item, $esc), 0, ',', '.') . ' <small>Cash</small></strong></div>';
	echo '<p class="mc-summary__after" id="mcDepois"></p>';
	echo '<button type="submit" class="mc-btn mc-btn--gold mc-btn--block mc-btn--shine mc-buy" id="mcComprar">' . $ui::icone('cart') . 'Comprar</button>';
	echo '<a class="mc-btn mc-btn--ruby mc-btn--block mc-buy" id="mcRecarregar" href="' . __BASE_URL__ . 'usercp/loja#cash" hidden>' . $ui::icone('coin') . 'Recarregar Cash</a>';
	echo '</section>';
	echo '</form>';
	echo '</div>';

	// ---------------- confirmação
	echo '<div class="mc-modal" id="mcConfirma" role="dialog" aria-modal="true" aria-labelledby="mcConfirmaTitulo"><div class="mc-modal__backdrop" data-mc-close></div><div class="mc-modal__box">';
	echo '<div class="mc-modal__head"><h3 id="mcConfirmaTitulo">Confirmar compra</h3><button type="button" class="mc-modal__close" data-mc-close aria-label="Fechar">' . $ui::icone('close') . '</button></div>';
	echo '<div class="mc-modal__body"><div class="mc-confirm-item"><img src="' . $imagem($item) . '" alt="" onerror="this.hidden=true"><div><strong id="mcConfNome"></strong><small id="mcConfOpcoes"></small></div></div>';
	echo '<ul class="mc-summary__lines"><li><span>Preço</span><span id="mcConfPreco"></span></li><li><span>Seu saldo depois</span><span id="mcConfSaldo"></span></li><li><span>Entrega</span><span>baú da conta ' . $h($conta) . '</span></li></ul>';
	echo $ui::aviso('atencao', '', 'Esteja <strong>fora do jogo</strong>. A compra não tem devolução.');
	echo '</div><div class="mc-modal__foot"><button type="button" class="mc-btn mc-btn--ghost" data-mc-close>Voltar</button>'
		. '<button type="submit" form="mcForm" class="mc-btn mc-btn--gold" id="mcConfirmar" data-mc-focus>' . $ui::icone('check') . 'Confirmar e pagar</button></div>';
	echo '</div></div>';

	echo '<script type="application/json" id="mcLojaDados">' . json_encode($dados, JSON_HEX_TAG | JSON_UNESCAPED_UNICODE) . '</script>';
	echo '<script src="' . __BASE_URL__ . 'templates/muchila/js/lojaitens.js?v=1" defer></script>';
	return;
}

// ====================================================================== vitrine
$grupos = [];
foreach($catalogo as $c) $grupos[$c['grupo']][] = $c;
$total = array_sum(array_map(function($c) { return count($c['itens']); }, $catalogo));
$classes = [];
foreach($catalogo as $c) foreach($c['itens'] as $it) foreach($it['classes'] as $cl) $classes[$cl] = true;
$p = $loja->cfg['precos'];

echo $ui::aviso('info', 'Como funciona', 'Escolha o item, ajuste nível, adicional, sorte, skill e opções excelentes, e pague com o seu <strong>Cash</strong>. '
	. 'O item chega no <strong>baú</strong> na hora, com você fora do jogo.', 'gift');

echo '<div class="mc-shop">';
echo '<aside class="mc-shop__aside"><nav class="mc-cats" aria-label="Categorias">';
echo '<div class="mc-cats__group"><div class="mc-cats__list"><button type="button" class="mc-cat mc-cat--all is-active" data-cat="">' . $ui::icone('sparkle') . 'Todos os itens <small>(' . $total . ')</small></button></div></div>';
foreach($grupos as $grupo => $cats) {
	echo '<div class="mc-cats__group"><h6>' . $h($grupo) . '</h6><div class="mc-cats__list">';
	foreach($cats as $c) echo '<button type="button" class="mc-cat" data-cat="' . $h($c['id']) . '">' . $ui::icone($c['icone']) . '<span>' . $h($c['nome']) . '</span><small>' . count($c['itens']) . ' itens</small></button>';
	echo '</div></div>';
}
echo '</nav>';
try { $compras = $loja->comprasDaConta($conta, 5); } catch(Exception $e) { $compras = []; }
if($compras) {
	echo '<div class="mc-card"><div class="mc-card__head"><h3 class="mc-card__title">' . $ui::icone('history') . 'Suas últimas compras</h3></div><ul class="mc-history">';
	foreach($compras as $c) echo '<li><span>' . $h($c['descricao']) . '<time>' . $h(MuChilaLoja::data($c['criado'])) . '</time></span><strong>' . number_format((int)$c['preco'], 0, ',', '.') . '</strong></li>';
	echo '</ul></div>';
}
echo '</aside>';

echo '<section>';
echo '<div class="mc-toolbar">'
	. '<label class="mc-field"><span class="mc-sr">Buscar item</span>' . $ui::icone('search') . '<input type="search" class="mc-input" id="mcBusca" placeholder="Buscar pelo nome do item..."></label>'
	. '<select class="mc-input" id="mcClasse" aria-label="Filtrar por classe"><option value="">Todas as classes</option>';
foreach(array_keys($classes) as $cl) echo '<option value="' . $h($cl) . '">' . $h($cl) . '</option>';
echo '</select><select class="mc-input" id="mcOrdem" aria-label="Ordenar"><option value="">Destaques primeiro</option><option value="preco">Menor preço</option>'
	. '<option value="-preco">Maior preço</option><option value="nome">Nome (A-Z)</option><option value="nivel">Nível mínimo</option></select></div>';
echo '<p class="mc-count" id="mcContagem">' . $total . ' itens</p>';
echo '<div class="mc-items" id="mcItens">';
$i = 0;
foreach($catalogo as $c) foreach($c['itens'] as $it) {
	echo '<a class="mc-item" href="' . $url(['item' => $it['id']]) . '" data-cat="' . $h($c['id']) . '" data-nome="' . $h(strtolower($it['nome'])) . '" data-classes="' . $h(implode('|', $it['classes'])) . '"'
		. ' data-preco="' . $it['preco'] . '" data-nivel="' . (int)$it['nivel_req'] . '" data-destaque="' . ($it['destaque'] ? 1 : 0) . '" data-ordem="' . $i++ . '" style="animation-delay:' . min(0.4, $i * 0.012) . 's">';
	if($it['destaque']) echo '<span class="mc-item__ribbon">' . $ui::etiqueta('Destaque', 'ouro') . '</span>';
	echo '<span class="mc-item__frame"><img src="' . $imagem($it) . '" alt="" loading="lazy" onerror="this.hidden=true;this.nextElementSibling.hidden=false"><span hidden>' . $ui::icone($c['icone']) . '</span></span>';
	echo '<span class="mc-item__name">' . $h($it['nome']) . '</span>';
	echo '<span class="mc-item__meta">' . $ui::etiqueta($it['largura'] . 'x' . $it['altura']) . ($it['exc'] ? $ui::etiqueta('Excelente', 'verde') : '')
		. (count($it['classes']) === 1 ? $ui::etiqueta($it['classes'][0], 'arcano') : '') . '</span>';
	echo '<span class="mc-item__price"><span>a partir de</span><strong>' . number_format($it['preco'], 0, ',', '.') . ' Cash</strong></span>';
	echo '</a>';
}
echo '</div>';
echo '<div id="mcVazio" hidden>' . $ui::vazio('Nenhum item com esse filtro.', 'search', '<button type="button" class="mc-btn mc-btn--sm" id="mcLimpar">Limpar filtros</button>') . '</div>';
echo '<p class="small" style="margin-top:22px">Preços: nível +' . $loja->cfg['nivel_max'] . ' ' . number_format($p['nivel'][$loja->cfg['nivel_max']], 0, ',', '.') . ' · adicional máximo '
	. number_format($p['adicional'][7], 0, ',', '.') . ' · sorte ' . $p['sorte'] . ' · skill ' . $p['skill'] . ' · ' . $loja->cfg['exc_max'] . ' excelentes '
	. number_format($p['excelente'][$loja->cfg['exc_max']], 0, ',', '.') . ' Cash (somados ao preço do item).</p>';
echo '</section></div>';
echo '<script src="' . __BASE_URL__ . 'templates/muchila/js/lojaitens.js?v=1" defer></script>';

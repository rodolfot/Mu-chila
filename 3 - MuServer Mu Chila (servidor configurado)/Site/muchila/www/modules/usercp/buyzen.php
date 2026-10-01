<?php
/**
 * Mu Chila - Comprar Zen (01/10/2026; troca o do WebEngine, que dependia do sistema de créditos dele, não usado aqui).
 * Paga com Cash e o Zen vai para o baú da conta. Núcleo: includes/muchila/MuChilaZen.php. Pacotes: muchila.zen.json
 * (aba Site > Comprar Zen do Mu Chila Admin).
 */
if(!isLoggedIn()) redirect(1, 'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaZen.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
$ui = 'MuChilaUI';
$h = function($v) { return MuChilaUI::h($v); };
$conta = (string)$_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];

$erro = null;
try {
	$zen = new MuChilaZen();
	if($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['comprar'])) {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra a página de novo.');
		$_SESSION['muchila_zen_ok'] = $zen->comprar($conta, (string)$_POST['comprar'], (int)($_POST['cash'] ?? -1), (string)($_SERVER['REMOTE_ADDR'] ?? ''));
		redirect(1, 'usercp/buyzen');
	}
} catch(Throwable $ex) {
	$erro = MuChilaUI::erro($ex, 'Não foi possível concluir a compra agora. Nada foi cobrado; tente de novo em alguns minutos.');
}

$saldo = 0; $noBau = 0;
if(isset($zen)) { try { $saldo = $zen->saldo($conta); $noBau = $zen->zenNoBau($conta); } catch(Throwable $e) { } }
echo $ui::titulo('Comprar Zen', 'Troque Cash por Zen. O Zen vai direto para o baú da sua conta.', 'coin', $ui::carteira($saldo, '', true));
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!isset($zen)) return;
if(!$zen->ativa()) { echo $ui::aviso('atencao', 'Fechado', 'A compra de Zen está fechada no momento.', 'lock'); return; }
if(!empty($_SESSION['muchila_zen_ok'])) {
	$ok = $_SESSION['muchila_zen_ok'];
	unset($_SESSION['muchila_zen_ok']);
	echo $ui::aviso('sucesso', 'Zen no baú!', '<strong>' . $h(MuChilaZen::zenTexto((int)$ok['zen'])) . '</strong> de Zen foram para o seu baú (agora ele tem '
		. $h(MuChilaZen::zenTexto((int)$ok['zen_bau'])) . '). Foram cobrados ' . number_format((int)$ok['cash'], 0, ',', '.') . ' Cash; seu saldo agora é '
		. number_format((int)$ok['saldo'], 0, ',', '.') . ' Cash.', 'gift');
}

echo $ui::aviso('atencao', 'Antes de comprar', '<ul><li><strong>Saia do jogo</strong> e espere uns 30 segundos: com o jogo aberto a compra é recusada.</li>'
	. '<li>O baú guarda no máximo <strong>2 bilhões</strong> de Zen; hoje o seu tem <strong>' . $h(MuChilaZen::zenTexto($noBau)) . '</strong>.</li>'
	. '<li>Cash gasto em Zen não volta (veja a <a href="' . __BASE_URL__ . 'refunds/">Política de Reembolso</a>).</li></ul>', 'alert');

$pacotes = array_values($zen->pacotes());
$melhor = null; $melhorTaxa = 0;
foreach($pacotes as $p) { $t = $p['zen'] / $p['cash']; if($t > $melhorTaxa) { $melhorTaxa = $t; $melhor = $p['id']; } }
echo '<div class="mc-packs mc-packs--zen">';
foreach($pacotes as $p) {
	$cabe = $noBau + $p['zen'] <= MuChilaZen::ZEN_MAXIMO;
	$destaque = $p['id'] === $melhor && count($pacotes) > 1;
	echo '<div class="mc-pack' . ($destaque ? ' mc-pack--featured' : '') . '">';
	if($destaque) echo '<span class="mc-pack__ribbon">' . $ui::etiqueta('Melhor preço', 'ouro') . '</span>';
	echo '<span class="mc-pack__icon">' . $ui::icone('coin') . '</span><span class="mc-pack__name">' . $h(MuChilaZen::zenTexto($p['zen'])) . ' de Zen</span>'
		. '<span class="mc-pack__price">' . number_format($p['cash'], 0, ',', '.') . ' <small>Cash</small></span>'
		. '<span class="mc-pack__note">' . number_format($p['zen'] / $p['cash'] / 1000000, 1, ',', '.') . ' milhões por Cash</span><div class="mc-pack__foot">';
	if(!$cabe) echo '<p>Não cabe no seu baú agora (limite de 2 bilhões).</p>';
	elseif($saldo < $p['cash']) echo '<a class="mc-btn mc-btn--block" href="' . __BASE_URL__ . 'usercp/loja#cash">' . $ui::icone('plus') . 'Recarregar Cash</a>';
	else echo '<button type="button" class="mc-btn ' . ($destaque ? 'mc-btn--gold mc-btn--shine' : '') . ' mc-btn--block" data-mc-zen="' . $h($p['id']) . '" data-cash="' . (int)$p['cash'] . '" data-nome="' . $h(MuChilaZen::zenTexto($p['zen'])) . ' de Zen">' . $ui::icone('cart') . 'Comprar</button>';
	echo '</div></div>';
}
echo '</div>';

// confirmação (preenchida pelo botão do pacote)
echo '<div class="mc-modal" id="mcZen" role="dialog" aria-modal="true" aria-labelledby="mcZenTitulo"><div class="mc-modal__backdrop" data-mc-close></div><div class="mc-modal__box">'
	. '<form method="post"><input type="hidden" name="csrf" value="' . $h($csrf) . '"><input type="hidden" name="cash">'
	. '<div class="mc-modal__head"><h3 id="mcZenTitulo">Confirmar compra</h3><button type="button" class="mc-modal__close" data-mc-close aria-label="Fechar">' . $ui::icone('close') . '</button></div>'
	. '<div class="mc-modal__body"><ul class="mc-summary__lines"><li><span>Pacote</span><span id="mcZenNome"></span></li><li><span>Preço</span><span id="mcZenPreco"></span></li>'
	. '<li><span>Seu saldo depois</span><span id="mcZenSaldo"></span></li><li><span>Entrega</span><span>baú da conta ' . $h($conta) . '</span></li></ul>'
	. $ui::aviso('atencao', '', 'Esteja <strong>fora do jogo</strong>. A compra não tem devolução.') . '</div>'
	. '<div class="mc-modal__foot"><button type="button" class="mc-btn mc-btn--ghost" data-mc-close>Voltar</button><button type="submit" name="comprar" class="mc-btn mc-btn--gold" data-mc-focus>' . $ui::icone('check') . 'Confirmar</button></div>'
	. '</form></div></div>';
echo '<script>(function(){var j=document.getElementById("mcZen"),f=j.querySelector("form"),saldo=' . (int)$saldo . ';document.addEventListener("click",function(e){var b=e.target.closest&&e.target.closest("[data-mc-zen]");if(!b)return;'
	. 'var c=parseInt(b.dataset.cash,10);f.comprar.value=b.dataset.mcZen;f.cash.value=c;document.getElementById("mcZenNome").textContent=b.dataset.nome;'
	. 'document.getElementById("mcZenPreco").textContent=c.toLocaleString("pt-BR")+" Cash";document.getElementById("mcZenSaldo").textContent=(saldo-c).toLocaleString("pt-BR")+" Cash";window.mcJanela.abrir(j);});})();</script>';

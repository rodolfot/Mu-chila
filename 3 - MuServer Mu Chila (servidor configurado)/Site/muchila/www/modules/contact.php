<?php
/**
 * Mu Chila - Contate-nos (01/10/2026; troca o do WebEngine, que precisava de servidor de e-mail e estava desligado).
 * A mensagem fica em MUCHILA_CONTATO e o dono lê e responde pela aba Site > Mensagens do Mu Chila Admin; quem está logado
 * vê as próprias mensagens e as respostas aqui. Texto e canais (Discord, WhatsApp...): muchila.paginas.json (aba Site > Páginas).
 */
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaPaginas.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
$ui = 'MuChilaUI';
$h = function($v) { return MuChilaUI::h($v); };
$logado = isLoggedIn();
$conta = $logado ? (string)$_SESSION['username'] : null;
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];
$cfg = MuChilaPaginas::contato();
$contato = new MuChilaContato();

$erro = null;
if($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['enviar']) && $cfg['formulario']) {
	try {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra a página de novo.');
		$id = $contato->enviar($conta, (string)($_POST['contato'] ?? ''), (string)($_POST['assunto'] ?? ''), (string)($_POST['mensagem'] ?? ''), (string)($_SERVER['REMOTE_ADDR'] ?? ''));
		$_SESSION['muchila_contato_ok'] = $id;
		redirect(1, 'contact');
	} catch(Throwable $ex) {
		$erro = MuChilaUI::erro($ex);
	}
}

echo $ui::titulo(lang('module_titles_txt_26', true), $h($cfg['texto']), 'mail');
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!empty($_SESSION['muchila_contato_ok'])) {
	echo $ui::aviso('sucesso', 'Mensagem enviada!', 'Recebemos a sua mensagem (nº ' . (int)$_SESSION['muchila_contato_ok'] . ').'
		. ($logado ? ' A resposta aparece aqui embaixo, em "Suas mensagens".' : ' Vamos responder pelo contato que você informou.'), 'check');
	unset($_SESSION['muchila_contato_ok']);
}

$icones = ['discord' => 'discord', 'whatsapp' => 'whatsapp', 'mail' => 'mail', 'email' => 'mail', 'instagram' => 'instagram', 'facebook' => 'facebook'];
echo '<div class="mc-contact">';
if($cfg['formulario']) {
	$v = function($k) use ($erro, $h) { return $erro ? $h($_POST[$k] ?? '') : ''; };
	echo '<form class="mc-card mc-form" method="post" action="' . __BASE_URL__ . 'contact/"><input type="hidden" name="csrf" value="' . $h($csrf) . '">';
	echo '<h2 class="mc-card__title" style="margin-bottom:14px">' . $ui::icone('scroll') . 'Mandar uma mensagem</h2>';
	if($logado) echo '<p class="small" style="margin-top:0">Enviando como <strong>' . $h($conta) . '</strong>. A resposta aparece nesta página.</p>';
	else echo '<label for="ctContato">Como falar com você</label><input id="ctContato" name="contato" class="mc-input" maxlength="120" placeholder="e-mail, Discord ou WhatsApp" value="' . $v('contato') . '" required>'
		. '<p class="small" style="margin:6px 0 12px">Tem conta? <a href="' . __BASE_URL__ . 'login/">Entre</a> antes de enviar e veja a resposta aqui.</p>';
	if($logado) echo '<label for="ctContato">Outro contato (opcional)</label><input id="ctContato" name="contato" class="mc-input" maxlength="120" placeholder="e-mail, Discord ou WhatsApp" value="' . $v('contato') . '">';
	echo '<label for="ctAssunto">Assunto</label><input id="ctAssunto" name="assunto" class="mc-input" maxlength="80" required placeholder="ex.: Pedido #12 não entregue" value="' . $v('assunto') . '">';
	echo '<label for="ctMensagem">Mensagem</label><textarea id="ctMensagem" name="mensagem" class="mc-input" rows="7" maxlength="2000" required placeholder="Conte o que aconteceu (personagem, horário, número do pedido...)">' . $v('mensagem') . '</textarea>';
	echo '<button type="submit" name="enviar" value="1" class="mc-btn mc-btn--gold mc-btn--block" style="margin-top:14px">' . $ui::icone('arrow-right') . 'Enviar mensagem</button>';
	echo '</form>';
}
echo '<div class="mc-stack">';
if($cfg['canais']) {
	echo '<div class="mc-card"><h2 class="mc-card__title" style="margin-bottom:12px">' . $ui::icone('users') . 'Outros canais</h2><div class="mc-channels">';
	foreach($cfg['canais'] as $c) {
		$link = trim((string)($c['link'] ?? ''));
		$abre = $link !== '' ? '<a class="mc-channel" href="' . $h($link) . '" target="_blank" rel="noopener">' : '<div class="mc-channel">';
		echo $abre . '<span class="mc-channel__icon">' . $ui::icone($icones[$c['tipo'] ?? ''] ?? 'mail') . '</span><span><strong>' . $h($c['rotulo'] ?? '') . '</strong>'
			. '<small>' . $h(($c['texto'] ?? '') !== '' ? $c['texto'] : $link) . '</small></span>' . ($link !== '' ? '</a>' : '</div>');
	}
	echo '</div></div>';
}
echo '<div class="mc-card"><h2 class="mc-card__title" style="margin-bottom:10px">' . $ui::icone('info') . 'Antes de escrever</h2><ul class="mc-tips">'
	. '<li>Compra não entregue? Diga o <strong>número do pedido</strong> (está em <a href="' . __BASE_URL__ . 'usercp/loja">VIP e Cash</a>).</li>'
	. '<li>Personagem travado num mapa? Use <a href="' . __BASE_URL__ . 'usercp/unstick">Descolar personagem</a>.</li>'
	. '<li>Reembolso: leia a <a href="' . __BASE_URL__ . 'refunds/">Política de Reembolso</a>.</li>'
	. '<li>A administração <strong>nunca</strong> pede a sua senha.</li></ul></div>';
if($logado) {
	try { $minhas = $contato->daConta($conta); } catch(Throwable $e) { $minhas = []; }
	if($minhas) {
		$tom = ['nova' => 'ouro', 'lida' => 'arcano', 'respondida' => 'verde', 'arquivada' => 'cinza'];
		$nome = ['nova' => 'enviada', 'lida' => 'lida', 'respondida' => 'respondida', 'arquivada' => 'encerrada'];
		echo '<div class="mc-card"><h2 class="mc-card__title" style="margin-bottom:12px">' . $ui::icone('history') . 'Suas mensagens</h2><div class="mc-messages">';
		foreach($minhas as $m) {
			echo '<details class="mc-message"' . ($m['status'] === 'respondida' ? ' open' : '') . '><summary><span>#' . (int)$m['id'] . ' · ' . $h($m['assunto']) . '</span>'
				. $ui::etiqueta($nome[$m['status']] ?? $m['status'], $tom[$m['status']] ?? 'cinza') . '</summary>'
				. '<p class="mc-message__text">' . nl2br($h($m['mensagem'])) . '</p>'
				. ($m['resposta'] ? '<div class="mc-message__reply"><strong>Resposta da administração</strong>' . ($m['respondido_em'] ? ' <small>' . $h(MuChilaLoja::data($m['respondido_em'])) . '</small>' : '')
					. '<p>' . nl2br($h($m['resposta'])) . '</p></div>' : '')
				. '</details>';
		}
		echo '</div></div>';
	}
}
echo '</div></div>';

<?php
/**
 * Mu Chila - Resets (área do jogador): Master Reset e Supreme Reset (o Reset normal é /reset no jogo).
 * Núcleo/regras: includes/muchila/MuChilaResets.php + procedures MuChila_* no banco.
 */
if(!isLoggedIn()) redirect(1,'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaResets.php');

$h = function($v){ return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
$conta = $_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];

$erro = null;
try {
	$svc = new MuChilaResets();
	if($_SERVER['REQUEST_METHOD'] === 'POST') {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra a página de novo.');
		$nome = (string)($_POST['personagem'] ?? '');
		if(isset($_POST['reset']))   $_SESSION['muchila_msg'] = $svc->reset($nome, $conta);
		if(isset($_POST['master']))  $_SESSION['muchila_msg'] = $svc->masterReset($nome, $conta);
		if(isset($_POST['supreme'])) $_SESSION['muchila_msg'] = $svc->supremeReset($nome, $conta);
		if(isset($_POST['reset']) || isset($_POST['master']) || isset($_POST['supreme'])) redirect(1, 'usercp/resets');
	}
} catch(Exception $ex) { $erro = $ex->getMessage(); }

require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
$ui = 'MuChilaUI';
echo $ui::titulo('Resets', 'Reset, Master Reset e Supreme Reset direto pelo site.', 'reset');
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!isset($svc)) return;
if(empty($svc->cfg['ativo'])) { echo $ui::aviso('atencao', 'Resets desativados', 'Os resets pelo site estão desativados no momento.', 'lock'); return; }
if(!empty($_SESSION['muchila_msg'])) { echo $ui::aviso('sucesso', '', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }

$maxStat = (int)$svc->cfg['max_stat'];
echo $ui::aviso('info', 'Como funciona', '<ul><li><strong>Reset</strong>: nível 400 → 1, com pontos escaláveis.</li>'
	.'<li><strong>Master Reset</strong>: Master Level 600.</li>'
	.'<li><strong>Supreme Reset</strong>: nível 400 + Master 600 + atributos no máximo ('.number_format($maxStat,0,',','.').').</li></ul>'
	.'Pode resetar com o jogo aberto: o personagem vai para a <strong>tela de seleção de personagem</strong> na hora, o reset é aplicado e é só entrar de novo.');

echo '<div class="mc-grid mc-grid--2" style="margin-bottom:22px">';
echo $ui::estatistica('Conta', $h($conta), 'user', 'arcano');
echo $ui::estatistica('Créditos', number_format($svc->creditos($conta),0,',','.'), 'coin', 'ouro', 'ganhos nos Master e Supreme Resets');
echo '</div>';

$pedidos = $svc->pedidos($conta);
if($pedidos) {
	$pendente = false;
	echo '<div class="mc-table"><table class="table table-condensed"><thead><tr><th>Pedido</th><th>Personagem</th><th>Tipo</th><th>Situação</th></tr></thead><tbody>';
	$tipos = ['reset' => 'Reset', 'master' => 'Master Reset', 'supreme' => 'Supreme Reset'];
	foreach($pedidos as $p) {
		if($p['Status'] === 'pendente') $pendente = true;
		$tom = ['feito' => 'verde', 'pendente' => 'arcano', 'erro' => 'rubi', 'expirado' => 'ouro'][$p['Status']] ?? 'cinza';
		echo '<tr><td>'.date('H:i:s', strtotime((string)$p['Criado'])).'</td><td>'.$h($p['Personagem']).'</td><td>'.$h($tipos[$p['Tipo']] ?? $p['Tipo']).'</td>'
			.'<td>'.$ui::etiqueta(ucfirst($p['Status']), $tom).' <small>'.$h($p['Texto']).'</small></td></tr>';
	}
	echo '</tbody></table></div>';
	if($pendente) echo '<meta http-equiv="refresh" content="3">';   // acompanha até o vigia aplicar
}

$chars = $svc->personagens($conta);
if(!$chars) { echo $ui::vazio('Nenhum personagem nesta conta.', 'user'); return; }

echo '<div class="mc-chars">';
foreach($chars as $c) {
	$online = (int)$c['Online'] === 1;
	// os botões são habilitados pelo REQUISITO (a checagem de "fora do jogo" é feita ao clicar, com aviso claro)
	$statsMax = (int)$c['Strength']>=$maxStat && (int)$c['Dexterity']>=$maxStat && (int)$c['Vitality']>=$maxStat && (int)$c['Energy']>=$maxStat;
	$podeReset   = (int)$c['cLevel'] === 400;
	$podeMaster  = (int)$c['MasterLevel'] === 600;
	$podeSupreme = (int)$c['cLevel'] === 400 && (int)$c['MasterLevel'] === 600 && $statsMax;

	echo '<article class="mc-char"><div class="mc-char__head"><strong>'.$h($c['Name']).'</strong>'.($online ? $ui::etiqueta('online', 'verde') : $ui::etiqueta('fora do jogo')).'</div>';
	echo '<div class="mc-char__stats">';
	foreach(['Nível' => $c['cLevel'], 'Master' => $c['MasterLevel'], 'Resets' => $c['ResetCount'], 'M.Resets' => $c['MasterResetCount'], 'S.Resets' => $c['SupremeResetCount']] as $rot => $v)
		echo '<div><span>'.$rot.'</span><strong>'.number_format((int)$v,0,',','.').'</strong></div>';
	echo '</div><div class="mc-char__actions">';
	$f = '<form method="post"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="personagem" value="'.$h($c['Name']).'">';
	echo $f.'<button name="reset" class="mc-btn mc-btn--sm" '.($podeReset?'':'disabled title="Precisa do nível 400"').' '
		.'onclick="return confirm(\'Reset em '.$h($c['Name']).'? Volta ao nível 1, zera os atributos e dá os pontos escaláveis.\')">Reset</button></form>';
	echo $f.'<button name="master" class="mc-btn mc-btn--sm mc-btn--gold" '.($podeMaster?'':'disabled title="Precisa do Master Level 600"').' '
		.'onclick="return confirm(\'Master Reset em '.$h($c['Name']).'? O Master Level e a árvore de skills serão zerados.\')">Master</button></form>';
	echo $f.'<button name="supreme" class="mc-btn mc-btn--sm mc-btn--ruby" '.($podeSupreme?'':'disabled title="Precisa do nível 400, Master 600 e atributos no máximo"').' '
		.'onclick="return confirm(\'Supreme Reset em '.$h($c['Name']).'? TUDO será reiniciado (inclusive os resets normais voltam a 0).\')">Supreme</button></form>';
	echo '</div></article>';
}
echo '</div>';
echo '<p class="small" style="margin-top:18px">Reset: nível 400. Master: Master Level 600. Supreme: nível 400 + Master 600 + atributos no máximo ('.number_format($maxStat,0,',','.').'). '
	.'Com o jogo aberto, o personagem vai para a seleção de personagem por alguns segundos enquanto o reset é aplicado.</p>';

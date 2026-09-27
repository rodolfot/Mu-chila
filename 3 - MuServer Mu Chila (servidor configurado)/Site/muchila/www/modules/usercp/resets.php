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
		if(isset($_POST['master']))  $_SESSION['muchila_msg'] = $svc->masterReset($nome, $conta);
		if(isset($_POST['supreme'])) $_SESSION['muchila_msg'] = $svc->supremeReset($nome, $conta);
		if(isset($_POST['master']) || isset($_POST['supreme'])) redirect(1, 'usercp/resets');
	}
} catch(Exception $ex) { $erro = $ex->getMessage(); }

echo '<div class="page-title"><span>Resets</span></div>';
if($erro) message('error', $h($erro));
if(!isset($svc)) return;
if(empty($svc->cfg['ativo'])) { message('warning', 'Os resets pelo site estão desativados no momento.'); return; }
if(!empty($_SESSION['muchila_msg'])) { message('success', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }

$maxStat = (int)$svc->cfg['max_stat'];
message('info', 'O <strong>Reset normal</strong> é feito no jogo com <strong>/reset</strong> (nível 400). '
	.'Aqui você faz o <strong>Master Reset</strong> (Master Level 600) e o <strong>Supreme Reset</strong> '
	.'(nível 400 + Master 600 + atributos no máximo). Para resetar, é preciso <strong>sair do jogo</strong>.');

echo '<div class="panel panel-general"><div class="panel-body">';
echo '<p><strong>Conta:</strong> '.$h($conta).' &nbsp;|&nbsp; <strong>Créditos:</strong> '.number_format($svc->creditos($conta),0,',','.').'</p>';
echo '</div></div>';

$chars = $svc->personagens($conta);
if(!$chars) { message('warning', 'Nenhum personagem nesta conta.'); return; }

echo '<table class="table table-condensed"><thead><tr>'
	.'<th>Personagem</th><th>Nível</th><th>Master</th><th>Resets</th><th>M.Resets</th><th>S.Resets</th><th>Ações</th>'
	.'</tr></thead><tbody>';
foreach($chars as $c) {
	$online = (int)$c['Online'] === 1;
	$podeMaster  = !$online && (int)$c['MasterLevel'] === 600;
	$statsMax = (int)$c['Strength']>=$maxStat && (int)$c['Dexterity']>=$maxStat && (int)$c['Vitality']>=$maxStat && (int)$c['Energy']>=$maxStat;
	$podeSupreme = !$online && (int)$c['cLevel'] === 400 && (int)$c['MasterLevel'] === 600 && $statsMax;

	echo '<tr>';
	echo '<td><strong>'.$h($c['Name']).'</strong>'.($online ? ' <span class="label label-warning">online</span>' : '').'</td>';
	echo '<td>'.(int)$c['cLevel'].'</td><td>'.(int)$c['MasterLevel'].'</td>';
	echo '<td>'.(int)$c['ResetCount'].'</td><td>'.(int)$c['MasterResetCount'].'</td><td>'.(int)$c['SupremeResetCount'].'</td>';
	echo '<td>';
	$f = '<form method="post" style="display:inline"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="personagem" value="'.$h($c['Name']).'">';
	// Master Reset
	echo $f.'<button name="master" class="btn btn-xs btn-primary" '.($podeMaster?'':'disabled').' '
		.'onclick="return confirm(\'Master Reset em '.$h($c['Name']).'? O Master Level e a árvore de skills serão zerados.\')">Master Reset</button></form> ';
	// Supreme Reset
	echo $f.'<button name="supreme" class="btn btn-xs btn-danger" '.($podeSupreme?'':'disabled').' '
		.'onclick="return confirm(\'Supreme Reset em '.$h($c['Name']).'? TUDO será reiniciado (inclusive os resets normais voltam a 0).\')">Supreme Reset</button></form>';
	echo '</td></tr>';
}
echo '</tbody></table>';
echo '<p><small>Botões desativados = requisito não atingido ou conta online. Master: Master Level 600. '
	.'Supreme: nível 400 + Master 600 + atributos no máximo ('.number_format($maxStat,0,',','.').').</small></p>';

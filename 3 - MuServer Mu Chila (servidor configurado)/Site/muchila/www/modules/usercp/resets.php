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

echo '<div class="page-title"><span>Resets</span></div>';
if($erro) message('error', $h($erro));
if(!isset($svc)) return;
if(empty($svc->cfg['ativo'])) { message('warning', 'Os resets pelo site estão desativados no momento.'); return; }
if(!empty($_SESSION['muchila_msg'])) { message('success', $h($_SESSION['muchila_msg'])); unset($_SESSION['muchila_msg']); }

$maxStat = (int)$svc->cfg['max_stat'];
message('info', '<strong>Reset</strong> (nível 400 → 1, com pontos escaláveis), '
	.'<strong>Master Reset</strong> (Master Level 600) e '
	.'<strong>Supreme Reset</strong> (nível 400 + Master 600 + atributos no máximo). '
	.'Pode resetar com o jogo aberto: o personagem vai para a <strong>tela de seleção de personagem</strong> na hora, '
	.'o reset é aplicado e é só entrar de novo.');

$pedidos = $svc->pedidos($conta);
if($pedidos) {
	$pendente = false;
	echo '<table class="table table-condensed"><thead><tr><th>Pedido</th><th>Personagem</th><th>Tipo</th><th>Situação</th></tr></thead><tbody>';
	$tipos = ['reset' => 'Reset', 'master' => 'Master Reset', 'supreme' => 'Supreme Reset'];
	foreach($pedidos as $p) {
		if($p['Status'] === 'pendente') $pendente = true;
		$cls = ['feito' => 'success', 'pendente' => 'info', 'erro' => 'danger', 'expirado' => 'warning'][$p['Status']] ?? 'default';
		echo '<tr class="'.$cls.'"><td>'.date('H:i:s', strtotime((string)$p['Criado'])).'</td><td>'.$h($p['Personagem']).'</td><td>'.$h($tipos[$p['Tipo']] ?? $p['Tipo']).'</td><td>'.$h($p['Texto']).'</td></tr>';
	}
	echo '</tbody></table>';
	if($pendente) echo '<meta http-equiv="refresh" content="3">';   // acompanha até o vigia aplicar
}

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
	// os botões são habilitados pelo REQUISITO (a checagem de "fora do jogo" é feita ao clicar, com aviso claro)
	$statsMax = (int)$c['Strength']>=$maxStat && (int)$c['Dexterity']>=$maxStat && (int)$c['Vitality']>=$maxStat && (int)$c['Energy']>=$maxStat;
	$podeReset   = (int)$c['cLevel'] === 400;
	$podeMaster  = (int)$c['MasterLevel'] === 600;
	$podeSupreme = (int)$c['cLevel'] === 400 && (int)$c['MasterLevel'] === 600 && $statsMax;

	echo '<tr>';
	echo '<td><strong>'.$h($c['Name']).'</strong>'.($online ? ' <span class="label label-warning">online</span>' : '').'</td>';
	echo '<td>'.(int)$c['cLevel'].'</td><td>'.(int)$c['MasterLevel'].'</td>';
	echo '<td>'.(int)$c['ResetCount'].'</td><td>'.(int)$c['MasterResetCount'].'</td><td>'.(int)$c['SupremeResetCount'].'</td>';
	echo '<td>';
	$f = '<form method="post" style="display:inline"><input type="hidden" name="csrf" value="'.$h($csrf).'"><input type="hidden" name="personagem" value="'.$h($c['Name']).'">';
	echo $f.'<button name="reset" class="btn btn-xs btn-default" '.($podeReset?'':'disabled').' '
		.'onclick="return confirm(\'Reset em '.$h($c['Name']).'? Volta ao nível 1, zera os atributos e dá os pontos escaláveis.\')">Reset</button></form> ';
	echo $f.'<button name="master" class="btn btn-xs btn-primary" '.($podeMaster?'':'disabled').' '
		.'onclick="return confirm(\'Master Reset em '.$h($c['Name']).'? O Master Level e a árvore de skills serão zerados.\')">Master Reset</button></form> ';
	echo $f.'<button name="supreme" class="btn btn-xs btn-danger" '.($podeSupreme?'':'disabled').' '
		.'onclick="return confirm(\'Supreme Reset em '.$h($c['Name']).'? TUDO será reiniciado (inclusive os resets normais voltam a 0).\')">Supreme Reset</button></form>';
	echo '</td></tr>';
}
echo '</tbody></table>';
echo '<p><small>Reset: nível 400. Master: Master Level 600. Supreme: nível 400 + Master 600 + atributos no máximo ('.number_format($maxStat,0,',','.').'). '
	.'Com o jogo aberto, o personagem vai para a seleção de personagem por alguns segundos enquanto o reset é aplicado.</small></p>';

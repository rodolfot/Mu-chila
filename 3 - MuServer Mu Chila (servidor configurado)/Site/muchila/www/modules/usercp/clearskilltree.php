<?php
/**
 * Mu Chila - Resetar Skill-Tree (01/10/2026; troca o do WebEngine, que apagava todas as habilidades do personagem e devolvia
 * os pontos master sem limpar a árvore). Zera a árvore master, devolve os pontos (1 por Master Level) e tira só os poderes
 * master da lista de habilidades. Núcleo: includes/muchila/MuChilaMaster.php + dbo.MuChila_LimparArvoreMaster.
 * Custo em Zen do personagem: <zen_cost> do includes/config/modules/usercp.clearskilltree.xml (o mesmo do WebEngine).
 */
if(!isLoggedIn()) redirect(1, 'login');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMaster.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
$ui = 'MuChilaUI';
$h = function($v) { return MuChilaUI::h($v); };
$conta = (string)$_SESSION['username'];
if(empty($_SESSION['muchila_csrf'])) $_SESSION['muchila_csrf'] = bin2hex(random_bytes(16));
$csrf = $_SESSION['muchila_csrf'];
$xml = @simplexml_load_file(__PATH_INCLUDES__ . 'config/modules/usercp.clearskilltree.xml');
$ativo = $xml === false || (string)$xml->active !== '0';
$custo = $xml !== false ? max(0, (int)$xml->zen_cost) : 1000000;

$erro = null;
try {
	$master = new MuChilaMaster();
	if($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['personagem'])) {
		if(!hash_equals($csrf, (string)($_POST['csrf'] ?? ''))) throw new Exception('Sessão expirada. Abra a página de novo.');
		if(!$ativo) throw new Exception('O reset da árvore master está desligado no momento.');
		$_SESSION['muchila_msg'] = $master->limparArvore($conta, (string)$_POST['personagem'], $custo);
		redirect(1, 'usercp/clearskilltree');
	}
} catch(Throwable $ex) {
	$erro = MuChilaUI::erro($ex);
}

echo $ui::titulo('Resetar Skill-Tree', 'Zera a árvore master de um personagem e devolve os pontos para distribuir de novo no jogo.', 'tree');
if($erro) echo $ui::aviso('perigo', 'Não deu certo', $h($erro));
if(!isset($master)) return;
if(!$ativo) { echo $ui::aviso('atencao', 'Desligado', 'O reset da árvore master está desligado no momento.', 'lock'); return; }
if(!empty($_SESSION['muchila_msg'])) { echo $ui::aviso('sucesso', 'Pronto!', $h($_SESSION['muchila_msg']), 'check'); unset($_SESSION['muchila_msg']); }
echo $ui::aviso('info', 'Como funciona', '<ul><li>A árvore master fica vazia e os pontos voltam: <strong>1 por Master Level</strong>.</li>'
	. '<li>Saem da lista só os <strong>poderes da árvore master</strong>; as habilidades normais (de orbs e pergaminhos) continuam.</li>'
	. '<li>Custa <strong>' . number_format($custo, 0, ',', '.') . ' de Zen</strong>, tirados do inventário do personagem. Esteja <strong>fora do jogo</strong>.</li></ul>');

try { $chars = $master->personagens($conta); } catch(Throwable $e) { echo $ui::aviso('perigo', 'Não deu certo', $h(MuChilaUI::erro($e))); return; }
if(!$chars) { echo $ui::vazio('Nenhum personagem nesta conta.', 'user'); return; }
echo '<div class="mc-chars">';
foreach($chars as $c) {
	$temArvore = $c['MasterLevel'] !== null && (int)$c['MasterLevel'] > 0;
	$pode = $temArvore && (int)$c['aprendidas'] > 0 && (int)$c['Money'] >= $custo;
	echo '<article class="mc-char"><div class="mc-char__head"><strong>' . $h($c['Name']) . '</strong>' . $ui::etiqueta('nível ' . (int)$c['cLevel'], 'cinza') . '</div>';
	echo '<div class="mc-char__stats mc-char__stats--4">';
	foreach(['Master' => $temArvore ? (int)$c['MasterLevel'] : '—', 'Pontos' => $temArvore ? (int)$c['MasterPoint'] : '—',
	         'Na árvore' => $temArvore ? (int)$c['aprendidas'] : '—', 'Zen' => $ui::h(number_format((int)$c['Money'] / 1000000, 1, ',', '.')) . 'kk'] as $rot => $v)
		echo '<div><span>' . $rot . '</span><strong>' . $v . '</strong></div>';
	echo '</div>';
	$motivo = !$temArvore ? 'Ainda sem Master Level.' : ((int)$c['aprendidas'] === 0 ? 'A árvore já está vazia.' : ((int)$c['Money'] < $custo ? 'Zen insuficiente no personagem.' : ''));
	echo '<form method="post"><input type="hidden" name="csrf" value="' . $h($csrf) . '"><input type="hidden" name="personagem" value="' . $h($c['Name']) . '">'
		. '<button class="mc-btn mc-btn--sm mc-btn--block' . ($pode ? ' mc-btn--gold' : '') . '"' . ($pode ? '' : ' disabled')
		. $ui::confirma('Resetar a árvore master de ' . $c['Name'] . '? Os pontos voltam para distribuir de novo e custa ' . number_format($custo, 0, ',', '.') . ' de Zen.') . '>'
		. $ui::icone('reset') . 'Resetar árvore master</button></form>';
	if($motivo !== '') echo '<p class="small" style="margin:0">' . $motivo . '</p>';
	echo '</article>';
}
echo '</div>';

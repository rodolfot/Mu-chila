<?php
/**
 * Mu Chila - painel do jogador (30/09/2026; substitui o do WebEngine pelo Instalar-Modulos.ps1): carteira, situação da
 * conta (VIP, Passe dos Mapas) e as páginas do usercp.json em cartões com ícone.
 */
if(!isLoggedIn()) redirect(1, 'login');
// tema padrão do WebEngine ligado (Instalar-Modulos.ps1 -TemaPadrao): o painel original, guardado pelo instalador
if(basename(rtrim(__PATH_TEMPLATE_ROOT__, '/')) !== 'muchila' && is_file(__FILE__ . '.original')) { include(__FILE__ . '.original'); return; }
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLoja.php');
$ui = 'MuChilaUI';
$conta = (string)$_SESSION['username'];

echo $ui::titulo(lang('module_titles_txt_3'), 'Olá, <strong>' . $ui::h($conta) . '</strong>. Tudo da sua conta em um lugar só.', 'user');

$c = null;
try { $c = (new MuChilaLoja())->conta($conta); } catch(Exception $e) { }
echo '<div class="mc-grid mc-grid--3" style="margin-bottom:26px">';
echo $ui::carteira($c ? (int)$c['cash'] : $ui::cashDaConta($conta));
if($c) {
	echo $ui::estatistica('Plano', $c['vip_ativo'] ? 'VIP ' . (int)$c['vip_nivel'] : 'Free', 'crown', $c['vip_ativo'] ? 'ouro' : 'arcano',
		$c['vip_ativo'] ? 'até ' . MuChilaLoja::data($c['vip_expira']) : '<a href="' . __BASE_URL__ . 'usercp/loja">Virar VIP</a>');
	echo $ui::estatistica('Passe dos Mapas 400+', $c['passe_ativo'] ? 'Ativo' : 'Sem passe', 'key', $c['passe_ativo'] ? 'verde' : 'rubi',
		$c['passe_ativo'] ? 'até ' . MuChilaLoja::data($c['passe_expira']) : '<a href="' . __BASE_URL__ . 'usercp/loja#passe">Comprar passe</a>');
}
echo '</div>';

$cfg = loadConfig('usercp');
if(!is_array($cfg)) throw new Exception('Could not load usercp, please contact support.');
echo '<h2 class="mc-section-title">' . $ui::icone('sparkle') . 'O que você quer fazer?</h2><div class="mc-tiles">';
foreach($cfg as $element) {
	if(!is_array($element) || !$element['active']) continue;
	$link = $element['type'] == 'internal' ? __BASE_URL__ . $element['link'] : $element['link'];
	$title = check_value(lang($element['phrase'], true)) ? lang($element['phrase']) : 'ERROR';
	echo '<a class="mc-tile" href="' . $link . '"' . ($element['newtab'] ? ' target="_blank" rel="noopener"' : '') . '>'
		. '<span class="mc-tile__icon">' . $ui::icone($ui::iconeDoLink((string)$element['link'])) . '</span><strong>' . $title . '</strong></a>';
}
echo '</div>';

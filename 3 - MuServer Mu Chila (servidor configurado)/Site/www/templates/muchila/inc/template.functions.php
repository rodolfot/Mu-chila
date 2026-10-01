<?php
/**
 * Mu Chila - funções da template "muchila". Os nomes são os mesmos da template padrão do WebEngine
 * (templateBuildNavbar, templateBuildUsercp, templateCastleSiegeWidget, templateLanguageSelector): módulos chamam por eles.
 */
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');

/** Link do menu é a página aberta? ('' = início; 'rankings' vale para rankings/*; 'usercp/loja' só para ela). */
function templateLinkAtivo(string $link): bool {
	$pagina = (string)($_REQUEST['page'] ?? '');
	$sub = (string)($_REQUEST['subpage'] ?? '');
	if($link === '') return $pagina === '' || $pagina === 'home';
	$partes = explode('/', trim($link, '/'));
	if($partes[0] !== $pagina) return false;
	return !isset($partes[1]) || $partes[1] === $sub;
}

/** Itens do navbar.json como links com ícone (<li> dentro do <ul class="mc-nav__links"> da template). */
function templateBuildNavbar() {
	$cfg = loadConfig('navbar');
	if(!is_array($cfg)) return;
	$icones = ['' => 'home', 'info' => 'info', 'downloads' => 'download', 'usercp/loja' => 'coin', 'usercp/lojaitens' => 'chest',
	           'usercp/mercado' => 'market', 'rankings' => 'trophy', 'castlesiege' => 'castle', 'news' => 'scroll', 'donation' => 'gift', 'usercp' => 'user'];
	foreach($cfg as $element) {
		if(!is_array($element) || !$element['active']) continue;
		if($element['visibility'] == 'guest' && isLoggedIn()) continue;
		if($element['visibility'] == 'user' && !isLoggedIn()) continue;
		$link = $element['type'] == 'internal' ? __BASE_URL__ . $element['link'] : $element['link'];
		$title = check_value(lang($element['phrase'], true)) ? lang($element['phrase'], true) : 'Unk_phrase';
		$ativo = $element['type'] == 'internal' && templateLinkAtivo((string)$element['link']);
		echo '<li><a class="mc-nav__link' . ($ativo ? ' is-active' : '') . '" href="' . $link . '"' . ($element['newtab'] ? ' target="_blank" rel="noopener"' : '') . ($ativo ? ' aria-current="page"' : '') . '>'
			. MuChilaUI::icone($icones[$element['link']] ?? 'chevron-right') . '<span>' . $title . '</span></a></li>';
	}
}

/** Menu do jogador (usercp.json) em lista com ícones; marca a página aberta. */
function templateBuildUsercp() {
	$cfg = loadConfig('usercp');
	if(!is_array($cfg)) return;
	echo '<ul class="mc-side-menu">';
	foreach($cfg as $element) {
		if(!is_array($element) || !$element['active']) continue;
		if($element['visibility'] == 'guest' && isLoggedIn()) continue;
		if($element['visibility'] == 'user' && !isLoggedIn()) continue;
		$link = $element['type'] == 'internal' ? __BASE_URL__ . $element['link'] : $element['link'];
		$title = check_value(lang($element['phrase'], true)) ? lang($element['phrase'], true) : 'Unk_phrase';
		$ativo = $element['type'] == 'internal' && templateLinkAtivo((string)$element['link']);
		echo '<li><a class="' . ($ativo ? 'is-active' : '') . '" href="' . $link . '"' . ($element['newtab'] ? ' target="_blank" rel="noopener"' : '') . '>'
			. MuChilaUI::icone(MuChilaUI::iconeDoLink((string)$element['link'])) . '<span>' . $title . '</span></a></li>';
	}
	echo '</ul>';
}

/** Menu da conta (canto da barra do topo). */
function templateUserMenu() {
	$b = __BASE_URL__;
	echo '<a href="' . $b . 'usercp/" role="menuitem">' . MuChilaUI::icone('user') . lang('module_titles_txt_3') . '</a>';
	echo '<a href="' . $b . 'usercp/lojaitens" role="menuitem">' . MuChilaUI::icone('chest') . 'Loja de itens</a>';
	echo '<a href="' . $b . 'usercp/loja" role="menuitem">' . MuChilaUI::icone('coin') . 'VIP, Cash e Passe</a>';
	echo '<a href="' . $b . 'usercp/mercado" role="menuitem">' . MuChilaUI::icone('market') . 'Mercado</a>';
	echo '<a href="' . $b . 'usercp/resets" role="menuitem">' . MuChilaUI::icone('reset') . 'Resets</a>';
	echo '<hr><a class="is-danger" href="' . $b . 'logout/" role="menuitem">' . MuChilaUI::icone('logout') . lang('menu_txt_6') . '</a>';
}

function templateCastleSiegeWidget() {
	$castleSiege = new CastleSiege();
	if(!$castleSiege->showWidget()) return;
	$siegeData = $castleSiege->siegeData();
	if(!is_array($siegeData) || !is_array($siegeData['castle_data'])) return;
	if($siegeData['castle_data'][_CLMN_MCD_OCCUPY_] == 1) {
		$guildOwner = guildProfile($siegeData['castle_data'][_CLMN_MCD_GUILD_OWNER_]);
		$guildOwnerMark = $siegeData['castle_owner_alliance'][0][_CLMN_GUILD_LOGO_];
		$guildMaster = playerProfile($siegeData['castle_owner_alliance'][0][_CLMN_GUILD_MASTER_]);
	} else {
		$guildOwner = '-';
		$guildOwnerMark = '1111111111111111111111111114411111144111111111111111111111111111';
		$guildMaster = '-';
	}
	echo '<div class="mc-card castle-owner-widget">';
		echo '<div class="mc-card__head"><h3 class="mc-card__title">' . MuChilaUI::icone('castle') . lang('castlesiege_widget_title') . '</h3></div>';
		echo '<div style="display:flex;gap:16px;align-items:center">';
			echo '<div>' . returnGuildLogo($guildOwnerMark, 72) . '</div>';
			echo '<div><span class="alt">' . lang('castlesiege_txt_2') . '</span><br>' . $guildOwner . '<br><span class="alt">' . lang('castlesiege_txt_12') . '</span><br>' . $guildMaster . '</div>';
		echo '</div>';
		echo '<div style="margin-top:14px"><span class="alt">' . lang('castlesiege_txt_21') . '</span><br>' . $siegeData['current_stage']['title']
			. '<br><span class="alt">' . lang('castlesiege_txt_1') . '</span><br><strong id="cscountdown">' . $siegeData['warfare_stage_countdown'] . '</strong></div>';
		echo '<a href="' . __BASE_URL__ . 'castlesiege" class="mc-btn mc-btn--sm" style="margin-top:14px">' . lang('castlesiege_txt_7') . '</a>';
	echo '</div>';
}

function templateLanguageSelector() {
	// as pastas de includes/languages deste site (a template padrão lista "br", que aqui é "pt")
	$langList = ['pt' => ['Português', 'BR'], 'en' => ['English', 'US'], 'es' => ['Español', 'ES'], 'ph' => ['Filipino', 'PH'],
	             'ro' => ['Romanian', 'RO'], 'cn' => ['Simplified Chinese', 'CN'], 'ru' => ['Russian', 'RU'], 'lt' => ['Lithuanian', 'LT']];
	$lang = $_SESSION['language_display'] ?? config('language_default', true);
	echo '<ul class="webengine-language-switcher">';
	foreach($langList as $language => $info) {
		echo '<li' . ($language == $lang ? ' style="border-color:var(--line-gold)"' : '') . '><a href="' . __BASE_URL__ . 'language/switch/to/' . strtolower($language) . '" title="' . $info[0] . '"><img src="' . getCountryFlag($info[1]) . '" alt=""/> ' . strtoupper($language) . '</a></li>';
	}
	echo '</ul>';
}

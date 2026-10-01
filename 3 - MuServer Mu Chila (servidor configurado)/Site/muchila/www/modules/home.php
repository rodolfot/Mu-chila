<?php
/**
 * Mu Chila - página inicial (30/09/2026; substitui a do WebEngine pelo Instalar-Modulos.ps1). O herói (título e situação
 * do servidor) fica na template; aqui: atalhos, notícias, quadro de eventos (api/events.php) e tops de nível e guild.
 * Usa os mesmos caches do WebEngine (news.cache, rankings_level.cache, rankings_guilds.cache).
 */
// tema padrão do WebEngine ligado (Instalar-Modulos.ps1 -TemaPadrao): a página original, guardada pelo instalador
if(basename(rtrim(__PATH_TEMPLATE_ROOT__, '/')) !== 'muchila' && is_file(__FILE__ . '.original')) { include(__FILE__ . '.original'); return; }
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
$ui = 'MuChilaUI';
$b = __BASE_URL__;

echo '<div class="mc-actions">';
foreach([
	['usercp/lojaitens', 'chest', 'ouro', 'Loja de itens', 'Monte seu item e receba no baú'],
	['usercp/loja', 'crown', 'rubi', 'VIP, Cash e Passe', 'Mais EXP, drop e mapas 400+'],
	['usercp/mercado', 'market', 'arcano', 'Mercado', 'Compre e venda entre jogadores'],
	['rankings', 'trophy', 'verde', 'Rankings', 'Os mais fortes do servidor'],
] as [$link, $icone, $tom, $titulo, $texto])
	echo '<a class="mc-action mc-action--' . $tom . '" href="' . $b . $link . '"><span class="mc-action__icon">' . $ui::icone($icone) . '</span><strong>' . $titulo . '</strong><span class="mc-action__text">' . $texto . '</span></a>';
echo '</div>';

echo '<div class="mc-grid mc-grid--3">';

// ---------------- notícias
echo '<section class="mc-card mc-span-2"><div class="mc-card__head"><h2 class="mc-card__title">' . $ui::icone('scroll') . 'Últimas notícias' . '</h2>'
	. '<a class="mc-card__link" href="' . $b . 'news/">' . 'Ver todas' . $ui::icone('chevron-right') . '</a></div>';
$newsList = loadCache('news.cache');
if(is_array($newsList) && $newsList) {
	echo '<ul class="mc-news">';
	foreach(array_slice($newsList, 0, 7) as $n) {
		$titulo = base64_decode($n['news_title']);
		if(config('language_switch_active', true) && isset($_SESSION['language_display'], $n['translations'][$_SESSION['language_display']]))
			$titulo = base64_decode($n['translations'][$_SESSION['language_display']]);
		echo '<li><a href="' . $b . 'news/' . $n['news_id'] . '/">' . $ui::etiqueta('Notícia', 'ouro')
			. '<span class="mc-news__title">' . $titulo . '</span><span class="mc-news__date">' . date('d/m/Y', $n['news_date']) . '</span></a></li>';
	}
	echo '</ul>';
} else {
	echo $ui::vazio('Nenhuma notícia publicada ainda.', 'scroll');
}
echo '</section>';

// ---------------- eventos (o tema preenche pelo api/events.php; evento sem agenda continua escondido)
echo '<section class="mc-card"><div class="mc-card__head"><h2 class="mc-card__title">' . $ui::icone('calendar') . 'Agenda de eventos' . '</h2></div><div class="mc-events" id="mcEvents">';
foreach(['bloodcastle' => 'castle', 'devilsquare' => 'flame', 'chaoscastle' => 'skull', 'dragoninvasion' => 'zap', 'goldeninvasion' => 'gem', 'castlesiege' => 'crown'] as $id => $icone)
	echo '<div class="mc-event" id="' . $id . '_box" hidden><span class="mc-event__icon">' . $ui::icone($icone) . '</span>'
		. '<span class="mc-event__info"><span class="mc-event__name" id="' . $id . '_name"></span><span class="mc-event__next">Próximo: <span id="' . $id . '_next"></span></span></span>'
		. '<span class="mc-event__timer" id="' . $id . '"></span></div>';
echo '</div></section>';

// ---------------- top nível
$level = LoadCacheData('rankings_level.cache');
echo '<section class="mc-card"><div class="mc-card__head"><h2 class="mc-card__title">' . $ui::icone('trophy') . 'Top nível' . '</h2>'
	. '<a class="mc-card__link" href="' . $b . 'rankings/level">Ver tudo' . $ui::icone('chevron-right') . '</a></div>';
if(is_array($level) && count($level) > 1) {
	echo '<ol class="mc-rank">';
	foreach(array_slice($level, 1, 10) as $r)
		echo '<li><span class="mc-rank__name">' . playerProfile($r[0]) . '<small>' . getPlayerClass($r[1]) . '</small></span><span class="mc-rank__val">' . number_format($r[2], 0, ',', '.') . '</span></li>';
	echo '</ol>';
} else echo $ui::vazio('O ranking aparece depois da primeira atualização.', 'trophy');
echo '</section>';

// ---------------- top guilds
$guilds = LoadCacheData('rankings_guilds.cache');
echo '<section class="mc-card"><div class="mc-card__head"><h2 class="mc-card__title">' . $ui::icone('shield') . 'Top guilds' . '</h2>'
	. '<a class="mc-card__link" href="' . $b . 'rankings/guilds">Ver tudo' . $ui::icone('chevron-right') . '</a></div>';
if(is_array($guilds) && count($guilds) > 1) {
	$rc = loadConfigurations('rankings');
	$mult = $rc['guild_score_formula'] == 1 ? 1 : $rc['guild_score_multiplier'];
	echo '<ol class="mc-rank">';
	foreach(array_slice($guilds, 1, 10) as $r)
		echo '<li><span class="mc-rank__name" style="display:flex;align-items:center;gap:8px">' . returnGuildLogo($r[3], 20) . guildProfile($r[0]) . '</span>'
			. '<span class="mc-rank__val">' . number_format(floor($r[2] * $mult), 0, ',', '.') . '</span></li>';
	echo '</ol>';
} else echo $ui::vazio('Nenhuma guild no ranking ainda.', 'shield');
echo '</section>';

// ---------------- convite / conta
echo '<section class="mc-card" style="display:flex;flex-direction:column;gap:14px;justify-content:center;background:radial-gradient(120% 100% at 100% 0%,rgba(227,57,79,.16),transparent 60%),var(--surface)">';
if(isLoggedIn()) {
	echo '<h2 class="mc-card__title" style="font-family:var(--font-display);font-size:22px">Bem-vindo de volta!</h2>';
	echo '<p style="margin:0">Use o seu Cash na loja de itens ou faça um reset pelo painel.</p>';
	echo '<a class="mc-btn mc-btn--gold" href="' . $b . 'usercp/">' . $ui::icone('user') . lang('module_titles_txt_3') . '</a>';
} else {
	echo '<h2 class="mc-card__title" style="font-family:var(--font-display);font-size:22px">Pronto para a aventura?</h2>';
	echo '<p style="margin:0">Crie sua conta grátis, baixe o launcher e entre no continente de MU em minutos.</p>';
	echo '<a class="mc-btn mc-btn--gold" href="' . $b . 'register/">' . $ui::icone('sparkle') . lang('menu_txt_3') . '</a>';
	echo '<a class="mc-btn" href="' . $b . 'login/">' . $ui::icone('login') . lang('menu_txt_4') . '</a>';
}
echo '</section>';

echo '</div>';

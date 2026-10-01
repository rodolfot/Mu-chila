<?php
/**
 * Mu Chila - tema do site (30/09/2026). Ligado pelo Instalar-Modulos.ps1 ("website_template": "muchila" no webengine.json);
 * a template "default" do WebEngine continua instalada (voltar = trocar o nome de volta).
 * Estrutura: barra do topo (menu do navbar.json + carteira de Cash) | herói na página inicial | conteúdo (com o menu do
 * jogador ao lado nas páginas usercp/*) | rodapé. Estilo: css/muchila.css; ícones: img/icones.svg; comportamento: js/muchila.js.
 */
if(!defined('access') or !access) die();
include('inc/template.functions.php');

$serverInfoCache = LoadCacheData('server_info.cache');
if(is_array($serverInfoCache)) $srvInfo = explode("|", $serverInfoCache[1][0]);
$maxOnline = config('maximum_online', true);
$onlinePlayers = isset($srvInfo[3]) ? (int)$srvInfo[3] : 0;
$onlinePlayersPercent = check_value($maxOnline) && $maxOnline > 0 ? min(100, $onlinePlayers * 100 / $maxOnline) : 0;

if(!isset($_REQUEST['page'])) $_REQUEST['page'] = '';
if(!isset($_REQUEST['subpage'])) $_REQUEST['subpage'] = '';
$mcPagina = (string)$_REQUEST['page'];
$mcInicio = $mcPagina === '' || $mcPagina === 'home';
// páginas do jogador com o menu ao lado; a loja de itens tem as próprias colunas (categorias) e ocupa a largura toda
$mcComMenu = $mcPagina === 'usercp' && $_REQUEST['subpage'] !== '' && !in_array($_REQUEST['subpage'], ['lojaitens'], true);
$mcLogado = isLoggedIn();
$mcConta = $mcLogado ? (string)$_SESSION['username'] : null;
$mcCash = $mcLogado ? MuChilaUI::cashDaConta($mcConta) : 0;
?>
<!DOCTYPE html>
<html lang="pt-BR">
<head>
	<meta charset="utf-8"/>
	<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover"/>
	<meta name="theme-color" content="#060608"/>
	<title><?php $handler->websiteTitle(); ?></title>
	<meta name="generator" content="WebEngine <?php echo __WEBENGINE_VERSION__; ?>"/>
	<meta name="description" content="<?php config('website_meta_description'); ?>"/>
	<meta name="keywords" content="<?php config('website_meta_keywords'); ?>"/>
	<meta property="og:type" content="website"/>
	<meta property="og:title" content="<?php $handler->websiteTitle(); ?>"/>
	<meta property="og:description" content="<?php config('website_meta_description'); ?>"/>
	<meta property="og:url" content="<?php echo __BASE_URL__; ?>"/>
	<link rel="shortcut icon" href="<?php echo __BASE_URL__; ?>templates/default/favicon.ico"/>
	<link rel="preconnect" href="https://fonts.googleapis.com"/>
	<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
	<link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@600;700;800&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet"/>
	<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@3.4.1/dist/css/bootstrap.min.css" integrity="sha384-HSMxcRTRxnN+Bdg0JdbxYKrThecOKuH5zCYotlSAcp1+c8xmyTe9GYg1l9a69psu" crossorigin="anonymous"/>
	<link href="<?php echo __BASE_URL__; ?>templates/default/css/profiles.css" rel="stylesheet"/>
	<link href="<?php echo __BASE_URL__; ?>templates/default/css/castle-siege.css" rel="stylesheet"/>
	<link href="<?php echo __PATH_TEMPLATE_CSS__; ?>muchila.css?v=1" rel="stylesheet"/>
	<script>var baseUrl = '<?php echo __BASE_URL__; ?>';</script>
</head>
<body class="mc-page-<?php echo MuChilaUI::h($mcPagina !== '' ? $mcPagina : 'home'); ?><?php echo $_REQUEST['subpage'] !== '' ? ' mc-sub-' . MuChilaUI::h($_REQUEST['subpage']) : ''; ?>">
	<a class="mc-sr" href="#content">Pular para o conteúdo</a>

	<nav class="mc-nav" id="mcNav" aria-label="Menu principal">
		<div class="mc-container mc-nav__bar">
			<a class="mc-brand" href="<?php echo __BASE_URL__; ?>" aria-label="<?php config('server_name'); ?> - início">
				<span class="mc-brand__gem"></span>
				<span class="mc-brand__name">MU CHILA</span>
			</a>
			<ul class="mc-nav__links"><?php templateBuildNavbar(); ?></ul>
			<div class="mc-nav__right">
				<?php if($mcLogado) { ?>
					<a class="mc-chip-cash" href="<?php echo __BASE_URL__; ?>usercp/loja#cash" title="Seu saldo de Cash. Clique para recarregar.">
						<?php echo MuChilaUI::icone('coin'); ?> <span><?php echo number_format($mcCash, 0, ',', '.'); ?></span>
						<span class="mc-chip-cash__plus"><?php echo MuChilaUI::icone('plus'); ?></span>
					</a>
					<div class="mc-user" data-mc-menu>
						<button type="button" class="mc-user__btn" aria-haspopup="true" aria-expanded="false">
							<span class="mc-user__avatar"><?php echo MuChilaUI::h(substr($mcConta, 0, 1)); ?></span>
							<span><?php echo MuChilaUI::h($mcConta); ?></span><?php echo MuChilaUI::icone('chevron-down'); ?>
						</button>
						<div class="mc-menu" role="menu"><?php templateUserMenu(); ?></div>
					</div>
				<?php } else { ?>
					<a class="mc-btn mc-btn--ghost mc-btn--sm" href="<?php echo __BASE_URL__; ?>login/"><?php echo MuChilaUI::icone('login'); ?> <?php echo lang('menu_txt_4'); ?></a>
					<a class="mc-btn mc-btn--gold mc-btn--sm" href="<?php echo __BASE_URL__; ?>register/"><?php echo lang('menu_txt_3'); ?></a>
				<?php } ?>
			</div>
			<button type="button" class="mc-nav__toggle" aria-label="Abrir o menu" aria-expanded="false" data-mc-toggle><?php echo MuChilaUI::icone('menu'); ?></button>
		</div>
		<div class="mc-drawer">
			<?php if($mcLogado) echo MuChilaUI::carteira($mcCash, '', true); ?>
			<ul class="mc-nav__links" style="display:block;margin:12px 0 0;padding:0"><?php templateBuildNavbar(); ?></ul>
			<div class="mc-drawer__actions">
				<?php if($mcLogado) { ?>
					<a class="mc-btn" href="<?php echo __BASE_URL__; ?>usercp/"><?php echo MuChilaUI::icone('user'); ?> <?php echo lang('module_titles_txt_3'); ?></a>
					<a class="mc-btn mc-btn--ghost" href="<?php echo __BASE_URL__; ?>logout/"><?php echo MuChilaUI::icone('logout'); ?> <?php echo lang('menu_txt_6'); ?></a>
				<?php } else { ?>
					<a class="mc-btn mc-btn--gold" href="<?php echo __BASE_URL__; ?>register/"><?php echo lang('menu_txt_3'); ?></a>
					<a class="mc-btn" href="<?php echo __BASE_URL__; ?>login/"><?php echo MuChilaUI::icone('login'); ?> <?php echo lang('menu_txt_4'); ?></a>
				<?php } ?>
			</div>
		</div>
	</nav>

	<?php if($mcInicio) { ?>
	<section class="mc-hero">
		<div class="mc-hero__art" aria-hidden="true"></div>
		<div class="mc-container mc-hero__inner">
			<div class="mc-reveal">
				<span class="mc-eyebrow"><?php echo MuChilaUI::icone('sparkle'); ?> <?php echo MuChilaUI::h(config('server_info_season', true) ?: 'Season 14'); ?> · Servidor brasileiro</span>
				<h1>MU CHILA</h1>
				<p class="mc-hero__lead">O continente de MU espera por você. Reúna sua party, suba de nível e escreva seu nome na história.</p>
				<div class="mc-hero__cta">
					<a class="mc-btn mc-btn--gold mc-btn--lg mc-btn--shine" href="<?php echo __BASE_URL__; ?>downloads/"><?php echo MuChilaUI::icone('download'); ?> Começar a jogar</a>
					<?php if($mcLogado) { ?>
						<a class="mc-btn mc-btn--lg" href="<?php echo __BASE_URL__; ?>usercp/lojaitens"><?php echo MuChilaUI::icone('chest'); ?> Loja de itens</a>
					<?php } else { ?>
						<a class="mc-btn mc-btn--lg" href="<?php echo __BASE_URL__; ?>register/"><?php echo MuChilaUI::icone('user'); ?> Criar conta grátis</a>
					<?php } ?>
				</div>
			</div>
			<aside class="mc-status mc-reveal" style="animation-delay:.12s" aria-label="Situação do servidor">
				<div class="mc-status__head">
					<span class="mc-status__title">Situação do servidor</span>
					<span class="mc-live">Online</span>
				</div>
				<div class="mc-status__online"><?php echo number_format($onlinePlayers, 0, ',', '.'); ?> <small>jogadores agora<?php echo check_value($maxOnline) ? ' de ' . number_format((int)$maxOnline, 0, ',', '.') : ''; ?></small></div>
				<div class="mc-bar"><span style="width:<?php echo max(2, round($onlinePlayersPercent)); ?>%"></span></div>
				<div class="mc-status__grid">
					<div class="mc-status__cell"><span><?php echo lang('server_time'); ?></span><strong id="tServerTime">--:--:--</strong> <small id="tServerDate"></small></div>
					<div class="mc-status__cell"><span><?php echo lang('user_time'); ?></span><strong id="tLocalTime">--:--:--</strong> <small id="tLocalDate"></small></div>
					<?php if(isset($srvInfo) && is_array($srvInfo)) { ?>
					<div class="mc-status__cell"><span><?php echo lang('sidebar_srvinfo_txt_2'); ?></span><strong><?php echo number_format((int)$srvInfo[0], 0, ',', '.'); ?></strong></div>
					<div class="mc-status__cell"><span><?php echo lang('sidebar_srvinfo_txt_3'); ?></span><strong><?php echo number_format((int)$srvInfo[1], 0, ',', '.'); ?></strong></div>
					<?php } ?>
				</div>
			</aside>
		</div>
	</section>
	<?php } ?>

	<main id="content" class="mc-main">
		<div class="mc-container">
			<?php if($mcComMenu) { ?>
			<div class="mc-layout">
				<div class="mc-content"><?php $handler->loadModule($_REQUEST['page'], $_REQUEST['subpage']); ?></div>
				<aside class="mc-sidebar"><?php include(__PATH_TEMPLATE_ROOT__ . 'inc/modules/sidebar.php'); ?></aside>
			</div>
			<?php } else { ?>
			<div class="mc-content"><?php $handler->loadModule($_REQUEST['page'], $_REQUEST['subpage']); ?></div>
			<?php } ?>
		</div>
	</main>

	<footer class="mc-footer">
		<?php include(__PATH_TEMPLATE_ROOT__ . 'inc/modules/footer.php'); ?>
	</footer>
	<div class="mc-toasts" id="mcToasts" aria-live="polite"></div>

	<script src="https://ajax.googleapis.com/ajax/libs/jquery/2.2.4/jquery.min.js"></script>
	<script src="https://cdn.jsdelivr.net/npm/bootstrap@3.4.1/dist/js/bootstrap.min.js" integrity="sha384-aJ21OjlMXNL5UyIl/XNwTMqvzeRMZH2w8c5cRVpzpU8Y5bApTppSuUkhZXN0VxHd" crossorigin="anonymous"></script>
	<script src="<?php echo __PATH_TEMPLATE_JS__; ?>muchila.js?v=1"></script>
</body>
</html>

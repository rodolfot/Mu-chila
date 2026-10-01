<?php
/**
 * Mu Chila - coluna lateral das páginas do jogador (usercp/*): carteira, menu do jogador e o castelo.
 */
if(!isLoggedIn()) {
	echo '<div class="mc-card"><div class="mc-card__head"><h3 class="mc-card__title">' . MuChilaUI::icone('login') . lang('module_titles_txt_2') . '</h3></div>';
	echo '<form class="mc-login" action="' . __BASE_URL__ . 'login" method="post">';
	echo '<input type="text" class="mc-input" name="webengineLogin_user" placeholder="Conta" autocomplete="username" required>';
	echo '<input type="password" class="mc-input" name="webengineLogin_pwd" placeholder="Senha" autocomplete="current-password" required>';
	echo '<button type="submit" name="webengineLogin_submit" value="submit" class="mc-btn mc-btn--gold mc-btn--block">' . lang('login_txt_3') . '</button>';
	echo '</form></div>';
	return;
}

echo MuChilaUI::carteira(MuChilaUI::cashDaConta((string)$_SESSION['username']), 'Conta <strong>' . MuChilaUI::h($_SESSION['username']) . '</strong>');
echo '<div class="mc-card mc-card--flush"><div class="mc-card__head" style="padding:16px 18px 0;margin:0 0 6px"><h3 class="mc-card__title">' . MuChilaUI::icone('user') . 'Menu do jogador</h3>'
	. '<a class="mc-card__link" href="' . __BASE_URL__ . 'logout">' . MuChilaUI::icone('logout') . 'Sair</a></div>';
templateBuildUsercp();
echo '</div>';
templateCastleSiegeWidget();

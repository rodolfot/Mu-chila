<?php
/**
 * Mu Chila - Downloads (01/10/2026; troca o do WebEngine): só o launcher, que instala, atualiza e confere o jogo.
 * O endereço e o tamanho vêm do cadastro de downloads do WebEngine (downloads.cache, refeito pelo Instalar-Modulos.ps1, que
 * deixa só o launcher). O site só abre pela rede do Radmin, então quem está aqui já pode jogar.
 */
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaUI.php');
$ui = 'MuChilaUI';

$launcher = null;
foreach((function_exists('loadCache') ? loadCache('downloads.cache') : null) ?: [] as $d)
	if(is_array($d) && stripos((string)($d['download_link'] ?? ''), 'MuChilaLauncher') !== false) { $launcher = $d; break; }
$link = $launcher['download_link'] ?? '/arquivos/MuChilaLauncher.exe';
$tamanho = isset($launcher['download_size']) ? number_format((float)$launcher['download_size'], 1, ',', '.') . ' MB' : '';

echo $ui::titulo('Downloads', 'Tudo o que você precisa para jogar é o launcher do Mu Chila.', 'download');
echo '<section class="mc-download">';
echo '<div class="mc-download__icon">' . $ui::icone('download') . '</div>';
echo '<div class="mc-download__info"><span class="mc-eyebrow">' . $ui::icone('sparkle') . 'Windows 10 e 11</span><h2>Launcher Mu Chila</h2>'
	. '<p>Instala o jogo, mantém tudo atualizado sozinho e confere se o computador tem o que precisa (Visual C++ e DirectX). Se faltar algo, ele mesmo instala.</p></div>';
echo '<div class="mc-download__cta"><a class="mc-btn mc-btn--gold mc-btn--lg mc-btn--shine" href="' . MuChilaUI::h($link) . '">' . $ui::icone('download') . 'Baixar o launcher</a>'
	. ($tamanho !== '' ? '<small>' . $tamanho . ' · o jogo (cerca de 1,3 GB) é baixado pelo launcher</small>' : '') . '</div>';
echo '</section>';

echo '<h2 class="mc-section-title">' . $ui::icone('sparkle') . 'Como jogar</h2><ol class="mc-steps-list">';
foreach([
	['download', 'Baixe e abra o launcher', 'Se o Windows avisar sobre o arquivo, clique em "Mais informações" e "Executar assim mesmo".'],
	['chest', 'Escolha onde instalar', 'Use uma pasta simples, como <strong>C:\Jogos\Mu Chila</strong>. O launcher baixa o jogo e cria o atalho "Mu Chila" na área de trabalho.'],
	['zap', 'Deixe ele preparar o computador', 'Se faltar o Visual C++ ou o DirectX, o launcher avisa e instala (o Windows pede permissão).'],
	['user', 'Crie a conta e entre', 'Crie a sua conta aqui no site, clique em <strong>JOGAR</strong> no launcher e entre com ela.'],
] as $i => [$icone, $titulo, $texto])
	echo '<li class="mc-step"><span class="mc-step__num">' . ($i + 1) . '</span><span class="mc-step__icon">' . $ui::icone($icone) . '</span><div><strong>' . $titulo . '</strong><p>' . $texto . '</p></div></li>';
echo '</ol>';

echo $ui::aviso('info', 'Rede do Radmin VPN', 'O servidor fica na rede do Radmin VPN, a mesma pela qual este site abriu. Deixe o Radmin ligado e conectado à rede do Mu Chila enquanto joga.', 'server');
echo $ui::aviso('atencao', 'O jogo não abre?', 'No launcher, clique em <strong>DIAGNÓSTICO</strong>: ele confere o computador, mostra o que falta e salva um relatório para mandar à administração pela página <a href="' . __BASE_URL__ . 'contact/">Contate-nos</a>.', 'alert');

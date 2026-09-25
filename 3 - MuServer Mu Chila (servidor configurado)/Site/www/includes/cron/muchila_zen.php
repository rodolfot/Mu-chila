<?php
/**
 * Mu Chila - tarefa do agendador (a cada minuto): entrega no baú o Zen de bônus dos VIPs comprados enquanto a conta
 * estava no jogo. O Zen só entra com a conta fora do jogo, porque o servidor regrava o baú aberto quando ele é fechado.
 * Registrada na WEBENGINE_CRON pelo muchila\Instalar-Modulos.ps1.
 */

$file_name = basename(__FILE__);
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLoja.php');

try {
	$loja = new MuChilaLoja();
	foreach($loja->entregarZenPendente() as $linha) {
		@file_put_contents(MuChilaLoja::pastaSite() . '/logs/loja.log', date('Y-m-d H:i:s') . ' ' . $linha . PHP_EOL, FILE_APPEND);
	}
} catch(Exception $ex) {
	@file_put_contents(MuChilaLoja::pastaSite() . '/logs/loja.log', date('Y-m-d H:i:s') . ' erro no Zen pendente: ' . $ex->getMessage() . PHP_EOL, FILE_APPEND);
}

updateCronLastRun($file_name);

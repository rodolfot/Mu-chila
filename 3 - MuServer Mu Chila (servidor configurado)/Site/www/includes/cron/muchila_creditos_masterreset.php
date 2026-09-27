<?php
/**
 * Mu Chila - tarefa do agendador (a cada minuto): credita os Master Resets feitos NO JOGO que ainda nao
 * foram pagos. Os Master Resets feitos pelo site ja saem creditados e marcados como pagos, entao nao
 * duplicam. Valor por reset vem de includes/config/muchila.resets.json (master_creditos).
 * Registrada na WEBENGINE_CRON pelo muchila\Instalar-Modulos.ps1.
 */
$file_name = basename(__FILE__);
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaResets.php');

try {
	(new MuChilaResets())->creditarMasterResets();
} catch(Exception $ex) {
	@file_put_contents(MuChilaLoja::pastaSite() . '/logs/loja.log', date('Y-m-d H:i:s') . ' erro no credito de master reset: ' . $ex->getMessage() . PHP_EOL, FILE_APPEND);
}

updateCronLastRun($file_name);

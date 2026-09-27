<?php
/**
 * Mu Chila - tarefa do agendador (a cada minuto): expira PIX vencidos do mercado (o anúncio volta à venda) e entrega
 * o que já foi pago quando o comprador estava no jogo, sem espaço no baú ou sem vaga de personagem.
 * Registrada na WEBENGINE_CRON pelo muchila\Instalar-Modulos.ps1.
 */
$file_name = basename(__FILE__);
require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMercado.php');
try {
	foreach((new MuChilaMercado())->entregarPendentes() as $linha)
		if(!str_contains($linha, 'aguardando')) @file_put_contents(MuChilaLoja::pastaSite() . '/logs/mercado.log', date('Y-m-d H:i:s') . ' ' . $linha . PHP_EOL, FILE_APPEND);
} catch(Exception $ex) {
	@file_put_contents(MuChilaLoja::pastaSite() . '/logs/mercado.log', date('Y-m-d H:i:s') . ' erro na tarefa do mercado: ' . $ex->getMessage() . PHP_EOL, FILE_APPEND);
}
updateCronLastRun($file_name);

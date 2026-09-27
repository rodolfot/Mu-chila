<?php
/**
 * Mu Chila - volta da autorização do vendedor no Mercado Pago (OAuth). O Mercado Pago redireciona para cá com
 * ?code=...&state=...; o state liga o retorno à conta do jogo que pediu. Os tokens são guardados criptografados.
 */
define('access', 'api');
$log = function($msg) { @file_put_contents(dirname(__DIR__, 2) . '/logs/mercado.log', date('Y-m-d H:i:s') . ' ' . $msg . PHP_EOL, FILE_APPEND); };
$destino = '/usercp/mercado?aba=conta';
try {
	if(!@include_once(rtrim(str_replace('\\','/', dirname(__DIR__)), '/') . '/includes/webengine.php')) throw new Exception('Could not load WebEngine CMS.');
	require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMercado.php');
	$destino = __BASE_URL__ . 'usercp/mercado?aba=conta';
	$state = (string)($_GET['state'] ?? '');
	$code = (string)($_GET['code'] ?? '');
	if(!preg_match('/^[a-f0-9]{40}$/', $state) || $code === '') throw new Exception('Retorno do Mercado Pago sem autorização (você pode ter cancelado).');
	$conta = (new MuChilaMercado())->concluirAutorizacao($state, $code);
	$log("vendedor $conta ligou a conta do Mercado Pago");
	$_SESSION['muchila_msg'] = 'Conta do Mercado Pago ligada! Agora você pode anunciar no mercado.';
} catch(Exception $ex) {
	$log('oauth: ' . $ex->getMessage());
	$_SESSION['muchila_msg'] = 'Não foi possível ligar a conta do Mercado Pago: ' . $ex->getMessage();
}
header('Location: ' . $destino);

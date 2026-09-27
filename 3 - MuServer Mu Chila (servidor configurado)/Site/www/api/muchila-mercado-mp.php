<?php
/**
 * Mu Chila - aviso de pagamento do Mercado Pago para o MERCADO entre jogadores (webhook).
 * Inativo (404) enquanto o provedor do mercado não for "mercadopago" (usercp.mercado.xml).
 * Confere a assinatura (x-signature) e o mercado CONSULTA o pagamento (com o token do vendedor) antes de entregar.
 */
define('access', 'api');
$log = function($msg) { @file_put_contents(dirname(__DIR__, 2) . '/logs/mercado.log', date('Y-m-d H:i:s') . ' ' . $msg . PHP_EOL, FILE_APPEND); };
try {
	if(!@include_once(rtrim(str_replace('\\','/', dirname(__DIR__)), '/') . '/includes/webengine.php')) throw new Exception('Could not load WebEngine CMS.');
	require_once(__PATH_INCLUDES__ . 'muchila/MuChilaMercado.php');
	$m = new MuChilaMercado();
	if($m->modoTeste()) { http_response_code(404); exit; }
	$corpo = json_decode((string)file_get_contents('php://input'), true) ?: [];
	$tipo = (string)($_GET['type'] ?? $corpo['type'] ?? '');
	$dataId = (string)($_GET['data_id'] ?? $corpo['data']['id'] ?? '');
	if($tipo !== 'payment' || $dataId === '' || !preg_match('/^[A-Za-z0-9_-]{1,64}$/', $dataId)) { http_response_code(200); echo 'ignorado'; exit; }
	if(!$m->assinaturaValida((string)($_SERVER['HTTP_X_SIGNATURE'] ?? ''), (string)($_SERVER['HTTP_X_REQUEST_ID'] ?? ''), $dataId)) {
		$log("assinatura invalida para o pagamento $dataId (ip " . ($_SERVER['REMOTE_ADDR'] ?? '?') . ")");
		http_response_code(401); exit;
	}
	$log($m->processarAviso($dataId, 'webhook'));
	http_response_code(200); echo 'ok';
} catch(Exception $ex) {
	$log('erro: ' . $ex->getMessage());
	http_response_code(500);   // o Mercado Pago tenta de novo mais tarde
}

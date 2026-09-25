<?php
/**
 * Mu Chila - aviso de pagamento do Mercado Pago (webhook).
 * Inativo (404) enquanto o provedor da loja não for "mercadopago" (usercp.loja.xml).
 * O aviso só diz "o pagamento X mudou": aqui se confere a assinatura (x-signature) e a loja CONSULTA o pagamento
 * na API antes de entregar; o mesmo pagamento avisado várias vezes é entregue uma vez só.
 */

define('access', 'api');

$log = function($msg) {
	@file_put_contents(dirname(__DIR__, 2) . '/logs/mercadopago.log', date('Y-m-d H:i:s') . ' ' . $msg . PHP_EOL, FILE_APPEND);
};

try {
	if(!@include_once(rtrim(str_replace('\\','/', dirname(__DIR__)), '/') . '/includes/webengine.php')) throw new Exception('Could not load WebEngine CMS.');
	require_once(__PATH_INCLUDES__ . 'muchila/MuChilaLoja.php');

	$loja = new MuChilaLoja();
	if($loja->cfg['provedor'] !== 'mercadopago') { http_response_code(404); exit; }

	$corpo = json_decode((string)file_get_contents('php://input'), true) ?: [];
	$tipo = (string)($_GET['type'] ?? $corpo['type'] ?? '');
	// "data.id" na URL chega ao PHP como data_id; é o valor usado na assinatura
	$dataId = (string)($_GET['data_id'] ?? $corpo['data']['id'] ?? '');
	if($tipo !== 'payment' || $dataId === '' || !preg_match('/^[A-Za-z0-9_-]{1,64}$/', $dataId)) { http_response_code(200); echo 'ignorado'; exit; }

	$prov = $loja->provedor();
	if(!$prov->assinaturaValida((string)($_SERVER['HTTP_X_SIGNATURE'] ?? ''), (string)($_SERVER['HTTP_X_REQUEST_ID'] ?? ''), $dataId)) {
		$log("assinatura invalida para o pagamento $dataId (ip " . ($_SERVER['REMOTE_ADDR'] ?? '?') . ")");
		http_response_code(401); exit;
	}

	$resultado = $loja->processarAviso($dataId, 'webhook');
	$log($resultado);
	http_response_code(200);
	echo 'ok';
} catch(Exception $ex) {
	$log('erro: ' . $ex->getMessage());
	http_response_code(500);   // o Mercado Pago tenta de novo mais tarde
}

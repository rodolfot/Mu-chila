<?php
/**
 * Mu Chila - foto de um item para a loja de itens: api/muchila-item-imagem.php?s=<seção>&t=<tipo>
 * As fotos são as do MuEditor do kit (C:\MuServer\1 - MuEditor\Item\<tipo>-<seção>.jpg, 60x60). Sem foto: 404
 * (a página mostra o ícone da categoria no lugar).
 */
$s = filter_input(INPUT_GET, 's', FILTER_VALIDATE_INT, ['options' => ['min_range' => 0, 'max_range' => 20]]);
$t = filter_input(INPUT_GET, 't', FILTER_VALIDATE_INT, ['options' => ['min_range' => 0, 'max_range' => 511]]);
$arquivo = ($s === null || $s === false || $t === null || $t === false) ? null : dirname(__DIR__, 3) . "/1 - MuEditor/Item/$t-$s.jpg";
if ($arquivo === null || !is_file($arquivo)) { http_response_code(404); exit; }
header('Content-Type: image/jpeg');
header('Cache-Control: public, max-age=604800');
header('Content-Length: ' . filesize($arquivo));
readfile($arquivo);

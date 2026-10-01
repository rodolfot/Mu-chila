<?php
// Teste das partes do site de 01/10/2026: quantidade dos empilháveis (Mercado), Comprar Zen, árvore master, páginas de texto,
// Contate-nos e o filtro de erros da issue #38.
//   php teste-paginas-site.php            sem banco (roda em qualquer PC com o repositório)
//   php teste-paginas-site.php --banco    também a compra de Zen e o Contate-nos de verdade, com a conta descartável "zenteste"
//                                         (criada e apagada pelo teste). Rodar no PC do servidor: C:\MuServer\Site\php\php.exe ...
$www = __DIR__ . '/../www/includes/muchila/';
require $www . 'MuChilaItens.php';
require $www . 'MuChilaZen.php';
require $www . 'MuChilaMaster.php';
require $www . 'MuChilaPaginas.php';
if (!function_exists('mb_strlen')) { function mb_strlen($s) { return strlen($s); } }   // o PHP do servidor tem mbstring; este teste roda sem
if (!defined('__BASE_URL__')) define('__BASE_URL__', 'http://localhost/');
require $www . 'MuChilaUI.php';

$ok = 0; $falhas = 0;
function confere(string $desc, bool $cond, string $extra = '') { global $ok, $falhas; $cond ? $ok++ : $falhas++; echo ($cond ? 'OK    ' : 'FALHA ') . $desc . ($extra !== '' ? "  [$extra]" : '') . PHP_EOL; }
function falha(callable $f): string { try { $f(); return ''; } catch (Throwable $e) { return $e->getMessage(); } }

// ------------------------------------------------------------------ empilháveis (ItemStack.txt)
$itens = new MuChilaItens(dirname(__DIR__, 3) . '/Data/Item/Item.txt');
$joia = pack('C*', 14, 0, 50, 0, 0, 0, 1, 0, 0, 14 << 4, 0, 255, 255, 255, 255, 255);   // Jewel of Soul (14,14) com durabilidade 50
$d = $itens->decodificar($joia);
confere('Jewel of Soul com 50 na pilha: quantidade 50', $d && $d['nome'] === 'Jewel of Soul' && $d['empilha'] && $d['quantidade'] === 50, json_encode(['nome' => $d['nome'] ?? '', 'qtd' => $d['quantidade'] ?? 0]));
confere('descrição mostra a quantidade', MuChilaItens::descrever($d) === 'Jewel of Soul x50', MuChilaItens::descrever($d));
$helm = pack('C*', 1, 15 << 3, 68, 0, 0, 0, 1, 0, 0, 7 << 4, 0, 255, 255, 255, 255, 255);   // Dragon Helm +15, durabilidade 68
$d = $itens->decodificar($helm);
confere('item que não empilha: quantidade 1 (durabilidade continua durabilidade)', $d && !$d['empilha'] && $d['quantidade'] === 1 && $d['durabilidade'] === 68 && MuChilaItens::descrever($d) === 'Dragon Helm +15');

// ------------------------------------------------------------------ Comprar Zen
$cfg = json_decode(file_get_contents(__DIR__ . '/../www/includes/config/muchila.zen.json'), true);
$zen = new MuChilaZen(null, $cfg);
confere('pacotes do muchila.zen.json carregados em ordem de Zen', count($zen->pacotes()) >= 2 && array_values($zen->pacotes())[0]['zen'] <= array_values($zen->pacotes())[1]['zen'], implode(', ', array_keys($zen->pacotes())));
$ruim = MuChilaZen::normalizar(['ativo' => true, 'pacotes' => [['id' => 'ok-1', 'zen' => 10, 'cash' => 1], ['id' => 'Com Espaço', 'zen' => 10, 'cash' => 1],
    ['id' => 'demais', 'zen' => 3000000000, 'cash' => 1], ['id' => 'gratis', 'zen' => 10, 'cash' => 0], ['id' => 'off', 'zen' => 10, 'cash' => 1, 'ativo' => false]]]);
confere('pacote inválido (id com espaço, acima de 2 bi, sem preço, desligado) fica de fora', array_keys($ruim['pacotes']) === ['ok-1'], implode(',', array_keys($ruim['pacotes'])));
confere('loja de Zen sem pacote fica fechada', !(new MuChilaZen(null, ['ativo' => true, 'pacotes' => []]))->ativa());
confere('Zen por extenso', MuChilaZen::zenTexto(50000000) === '50 milhões' && MuChilaZen::zenTexto(1000000) === '1 milhão'
    && MuChilaZen::zenTexto(1500000000) === '1,5 bilhão' && MuChilaZen::zenTexto(2000000000) === '2 bilhões', MuChilaZen::zenTexto(1500000000));
$primeiro = array_values($zen->pacotes())[0];
confere('preço diferente do mostrado é recusado antes de qualquer coisa', str_contains(falha(function() use ($zen, $primeiro) { $zen->comprar('ninguem', $primeiro['id'], $primeiro['cash'] + 1, '127.0.0.1'); }), 'mudou'));

// ------------------------------------------------------------------ árvore master
confere('árvore: conta só as posições aprendidas', MuChilaMaster::aprendidas('2C0114FF00FF4A0A01FF00FF') === 2 && MuChilaMaster::aprendidas('') === 0 && MuChilaMaster::aprendidas(null) === 0);

// ------------------------------------------------------------------ páginas de texto e Contate-nos
foreach (['tos', 'privacy', 'refunds'] as $id) {
    $p = MuChilaPaginas::pagina($id);
    confere("página $id tem título e texto (sem o Lorem ipsum)", $p && $p['titulo'] !== '' && strlen($p['html']) > 500 && stripos($p['html'], 'lorem') === false, $p['titulo'] ?? '');
}
$c = MuChilaPaginas::contato();
confere('Contate-nos: canais vazios não aparecem; formulário ligado', $c['formulario'] && $c['canais'] === [] && $c['texto'] !== '');
$contato = new MuChilaContato();
confere('mensagem curta é recusada sem ir ao banco', str_contains(falha(function() use ($contato) { $contato->enviar('alguem', '', 'Oi', 'curta', '127.0.0.1'); }), 'assunto'));
confere('visitante sem contato é recusado', str_contains(falha(function() use ($contato) { $contato->enviar(null, '', 'Pedido', 'mensagem com tamanho suficiente', '127.0.0.1'); }), 'falar com você'));

// ------------------------------------------------------------------ issue #38: erro do banco nunca vai para a tela
$log = ini_set('error_log', sys_get_temp_dir() . '/muchila-teste-erro.log');
$pdo = new PDOException("SQLSTATE[42000]: A permissão EXECUTE foi negada no objeto 'WZ_GetItemSerial'");
confere('PDOException vira texto genérico', strpos(MuChilaUI::erro($pdo), 'SQLSTATE') === false && strpos(MuChilaUI::erro($pdo), 'WZ_') === false, MuChilaUI::erro($pdo));
confere('mensagem do Mu Chila passa como está', MuChilaUI::erro(new Exception('Cash insuficiente.')) === 'Cash insuficiente.');
confere('erro do PHP (TypeError) também vira texto genérico', strpos(MuChilaUI::erro(new TypeError('Argument #1 must be of type int')), 'Argument') === false);
ini_set('error_log', $log ?: '');

// ------------------------------------------------------------------ confirmação com apóstrofo no nome do item
$attr = MuChilaUI::confirma("Comprar Gladiator's Dagger por R$ 10,00?");
preg_match('/^ onclick="([^"]*)"$/', $attr, $m);
confere('confirmação: nome com apóstrofo vira JavaScript válido', isset($m[1]) && html_entity_decode($m[1], ENT_QUOTES, 'UTF-8') === 'return confirm("Comprar Gladiator\'s Dagger por R$ 10,00?")', $attr);

// ------------------------------------------------------------------ banco (só no servidor)
if (in_array('--banco', $argv, true)) {
    $db = MuChilaLoja::conectar();
    $conta = 'zenteste';
    // nunca mexe numa conta de verdade com o mesmo nome: a do teste é a que tem o e-mail teste@exemplo.com
    $s = $db->prepare("SELECT mail_addr FROM MEMB_INFO WHERE memb___id = ?");
    $s->execute([$conta]);
    $mail = $s->fetchColumn();
    if ($mail !== false && trim((string)$mail) !== 'teste@exemplo.com') {
        echo PHP_EOL . "PARADO: já existe uma conta \"$conta\" que não é deste teste; nada foi alterado." . PHP_EOL;
        exit(1);
    }
    $limpar = function() use ($db, $conta) {
        foreach (["DELETE FROM MUCHILA_ZEN_COMPRAS WHERE conta = ?", "DELETE FROM MUCHILA_CONTATO WHERE conta = ?", "DELETE FROM warehouse WHERE AccountID = ?",
                  "DELETE FROM CashShopData WHERE AccountID = ?", "DELETE FROM MEMB_STAT WHERE memb___id = ?", "DELETE FROM MEMB_INFO WHERE memb___id = ?"] as $sql)
            $db->prepare($sql)->execute([$conta]);
    };
    $limpar();
    $db->prepare("INSERT INTO MEMB_INFO (memb___id, memb__pwd, memb_name, sno__numb, mail_addr, bloc_code, ctl1_code, appl_days) VALUES (?, 'teste1', 'teste', '1', 'teste@exemplo.com', '0', '0', GETDATE())")->execute([$conta]);
    $db->prepare("INSERT INTO CashShopData (AccountID, WCoinC, WCoinP, GoblinPoint) VALUES (?, 1000, 0, 0)")->execute([$conta]);
    $z = new MuChilaZen($db, ['ativo' => true, 'pacotes' => [['id' => 'teste-1kkk', 'zen' => 1000000000, 'cash' => 100]]]);
    try {
        $r = $z->comprar($conta, 'teste-1kkk', 100, '127.0.0.1');
        confere('compra de Zen: baú criado com 1 bilhão e Cash debitado', $z->zenNoBau($conta) === 1000000000 && $z->saldo($conta) === 900 && $r['zen_bau'] === 1000000000, json_encode($r));
        $z->comprar($conta, 'teste-1kkk', 100, '127.0.0.1');
        confere('2ª compra soma no baú (2 bilhões)', $z->zenNoBau($conta) === 2000000000 && $z->saldo($conta) === 800);
        confere('3ª compra passaria do limite: recusa sem cobrar', str_contains(falha(function() use ($z, $conta) { $z->comprar($conta, 'teste-1kkk', 100, '127.0.0.1'); }), 'limite') && $z->saldo($conta) === 800);
        $db->prepare("UPDATE warehouse SET Money = 0 WHERE AccountID = ?")->execute([$conta]);
        $db->prepare("UPDATE CashShopData SET WCoinC = 50 WHERE AccountID = ?")->execute([$conta]);
        confere('Cash insuficiente: recusa sem mexer no baú', str_contains(falha(function() use ($z, $conta) { $z->comprar($conta, 'teste-1kkk', 100, '127.0.0.1'); }), 'insuficiente') && $z->zenNoBau($conta) === 0);
        $db->prepare("UPDATE CashShopData SET WCoinC = 500 WHERE AccountID = ?")->execute([$conta]);
        $db->prepare("INSERT INTO MEMB_STAT (memb___id, ConnectStat, ServerName, IP, ConnectTM, DisConnectTM) VALUES (?, 1, 'Teste', '127.0.0.1', GETDATE(), GETDATE())")->execute([$conta]);
        confere('conta no jogo: recusa sem cobrar', str_contains(falha(function() use ($z, $conta) { $z->comprar($conta, 'teste-1kkk', 100, '127.0.0.1'); }), 'Saia do jogo') && $z->saldo($conta) === 500);
        $s = $db->prepare("SELECT COUNT(*) FROM MUCHILA_ZEN_COMPRAS WHERE conta = ?"); $s->execute([$conta]);
        confere('registro das compras (2)', (int)$s->fetchColumn() === 2);
        $ct = new MuChilaContato($db);
        $id = $ct->enviar($conta, '', 'Teste do contato', 'Mensagem de teste do Contate-nos com tamanho suficiente.', '10.0.0.99');
        confere('Contate-nos grava a mensagem da conta', $id > 0 && count($ct->daConta($conta)) === 1 && $ct->daConta($conta)[0]['status'] === 'nova');
        $ct->enviar($conta, '', 'Teste 2', 'Segunda mensagem de teste com tamanho suficiente.', '10.0.0.99');
        $ct->enviar($conta, '', 'Teste 3', 'Terceira mensagem de teste com tamanho suficiente.', '10.0.0.99');
        confere('4ª mensagem na mesma hora é recusada', str_contains(falha(function() use ($ct, $conta) { $ct->enviar($conta, '', 'Teste 4', 'Quarta mensagem de teste com tamanho suficiente.', '10.0.0.99'); }), 'várias mensagens'));
    } finally {
        $limpar();
    }
}

echo PHP_EOL . "resultado: $ok ok, $falhas falha(s)" . PHP_EOL;
exit($falhas ? 1 : 0);

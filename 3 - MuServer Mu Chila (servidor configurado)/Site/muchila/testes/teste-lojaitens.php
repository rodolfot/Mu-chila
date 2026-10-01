<?php
// Teste do núcleo da loja de itens (MuChilaLojaItens).
//   php teste-lojaitens.php            regras, preços e montagem do item (sem banco; roda em qualquer PC com o repositório)
//   php teste-lojaitens.php --banco    também a compra de verdade no banco do servidor, com a conta descartável "lojateste"
//                                      (criada e apagada pelo teste). Rodar no PC do servidor: C:\MuServer\Site\php\php.exe ...
require __DIR__ . '/../www/includes/muchila/MuChilaLojaItens.php';

$ok = 0; $falhas = 0;
function confere(string $desc, bool $cond, string $extra = '') { global $ok, $falhas; $cond ? $ok++ : $falhas++; echo ($cond ? 'OK    ' : 'FALHA ') . $desc . ($extra !== '' ? "  [$extra]" : '') . PHP_EOL; }
function falha(callable $f): string { try { $f(); return ''; } catch (Exception $e) { return $e->getMessage(); } }

$cfg = json_decode(file_get_contents(__DIR__ . '/../www/includes/config/muchila.lojaitens.json'), true);
$itemTxt = dirname(__DIR__, 3) . '/Data/Item/Item.txt';
$loja = new MuChilaLojaItens(null, $cfg, $itemTxt);
$cfg = $loja->cfg;

// ------------------------------------------------------------------ catálogo e regras
$cat = $loja->catalogo();
$total = array_sum(array_map(function($c) { return count($c['itens']); }, $cat));
confere('catálogo carregado do json + Item.txt', count($cat) >= 10 && $total > 100, count($cat) . " categorias, $total itens");
$helm = $loja->item('7-1');
confere('Dragon Helm: defesa, sem skill, com sorte, adicional de 4 em 4', $helm && $helm['nome'] === 'Dragon Helm' && $helm['exc'] === 'defesa' && !$helm['tem_skill']
    && $helm['sorte'] && $helm['adicional_passo'] === 4 && $helm['largura'] === 2 && $helm['altura'] === 2, json_encode(array_intersect_key($helm ?? [], array_flip(['nome', 'exc', 'tem_skill', 'largura', 'altura', 'durabilidade']))));
confere('excelentes de defesa na ordem do ItemOption.txt (bit 0 = Zen, bit 5 = Vida)', $helm['exc_nomes'][0] === MuChilaLojaItens::EXC_DEFESA[0] && str_contains($helm['exc_nomes'][5], 'Vida'));
$anel = $loja->item('13-8');
confere('Ring of Ice: sem sorte, adicional em % de vida, excelente de defesa, até +4', $anel && !$anel['sorte'] && $anel['adicional_passo'] === 1 && $anel['exc'] === 'defesa' && $anel['nivel_max'] === 4);
$pingente = $loja->item('13-13');
confere('Pendant of Fire: excelente de ataque', $pingente && $pingente['exc'] === 'arma');
$asa = $loja->item('12-2');
confere('Wings of Satan: sem excelente, com sorte', $asa && $asa['exc'] === null && $asa['exc_max'] === 0 && $asa['sorte']);
$armaSkill = null;
foreach ($cat as $c) foreach ($c['itens'] as $it) if ($it['secao'] <= 4 && $it['tem_skill']) { $armaSkill = $it; break 2; }
confere('existe arma com skill no catálogo', $armaSkill !== null, $armaSkill['nome'] ?? '');

// ------------------------------------------------------------------ escolha (validação)
confere('nível acima do máximo é recusado', falha(function() use ($loja, $helm) { $loja->escolha($helm, ['nivel' => 16]); }) !== '');
confere('anel +5 é recusado (máximo +4)', falha(function() use ($loja, $anel) { $loja->escolha($anel, ['nivel' => 5]); }) !== '');
confere('sorte em anel é recusada', falha(function() use ($loja, $anel) { $loja->escolha($anel, ['sorte' => 1]); }) !== '');
confere('skill em item sem skill é recusada', falha(function() use ($loja, $helm) { $loja->escolha($helm, ['skill' => 1]); }) !== '');
confere('excelente em asa é recusado', falha(function() use ($loja, $asa) { $loja->escolha($asa, ['exc' => [0]]); }) !== '');
confere('opção excelente fora de 0-5 é recusada', falha(function() use ($loja, $helm) { $loja->escolha($helm, ['exc' => [6]]); }) !== '');
confere('adicional fora de 0-7 é recusado', falha(function() use ($loja, $helm) { $loja->escolha($helm, ['adicional' => 8]); }) !== '');
$full = $loja->escolha($helm, ['nivel' => 15, 'adicional' => 7, 'sorte' => 1, 'exc' => [0, 1, 2, 3, 4, 5]]);
confere('escolha completa aceita (bits 0-5 = 63)', $full === ['nivel' => 15, 'adicional' => 7, 'sorte' => true, 'skill' => false, 'exc' => 63]);
$cfgExc3 = $cfg; $cfgExc3['exc_max'] = 3;
$loja3 = new MuChilaLojaItens(null, $cfgExc3, $itemTxt);
confere('limite de excelentes do json vale (exc_max 3 recusa 4)', falha(function() use ($loja3) { $loja3->escolha($loja3->item('7-1'), ['exc' => [0, 1, 2, 3]]); }) !== '');

// ------------------------------------------------------------------ preço
$p = $cfg['precos'];
$esperado = $helm['preco'] + $p['nivel'][15] + $p['adicional'][7] + $p['sorte'] + $p['excelente'][6];
confere('preço do Dragon Helm +15 +28 sorte 6 exc = soma da tabela', $loja->preco($helm, $full) === $esperado, "$esperado Cash");
$nada = $loja->escolha($helm, []);
confere('item sem nada custa só o preço base', $loja->preco($helm, $nada) === $helm['preco'] && count($loja->precoDetalhado($helm, $nada)) === 1);
$det = $loja->precoDetalhado($helm, $full);
confere('detalhamento tem item, nível, adicional, sorte e excelente', array_keys($det) === ['Item', 'Nível +15', 'Adicional +28', 'Sorte', 'Excelente (6)'], implode(' | ', array_keys($det)));
confere('descrição do item', MuChilaLojaItens::descrever($helm, $full) === 'Dragon Helm +15 +28, Sorte, Excelente (6)', MuChilaLojaItens::descrever($helm, $full));
confere('descrição do anel usa %', MuChilaLojaItens::descrever($anel, $loja->escolha($anel, ['nivel' => 4, 'adicional' => 3])) === 'Ring of Ice +4 +3%');
confere('tabela da tela tem os mesmos preços do servidor', $loja->tabelaParaTela()['precos'] === $cfg['precos']);

// ------------------------------------------------------------------ montagem dos 16 bytes (decodificada pelo mesmo código do Mercado)
$dec = new MuChilaItens($itemTxt);
$casos = [
    ['Dragon Helm +15 +28 sorte 6 exc', $helm, $full],
    ['Dragon Helm sem nada', $helm, $nada],
    ['adicional +12 (opção 3, sem o bit +4)', $helm, $loja->escolha($helm, ['adicional' => 3])],
    ['adicional +16 (opção 4, liga o bit +4)', $helm, $loja->escolha($helm, ['adicional' => 4, 'nivel' => 9])],
];
if ($armaSkill) $casos[] = ['arma com skill', $armaSkill, $loja->escolha($armaSkill, ['skill' => 1, 'nivel' => 13, 'exc' => [2, 5]])];
foreach ($casos as [$nome, $it, $esc]) {
    $bytes = MuChilaLojaItens::montar($it, $esc, 0x12345678);
    $d = $dec->decodificar($bytes);
    confere("montar/decodificar: $nome", strlen($bytes) === 16 && $d && $d['secao'] === $it['secao'] && $d['tipo'] === $it['tipo'] && $d['nivel'] === $esc['nivel']
        && $d['opcao'] === $esc['adicional'] && $d['sorte'] === $esc['sorte'] && $d['skill'] === $esc['skill'] && $d['excelente'] === $esc['exc']
        && $d['serial'] === 0x12345678 && $d['durabilidade'] === $it['durabilidade'] && $d['sockets'] === 0 && !$d['ancient'] && !$d['harmonia'], $d['hex'] ?? 'nulo');
}
// tipo >= 256 usa o bit 7 do byte 7
$falso = ['secao' => 12, 'tipo' => 300, 'durabilidade' => 255];
$d = $dec->decodificar(MuChilaLojaItens::montar($falso, ['nivel' => 0, 'adicional' => 0, 'sorte' => false, 'skill' => false, 'exc' => 0], 1));
confere('tipo acima de 255 (bit 8 no byte 7)', $d && $d['secao'] === 12 && $d['tipo'] === 300, $d['hex'] ?? '');

// ------------------------------------------------------------------ compra no banco (só no servidor)
if (in_array('--banco', $argv, true)) {
    $db = MuChilaLoja::conectar();
    $conta = 'lojateste';   // o nome da conta no banco do jogo tem no máximo 10 letras (MEMB_INFO.memb___id)
    // nunca mexe numa conta de verdade com o mesmo nome: a do teste é a que tem o e-mail teste@exemplo.com
    $s = $db->prepare("SELECT mail_addr FROM MEMB_INFO WHERE memb___id = ?");
    $s->execute([$conta]);
    $mail = $s->fetchColumn();
    if ($mail !== false && trim((string)$mail) !== 'teste@exemplo.com') {
        echo PHP_EOL . "PARADO: já existe uma conta \"$conta\" que não é deste teste; nada foi alterado." . PHP_EOL;
        exit(1);
    }
    $limpar = function() use ($db, $conta) {
        foreach (["DELETE FROM MUCHILA_LOJAITENS_COMPRAS WHERE conta = ?", "DELETE FROM warehouse WHERE AccountID = ?", "DELETE FROM CashShopData WHERE AccountID = ?",
                  "DELETE FROM MEMB_STAT WHERE memb___id = ?", "DELETE FROM AccountCharacter WHERE Id = ?", "DELETE FROM MEMB_INFO WHERE memb___id = ?"] as $sql)
            $db->prepare($sql)->execute([$conta]);
    };
    $limpar();
    $db->prepare("INSERT INTO MEMB_INFO (memb___id, memb__pwd, memb_name, sno__numb, mail_addr, bloc_code, ctl1_code, appl_days) VALUES (?, 'teste1', 'teste', '1', 'teste@exemplo.com', '0', '0', GETDATE())")->execute([$conta]);
    $db->prepare("INSERT INTO CashShopData (AccountID, WCoinC, WCoinP, GoblinPoint) VALUES (?, 2000, 0, 0)")->execute([$conta]);
    $lb = new MuChilaLojaItens($db, $cfg, $itemTxt);
    $saldo = function() use ($lb, $conta) { return $lb->saldo($conta); };
    $bau = function() use ($db, $conta) { $s = $db->prepare("SELECT CONVERT(varchar(7680), Items, 2) FROM warehouse WHERE AccountID = ?"); $s->execute([$conta]); $h = $s->fetchColumn(); return $h ? hex2bin($h) : null; };
    $escTela = ['nivel' => 15, 'adicional' => 7, 'sorte' => 1, 'exc' => [0, 1, 2, 3, 4, 5]];
    try {
        $preco = $lb->preco($helm, $full);
        confere('preço visto diferente é recusado sem cobrar', str_contains(falha(function() use ($lb, $conta, $escTela, $preco) { $lb->comprar($conta, '7-1', $escTela, $preco - 1, '127.0.0.1'); }), 'mudou') && $saldo() === 2000);
        $r = $lb->comprar($conta, '7-1', $escTela, $preco, '127.0.0.1');
        $b = $bau();
        $d = $b ? $dec->decodificar(substr($b, $r['posicao'] * 16, 16)) : null;
        confere('compra: Cash debitado, baú criado com o item na posição 0', $saldo() === 2000 - $preco && $r['posicao'] === 0 && $d && $d['nome'] === 'Dragon Helm'
            && $d['nivel'] === 15 && $d['opcao'] === 7 && $d['excelente'] === 63 && $d['serial'] > 0, json_encode($r));
        $s = $db->prepare("SELECT COUNT(*) FROM MUCHILA_LOJAITENS_COMPRAS WHERE conta = ? AND preco = ? AND hex = ?");
        $s->execute([$conta, $preco, $d['hex'] ?? '']);
        confere('compra registrada com o item exato', (int)$s->fetchColumn() === 1);
        $r2 = $lb->comprar($conta, '7-1', [], $helm['preco'], '127.0.0.1');
        confere('2ª compra vai para o lado (posição 2, item 2x2)', $r2['posicao'] === 2, json_encode($r2));
        $db->prepare("UPDATE CashShopData SET WCoinC = 10 WHERE AccountID = ?")->execute([$conta]);
        $antes = bin2hex($bau());
        confere('Cash insuficiente: recusa e não mexe no baú', str_contains(falha(function() use ($lb, $conta) { $lb->comprar($conta, '7-1', [], 40, '127.0.0.1'); }), 'insuficiente') && bin2hex($bau()) === $antes);
        $db->prepare("UPDATE CashShopData SET WCoinC = 5000 WHERE AccountID = ?")->execute([$conta]);
        $db->prepare("INSERT INTO MEMB_STAT (memb___id, ConnectStat, ServerName, IP, ConnectTM, DisConnectTM) VALUES (?, 1, 'Teste', '127.0.0.1', GETDATE(), GETDATE())")->execute([$conta]);
        confere('conta no jogo: recusa sem cobrar', str_contains(falha(function() use ($lb, $conta) { $lb->comprar($conta, '7-1', [], 40, '127.0.0.1'); }), 'Saia do jogo') && $saldo() === 5000);
        $db->prepare("UPDATE MEMB_STAT SET ConnectStat = 0, DisConnectTM = DATEADD(minute, -1, GETDATE()) WHERE memb___id = ?")->execute([$conta]);
        $cheio = str_repeat(MuChilaLojaItens::montar(['secao' => 14, 'tipo' => 13, 'durabilidade' => 1], $nada, 1), 240);   // 240 joias 1x1
        $db->prepare("UPDATE warehouse SET Items = CONVERT(varbinary(3840), ?, 2) WHERE AccountID = ?")->execute([bin2hex($cheio), $conta]);
        confere('baú cheio: recusa sem cobrar', str_contains(falha(function() use ($lb, $conta) { $lb->comprar($conta, '7-1', [], 40, '127.0.0.1'); }), 'espaço') && $saldo() === 5000);
        $cfgOff = $cfg; $cfgOff['ativo'] = false;
        confere('loja desativada recusa', str_contains(falha(function() use ($db, $cfgOff, $itemTxt, $conta) { (new MuChilaLojaItens($db, $cfgOff, $itemTxt))->comprar($conta, '7-1', [], 40, '127.0.0.1'); }), 'fechada'));
        confere('histórico da conta', count($lb->comprasDaConta($conta)) === 2);
    } finally {
        $limpar();
    }
}

echo PHP_EOL . "resultado: $ok ok, $falhas falha(s)" . PHP_EOL;
exit($falhas ? 1 : 0);

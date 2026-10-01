<?php
/**
 * Mu Chila - notícias do site pela linha de comando, para o Mu Chila Admin (aba Site > Notícias), 01/10/2026.
 * Mesma tabela do painel admin do WebEngine (WEBENGINE_NEWS, título e texto em base64) e o mesmo cache (News::cacheNews e
 * News::updateNewsCacheIndex), que é o que a página inicial e a página de notícias leem.
 *   php noticias-cli.php listar                  -> {"ok":true,"noticias":[{id,titulo,autor,data,comentarios}]}
 *   php noticias-cli.php obter <id>              -> {"ok":true,"noticia":{id,titulo,autor,data,comentarios,conteudo}}
 *   php noticias-cli.php salvar <arquivo.json>   -> cria (sem "id") ou altera (com "id"); {"ok":true,"id":N}
 *   php noticias-cli.php apagar <id>             -> {"ok":true}
 * O arquivo do "salvar" (UTF-8): {"id"?, "titulo", "autor", "conteudo" (HTML), "comentarios" (0/1), "data"? ("AAAA-MM-DD HH:MM")}.
 * Erro: {"ok":false,"erro":"..."} e código de saída 1. Fica em includes\ (bloqueado para o navegador) e só roda na linha de comando.
 */
if (PHP_SAPI !== 'cli') { http_response_code(404); exit; }
define('access', 'cron');
ob_start();   // o WebEngine pode imprimir avisos; a saída deste script é só o JSON
$resposta = function(array $r, int $codigo = 0) { ob_end_clean(); echo json_encode($r, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); exit($codigo); };

try {
    if (!@include_once(dirname(__DIR__) . '/webengine.php')) throw new Exception('Não consegui carregar o WebEngine.');
    require_once(__DIR__ . '/MuChilaLoja.php');
    $db = MuChilaLoja::conectar();
    $tabela = defined('WEBENGINE_NEWS') ? WEBENGINE_NEWS : 'WEBENGINE_NEWS';
    $refazerCache = function() {
        $news = new News();
        if (!$news->cacheNews() && !is_writable(__PATH_NEWS_CACHE__)) throw new Exception('A pasta do cache de notícias não pode ser gravada: ' . __PATH_NEWS_CACHE__);
        if (!$news->updateNewsCacheIndex()) throw new Exception('Não consegui atualizar o índice de notícias (includes\\cache\\news.cache).');
    };
    $acao = $argv[1] ?? '';

    if ($acao === 'listar') {
        $lista = [];
        foreach ($db->query("SELECT news_id, news_title, news_author, news_date, allow_comments FROM $tabela ORDER BY news_id DESC")->fetchAll() as $n)
            $lista[] = ['id' => (int)$n['news_id'], 'titulo' => base64_decode($n['news_title']), 'autor' => $n['news_author'],
                        'data' => date('Y-m-d H:i', (int)$n['news_date']), 'comentarios' => (int)$n['allow_comments']];
        $resposta(['ok' => true, 'noticias' => $lista]);
    }

    if ($acao === 'obter') {
        $st = $db->prepare("SELECT news_id, news_title, news_author, news_date, allow_comments, news_content FROM $tabela WHERE news_id = ?");
        $st->execute([(int)($argv[2] ?? 0)]);
        $n = $st->fetch();
        if (!$n) throw new Exception('Notícia não encontrada.');
        $resposta(['ok' => true, 'noticia' => ['id' => (int)$n['news_id'], 'titulo' => base64_decode($n['news_title']), 'autor' => $n['news_author'],
            'data' => date('Y-m-d H:i', (int)$n['news_date']), 'comentarios' => (int)$n['allow_comments'], 'conteudo' => base64_decode($n['news_content'])]]);
    }

    if ($acao === 'salvar') {
        $dados = json_decode((string)@file_get_contents($argv[2] ?? ''), true);
        if (!is_array($dados)) throw new Exception('Arquivo da notícia inválido.');
        $titulo = trim((string)($dados['titulo'] ?? ''));
        $conteudo = trim((string)($dados['conteudo'] ?? ''));
        $autor = trim((string)($dados['autor'] ?? '')) ?: 'Administração';
        $comentarios = !empty($dados['comentarios']) ? 1 : 0;
        if (strlen($titulo) < 4 || strlen($titulo) > 255) throw new Exception('O título precisa ter de 4 a 255 caracteres.');
        if (strlen($conteudo) < 4) throw new Exception('Escreva o texto da notícia.');
        $data = !empty($dados['data']) ? strtotime((string)$dados['data']) : time();
        if (!$data) throw new Exception('Data inválida (use AAAA-MM-DD HH:MM).');
        if (!empty($dados['id'])) {
            $st = $db->prepare("UPDATE $tabela SET news_title = ?, news_content = ?, news_author = ?, news_date = ?, allow_comments = ? WHERE news_id = ?");
            $st->execute([base64_encode($titulo), base64_encode($conteudo), substr($autor, 0, 50), $data, $comentarios, (int)$dados['id']]);
            if ($st->rowCount() !== 1) throw new Exception('Notícia não encontrada.');
            $id = (int)$dados['id'];
        } else {
            $st = $db->prepare("INSERT INTO $tabela (news_title, news_author, news_date, news_content, allow_comments) OUTPUT INSERTED.news_id VALUES (?, ?, ?, ?, ?)");
            $st->execute([base64_encode($titulo), substr($autor, 0, 50), $data, base64_encode($conteudo), $comentarios]);
            $id = (int)$st->fetchColumn();
        }
        $refazerCache();
        $resposta(['ok' => true, 'id' => $id]);
    }

    if ($acao === 'apagar') {
        $id = (int)($argv[2] ?? 0);
        $news = new News();
        if (!$news->removeNews($id)) throw new Exception('Notícia não encontrada.');   // apaga também as traduções
        $refazerCache();
        $resposta(['ok' => true]);
    }

    throw new Exception('Uso: php noticias-cli.php listar | obter <id> | salvar <arquivo.json> | apagar <id>');
} catch (Throwable $e) {
    $resposta(['ok' => false, 'erro' => $e->getMessage()], 1);
}

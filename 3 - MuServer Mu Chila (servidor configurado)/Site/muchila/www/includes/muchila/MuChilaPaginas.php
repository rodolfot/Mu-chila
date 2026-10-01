<?php
/**
 * Mu Chila - páginas de texto do site (Termos, Privacidade, Reembolso) e o Contate-nos (01/10/2026).
 * Textos e canais de contato: includes/config/muchila.paginas.json (aba Site > Páginas do Mu Chila Admin).
 * O servidor não tem e-mail: a mensagem do Contate-nos fica em MUCHILA_CONTATO e o dono responde pelo painel; a resposta
 * aparece para o jogador na própria página (conta logada).
 */
require_once(__DIR__ . '/MuChilaLoja.php');

class MuChilaPaginas
{
    public static function arquivo(): string { return MuChilaLoja::pastaWww() . '/includes/config/muchila.paginas.json'; }

    public static function ler(): array
    {
        $j = json_decode((string)@file_get_contents(self::arquivo()), true);
        return is_array($j) ? $j : [];
    }

    /** ['titulo', 'atualizado', 'html'] da página (tos, privacy, refunds) ou null. */
    public static function pagina(string $id): ?array
    {
        $p = self::ler()['paginas'][$id] ?? null;
        return is_array($p) && trim((string)($p['html'] ?? '')) !== '' ? $p + ['titulo' => '', 'atualizado' => ''] : null;
    }

    /** Página de texto inteira (título, data e o texto num cartão), para tos.php, privacy.php e refunds.php. */
    public static function mostrar(string $id, string $tituloPadrao, string $icone): void
    {
        require_once(__DIR__ . '/MuChilaUI.php');
        $p = self::pagina($id);
        echo MuChilaUI::titulo(($p['titulo'] ?? '') ?: $tituloPadrao, !empty($p['atualizado']) ? 'Atualizado em ' . MuChilaUI::h($p['atualizado']) : '', $icone);
        echo $p ? '<article class="mc-card mc-prose">' . $p['html'] . '</article>' : MuChilaUI::vazio('Este texto ainda não foi publicado.', $icone);
    }

    /** Texto, formulário ligado e só os canais preenchidos. */
    public static function contato(): array
    {
        $c = self::ler()['contato'] ?? [];
        $canais = array_values(array_filter($c['canais'] ?? [], function($x) {
            return is_array($x) && (trim((string)($x['texto'] ?? '')) !== '' || trim((string)($x['link'] ?? '')) !== '');
        }));
        return ['texto' => (string)($c['texto'] ?? ''), 'formulario' => ($c['formulario'] ?? true) !== false, 'canais' => $canais];
    }
}

class MuChilaContato
{
    const POR_HORA = 3;   // mensagens por hora, por conta ou por IP
    private ?PDO $db;

    public function __construct(?PDO $db = null) { $this->db = $db; }
    private function db(): PDO { return $this->db ??= MuChilaLoja::conectar(); }

    /** Grava a mensagem. Erro (Exception, texto para o jogador) se faltar algo ou passar do limite por hora. */
    public function enviar(?string $conta, string $contato, string $assunto, string $mensagem, string $ip): int
    {
        $assunto = trim($assunto); $mensagem = trim($mensagem); $contato = trim($contato);
        if (mb_strlen($assunto) < 3 || mb_strlen($assunto) > 80) throw new Exception('Escreva um assunto de 3 a 80 letras.');
        if (mb_strlen($mensagem) < 10 || mb_strlen($mensagem) > 2000) throw new Exception('A mensagem precisa ter de 10 a 2.000 letras.');
        if ($conta === null && mb_strlen($contato) < 5) throw new Exception('Diga como falar com você (e-mail, Discord ou WhatsApp), ou entre na sua conta antes de enviar.');
        if (mb_strlen($contato) > 120) throw new Exception('O contato pode ter no máximo 120 letras.');
        $st = $this->db()->prepare("SELECT COUNT(*) FROM MUCHILA_CONTATO WHERE criado > DATEADD(hour, -1, GETDATE()) AND " . ($conta !== null ? "(ip = ? OR conta = ?)" : "ip = ?"));
        $st->execute($conta !== null ? [substr($ip, 0, 45), $conta] : [substr($ip, 0, 45)]);
        if ((int)$st->fetchColumn() >= self::POR_HORA) throw new Exception('Você já mandou várias mensagens na última hora. Aguarde a resposta antes de mandar outra.');
        $st = $this->db()->prepare("INSERT INTO MUCHILA_CONTATO (conta, contato, assunto, mensagem, ip) OUTPUT INSERTED.id VALUES (?, ?, ?, ?, ?)");
        $st->execute([$conta, $contato !== '' ? $contato : null, $assunto, $mensagem, substr($ip, 0, 45)]);
        return (int)$st->fetchColumn();
    }

    /** Últimas mensagens da conta, com a resposta (se houver). */
    public function daConta(string $conta, int $limite = 10): array
    {
        $st = $this->db()->prepare("SELECT TOP (" . max(1, $limite) . ") id, criado, assunto, mensagem, status, resposta, respondido_em
            FROM MUCHILA_CONTATO WHERE conta = ? ORDER BY id DESC");
        $st->execute([$conta]);
        return $st->fetchAll();
    }
}

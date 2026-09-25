<?php
/**
 * Mu Chila - loja do site: VIP e cash pagos por PIX.
 *
 * Fluxo: o jogador escolhe um pacote -> criarPedido() grava o pedido e pede a cobrança ao provedor
 * -> o provedor avisa que houve pagamento (webhook do Mercado Pago ou, no modo de teste, o botão "Simular pagamento")
 * -> processarAviso() CONSULTA o provedor (o aviso em si não é confiável) e, se aprovado, entregar() entrega uma única vez.
 *
 * Provedores:
 *   simulado    (padrão) não fala com nenhum serviço; o pedido fica pendente até alguém simular a aprovação.
 *   mercadopago API de pagamentos PIX. Só é usado com <provedor>mercadopago</provedor> no usercp.loja.xml e o token
 *               em Site\config-local\mercadopago.json (fora do repositório).
 */

class MuChilaLoja
{
    const PENDENTE = 'pendente', ENTREGUE = 'entregue', CANCELADO = 'cancelado', EXPIRADO = 'expirado', FALHOU = 'falhou';

    public PDO $db;
    public array $cfg;
    public array $pacotes;
    private ?MuChilaProvedor $provedor = null;

    public function __construct(?PDO $db = null)
    {
        $this->db = $db ?? self::conectar();
        $this->cfg = self::lerConfig();
        $this->pacotes = self::lerPacotes();
    }

    // ------------------------------------------------------------------ configuração

    /** Pasta Site (a que tem config-local): www\includes\muchila instalado, ou muchila\www\includes\muchila no preparo. */
    public static function pastaSite(): string
    {
        for ($d = dirname(__DIR__), $i = 0; $i < 5; $i++, $d = dirname($d)) if (is_dir("$d/config-local")) return $d;
        return dirname(__DIR__, 3);
    }
    public static function pastaWww(): string { return dirname(__DIR__, 2); }

    public static function lerConfig(): array
    {
        $xml = @simplexml_load_file(self::pastaWww() . '/includes/config/modules/usercp.loja.xml');
        if ($xml === false) throw new Exception('Configuração da loja (usercp.loja.xml) não encontrada.');
        $cfg = [];
        foreach ($xml->children() as $k => $v) $cfg[$k] = trim((string)$v);
        return $cfg + ['active' => '0', 'provedor' => 'simulado', 'expira_minutos' => '30', 'max_pendentes' => '3',
                       'email_padrao' => '', 'mp_notification_url' => ''];
    }

    public static function lerPacotes(): array
    {
        $json = json_decode((string)@file_get_contents(self::pastaWww() . '/includes/config/muchila.pacotes.json'), true);
        if (!is_array($json) || !is_array($json['pacotes'] ?? null)) throw new Exception('Lista de pacotes (muchila.pacotes.json) inválida.');
        $lista = [];
        foreach ($json['pacotes'] as $p) {
            if (empty($p['ativo']) || !isset($p['id'], $p['tipo'], $p['nome'], $p['valor'])) continue;
            if ($p['tipo'] === 'vip' && (int)($p['vip_nivel'] ?? 0) >= 1 && (int)($p['vip_dias'] ?? 0) >= 1
                || $p['tipo'] === 'cash' && (int)($p['cash'] ?? 0) >= 1) $lista[$p['id']] = $p;
        }
        return $lista;
    }

    /** Conexão com o banco do jogo usando a configuração do WebEngine (ou config-local\banco.txt, nos testes por linha de comando). */
    public static function conectar(): PDO
    {
        if (function_exists('webengineConfigs')) {
            $c = webengineConfigs();
            return self::abrirPdo($c['SQL_DB_HOST'], $c['SQL_DB_PORT'] ?? '', $c['SQL_DB_NAME'], $c['SQL_DB_USER'], $c['SQL_DB_PASS']);
        }
        $c = [];
        foreach (file(self::pastaSite() . '/config-local/banco.txt') as $l)
            if (preg_match('/^(\w+)=(.*)$/', trim($l), $m)) $c[$m[1]] = $m[2];
        return self::abrirPdo($c['servidor'], $c['porta'] ?? '', $c['banco'], $c['usuario'], $c['senha']);
    }

    /** Mesma regra do class.database.php ajustado: instância nomeada (.\MUONLINE) sem porta; certificado local aceito. */
    public static function abrirPdo(string $host, string $porta, string $banco, string $usuario, string $senha): PDO
    {
        $servidor = $porta !== '' ? "$host,$porta" : $host;
        return new PDO("sqlsrv:Server=$servidor;Database=$banco;TrustServerCertificate=1", $usuario, $senha,
            [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]);
    }

    public function ativa(): bool { return $this->cfg['active'] === '1'; }
    public function modoTeste(): bool { return $this->provedor()->nome() === 'simulado'; }

    public function provedor(): MuChilaProvedor
    {
        if ($this->provedor) return $this->provedor;
        switch ($this->cfg['provedor']) {
            case 'simulado':
                return $this->provedor = new MuChilaProvedorSimulado($this->db);
            case 'mercadopago':
                $s = json_decode((string)@file_get_contents(self::pastaSite() . '/config-local/mercadopago.json'), true);
                if (empty($s['access_token'])) throw new Exception('Mercado Pago sem token (config-local\mercadopago.json).');
                return $this->provedor = new MuChilaProvedorMercadoPago($s['access_token'], $s['webhook_secret'] ?? '', $this->cfg['mp_notification_url']);
            default:
                throw new Exception('Provedor de pagamento desconhecido: ' . $this->cfg['provedor']);
        }
    }

    // ------------------------------------------------------------------ consultas

    public function conta(string $conta): array
    {
        $st = $this->db->prepare("SELECT m.memb___id AS conta, m.mail_addr AS email, m.bloc_code, m.AccountLevel AS vip_nivel,
                m.AccountExpireDate AS vip_expira, CASE WHEN m.AccountLevel > 0 AND m.AccountExpireDate > GETDATE() THEN 1 ELSE 0 END AS vip_ativo,
                ISNULL(c.WCoinC, 0) AS cash
            FROM MEMB_INFO m LEFT JOIN CashShopData c ON c.AccountID = m.memb___id WHERE m.memb___id = ?");
        $st->execute([$conta]);
        $r = $st->fetch();
        if (!$r) throw new Exception('Conta não encontrada.');
        return $r;
    }

    public function pedido(int $id, ?string $conta = null): ?array
    {
        $this->expirarVencidos();
        $sql = "SELECT * FROM MUCHILA_PEDIDOS WHERE id = ?" . ($conta !== null ? " AND conta = ?" : "");
        $st = $this->db->prepare($sql);
        $st->execute($conta !== null ? [$id, $conta] : [$id]);
        return $st->fetch() ?: null;
    }

    public function pedidosDaConta(string $conta, int $limite = 20): array
    {
        $this->expirarVencidos();
        $st = $this->db->prepare("SELECT TOP (" . max(1, $limite) . ") * FROM MUCHILA_PEDIDOS WHERE conta = ? ORDER BY id DESC");
        $st->execute([$conta]);
        return $st->fetchAll();
    }

    public function pedidos(?string $status = null, int $limite = 200): array
    {
        $this->expirarVencidos();
        $sql = "SELECT TOP (" . max(1, $limite) . ") * FROM MUCHILA_PEDIDOS" . ($status ? " WHERE status = ?" : "") . " ORDER BY id DESC";
        $st = $this->db->prepare($sql);
        $st->execute($status ? [$status] : []);
        return $st->fetchAll();
    }

    /** Pendente que passou do prazo vira "expirado" (se o pagamento chegar depois, ainda é entregue). */
    public function expirarVencidos(): void
    {
        $this->db->exec("UPDATE MUCHILA_PEDIDOS SET status = 'expirado' WHERE status = 'pendente' AND expira < GETDATE()");
    }

    // ------------------------------------------------------------------ pedido

    public function criarPedido(string $conta, string $pacoteId, string $ip): array
    {
        if (!$this->ativa()) throw new Exception('A loja está desativada.');
        $p = $this->pacotes[$pacoteId] ?? null;
        if (!$p) throw new Exception('Pacote inválido.');
        $c = $this->conta($conta);
        if ($c['bloc_code'] === '1') throw new Exception('Conta bloqueada.');
        if ($p['tipo'] === 'vip' && $c['vip_ativo'] && (int)$c['vip_nivel'] !== (int)$p['vip_nivel'])
            throw new Exception(sprintf('Você já tem VIP %d até %s. Para renovar, escolha o VIP %d; outro nível só depois que este acabar.',
                $c['vip_nivel'], self::data($c['vip_expira']), $c['vip_nivel']));

        $this->expirarVencidos();
        $st = $this->db->prepare("SELECT COUNT(*) FROM MUCHILA_PEDIDOS WHERE conta = ? AND status = 'pendente'");
        $st->execute([$conta]);
        if ((int)$st->fetchColumn() >= (int)$this->cfg['max_pendentes'])
            throw new Exception('Você já tem pedidos aguardando pagamento. Pague ou espere eles expirarem antes de criar outro.');

        $prov = $this->provedor();
        $st = $this->db->prepare("INSERT INTO MUCHILA_PEDIDOS (conta, pacote, tipo, descricao, valor, vip_nivel, vip_dias, cash, provedor, expira, ip)
            OUTPUT INSERTED.id VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, DATEADD(minute, CAST(? AS int), GETDATE()), ?)");
        $st->execute([$conta, $p['id'], $p['tipo'], $p['nome'], number_format((float)$p['valor'], 2, '.', ''),
            $p['tipo'] === 'vip' ? (int)$p['vip_nivel'] : null, $p['tipo'] === 'vip' ? (int)$p['vip_dias'] : null,
            $p['tipo'] === 'cash' ? (int)$p['cash'] : null, $prov->nome(), max(5, (int)$this->cfg['expira_minutos']), substr($ip, 0, 45)]);
        $id = (int)$st->fetchColumn();
        $pedido = $this->pedido($id);

        try {
            $email = filter_var($c['email'], FILTER_VALIDATE_EMAIL) ? $c['email'] : $this->cfg['email_padrao'];
            $cob = $prov->criarCobranca($pedido, (string)$email);
        } catch (Exception $e) {
            $this->marcar($id, self::FALHOU, 'Falha ao gerar a cobrança: ' . $e->getMessage(), [self::PENDENTE]);
            throw new Exception('Não foi possível gerar o PIX agora. Tente de novo em alguns minutos.');
        }
        $this->db->prepare("UPDATE MUCHILA_PEDIDOS SET provedor_id = ?, pix_copia_cola = ?, qr_base64 = ? WHERE id = ?")
                 ->execute([$cob['provedor_id'], $cob['pix_copia_cola'], $cob['qr_base64'] ?? null, $id]);
        return $this->pedido($id);
    }

    /**
     * Aviso de pagamento (webhook ou simulação). Não confia no aviso: consulta o provedor e só entrega se estiver
     * aprovado, com o valor e o pedido certos.
     */
    public function processarAviso(string $provedorId, string $origem): string
    {
        $prov = $this->provedor();
        $st = $this->db->prepare("SELECT * FROM MUCHILA_PEDIDOS WHERE provedor = ? AND provedor_id = ?");
        $st->execute([$prov->nome(), $provedorId]);
        $pedido = $st->fetch();
        if (!$pedido) return "pagamento $provedorId não pertence a nenhum pedido";

        $info = $prov->consultar($provedorId);
        switch ($info['status']) {
            case 'aprovado':
                if ((string)$info['referencia'] !== (string)$pedido['id']) return "pagamento $provedorId aponta para outro pedido ({$info['referencia']})";
                if (round((float)$info['valor'], 2) < round((float)$pedido['valor'], 2))
                    return $this->marcar($pedido['id'], self::FALHOU, "Pago R$ {$info['valor']}, esperado R$ {$pedido['valor']}", [self::PENDENTE, self::EXPIRADO]);
                return $this->entregar((int)$pedido['id'], $origem);
            case 'recusado':
            case 'cancelado':
                return $this->marcar($pedido['id'], self::CANCELADO, "Pagamento {$info['status']} no provedor", [self::PENDENTE, self::EXPIRADO]);
            default:
                return "pedido {$pedido['id']}: pagamento ainda {$info['status']}";
        }
    }

    /**
     * Entrega o pedido uma única vez: a troca de status e a entrega ficam na mesma transação; um segundo aviso
     * do mesmo pagamento não encontra mais o pedido em aberto e não entrega de novo.
     */
    public function entregar(int $id, string $origem): string
    {
        $this->db->beginTransaction();
        try {
            $st = $this->db->prepare("UPDATE MUCHILA_PEDIDOS SET status = 'entregue', entregue_em = GETDATE(), entregue_por = ?
                WHERE id = ? AND status IN ('pendente', 'expirado', 'falhou')");
            $st->execute([substr($origem, 0, 40), $id]);
            if ($st->rowCount() !== 1) { $this->db->rollBack(); return "pedido $id já estava entregue ou cancelado; nada feito"; }
            $p = $this->pedido($id);

            if ($p['tipo'] === 'vip') {
                $antes = $this->db->prepare("SELECT AccountLevel, AccountExpireDate FROM MEMB_INFO WITH (UPDLOCK, ROWLOCK) WHERE memb___id = ?");
                $antes->execute([$p['conta']]);
                $a = $antes->fetch();
                if (!$a) throw new Exception('conta não existe mais');
                // mesmo nível ainda ativo: soma os dias ao vencimento; senão começa agora (na atribuição, AccountLevel é o valor antigo)
                $up = $this->db->prepare("UPDATE MEMB_INFO SET AccountLevel = ?,
                        AccountExpireDate = DATEADD(day, CAST(? AS int), CASE WHEN AccountLevel = ? AND AccountExpireDate > GETDATE() THEN AccountExpireDate ELSE GETDATE() END)
                    WHERE memb___id = ?");
                $up->execute([$p['vip_nivel'], $p['vip_dias'], $p['vip_nivel'], $p['conta']]);
                $obs = sprintf('VIP antes: nível %d até %s', $a['AccountLevel'], self::data($a['AccountExpireDate']));
            } else {
                $up = $this->db->prepare("UPDATE CashShopData SET WCoinC = ISNULL(WCoinC, 0) + ? WHERE AccountID = ?");
                $up->execute([$p['cash'], $p['conta']]);
                if ($up->rowCount() === 0)
                    $this->db->prepare("INSERT INTO CashShopData (AccountID, WCoinC, WCoinP, GoblinPoint) VALUES (?, ?, 0, 0)")->execute([$p['conta'], $p['cash']]);
                $obs = "+{$p['cash']} cash";
            }
            $this->anotar($id, $obs);
            $this->db->commit();
            return "pedido $id entregue ({$p['descricao']} para {$p['conta']})";
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            return $this->marcar($id, self::FALHOU, 'Entrega falhou: ' . $e->getMessage(), [self::PENDENTE, self::EXPIRADO, self::FALHOU]);
        }
    }

    public function cancelar(int $id, string $motivo): string
    {
        return $this->marcar($id, self::CANCELADO, $motivo, [self::PENDENTE, self::EXPIRADO, self::FALHOU]);
    }

    /** Teste: aprova ou recusa o pagamento simulado e segue o mesmo caminho do aviso real. */
    public function simular(int $id, string $conta, bool $aprovar): string
    {
        if (!$this->modoTeste()) throw new Exception('Simulação só existe no modo de teste.');
        $p = $this->pedido($id, $conta);
        if (!$p || !$p['provedor_id']) throw new Exception('Pedido não encontrado.');
        $this->db->prepare("UPDATE MUCHILA_PEDIDOS SET simulado_status = ? WHERE id = ?")->execute([$aprovar ? 'aprovado' : 'recusado', $id]);
        return $this->processarAviso($p['provedor_id'], 'simulacao');
    }

    private function marcar(int $id, string $status, string $obs, array $de): string
    {
        $st = $this->db->prepare("UPDATE MUCHILA_PEDIDOS SET status = ?, obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000)
            WHERE id = ? AND status IN (" . implode(',', array_fill(0, count($de), '?')) . ")");
        $st->execute(array_merge([$status, $obs, $id], $de));
        return "pedido $id: $status ($obs)";
    }

    private function anotar(int $id, string $obs): void
    {
        $this->db->prepare("UPDATE MUCHILA_PEDIDOS SET obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000) WHERE id = ?")->execute([$obs, $id]);
    }

    // ------------------------------------------------------------------ formatação

    public static function real($v): string { return 'R$ ' . number_format((float)$v, 2, ',', '.'); }

    public static function data($d): string
    {
        if (!$d) return '-';
        $t = strtotime((string)$d);
        return $t ? date('d/m/Y H:i', $t) : (string)$d;
    }

    public static function statusTexto(string $s): string
    {
        return ['pendente' => 'Aguardando pagamento', 'entregue' => 'Pago e entregue', 'cancelado' => 'Cancelado',
                'expirado' => 'Expirado', 'falhou' => 'Com problema (fale com a administração)'][$s] ?? $s;
    }
}

// ====================================================================== provedores

interface MuChilaProvedor
{
    public function nome(): string;

    /** @return array{provedor_id:string, pix_copia_cola:string, qr_base64:?string} */
    public function criarCobranca(array $pedido, string $email): array;

    /** @return array{status:string, valor:float, referencia:string}  status: aprovado | pendente | recusado | cancelado */
    public function consultar(string $provedorId): array;
}

/** Modo de teste: não fala com nenhum serviço. O "pagamento" é a coluna simulado_status, preenchida pelos botões de teste. */
class MuChilaProvedorSimulado implements MuChilaProvedor
{
    public function __construct(private PDO $db) {}

    public function nome(): string { return 'simulado'; }

    public function criarCobranca(array $pedido, string $email): array
    {
        return [
            'provedor_id' => 'SIM-' . $pedido['id'] . '-' . bin2hex(random_bytes(4)),
            'pix_copia_cola' => 'PIX-DE-TESTE-NAO-PAGUE|MuChila|pedido ' . $pedido['id'] . '|' . MuChilaLoja::real($pedido['valor']),
            'qr_base64' => null,
        ];
    }

    public function consultar(string $provedorId): array
    {
        $st = $this->db->prepare("SELECT id, valor, simulado_status FROM MUCHILA_PEDIDOS WHERE provedor = 'simulado' AND provedor_id = ?");
        $st->execute([$provedorId]);
        $p = $st->fetch();
        if (!$p) throw new Exception("pagamento simulado $provedorId não existe");
        return ['status' => $p['simulado_status'] ?: 'pendente', 'valor' => (float)$p['valor'], 'referencia' => (string)$p['id']];
    }
}

/**
 * Mercado Pago (PIX), API v1/payments. AINDA NÃO TESTADO CONTRA A API: fica desligado até o modo de teste ser aprovado.
 * Para ligar: token e segredo do webhook em config-local\mercadopago.json, notification_url pública (HTTPS) no
 * usercp.loja.xml e curl.cainfo configurado no php.ini (o PHP para Windows não traz certificados de CA).
 */
class MuChilaProvedorMercadoPago implements MuChilaProvedor
{
    const API = 'https://api.mercadopago.com';

    public function __construct(private string $token, private string $segredoWebhook, private string $urlAviso) {}

    public function nome(): string { return 'mercadopago'; }

    public function criarCobranca(array $pedido, string $email): array
    {
        if (!filter_var($email, FILTER_VALIDATE_EMAIL)) throw new Exception('conta sem e-mail válido (o Mercado Pago exige o e-mail do pagador)');
        $corpo = [
            'transaction_amount' => (float)$pedido['valor'],
            'description' => 'Mu Chila - ' . $pedido['descricao'],
            'payment_method_id' => 'pix',
            'payer' => ['email' => $email],
            'external_reference' => (string)$pedido['id'],
            'date_of_expiration' => date('Y-m-d\TH:i:s.vP', strtotime($pedido['expira'])),
        ];
        if ($this->urlAviso !== '') $corpo['notification_url'] = $this->urlAviso;
        // a mesma chave de idempotência faz o Mercado Pago devolver a mesma cobrança se o pedido for reenviado
        $r = $this->chamar('POST', '/v1/payments', $corpo, ['X-Idempotency-Key: muchila-pedido-' . $pedido['id']]);
        $td = $r['point_of_interaction']['transaction_data'] ?? [];
        if (empty($r['id']) || empty($td['qr_code'])) throw new Exception('resposta sem id ou sem código PIX');
        return ['provedor_id' => (string)$r['id'], 'pix_copia_cola' => $td['qr_code'], 'qr_base64' => $td['qr_code_base64'] ?? null];
    }

    public function consultar(string $provedorId): array
    {
        $r = $this->chamar('GET', '/v1/payments/' . rawurlencode($provedorId));
        $mapa = ['approved' => 'aprovado', 'pending' => 'pendente', 'in_process' => 'pendente', 'authorized' => 'pendente',
                 'rejected' => 'recusado', 'cancelled' => 'cancelado', 'refunded' => 'cancelado', 'charged_back' => 'cancelado'];
        return ['status' => $mapa[$r['status'] ?? ''] ?? 'pendente', 'valor' => (float)($r['transaction_amount'] ?? 0),
                'referencia' => (string)($r['external_reference'] ?? '')];
    }

    /** Confere a assinatura x-signature do webhook: HMAC-SHA256 de "id:<data.id>;request-id:<x-request-id>;ts:<ts>;". */
    public function assinaturaValida(string $xSignature, string $xRequestId, string $dataId): bool
    {
        if ($this->segredoWebhook === '') return false;
        $partes = [];
        foreach (explode(',', $xSignature) as $par) {
            [$k, $v] = array_pad(explode('=', trim($par), 2), 2, '');
            $partes[$k] = $v;
        }
        if (empty($partes['ts']) || empty($partes['v1'])) return false;
        $id = ctype_alnum($dataId) ? strtolower($dataId) : $dataId;
        $manifesto = "id:$id;request-id:$xRequestId;ts:{$partes['ts']};";
        return hash_equals(hash_hmac('sha256', $manifesto, $this->segredoWebhook), $partes['v1']);
    }

    private function chamar(string $metodo, string $caminho, ?array $corpo = null, array $cabecalhos = []): array
    {
        $ch = curl_init(self::API . $caminho);
        curl_setopt_array($ch, [
            CURLOPT_CUSTOMREQUEST => $metodo,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 20,
            CURLOPT_HTTPHEADER => array_merge(['Authorization: Bearer ' . $this->token, 'Content-Type: application/json'], $cabecalhos),
        ]);
        if ($corpo !== null) curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($corpo));
        $resp = curl_exec($ch);
        $http = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $erro = curl_error($ch);
        if ($resp === false) throw new Exception("Mercado Pago inacessível: $erro");
        $json = json_decode($resp, true);
        if ($http < 200 || $http >= 300 || !is_array($json)) throw new Exception("Mercado Pago respondeu HTTP $http: " . substr((string)$resp, 0, 300));
        return $json;
    }
}

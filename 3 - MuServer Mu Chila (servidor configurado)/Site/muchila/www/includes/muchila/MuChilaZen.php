<?php
/**
 * Mu Chila - Comprar Zen (usercp/buyzen, 01/10/2026): pacotes de Zen pagos com Cash (CashShopData.WCoinC).
 * O Zen vai para o baú da conta (warehouse.Money, limite de 2 bilhões do jogo), com a conta FORA do jogo (o servidor regrava
 * o baú ao sair). Débito do Cash, Zen no baú e registro em MUCHILA_ZEN_COMPRAS na mesma transação.
 * Pacotes: includes/config/muchila.zen.json (aba Site > Comprar Zen do Mu Chila Admin).
 */
require_once(__DIR__ . '/MuChilaLoja.php');

class MuChilaZen
{
    const ZEN_MAXIMO = 2000000000;   // limite de Zen do baú no jogo
    public array $cfg;
    private ?PDO $db;

    public function __construct(?PDO $db = null, ?array $cfg = null)
    {
        $this->db = $db;
        $this->cfg = self::normalizar($cfg ?? self::lerConfig());
    }

    public static function arquivo(): string { return MuChilaLoja::pastaWww() . '/includes/config/muchila.zen.json'; }

    public static function lerConfig(): array
    {
        $j = json_decode((string)@file_get_contents(self::arquivo()), true);
        if (!is_array($j)) throw new Exception('Configuração da compra de Zen (muchila.zen.json) não encontrada ou inválida.');
        return $j;
    }

    /** Só pacotes válidos (id, 1 a 2 bilhões de Zen, pelo menos 1 Cash), em ordem de Zen. */
    public static function normalizar(array $c): array
    {
        $pacotes = [];
        foreach ($c['pacotes'] ?? [] as $p) {
            if (!is_array($p) || !preg_match('/^[a-z0-9_-]{1,30}$/', (string)($p['id'] ?? ''))) continue;
            $zen = (int)($p['zen'] ?? 0); $cash = (int)($p['cash'] ?? 0);
            if ($zen < 1 || $zen > self::ZEN_MAXIMO || $cash < 1 || (isset($p['ativo']) && !$p['ativo'])) continue;
            $pacotes[$p['id']] = ['id' => (string)$p['id'], 'zen' => $zen, 'cash' => $cash];
        }
        uasort($pacotes, function($a, $b) { return $a['zen'] <=> $b['zen']; });
        return ['ativo' => !empty($c['ativo']), 'pacotes' => $pacotes];
    }

    public function ativa(): bool { return $this->cfg['ativo'] && $this->cfg['pacotes']; }
    public function pacotes(): array { return $this->cfg['pacotes']; }
    private function db(): PDO { return $this->db ??= MuChilaLoja::conectar(); }

    public function saldo(string $conta): int
    {
        $st = $this->db()->prepare("SELECT ISNULL(WCoinC, 0) FROM CashShopData WHERE AccountID = ?");
        $st->execute([$conta]);
        return (int)$st->fetchColumn();
    }

    /** Zen no baú da conta (0 se ela nunca abriu o baú). */
    public function zenNoBau(string $conta): int
    {
        $st = $this->db()->prepare("SELECT ISNULL(Money, 0) FROM warehouse WHERE AccountID = ?");
        $st->execute([$conta]);
        return (int)$st->fetchColumn();
    }

    public function contaFora(string $conta): bool
    {
        $st = $this->db()->prepare("SELECT COUNT(*) FROM MEMB_STAT WHERE memb___id = ? AND (ConnectStat = 1 OR DATEDIFF(second, DisConnectTM, GETDATE()) < 30)");
        $st->execute([$conta]);
        return (int)$st->fetchColumn() === 0;
    }

    /** "500 milhões", "1,5 bilhão": Zen por extenso curto para a tela. */
    public static function zenTexto(int $zen): string
    {
        if ($zen >= 1000000000) { $v = $zen / 1000000000; return rtrim(rtrim(number_format($v, 2, ',', '.'), '0'), ',') . ($v >= 2 ? ' bilhões' : ' bilhão'); }
        if ($zen >= 1000000) { $v = $zen / 1000000; return rtrim(rtrim(number_format($v, 2, ',', '.'), '0'), ',') . ($v >= 2 ? ' milhões' : ' milhão'); }
        return number_format($zen, 0, ',', '.');
    }

    /**
     * Compra: confere tudo de novo e, numa transação só, debita o Cash, soma o Zen no baú e registra.
     * $cashTela = preço que o jogador viu; se o pacote mudou nesse meio tempo, recusa em vez de cobrar outro valor.
     */
    public function comprar(string $conta, string $pacoteId, int $cashTela, string $ip): array
    {
        if (!$this->ativa()) throw new Exception('A compra de Zen está fechada no momento.');
        $p = $this->cfg['pacotes'][$pacoteId] ?? null;
        if (!$p) throw new Exception('Pacote não encontrado.');
        if ($p['cash'] !== $cashTela) throw new Exception('O preço deste pacote mudou para ' . number_format($p['cash'], 0, ',', '.') . ' Cash. Confira e confirme de novo.');
        if (!$this->contaFora($conta)) throw new Exception('Saia do jogo e espere uns 30 segundos antes de comprar: o Zen vai para o seu baú, e o jogo regravaria o baú ao sair.');

        $db = $this->db();
        $db->beginTransaction();
        try {
            $st = $db->prepare("SELECT ISNULL(WCoinC, 0) FROM CashShopData WITH (UPDLOCK, ROWLOCK) WHERE AccountID = ?");
            $st->execute([$conta]);
            $saldo = (int)$st->fetchColumn();
            if ($saldo < $p['cash']) throw new Exception('Cash insuficiente: o pacote custa ' . number_format($p['cash'], 0, ',', '.') . ' Cash e você tem ' . number_format($saldo, 0, ',', '.') . '.');

            $st = $db->prepare("SELECT ISNULL(Money, 0) FROM warehouse WITH (UPDLOCK, ROWLOCK) WHERE AccountID = ?");
            $st->execute([$conta]);
            $antes = $st->fetchColumn();
            $temBau = $antes !== false;
            $antes = (int)$antes;
            if ($antes + $p['zen'] > self::ZEN_MAXIMO)
                throw new Exception('Não cabe: seu baú tem ' . self::zenTexto($antes) . ' de Zen e o limite do jogo é 2 bilhões. Escolha um pacote menor ou tire Zen do baú no jogo.');

            $offline = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = ? AND ConnectStat = 1)";
            if ($temBau) {
                $st = $db->prepare("UPDATE warehouse SET Money = ? WHERE AccountID = ? AND ISNULL(Money, 0) = ? AND $offline");
                $st->execute([$antes + $p['zen'], $conta, $antes, $conta]);
            } else {
                $st = $db->prepare("INSERT INTO warehouse (AccountID, Items, Money, EndUseDate, DbVersion, pw)
                    SELECT ?, CAST(REPLICATE(CAST(CHAR(255) AS varchar(max)), 3840) AS varbinary(3840)), ?, GETDATE(), 3, 0 WHERE $offline");
                $st->execute([$conta, $p['zen'], $conta]);
            }
            if ($st->rowCount() !== 1) throw new Exception('O baú mudou ou a conta entrou no jogo no meio da compra; nada foi cobrado.');

            $st = $db->prepare("UPDATE CashShopData SET WCoinC = WCoinC - ? WHERE AccountID = ? AND WCoinC >= ?");
            $st->execute([$p['cash'], $conta, $p['cash']]);
            if ($st->rowCount() !== 1) throw new Exception('Não consegui debitar o Cash; nada foi feito.');

            $db->prepare("INSERT INTO MUCHILA_ZEN_COMPRAS (conta, pacote, zen, cash, saldo_antes, zen_antes, ip) VALUES (?, ?, ?, ?, ?, ?, ?)")
               ->execute([$conta, $p['id'], $p['zen'], $p['cash'], $saldo, $antes, substr($ip, 0, 45)]);
            $db->commit();
            return ['zen' => $p['zen'], 'cash' => $p['cash'], 'saldo' => $saldo - $p['cash'], 'zen_bau' => $antes + $p['zen']];
        } catch (Throwable $e) {
            if ($db->inTransaction()) $db->rollBack();
            throw $e;
        }
    }
}

<?php
/**
 * Mu Chila - Loja de itens do site (30/09/2026): o jogador monta o item (nível, adicional, sorte, skill, excelentes),
 * paga com Cash (CashShopData.WCoinC, o mesmo da Cash Shop do jogo) e o item cai no baú da conta.
 *
 * Catálogo e preços: includes/config/muchila.lojaitens.json (editado também pela aba "Loja de itens" do Mu Chila Admin).
 * Preço = preço base do item + nivel[nível] + adicional[opção] + sorte + skill + excelente[quantidade de excelentes].
 * O preço é sempre recalculado aqui no servidor; o da tela é só exibição (e precisa bater, senão a compra é recusada).
 *
 * O que cada item aceita vem dos arquivos do servidor (conferido no ItemOption.txt em 30/09/2026):
 *   skill     só se o item tem skill no Item.txt (coluna Skill > 0)
 *   sorte     armas, cajados, escudos, armaduras e asas (anéis e pingentes não têm)
 *   adicional armas/armaduras/asas: +4 a +28; anéis e pingentes: +1% a +7% de vida
 *   excelente armas, cajados e pingentes: opções de ataque; escudos, armaduras e anéis: opções de defesa; asas: nenhuma
 *
 * Compra: conta FORA do jogo (o servidor regrava o baú ao sair), Cash suficiente e espaço no baú. Débito do Cash, item no
 * baú e registro em MUCHILA_LOJAITENS_COMPRAS ficam na mesma transação: ou acontece tudo, ou nada.
 * Item novo: 16 bytes como o "Colocar item..." do painel (Items.cs): série do WZ_GetItemSerial, durabilidade do Item.txt.
 */

require_once(__DIR__ . '/MuChilaLoja.php');
require_once(__DIR__ . '/MuChilaItens.php');

class MuChilaLojaItens
{
    /** Excelentes por bit (0 a 5), na ordem do ItemOption.txt do servidor. */
    const EXC_ARMA = [
        'Recupera mana ao matar monstros (mana/8)',
        'Recupera vida ao matar monstros (vida/8)',
        'Velocidade de ataque +7',
        'Dano +2%',
        'Dano +nível/20',
        'Taxa de dano excelente +10%',
    ];
    const EXC_DEFESA = [
        'Mais Zen ao caçar (+30%)',
        'Taxa de sucesso da defesa +10%',
        'Reflete 5% do dano recebido',
        'Reduz o dano recebido em 4%',
        'Mana máxima +4%',
        'Vida máxima +4%',
    ];
    /** Anéis (defesa) e pingentes (ataque) que aceitam excelente: tipo dentro da seção 13. */
    const ANEIS = [8, 9, 21, 22, 23, 24, 169, 170, 173, 176];
    const PINGENTES = [12, 13, 25, 26, 27, 28, 171, 172];
    const CLASSES = ['DW' => 'Wizard', 'DK' => 'Knight', 'FE' => 'Elf', 'MG' => 'Gladiator', 'DL' => 'Lord', 'SU' => 'Summoner',
                     'RF' => 'Fighter', 'GL' => 'Lancer', 'RW' => 'Rune Wizard', 'SL' => 'Slayer', 'GC' => 'Gun Crusher'];

    public array $cfg;
    private ?PDO $db;
    /** @var array<string, array> "secao-tipo" => dados do Item.txt */
    private array $itemTxt = [];
    private ?array $catalogo = null;

    /** $db null = conecta quando precisar; $cfg/$itemTxt permitem testar sem o site instalado. */
    public function __construct(?PDO $db = null, ?array $cfg = null, ?string $itemTxt = null)
    {
        $this->db = $db;
        $this->cfg = self::normalizar($cfg ?? self::lerConfig());
        $this->lerItemTxt($itemTxt ?? $this->cfg['item_txt']);
    }

    // ------------------------------------------------------------------ configuração

    public static function arquivoConfig(): string { return MuChilaLoja::pastaWww() . '/includes/config/muchila.lojaitens.json'; }

    public static function lerConfig(): array
    {
        $json = json_decode((string)@file_get_contents(self::arquivoConfig()), true);
        if (!is_array($json)) throw new Exception('Configuração da loja de itens (muchila.lojaitens.json) não encontrada ou inválida.');
        return $json;
    }

    /** Preenche o que faltar com os padrões e garante os tamanhos das tabelas de preço. */
    public static function normalizar(array $c): array
    {
        $p = $c['precos'] ?? [];
        $tabela = function($v, int $n) { $v = array_values(is_array($v) ? $v : []); $v = array_map('intval', array_slice($v, 0, $n)); return array_pad($v, $n, $v ? end($v) : 0); };
        $c['ativo'] = !empty($c['ativo']);
        $c['nivel_max'] = max(0, min(15, (int)($c['nivel_max'] ?? 15)));
        $c['exc_max'] = max(0, min(6, (int)($c['exc_max'] ?? 6)));
        $c['precos'] = [
            'nivel' => $tabela($p['nivel'] ?? [], 16),
            'adicional' => $tabela($p['adicional'] ?? [], 8),
            'sorte' => max(0, (int)($p['sorte'] ?? 0)),
            'skill' => max(0, (int)($p['skill'] ?? 0)),
            'excelente' => $tabela($p['excelente'] ?? [], 7),
        ];
        $c['categorias'] = array_values(array_filter($c['categorias'] ?? [], 'is_array'));
        $c['item_txt'] = $c['item_txt'] ?? dirname(MuChilaLoja::pastaSite()) . '/Data/Item/Item.txt';
        return $c;
    }

    public function ativa(): bool { return $this->cfg['ativo']; }

    private function db(): PDO { return $this->db ??= MuChilaLoja::conectar(); }

    // ------------------------------------------------------------------ Item.txt

    /** Lê nome, tamanho, skill, durabilidade, nível de drop e classes de cada item (colunas pelo cabeçalho de cada seção). */
    private function lerItemTxt(string $arquivo): void
    {
        $secao = -1; $cab = null;
        foreach (@file($arquivo) ?: [] as $linha) {
            $t = trim($linha);
            if ($t === '') continue;
            if (preg_match('/^\d+$/', $t)) { $secao = (int)$t; $cab = null; continue; }
            if (preg_match('#^//\s*(Type|Index)\b#', $t)) {
                $h = preg_split('/\s+/', trim(substr($t, 2)));
                $i = array_search('Name', $h, true);
                $cab = $i === false ? null : array_slice($h, $i + 1);
                continue;
            }
            if ($secao < 0 || !preg_match('/^(\d+)\s+(\S+)\s+(\d+)\s+(\d+)\s+(\d+)\s+\d+\s+\d+\s+\d+\s+"([^"]*)"\s*(.*)$/', $t, $m)) continue;
            $id = $secao . '-' . (int)$m[1];
            if (isset($this->itemTxt[$id])) continue;
            $resto = preg_split('/\s+/', trim($m[7]));
            $col = function(string $nome) use ($cab, $resto) {
                $i = $cab === null ? false : array_search($nome, $cab, true);
                return $i !== false && isset($resto[$i]) && is_numeric($resto[$i]) ? (int)$resto[$i] : null;
            };
            $classes = [];
            foreach (self::CLASSES as $sigla => $nome) if (($col($sigla) ?? 0) > 0) $classes[] = $nome;
            $this->itemTxt[$id] = [
                'secao' => $secao, 'tipo' => (int)$m[1], 'nome' => trim($m[6]),
                'largura' => max(1, (int)$m[4]), 'altura' => max(1, (int)$m[5]), 'tem_skill' => (int)$m[3] > 0,
                'durabilidade' => max(1, min(255, $col('Durability') ?? 1)), 'nivel_drop' => $col('Level') ?? 0,
                'nivel_req' => $col('ReqLevel') ?? 0, 'classes' => $classes,
            ];
        }
        if (!$this->itemTxt) throw new Exception("Não consegui ler a lista de itens ($arquivo).");
    }

    // ------------------------------------------------------------------ catálogo

    /** Regras do item conforme a seção (ver o comentário do topo). */
    public static function regras(int $secao, int $tipo): array
    {
        $anel = $secao === 13 && in_array($tipo, self::ANEIS, true);
        $pingente = $secao === 13 && in_array($tipo, self::PINGENTES, true);
        $exc = $secao <= 5 || $pingente ? 'arma' : (($secao >= 6 && $secao <= 11) || $anel ? 'defesa' : null);
        return [
            'exc' => $exc,
            'sorte' => $secao <= 12,
            'adicional' => $secao <= 13,
            'adicional_passo' => $secao === 13 ? 1 : 4,
            'adicional_sufixo' => $secao === 13 ? '% de vida' : '',
            'grupo' => $secao <= 5 || $pingente ? 'ataque' : 'defesa',
        ];
    }

    /**
     * Categorias com os itens prontos para a tela: [id, nome, grupo, icone, itens => [...]].
     * Itens que não existem no Item.txt ou estão desativados ficam de fora.
     */
    public function catalogo(): array
    {
        if ($this->catalogo !== null) return $this->catalogo;
        $lista = [];
        foreach ($this->cfg['categorias'] as $ci => $cat) {
            if (isset($cat['ativo']) && !$cat['ativo']) continue;
            $itens = [];
            foreach ($cat['itens'] ?? [] as $it) {
                if (!is_array($it) || (isset($it['ativo']) && !$it['ativo'])) continue;
                $base = $this->itemTxt[(int)($it['secao'] ?? -1) . '-' . (int)($it['tipo'] ?? -1)] ?? null;
                if (!$base) continue;
                $r = self::regras($base['secao'], $base['tipo']);
                if (isset($it['exc']) && in_array($it['exc'], ['arma', 'defesa', 'nenhuma'], true)) $r['exc'] = $it['exc'] === 'nenhuma' ? null : $it['exc'];
                $item = $base + $r + [
                    'id' => $base['secao'] . '-' . $base['tipo'],
                    'categoria' => (string)($cat['id'] ?? $ci),
                    'preco' => max(0, (int)($it['preco'] ?? 0)),
                    'nivel_max' => max(0, min($this->cfg['nivel_max'], (int)($it['nivel_max'] ?? $this->cfg['nivel_max']))),
                    'destaque' => !empty($it['destaque']),
                ];
                $item['exc_max'] = $item['exc'] ? $this->cfg['exc_max'] : 0;
                $item['exc_nomes'] = $item['exc'] === 'arma' ? self::EXC_ARMA : ($item['exc'] === 'defesa' ? self::EXC_DEFESA : []);
                $itens[$item['id']] = $item;
            }
            if (!$itens) continue;
            $lista[] = ['id' => (string)($cat['id'] ?? $ci), 'nome' => (string)($cat['nome'] ?? 'Itens'), 'grupo' => (string)($cat['grupo'] ?? 'Itens'),
                        'icone' => (string)($cat['icone'] ?? 'item'), 'itens' => $itens];
        }
        return $this->catalogo = $lista;
    }

    public function item(string $id): ?array
    {
        foreach ($this->catalogo() as $cat) if (isset($cat['itens'][$id])) return $cat['itens'][$id];
        return null;
    }

    // ------------------------------------------------------------------ escolha e preço

    /**
     * Confere e organiza o que o jogador escolheu. Erro (Exception) para qualquer valor fora do que o item aceita:
     * a tela nunca manda isso, então só acontece com formulário adulterado.
     * @return array{nivel:int, adicional:int, sorte:bool, skill:bool, exc:int}
     */
    public function escolha(array $item, array $e): array
    {
        $nivel = (int)($e['nivel'] ?? 0);
        $adicional = (int)($e['adicional'] ?? 0);
        $sorte = !empty($e['sorte']);
        $skill = !empty($e['skill']);
        $exc = 0;
        foreach ((array)($e['exc'] ?? []) as $bit) {
            $bit = (int)$bit;
            if ($bit < 0 || $bit > 5) throw new Exception('Opção excelente inválida.');
            $exc |= 1 << $bit;
        }
        if ($nivel < 0 || $nivel > $item['nivel_max']) throw new Exception("Nível inválido (0 a +{$item['nivel_max']}).");
        if ($adicional < 0 || $adicional > 7 || ($adicional > 0 && !$item['adicional'])) throw new Exception('Adicional inválido para este item.');
        if ($sorte && !$item['sorte']) throw new Exception('Este item não aceita sorte.');
        if ($skill && !$item['tem_skill']) throw new Exception('Este item não tem skill.');
        $qtd = substr_count(decbin($exc), '1');
        if ($qtd > $item['exc_max']) throw new Exception($item['exc_max'] ? "No máximo {$item['exc_max']} opções excelentes." : 'Este item não aceita opções excelentes.');
        return ['nivel' => $nivel, 'adicional' => $adicional, 'sorte' => $sorte, 'skill' => $skill, 'exc' => $exc];
    }

    /** Preço em Cash, item por item (para a tela mostrar o detalhamento). */
    public function precoDetalhado(array $item, array $esc): array
    {
        $p = $this->cfg['precos'];
        $linhas = ['Item' => $item['preco']];
        if ($esc['nivel'] > 0) $linhas['Nível +' . $esc['nivel']] = $p['nivel'][$esc['nivel']];
        if ($esc['adicional'] > 0) $linhas['Adicional +' . $esc['adicional'] * $item['adicional_passo'] . ($item['adicional_passo'] === 1 ? '%' : '')] = $p['adicional'][$esc['adicional']];
        if ($esc['sorte']) $linhas['Sorte'] = $p['sorte'];
        if ($esc['skill']) $linhas['Skill'] = $p['skill'];
        $qtd = substr_count(decbin($esc['exc']), '1');
        if ($qtd > 0) $linhas["Excelente ($qtd)"] = $p['excelente'][$qtd];
        return $linhas;
    }

    public function preco(array $item, array $esc): int { return (int)array_sum($this->precoDetalhado($item, $esc)); }

    /** Texto do item: "Dragon Helm +15 +28, Sorte, Excelente (6)". */
    public static function descrever(array $item, array $esc): string
    {
        $p = [$item['nome'] . ($esc['nivel'] > 0 ? ' +' . $esc['nivel'] : '')
              . ($esc['adicional'] > 0 ? ' +' . $esc['adicional'] * $item['adicional_passo'] . ($item['adicional_passo'] === 1 ? '%' : '') : '')];
        if ($esc['skill']) $p[] = 'Skill';
        if ($esc['sorte']) $p[] = 'Sorte';
        $qtd = substr_count(decbin($esc['exc']), '1');
        if ($qtd > 0) $p[] = "Excelente ($qtd)";
        return implode(', ', $p);
    }

    // ------------------------------------------------------------------ o item (16 bytes)

    /** Monta o item no formato do baú (o mesmo do Items.Place do painel). */
    public static function montar(array $item, array $esc, int $serial): string
    {
        $tipo = $item['tipo']; $secao = $item['secao']; $opcao = $esc['adicional'];
        $b = [
            $tipo & 0xFF,
            ($esc['nivel'] << 3) | ($esc['skill'] ? 0x80 : 0) | ($esc['sorte'] ? 0x04 : 0) | ($opcao & 3),
            max(1, min(255, (int)$item['durabilidade'])),
            ($serial >> 24) & 0xFF, ($serial >> 16) & 0xFF, ($serial >> 8) & 0xFF, $serial & 0xFF,
            ($esc['exc'] & 0x3F) | ($opcao >= 4 ? 0x40 : 0) | (($tipo & 0x100) ? 0x80 : 0),
            0,
            $secao << 4,
            0,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF,   // sem sockets (igual aos itens comuns do servidor)
        ];
        return pack('C*', ...$b);
    }

    // ------------------------------------------------------------------ conta e compra

    public function saldo(string $conta): int
    {
        $st = $this->db()->prepare("SELECT ISNULL(WCoinC, 0) FROM CashShopData WHERE AccountID = ?");
        $st->execute([$conta]);
        return (int)$st->fetchColumn();
    }

    /** Conta fora do jogo há pelo menos 30 s (o servidor já gravou o baú). */
    public function contaFora(string $conta): bool
    {
        $st = $this->db()->prepare("SELECT COUNT(*) FROM MEMB_STAT WHERE memb___id = ? AND (ConnectStat = 1 OR DATEDIFF(second, DisConnectTM, GETDATE()) < 30)");
        $st->execute([$conta]);
        return (int)$st->fetchColumn() === 0;
    }

    /**
     * Compra: confere tudo de novo, debita o Cash, põe o item no baú e registra, numa transação só.
     * $precoTela = preço que o jogador viu; se a tabela mudou nesse meio tempo, recusa em vez de cobrar outro valor.
     */
    public function comprar(string $conta, string $itemId, array $escolhaTela, int $precoTela, string $ip): array
    {
        if (!$this->ativa()) throw new Exception('A loja de itens está fechada no momento.');
        $item = $this->item($itemId);
        if (!$item) throw new Exception('Item não encontrado na loja.');
        $esc = $this->escolha($item, $escolhaTela);
        $preco = $this->preco($item, $esc);
        if ($preco !== $precoTela) throw new Exception('O preço deste item mudou para ' . self::cash($preco) . '. Confira e confirme de novo.');
        if (!$this->contaFora($conta)) throw new Exception('Saia do jogo e espere uns 30 segundos antes de comprar: o item vai para o seu baú, e o jogo regravaria o baú ao sair.');

        $db = $this->db();
        $db->beginTransaction();
        try {
            $st = $db->prepare("SELECT ISNULL(WCoinC, 0) FROM CashShopData WITH (UPDLOCK, ROWLOCK) WHERE AccountID = ?");
            $st->execute([$conta]);
            $saldo = (int)$st->fetchColumn();
            if ($saldo < $preco) throw new Exception('Cash insuficiente: o item custa ' . self::cash($preco) . ' e você tem ' . self::cash($saldo) . '.');

            // baú: lê travado, acha espaço (baú e, se liberado, o estendido) e grava só se nada mudou e a conta continua fora
            $st = $db->prepare("SELECT CONVERT(varchar(7680), Items, 2) FROM warehouse WITH (UPDLOCK, ROWLOCK) WHERE AccountID = ?");
            $st->execute([$conta]);
            $hex = $st->fetchColumn();
            $bau = $hex === false || $hex === null ? null : hex2bin(str_pad((string)$hex, 7680, 'F'));
            $st = $db->prepare("SELECT ISNULL(ExtWarehouse, 0) FROM AccountCharacter WHERE Id = ?");
            $st->execute([$conta]);
            $estendido = (int)$st->fetchColumn() === 1;
            $itens = new MuChilaItens($this->cfg['item_txt']);
            $base = $bau ?? MuChilaItens::bauVazio();
            $pos = $itens->acharEspaco($base, $item['largura'], $item['altura'], $estendido);
            if ($pos === null) throw new Exception("Não há espaço {$item['largura']}x{$item['altura']} livre no seu baú. Libere espaço no jogo e tente de novo.");

            $serial = (int)$db->query("EXEC WZ_GetItemSerial")->fetchColumn();
            if ($serial <= 0) throw new Exception('O banco não deu um número de série para o item (WZ_GetItemSerial).');
            $bytes = self::montar($item, $esc, $serial);
            $novo = substr_replace($base, $bytes, $pos * MuChilaItens::TAMANHO, MuChilaItens::TAMANHO);
            $offline = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = ? AND ConnectStat = 1)";
            if ($bau === null) {
                $st = $db->prepare("INSERT INTO warehouse (AccountID, Items, Money, EndUseDate, DbVersion, pw)
                    SELECT ?, CONVERT(varbinary(3840), ?, 2), 0, GETDATE(), 3, 0 WHERE $offline");
                $st->execute([$conta, bin2hex($novo), $conta]);
            } else {
                $st = $db->prepare("UPDATE warehouse SET Items = CONVERT(varbinary(3840), ?, 2)
                    WHERE AccountID = ? AND Items = CONVERT(varbinary(3840), ?, 2) AND $offline");
                $st->execute([bin2hex($novo), $conta, bin2hex($bau), $conta]);
            }
            if ($st->rowCount() !== 1) throw new Exception('O baú mudou ou a conta entrou no jogo no meio da compra; nada foi cobrado.');

            $st = $db->prepare("UPDATE CashShopData SET WCoinC = WCoinC - ? WHERE AccountID = ? AND WCoinC >= ?");
            $st->execute([$preco, $conta, $preco]);
            if ($st->rowCount() !== 1) throw new Exception('Não consegui debitar o Cash; nada foi feito.');

            $descricao = self::descrever($item, $esc);
            $st = $db->prepare("INSERT INTO MUCHILA_LOJAITENS_COMPRAS (conta, item_id, descricao, preco, saldo_antes, posicao, serial, hex, ip)
                OUTPUT INSERTED.id VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)");
            $st->execute([$conta, $item['id'], $descricao, $preco, $saldo, $pos, $serial, strtoupper(bin2hex($bytes)), substr($ip, 0, 45)]);
            $id = (int)$st->fetchColumn();
            $db->commit();
            return ['id' => $id, 'descricao' => $descricao, 'preco' => $preco, 'saldo' => $saldo - $preco, 'posicao' => $pos,
                    'pagina' => $pos < MuChilaItens::POR_PAGINA ? 'baú' : 'baú estendido'];
        } catch (Exception $e) {
            if ($db->inTransaction()) $db->rollBack();
            throw $e;
        }
    }

    public function comprasDaConta(string $conta, int $limite = 10): array
    {
        $st = $this->db()->prepare("SELECT TOP (" . max(1, $limite) . ") id, item_id, descricao, preco, criado FROM MUCHILA_LOJAITENS_COMPRAS WHERE conta = ? ORDER BY id DESC");
        $st->execute([$conta]);
        return $st->fetchAll();
    }

    // ------------------------------------------------------------------ formatação

    public static function cash(int $v): string { return number_format($v, 0, ',', '.') . ' Cash'; }

    /** Dados que a tela precisa para calcular o preço igual ao servidor (o servidor recalcula na compra). */
    public function tabelaParaTela(): array
    {
        return ['precos' => $this->cfg['precos'], 'exc_max' => $this->cfg['exc_max']];
    }
}

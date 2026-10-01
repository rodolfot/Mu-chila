<?php
/**
 * Mu Chila - itens do jogo para o site: decodifica os 16 bytes de um item (mesmo formato do painel, Items.cs),
 * lê nome e tamanho no Item.txt do servidor e encaixa itens no baú.
 *
 * Item (16 bytes): [0] índice (8 bits baixos)  [1] nível (bits 3-6), skill (bit 7), sorte (bit 2), opção (bits 0-1)
 *   [2] durabilidade (nos itens que empilham, ItemStack.txt, é a QUANTIDADE: Jewel of Soul x50 ocupa 1 posição)
 *   [3..6] serial  [7] excelente (bits 0-5), opção +4 (bit 6), índice bit 8 (bit 7)  [8] set (ancient)
 *   [9] seção (bits 4-7)  [10] harmonia  [11..15] sockets.  Posição vazia = 16 bytes 0xFF.
 * Baú (warehouse.Items, 3840 bytes = 240 posições): 0-119 baú (8 colunas x 15 linhas), 120-239 baú estendido
 *   (só com AccountCharacter.ExtWarehouse = 1). O item fica na posição do canto de cima à esquerda e ocupa largura x altura.
 */
class MuChilaItens
{
    const TAMANHO = 16, COLUNAS = 8, LINHAS = 15, POR_PAGINA = 120, POSICOES = 240;

    /** @var array<int, array{nome:string, largura:int, altura:int}> índice (seção*512+tipo) => dados */
    private array $catalogo = [];
    /** @var array<int, int> índice => quantidade máxima na pilha (ItemStack.txt, ao lado do Item.txt); só os que empilham */
    private array $pilhas = [];

    public function __construct(string $itemTxt)
    {
        foreach (@file(dirname($itemTxt) . '/ItemStack.txt') ?: [] as $linha) {
            // Index MaxStack CreateIndex MultiSplitStatus //Nome
            $c = preg_split('/\s+/', trim(preg_replace('#//.*$#', '', $linha)));
            if (count($c) >= 2 && ctype_digit($c[0]) && ctype_digit($c[1]) && (int)$c[1] > 1) $this->pilhas[(int)$c[0]] = (int)$c[1];
        }
        $secao = -1;
        foreach (@file($itemTxt) ?: [] as $linha) {
            $t = trim($linha);
            if ($t === '' || str_starts_with($t, '//')) continue;
            if (preg_match('/^\d+$/', $t)) { $secao = (int)$t; continue; }
            // Index Slot Skill Width Height HaveSerial HaveOption DropItem "Nome" ...   (Slot pode ser "*")
            if ($secao >= 0 && preg_match('/^(\d+)\s+(?:-?\d+|\*)\s+\d+\s+(\d+)\s+(\d+)\s+\d+\s+\d+\s+\d+\s+"([^"]+)"/', $t, $m))
                $this->catalogo[$secao * 512 + (int)$m[1]] ??= ['nome' => trim($m[4]), 'largura' => max(1, (int)$m[2]), 'altura' => max(1, (int)$m[3])];
        }
        if (!$this->catalogo) throw new Exception("Não consegui ler a lista de itens ($itemTxt).");
    }

    public static function vazio(string $bytes): bool { return $bytes === str_repeat("\xFF", self::TAMANHO); }

    /** Decodifica um item (16 bytes). Devolve null para posição vazia. */
    public function decodificar(string $bytes): ?array
    {
        if (strlen($bytes) !== self::TAMANHO || self::vazio($bytes)) return null;
        $b = array_values(unpack('C*', $bytes));
        $indice = $b[0] | (($b[7] & 0x80) << 1) | (($b[9] & 0xF0) << 5);
        $cat = $this->catalogo[$indice] ?? null;
        $opcao = ($b[1] & 3) + (($b[7] & 0x40) ? 4 : 0);
        $exc = $b[7] & 0x3F;
        $sockets = 0;
        for ($i = 11; $i <= 15; $i++) if ($b[$i] !== 0xFF && $b[$i] !== 0xFE && $b[$i] !== 0) $sockets++;
        $pilha = $this->pilhas[$indice] ?? 1;
        return [
            'empilha' => $pilha > 1, 'quantidade' => $pilha > 1 ? max(1, $b[2]) : 1,
            'indice' => $indice, 'secao' => intdiv($indice, 512), 'tipo' => $indice % 512,
            'nome' => $cat['nome'] ?? sprintf('Item %d,%d', intdiv($indice, 512), $indice % 512),
            'largura' => $cat['largura'] ?? 1, 'altura' => $cat['altura'] ?? 1, 'conhecido' => $cat !== null,
            'nivel' => ($b[1] >> 3) & 0xF, 'skill' => ($b[1] & 0x80) !== 0, 'sorte' => ($b[1] & 0x04) !== 0,
            'opcao' => $opcao, 'excelente' => $exc, 'exc_qtd' => substr_count(decbin($exc), '1'),
            'ancient' => $b[8] > 0, 'harmonia' => $b[10] > 0 && $b[10] !== 0xFF, 'sockets' => $sockets,
            'durabilidade' => $b[2], 'serial' => ($b[3] << 24) | ($b[4] << 16) | ($b[5] << 8) | $b[6],
            'hex' => strtoupper(bin2hex($bytes)),
        ];
    }

    /** Texto curto do item: "Dragon Sword +9, Skill, Sorte, Opção +12, Excelente (2)"; empilháveis: "Jewel of Soul x50". */
    public static function descrever(array $it): string
    {
        $p = [$it['nome'] . (($it['quantidade'] ?? 1) > 1 ? ' x' . $it['quantidade'] : '') . ($it['nivel'] > 0 ? ' +' . $it['nivel'] : '')];
        if ($it['skill']) $p[] = 'Skill';
        if ($it['sorte']) $p[] = 'Sorte';
        if ($it['opcao'] > 0) $p[] = 'Opção +' . ($it['opcao'] * 4);
        if ($it['exc_qtd'] > 0) $p[] = 'Excelente (' . $it['exc_qtd'] . ')';
        if ($it['ancient']) $p[] = 'Ancient';
        if ($it['harmonia']) $p[] = 'Harmonia';
        if ($it['sockets'] > 0) $p[] = 'Sockets (' . $it['sockets'] . ')';
        return implode(', ', $p);
    }

    /** Motivo para não deixar vender (ou null): pentagrama/errtel guardam dados em outra tabela; item com prazo. */
    public function bloqueio(array $it, PDO $db): ?string
    {
        if (preg_match('/pentagram|errtel/i', $it['nome'])) return 'Pentagramas e errtels não podem ser vendidos (os dados deles ficam no personagem).';
        if (!$it['conhecido']) return 'Item desconhecido na lista do servidor.';
        if ($it['serial'] > 0) {
            $st = $db->prepare("SELECT (SELECT COUNT(*) FROM CashShopPeriodItem WHERE ItemSerial = ?) + (SELECT COUNT(*) FROM CashShopPeriodicItem WHERE ItemSerial = ?)");
            $st->execute([$it['serial'], $it['serial']]);
            if ((int)$st->fetchColumn() > 0) return 'Itens com prazo (da loja de cash) não podem ser vendidos.';
        }
        return null;
    }

    /**
     * Acha uma posição livre no baú para um item largura x altura. $bau = 3840 bytes. Procura no baú e, se liberado,
     * no estendido; de cima para baixo, da esquerda para a direita. Devolve a posição (0-239) ou null.
     */
    public function acharEspaco(string $bau, int $largura, int $altura, bool $estendido): ?int
    {
        foreach ($estendido ? [0, 1] : [0] as $pagina) {
            $ocupado = array_fill(0, self::LINHAS, array_fill(0, self::COLUNAS, false));
            for ($p = 0; $p < self::POR_PAGINA; $p++) {
                $pos = $pagina * self::POR_PAGINA + $p;
                $it = $this->decodificar(substr($bau, $pos * self::TAMANHO, self::TAMANHO));
                if (!$it) continue;
                $x0 = $p % self::COLUNAS; $y0 = intdiv($p, self::COLUNAS);
                for ($y = $y0; $y < min(self::LINHAS, $y0 + $it['altura']); $y++)
                    for ($x = $x0; $x < min(self::COLUNAS, $x0 + $it['largura']); $x++) $ocupado[$y][$x] = true;
            }
            for ($y = 0; $y + $altura <= self::LINHAS; $y++)
                for ($x = 0; $x + $largura <= self::COLUNAS; $x++) {
                    $livre = true;
                    for ($dy = 0; $dy < $altura && $livre; $dy++)
                        for ($dx = 0; $dx < $largura && $livre; $dx++) if ($ocupado[$y + $dy][$x + $dx]) $livre = false;
                    if ($livre) return $pagina * self::POR_PAGINA + $y * self::COLUNAS + $x;
                }
        }
        return null;
    }

    /** Baú vazio (3840 bytes 0xFF). */
    public static function bauVazio(): string { return str_repeat("\xFF", self::POSICOES * self::TAMANHO); }
}

<?php
/**
 * Mu Chila - Resetar Skill-Tree pelo site (usercp/clearskilltree, 01/10/2026). Substitui o do WebEngine, que apagava a
 * lista INTEIRA de habilidades do personagem e devolvia os pontos master sem limpar a árvore (ficava com a árvore aprendida
 * e os pontos de volta). Quem faz o serviço é dbo.MuChila_LimparArvoreMaster (muchila\sql\MUCHILA_SITE.sql), o mesmo
 * "Zerar habilidades master" do painel: árvore vazia, pontos = Master Level, poderes master fora da MagicList.
 * Árvore master (MasterSkillTree.MasterSkill): 3 bytes por habilidade (índice baixo, nível, índice alto); FF 00 FF = vazio.
 */
require_once(__DIR__ . '/MuChilaLoja.php');

class MuChilaMaster
{
    private ?PDO $db;

    public function __construct(?PDO $db = null) { $this->db = $db; }
    private function db(): PDO { return $this->db ??= MuChilaLoja::conectar(); }

    /** Habilidades aprendidas na árvore (posições que não são FF 00 FF), pelo hexadecimal da coluna MasterSkill. */
    public static function aprendidas(?string $hex): int
    {
        $n = 0;
        foreach (str_split(strtoupper((string)$hex), 6) as $slot) if (strlen($slot) === 6 && $slot !== 'FF00FF') $n++;
        return $n;
    }

    /** Personagens da conta com o que a página mostra (nível, master, pontos livres, habilidades na árvore, Zen). */
    public function personagens(string $conta): array
    {
        $st = $this->db()->prepare("SELECT c.Name, c.Class, c.cLevel, ISNULL(c.Money, 0) AS Money, m.MasterLevel, m.MasterPoint,
                CONVERT(varchar(max), m.MasterSkill, 2) AS arvore
            FROM Character c LEFT JOIN MasterSkillTree m ON m.Name = c.Name WHERE c.AccountID = ? ORDER BY c.Name");
        $st->execute([$conta]);
        $lista = [];
        foreach ($st->fetchAll() as $c) {
            $c['aprendidas'] = self::aprendidas($c['arvore']);
            unset($c['arvore']);
            $lista[] = $c;
        }
        return $lista;
    }

    /** Limpa a árvore master do personagem e cobra o Zen. Devolve a mensagem para o jogador; erro (Exception) se não puder. */
    public function limparArvore(string $conta, string $nome, int $custoZen): string
    {
        $st = $this->db()->prepare("DECLARE @r int; EXEC @r = dbo.MuChila_LimparArvoreMaster @Conta = ?, @Nome = ?, @CustoZen = ?; SELECT @r AS r");
        $st->execute([$conta, $nome, max(0, $custoZen)]);
        $r = null;
        do { $row = $st->fetch(PDO::FETCH_ASSOC); if (is_array($row) && array_key_exists('r', $row)) { $r = (int)$row['r']; break; } } while ($st->nextRowset());
        switch ($r) {
            case 0: return "$nome: árvore master zerada. Os pontos master voltaram (1 por Master Level) e os poderes master saíram da lista de habilidades; as habilidades normais continuam. Entre no jogo e distribua de novo.";
            case 1: throw new Exception('Saia do jogo e espere uns 30 segundos antes de resetar a árvore master (o jogo regravaria o personagem ao sair).');
            case 2: throw new Exception('Personagem não encontrado na sua conta.');
            case 3: throw new Exception("$nome ainda não tem árvore master (precisa de Master Level).");
            case 4: throw new Exception("$nome não tem Zen suficiente: o reset da árvore custa " . number_format($custoZen, 0, ',', '.') . ' de Zen (no inventário do personagem).');
            case 5: throw new Exception("A árvore master de $nome já está vazia; nada foi cobrado.");
            default: throw new Exception('Não foi possível resetar a árvore master agora. Tente de novo em alguns minutos.');
        }
    }
}

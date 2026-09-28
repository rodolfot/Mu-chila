<?php
/**
 * Mu Chila - Reset, Master Reset e Supreme Reset pela area do jogador, chamando as procedures do banco
 * (MuChila-Resets.sql). As procedures validam pre-requisitos e que a conta esta offline; ESTA classe valida que o
 * personagem e da conta logada. Conta ONLINE: vira pedido (MuChila_ResetPedido) que o vigia do servidor aplica,
 * mandando o personagem para a selecao de personagem na hora (reset sem deslogar, 28/09/2026).
 * Valores (creditos de recompensa, atributo maximo) ficam em includes/config/muchila.resets.json.
 */
class MuChilaResets
{
    private PDO $db;
    public array $cfg;

    public function __construct(?PDO $db = null)
    {
        require_once(__DIR__ . '/MuChilaLoja.php');
        $this->db = $db ?? MuChilaLoja::conectar();
        $this->cfg = self::lerConfig();
    }

    public static function lerConfig(): array
    {
        $f = MuChilaLoja::pastaWww() . '/includes/config/muchila.resets.json';
        $j = json_decode((string)@file_get_contents($f), true);
        if (!is_array($j)) $j = [];
        // valores padrao (o dono ajusta no JSON)
        return $j + [
            'master_creditos'  => 0,
            'supreme_creditos' => 0,
            'max_stat'         => 32767,
            'ativo'            => true,
        ];
    }

    /** Personagens da conta com o estado de reset (nivel, master, atributos, contadores, online). */
    public function personagens(string $conta): array
    {
        $sql = "SELECT c.Name, c.Class, c.cLevel, c.Strength, c.Dexterity, c.Vitality, c.Energy,
                       c.ResetCount, c.MasterResetCount, c.SupremeResetCount,
                       ISNULL(m.MasterLevel,0) AS MasterLevel,
                       ISNULL(s.ConnectStat,0) AS Online
                  FROM Character c
                  LEFT JOIN MasterSkillTree m ON m.Name = c.Name
                  LEFT JOIN MEMB_STAT s ON s.memb___id = c.AccountID
                 WHERE c.AccountID = ?
                 ORDER BY c.Name";
        $st = $this->db->prepare($sql);
        $st->execute([$conta]);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }

    /** Saldo de creditos do site da conta. */
    public function creditos(string $conta): int
    {
        $st = $this->db->prepare("SELECT Creditos FROM MuChila_Creditos WHERE AccountID = ?");
        $st->execute([$conta]);
        return (int)($st->fetchColumn() ?: 0);
    }

    /** Confere que o personagem pertence a conta (evita resetar personagem de outro). */
    private function ehDaConta(string $name, string $conta): bool
    {
        $st = $this->db->prepare("SELECT 1 FROM Character WHERE Name = ? AND AccountID = ?");
        $st->execute([$name, $conta]);
        return (bool)$st->fetchColumn();
    }

    /** Executa uma procedure de reset (com @Coins) e devolve o codigo de retorno. */
    private function executar(string $proc, string $name, int $coins): int
    {
        // @return dentro de procedure: capturamos com uma variavel de saida
        $sql = "DECLARE @r int; EXEC @r = $proc @Name = ?, @Coins = ?; SELECT @r AS r";
        $st = $this->db->prepare($sql);
        $st->execute([$name, $coins]);
        // pode haver result sets vazios antes; pega o que tem a coluna r
        do {
            $row = $st->fetch(PDO::FETCH_ASSOC);
            if (is_array($row) && array_key_exists('r', $row)) return (int)$row['r'];
        } while ($st->nextRowset());
        return -1;
    }

    public function reset(string $name, string $conta): string
    {
        if (!$this->ehDaConta($name, $conta)) throw new Exception('Esse personagem não é da sua conta.');
        $sql = "DECLARE @r int; EXEC @r = MuChila_Reset @Name = ?; SELECT @r AS r";
        $st = $this->db->prepare($sql);
        $st->execute([$name]);
        $r = -1;
        do { $row = $st->fetch(PDO::FETCH_ASSOC); if (is_array($row) && array_key_exists('r', $row)) { $r = (int)$row['r']; break; } } while ($st->nextRowset());
        if ($r === 2) return $this->pedir('reset', $name, $conta);
        return $this->mensagem($r, 'Reset feito! Nível de volta a 1, atributos zerados e pontos escaláveis adicionados.');
    }

    public function masterReset(string $name, string $conta): string
    {
        if (!$this->ehDaConta($name, $conta)) throw new Exception('Esse personagem não é da sua conta.');
        $r = $this->executar('MuChila_MasterReset', $name, (int)$this->cfg['master_creditos']);
        if ($r === 2) return $this->pedir('master', $name, $conta);
        return $this->mensagem($r, 'Master Reset feito! Master level e árvore de skills zerados; ' . (int)$this->cfg['master_creditos'] . ' créditos adicionados.');
    }

    public function supremeReset(string $name, string $conta): string
    {
        if (!$this->ehDaConta($name, $conta)) throw new Exception('Esse personagem não é da sua conta.');
        // o Supreme tem @MaxStat: chamamos direto para passar os 3 parametros
        $sql = "DECLARE @r int; EXEC @r = MuChila_SupremeReset @Name = ?, @Coins = ?, @MaxStat = ?; SELECT @r AS r";
        $st = $this->db->prepare($sql);
        $st->execute([$name, (int)$this->cfg['supreme_creditos'], (int)$this->cfg['max_stat']]);
        $r = -1;
        do { $row = $st->fetch(PDO::FETCH_ASSOC); if (is_array($row) && array_key_exists('r', $row)) { $r = (int)$row['r']; break; } } while ($st->nextRowset());
        if ($r === 2) return $this->pedir('supreme', $name, $conta);
        return $this->mensagem($r, 'Supreme Reset feito! Tudo reiniciado e ' . (int)$this->cfg['supreme_creditos'] . ' créditos adicionados.');
    }

    // ---------- reset com a conta online (o vigia do servidor aplica) ----------
    /** O vigia (programa do servidor) atualiza um sinal a cada ~5 s; sem ele não dá para resetar com a conta online. */
    public function vigiaVivo(): bool
    {
        $v = $this->db->query("SELECT DATEDIFF(second, Batida, GETDATE()) FROM MuChila_VigiaStatus WHERE Id = 1")->fetchColumn();
        return $v !== false && (int)$v < 20;
    }

    /**
     * Conta online: grava o pedido. O vigia manda o personagem para a tela de seleção de personagem na hora (se ele estiver
     * jogando), espera o servidor gravar e aplica o reset; o jogador entra de novo já resetado.
     */
    private function pedir(string $tipo, string $name, string $conta): string
    {
        if (!$this->vigiaVivo()) throw new Exception('Você precisa SAIR do jogo para resetar agora (o reset pelo jogo aberto está indisponível no momento).');
        $st = $this->db->prepare("SELECT COUNT(*) FROM MuChila_ResetPedido WHERE Personagem = ? AND Status = 'pendente'");
        $st->execute([$name]);
        if ((int)$st->fetchColumn() > 0) return "Já tem um pedido em andamento para {$name}. Aguarde alguns segundos.";
        $coins = $tipo === 'master' ? (int)$this->cfg['master_creditos'] : ($tipo === 'supreme' ? (int)$this->cfg['supreme_creditos'] : 0);
        $st = $this->db->prepare("INSERT INTO MuChila_ResetPedido (Conta, Personagem, Tipo, Coins, MaxStat) VALUES (?, ?, ?, ?, ?)");
        $st->execute([$conta, $name, $tipo, $coins, (int)$this->cfg['max_stat']]);
        return "Pedido enviado! Se {$name} estiver no jogo, ele vai para a tela de seleção de personagem em instantes; "
             . 'o reset é aplicado ali e é só entrar de novo. Acompanhe abaixo.';
    }

    /** Pedidos recentes da conta (últimos 30 minutos) com o resultado em texto. */
    public function pedidos(string $conta): array
    {
        $st = $this->db->prepare("SELECT TOP 5 Id, Personagem, Tipo, Criado, Status, Resultado, Mensagem FROM MuChila_ResetPedido
                                   WHERE Conta = ? AND Criado >= DATEADD(minute, -30, GETDATE()) ORDER BY Id DESC");
        $st->execute([$conta]);
        $out = [];
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $p) {
            if ($p['Status'] === 'feito') $p['Texto'] = 'Feito! Entre de novo com o personagem.';
            elseif ($p['Status'] === 'pendente') $p['Texto'] = 'Aguardando o personagem ir para a seleção de personagem...';
            elseif ($p['Status'] === 'expirado') $p['Texto'] = (string)$p['Mensagem'];
            else { try { $this->mensagem((int)$p['Resultado'], ''); $p['Texto'] = 'Erro.'; } catch (Exception $e) { $p['Texto'] = 'Não foi feito: ' . $e->getMessage(); } }
            $out[] = $p;
        }
        return $out;
    }

    /** Cron: credita os Master Resets feitos NO JOGO ainda nao pagos (os do site ja saem creditados e marcados). */
    public function creditarMasterResets(?int $coins = null): void
    {
        $c = $coins ?? (int)$this->cfg['master_creditos'];
        $st = $this->db->prepare("EXEC MuChila_CreditarMasterResets @CoinsPorReset = ?");
        $st->execute([$c]);
    }

    /** Traduz o codigo de retorno das procedures em mensagem para o jogador. */
    private function mensagem(int $r, string $ok): string
    {
        switch ($r) {
            case 0: return $ok;
            case 1: throw new Exception('Personagem não encontrado.');
            case 2: throw new Exception('Você precisa SAIR do jogo (a conta não pode estar online) para resetar.');
            case 3: throw new Exception('O personagem precisa estar no nível 400.');
            case 4: throw new Exception('O personagem precisa estar com Master Level 600.');
            case 5: throw new Exception('Os atributos precisam estar no máximo para o Supreme Reset.');
            case 6: throw new Exception('A base de atributos dessa classe ainda não foi cadastrada. Avise o administrador.');
            default: throw new Exception('Não foi possível resetar agora. Tente de novo em instantes.');
        }
    }
}

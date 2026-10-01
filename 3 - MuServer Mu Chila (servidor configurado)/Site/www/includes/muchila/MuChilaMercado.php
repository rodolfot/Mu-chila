<?php
/**
 * Mu Chila - loja entre jogadores (issue #22): itens do baú e personagens, vendidos por dinheiro real.
 *
 * Pagamento: PIX do Mercado Pago com SPLIT — a cobrança é criada com o token do VENDEDOR (ele liga a conta dele pelo
 * OAuth do Mercado Pago), com a taxa da loja em "application_fee". O dinheiro cai direto na conta do vendedor e a taxa
 * na conta da loja; o site nunca guarda nem repassa dinheiro. Provedor "simulado" (padrão) = modo de teste, sem dinheiro.
 *
 * Custódia (impede duplicar):
 *   item       -> ao anunciar sai do baú do vendedor e fica só no anúncio; vendido vai para o baú do comprador;
 *                 cancelado volta para o baú do vendedor.
 *   personagem -> ao anunciar sai das vagas da conta e passa para a conta MUCHILAMKT (ninguém entra nela); vendido
 *                 vai para uma vaga do comprador (com os dados ligados à conta: presentes da Gremory, caça, restauração).
 * Tudo que mexe em baú/personagem exige a conta FORA do jogo (o servidor regrava o baú e o personagem ao sair).
 *
 * Fluxo da compra: comprar() reserva o anúncio (só um comprador consegue) e gera o PIX -> o provedor avisa ->
 * processarAviso() CONSULTA o pagamento -> confirmarPago() marca pago/vendido -> entregar() (na hora ou pela tarefa
 * do minuto, quando o comprador sair do jogo ou liberar espaço).
 */
require_once(__DIR__ . '/MuChilaLoja.php');
require_once(__DIR__ . '/MuChilaItens.php');

class MuChilaMercado
{
    const CUSTODIA = 'MUCHILAMKT';
    const FAMILIAS = [0 => 'Wizard', 16 => 'Knight', 32 => 'Elf', 48 => 'Magic Gladiator', 64 => 'Dark Lord', 80 => 'Summoner',
                      96 => 'Rage Fighter', 112 => 'Grow Lancer', 128 => 'Rune Wizard', 144 => 'Slayer', 160 => 'Gun Crusher'];
    const OFFLINE_SQL = "NOT EXISTS (SELECT 1 FROM MEMB_STAT WHERE memb___id = ? AND ConnectStat = 1)";

    public PDO $db;
    public array $cfg;
    public MuChilaItens $itens;
    private ?array $segredos = null;

    public function __construct(?PDO $db = null)
    {
        $this->db = $db ?? MuChilaLoja::conectar();
        $this->cfg = self::lerConfig();
        $this->itens = new MuChilaItens($this->cfg['item_txt']);
    }

    // ------------------------------------------------------------------ configuração

    public static function lerConfig(): array
    {
        $cfg = [];
        $xml = @simplexml_load_file(MuChilaLoja::pastaWww() . '/includes/config/modules/usercp.mercado.xml');
        if ($xml !== false) foreach ($xml->children() as $k => $v) $cfg[$k] = trim((string)$v);
        return $cfg + ['active' => '0', 'provedor' => 'simulado', 'taxa' => '10', 'expira_minutos' => '30', 'max_pendentes' => '3',
                       'valor_minimo' => '1.00', 'valor_maximo' => '5000.00', 'email_padrao' => '', 'mp_notification_url' => '',
                       'mp_redirect_uri' => '', 'item_txt' => dirname(MuChilaLoja::pastaSite()) . '/Data/Item/Item.txt'];
    }

    public function ativa(): bool { return $this->cfg['active'] === '1'; }
    public function modoTeste(): bool { return $this->cfg['provedor'] !== 'mercadopago'; }
    public function taxaPct(): float { return max(0, min(50, (float)$this->cfg['taxa'])); }

    private function segredos(): array
    {
        if ($this->segredos !== null) return $this->segredos;
        $s = json_decode((string)@file_get_contents(MuChilaLoja::pastaSite() . '/config-local/mercadopago.json'), true);
        return $this->segredos = is_array($s) ? $s : [];
    }

    // ------------------------------------------------------------------ utilidades

    public function contaFora(string $conta): bool
    {
        $st = $this->db->prepare("SELECT COUNT(*) FROM MEMB_STAT WHERE memb___id = ? AND (ConnectStat = 1 OR DATEDIFF(second, DisConnectTM, GETDATE()) < 30)");
        $st->execute([$conta]);
        return (int)$st->fetchColumn() === 0;
    }

    private function exigirFora(string $conta, string $quem = 'Você'): void
    {
        if (!$this->contaFora($conta)) throw new Exception("$quem precisa estar FORA do jogo (saia e espere uns 30 segundos).");
    }

    private function valorValido($valor): string
    {
        $v = round((float)str_replace(',', '.', (string)$valor), 2);
        if ($v < (float)$this->cfg['valor_minimo'] || $v > (float)$this->cfg['valor_maximo'])
            throw new Exception('Preço fora do permitido (' . MuChilaLoja::real($this->cfg['valor_minimo']) . ' a ' . MuChilaLoja::real($this->cfg['valor_maximo']) . ').');
        return number_format($v, 2, '.', '');
    }

    /** Baú da conta como bytes (3840) + se o estendido está liberado. Cria nada: $bau = null se a conta nunca abriu o baú. */
    private function lerBau(string $conta, bool $travar = false): array
    {
        $st = $this->db->prepare("SELECT CONVERT(varchar(7680), Items, 2) AS hex FROM warehouse" . ($travar ? " WITH (UPDLOCK, ROWLOCK)" : "") . " WHERE AccountID = ?");
        $st->execute([$conta]);
        $hex = $st->fetchColumn();
        $bau = $hex === false || $hex === null ? null : hex2bin(str_pad((string)$hex, 7680, 'F'));
        $st = $this->db->prepare("SELECT ISNULL(ExtWarehouse, 0) FROM AccountCharacter WHERE Id = ?");
        $st->execute([$conta]);
        return [$bau, (int)$st->fetchColumn() === 1];
    }

    /** Grava o baú inteiro se ele não mudou desde a leitura e a conta continua fora do jogo. */
    private function gravarBau(string $conta, ?string $antes, string $depois): void
    {
        if ($antes === null) {
            $st = $this->db->prepare("INSERT INTO warehouse (AccountID, Items, Money, EndUseDate, DbVersion, pw)
                SELECT ?, CONVERT(varbinary(3840), ?, 2), 0, GETDATE(), 3, 0 WHERE " . self::OFFLINE_SQL);
            $st->execute([$conta, bin2hex($depois), $conta]);
        } else {
            $st = $this->db->prepare("UPDATE warehouse SET Items = CONVERT(varbinary(3840), ?, 2)
                WHERE AccountID = ? AND Items = CONVERT(varbinary(3840), ?, 2) AND " . self::OFFLINE_SQL);
            $st->execute([bin2hex($depois), $conta, bin2hex($antes), $conta]);
        }
        if ($st->rowCount() !== 1) throw new Exception('O baú mudou ou a conta entrou no jogo no meio da operação; nada foi feito.');
    }

    /** Coloca um item no baú da conta (dentro de transação). Erro se não couber. */
    private function colocarNoBau(string $conta, string $item): int
    {
        [$bau, $estendido] = $this->lerBau($conta, true);
        $it = $this->itens->decodificar($item);
        if (!$it) throw new Exception('item vazio');
        $base = $bau ?? MuChilaItens::bauVazio();
        $pos = $this->itens->acharEspaco($base, $it['largura'], $it['altura'], $estendido);
        if ($pos === null) throw new Exception("Sem espaço no baú de $conta para " . $it['nome'] . " ({$it['largura']}x{$it['altura']}).");
        $novo = substr_replace($base, $item, $pos * MuChilaItens::TAMANHO, MuChilaItens::TAMANHO);
        $this->gravarBau($conta, $bau, $novo);
        return $pos;
    }

    private static function familia(int $classe): string
    {
        $f = intdiv($classe, 16) * 16;
        return (self::FAMILIAS[$f] ?? 'Classe ' . $classe) . ($classe % 16 > 0 ? ' (evolução ' . ($classe % 16) . ')' : '');
    }

    // ------------------------------------------------------------------ o que o jogador pode vender

    /** Itens do baú, com o motivo de bloqueio quando não pode vender. */
    public function itensDoBau(string $conta): array
    {
        [$bau] = $this->lerBau($conta);
        if ($bau === null) return [];
        $lista = [];
        for ($p = 0; $p < MuChilaItens::POSICOES; $p++) {
            $it = $this->itens->decodificar(substr($bau, $p * MuChilaItens::TAMANHO, MuChilaItens::TAMANHO));
            if (!$it) continue;
            $it['posicao'] = $p;
            $it['descricao'] = MuChilaItens::descrever($it);
            $it['bloqueio'] = $this->itens->bloqueio($it, $this->db);
            $lista[] = $it;
        }
        return $lista;
    }

    /** Personagens nas vagas da conta, com o motivo de bloqueio quando não pode vender. */
    public function personagensDaConta(string $conta): array
    {
        $st = $this->db->prepare("SELECT c.Name, c.Class, c.cLevel, c.CtlCode, c.ResetCount, c.MasterResetCount, c.SupremeResetCount,
                ISNULL(m.MasterLevel, 0) AS MasterLevel
            FROM Character c LEFT JOIN MasterSkillTree m ON m.Name = c.Name
            WHERE c.AccountID = ? AND c.Name IN (SELECT v FROM AccountCharacter a
                CROSS APPLY (VALUES (a.GameID1), (a.GameID2), (a.GameID3), (a.GameID4), (a.GameID5), (a.GameID6), (a.GameID7), (a.GameID8)) x(v)
                WHERE a.Id = ? AND v IS NOT NULL) ORDER BY c.Name");
        $st->execute([$conta, $conta]);
        $lista = [];
        foreach ($st->fetchAll() as $c) {
            $c['classe'] = self::familia((int)$c['Class']);
            $c['descricao'] = sprintf('%s, nível %d, master %d, resets %d/%d/%d', $c['classe'], $c['cLevel'], $c['MasterLevel'],
                $c['ResetCount'], $c['MasterResetCount'], $c['SupremeResetCount']);
            $c['bloqueio'] = (int)$c['CtlCode'] > 0 ? 'Personagem com status especial (GM/bloqueado) não pode ser vendido.' : null;
            $lista[] = $c;
        }
        return $lista;
    }

    // ------------------------------------------------------------------ anunciar / cancelar

    public function anunciarItem(string $conta, int $posicao, string $hexEsperado, $valor): int
    {
        $this->exigirAnunciar($conta);
        $valor = $this->valorValido($valor);
        $this->exigirFora($conta);
        $this->db->beginTransaction();
        try {
            [$bau] = $this->lerBau($conta, true);
            if ($bau === null || $posicao < 0 || $posicao >= MuChilaItens::POSICOES) throw new Exception('Item não encontrado no baú.');
            $bytes = substr($bau, $posicao * MuChilaItens::TAMANHO, MuChilaItens::TAMANHO);
            if (strtoupper(bin2hex($bytes)) !== strtoupper($hexEsperado)) throw new Exception('O item mudou de lugar no baú. Atualize a página e tente de novo.');
            $it = $this->itens->decodificar($bytes);
            if (!$it) throw new Exception('Posição vazia.');
            if ($b = $this->itens->bloqueio($it, $this->db)) throw new Exception($b);
            $novo = substr_replace($bau, str_repeat("\xFF", MuChilaItens::TAMANHO), $posicao * MuChilaItens::TAMANHO, MuChilaItens::TAMANHO);
            $this->gravarBau($conta, $bau, $novo);
            $st = $this->db->prepare("INSERT INTO MUCHILA_MERCADO_ANUNCIOS (tipo, vendedor, titulo, detalhes, item, item_index, valor)
                OUTPUT INSERTED.id VALUES ('item', ?, ?, ?, CONVERT(varbinary(16), ?, 2), ?, ?)");
            $st->execute([$conta, mb_substr($it['nome'], 0, 100), mb_substr(MuChilaItens::descrever($it), 0, 400), bin2hex($bytes), $it['indice'], $valor]);
            $id = (int)$st->fetchColumn();
            $this->db->commit();
            return $id;
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            throw $e;
        }
    }

    public function anunciarPersonagem(string $conta, string $nome, $valor): int
    {
        $this->exigirAnunciar($conta);
        $valor = $this->valorValido($valor);
        $this->exigirFora($conta);
        $info = null;
        foreach ($this->personagensDaConta($conta) as $c) if (strcasecmp($c['Name'], $nome) === 0) $info = $c;
        if (!$info) throw new Exception('Personagem não encontrado nesta conta.');
        if ($info['bloqueio']) throw new Exception($info['bloqueio']);
        $nome = $info['Name'];
        $this->db->beginTransaction();
        try {
            $this->tirarDaVaga($conta, $nome);
            $st = $this->db->prepare("UPDATE Character SET AccountID = ? WHERE Name = ? AND AccountID = ? AND " . self::OFFLINE_SQL);
            $st->execute([self::CUSTODIA, $nome, $conta, $conta]);
            if ($st->rowCount() !== 1) throw new Exception('O personagem mudou ou a conta entrou no jogo; nada foi feito.');
            $st = $this->db->prepare("INSERT INTO MUCHILA_MERCADO_ANUNCIOS (tipo, vendedor, titulo, detalhes, personagem, valor)
                OUTPUT INSERTED.id VALUES ('personagem', ?, ?, ?, ?, ?)");
            $st->execute([$conta, $nome, mb_substr($info['descricao'], 0, 400), $nome, $valor]);
            $id = (int)$st->fetchColumn();
            $this->db->commit();
            return $id;
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            throw $e;
        }
    }

    /** Cancela um anúncio ativo do vendedor e devolve o item (baú) ou o personagem (vaga livre). */
    public function cancelarAnuncio(string $conta, int $id): string
    {
        $this->expirarVencidos();
        $this->exigirFora($conta);
        $this->db->beginTransaction();
        try {
            $st = $this->db->prepare("UPDATE MUCHILA_MERCADO_ANUNCIOS SET status = 'cancelado', fechado_em = GETDATE()
                OUTPUT INSERTED.tipo, CONVERT(varchar(32), INSERTED.item, 2) AS item_hex, INSERTED.personagem, INSERTED.titulo
                WHERE id = ? AND vendedor = ? AND status = 'ativo'");
            $st->execute([$id, $conta]);
            $a = $st->fetch();
            if (!$a) throw new Exception('Anúncio não encontrado, já vendido ou com compra em andamento (espere o PIX expirar).');
            if ($a['tipo'] === 'item') {
                $pos = $this->colocarNoBau($conta, hex2bin($a['item_hex']));
                $msg = "{$a['titulo']} voltou para o seu baú.";
            } else {
                $this->devolverPersonagem($a['personagem'], $conta);
                $msg = "{$a['personagem']} voltou para a sua conta.";
            }
            $this->db->commit();
            return "Anúncio cancelado. $msg";
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            throw $e;
        }
    }

    private function exigirAnunciar(string $conta): void
    {
        if (!$this->ativa()) throw new Exception('O mercado está fechado no momento.');
        if (!$this->vendedorLigado($conta))
            throw new Exception('Para vender, primeiro ligue a sua conta do Mercado Pago (aba "Minha conta de vendedor"). É para lá que o dinheiro vai.');
        $st = $this->db->prepare("SELECT bloc_code FROM MEMB_INFO WHERE memb___id = ?");
        $st->execute([$conta]);
        if ((string)$st->fetchColumn() === '1') throw new Exception('Conta bloqueada.');
    }

    /** Tira o personagem da vaga da conta (dentro de transação). */
    private function tirarDaVaga(string $conta, string $nome): void
    {
        $st = $this->db->prepare("SELECT * FROM AccountCharacter WITH (UPDLOCK, ROWLOCK) WHERE Id = ?");
        $st->execute([$conta]);
        $a = $st->fetch();
        if (!$a) throw new Exception('Conta sem lista de personagens.');
        for ($i = 1; $i <= 8; $i++) {
            if (strcasecmp((string)$a["GameID$i"], $nome) !== 0) continue;
            $sql = "UPDATE AccountCharacter SET GameID$i = NULL" . (strcasecmp((string)$a['GameIDC'], $nome) === 0 ? ", GameIDC = NULL" : '') . " WHERE Id = ?";
            $this->db->prepare($sql)->execute([$conta]);
            return;
        }
        throw new Exception('O personagem não está numa vaga da conta.');
    }

    /** Põe o personagem numa vaga livre da conta (dentro de transação). */
    private function porNaVaga(string $conta, string $nome): void
    {
        $st = $this->db->prepare("SELECT * FROM AccountCharacter WITH (UPDLOCK, ROWLOCK) WHERE Id = ?");
        $st->execute([$conta]);
        $a = $st->fetch();
        if (!$a) {
            $this->db->prepare("INSERT INTO AccountCharacter (Id, GameID1, MoveCnt, ExtClass, ExtWarehouse) VALUES (?, ?, 0, 0, 0)")->execute([$conta, $nome]);
            return;
        }
        for ($i = 1; $i <= 8; $i++) {
            if ($a["GameID$i"] !== null && $a["GameID$i"] !== '') continue;
            $this->db->prepare("UPDATE AccountCharacter SET GameID$i = ? WHERE Id = ?")->execute([$nome, $conta]);
            return;
        }
        throw new Exception("A conta $conta não tem vaga livre de personagem (máximo 8).");
    }

    /** Leva o personagem da custódia para a conta (vendedor ao cancelar, comprador na entrega), com os dados ligados à conta. */
    private function devolverPersonagem(string $nome, string $conta): void
    {
        $this->porNaVaga($conta, $nome);
        $st = $this->db->prepare("UPDATE Character SET AccountID = ? WHERE Name = ? AND AccountID = ? AND " . self::OFFLINE_SQL);
        $st->execute([$conta, $nome, self::CUSTODIA, $conta]);
        if ($st->rowCount() !== 1) throw new Exception("O personagem $nome não está em custódia ou a conta $conta entrou no jogo.");
        // dados do personagem guardados junto com a conta
        $this->db->prepare("UPDATE GremoryCase SET AccountID = ? WHERE Name = ? AND StorageType = 2")->execute([$conta, $nome]);
        foreach (['T_HuntingRecord', 'T_HuntingRecordOption', 'T_RestoreItem_Inventory'] as $t)
            $this->db->prepare("UPDATE $t SET AccountID = ? WHERE Name = ?")->execute([$conta, $nome]);
    }

    // ------------------------------------------------------------------ consultas

    public function anuncio(int $id): ?array
    {
        $st = $this->db->prepare("SELECT *, CONVERT(varchar(32), item, 2) AS item_hex FROM MUCHILA_MERCADO_ANUNCIOS WHERE id = ?");
        $st->execute([$id]);
        return $st->fetch() ?: null;
    }

    public function anunciosAtivos(string $tipo = '', string $busca = '', int $limite = 200): array
    {
        $this->expirarVencidos();
        // item_hex: os 16 bytes do item, para a tela mostrar foto e quantidade (anúncios antigos não tinham a quantidade no texto)
        $sql = "SELECT TOP (" . max(1, $limite) . ") *, CONVERT(varchar(32), item, 2) AS item_hex FROM MUCHILA_MERCADO_ANUNCIOS WHERE status = 'ativo'";
        $p = [];
        if (in_array($tipo, ['item', 'personagem'], true)) { $sql .= " AND tipo = ?"; $p[] = $tipo; }
        if ($busca !== '') { $sql .= " AND (titulo LIKE ? OR detalhes LIKE ?)"; $p[] = "%$busca%"; $p[] = "%$busca%"; }
        $st = $this->db->prepare($sql . " ORDER BY criado DESC");
        $st->execute($p);
        return $st->fetchAll();
    }

    public function anunciosDoVendedor(string $conta): array
    {
        $this->expirarVencidos();
        $st = $this->db->prepare("SELECT TOP 100 *, CONVERT(varchar(32), item, 2) AS item_hex FROM MUCHILA_MERCADO_ANUNCIOS WHERE vendedor = ? ORDER BY id DESC");
        $st->execute([$conta]);
        return $st->fetchAll();
    }

    public function pedido(int $id, ?string $comprador = null): ?array
    {
        $st = $this->db->prepare("SELECT * FROM MUCHILA_MERCADO_PEDIDOS WHERE id = ?" . ($comprador !== null ? " AND comprador = ?" : ""));
        $st->execute($comprador !== null ? [$id, $comprador] : [$id]);
        return $st->fetch() ?: null;
    }

    public function pedidosDoComprador(string $conta): array
    {
        $this->expirarVencidos();
        $st = $this->db->prepare("SELECT TOP 50 * FROM MUCHILA_MERCADO_PEDIDOS WHERE comprador = ? ORDER BY id DESC");
        $st->execute([$conta]);
        return $st->fetchAll();
    }

    public function pedidos(?string $status = null, int $limite = 200): array
    {
        $this->expirarVencidos();
        $st = $this->db->prepare("SELECT TOP (" . max(1, $limite) . ") * FROM MUCHILA_MERCADO_PEDIDOS" . ($status ? " WHERE status = ?" : "") . " ORDER BY id DESC");
        $st->execute($status ? [$status] : []);
        return $st->fetchAll();
    }

    /** PIX vencido: pedido "expirado" e o anúncio volta a ficar à venda. */
    public function expirarVencidos(): void
    {
        $this->db->exec("UPDATE a SET a.status = 'ativo', a.pedido_id = NULL
            FROM MUCHILA_MERCADO_ANUNCIOS a JOIN MUCHILA_MERCADO_PEDIDOS p ON p.id = a.pedido_id
            WHERE a.status = 'reservado' AND p.status = 'pendente' AND p.expira < GETDATE()");
        $this->db->exec("UPDATE MUCHILA_MERCADO_PEDIDOS SET status = 'expirado' WHERE status = 'pendente' AND expira < GETDATE()");
    }

    // ------------------------------------------------------------------ comprar

    public function comprar(string $comprador, int $anuncioId, string $ip): array
    {
        if (!$this->ativa()) throw new Exception('O mercado está fechado no momento.');
        $this->expirarVencidos();
        $a = $this->anuncio($anuncioId);
        if (!$a || $a['status'] !== 'ativo') throw new Exception('Este anúncio não está mais à venda.');
        if (strcasecmp($a['vendedor'], $comprador) === 0) throw new Exception('Você não pode comprar o seu próprio anúncio.');
        if (!$this->vendedorLigado($a['vendedor'])) throw new Exception('O vendedor desligou a conta do Mercado Pago; este anúncio não pode ser comprado agora.');
        $conta = (new MuChilaLoja($this->db))->conta($comprador);
        if ($conta['bloc_code'] === '1') throw new Exception('Conta bloqueada.');
        $st = $this->db->prepare("SELECT COUNT(*) FROM MUCHILA_MERCADO_PEDIDOS WHERE comprador = ? AND status = 'pendente'");
        $st->execute([$comprador]);
        if ((int)$st->fetchColumn() >= (int)$this->cfg['max_pendentes']) throw new Exception('Você já tem compras aguardando pagamento. Pague ou espere expirarem.');
        if ($a['tipo'] === 'personagem') $this->exigirVagaLivre($comprador);

        $valor = (float)$a['valor'];
        $taxa = round($valor * $this->taxaPct() / 100, 2);
        $this->db->beginTransaction();
        try {
            $st = $this->db->prepare("INSERT INTO MUCHILA_MERCADO_PEDIDOS (anuncio_id, comprador, vendedor, descricao, valor, taxa, provedor, expira, ip)
                OUTPUT INSERTED.id VALUES (?, ?, ?, ?, ?, ?, ?, DATEADD(minute, CAST(? AS int), GETDATE()), ?)");
            $st->execute([$anuncioId, $comprador, $a['vendedor'], mb_substr(($a['tipo'] === 'item' ? 'Item: ' : 'Personagem: ') . $a['titulo'], 0, 100),
                number_format($valor, 2, '.', ''), number_format($taxa, 2, '.', ''), $this->modoTeste() ? 'simulado' : 'mercadopago',
                max(5, (int)$this->cfg['expira_minutos']), substr($ip, 0, 45)]);
            $id = (int)$st->fetchColumn();
            // reserva: só um comprador consegue (o anúncio precisa ainda estar "ativo")
            $st = $this->db->prepare("UPDATE MUCHILA_MERCADO_ANUNCIOS SET status = 'reservado', pedido_id = ? WHERE id = ? AND status = 'ativo'");
            $st->execute([$id, $anuncioId]);
            if ($st->rowCount() !== 1) throw new Exception('Outro jogador acabou de reservar este anúncio.');
            $this->db->commit();
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            throw $e;
        }

        $pedido = $this->pedido($id);
        try {
            $email = filter_var($conta['email'], FILTER_VALIDATE_EMAIL) ? $conta['email'] : $this->cfg['email_padrao'];
            $cob = $this->criarCobranca($pedido, (string)$email);
        } catch (Exception $e) {
            $this->liberar($id, 'Falha ao gerar a cobrança: ' . $e->getMessage(), 'falhou');
            throw new Exception('Não foi possível gerar o PIX agora. Tente de novo em alguns minutos.');
        }
        $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET provedor_id = ?, pix_copia_cola = ?, qr_base64 = ? WHERE id = ?")
                 ->execute([$cob['provedor_id'], $cob['pix_copia_cola'], $cob['qr_base64'] ?? null, $id]);
        return $this->pedido($id);
    }

    private function exigirVagaLivre(string $conta): void
    {
        $st = $this->db->prepare("SELECT * FROM AccountCharacter WHERE Id = ?");
        $st->execute([$conta]);
        $a = $st->fetch();
        if (!$a) return;
        for ($i = 1; $i <= 8; $i++) if ($a["GameID$i"] === null || $a["GameID$i"] === '') return;
        throw new Exception('Sua conta já tem 8 personagens; apague um antes de comprar outro.');
    }

    /** Pedido que não vai adiante: muda o status e devolve o anúncio para "ativo". */
    private function liberar(int $pedidoId, string $obs, string $status): string
    {
        $this->db->beginTransaction();
        try {
            $st = $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET status = ?, obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000)
                WHERE id = ? AND status IN ('pendente', 'expirado')");
            $st->execute([$status, $obs, $pedidoId]);
            $this->db->prepare("UPDATE MUCHILA_MERCADO_ANUNCIOS SET status = 'ativo', pedido_id = NULL WHERE pedido_id = ? AND status = 'reservado'")->execute([$pedidoId]);
            $this->db->commit();
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            throw $e;
        }
        return "pedido $pedidoId: $status ($obs)";
    }

    /** Aviso do provedor (webhook ou simulação): CONSULTA o pagamento e, se aprovado, confirma e entrega. */
    public function processarAviso(string $provedorId, string $origem): string
    {
        $st = $this->db->prepare("SELECT * FROM MUCHILA_MERCADO_PEDIDOS WHERE provedor_id = ?");
        $st->execute([$provedorId]);
        $p = $st->fetch();
        if (!$p) return "pagamento $provedorId não é do mercado";
        $info = $this->consultar($p);
        switch ($info['status']) {
            case 'aprovado':
                if ((string)$info['referencia'] !== 'mercado-' . $p['id']) return "pagamento $provedorId aponta para outro pedido ({$info['referencia']})";
                if (round((float)$info['valor'], 2) < round((float)$p['valor'], 2)) return $this->anotarPedido((int)$p['id'], "Pago R$ {$info['valor']}, esperado R$ {$p['valor']}", 'falhou');
                return $this->confirmarPago((int)$p['id'], $origem);
            case 'recusado':
            case 'cancelado':
                return $this->liberar((int)$p['id'], "Pagamento {$info['status']} no provedor", 'cancelado');
            default:
                return "pedido {$p['id']}: pagamento ainda {$info['status']}";
        }
    }

    /** Dinheiro confirmado: pedido "pago" e anúncio "vendido" (uma vez só); depois tenta entregar. */
    public function confirmarPago(int $pedidoId, string $origem): string
    {
        $this->db->beginTransaction();
        try {
            $st = $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET status = 'pago', pago_em = GETDATE(),
                    obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000)
                WHERE id = ? AND status IN ('pendente', 'expirado')");
            $st->execute(['pago (' . $origem . ')', $pedidoId]);
            if ($st->rowCount() !== 1) { $this->db->rollBack(); return "pedido $pedidoId já estava pago/entregue; nada feito"; }
            $p = $this->pedido($pedidoId);
            // o anúncio precisa continuar reservado para ESTE pedido (ou ter voltado a "ativo" sem outro comprador)
            $st = $this->db->prepare("UPDATE MUCHILA_MERCADO_ANUNCIOS SET status = 'vendido', comprador = ?, pedido_id = ?, fechado_em = GETDATE()
                WHERE id = ? AND ((status = 'reservado' AND pedido_id = ?) OR (status = 'ativo' AND pedido_id IS NULL))");
            $st->execute([$p['comprador'], $pedidoId, $p['anuncio_id'], $pedidoId]);
            if ($st->rowCount() !== 1) {
                $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET status = 'falhou', obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000) WHERE id = ?")
                         ->execute(['PAGO, mas o anúncio já tinha outro comprador: REEMBOLSAR no Mercado Pago', $pedidoId]);
                $this->db->commit();
                return "pedido $pedidoId pago depois de expirar e o anúncio já foi vendido: reembolsar";
            }
            $this->db->commit();
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            throw $e;
        }
        return $this->entregar($pedidoId, $origem);
    }

    /** Entrega o que foi pago (uma vez só). Se o comprador está no jogo ou sem espaço, fica "pago" e a tarefa tenta de novo. */
    public function entregar(int $pedidoId, string $origem): string
    {
        $p = $this->pedido($pedidoId);
        if (!$p || $p['status'] !== 'pago') return "pedido $pedidoId não está aguardando entrega";
        if (!$this->contaFora($p['comprador'])) return $this->anotarPedido($pedidoId, 'aguardando o comprador sair do jogo');
        $a = $this->anuncio((int)$p['anuncio_id']);
        $this->db->beginTransaction();
        try {
            $st = $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET status = 'entregue', entregue_em = GETDATE() WHERE id = ? AND status = 'pago'");
            $st->execute([$pedidoId]);
            if ($st->rowCount() !== 1) { $this->db->rollBack(); return "pedido $pedidoId já foi entregue"; }
            if ($a['tipo'] === 'item') {
                $pos = $this->colocarNoBau($p['comprador'], hex2bin($a['item_hex']));
                $obs = "item no baú de {$p['comprador']} (posição $pos)";
            } else {
                $this->devolverPersonagem($a['personagem'], $p['comprador']);
                $obs = "personagem {$a['personagem']} na conta {$p['comprador']}";
            }
            $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000) WHERE id = ?")->execute(["entregue ($origem): $obs", $pedidoId]);
            $this->db->commit();
            return "pedido $pedidoId entregue: $obs";
        } catch (Exception $e) {
            if ($this->db->inTransaction()) $this->db->rollBack();
            return $this->anotarPedido($pedidoId, 'entrega pendente: ' . $e->getMessage());
        }
    }

    /** Tarefa do minuto (e ao abrir o mercado): expira PIX vencidos e entrega o que ficou pendente. */
    public function entregarPendentes(?string $comprador = null): array
    {
        $this->expirarVencidos();
        $st = $this->db->prepare("SELECT id FROM MUCHILA_MERCADO_PEDIDOS WHERE status = 'pago'" . ($comprador !== null ? " AND comprador = ?" : "") . " ORDER BY id");
        $st->execute($comprador !== null ? [$comprador] : []);
        $r = [];
        foreach ($st->fetchAll(PDO::FETCH_COLUMN) as $id) $r[] = $this->entregar((int)$id, 'tarefa');
        return $r;
    }

    private function anotarPedido(int $id, string $obs, ?string $status = null): string
    {
        $sql = "UPDATE MUCHILA_MERCADO_PEDIDOS SET obs = LEFT(ISNULL(obs + ' | ', '') + ?, 1000)" . ($status ? ", status = ?" : "") . " WHERE id = ?";
        $this->db->prepare($sql)->execute($status ? [$obs, $status, $id] : [$obs, $id]);
        return "pedido $id: $obs";
    }

    /** Teste: aprova ou recusa o pagamento simulado e segue o mesmo caminho do aviso real. */
    public function simular(int $pedidoId, string $comprador, bool $aprovar): string
    {
        if (!$this->modoTeste()) throw new Exception('Simulação só existe no modo de teste.');
        $p = $this->pedido($pedidoId, $comprador);
        if (!$p || !$p['provedor_id']) throw new Exception('Compra não encontrada.');
        $this->db->prepare("UPDATE MUCHILA_MERCADO_PEDIDOS SET simulado_status = ? WHERE id = ?")->execute([$aprovar ? 'aprovado' : 'recusado', $pedidoId]);
        return $this->processarAviso($p['provedor_id'], 'simulacao');
    }

    /** Admin: cancela uma compra ainda não paga (o anúncio volta a ficar à venda). */
    public function cancelarPedido(int $id, string $motivo): string { return $this->liberar($id, $motivo, 'cancelado'); }

    // ------------------------------------------------------------------ pagamento (simulado ou Mercado Pago com split)

    private function criarCobranca(array $p, string $email): array
    {
        if ($this->modoTeste())
            return ['provedor_id' => 'SIM-MKT-' . $p['id'] . '-' . bin2hex(random_bytes(4)),
                    'pix_copia_cola' => 'PIX-DE-TESTE-NAO-PAGUE|MuChila Mercado|pedido ' . $p['id'] . '|' . MuChilaLoja::real($p['valor']), 'qr_base64' => null];
        if (!filter_var($email, FILTER_VALIDATE_EMAIL)) throw new Exception('conta sem e-mail válido (o Mercado Pago exige o e-mail do pagador)');
        $corpo = [
            'transaction_amount' => (float)$p['valor'],
            'application_fee' => (float)$p['taxa'],            // parte da loja: o resto vai para o vendedor
            'description' => 'Mu Chila Mercado - ' . $p['descricao'],
            'payment_method_id' => 'pix',
            'payer' => ['email' => $email],
            'external_reference' => 'mercado-' . $p['id'],
            'date_of_expiration' => date('Y-m-d\TH:i:s.vP', strtotime($p['expira'])),
        ];
        if ($this->cfg['mp_notification_url'] !== '') $corpo['notification_url'] = $this->cfg['mp_notification_url'];
        $r = $this->mp('POST', '/v1/payments', $this->tokenVendedor($p['vendedor']), $corpo, ['X-Idempotency-Key: muchila-mercado-' . $p['id']]);
        $td = $r['point_of_interaction']['transaction_data'] ?? [];
        if (empty($r['id']) || empty($td['qr_code'])) throw new Exception('resposta sem id ou sem código PIX');
        return ['provedor_id' => (string)$r['id'], 'pix_copia_cola' => $td['qr_code'], 'qr_base64' => $td['qr_code_base64'] ?? null];
    }

    private function consultar(array $p): array
    {
        if ($p['provedor'] === 'simulado')
            return ['status' => $p['simulado_status'] ?: 'pendente', 'valor' => (float)$p['valor'], 'referencia' => 'mercado-' . $p['id']];
        // o pagamento pertence à conta do vendedor: consulta com o token dele
        $r = $this->mp('GET', '/v1/payments/' . rawurlencode($p['provedor_id']), $this->tokenVendedor($p['vendedor']));
        $mapa = ['approved' => 'aprovado', 'pending' => 'pendente', 'in_process' => 'pendente', 'authorized' => 'pendente',
                 'rejected' => 'recusado', 'cancelled' => 'cancelado', 'refunded' => 'cancelado', 'charged_back' => 'cancelado'];
        return ['status' => $mapa[$r['status'] ?? ''] ?? 'pendente', 'valor' => (float)($r['transaction_amount'] ?? 0),
                'referencia' => (string)($r['external_reference'] ?? '')];
    }

    /** Confere a assinatura x-signature do webhook (mesmo esquema da loja). */
    public function assinaturaValida(string $xSignature, string $xRequestId, string $dataId): bool
    {
        $segredo = (string)($this->segredos()['webhook_secret'] ?? '');
        if ($segredo === '') return false;
        $partes = [];
        foreach (explode(',', $xSignature) as $par) { [$k, $v] = array_pad(explode('=', trim($par), 2), 2, ''); $partes[$k] = $v; }
        if (empty($partes['ts']) || empty($partes['v1'])) return false;
        $id = ctype_alnum($dataId) ? strtolower($dataId) : $dataId;
        return hash_equals(hash_hmac('sha256', "id:$id;request-id:$xRequestId;ts:{$partes['ts']};", $segredo), $partes['v1']);
    }

    private function mp(string $metodo, string $caminho, string $token, ?array $corpo = null, array $cabecalhos = [], bool $form = false): array
    {
        $ch = curl_init('https://api.mercadopago.com' . $caminho);
        curl_setopt_array($ch, [CURLOPT_CUSTOMREQUEST => $metodo, CURLOPT_RETURNTRANSFER => true, CURLOPT_TIMEOUT => 20,
            CURLOPT_HTTPHEADER => array_merge($token !== '' ? ['Authorization: Bearer ' . $token] : [], ['Content-Type: application/json'], $cabecalhos)]);
        if ($corpo !== null) curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($corpo));
        $resp = curl_exec($ch);
        $http = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        if ($resp === false) throw new Exception('Mercado Pago inacessível: ' . curl_error($ch));
        $json = json_decode($resp, true);
        if ($http < 200 || $http >= 300 || !is_array($json)) throw new Exception("Mercado Pago respondeu HTTP $http: " . substr((string)$resp, 0, 300));
        return $json;
    }

    // ------------------------------------------------------------------ vendedor: ligação com o Mercado Pago (OAuth)

    public function vendedor(string $conta): ?array
    {
        $st = $this->db->prepare("SELECT * FROM MUCHILA_MERCADO_VENDEDORES WHERE conta = ?");
        $st->execute([$conta]);
        return $st->fetch() ?: null;
    }

    public function vendedorLigado(string $conta): bool
    {
        $v = $this->vendedor($conta);
        if (!$v || !$v['conectado_em']) return false;
        return $this->modoTeste() ? true : ((int)$v['simulado'] === 0 && $v['access_token']);
    }

    /** Modo de teste: liga a conta sem Mercado Pago de verdade. */
    public function conectarSimulado(string $conta): void
    {
        if (!$this->modoTeste()) throw new Exception('Só no modo de teste.');
        $this->db->prepare("MERGE MUCHILA_MERCADO_VENDEDORES AS d USING (SELECT ? AS conta) s ON d.conta = s.conta
            WHEN MATCHED THEN UPDATE SET simulado = 1, conectado_em = GETDATE(), mp_user_id = 'SIMULADO'
            WHEN NOT MATCHED THEN INSERT (conta, simulado, conectado_em, mp_user_id) VALUES (s.conta, 1, GETDATE(), 'SIMULADO');")->execute([$conta]);
    }

    public function desligarVendedor(string $conta): void
    {
        $st = $this->db->prepare("SELECT COUNT(*) FROM MUCHILA_MERCADO_ANUNCIOS WHERE vendedor = ? AND status IN ('ativo', 'reservado')");
        $st->execute([$conta]);
        if ((int)$st->fetchColumn() > 0) throw new Exception('Cancele os seus anúncios antes de desligar a conta do Mercado Pago.');
        $this->db->prepare("DELETE FROM MUCHILA_MERCADO_VENDEDORES WHERE conta = ?")->execute([$conta]);
    }

    /** Link para o vendedor autorizar a loja no Mercado Pago (volta em api/muchila-mercado-oauth.php). */
    public function urlAutorizacao(string $conta): string
    {
        $s = $this->segredos();
        if (empty($s['client_id']) || $this->cfg['mp_redirect_uri'] === '') throw new Exception('Mercado Pago ainda não configurado para vendedores.');
        $state = bin2hex(random_bytes(20));
        $this->db->prepare("MERGE MUCHILA_MERCADO_VENDEDORES AS d USING (SELECT ? AS conta) s ON d.conta = s.conta
            WHEN MATCHED THEN UPDATE SET oauth_state = ?, oauth_state_em = GETDATE()
            WHEN NOT MATCHED THEN INSERT (conta, oauth_state, oauth_state_em) VALUES (s.conta, ?, GETDATE());")->execute([$conta, $state, $state]);
        return 'https://auth.mercadopago.com/authorization?' . http_build_query(['client_id' => $s['client_id'], 'response_type' => 'code',
            'platform_id' => 'mp', 'state' => $state, 'redirect_uri' => $this->cfg['mp_redirect_uri']]);
    }

    /** Retorno do OAuth: troca o código pelos tokens do vendedor e guarda criptografado. Devolve a conta do jogo. */
    public function concluirAutorizacao(string $state, string $code): string
    {
        $st = $this->db->prepare("SELECT conta FROM MUCHILA_MERCADO_VENDEDORES WHERE oauth_state = ? AND oauth_state_em > DATEADD(minute, -15, GETDATE())");
        $st->execute([$state]);
        $conta = $st->fetchColumn();
        if ($conta === false) throw new Exception('Autorização expirada ou inválida. Tente ligar de novo pelo site.');
        $s = $this->segredos();
        $r = $this->mp('POST', '/oauth/token', '', ['client_id' => $s['client_id'], 'client_secret' => $s['client_secret'],
            'grant_type' => 'authorization_code', 'code' => $code, 'redirect_uri' => $this->cfg['mp_redirect_uri']]);
        if (empty($r['access_token'])) throw new Exception('O Mercado Pago não devolveu o acesso.');
        $this->gravarTokens($conta, $r);
        return (string)$conta;
    }

    private function gravarTokens(string $conta, array $r): void
    {
        $this->db->prepare("UPDATE MUCHILA_MERCADO_VENDEDORES SET mp_user_id = ?, access_token = ?, refresh_token = ?,
                token_expira = DATEADD(second, CAST(? AS int), GETDATE()), simulado = 0, conectado_em = GETDATE(), oauth_state = NULL WHERE conta = ?")
            ->execute([(string)($r['user_id'] ?? ''), $this->cifrar($r['access_token']), $this->cifrar((string)($r['refresh_token'] ?? '')),
                (int)($r['expires_in'] ?? 15552000), $conta]);
    }

    /** Token do vendedor para cobrar/consultar; renova quando faltar menos de 7 dias. */
    private function tokenVendedor(string $conta): string
    {
        $v = $this->vendedor($conta);
        if (!$v || !$v['access_token']) throw new Exception("o vendedor $conta não ligou a conta do Mercado Pago");
        if ($v['token_expira'] && strtotime($v['token_expira']) < time() + 7 * 86400 && $v['refresh_token']) {
            $s = $this->segredos();
            $r = $this->mp('POST', '/oauth/token', '', ['client_id' => $s['client_id'], 'client_secret' => $s['client_secret'],
                'grant_type' => 'refresh_token', 'refresh_token' => $this->decifrar($v['refresh_token'])]);
            if (!empty($r['access_token'])) { $this->gravarTokens($conta, $r); return $r['access_token']; }
        }
        return $this->decifrar($v['access_token']);
    }

    private function chave(): string
    {
        $k = base64_decode((string)($this->segredos()['mercado_chave'] ?? ''), true);
        if ($k === false || strlen($k) !== 32) throw new Exception('Falta a chave de criptografia (mercado_chave, 32 bytes em base64) no config-local\mercadopago.json.');
        return $k;
    }

    private function cifrar(string $texto): string
    {
        if ($texto === '') return '';
        $iv = random_bytes(12);
        $ct = openssl_encrypt($texto, 'aes-256-gcm', $this->chave(), OPENSSL_RAW_DATA, $iv, $tag);
        return base64_encode($iv . $tag . $ct);
    }

    private function decifrar(string $dado): string
    {
        $b = base64_decode($dado, true);
        if ($b === false || strlen($b) < 29) throw new Exception('token do vendedor ilegível');
        $txt = openssl_decrypt(substr($b, 28), 'aes-256-gcm', $this->chave(), OPENSSL_RAW_DATA, substr($b, 0, 12), substr($b, 12, 16));
        if ($txt === false) throw new Exception('token do vendedor não pôde ser aberto (chave trocada?)');
        return $txt;
    }

    public static function statusPedido(string $s): string
    {
        return ['pendente' => 'Aguardando pagamento', 'pago' => 'Pago, aguardando entrega', 'entregue' => 'Entregue', 'cancelado' => 'Cancelado',
                'expirado' => 'Expirado', 'falhou' => 'Com problema (fale com a administração)'][$s] ?? $s;
    }

    public static function statusAnuncio(string $s): string
    {
        return ['ativo' => 'À venda', 'reservado' => 'Compra em andamento', 'vendido' => 'Vendido', 'cancelado' => 'Cancelado'][$s] ?? $s;
    }
}

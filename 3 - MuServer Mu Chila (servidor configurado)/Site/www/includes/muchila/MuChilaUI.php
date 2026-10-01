<?php
/**
 * Mu Chila - componentes visuais do tema "muchila" (templates/muchila, 30/09/2026). Cada função devolve HTML pronto;
 * o estilo está em templates/muchila/css/muchila.css (classes mc-*). Usado pelas páginas do Mu Chila e pela template.
 * Textos que vêm do banco/usuário: passe por MuChilaUI::h() antes (as funções só escapam os parâmetros de texto simples).
 */
class MuChilaUI
{
    public static function h($v): string { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); }

    /** URL do sprite de ícones do tema (não depende da template ativa). */
    public static function sprite(): string { return __BASE_URL__ . 'templates/muchila/img/icones.svg'; }

    /** Ícone do sprite (ver templates/muchila/img/icones.svg). */
    public static function icone(string $nome, string $classe = ''): string
    {
        // width/height: tamanho de reserva se o CSS do tema não estiver carregado (o .mc-ico do CSS manda quando está)
        return '<svg class="mc-ico' . ($classe !== '' ? ' ' . self::h($classe) : '') . '" width="20" height="20" aria-hidden="true"><use href="' . self::sprite() . '#' . self::h($nome) . '"></use></svg>';
    }

    /** Cabeçalho da página (substitui o .page-title do WebEngine). $acoes = HTML já pronto (botões). */
    public static function titulo(string $titulo, string $subtitulo = '', string $icone = '', string $acoes = ''): string
    {
        return '<header class="mc-page-head">'
            . ($icone !== '' ? '<div class="mc-page-head__icon">' . self::icone($icone) . '</div>' : '')
            . '<div class="mc-page-head__text"><h1>' . self::h($titulo) . '</h1>' . ($subtitulo !== '' ? '<p>' . $subtitulo . '</p>' : '') . '</div>'
            . ($acoes !== '' ? '<div class="mc-page-head__actions">' . $acoes . '</div>' : '')
            . '</header>';
    }

    /** Aviso destacado. $tipo: info | sucesso | atencao | perigo. $html já pronto (pode ter <strong>, listas...). */
    public static function aviso(string $tipo, string $titulo, string $html, string $icone = ''): string
    {
        $icones = ['info' => 'info', 'sucesso' => 'check', 'atencao' => 'alert', 'perigo' => 'alert'];
        return '<div class="mc-callout mc-callout--' . self::h($tipo) . '" role="' . ($tipo === 'perigo' ? 'alert' : 'note') . '">'
            . '<div class="mc-callout__icon">' . self::icone($icone !== '' ? $icone : ($icones[$tipo] ?? 'info')) . '</div>'
            . '<div class="mc-callout__body">' . ($titulo !== '' ? '<strong class="mc-callout__title">' . self::h($titulo) . '</strong>' : '') . $html . '</div></div>';
    }

    /** Cartão da carteira: saldo de Cash com botão de recarga. */
    public static function carteira(int $cash, string $extra = '', bool $compacta = false): string
    {
        return '<div class="mc-wallet' . ($compacta ? ' mc-wallet--compact' : '') . '">'
            . '<div class="mc-wallet__icon">' . self::icone('coin') . '</div>'
            . '<div class="mc-wallet__info"><span class="mc-wallet__label">Sua carteira</span>'
            . '<strong class="mc-wallet__value" data-mc-saldo="' . $cash . '">' . number_format($cash, 0, ',', '.') . ' <small>Cash</small></strong>'
            . ($extra !== '' ? '<span class="mc-wallet__extra">' . $extra . '</span>' : '') . '</div>'
            . '<a class="mc-btn mc-btn--gold mc-btn--sm" href="' . __BASE_URL__ . 'usercp/loja#cash">' . self::icone('plus') . '<span>Recarregar</span></a>'
            . '</div>';
    }

    /** Número em destaque com rótulo. $tom: ouro | rubi | arcano | verde. */
    public static function estatistica(string $rotulo, string $valor, string $icone, string $tom = 'ouro', string $extra = ''): string
    {
        return '<div class="mc-stat mc-stat--' . self::h($tom) . '"><div class="mc-stat__icon">' . self::icone($icone) . '</div>'
            . '<div><span class="mc-stat__label">' . self::h($rotulo) . '</span><strong class="mc-stat__value">' . $valor . '</strong>'
            . ($extra !== '' ? '<span class="mc-stat__extra">' . $extra . '</span>' : '') . '</div></div>';
    }

    /** Lista vazia com ícone e uma ação opcional (HTML pronto). */
    public static function vazio(string $texto, string $icone = 'chest', string $acao = ''): string
    {
        return '<div class="mc-empty">' . self::icone($icone) . '<p>' . $texto . '</p>' . $acao . '</div>';
    }

    /** Etiqueta pequena. $tom: ouro | rubi | arcano | verde | cinza. */
    public static function etiqueta(string $texto, string $tom = 'cinza'): string
    {
        return '<span class="mc-badge mc-badge--' . self::h($tom) . '">' . self::h($texto) . '</span>';
    }

    /** Ícone de cada página do painel do jogador (menu lateral e painel), pelo link do usercp.json. */
    public static function iconeDoLink(string $link): string
    {
        $mapa = ['usercp/loja' => 'coin', 'usercp/lojaitens' => 'chest', 'usercp/mercado' => 'market', 'usercp/resets' => 'reset', 'usercp/myaccount' => 'user',
                 'usercp/mypassword' => 'key', 'usercp/myemail' => 'mail', 'usercp/reset' => 'reset', 'usercp/unstick' => 'move', 'usercp/clearpk' => 'skull',
                 'usercp/resetstats' => 'eraser', 'usercp/addstats' => 'stats', 'usercp/clearskilltree' => 'tree', 'usercp/vote' => 'vote', 'usercp/buyzen' => 'coin',
                 'donation' => 'gift'];
        return $mapa[$link] ?? 'chevron-right';
    }

    /** Saldo de Cash da conta logada (0 se não der para ler); uma consulta por página, guardada. */
    public static function cashDaConta(?string $conta): int
    {
        static $cache = [];
        if ($conta === null || $conta === '') return 0;
        if (isset($cache[$conta])) return $cache[$conta];
        try {
            require_once(__DIR__ . '/MuChilaLoja.php');
            $st = MuChilaLoja::conectar()->prepare("SELECT ISNULL(WCoinC, 0) FROM CashShopData WHERE AccountID = ?");
            $st->execute([$conta]);
            return $cache[$conta] = (int)$st->fetchColumn();
        } catch (Throwable $e) {
            return $cache[$conta] = 0;
        }
    }
}

using MuChilaAdmin.Web;
using MuChilaAdmin.Web.Seguranca;

namespace MuChilaAdmin.Components.Layout;

/// <summary>Uma tela no menu. Politica null = qualquer usuário do painel vê.</summary>
public sealed record ItemMenu(string Grupo, string Titulo, string Href, string Icone, string? Politica, string Descricao);

/// <summary>Menu do painel (lateral e busca rápida usam a mesma lista).</summary>
public static class Navegacao
{
    public static readonly ItemMenu[] Itens =
    {
        new("Visão geral", "Dashboard", "", "grid", null, "Jogadores online, contas, vendas e situação do servidor"),
        new("Visão geral", "Alertas", "alertas", "bell", null, "Avisos do painel: servidor parado, mensagens, compras com problema"),

        new("Servidor", "Processos e jogadores", "servidor", "server", null, "Ligar, parar, recarregar e jogadores online"),
        new("Servidor", "Eventos", "eventos", "calendar", Politicas.Administrar, "Disparar Blood Castle, invasões e outros eventos"),
        new("Servidor", "Bônus de EXP e drop", "bonus", "zap", Politicas.Administrar, "EXP e drop em dobro por tempo"),
        new("Servidor", "Avisos aos jogadores", "avisos", "megaphone", Politicas.Moderar, "Mensagem na tela de todos e avisos automáticos"),
        new("Servidor", "Comandos do jogo", "comandos", "terminal", null, "O que cada comando faz"),

        new("Jogadores", "Contas", "contas", "users", null, "Contas, VIP, personagens, pontos e senha"),
        new("Jogadores", "Bans", "bans", "ban", Politicas.Moderar, "Banir com motivo e prazo, histórico"),
        new("Jogadores", "Itens e baú", "itens", "chest", Politicas.Moderar, "Inventário, baú e presentes de um jogador"),
        new("Jogadores", "Passe dos Mapas", "passe", "map", Politicas.Moderar, "Quem tem passe para os mapas acima do 400"),
        new("Jogadores", "Vendas", "vendas", "cart", Politicas.Moderar, "Compras de VIP/Cash, loja de itens, Zen e mercado"),

        new("Economia", "Lojas de NPC", "lojas", "market", Politicas.Administrar, "O que cada NPC vende"),
        new("Economia", "Loja de Cash", "cashshop", "coin", Politicas.Administrar, "Preços e itens da tecla X"),
        new("Economia", "Drops", "drops", "gift", Politicas.Administrar, "O que cada monstro dropa e com que chance"),
        new("Economia", "Taxas e opções", "opcoes", "sliders", Politicas.Administrar, "EXP, drop, zen e opções dos GameServers"),
        new("Economia", "EXP dinâmica", "exp", "activity", Politicas.Administrar, "% da EXP por faixa de nível"),
        new("Economia", "Resets", "resets", "reset", Politicas.Administrar, "Valores dos resets feitos pelo site"),

        new("Conteúdo", "Monstros (respawn)", "monstros", "skull", Politicas.Administrar, "Onde e quantos monstros nascem"),
        new("Conteúdo", "Itens novos", "itens-novos", "sparkle", Politicas.Administrar, "Criar item com o visual de outro"),

        new("Site", "Loja de itens", "site/loja-itens", "package", Politicas.Administrar, "Catálogo e preços da loja paga com Cash"),
        new("Site", "Notícias", "site/noticias", "newspaper", Politicas.Moderar, "Publicar notícias no site"),
        new("Site", "Páginas", "site/paginas", "file", Politicas.Administrar, "Termos, privacidade, reembolso e Contate-nos"),
        new("Site", "Comprar Zen", "site/zen", "coin", Politicas.Administrar, "Pacotes de Zen pagos com Cash"),
        new("Site", "Mensagens", "site/mensagens", "inbox", Politicas.Moderar, "Contate-nos: ler e responder"),

        new("Painel", "Relatórios", "relatorios", "chart", null, "Exportar CSV e PDF"),
        new("Painel", "Auditoria", "auditoria", "shield-check", Politicas.Administrar, "Quem fez o quê no painel"),
        new("Painel", "Usuários do painel", "usuarios", "user-plus", Politicas.Administrar, "Quem entra no painel e o que pode fazer"),
        new("Painel", "Minha conta", "minha-conta", "user", null, "Trocar a sua senha"),
    };

    public static IEnumerable<IGrouping<string, ItemMenu>> Grupos => Itens.GroupBy(i => i.Grupo);

    public static bool Pode(ItemMenu i, System.Security.Claims.ClaimsPrincipal u) => i.Politica switch
    {
        Politicas.Administrar => Identidade.Admin(u),
        Politicas.Moderar => Identidade.Moderador(u),
        _ => u.Identity?.IsAuthenticated == true,
    };
}

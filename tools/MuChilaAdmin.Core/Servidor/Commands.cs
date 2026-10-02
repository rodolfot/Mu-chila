namespace MuChilaAdmin.Core;

/// <summary>Um comando do GameServer, para a aba de consulta. "grupo" separa jogador de GM.</summary>
public record CommandInfo(string Cmd, string Grupo, string Descricao);

/// <summary>
/// Lista de comandos do chat do GameServer. É referência para consulta no painel. O nome de cada comando é uma mensagem do
/// Data\Lang\Portuguese.xml (IDs 32–58 e outros), que o GameServer lê ao ligar; Command.dat só liga/desliga e define custos.
/// Comandos de GM exigem nível de conta 32.
/// </summary>
public static class Commands
{
    public static readonly IReadOnlyList<CommandInfo> All = new List<CommandInfo>
    {
        // ---- jogador: atributos (mensagens 34–38 do Portuguese.xml; eram /addstr /addagi /addvit /addene /addcmd, issue #40) ----
        new("/f", "Jogador", "Adiciona pontos em Força. Ex.: /f 100."),
        new("/a", "Jogador", "Adiciona pontos em Agilidade. Ex.: /a 100."),
        new("/v", "Jogador", "Adiciona pontos em Vitalidade. Ex.: /v 100."),
        new("/e", "Jogador", "Adiciona pontos em Energia. Ex.: /e 100."),
        new("/c", "Jogador", "Adiciona pontos em Comando/Liderança (só Dark Lord). Ex.: /c 100."),
        // ---- jogador: geral ----
        new("/reset", "Jogador", "Reseta o personagem (nível 400 → 1) pelas regras do Command.dat."),
        new("/mreset", "Jogador", "Master Reset embutido do kit (grand-reset por contagem de resets; está desligado — o Master Reset oficial é pelo site)."),
        new("/change", "Jogador", "Evolui a classe, até a 4ª. Desligado desde 27/09 (evolução pelas missões)."),
        new("/ware", "Jogador", "/ware <número>: troca o baú da conta (0 = principal; extras até CommandWareNumber do plano). Com o baú fechado."),
        new("/money", "Jogador", "Mostra/gerencia o Zen conforme a config."),
        new("/pkclear", "Jogador", "Limpa o status de PK (assassino)."),
        new("/offattack", "Jogador", "Ataque automático offline. Só VIP; tempo e Zen por plano no Custom.dat."),
        new("/offstore", "Jogador", "Deixa a loja pessoal aberta com a conta offline."),
        new("/store", "Jogador", "Abre/gerencia a loja pessoal por comando."),
        new("/post", "Jogador", "Mensagem no chat do grupo/party."),
        new("/re", "Jogador", "Responde a última sussurro (mensagem privada)."),
        new("/hide", "Jogador", "Esconde/mostra o personagem para outros (conforme config)."),
        new("/war", "Jogador", "Inicia/aceita guerra de guildas."),
        new("/soccer", "Jogador", "Funções do evento de futebol."),
        new("/gremgif", "Jogador/GM", "Entrega/gera um presente do Gremory Case (usado para calibrar a recompensa)."),
        // ---- GM (nível de conta 32) ----
        new("/setlvl", "GM", "Define o nível de um personagem."),
        new("/msetlvl", "GM", "Define o Master Level."),
        new("/statstr", "GM", "Define a Força de um personagem."),
        new("/statagi", "GM", "Define a Agilidade."),
        new("/statvit", "GM", "Define a Vitalidade."),
        new("/statene", "GM", "Define a Energia."),
        new("/statcom", "GM", "Define o Comando/Liderança."),
        new("/make", "GM", "Cria um item pelo código."),
        new("/setmoney", "GM", "Define o Zen de um personagem."),
        new("/move", "GM", "Teleporta para um mapa/posição."),
        new("/gmmove", "GM", "Teleporta até um jogador (ou o traz)."),
        new("/notice", "GM", "Envia um aviso global (dourado)."),
        new("/gpost", "GM", "Aviso azul global."),
        new("/gmpost", "GM", "Mensagem de GM."),
        new("/disconnect", "GM", "Desconecta um jogador."),
        new("/banuser", "GM", "Bane uma conta."),
        new("/unbanuser", "GM", "Remove o banimento de uma conta."),
        new("/banmac", "GM", "Bane pelo endereço MAC."),
        new("/godmode", "GM", "Invencibilidade para o GM."),
        new("/attack", "GM", "Alterna se o GM pode atacar/ser atacado."),
        new("/debuff", "GM", "Remove efeitos negativos."),
        new("/addeffect", "GM", "Aplica um efeito/buff."),
        new("/clearinv", "GM", "Limpa o inventário."),
        new("/clearextinv", "GM", "Limpa o inventário estendido."),
        new("/clearinvmuun", "GM", "Limpa os itens Muun/pet."),
        new("/pkset", "GM", "Define o status de PK de um jogador."),
        new("/pack", "GM", "Empacota itens (ex.: joias)."),
        new("/unpack", "GM", "Desempacota itens."),
        new("/csstate", "GM", "Ajusta o estado do Castle Siege."),
        new("/chatblock", "GM", "Silencia um jogador no chat."),
        new("/unchatblock", "GM", "Remove o silêncio."),
        new("/fireworks", "GM", "Solta fogos de artifício."),
        new("/skin", "GM", "Assume a aparência de um monstro."),
        new("/track", "GM", "Localiza um jogador."),
        new("/trace", "GM", "Vai até um jogador."),
        new("/Buy", "GM", "Funções de compra/loja (debug)."),
        new("/Sell", "GM", "Funções de venda/loja (debug)."),
    };
}

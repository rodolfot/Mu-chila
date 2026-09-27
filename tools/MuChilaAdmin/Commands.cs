namespace MuChilaAdmin;

/// <summary>Um comando do GameServer, para a aba de consulta. "grupo" separa jogador de GM.</summary>
public record CommandInfo(string Cmd, string Grupo, string Descricao);

/// <summary>
/// Lista de comandos do chat do GameServer (achados nas tabelas do binário). É referência para consulta no painel;
/// a sintaxe fica no executável (o kit só liga/desliga). Comandos de GM exigem nível de conta 32.
/// </summary>
public static class Commands
{
    public static readonly IReadOnlyList<CommandInfo> All = new List<CommandInfo>
    {
        // ---- jogador: atributos (podem virar /f /a /v /e /c com os comandos curtos do vigia) ----
        new("/addstr", "Jogador", "Adiciona pontos em Força. Ex.: /addstr 100. (curto: /f)"),
        new("/addagi", "Jogador", "Adiciona pontos em Agilidade. Ex.: /addagi 100. (curto: /a)"),
        new("/addvit", "Jogador", "Adiciona pontos em Vitalidade. Ex.: /addvit 100. (curto: /v)"),
        new("/addene", "Jogador", "Adiciona pontos em Energia. Ex.: /addene 100. (curto: /e)"),
        new("/addcmd", "Jogador", "Adiciona pontos em Comando/Liderança (só Dark Lord). (curto: /c)"),
        // ---- jogador: geral ----
        new("/reset", "Jogador", "Reseta o personagem (nível 400 → 1) pelas regras do Command.dat."),
        new("/mreset", "Jogador", "Master Reset embutido do kit (grand-reset por contagem de resets; está desligado — o Master Reset oficial é pelo site)."),
        new("/change", "Jogador", "Evolui a classe (1ª → 2ª → 3ª), grátis."),
        new("/ware", "Jogador", "Abre o baú (warehouse) à distância."),
        new("/money", "Jogador", "Mostra/gerencia o Zen conforme a config."),
        new("/pkclear", "Jogador", "Limpa o status de PK (assassino)."),
        new("/offattack", "Jogador", "Liga o ataque automático offline (limites por plano no Custom.dat)."),
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

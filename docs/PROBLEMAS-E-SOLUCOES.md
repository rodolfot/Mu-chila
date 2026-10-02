# Problemas e soluções

Problemas encontrados na instalação e no teste, com a causa diagnosticada e a solução aplicada.
No fim, as pendências que ainda estão abertas.

## Resolvidos

### Cliente: "You are disconnected from the server" logo na tela de login
- **Causa:** o `main.exe` tem uma trava que recusa conectar em `127.0.0.1`. Ele compara o IP byte a byte e desiste antes de chamar `connect()`. No `Logs\Error.log` aparece `Failed to connect`.
- **Solução:** usar outro IP para o cliente. Para jogar só na máquina, `127.0.0.2`; para amigos, o IP do Radmin. Veja [INSTALACAO.md §6](INSTALACAO.md#6-ips).

### Cliente trava ao ir para um mapa de evento ou do Castle Siege
- **Causa:** o GameServer descarta o primeiro caractere do IP em `Data\MapServerInfo.dat`. O kit usa `STEUIPAQUI`, com um "S" de propósito. Sem ele, o cliente é mandado para `27.0.0.2`.
- **Solução:** manter a letra na frente, como em `S26.139.39.123`.

### Castle Siege Server fecha ao iniciar
- **Causa:** o erro `CheckSync() - iEVENT_END_DATE_NUM (2020-01-23) <= iTODAY_DATE_NUM`. O backup veio com as datas do cerco em 2020.
- **Solução:** `UPDATE` em `MuCastle_DATA` com o ciclo de 7 dias a partir de hoje (veja INSTALACAO §4).
- **Atenção:** se o servidor ficar desligado depois da data de fim, o erro volta. Rode o `UPDATE` de novo.

### DataServer: `Não foi possível encontrar o procedimento armazenado 'GremoryCaseGetItemList'` / `'WZ_GetLoadRestoreInventory'`
- **Causa:** o backup `MuOnlineS14` veio sem os objetos do Gremory Case e do Restore Item. O site do kit não tem scripts para isso.
- **Solução:** scripts em `DB\1 - Querys\Correcoes (Gremory e RestoreItem)`.
  - **Gremory:** esquema IGCN, idêntico à tabela que existe no banco BattleCore do kit.
  - **Restore Item:** procedimentos originais da Webzen, tirados de um banco Season 13 de referência.
- **Nomes de procedimento:** só o `GremoryCaseGetItemList` foi confirmado pelo log. Os outros três Gremory foram deduzidos. Se o DataServer registrar outro nome faltando, crie um apelido.

### Servidores não achavam o banco (ODBC)
- **Causa:** os `.reg` do kit criam o DSN com o nome errado (`MuOnline`), em 64 bits e com `Server=(local)`.
- **Solução:** DSNs de 32 bits criados com `Add-OdbcDsn` (INSTALACAO §5).

### Desconecta depois de ~30 s na tela de login
- **Causa:** o GameServer registra `[CloseClient] CloseType:79 / Response error after connection causes conclusion`. A conexão que não faz login em ~30 s é derrubada. O limite é fixo no executável, então é comportamento normal.
- **Solução:** fazer o login logo depois de escolher o servidor.

### "Não é possível dropar" certos itens
- **Causa:** `Data\Item\ItemMove.txt` do kit proíbe largar no chão asas, joias, pedras de refino e talismãs (`AllowDrop = 0`).
- **Solução:** é regra do kit, não bug. Para mudar, veja OPERACAO.md.

### Caixas que não abrem (`[CGOpenBoxRecv] [Error] [Box Type] Unknown box identifier`)
- **Causa:** a abertura com o botão direito do S14 envia um identificador de caixa que o GameServer MuDevs FREE não conhece. O `EventItemBagManager` não é consultado nesse caminho, e não existe configuração para ele. Os prêmios criados nas entradas 236 a 254 não tiveram efeito e foram removidos.
- **Solução:** as 19 caixas (Earring of Wrath Box, [Speed] Earring Box, Ruud Box, Gift Box, Mastery Box, Chicken Box...) **deixaram de cair dos monstros** (`DropItem = 0` no `Item.txt`).
- **Limpeza:** as que estavam em inventários, inventários de evento e baús foram removidas com `tools\Remover-ItensSemSuporte.ps1`, que só mexe em contas offline há mais de 1 minuto e guarda backup em `C:\MuServer\DB\backup-caixas-*.csv`.

### Item laranja sem nome que não vende, não usa e não dropa ("Esses itens não podem ser trocados")
- **O que é:** um **cartão de personagem** (Summoner, Rage Fighter, Grow Lancer ou Rune Mage Character Card: 7259, 7337, 7449, 7655).
  - No MU oficial ele libera a criação da classe na conta.
  - Aqui todas as classes já podem ser criadas sem ele (`AccountCharacter.ExtClass = 0` em todas as contas, e mesmo assim há personagens dessas classes).
  - O cliente o trata como intransferível, e o servidor não faz nada ao usá-lo.
- **Solução:**
  - o Rune Mage Character Card, que caía de monstros, passou a ter `DropItem = 0`;
  - os cartões existentes são removidos com `tools\Remover-ItensSemSuporte.ps1`, só com o personagem offline.

### Equipamento que não vende, não troca e não dropa (ex.: Legendary Rune Boots)
- **Causa:** o cliente tem permissões por item. Em `Data\Local\Eng\item_eng.bmd` são registros de 672 bytes, com o código do item nos bytes 0–1 e as permissões nos bytes 661–667; o XOR `FC CF AB` recomeça em cada registro.
  - As versões "presas" de alguns equipamentos têm tudo zerado: Legendary Rune 125 e (Bound), Rune Sphinx, Storm Jahad, [Bound] Wings of Disillusion...
  - O servidor deixava esses itens caírem de monstros. Quem pegava ficava com um item que nunca mais saía do inventário.
- **Solução:**
  - os **21 equipamentos presos** passaram a ter `DropItem = 0`. As versões normais, negociáveis, continuam caindo;
  - os 3 itens de missão presos (Elena's Letter, Key of Dimension, Switch Scroll) continuam caindo;
  - para tirar um item específico de alguém: `tools\Remover-ItensSemSuporte.ps1 -Contas <conta> -Codigos <código>`, com a conta offline.

### NPC responde "Could not find message 501/502" (ou 248 etc.)
- **Causa:** o servidor busca as mensagens no idioma 0 do personagem. Com o inglês desativado e o português só no idioma 2, as mensagens sumiram.
- **Solução:** no `LangManager.xml`, os idiomas 0 e 2 apontam para o `Portuguese.xml`.
- **O que 501/502 significam:** são as falas de NPC para jogador **PK** ("Não vendo nada para gente como você"). O jogador precisa usar `/pkclear`.

### Aviso de invasão aparece como "Could not find message 194" (ou 197, 200...)
- **Sintoma:** só os avisos de invasão falham: início (192–200, 210, 211) e chefe derrotado (202–207). As outras mensagens do servidor funcionam.
- **Causa:** o GameServer procura os avisos do `InvasionManager.dat` (colunas `RespawnMessage` e `BossMessage`) na seção `<InvacionMsg>` do arquivo de idioma. O kit usa os IDs 192–211, que só existiam na seção `<Message>`; a `<InvacionMsg>` tinha só os IDs 0–12. O inglês do kit tinha o mesmo defeito.
  - Não era o acento. Conferido na memória do GameServer: a tabela `Message` tinha as 505 mensagens em UTF-8, com a 197 correta; as tabelas `InvacionMsg` tinham 13 entradas, sem a 197.
- **Solução:** as 17 mensagens de invasão foram copiadas para a `<InvacionMsg>` do `Portuguese.xml` e do `English.xml`, com Reload Common no GameServer e no Castle Siege. O `InvasionManager.dat` não mudou.

### Personagem dá "Miss" em tudo e a janela (C) mostra "Suces de Atq" ≈ -2147479000
- **Sintoma:** "Suces de Atq(%)", "Ataq PvP (%)" e "Def PvP (%)" negativos, perto de -2.147.483.648, mesmo sem nenhum item equipado. O personagem erra quase todos os golpes, até em monstros de Lorencia.
- **Causa (conferida na memória do GameServer):** o Quest1 tinha 1 ponto em cada uma das habilidades master 301 "Add Defense Success Rate PvP", 325 "Add Attack Success Rate" e 347 "Add Attack Success Rate PvP". O bônus delas saía como -2³¹ e era somado justamente a esses três atributos. A base calculada estava certa (3.923).
  - O banco guarda o nível da habilidade a partir de 0: `00` = 1 ponto, `09` = 10 pontos. Registro de 3 bytes por habilidade (`índice baixo, nível, índice alto`); `FF 00 FF` = vazio.
  - Não é a 4ª classe abaixo do 400 nem o Master Level: a mamaeupo está assim e acerta normalmente, com 10 pontos nas mesmas habilidades.
  - **Não se reproduz jogando normalmente.** Testado no YolaxD (24/09/2026): 1 e 2 pontos em "Add Attack Success Rate" dão o bônus certo (+511 com 2 pontos), e `/reset` feito com habilidades aprendidas mantém tudo normal.
  - O Quest1 é um personagem que veio pronto no kit (13 milhões de experiência master e 1 reset de fábrica). Antes da correção, a memória mostrava o Master Level dele valendo **-3**. O estado inválido provavelmente veio desses dados de fábrica junto com o reset.
- **Solução:** no **Mu Chila Admin**, página **Contas**, abra a conta e use **"Zerar habilidades master"**. A conta precisa estar offline. As habilidades são apagadas, os pontos master voltam a ficar livres (1 por Master Level) e o estado anterior fica em `C:\MuServer\DB\backup-caixas-master-habilidades.csv`.
  - Sem abrir a janela: `C:\MuServer\MuChilaAdmin\MuChilaAdmin.exe --zerar-master <conta> <personagem> <arquivo-de-resultado>`.
  - Feito no Quest1: acerto 3.923, acerto PvP 8.484, defesa PvP 1.006.

### GameServer trava ao carregar e o cliente não mostra o servidor (24/09/2026)
- **Sintoma:** depois de um Reload EventItemBag, o GameServer parou de responder. Ao religar, o log parou em `Event loaded successfully` e o ConnectServer não recebeu o `GameServer online`. No cliente, o botão do servidor não aparecia.
- **Causa:** dois arquivos novos em `Data\EventItemBag` (caixas Season Level Reward A/B) foram gravados com o texto literal `` `t `` no lugar de TAB, por erro no script que os criou. O carregador do MuDevs não trata a linha malformada e fica preso nela.
- **Solução:** `EventItemBagManager.txt` voltou ao backup, os dois arquivos foram renomeados para `.desativado-*` e o GameServer e o Castle Siege foram religados. As caixas Season Level Reward continuam sem conteúdo, como no kit.
- **Regra:** depois de mexer em qualquer arquivo de `Data`, confira no LOG se aparece o `loaded successfully` daquele item antes de dar o assunto por encerrado.

### Janela de atributos (C) não atualiza os pontos depois de `/addstr` ou `/reset` (issues #1, #6, #7)
- **Sintoma:** depois de `/addstr` (e `/addagi`, `/addvit`...), a força etc. mudam, mas "Pontos restantes" continua com o valor antigo. Depois do `/reset`, a janela continua mostrando o nível antigo. Suspeita para a #1 (janela travada): clicar no "+" com pontos que já não existem, o servidor recusa e a janela fica esperando.
- **Causa (lida no código do GameServer):** o `/addstr` (`0x445D40`) soma o atributo, desconta os pontos (`+0x90`) e recalcula o personagem. O cliente recebe só o pacote `C1 1C EC 25`, com força, agilidade, vitalidade, energia e comando (base e adicional), **sem os pontos livres**. O cliente só recebe os pontos livres ao subir de nível ou ao entrar no personagem.
- Os valores no servidor estão certos; é só a tela. Não há opção de configuração para mandar os pontos, e corrigir exigiria alterar o executável do servidor ou do cliente, que são fechados e protegidos.
- **Contorno:** depois de `/addstr` ou `/reset`, trocar de personagem (ou relogar) antes de abrir a janela C. Os botões "+" da própria janela atualizam certo.

### Mensagens do servidor com acento aparecem como "invasÃ£o", "comeÃ§ou"
- **Causa:** o `Portuguese.xml` estava em UTF-8. O servidor repassa os bytes do texto sem converter, e o cliente lê em Windows-1252, então cada letra acentuada (2 bytes em UTF-8) vira dois caracteres.
- **Solução:** o arquivo foi salvo em **Windows-1252**, mantendo o cabeçalho `encoding="utf-8"`. Conferido na memória do GameServer: as 505 mensagens carregam, com os acentos em 1 byte (`ã` = E3).
- Numa sessão anterior a documentação atribuía o "Could not find message" ao Windows-1252. Estava errado: aquele erro era a seção `<InvacionMsg>` (item acima).

### Caixas (Chicken Box, Earring Box...) não sobem do chão nem descem para o chão
- **Causa:** as caixas de evento ficam no **Inventário de Evento**. No `GameServerInfo - Common.dat`, `EventInventoryExpireYear/Month/Day` vinha como **31/12/2015**; com a data vencida, o servidor trata esse inventário como fechado e recusa, sem mensagem, pegar ou mover esses itens.
- **Solução:** validade em **31/12/2037** nos três GameServers, aplicada com Reload Common. Talvez seja preciso sair e entrar no personagem para o cliente mostrar o inventário de evento. O ano 2037 é o limite seguro para executáveis 32 bits antigos, por causa do problema de 2038.

### Árvore master mostra "10/20", mas não passa do 10; Defense Increase com "+445%" (issue #36)
- **Causa:** erros da tradução em português do cliente (`Data\Local\Por\masterskilltooltip_por.bmd`).
  - 11 habilidades vão só até o nível 10: o servidor (`Data\Skill\MasterSkillTree.txt`) e o próprio cliente (`Data\Local\masterskilltreedata.bmd`) concordam, e a dica em inglês diz "/10". A em português dizia "/20". As habilidades: 353, 400, 418, 425, 426, 427, 430, 432, 438, 468 e 695 (ex.: One-handed Sword Mastery). O "Você não pode aumentar mais níveis" vem do cliente, que para no 10.
  - Dicas que **somam pontos** apareciam como porcentagem. A Defense Increase (309) dá **+445 de defesa** no nível 20, não 445%: em inglês é "Defense increases by %d". O mesmo nas habilidades 322, 375, 412, 447, 478, 549 e 550.
  - Descrições quebradas ou com um número a mais, que mostrariam lixo (392 a 395, 529, 574 e 577). Duas sem o "%" (415, 517). A 662 dizia "/10" com máximo 20.
  - A habilidade 470 ia até 20 no cliente e até 10 no servidor.
- **Solução (01/10/2026):** `tools\Corrigir-DicasArvoreMaster.ps1`.
  - Troca só os textos (os marcadores `%d`/`%0.2f` continuam iguais) e põe o máximo da 470 em 10 no cliente.
  - Recalcula a soma de verificação (chave 0x2BC1) e faz backup.
  - `-SoConferir` mostra o que mudaria. Rodar de novo não muda nada.
  - Os jogadores recebem pelo launcher (`Publicar-Launcher.ps1`).
  - **Falta conferir no jogo.**
- **Ficou como está:** 563 e 753 vão até 20 no servidor, mas o cliente para no 10 e a dica diz "/10" (o jogador vê 10/10, coerente).

## Pendências

### Skill evoluída (Twisting Slash Strengthener etc.) para de causar dano (issue #12) — RESOLVIDO (contorno no servidor)
- **Solução aplicada (25/09/2026):** o **vigia** do Mu Chila Admin (`MuChilaAdmin.exe --vigia-reset`, liga com o Windows e com o painel) acha no GameServer e no Castle Siege as 6 chamadas à checagem anti-hack de ataques do MuDevs e troca o desvio "descartar" por NOPs (a checagem continua rodando; só não descarta mais o ataque). É feito só na memória, cada vez que o vigia encontra um servidor novo (inclusive depois de reiniciar), conferindo os bytes antes; em versão diferente do executável ele não mexe. Registro em `C:\MuServer\MuChilaAdmin\vigia.log`; conferir com `MuChilaAdmin.exe --vigia-sondar <arquivo>`.
- **Custo:** fica sem a proteção anti-hack de velocidade de ataque do MuDevs (as outras checagens de velocidade — `CheckSpeedHack` etc. — já estavam desligadas no kit).
- **Tentativa que não serviu:** trocar o `Data\Character\Player.bmd` do servidor (BMD 0x0C) pelo do jogo (0x0E). O GameServer **fechou** ao recarregar ("Reload Character") às 21:40 de 25/09: ele não lê o formato novo. O original voltou (backup `Player.bmd.bak-20260925-214027`) e o GameServer foi religado às 21:40:53.
- **Sintoma:** com habilidades evoluídas na árvore master, depois de menos de 1 minuto usando a skill ela só faz a animação, sem efeito e sem dano. Os atributos da janela C continuam normais (não é o "Miss" do Quest1).
- **Descartado (25/09/2026):**
  - Configuração: `Skill.txt` e `MasterSkillTree.txt` coerentes (cada evolução substitui a skill certa: 326→22, 330→41, 331→42, 336→43, 339→336, 346→344; classe exigida compatível).
  - Fórmulas (`Formula.txt`): as 35 habilidades master da mamaeupo dão valores normais de 0 a 20 pontos; nenhum NaN, infinito, zero ou estouro.
  - Anti-hack: `CheckSpeedHack`, `CheckLatencyHack`, `CheckAutoPotionHack`, `CheckAutoComboHack` desligados. O `HackPacketCheck` do kit só limita os pacotes 0 e 2 (chat), 0x10, 0x18 (15/20 por segundo), 0xD7 e 0xDF (30/40 por segundo), e não registrou nada desses tipos no horário.
  - Estado do personagem: o monitor de memória (1 foto/s) no teste das 19:38–19:39 mostrou o servidor aceitando golpes, gastando mana/AG e dando experiência até a personagem morrer às 19:40:00; nenhum campo travou ou congelou.
- **Rotinas do GameServer por pacote** (para as próximas análises): 0x1E → `0x50B7A0` (skill de duração: cobra mana/AG e confirma); 0xDF → `0x50B210` (lista de alvos atingidos, aplica o dano); 0x11 → `0x415510` (golpe normal); 0x19 → `0x50B550`; checagem de pacotes `0x475940`. A `0x50B7A0` chama no fim uma checagem protegida (código virtualizado) do MuDevs, `0x40ED40`, que usa dois números enviados pelo jogo e um objeto por jogador com as animações do `Data\Character\Player.bmd` (o do servidor é BMD versão 0x0C de 2018; o do jogo é versão 0x0E de 2019). Ainda **sem evidência** de que ela cause o defeito.
- **Capturas de 25/09 a partir de 20:38 NÃO valem**: foram feitas registrando pacotes pelo `HackPacketCheck`, e essa rotina **descarta** todo pacote que passa do `MinCount` (e desconecta ao chegar no `MaxCount`). Com o mínimo baixo, o próprio registro fez o servidor ignorar os pedidos de skill — não era o defeito.
- **Incidente (25/09, 21:03–21:08):** com o registro ligado para todos os tipos de pacote, o pacote de identificação do computador (0xF3) passou a ser descartado no login e o servidor recusou as entradas (`HardwareIdSystem: HardwareIdStatus == false`, `CloseType:73`). Resolvido voltando o `HackPacketCheck.txt` original e `Reload Hack`. **Não usar o `HackPacketCheck` para registrar pacotes.**
- **Contorno relatado:** voltar para a seleção de personagem (ou relogar) às vezes faz a skill voltar a dar dano.
- **CAUSA COMPROVADA (25/09/2026, 21:30):**
  - Captura de rede sem interferência (`pktmon`, só observando): o jogo da Mario manda o pedido de skill (pacote de 27 bytes, 1 por segundo) e o servidor recebe, mas não executa (mana/AG não descontados, nenhum monstro morre). Os pacotes têm uma camada de XOR própria do MuDevs por cima do protocolo do MU.
  - Todas as rotinas de ataque do GameServer chamam a checagem protegida `0x40ED40` com dois números do pacote: golpe normal (0x11, tipo 3), skill num alvo (0x19, tipo 1), skill de duração (0x1E, tipo 2), lista de alvos (0xDF, tipo 6) e outras duas (tipos 4 e 5). Quando ela recusa um jogador, **param as skills evoluídas, o golpe básico e as outras skills juntos** — o sintoma relatado.
  - Teste autorizado pelo dono, só na memória: com a recusa da `0x40ED40` desligada no pedido 0x1E (`0x50B920`: `74 3B` → `90 90`) por 75 s, a mamaeupo teve **48 usos de skill com mana gasta e 14 monstros mortos**; nos 15 s antes e depois, com a checagem ativa, **0 e 0**. Bytes originais devolvidos e conferidos.
  - A `0x40ED40` é o anti-hack de ataques do MuDevs (código virtualizado); o objeto por jogador que ela usa (`[obj+0xD34]`) é o modelo/animações do personagem (`player.smd`, lido de `Data\Character\Player.bmd`, versão BMD 0x0C de 2018, enquanto o jogo usa `Data\Player\player.bmd` versão 0x0E de 2019). Hipótese para o motivo da recusa: animações do servidor diferentes das do jogo nas skills evoluídas.

### Confirmar as caixas no jogo
- Testar se Chicken Box e Earring Box sobem do chão depois da correção do Inventário de Evento.
- Testar se, ao abrir, dão o item inicial +6.

### Moss Merchant disparado pelo painel não aparecia (issue #43)
- **Sintoma:** ao disparar o Moss pela página Eventos, nada acontecia, nem o aviso.
- **Causa:** é o mesmo caso das invasões. O painel gravava uma linha no `MossMerchant.dat` e dava Reload Event, mas o GameServer só calcula o próximo horário do Moss ao ligar e quando ele vai embora.
  - No código (0x4B9F00 em diante), o objeto do evento é `0x2E59700`: +04 estado (1 esperando, 2 aberto), +08 segundos que faltam, +0C horário-alvo, +14 NPC.
  - Com o estado 1 e o horário passado, o servidor manda a mensagem 208, cria o Moss em Elbeland e abre a loja.
- **Solução:**
  - O painel grava o horário-alvo na memória dos GameServers (`ResetWatcher.Moss.cs`), como já faz com as invasões. O endereço sai do código pelos bytes, sem número fixo.
  - Testado nos bytes reais do GameServer: acha `0x2E59700`.
  - **Falta conferir no jogo:** disparar com 1 minuto e ver o aviso e o NPC em Elbeland.
- Como o Moss funciona: OPERACAO, "Moss Merchant".

### Invasão "Demônios invocados" pelo painel (issue #42)
- **O que a invasão faz:** a invasão 9 do kit não tem demônios. Ela põe 10 **Golden Goblins nível 100** (5 nos Karutans) em **um** mapa sorteado entre Tarkan, Aida, Kanturu 1, Karutan 1, Karutan 2 e Acheron 1. O aviso ("A invasão dos Demônios invocados começou!") não diz qual mapa.
- **O disparo:** é igual ao da Páscoa (horário na memória), que foi conferido em 29/09 com 10 coelhos. A configuração da invasão 9 tem a mesma forma da Páscoa.
- **O que mudou:**
  - a página Eventos mostra o estado real de cada invasão (acontecendo até / próximo / desligado);
  - o resultado do disparo lista o aviso, os monstros e os mapas possíveis.
  - Se o resultado disser "invasões desligadas neste servidor" ou "não conferiu na memória", é outro problema: mandar o print.
- **Falta conferir no jogo:** disparar, esperar o aviso e procurar os Golden Goblins nos 6 mapas. O `MonsterCount` do título da janela do GameServer sobe 10 (ou 5).

### Comandos curtos `/f /a /v /e /c` não funcionavam (issue #40)
- **Sintoma:** `/f 100` etc. não faziam nada e não mostravam erro. Desde 27/09 o vigia "renomeava" `/addstr` → `/f` na memória do GameServer. Isso nunca foi testado no jogo.
- **Causa:** o nome de cada comando vem do `Data\Lang\Portuguese.xml` (mensagens 34–38), lido quando o GameServer liga.
  - O vigia trocava o texto na tabela de mensagens já carregada, mas a busca do comando não passa a reconhecer o nome novo.
  - Um "Reload Common" relê as mensagens e desfaz a troca.
- **Solução:**
  - Mensagens 34–38 trocadas no `Portuguese.xml` (e no `English.xml`): `/f /a /v /e /c`. O GameServer compara a palavra inteira; `/re` e `/reset` já convivem no kit.
  - A troca na memória saiu do vigia. O `Publicar-Painel` apaga a chave antiga `vigia-comandos-curtos.ligado`.
  - A página Informações do site e a aba Comandos do painel mostram os nomes que estão no XML.
- **Falta conferir no jogo**, depois de copiar o XML para `C:\MuServer\Data\Lang` e reiniciar os GameServers: `/f 10` soma 10 de força. Os pontos livres da janela C só atualizam ao trocar de personagem; ver acima.

### Personagem novo aparece como "Blade Knight" no cliente, mas é Dark Knight no servidor
- **Causa:** o cliente S14 mostra a classe básica e a 2ª classe com o mesmo nome (16 e 17 aparecem ambos como "Blade Knight"). O servidor, porém, cria o personagem na básica (+0). Com isso, o cliente oferecia itens e skills de 2ª classe que o servidor recusava ("Não pode vestir o item").
- **Primeira tentativa (24/09/2026, desfeita em 01/10/2026):**
  - o gatilho `TR_MuChila_ClasseInicial` na tabela `Character` fazia DW, DK, Elfa e Summoner nascerem na 2ª classe (+1, igual ao `/change`);
  - os personagens que estavam na 1ª classe subiram +1;
  - em 01/10 o gatilho passou também a marcar como concluídas as missões da 2ª classe (índices 0 e 1) e a dar 20 pontos.
- **Decisão do dono (issue #35, 01/10/2026):** o personagem **nasce na 1ª classe** e só passa para a 2ª fazendo a missão. O erro era só o nome que o jogo mostra.
- **Solução atual:**
  - O gatilho é removido por `DB\1 - Querys\Correcoes (Gremory e RestoreItem)\ClasseInicial_1aClasse.sql`.
  - `DB\1 - Querys\MuChila-Issue35-VoltarPara1aClasse-ver.sql` lista quem volta. `-aplicar.sql` faz a volta e remove o gatilho. Rodar uma vez, com o servidor desligado; as contas online são puladas e ficam na lista, e basta rodar de novo depois.
  - Quem volta para a 1ª classe:
    - **A:** 2ª classe (1, 17, 33, 81) sem as missões 0 e 1 concluídas (os 2 bits mais baixos de cada uma no 1º byte de `Character.Quest`, nibble `0xA`);
    - **B:** criados a partir de 01/10/2026 com as missões marcadas pelo gatilho. As missões são desmarcadas e saem os 20 pontos ainda não distribuídos;
    - **C:** igual ao B, mas já na 1ª classe (o personagem de teste `fggg`): só as missões e os pontos.
  - Quem fez a missão (nibble `0xA` e criado antes de 01/10) ou já está na 3ª/4ª classe não muda.
  - Testado no banco de desenvolvimento com personagens criados pelo `WZ_CreateCharacter`: os casos A, B e C, conta online pulada, segunda e terceira rodadas, os personagens do kit sem mudança, e personagem novo nascendo na classe 16 com as missões zeradas.
  - **No servidor, o `ClasseInicial_MissoesDa2aClasse_existentes.sql` já tinha rodado** (Etapa 6 da implantação de 01/10). Ele marcou as missões 0 e 1 dos personagens antigos de 2ª classe e deu 10 pontos por missão, e por isso eles não entram no caso A.
    - Quem desfaz é o `tools\Desfazer-MissoesDa2aClasse.ps1`. Ele restaura um backup de `D:\MuServerBackup\DB` como `MuChila_Antes35`, só para comparar, e apaga a cópia no fim.
    - Ele pega o personagem que no backup estava na 2ª classe sem as missões e agora está na mesma classe com as duas marcadas. Esse personagem volta para a 1ª classe, as missões 0 e 1 voltam ao que eram no backup e saem os pontos ainda não distribuídos.
    - Sem `-Aplicar` só mostra a lista. Sem `-Backup`, tenta os backups do mais novo para o mais antigo: um backup de depois da marcação não mostra ninguém, então o script passa para o anterior.
    - Testado no banco de desenvolvimento simulando a sequência do servidor:
      - backup antes, script antigo e backup depois;
      - o backup de depois é ignorado;
      - conta online pulada, segunda e terceira rodadas;
      - a missão 0 feita sozinha (`FE`) volta como estava;
      - quem passou para a 3ª classe e quem fez a missão de verdade não mudam.
- **O nome no jogo: causa no GameServer (achada em 01/10/2026).**
  - O "Blade Knight" aparece para a classe 16 em português e em inglês. Isso mostra que o texto não é o problema.
  - O cliente funciona certo. Ele recebe um byte de classe: família nos bits 4–7, e 08 = 2ª classe, 0C = 3ª e 0E = 4ª.
    - Esse byte é traduzido em `0x9CBE62` do `main.exe`. O nome sai de `0x9CC288`, que usa os textos 20–27 (Dark Wizard, Dark Knight, Fairy Elf, Magic Gladiator, Dark Lord, Soul Master, Blade Knight, Muse Elf) e 1668+.
    - O cliente tem os nomes da 1ª classe.
  - **O erro é do GameServer.** Ao montar esse byte, ele faz: estágio 1 → 08, 2 → 0C, 3 → 0E e **qualquer outro → 08**. O estágio 0 (1ª classe) cai no "qualquer outro" e vai como 2ª classe.
  - Por isso o jogo mostra "Blade Knight" e deixa tentar vestir itens de 2ª classe, que o servidor recusa.
  - São 4 pontos no `Game Server S14.exe`:
    - `0x45DBB1`: lista de personagens (`F3 00`);
    - `0x45E429`: criação (`F3 01`);
    - `0x4C83D2`: aparência vista pelos outros jogadores;
    - `0x4E6C72`: entrada no jogo.
  - Em todos aparece a mesma sequência: `cmp r8,2 / jne / mov r32,0Ch / jmp / cmp r8,3 / mov r32,08h / mov r32,0Eh / cmove`.
  - **Correção:**
    - O vigia troca o `08h` desse `mov` por `00h`, sempre que acha um GameServer ou Castle Siege. O código está em `ResetWatcher.Classe.cs`, e a chave é `vigia-classe-inicial.ligado`.
    - Ele procura a sequência inteira (28 bytes). Se não achar, não mexe e registra no `vigia.log`. No GameServer desembrulhado, a busca acha exatamente os 4 pontos.
    - Para desfazer: apagar a chave e reiniciar o GameServer.
  - MG, DL, RF e GL não mudam na prática: o nome deles não depende desse bit, e no `Item.txt` os itens deles só pedem 0, 1, 3 ou 4, nunca 2.
  - **Falta conferir no jogo:** o `fggg` (classe 16) deve aparecer como Dark Knight, e a Brova deve aparecer como item que ele não pode usar.
  - O Castle Siege Server é outro executável. Não deu para conferir aqui, porque o Windows Defender bloqueia a cópia, mas o vigia procura a mesma sequência nele (`--vigia-sondar` mostra o que achou).
  - O BattleCore Server não é vigiado.
  - Como o código foi lido:
    - o `main.exe` e o GameServer são protegidos (embaralhados no arquivo, entropia 8,0) e se decifram na memória ao abrir;
    - o código foi copiado da memória com o programa aberto por alguns segundos (cliente com `__COMPAT_LAYER=RunAsInvoker`, sem pedir administrador) e lido com o desmontador Iced.
  - Os textos do cliente ficam em `Data\Lang.mpr`: um ZIP com cada byte em XOR com `20 13 77` (pela posição, de 3 em 3), com senha (ZipCrypto) nas entradas.
  - Desde 27/09/2026 o `/change` está desligado: a evolução é pelas missões (ver OPERACAO.md).

### Item comprado não pode ser usado / habilidades não funcionam em algumas contas (provável causa única)
- **Caso da mamaeupo:**
  - a "Beuroba +2" é a **Brova** do servidor (item 3,11; o cliente usa outro nome);
  - ela exige a **2ª classe de Dark Knight** (Blade Knight): o marcador `2` na coluna DK do `Item.txt`;
  - a mamaeupo continua **Dark Knight básico** (classe 16) no nível 249.
- **Padrão geral:** todos os personagens novos estão na classe básica (dodo 16, Maloco 48, MalocoBR 0). Os das contas do kit, onde "funciona", já estão evoluídos.
- **Solução (na época):** digitar **`/change`** no chat para evoluir. Desde 27/09/2026 o `/change` está desligado e a evolução é pelas missões; veja OPERACAO.md.
- **Confirmar:** se as habilidades continuarem falhando depois da evolução, investigar de novo.
- O cliente e o servidor usam nomes diferentes para alguns itens. Para achar o código de um nome do cliente, busque no `Data\Local\Eng\itemtooltip_eng.bmd`: registros de 124 bytes, XOR `FC CF AB` por registro, `WORD seção` + `WORD índice` + nome.

### Mercado às vezes não abre
- **O que foi verificado:**
  - nenhum erro de loja no GameServer nem no DataServer;
  - nenhum pacote da loja (`0xD2`) rejeitado;
  - as versões dos scripts da Cash Shop batem entre cliente e servidor (`512.2011.006` e `583.2010.005`).
- **Falta saber:** qual "mercado" é (Cash Shop, loja de NPC, loja pessoal ou mapa Loren Market) e o horário em que falhou, para cruzar com os logs.

### Outros avisos conhecidos (sem impacto no jogo)
- **Pacote desconhecido:** o GameServer registra `PacketUnk Head 0xF6 Sub 71`, do sistema de quests/guia.
- **Item com código inválido:** o cliente registra `Not Exist ItemScriptData[0, -1]`.
- **Brincos:** usam um espaço de equipamento (237/238) que não existe no inventário deste servidor. Provavelmente não podem ser equipados.
- **Driver de vídeo:** o Windows registrou `LiveKernelEvent 141/1b8`, travamentos momentâneos do driver de vídeo, com o cliente aberto em 1920x1080.

## Como diagnosticar

- **Logs dos servidores:** `C:\MuServer\<servidor>\LOG\<data>.txt`. O DataServer mostra o SQL que falhou.
- **Log do cliente:** `.\tools\Decifrar-LogCliente.ps1`. Ele mostra o IP e a porta que o cliente tentou e o motivo da falha.
- **Contagem de jogadores e monstros:** está no título da janela de cada GameServer.

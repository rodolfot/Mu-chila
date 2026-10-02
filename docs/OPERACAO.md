# Operação do dia a dia

## Ligar e desligar

- **Ligar:** Radmin VPN conectado → `C:\MuServer\Startup - Iniciar Server.lnk` → iniciar. Espere todas as linhas ficarem verdes (amarelo = inicializando).
- **Desligar com jogadores online:** menu `File` do GameServer → "1/3/5 Minute(s) Server Close". Os jogadores recebem aviso.
- O launcher **não** religa servidores que caírem nem inicia sozinho quando é aberto.
- **Site** (cadastro, rankings, loja): `C:\MuServer\Site\Ligar Site.bat` / `Desligar Site.bat`; endereço `http://26.139.39.123` pelo Radmin. Tudo sobre ele em [SITE.md](SITE.md).

## Servidor Non-PvP (desde 26/09/2026)

Um segundo GameServer, **"Mu Chila Non-PvP"**. No cliente, ao clicar em "Mu Chila", ele aparece como o segundo sub-servidor (Mu Chila-2; o normal é o Mu Chila-1). A única diferença é que um jogador não pode atacar outro.

| O quê | Onde |
|---|---|
| Programa | `C:\MuServer\GameServerNonPvP` (cópia do `GameServer`); o launcher liga os dois |
| Identidade | `GameServerNonPvP\DATA\GameServerInfo - Common.dat`: `ServerName = Mu Chila Non-PvP`, `ServerCode = 21`, `ServerPort = 55902`, **`NonPK = 1`**, `IsArcaWarServer = 0` (só um servidor faz a Arca War) |
| Lista de servidores | `ConnectServer\ServerList.dat`: linha 21 (era "Mu Chila 2", que o kit deixou reservada e nunca foi usada), com a coluna **`PasiveServer = 1`**: é ela que faz o cliente escrever "Non-PvP" ao lado do Mu Chila-2 (com 0 ele escreve "PvP") |
| Mapas | `Data\MapServerInfo.dat`: o 21 hospeda todos os mapas (`InitSetVal 1`) e manda os 48 mapas de evento e de cerco para o Castle Siege (19), igual ao 20; quem sai de um evento volta para o servidor de onde veio |
| Nome no cliente | O cliente agrupa os servidores pelo código ÷ 20: 20–39 = grupo 1 "Mu Chila", com os sub-servidores numerados (20 = Mu Chila-1, 21 = Mu Chila-2). O cliente não precisa de mudança. **Não mexa nos 7 bytes depois do nome nos `serverlist*.bmd`**: trocar `02 00 20 00` fez o cliente fechar sozinho ao abrir (26/09). Só o nome do grupo pode ser trocado |

- **Mesmas contas e personagens** (mesmo banco). Um personagem fica em um servidor por vez.
- **Arquivos compartilhados e separados:**
  - A pasta `Data` (monstros, drops, eventos, caixas) é a mesma para os dois.
  - Os `GameServerInfo - *.dat` são **um por pasta**. Mudou taxa, VIP ou comando no `GameServer\DATA`? Repita no `GameServerNonPvP\DATA`.
- **Limite próprio:** cada GameServer tem o seu próprio limite de monstros (índices 0–9169), e cada um roda as próprias invasões.
- **Vigia e ferramentas:**
  - O Vigia corrige a checagem de ataques (issue #12) nos dois.
  - `Recarregar-Servidor.ps1 -Servidor GameServer` e o painel admin recarregam os dois.
  - O painel lista "GameServer" e "GameServer Non-PvP" e mostra jogadores e monstros de cada um.
- **Mudança no `MapServerInfo.dat`:** exige reiniciar o Castle Siege, para ele saber devolver os jogadores ao 21.
- **Histórico:** no primeiro dia foi montado com o código 80, como uma linha própria na lista ("Mu Chila Non-PvP", grupo 4 renomeado no cliente). O dono preferiu como sub-servidor do Mu Chila. Então virou o 21, e os arquivos do cliente voltaram ao original.

## Servidor VIP (desde 27/09/2026)

Terceiro GameServer, **"Mu Chila VIP"** (Mu Chila-3 no cliente): **sem PvP** e só para contas **Vip e Vipzão**.

| O quê | Onde |
|---|---|
| Programa | `C:\MuServer\GameServerVIP` (cópia do Non-PvP); o launcher liga os três GameServers |
| Identidade | `GameServerVIP\DATA\GameServerInfo - Common.dat`: `ServerName = Mu Chila VIP`, `ServerCode = 22`, `ServerPort = 55903`, `NonPK = 1`, **`ServerLock = 2`**, `IsArcaWarServer = 0` |
| Lista de servidores | `ConnectServer\ServerList.dat`: linha 22 (era "Mu Chila 3", reservada pelo kit), `PasiveServer = 1` para o cliente mostrar "Non-PvP" |
| Mapas | `Data\MapServerInfo.dat`: o 22 hospeda todos os mapas e manda os 48 de evento e de cerco para o Castle Siege, igual ao 20 e ao 21 |

**`ServerLock` = nível mínimo de conta para entrar.** É um recurso do próprio MuDevs, descoberto lendo o código do GameServer em 27/09/2026:

- O valor fica no objeto de configuração, `ServerInfo+0x6c` (0xB91B9C).
- **Login (0x495F80 e 0x4963EE):** se `ServerLock` for maior que o nível da conta, o login é recusado com o código 9.
- **Atualização da conta durante o jogo (0x49658F):** se o VIP vencer e o nível cair abaixo do `ServerLock`, o servidor tira o jogador em 5 segundos. O nível da conta fica no objeto do jogador em `+0x654`, e a validade, como texto, em `+0x658`.
- **Status para o ConnectServer (0x496B07):** o servidor envia um sinal de "servidor travado".
- **Valores:** 0 = todos; 1 = qualquer VIP; 2 = Vip e Vipzão; 3 = só Vipzão.

Mudar a trava depois: edite o `ServerLock` no `Common.dat` do servidor e reinicie esse GameServer. Não conferi se o Reload Common também aplica.

## Painel Mu Chila Admin

**Painel web** desde 01/10/2026: http://localhost:5170 no PC do servidor (atalho **Mu Chila Admin** na área de trabalho), com usuário e
senha. Programa em `C:\MuServer\MuChilaAdmin`, código em `tools\MuChilaAdmin` e `tools\MuChilaAdmin.Core`. Instalação, usuários e
papéis, segurança, todas as páginas, configuração e o ambiente de desenvolvimento: **[MU-ADMIN.md](MU-ADMIN.md)**.
Roda na máquina do servidor, porque conversa com as janelas dos servidores e com o SQL local. As abas do programa antigo viraram
páginas (menu à esquerda), com as mesmas regras:

| Página | O que faz |
|---|---|
| **Processos e jogadores** | Mostra os processos (os GameServers separados), os jogadores online (conta, personagem, IP) e jogadores e monstros de cada GameServer. Atualiza a cada 5 s. |
| | Botões: iniciar e parar tudo pelo launcher, desconectar todos os jogadores (os personagens são salvos), abrir o MuEditor e o launcher. |
| | "Recarregar sem reiniciar": manda o Reload escolhido para os dois GameServers **e** o Castle Siege ao mesmo tempo. |
| **Eventos** | Escolha o evento e em quantos minutos ele começa, e clique em "Disparar evento". Veja [Eventos](#eventos). |
| **Bônus de EXP e drop** | Ex.: "EXP + EXP master x2 por 60 minutos", começando agora ou numa data e hora. Multiplica a taxa de cada plano em todos os GameServers e avisa os jogadores no início, a cada 5 minutos e no fim. Cancela a qualquer momento. Quem liga e desliga é o vigia. Ver [Bônus de EXP e drop](#bônus-de-exp-e-drop). |
| **Avisos aos jogadores** | "Enviar agora" manda uma mensagem para todos os jogadores. Também lista e edita os avisos automáticos (repetidos). Ver [Avisos para todos os jogadores](#avisos-para-todos-os-jogadores). |
| **Lojas de NPC** | Escolha o NPC e edite o que ele vende: adicionar (busca pelo nome), remover, reordenar, nível, durabilidade e opções. Mostra quantos dos 120 espaços (8×15) da janela a loja ocupa e avisa o que não cabe. "Salvar e aplicar" faz backup e recarrega as lojas sem reiniciar. |
| **Loja de Cash** | A loja da tecla X. Muda o preço, esconde e mostra pacotes, **adiciona item** (pacote novo: item, aba/moeda, preço, nível e opções, quantidade ou prazo) e **remove da loja**: apaga de vez o que o painel criou, e os pacotes do kit ficam só escondidos. Grava servidor + cliente e recarrega a loja. Os jogadores só veem depois de "Publicar para o launcher". Ver [Adicionar e remover itens da loja de cash](#adicionar-e-remover-itens-da-loja-de-cash). |
| **Drops** | **Por monstro / por item** (a primeira aba da página): escolha um monstro e veja tudo o que ele dropa (inclusive as regras que valem para vários monstros), com a chance em % e em "1 em N", para quem a regra vale e quantos monstros ela alcança, mais o drop comum e o zen dele; ou escolha um item e veja de onde ele cai. Adiciona item a um monstro, faz um item cair de um monstro, mapa, faixa de nível ou qualquer monstro, muda a chance e remove; regras que valem para vários monstros avisam antes. Edita as mesmas linhas das abas avançadas. As abas **Regras do arquivo** e **Drop comum e zen** são os arquivos completos: **Regras do arquivo** (`Data\Item\ItemDrop.txt`): cada regra diz qual item cai de qual monstro, mapa ou faixa de nível e com que chance em % (1% = 1 em 100). Adicionar (busca pelo nome), escolher monstro ou mapa numa lista, remover, filtrar; "Salvar e aplicar" faz backup e dá Reload Item. **Drop comum e zen por monstro** (`Monster.txt`: ItemRate, MoneyRate, MaxItemLevel): "Salvar e aplicar" faz backup e recarrega os monstros só nos GameServers e só fora da invasão (se houver uma no ar, o painel agenda para quando ela acabar). Linhas não alteradas são regravadas iguais. |
| **Taxas e opções** | Muda as opções dos GameServers sem abrir arquivo. **Principais** junta a economia num lugar só: EXP, EXP master, drop de itens, zen, **drop de joias** (`ItemDrop.txt`), chance das joias (Soul/Life/Harmony), Chaos Machine +10 a +15, pontos por nível e máximo por atributo, baús (/ware), MU Helper, /offattack, Goblin Point e EXP em party. As outras visões mostram **todas** as opções de cada um dos 7 arquivos `GameServerInfo - X.dat`. Colunas Free/Vip1/Vip2/Vipzão nas opções por plano. Ver [Taxas e opções](#taxas-e-opções-painel). |
| **EXP dinâmica** | As faixas de nível do `Data\Util\ExperienceTable.txt` (issue #37): para cada faixa, a EXP é um % da taxa do plano. Mostra ao lado a EXP efetiva de Free/Vip1/Vip2/Vipzão. Adicionar, remover e mudar faixas; recusa faixas sobrepostas; "Salvar e aplicar" faz backup e dá Reload Util. Ver [EXP dinâmica (painel)](#exp-dinâmica-painel). |
| **Itens novos** | Cria um item novo a partir de um que já existe: mesmo visual (modelo 3D), nome e atributos próprios (dano, defesa, velocidade, durabilidade, requisitos). Grava no `Item.txt` do servidor (Reload Item na hora) e nos arquivos do cliente da pasta do repositório. Os jogadores só recebem depois de "Publicar atualização do cliente". Teste antes abrindo o jogo pela pasta do repositório. Ver [Itens novos](#itens-novos). |
| **Itens e baú** | Mostra o inventário de qualquer personagem ou o baú da conta, com nome, nível e opções. **Colocar item...** (29/09/2026) põe um item novo (nível, opção, excelente, skill, sorte, quantidade para os que empilham) no inventário do personagem escolhido ou no baú da conta, no 1º espaço livre em que ele cabe. Remover e colocar deslogam a conta se ela estiver no jogo e salvam antes o inventário/baú em `C:\MuServer\DB\backup-itens-*.csv`. Ver [Colocar item no inventário ou no baú](#colocar-item-no-inventário-ou-no-baú). |
| **Contas** | Lista as contas com nível, validade, ban, status e personagens (busca e filtros). Na página da conta: VIP 1–3 por N dias, remover VIP, banir com motivo e prazo e desbanir (página **Bans**: banidas agora e histórico), senha do jogo, Cash, pontos, nível e reset, zerar a árvore master e o histórico de ações. |
| **Site → Loja de itens** | Catálogo e preços da **loja de itens do site** (o jogador monta o item, paga com Cash e recebe no baú). **Catálogo**: categorias (nome, grupo Defesa/Ataque, ícone, visível ou não, ordem) e os itens de cada uma (preço base, nível máximo, tipo de excelente, destaque, à venda), com a foto do item; "Adicionar itens..." busca no Item.txt. **Preços**: Cash de cada nível, adicional e quantidade de excelentes, sorte e skill, com exemplos calculados. **Vendas**: últimas compras e o Cash gasto em 24 h, 7 e 30 dias. Na barra de cima: abrir/fechar a loja, nível máximo e quantidade máxima de excelentes. "Salvar" grava o `muchila.lojaitens.json` (cópia ativa e repositório, com backup) e o site usa na hora. Testes: `--testar-lojaitens <saida>` (numa cópia, MUCHILA_ROOT). Ver [Loja de itens](SITE.md#loja-de-itens-30092026). |
| **Site → Notícias, Páginas, Comprar Zen e Mensagens** | O conteúdo do site (01/10/2026). **Notícias**: lista as publicadas; "Nova notícia" ou clique numa notícia para editar (título, autor, data, comentários e o texto em HTML, com botões Negrito, Título, Lista, Link, Imagem...); "Publicar / salvar" grava no banco do WebEngine e já atualiza o cache, então a notícia aparece na hora na página inicial; "Apagar" e "Ver no site". **Páginas**: os textos de Termos de Serviço, Política de Privacidade e Política de Reembolso e o Contate-nos (texto do topo, formulário ligado ou não e os canais: Discord, WhatsApp, e-mail, Instagram; canal vazio não aparece). "Salvar páginas" grava o `muchila.paginas.json` (cópia ativa e repositório, com backup). **Comprar Zen**: abrir/fechar e os pacotes (Zen, preço em Cash, ativo), com "milhões por Cash" calculado; "Salvar" grava o `muchila.zen.json`; embaixo, as últimas compras. **Mensagens**: o que os jogadores mandaram pelo Contate-nos (novas primeiro); "Marcar como lida", "Arquivar" e "Enviar resposta" (a resposta aparece para o jogador na própria página, se ele mandou logado; quem mandou sem conta deixou um contato). As notícias usam o PHP do site (`Site\php\php.exe` + `includes\muchila\noticias-cli.php`). Ver [Páginas de 01/10/2026](SITE.md#páginas-de-01102026). |
| **Passe dos Mapas** | Dá passe a uma conta (em minutos, horas ou dias; soma ao que ela já tem) e tira o passe. Mostra quem tem e quanto falta, os mapas que exigem passe, o histórico (loja de cash, site e painel) e quem o vigia tirou dos mapas. O cartão "Cobrança" liga e desliga a cobrança. Ver [Passe dos Mapas](#passe-dos-mapas-acima-do-nível-400). |
| | "Zerar habilidades master": escolhe um personagem da conta, apaga a árvore master, devolve os pontos (1 por Master Level) e tira os poderes master da lista de habilidades (`MagicList`). Os melhorados voltam à habilidade normal (ex.: 330 Twisting Slash Improved → 41 Twisting Slash), seguindo a coluna `ReplaceSkill` do `MasterSkillTree.txt`. Serve também para quem mostra "Suces de Atq" negativo na janela (C); ver PROBLEMAS-E-SOLUCOES. O "Resetar Skill-Tree" do site faz o mesmo (procedimento `dbo.MuChila_LimparArvoreMaster`), cobrando Zen. |
| | Raio-x da árvore master (só leitura, 01/10/2026): `MuChilaAdmin.exe --arvore-master <personagem> <arquivo>` grava os pontos, cada posição ocupada da árvore (`MasterSkill`, 3 bytes: índice baixo, nível, índice alto) com o nome do poder (`Skill.txt`) e a regra dele na árvore (seção, grupo, níveis, pré-requisitos, do `MasterSkillTree.txt`), e o mesmo para a `MagicList`. Serve para conferir o formato antes de deixar distribuir os pontos master pelo site: rode antes e depois de aprender um poder no jogo e compare. |
| | "Dar pontos" (página da conta): escolhe um personagem da conta, o tipo e a quantidade. **Pontos de atributo** somam em `Character.LevelUpPoint` (o jogador distribui na janela C); **pontos master** somam em `MasterSkillTree.MasterPoint` (só quem já tem árvore master). Número negativo tira, sem passar de 0. Cada entrega fica em `C:\MuServer\DB\muchila-pontos-dados.csv`. Linha de comando: `MuChilaAdmin.exe --dar-pontos <conta> <personagem> <atributo\|master> <quantidade> <arquivo-de-resultado>`. |

- **Mexeu na conta, ela sai do jogo sozinha** (decisão do dono, 28/09/2026). VIP, ban, "Dar pontos", "Zerar habilidades master" e remover item (página Itens e baú), com a conta online, deslogam a conta **sem perguntar**. VIP e ban valem no próximo login, e os outros precisam dela fora porque o servidor grava o personagem na saída e desfaria a mudança. "Nível e reset..." só manda o personagem para a seleção de personagem (não precisa deslogar a conta).
- **Como o painel desloga** (também no botão "Desconectar" da página Processos e jogadores):
  1. Pela memória do GameServer, como a opção "Trocar servidor" do jogo: o jogo volta à seleção de servidor, o servidor salva o personagem e fecha a conta. Não pede administrador e só aquela conta sai. Procura a conta em todos os GameServers e no Castle Siege, jogando ou na seleção de personagem (`ResetWatcher.LogoutAccount`).
  2. Se a conta não sair em 10 s: plano B, derruba a conexão de rede (pede permissão de administrador e derruba também as contas do mesmo IP, pelo IP de `MEMB_STAT`), agora no GameServer certo (Mu Chila, Non-PvP, VIP ou Castle Siege).
  - Linha de comando: `MuChilaAdmin.exe --forcar-logout <conta> <arquivo-de-resultado>`.
- VIP = `MEMB_INFO.AccountLevel` (1–3) + `AccountExpireDate`. Os benefícios de cada nível ficam nas linhas `*_AL1`, `*_AL2` e `*_AL3` do `GameServerInfo - Common.dat`.
- Ban = `MEMB_INFO.bloc_code = 1`.
- Teste rápido sem abrir a janela: `C:\MuServer\MuChilaAdmin\MuChilaAdmin.exe --teste C:\temp\teste.txt`. O arquivo lista banco, servidores, GameServer e agendas; o código de saída é o número de falhas.
- **Vigia** (`MuChilaAdmin.exe --vigia-reset`, processo sem janela, uma instância só): liga com o Windows (atalho na pasta Inicializar) e quando o painel abre; o painel mostra "Vigia: ligado/DESLIGADO". Ele:
  - corrige a checagem de ataques do MuDevs no GameServer e no Castle Siege sempre que encontra um servidor novo (issue #12, ver PROBLEMAS-E-SOLUCOES). **Se o vigia não estiver rodando quando o GameServer religar, as skills evoluídas voltam a travar;**
  - depois do `/reset`, leva o jogador à seleção de personagem (issue #7). **Ligado desde 26/09/2026**.
    - O arquivo vazio `C:\MuServer\MuChilaAdmin\vigia-reset-selecao.ligado` liga; apagar o arquivo desliga.
    - Dispara quando o personagem cai de nível ≥ 50 para ≤ 10. A troca é imediata: o Vigia confere a cada 250 ms e marca a contagem de saída em 1, sem os 5 s de contagem na tela que o "Trocar personagem" do menu tem (a pedido do dono, 26/09).
    - Testado com o Mario: `/reset` às 16:04:44; às 16:04:50 foi para a seleção sem desconectar (`DelCharacterInfo` no log do GameServer).
  - Registro: `C:\MuServer\MuChilaAdmin\vigia.log`. Conferir sem mexer em nada: `MuChilaAdmin.exe --vigia-sondar C:\temp\vigia.txt`.
  - Botão "Levar à seleção de personagem" (página Processos e jogadores): o mesmo que o jogador escolher "Trocar personagem" no jogo, sem desconectar.
  - Também: resets pedidos pelo site com o jogo aberto, bônus por tempo, avisos "enviar agora", [Passe dos Mapas](#passe-dos-mapas-acima-do-nível-400) (`vigia-passe-mapas.ligado`), Magic Backpack (`vigia-mochila.ligado`) e o nome certo da 1ª classe no jogo ([issue #35](#evolução-de-classe-missões-change-desligado), `vigia-classe-inicial.ligado`).
- Para publicar depois de mudar o código: rode `.\tools\Publicar-Painel.ps1` (como administrador; ver [MU-ADMIN.md](MU-ADMIN.md)). Ele fecha o painel e o vigia, publica em `C:\MuServer\MuChilaAdmin`, cria as chaves `.ligado` que faltarem, refaz os atalhos da pasta Inicializar (painel e vigia), liga os dois e confere.

## Bônus de EXP e drop

Página **Bônus de EXP e drop** do painel. Desde 28/09/2026 quem liga e desliga é o **vigia** (`MuChilaAdmin --vigia-reset`), não mais o `BonusManager.dat` do kit.
O BonusManager era caixa-preta: não mostrava se o bônus tinha começado, não deixava cancelar um ativo, e cada agendamento pedia Reload Event, que reinicia a contagem do Blood Castle.

- **Como funciona:** os bônus ficam em `C:\MuServer\MuChilaAdmin\bonus.json`. A cada 5 s o vigia confere:
  - começou → grava em cada `GameServer*\DATA\GameServerInfo - Common.dat` a taxa do plano × multiplicador (`AddExperienceRate_AL0..3`, `AddMasterExperienceRate_AL0..3`, `ItemDropRate_AL0..3`) e dá **Reload Common**;
  - terminou ou foi cancelado → devolve a taxa original e dá Reload Common.
- **Vários ao mesmo tempo** somam: x2 + x1,5 = x2,5.
- **Taxa mudada à mão durante um bônus:** o valor novo vira a taxa normal e não é sobrescrito na volta.
- **Avisos na tela** (pelo `Notice.txt`): "Começou: ... até HH:mm" no início, lembrete "Bônus ativo" a cada 5 minutos e "Terminou..." no fim (ou "foi encerrado", se cancelado).
- **Início:** agora, ou numa data e hora escolhida. **Cancelar** vale a qualquer momento, inclusive com o bônus ativo.
- **O vigia precisa estar rodando.** A página avisa em vermelho quando ele está parado, e agendar um bônus liga o vigia.
- Durante um bônus, a página Informações do site mostra as taxas com o bônus, porque lê o `Common.dat`.
- As linhas antigas do painel no `BonusManager.dat` (vagas 3–9, `//MuChilaAdmin`) não são mais criadas e podem ser apagadas à mão.
- Testes: `MuChilaAdmin.exe --testar-bonus <saída>` com `MUCHILA_ROOT` apontando para uma cópia (9 checagens).

## Itens e baú

- **Formato:** cada item ocupa 16 bytes em `Character.Inventory` (237 posições; 0–11 equipado) e em `warehouse.Items` (240 posições: 0–119 baú, 120–239 baú estendido). O layout, conferido em inventários reais, está descrito no topo de `tools\MuChilaAdmin.Core\Jogo\Items.cs`.
- **MuEditor:** é da Season 8 e **não deve salvar** inventários do S14, porque pode apagar o inventário expandido e o baú estendido. Use a página **Itens e baú** do painel para ver e remover.
- **Mais de um baú:** `/ware <número>`, com o baú fechado, troca o baú da conta (`/ware 0` = o principal). O limite é `CommandWareNumber_AL0..3` (`Command.dat`: 1/5/7/9, ou seja 2, 6, 8 e 10 baús). Os extras ficam em `ExtWarehouse` (coluna `Number`). O baú é da **conta**: todos os personagens veem os mesmos.
- **Baú estendido:** o Vault Expansion Certificate (14,163) libera a 2ª metade do baú (`AccountCharacter.ExtWarehouse`, máximo 1).
- **Magic Backpack (14,162):** cada uso soma uma faixa ao inventário (`Character.ExtInventory`, **máximo 2**, limite do GameServer em 0x421FF9; com 2 o item é recusado). O servidor não avisa o cliente, então a faixa só aparece ao reentrar. Com `vigia-mochila.ligado`, o vigia manda o personagem para a seleção logo depois do uso.
- **Presentes:** a **Gremory Case** (tabela `GremoryCase`, procedure `GremoryCaseAddItem`) guarda até 50 presentes por conta.
  - `StorageType 1` = para a conta; `2` = para um personagem.
  - O jogador recebe ao entrar no jogo.
- **Calibração pendente:** o campo `RewardSource` precisa sair de um presente criado pelo próprio servidor. Um GM usa `/gremgif` uma vez no jogo; depois grava-se `RewardSource=N` em `C:\MuServer\MuChilaAdmin\gremory.txt`. Até lá o painel não cria presentes.

## Servidor de testes (`C:\MuServerTeste`)

Antes de mexer num formato novo nos servidores reais, teste num GameServer separado:

- **Criar ou refazer:** `.\tools\Criar-ServidorTeste.ps1 [-Refazer]` copia o GameServer e a `Data` para `C:\MuServerTeste`.
  - Código 39, porta 55939, nome "Mu Chila Teste".
  - DataServer, JoinServer e ConnectServer apontam para uma porta vazia (55999): ninguém entra, ele não aparece na lista, não grava no banco e, se cair, não leva nada junto.
- **Ligar:** `Start-Process 'C:\MuServerTeste\GameServerTeste\Game Server S14.exe' -WorkingDirectory 'C:\MuServerTeste\GameServerTeste'`.
- **Testar:** altere o arquivo em `C:\MuServerTeste\Data` e use `.\tools\Recarregar-Teste.ps1 -Item Event|Shop|Common|...`. Veja se o processo continua vivo e leia `C:\MuServerTeste\GameServerTeste\LOG`.
- **Painel contra a cópia:** o painel e o `Recarregar-Servidor.ps1` só mexem em processos dentro de `C:\MuServer`. Para testar o próprio painel contra a cópia, rode-o com a variável `MUCHILA_ROOT=C:\MuServerTeste`. As opções `--agendar-bonus` e `--testar-lojas` foram feitas para isso.

## MuEditor (personagens, inventário, baú)

`C:\MuServer\1 - MuEditor\MuEditor.exe` já vem no kit e está configurado para o banco local (`config.ini`: `SERVER = .\MUONLINE`, `PORT = 61764`, login do Windows).
Use-o para editar contas, personagens (nível, pontos, classe, zen, mapa), inventário, baú e guildas.

- Edite só personagens de contas **offline**. O servidor salva o personagem ao sair e sobrescreve o que foi mudado com o jogador conectado. Para garantir, use "Desconectar todos os jogadores" no painel ou espere o jogador sair.
- A porta `61764` é dinâmica do SQL Express. Se o MuEditor parar de conectar depois de reinstalar o SQL, veja a porta atual em *SQL Server Configuration Manager → Protocolos para MUONLINE → TCP/IP → Endereços → IPAll → Portas Dinâmicas TCP* e ajuste o `config.ini`.
- O MuEditor é da versão S8 (3.5.8). Itens novos do S14 podem aparecer sem nome ou com o ícone errado. Para esses itens, prefira `/make` no jogo com um GM.

## Recarregar sem reiniciar

A maioria das configurações pode ser recarregada com o servidor rodando, sem derrubar ninguém. Use o menu `Reload` na janela do servidor ou o script:

```powershell
.\tools\Recarregar-Servidor.ps1 -Servidor GameServer    -Item Event          # agendas de eventos
.\tools\Recarregar-Servidor.ps1 -Servidor GameServer    -Item EventItemBag   # conteúdo das caixas
.\tools\Recarregar-Servidor.ps1 -Servidor GameServer    -Item Item           # Item.txt, ItemMove.txt...
.\tools\Recarregar-Servidor.ps1 -Servidor GameServer    -Item Util           # GameMaster.txt, Notice.txt
.\tools\Recarregar-Servidor.ps1 -Servidor GameServer    -Item Common         # Common.dat + mensagens (English.xml)
.\tools\Recarregar-Servidor.ps1 -Servidor ConnectServer -Item ServerList
```

Os arquivos da pasta `Data` são compartilhados pelos dois GameServers (normal e Non-PvP) e pelo Castle Siege. `-Servidor GameServer` já recarrega os dois GameServers; recarregue também o Castle Siege (`-Servidor CastleSiege`).
Mudanças de IP no `MapServerInfo.dat` e nas portas exigem reiniciar.

## Contas e GM

- **Criar conta:** `C:\MuServer\Criar Conta.bat`. Pede login e senha (4 a 10 letras/números) e o código pessoal de 7 dígitos, que o jogo pede para apagar personagem. O padrão do código é `1111111`.
- **Contas do kit:** `yolaxd`, `yolaxd1`, `yolaxd2`, `yolaxd4`, `yolaxd5`. A senha fica na tabela `MEMB_INFO` do banco e não é publicada aqui. Troque as senhas antes de liberar o servidor.
- **Tornar GM:** crie o personagem e adicione `conta  Personagem  32` em `Data\Util\GameMaster.txt`, antes do `end`. Depois, Reload Util.
- **Comandos de GM:** `/gmmove`, `/make`, `/notice`, `/hide`, `/setlvl`, `/disconnect`, `/banuser`, entre outros. A lista completa está nos `Msg ID` com `/` em `Data\Lang\English.xml`.
- **Comandos de jogador:** `/f`, `/a`, `/v`, `/e` e `/c` (força, agilidade, vitalidade, energia e comando) com a quantidade de pontos, ex.: `/f 100`; `/reset`, `/pkclear`, `/post`.
  - O nome de cada comando é uma mensagem do `Data\Lang\Portuguese.xml`: IDs 32–58 e outros, por exemplo `<Msg ID="34" Text="/f" />`. É o arquivo que o `Data\LangManager.xml` liga.
  - O GameServer lê os nomes ao ligar. Depois de mudar, **reinicie os GameServers e o Castle Siege**.
  - Os de pontos eram `/addstr /addagi /addvit /addene /addcmd` e mudaram em 01/10/2026 (issue #40). O `English.xml` foi trocado igual.
  - O modo automático (`/f auto 1`) segue a sintaxe do MuEmu e ainda não foi testado.

## Eventos

Não há comando para iniciar evento na hora. Tudo vem das agendas em `Data\Event\*.dat`.
Bloco 0 = `Index  Year  Month  Day  DoW  Hour  Minute  Second`, onde `*` significa qualquer valor.

- **Invasões** (`InvasionManager.dat`): 0 Underworld, 1 Red Dragon, 2 Golden, 3 White Wizard, 4 Ano Novo, 5 Páscoa, 6 Verão, 7 Christmas, 8 Medusa, 9 Demônios invocados, 10 Ovos.
  - **Golden:** a cada **10 minutos** (00, 10, 20, 30, 40 e 50), com duração de **540 s**, para não sobrepor.
  - **Red Dragon:** 0:15, 4:15, 8:15, 12:15, 16:15 e 18:32.
- **Disparar uma vez:** página **Eventos** do painel. Ela grava uma linha com data exata no `.dat` do evento, marcada com `//MuChilaAdmin` (vale se o GameServer reiniciar antes da hora), e então:
  - **Invasões** (29/09/2026): marca o horário direto na **memória** dos GameServers (`ResetWatcher.Invasoes.cs`). O **Reload Event NÃO recalcula** o horário das invasões: o GameServer só calcula o próximo horário quando liga e quando a rodada anterior termina. Por isso, até 29/09, disparar invasão pelo painel nunca funcionava (a Páscoa das 20:41 continuava marcada para as 02:15).
  - Estado de cada invasão na memória: 1 = esperando (com o horário-alvo), 2 = acontecendo (com o horário do fim). Se já estiver acontecendo, o painel avisa e não mexe.
  - **Os outros eventos** (Blood Castle, Devil Square, Chaos Castle, Illusion Temple, Moss Merchant, Castle Deep) ainda vão pelo arquivo + Reload Event, só nos GameServers (o Castle Deep, só no Castle Siege). **Ainda não é garantido** que comecem na hora: pelos logs de 29/09, o Reload Event também não refaz o horário deles ("Sync Start Time" só aparece no fim de cada rodada).
  - Blood Castle, Devil Square, Chaos Castle e Illusion Temple têm sala de espera e avisam 5 minutos antes (padrão de 6 minutos). Sem ninguém com ingresso, o evento fecha vazio ("Not enough users").
  - Reload Event no **Castle Siege** reinicia o ciclo do cerco (anuncia "começou o período de preparação" e adia o cerco ~15 min). O painel não manda mais para lá, a não ser no Castle Deep (com aviso) ou se você confirmar no botão manual.
  - "Limpar disparos já executados" tira as linhas que passaram há mais de 30 minutos.
  - Linha de comando: `MuChilaAdmin.exe --invasao <índice> <segundos> <saída>` (dispara) e `--invasao-ler <índice> <saída>` (só lê o estado).
- **Invasões sem monstro (defeito do kit, corrigido em 29/09/2026):** cada invasão sorteia um mapa do bloco 2 e só põe os monstros do bloco 3 que têm o **mesmo mapa e o mesmo Value**. No kit, o bloco 2 estava com Value 0 em todos os mapas, e o bloco 3 numerava os mapas (0, 1, 2...). Na maioria dos sorteios, a invasão "acontecia" **sem nenhum monstro**:
  - Páscoa, Ano Novo, Verão e Demônios: só 1 em 6 sorteios tinha monstros;
  - Red Dragon, White Wizard e Natal: 1 em 3;
  - Underworld: 1 em 2;
  - Golden: 4 mapas vazios em 23;
  - só a Medusa estava certa.
- **A correção:** `tools\Corrigir-Invasoes.py` acertou 40 linhas do bloco 2 (o Value de cada mapa = o do bloco 3). No servidor de testes, 3 disparos seguidos da Páscoa deram 10 coelhos cada. Com isso, as invasões da agenda passam a ter monstros de verdade, **inclusive as 11 que o kit marcou todo dia às 18:32**.
- **Conferir se nasceu:** o título da janela do GameServer mostra `MonsterCount`, que sobe quando a invasão aparece (Golden: +66). Cada invasão sorteia o mapa, então pode acontecer longe de quem está olhando.

## Colocar item no inventário ou no baú

Página **Itens e baú** do painel → escolha a conta e o personagem (ou "baú") → **Colocar item...** → busque o item, escolha nível, opção, excelente, skill, sorte e quantidade → **Colocar**.

**O que o painel faz:**

1. Se a conta estiver no jogo, desloga (o servidor regravaria o inventário ao sair e apagaria o item).
2. Acha o **1º espaço livre** em que o item cabe:
   - baú: grade 8 × 15, posições 0–119;
   - inventário: grade principal 8 × 8, posições 12–75 (sem os equipados e sem as expansões);
   - usa largura × altura do `Item.txt` e os itens que já estão lá. Sem espaço, avisa e não grava nada.
3. Monta os 16 bytes no mesmo formato dos itens do servidor:
   - número de série novo pelo `WZ_GetItemSerial` (o mesmo contador do servidor);
   - durabilidade = a da coluna "Durability" do `Item.txt`; nos itens que empilham (`ItemStack.txt`), a durabilidade é a quantidade;
   - sem sockets.
4. Salva o inventário ou baú inteiro em `C:\MuServer\DB\backup-itens-*.csv` e grava, conferindo que nada mudou no meio e que a conta continua fora do jogo.

- **Linha de comando:** `MuChilaAdmin.exe --colocar-item <conta> <personagem | bau> <seção> <índice> <nível> <quantidade> <arquivo>`.
- **Testado em 29/09/2026 na conta de teste do kit (yolaxd):** Jewel of Bless ×10 no baú (posição 37, entre asas 4×3 e outras joias) e Short Sword +9 (1×3) no inventário do Quest6. Os bytes ficaram no mesmo formato dos itens reais, sem sobrepor nada; depois os dois foram removidos pelo painel.
- **Antes, "Dar item (Gremory Case)":** nunca funcionou, porque dependia de uma calibração com o `/gremgif`, cuja sintaxe o kit não traz, e a tabela `GremoryCase` estava vazia. Foi trocado pelo "Colocar item...". A lista de presentes pendentes continua na aba.

## Itens novos

Página **Itens novos** do painel (`tools\MuChilaAdmin.Core\Jogo\NewItems.cs`, desde 28/09/2026). O item novo copia o item base e troca:

- o índice (o próximo livre na seção, acima de todos os usados no servidor e no cliente);
- o nome;
- os atributos escolhidos.

Classes, tamanho, skill e opções continuam iguais aos do item base.

- **Servidor:** uma linha nova no `Data\Item\Item.txt`, logo depois do último item da seção, com o espaçamento do item base.
- **Cliente** (pasta do repositório, `2 - Cliente Season 14 Full`):
  - `Data\Local\{Eng,Por}\item_{eng,por}.bmd`: contador (4 bytes) + registros de 672 bytes + checksum. Registro: código (int), seção, índice, pasta do modelo (260), arquivo do modelo (260), nome (64) e 80 bytes de atributos (nível +596, dano +604/+606, taxa de defesa +608, defesa +610, defesa mágica +612, velocidade +614, durabilidade +616, requisitos +624 a +634).
  - `Data\Local\{Eng,Por}\itemtooltip_{eng,por}.bmd`: 10.240 registros de 124 bytes (seção, índice, nome...), com as vagas vazias no fim, e checksum. O item novo entra na primeira vaga vazia.
  - `Data\Local\ItemTRSData.bmd`: contador + registros de 32 bytes (código + posição/rotação/escala no inventário) + checksum.
  - Em todos, cada registro leva XOR `FC CF AB` a partir do começo dele. O checksum é o GenerateCheckSum2 da Webzen (chave `0xE2F1`) sobre os registros cifrados.
  - As posições dos atributos foram conferidas contra os 2.508 itens do servidor (100% em dano, defesa, velocidade, requisitos, nível, skill e tamanho).
- **Registro:** `C:\MuServer\MuChilaAdmin\itens-novos.json`. Só itens desta lista podem ser removidos pelo painel; remover devolve os arquivos ao que eram.
- **Backups:** `.bak-*` ao lado de cada arquivo.
- **Testes:** `--testar-itens-novos` (cria arma e elmo em cópias, confere tudo e ao remover os arquivos voltam idênticos) e `--criar-item`. Uma linha nova do `Item.txt` foi validada no servidor de testes (carrega e recarrega sem erro).
- **Falta confirmar no jogo:** que o cliente mostra o item (nome, visual, tooltip) e que dá para equipar. Crie um item de teste, abra o jogo pela pasta do repositório, peça a um GM para criar com `/make` e confira. Só depois publique para os jogadores.

## Avisos para todos os jogadores

Página **Avisos aos jogadores** do painel, usando o `Data\Util\Notice.txt` do kit. O GameServer não tem opção de aviso no menu.

- **Enviar agora:** manda uma vez para todos, em todos os servidores. O aviso entra no topo do `Notice.txt` com 1 s e o painel dá Reload Util; o vigia tira a linha do arquivo em menos de 1 minuto.
- **Avisos automáticos:** o servidor manda um de cada vez, em ordem. Cada um espera o seu tempo ("Repetir a cada", em segundos) depois do anterior; com um aviso só, esse tempo é o intervalo. O do kit é "Server Season 14 AM" a cada 60 s.
- Até 90 caracteres, com acentos. Emoji e símbolos que o jogo não mostra são recusados.
- Colunas: `"Mensagem" Type Count Opacity Delay Red Green Blue Speed RepeatTime`. O painel usa Type 0 (aviso no topo) e mantém as outras colunas do kit.
- **O Reload Util não reinicia o rodízio** (issue #31, comprovado na memória, 29/09/2026). A rotina do GameServer (0x4C1ED0) guarda "próximo aviso" e "último envio" e só troca de aviso quando passa o tempo do próximo. Antes, o "enviar agora" ficava esperando o tempo de outro aviso (ex.: 600 s) e o vigia o apagava antes da vez dele; com a lista menor, o servidor ainda podia mandar uma mensagem velha, como um "Bônus ativo" já encerrado.
  - **Como ficou:** depois de todo Reload de avisos (enviar agora, salvar a lista, bônus começando ou terminando, limpeza do vigia), o painel/vigia grava na memória de cada GameServer e do Castle Siege "próximo = 0". No "enviar agora", grava também o último envio no passado, e o aviso sai no segundo seguinte.
  - Os endereços são achados pelos bytes da rotina (`ResetWatcher.Avisos.cs`), então servem para as duas versões do programa.
  - Conferir sem mandar nada: `MuChilaAdmin.exe --avisos-reiniciar <saida>`.

## Caixas

As caixas abertas com o botão direito estão em `Data\EventItemBagManager.txt` (código do item → saco) e `Data\EventItemBag\NNN - Nome.txt` (itens possíveis).
Isso vale para caixas usadas pelo caminho clássico, como a Box of Kundun (7179 níveis 8–12). Depois de editar, use Reload EventItemBag.

Cada abertura sorteia **uma linha** da lista, com a mesma chance para todas. Para uma coisa sair mais vezes, repita a linha.

**Box of Kundun +4 e +5** (desde 26/09/2026):

- **Conteúdo:** as joias e tickets de antes e sets e armas de todas as classes, sem os 4 melhores sets e as 4 melhores armas de cada classe.
- **Top 4:** Blue Eye, Soul, Holyangel e Darkangel em todas as classes, menos o Rune Wizard. No Rune Wizard são Holyangel, Darkangel, Bloodangel e Light Load Rune. As armas seguem a mesma ideia.
- **+5:** os 2 melhores sets e as 2 melhores armas permitidos de cada classe, de +2 a +6.
- **+4:** os 2 seguintes de cada classe, de +0 a +4.
- **Chances:** as 10 linhas originais estão repetidas 5 vezes (50 de ~154). Mais ou menos 1 de cada 3 caixas dá joia ou ticket e 2 de cada 3 dão equipamento.
- **Limite de tamanho:** o arquivo ficou com 154 linhas, o mesmo tamanho da maior caixa do kit que já funcionava. Evite passar disso sem testar.
- **Ver o que cada classe ganha:** `python tools\Montar-CaixasKundun.py`.
- **Regravar:** `--gravar`, depois de restaurar os arquivos originais pelos `.bak-*`.

As caixas novas do S14 que abrem com o botão direito (Ruud Box, Earring Box, Gift Box, Mastery Box, Chicken Box...) passam por outro caminho: o servidor MuDevs FREE só aceita as que conhece e **não tem configuração para elas**, e o log registra `Unknown box identifier`. As 19 que não abrem deixaram de cair dos monstros (`DropItem = 0` no `Item.txt`).

## Idioma (português)

- **Servidor:** as mensagens do servidor estão em `Data\Lang\Portuguese.xml`, ativado no `Data\LangManager.xml` (English `Enable="0"`, Portuguese `Enable="1"`). Aplique com Reload Common.
  - Salve o arquivo em **Windows-1252** (ANSI), mantendo o cabeçalho `encoding="utf-8"` como está. O servidor repassa os bytes do texto sem converter, e o cliente lê em Windows-1252: com o arquivo em UTF-8 o jogo mostra "invasÃ£o" em vez de "invasão".
  - O arquivo tem três seções: `<Message>` (mensagens gerais), `<MapName>` e `<InvacionMsg>`. Os avisos de invasão saem da `<InvacionMsg>`; os IDs usados no `InvasionManager.dat` (192–211) precisam estar lá.
  - Os comandos (`/move`, `/post`...) continuam com o nome original.
- **Cliente:** registro `HKCU\Software\Webzen\Mu\Config` → `LangSelection = Por`. Os arquivos `Idioma - Portugues.reg` e `Idioma - Ingles.reg` na pasta do cliente fazem a troca com dois cliques.
  - O cliente passa a carregar `Data\Local\Por\*.bmd`: missões, dicas de skill, ajuda e opções de conjunto já vêm traduzidas.
  - Os nomes e dicas de itens continuam em inglês, porque `item_por` e `itemtooltip_por` são cópias do inglês.
  - O texto da interface fica no `Data\Lang.mpr`, que é criptografado.

## Resets pelo site (inclusive com o jogo aberto)

Reset, Master Reset e Supreme Reset ficam em **Painel do jogador → Resets** (`modules\usercp\resets.php` + `includes\muchila\MuChilaResets.php`), pelas procedures `MuChila_Reset`, `MuChila_MasterReset` e `MuChila_SupremeReset` (`DB\1 - Querys\MuChila-Resets.sql`). O `/reset` do jogo está desligado. Comandos novos no jogo não são possíveis, porque o GameServer é fechado e nem registra o que o jogador digita.

**Com a conta online (desde 28/09/2026):**
1. O site grava um pedido em `dbo.MuChila_ResetPedido`, se o vigia estiver vivo (`dbo.MuChila_VigiaStatus`, atualizado a cada ~5 s).
2. O vigia (`MuChilaAdmin --vigia-reset`) confere os pedidos a cada 1 s. Se o personagem estiver jogando, manda para a **seleção de personagem na hora** (a mesma rotina da opção "Trocar personagem", sem contagem na tela).
3. Com o personagem fora do jogo há 2 s (tempo de o servidor gravá-lo), o vigia chama a procedure com `@IgnorarOnline = 1` e grava o resultado.
4. A página mostra o andamento e se atualiza sozinha; o jogador entra de novo já resetado.

- O vigia só age com **todos** os GameServers e o Castle Siege lidos, porque só assim tem certeza de que o personagem saiu do jogo.
- Se o jogador entrar de novo antes do reset, o vigia espera ele sair outra vez. Pedido com mais de 3 minutos expira.
- Sem o vigia, o site volta a pedir para sair do jogo.
- O reset zera a EXP (desde 28/09/2026 à noite). Antes ela ficava a do nível 400, e o personagem subia 1 nível por abate depois do reset. Os 4 personagens nessa situação foram corrigidos.
- O master level **não** muda no Reset normal, só no Master Reset (no 600) e no Supreme. Como a tela soma nível + master, um nível 400 com master 140 aparece como 540 e, depois do reset, como ~141.

**Pelo painel (testes):** página da conta (**Contas** → a conta → **"Nível e reset..."**). Define nível (1–400) ou master level (0–600), dá EXP (sobe os níveis que ela der), ou faz Reset, Master Reset e Supreme Reset num personagem. Usa a mesma fila (`Tipo` `nivel`, `exp`, `mlevel`, ou os do site, com o valor em `Valor`) e a procedure `MuChila_AjustarPersonagem`. Com o jogo aberto, o personagem vai para a seleção e a mudança vale ao entrar de novo. Subindo de nível, soma os pontos da regra do jogo (5 ou 7 por nível, +1 da 3ª classe; 1 ponto master por master level). Os resets seguem os mesmos requisitos do site.

## Passe dos Mapas (acima do nível 400)

Decisão do dono (28/09/2026): Nixies Lake, Deep Dungeon 1–5, Swamp of Darkness e Kubera Mine 1–5 (nível mínimo acima de 400) só com o passe. Ferea e Swamp of Calmness pedem exatamente 400 e ficam livres.

- **Banco** (`DB\1 - Querys\MuChila-PasseMapas.sql`): o passe é por **conta** (`dbo.MuChila_PasseMapas.Expira`), com histórico em `MuChila_PasseMapasLog`. A lista de mapas fica em `MuChila_PasseMapasLista`; para incluir ou tirar um mapa, basta mudar essa tabela, e o vigia relê a cada minuto. `MuChila_PasseAdicionar @Conta, @Segundos, @Origem` soma ao passe ativo (ou começa agora).
- **No jogo (WCoin):** Gold Channel Ticket da Cash Shop, de 1, 3, 7 ou 30 dias. Ao usar o ticket guardado na loja, o GameServer manda o JoinServer rodar `WZ_SetAccountLevel`, que foi ajustada para somar ao passe (nível 0 do produto) sem mexer no VIP. Preços e textos: `tools\Loja-PasseMapas.ps1` (`-Restaurar` volta ao kit).
- **No site (PIX):** Loja → "Passe dos Mapas" (pacotes `passe-1/3/7/30` em `muchila.pacotes.json`, R$ 9/18/27/45). Vale na hora, sem sair do jogo.
- **Quem cobra:** o vigia (`vigia-passe-mapas.ligado`) confere a cada 1 s. Quem estiver num mapa da lista sem passe vai para a seleção de personagem e, 2 s depois de sair do jogo, é posto em Lorencia (área do gate 17) no banco. Se reentrar antes, é mandado de novo. Se a consulta ao banco falhar, ninguém é tirado.
- **Dar ou tirar passe (testes):** página **Passe dos Mapas** do painel. Para testar o vencimento, dê alguns minutos, entre num mapa da lista e espere. Pela linha de comando: `MuChilaAdmin.exe --testar-passe <saida>` (conta descartável, criada e apagada pelo teste).
- Não há mensagem na tela explicando a saída (o GameServer é fechado). A regra está na página Informações e na Loja do site.

## Evolução de classe (missões; `/change` desligado)

**DW, DK, Elfa e Summoner nascem na 1ª classe** (Dark Wizard, Dark Knight, Fairy Elf e Summoner), como no kit, e **só passam para a 2ª fazendo a missão** (issue #35, decisão do dono em 01/10/2026). De 24/09 a 01/10/2026 o gatilho `TR_MuChila_ClasseInicial` os fazia nascer na 2ª. Ele é removido por `ClasseInicial_1aClasse.sql`. Os personagens desse período voltam para a 1ª com `DB\1 - Querys\MuChila-Issue35-VoltarPara1aClasse-aplicar.sql`: rodar uma vez, com o servidor desligado; antes, `-ver.sql` mostra a lista sem mudar nada. Os que tiveram as missões marcadas pelo `ClasseInicial_MissoesDa2aClasse_existentes.sql` (rodado na implantação de 01/10) voltam com `tools\Desfazer-MissoesDa2aClasse.ps1`, que compara com o backup de antes (sem `-Aplicar` só mostra).
O GameServer do kit manda a 1ª classe ao jogo como se fosse a 2ª: um Dark Knight (classe 16) aparecia como "Blade Knight" na janela C. O vigia corrige isso na memória de cada GameServer e Castle Siege. A chave é `vigia-classe-inicial.ligado`; detalhes em PROBLEMAS-E-SOLUCOES. **Sem o vigia rodando quando o GameServer liga, o nome errado volta.** Vale sempre o que está no banco (`Character.Class`).
Itens e skills marcados com `2` nas colunas de classe do `Item.txt` exigem a 2ª classe (ex.: a foice Brova/"Beuroba" exige Blade Knight).
Desde 27/09/2026 o **`/change` está desligado** para todas as contas (`CommandChangeEnable_AL0..3 = 0` em `GameServerInfo - Command.dat`; quem tentar recebe a mensagem 81 "Você não tem permissão para usar /change").
A 3ª e a 4ª classe saem pelas **missões** de `Data\Quest\Quest.txt` (nível mínimo 150): índices 0–1 levam à 2ª classe, 2 é o "Hero Status", 3 é a do combo (DK e GL), 4–6 à 3ª e 7–9 à 4ª. Recompensas em `QuestReward.txt`: tipo 1 = pontos, 2 = 2ª classe (missão 1), 4 = "Hero Status" (missão 2), 8 = combo (missão 3), 16 = 3ª classe (missão 6) e 32 = 4ª classe (missão 9). O progresso fica em `Character.Quest`: 2 bits por missão, a 0 nos bits mais baixos do 1º byte (3 = não começada, 2 = concluída).
Para religar: `CommandChangeEnable_AL0..3 = 1` e Reload Command (o limite é `CommandChangeLimit = 3`, ou seja, até a 4ª classe).

**Código da classe no banco** (`Character.Class`): família × 16 + estágio (0 = 1ª classe, 1 = 2ª, 2 = 3ª, 3 = 4ª). Ex.: 16 Dark Knight, 17 Blade Knight, 18 Blade Master, 19 Dragon Knight.
MG, DL, RF e GL não têm o estágio 1 (ex.: 48 Magic Gladiator → 50 Duel Master → 51 Magic Knight). O site usa essa numeração em `includes\config\xteam.tables.php`, corrigido em 27/09 porque o WebEngine chamava o 19 de Blade Master.

**Subir de nível para testes, sem caçar:** um personagem GM (conta e personagem em `Data\Util\GameMaster.txt`, nível 32) usa `/setlvl <personagem> <nível>` e `/msetlvl <personagem> <master level>` no chat, com o alvo online (a ordem dos parâmetros ainda não foi confirmada no jogo). Os pontos que o personagem ganharia subindo talvez não venham junto; confira depois.

## Spawn de monstros (grupos espalhados + pontos de farm)

Desde 26/09/2026 os spawns dos mapas de caça são gerados por `tools\Distribuir-Spawns.py` a partir dos arquivos **originais do kit**:

- **Grupos de 3** do mesmo monstro, cada um numa caixa de 5×5 tiles, espalhados por igual na área onde o monstro nascia no kit.
  - Os centros saem de um k-means sobre o chão livre, então ocupam o meio da área e não as bordas.
  - Monstros que dividem a mesma área não repetem o mesmo lugar.
- **Pontos de farm:** 12 monstros numa caixa de 7×7.
  - Quantidade: 1 a 5 por mapa (1 a cada 100 monstros).
  - Monstro: os mais numerosos do mapa.
  - Posição: pelo menos 18 tiles da cidade e 40 tiles entre um farm e outro.
- **Chão que conta:** só o que é alcançável andando a partir da cidade ou de algum ponto de chegada de portal (`Data\Move\Gate.txt`, portais com `TargetGate = 0`). Nenhum grupo ou farm fica a menos de 6 tiles de um portal.
- **Ajustes por mapa** (`AMPLIAR` e `EXTRA` no início do script):
  - **Área maior:**
    - **Raklion:** a área de cada monstro cresce 10 tiles em volta dos pontos do kit, que usa pontos soltos e deixava corredores vazios.
    - **Nars:** os monstros usam o mapa todo. O kit só tinha retângulos pequenos nas bordas, e os 3 monstros de Nars têm nível 116–120.
  - **Monstros movidos para onde o pessoal caça** (26/09, pelo tempo de caçada nos logs):
    - Os mapas onde se caça mais ganharam monstros: Raklion +80 (120 → 200), Nars +60, Kanturu 1 +40, Tarkan +35, Karutan 1 +35, Kubera Mine 1 +30 e Atlans +15.
    - Os monstros saíram de mapas onde ninguém caçou, cerca de 13% em cada, para não enfraquecer os mapas: Deep Dungeon 1–5 (−44, −38, −42, −35, −40), Swamp of Darkness −40 e Kalima 1–7 (−8 cada).
    - Para refazer com dados novos: cada caçada grava uma linha `DB Save Hunting Record Info - MapIndex:N ... Second:... MonsterKillCount:...` no log do GameServer.
  - **Mapas com menos de 70 monstros** (Kalima, Kanturu 2) não têm farm, porque ele levaria boa parte dos monstros do mapa.
  - **Cobertura ao vivo:**
    - Raklion 31% → 74%, Nars 17% → 56%.
    - Kanturu 1 45% → 83%, Tarkan 50% → 85%, Karutan 1 36% → 77%, Kubera Mine 1 54% → 95%.
- **Números:** grupos de 3 monstros (alguns de 2 ou 4, pelo arredondamento) e farms de 12. O total de monstros é igual ao do kit (9.078 nos spawns).
- **Limite de monstros (não passe do total do kit):**
  - O título da janela mostra `MonsterCount x/10000`, mas na prática o GameServer só usa os índices de objeto 0–9169 para os monstros dos spawns **e** das invasões.
  - Os spawns ocupam uns 9.080. Sobram cerca de 90 vagas, que a invasão Golden (66 monstros a cada 10 minutos) usa.
  - Em 26/09, +100 monstros líquidos fizeram o último mapa carregado (127, Kubera Mine 5) ficar com 20 de 86 monstros, e as invasões ficariam sem vaga.
  - Não há configuração para esse limite; ele é fixo no executável do MuDevs FREE. Para ter mais monstros, a saída é um segundo GameServer com parte dos mapas (os códigos 21 e 22 já existem no `MapServerInfo.dat`).
- **Ficam como no kit:**
  - chefes e raros (1 ou 2 no mapa, ou respawn de 10 minutos ou mais);
  - armadilhas;
  - Crywolf e o Refúgio de Balgass.
- **Cobertura:** medida na memória do GameServer, é a parte do chão livre com monstro vivo a até 10 tiles.
  - Lorencia foi de 36% para 61%, Noria de 44% para 71% e Devias de 38% para 70%.
  - Os mapas altos ficaram entre 80% e 93%.
  - A média dos 41 mapas foi de 50% para 78%.
- **Uso:** `python tools\Distribuir-Spawns.py` só simula e mostra a cobertura antes e depois.
  - `--png <pasta>` desenha os mapas: grupos em vermelho, farms em amarelo.
  - `--grupo N` muda o tamanho do grupo.
  - `--gravar` grava, com backup `.bak-*`.
  - `--mapas 57,110` limita a esses mapas; os outros não são tocados.
- **Aplicar no ar:** Reload Monster no GameServer (recria todos os monstros e limpa invasões em andamento).
  - **Nunca com invasão no ar.** Os monstros da invasão seguram vagas enquanto os spawns são recriados, e o último mapa carregado (127, Kubera Mine 5) fica sem parte dos monstros até a próxima recarga.
  - Com a Golden a cada 10 minutos (9 no ar), sobra só o minuto xx:x9.
  - `.\tools\Recarregar-Servidor.ps1 -Servidor GameServer -Item Monster` lê a agenda do `InvasionManager.dat` e espera sozinho a invasão acabar. O menu da janela do GameServer não espera.
  - Land of Trials (31) e Barracks (41) ficam no servidor do Castle Siege e só mudam quando ele reinicia. Não use Reload Monster no Castle Siege, que recria também os monstros do cerco.
- **Histórico:** em 23/09 os spawns tinham virado 1.676 grupos de 5, com centros escolhidos pelo "ponto mais distante". Esse método empurrava os grupos para as bordas e cantos de cada área e deixava o meio dos mapas vazio.

## Drops de monstros

Pelo painel, use a página **Drops → Por monstro / por item**.

`Data\Item\ItemDrop.txt` tem uma linha por regra: `Index Level Grade Option0..6 Duration MapNumber MonsterClass MonsterLevelMin MonsterLevelMax DropRate`.
O `DropRate` é **por milhão** (1000 = 0,1% por monstro morto). Depois de editar, use Reload Item. A visão "por monstro" do painel liga uma regra com `MapNumber` aos monstros que **nascem** naquele mapa (arquivos de spawn). Para monstros de evento ou invasão, que aparecem em mapas variados, ela mostra só as regras sem mapa. No jogo, eles também pegam as regras do mapa onde estiverem.

- **Box of Kundun** (item 7179): cai em Kalima com 1% de chance por monstro.
  - Kalima 1–2 dão +1 (nível 8); Kalima 3, +2; Kalima 4, +3; Kalima 5, +4; **Kalima 6–7, +5** (nível 12).
  - Mapas: 24–29 e 36.
- **Custom Event Drop** (`Data\Custom\CustomEventDrop.txt`): chuva de joias e Box of Kundun +4/+5 em Lorencia às 20:00. Vem **desligado** (`CustomEventDropSwitch = 0` no `GameServerInfo - Custom.dat`).

## Itens que não podem ser largados

São duas travas, e as duas precisam permitir:
- **Servidor:** `Data\Item\ItemMove.txt` define `AllowDrop/AllowSell/AllowTrade/AllowVault` por item (item fora da lista = liberado).
  O kit proíbe largar no chão **asas, joias, pedras de refino e talismãs** (`AllowDrop = 0`). Para liberar, troque para `1` e use Reload Item.
- **Cliente:** cada item tem 7 permissões no `Data\Local\{Eng,Por,Spn}\item_*.bmd` (ex.: Jewel of Bless `0111110`; os itens "presos" costumam ter `0111000` ou `0000000`).
  Para mudar: `.\tools\Liberar-ItemCliente.ps1 -Codigos 7617, 7618 -Permissoes 0111110`. A ferramenta faz backup e recalcula a soma de verificação.
  Sem a soma certa, o cliente para com "Item_Por.bmd - File corrupted". O algoritmo foi lido do `main.exe` e está descrito no cabeçalho do script.
  Depois, os jogadores precisam receber os três arquivos novos (pacote de patch).

## Loja de cash: moedas e itens que saíram das lojas

**As três moedas** (issue #33, 29/09/2026). Cada aba da loja cobra numa moeda, guardada em `CashShopData` por conta:

| Aba | Moeda (`CoinIndex`) | Coluna | Como o jogador ganha |
|---|---|---|---|
| W Coin (C) | 508 | `WCoinC` | Comprando no site (pacotes de cash por PIX) e 1 por vitória no Blood Castle |
| W Coin (P) | 509 | `WCoinP` | **Só** vencendo o Blood Castle: 2 por vitória (`Data\Custom\CustomRewardCashShopPoint.txt`) |
| Goblin Point | 0 | `GoblinPoint` | 1 a cada 10 minutos online (`CashShopGoblinPointDelay` no `Common.dat`) e 3 por Blood Castle |

- A aba **W Coin (P)** tem 68 pacotes, quase a mesma loja da aba (C) e com os mesmos preços (100 a 1.400). Como o (P) só vem do Blood Castle, em 29/09 todas as contas estavam com 0 e ninguém conseguia comprar nada nela. **Não é defeito:** a compra funciona com saldo. Por decisão do dono, a aba ficou como está.
- Se um dia quiser mudar, há dois caminhos. (a) Esconder a aba inteira pelo painel (página Loja de Cash → desmarcar "Na loja"; os pacotes ficam guardados em `Data\CashShop\muchila-cash-oculto.txt`). (b) Dar (P) em mais eventos, na mesma tabela `CustomRewardCashShopPoint.txt` (Devil Square, Chaos Castle, Illusion Temple...), e deixar na aba (P) só consumíveis.
- Os pacotes do passe e dos cartões em (P) já estão escondidos: passe e cartões só por W Coin (C).

**Itens que saíram das lojas** (issue #32, 29/09/2026). O pedido era tirar do market e dos NPCs o que não se vende nem se equipa. Os 281 itens à venda foram cruzados com o `Item.txt` (quem equipa), o `ItemMove.txt` (`AllowSell`) e as 7 permissões do cliente (`item_*.bmd`, ver [Itens que não podem ser largados](#itens-que-não-podem-ser-largados); a 5ª é vender ao NPC).

- **Ficaram** os consumíveis que não vendem mas têm uso: selos, pergaminhos, poções elite, frutas de reset, tickets, talismãs e cartões de classe.
- **Saíram** os que o cliente não conhece (modelo e nome "Empty": item invisível, que não equipa, não vende e não usa):
  - Hanzo: 13,166 (não existe nem no servidor) e 14,125 Package Box;
  - Liaman: 13,95 Gladiator's Honor;
  - Loja de cash: 13,20 Wizard Ring (pacotes 24:34 e 31:35 escondidos).
- Nenhum deles cai de monstro nem sai de caixa. Antes de pôr um item novo à venda, confira se ele tem modelo no cliente.

### EXP dinâmica (painel)

Página **EXP dinâmica** (issue #37, 29/09/2026). Cada linha é uma faixa do `Data\Util\ExperienceTable.txt`: "para quem está entre estes níveis, a EXP é X% da taxa do plano".

- **A conta:** EXP final = "EXP (x)" do plano (página Taxas e opções) × "EXP %" da faixa ÷ 100. Isso foi comprovado no teste do #24. Níveis sem faixa ficam em 100%. As colunas Free/Vip1/Vip2/Vipzão mostram a EXP efetiva de cada faixa com as taxas atuais.
- **Colunas avançadas** (master de/até, resets de/até, master resets de/até): a faixa só vale para quem está dentro delas. O padrão 0–600 e 0–10000 vale para todos.
- **Salvar e aplicar:**
  - recusa faixas que se sobrepõem (o servidor usaria só uma) e "de" maior que "até";
  - mostra os níveis sem faixa;
  - grava só o bloco "Mu Chila - EXP dinamica" do arquivo, com backup `.bak-*`;
  - dá Reload Util.
- **Voltar à curva padrão:** carrega na tela a curva de 28/09 (100% até o nível 50, caindo até 10% nos 351–399, 100% no 400). Só grava ao salvar.
- **Testes:** `--testar-exp-dinamica` (cópia: regravar igual não muda nada, editar e trocar faixa, formato TAB/CRLF/end, sobreposição e "de > até" recusados, níveis sem faixa, desfazer).

### Taxas e opções (painel)

Página **Taxas e opções** (29/09/2026). Digite o valor novo na caixa (ela ganha borda amarela). Depois clique em **Salvar e aplicar**, e o painel:

1. grava a mesma mudança nas 5 pastas `GameServer*\DATA` (Mu Chila, Non-PvP, VIP, Castle Siege e BattleCore), com backup `.bak-*`. Troca só o valor, e o resto do arquivo fica igual;
2. dá o Reload do arquivo (Common, ChaosMix, Custom, Command, Character, Skill ou Event; drop de joias → Reload Item);
3. **confere na memória** dos GameServers se a opção valeu:
   - "✓ valendo na hora";
   - "✗ continua X na memória: só vale depois de REINICIAR o GameServer";
   - "(não deu para conferir)".

**O que fica travado (linhas cinza):**

- as opções que já são diferentes em cada servidor (nome, código, porta, PvP, `ServerLock`, eventos ligados em cada um);
- as de identidade e conexão;
- as chaves que aparecem 2 vezes no mesmo arquivo;
- durante um **bônus por tempo**, EXP, EXP master e drop, porque o arquivo está com o valor do bônus.

**Como a conferência funciona:**

- O GameServer lê cada opção com `GetPrivateProfileInt` e guarda o valor num objeto de configuração (`gServerInfo`, 0xB91B30). O painel acha onde cada opção fica pelos bytes do próprio GameServer (`ResetWatcher.Config.cs`).
- Antes de confiar, compara várias opções da memória com o arquivo.
- Em 29/09, com `--conferir-memoria` (só leitura, nos 3 GameServers), o painel achou quase todas as opções: Common 284 de 286, ChaosMix 244 de 244, Command 336 de 340, Character 549 de 549, Skill 150 de 150, Event 24 de 29. **Todas** eram iguais ao arquivo.
- O Custom (/offattack, loja offline) fica em outro lugar da memória: o painel acha só 1 de 81, e para essas opções mostra "não deu para conferir".

**O que já se sabe:**

- EXP, drop e zen valem na hora.
- Os registros (`Write*Log`) só valem ao reiniciar: o GameServer abre os arquivos de log quando liga. Isso foi conferido com `WriteChaosMixLog` e `WriteChatLog`.

**Testes:**

- `--testar-opcoes` (cópia): grava uma opção por plano e uma única nas 5 pastas, confere que só essas linhas mudaram, recusa opção travada e texto em opção numérica, e desfaz byte a byte.
- `--testar-opcoes-aplicar` (GameServer de testes ligado): o ciclo inteiro deu `ItemDropRate_AL1` ✓ na hora e `WriteChatLog` ✗ só ao reiniciar.

### Preço na loja de cash: vitrine x opções (29/09/2026)

**O que o jogo mostra e cobra é o preço da OPÇÃO** (1 dia, 7 dias, 10 un...), guardado no **produto**: `CashShopProduct.txt` no servidor e `IBSProduct.txt` no cliente. O preço do pacote (`CashShopPackage` / `IBSPackage`) é só a **vitrine** da lista; no kit, ele é igual ao da 1ª opção.

**O que estava errado:** até 29/09 o painel mudava só a vitrine. Pelo relato do dono: o Panda Ring (C) ficou "0" na lista, mas as opções continuaram 100 (1 dia) e 500 (7 dias), no jogo e na cobrança. O texto da descrição ("100 W Coin - 1Day") também não mudava.

**Como ficou:**

- A grade tem a coluna **Opções** (ex.: "1 dia: 100 · 7 dias: 500").
- Pacote de **uma opção**: a coluna Preço muda a opção (o que é cobrado) e a vitrine juntas.
- Pacote de **várias opções** (87 dos 140): o botão **"N preços…"** abre uma janela com o preço de cada opção. A vitrine passa a ser o da 1ª.
- **Produto compartilhado:** 108 produtos são usados pelo mesmo item nas abas W Coin (C) e (P). Ao mudar o preço de um deles, o pacote editado ganha uma **cópia própria** do produto (número novo, mesmas linhas). Assim o preço da outra aba não muda junto.
- **Descrição:** troca o número colado a "W Coin"/"Goblin Point" na linha daquela opção. Quando há mais de uma linha, usa o prazo ou a quantidade. O "200 Point" das frutas são pontos e não é tocado. Quando a descrição não tem o preço escrito, o log avisa e ela fica como estava.
- **Pacote de vários itens com um preço só** (ex.: "Level up Package", sem opções): continua pela coluna Preço (vitrine).
- **Ajuste automático:** ao salvar, a vitrine que estava diferente da 1ª opção volta a mostrar o preço cobrado. Em 29/09 só havia um caso: o Panda Ring (C), com 0.

**Testes (em cópias):**

- `--testar-cashshop-precos`: Panda Ring (C) com 150/700, sem mexer no (P); vitrine, descrição e produto próprio; opção única pela coluna Preço; 2ª edição no mesmo produto, sem nova cópia.
- `--testar-cashshop` e `--testar-cashshop-adicionar` continuam passando.

### Adicionar e remover itens da loja de cash

Painel, página **Loja de Cash** (29/09/2026).

**Adicionar item...**

- Você escolhe:
  - o item (busca pelo nome);
  - a aba (a aba define a moeda: W Coin (C), W Coin (P) ou Goblin Point);
  - o preço, o nível, a opção, o excelente, skill e sorte;
  - quantidade **ou** prazo em dias (0 = para sempre);
  - nome e descrição.
- O painel cria um pacote novo com um item e um preço.
- **Regras:**
  - Quantidade maior que 1 só para item que empilha (`Data\Item\ItemStack.txt`; a janela mostra o limite).
  - Item com prazo vai 1 por pacote. O prazo funciona nos itens de tempo (selos, pergaminhos, anéis, pets); para os outros, deixe 0.
  - A fonte da loja não tem acento, então o painel tira os acentos. Na aba W Coin (P), o nome ganha `[ ]`, como no kit.

**Remover da loja**

- Pacote criado pelo painel: **apaga de vez** as linhas dele.
- Pacote do kit: fica só **escondido** (volta marcando "Na loja" e salvando).

**Como funciona**

- Um pacote são 4 peças que precisam casar:
  - **servidor:** `Data\CashShop\CashShopPackage.txt` (o pacote: aba, moeda, preço, qual produto) e `CashShopProduct.txt` (o produto: item, nível, opções, quantidade, prazo);
  - **cliente:** `Data\InGameShopScript\512.2011.006\IBSPackage.txt` e `IBSProduct.txt` (o que aparece na tela).
- Cada peça nova é copiada de uma linha do kit do mesmo tipo (mesma moeda; produto por quantidade ou por prazo), trocando só os campos conhecidos. Os números são novos (maior existente + 1).
- A lista do que o painel criou fica em `Data\CashShop\muchila-cash-adicionados.txt`, e todo arquivo mexido ganha um backup `.bak-*` antes.
- No pacote criado pelo painel, mudar o preço na grade muda também o preço do produto.
- Salvar recarrega a loja nos GameServers na hora (Reload CashShop). **O jogador só vê o item novo depois de atualizar o cliente**: use "Publicar p/ launcher".

**Testes**

- `MuChilaAdmin.exe --testar-cashshop-adicionar <arquivo>` passou em tudo (só com `MUCHILA_ROOT` e `MUCHILA_CLIENTE` apontando para cópias). Ele:
  - cria pacotes por quantidade, por prazo e em Goblin;
  - confere as 4 peças e os erros esperados;
  - muda o preço, esconde e apaga;
  - confere que os 6 arquivos voltam byte a byte.
- No GameServer de testes (`C:\MuServerTeste`), pacotes com os números novos (pacote 269–271, produto 363–365):
  - carregaram na partida e no Reload, com "CashShop loaded successfully";
  - o servidor continuou de pé;
  - depois de apagados, os arquivos ficaram idênticos aos reais.
- **Falta o teste no jogo:** comprar um item adicionado e conferir que ele chega certo (quantidade, prazo, opções) e que a moeda descontada é a do preço.

## Resolução do cliente

O cliente S14 não tem opção de resolução dentro do jogo (issue #4). Com o jogo fechado, dois cliques em `2 - Cliente Season 14 Full\Resolucao - Configurar.bat`: ele mostra um menu e grava as duas chaves abaixo (não precisa de administrador). O arquivo vai nos dois pacotes dos amigos.

No registro, em `HKEY_CURRENT_USER\Software\Webzen\Mu\Config` (com o jogo fechado):

- `DisplayDeviceModeIndex`: 0=800x600, 1=1024x768, 2=1152x864, 3=1280x720, 4=1280x800, 5=1280x960, 6=1440x1080, 7=1600x900, 8=1680x1050, 9=1920x1080, 10=1440x900. De 11 em diante volta para 800x600. A lista pode variar conforme a placa de vídeo.
- `FullScreenMode`: 0 = janela, 1 = tela cheia.

## Distribuir o cliente aos amigos

1. O cliente pronto é a pasta `2 - Cliente Season 14 Full`, com o IP do Radmin e o nome Mu Chila.
2. Para mandar, compacte a pasta **sem a subpasta `Logs`**, que contém seus logins. O zip tem cerca de 1,2 GB.
3. Os amigos precisam:
   - entrar na mesma rede do Radmin VPN;
   - abrir o `main.exe` como administrador (ele exige);
   - fazer o login em até 30 segundos depois de escolher o servidor.
4. Se mudar o IP ou o nome, mande só os arquivos alterados: `Config - Dev.ini` e `Data\Local\**\serverlist*.bmd`.

**Launcher (`MuChilaLauncher.exe`, versão 2, 28/09/2026):** é o jeito normal de instalar e atualizar. Publicar: `tools\Publicar-Launcher.ps1`.

- **Janela:** a arte oficial do MU (duas telas de carregamento do próprio cliente, `tools\MuChilaLauncher\arte1.jpg` e `arte2.jpg`, embutidas no `.exe`) alternando a cada 9 s, com bolinhas para trocar.
- **Status do servidor ao vivo:** a cada 20 s testa a porta 44405 do endereço do launcher.
- **Atalho "Mu Chila" na área de trabalho** (versão 2.1, 29/09/2026): criado logo ao escolher a pasta na 1ª instalação, antes do download (se o download cair, o atalho já está lá). Quem já tinha o jogo, ou abre o launcher de dentro da pasta do jogo, ganha o atalho **uma vez** na próxima verificação sem erro. `Atalho=1` ou `Atalho=0` no `%APPDATA%\MuChila\launcher.ini` registra que já foi criado ou que o jogador desmarcou; se ele apagar o atalho, não volta. Teste: `MuChilaLauncher.exe --testar-garantir-atalho <pasta-do-jogo> <launcher.ini> <atalho.lnk>` (os 5 casos passaram).
- **Botões:** JOGAR, **Verificar integridade** e Configurações. A verificação normal confia no tamanho e na data de cada arquivo e é rápida. A de integridade recalcula o SHA-1 de todos e baixa de novo o que estiver corrompido.
- **Configurações (painel da direita):**
  - resolução (a tabela de 11 do cliente; avisa as maiores que a tela), tela cheia, idioma (Português/English) e volume (0–10). Ficam no registro que o `main.exe` lê, `HKCU\Software\Webzen\Mu\Config` (`DisplayDeviceModeIndex`, `FullScreenMode`, `LangSelection`, `VolumeLevel`), e são gravadas de novo ao clicar em JOGAR;
  - pasta do jogo (Alterar...) e "fechar o launcher ao abrir o jogo" (`%APPDATA%\MuChila\launcher.ini`).
- Espanhol não é oferecido: os itens novos do Mu Chila só existem em `Eng` e `Por`.
- **Programas obrigatórios e diagnóstico** (versão 2.2, 30/09/2026, `Diagnostico.cs`). Motivo: no PC de um amigo o mouse virava "carregando" e o jogo não abria.
  - **Programas obrigatórios do Windows:** **Visual C++ 2013 x86** (`msvcr120.dll`/`msvcp120.dll`) e **DirectX End-User Runtime de junho/2010** (`d3dx9_43.dll`/`d3dcompiler_43.dll`), conferidos em `SysWOW64`.
    - A lista vem das importações das DLLs do cliente. O `main.exe` carrega o `Main.dll`, que precisa do VC++ 2013. Sem ele, o jogo fecha sem mostrar nada.
    - O DirectX é pedido pelo `awesomium.dll` só quando usado (navegador do jogo).
  - **Quando confere:** depois de cada verificação e ao clicar em JOGAR. Se faltar algo, abre uma janela com **Instalar automaticamente** (e **Jogar mesmo assim** no JOGAR).
  - **Instalação automática:** baixa primeiro de `<ServerUrl>/requisitos/` e, se falhar, da Microsoft. Instala em silêncio e o Windows pede permissão de administrador. Se não der certo (download falhou ou o jogador negou a permissão), a janela mostra o motivo, o link do instalador e o link da página oficial.
  - `Publicar-Launcher.ps1` baixa os dois instaladores uma vez para `launcher\requisitos\`.
  - **Acompanhamento do jogo:** depois do JOGAR, o launcher acompanha o jogo por 30 s. Se o jogo fechar sozinho, o launcher volta, explica o código de saída (ex.: `0xC0000135` = DLL não encontrada) e grava um diagnóstico.
  - **Botão DIAGNÓSTICO:** gera o relatório e abre no Bloco de Notas.
  - **Logs** (pasta do jogo `\Logs\launcher\`, ou `%APPDATA%\MuChila\logs`):
    - `launcher.log`: uma linha por evento (abriu, atualizou, requisitos, instalou, JOGAR, como o jogo terminou).
    - `diagnostico-<data>.txt` (guarda os 20 mais novos), com estas seções:
      1. computador: Windows, RAM, placa de vídeo e driver, antivírus;
      2. programas obrigatórios;
      3. DLLs que o jogo carrega ao abrir (`main.exe` + `Main.dll` e as DLLs da pasta que eles pedem; GameGuard e outras DLLs que não são carregadas ficam de fora);
      4. SHA-1 dos arquivos da pasta principal comparado com o manifesto do servidor;
      5. pasta do jogo;
      6. código de saída do jogo;
      7. erros do `main.exe` no Log de Eventos do Windows;
      8. **conclusão**: diz se o problema é do computador ou de arquivos do jogo.
    - Erro inesperado do próprio launcher vai para `%APPDATA%\MuChila\launcher-erros.log`.
  - **Teste:** `--diagnostico <saida.txt> [pasta]` (código 0 = nada faltando, 2 = falta programa obrigatório).
- **Código e testes:** `MuChilaLauncher.cs` tem a lógica (pasta, verificar, baixar, abrir o jogo); `LauncherUi.cs`, a janela (C# 5, desenhada em GDI+); `Diagnostico.cs`, programas obrigatórios, logs e diagnóstico.
  - `--verificar <saida> <pasta>` e `--integridade <saida> <pasta>`: silenciosos.
  - `--foto <saida.png> <pasta>`: fotos das telas, sem rede.

## Versionar alterações

```powershell
.\tools\Sincronizar-Servidor.ps1        # copia C:\MuServer para a pasta 3 do repositório
git add -A; git commit -m "descrição"; git push
```

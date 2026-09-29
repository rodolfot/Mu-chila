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

Atalho: `C:\MuServer\Mu Chila Admin.lnk` (programa em `C:\MuServer\MuChilaAdmin`, código em `tools\MuChilaAdmin`).
Precisa do .NET 10 Desktop Runtime e deve rodar na máquina do servidor, porque conversa com as janelas dos servidores e com o SQL local.

| Aba | O que faz |
|---|---|
| **Servidor** | Mostra os 8 processos (os três GameServers separados), os jogadores online (conta, personagem, IP) e jogadores e monstros de cada GameServer. Atualiza a cada 5 s. |
| | Botões: iniciar e parar tudo pelo launcher, desconectar todos os jogadores (os personagens são salvos), abrir o MuEditor e o launcher. |
| | "Recarregar sem reiniciar": manda o Reload escolhido para os dois GameServers **e** o Castle Siege ao mesmo tempo. |
| **Eventos** | Escolha o evento e em quantos minutos ele começa, e clique em "Disparar evento". Veja [Eventos](#eventos). |
| **Bônus** | Ex.: "EXP + EXP master x2 por 60 minutos", começando agora ou numa data e hora. Multiplica a taxa de cada plano em todos os GameServers e avisa os jogadores no início, a cada 5 minutos e no fim. Cancela a qualquer momento. Quem liga e desliga é o vigia. Ver [Bônus de EXP e drop](#bônus-de-exp-e-drop). |
| **Avisos** | "Enviar agora" manda uma mensagem para todos os jogadores. Também lista e edita os avisos automáticos (repetidos). Ver [Avisos para todos os jogadores](#avisos-para-todos-os-jogadores). |
| **Lojas** | Escolha o NPC e edite o que ele vende: adicionar (busca pelo nome), remover, reordenar, nível, durabilidade e opções. Mostra quantos dos 120 espaços (8×15) da janela a loja ocupa e avisa o que não cabe. "Salvar e aplicar" faz backup e recarrega as lojas sem reiniciar. |
| **Loja de Cash** | A loja da tecla X. Muda o preço, esconde e mostra pacotes, **adiciona item** (pacote novo: item, aba/moeda, preço, nível e opções, quantidade ou prazo) e **remove da loja**: apaga de vez o que o painel criou, e os pacotes do kit ficam só escondidos. Grava servidor + cliente e recarrega a loja. Os jogadores só veem depois de "Publicar p/ launcher". Ver [Adicionar e remover itens da loja de cash](#adicionar-e-remover-itens-da-loja-de-cash). |
| **Drops** | **Por monstro / por item** (a primeira sub-aba): escolha um monstro e veja tudo o que ele dropa (inclusive as regras que valem para vários monstros), com a chance em % e em "1 em N", para quem a regra vale e quantos monstros ela alcança, mais o drop comum e o zen dele; ou escolha um item e veja de onde ele cai. Adiciona item a um monstro, faz um item cair de um monstro, mapa, faixa de nível ou qualquer monstro, muda a chance (duplo clique) e remove; regras que valem para vários monstros avisam antes. Edita as mesmas linhas das abas avançadas. As duas abas **Avançado** são o arquivo completo: **Itens que os monstros dropam** (`Data\Item\ItemDrop.txt`): cada regra diz qual item cai de qual monstro, mapa ou faixa de nível e com que chance em % (1% = 1 em 100). Adicionar (busca pelo nome), escolher monstro ou mapa numa lista, remover, filtrar; "Salvar e aplicar" faz backup e dá Reload Item. **Drop comum e zen por monstro** (`Monster.txt`: ItemRate, MoneyRate, MaxItemLevel): "Salvar e aplicar" faz backup e recarrega os monstros só nos GameServers e só fora da invasão (se houver uma no ar, agenda para quando ela acabar; o painel precisa ficar aberto). Linhas não alteradas são regravadas iguais. |
| **Taxas e opções** | Muda as opções dos GameServers sem abrir arquivo. **Principais** junta a economia num lugar só: EXP, EXP master, drop de itens, zen, **drop de joias** (`ItemDrop.txt`), chance das joias (Soul/Life/Harmony), Chaos Machine +10 a +15, pontos por nível e máximo por atributo, baús (/ware), MU Helper, /offattack, Goblin Point e EXP em party. As outras visões mostram **todas** as opções de cada um dos 7 arquivos `GameServerInfo - X.dat`. Colunas Free/Vip1/Vip2/Vipzão nas opções por plano. Ver [Taxas e opções](#taxas-e-opções-painel). |
| **Itens novos** | Cria um item novo a partir de um que já existe: mesmo visual (modelo 3D), nome e atributos próprios (dano, defesa, velocidade, durabilidade, requisitos). Grava no `Item.txt` do servidor (Reload Item na hora) e nos arquivos do cliente da pasta do repositório. Os jogadores só recebem depois de "Publicar atualização do cliente". Teste antes abrindo o jogo pela pasta do repositório. Ver [Itens novos](#itens-novos). |
| **Itens e baú** | Mostra o inventário de qualquer personagem ou o baú da conta, com nome, nível e opções. Remove itens só com a conta fora do jogo e com backup em `C:\MuServer\DB\backup-itens-*.csv`. Presentes pela Gremory Case aparecem ao lado; criar presentes pelo painel fica desligado até a calibração (ver [Itens e baú](#itens-e-baú)). |
| **VIP e contas** | Lista as contas com nível, validade, ban, status e personagens. Aplica VIP 1–3 por N dias, remove VIP, bane e desbane. |
| **Passe dos Mapas** | Dá passe a uma conta (em minutos, horas ou dias; soma ao que ela já tem) e tira o passe. Mostra quem tem e quanto falta, os mapas que exigem passe, o histórico (loja de cash, site e painel) e quem o vigia tirou dos mapas. A caixa "Vigia cobra o passe" liga e desliga a cobrança. Ver [Passe dos Mapas](#passe-dos-mapas-acima-do-nível-400). |
| | "Zerar habilidades master": escolhe um personagem da conta, apaga a árvore master, devolve os pontos (1 por Master Level) e tira os poderes master da lista de habilidades (`MagicList`). Os melhorados voltam à habilidade normal (ex.: 330 Twisting Slash Improved → 41 Twisting Slash), seguindo a coluna `ReplaceSkill` do `MasterSkillTree.txt`. Serve também para quem mostra "Suces de Atq" negativo na janela (C); ver PROBLEMAS-E-SOLUCOES. |
| | "Dar pontos" (aba VIP e contas): escolhe um personagem da conta, o tipo e a quantidade. **Pontos de atributo** somam em `Character.LevelUpPoint` (o jogador distribui na janela C); **pontos master** somam em `MasterSkillTree.MasterPoint` (só quem já tem árvore master). Número negativo tira, sem passar de 0. Cada entrega fica em `C:\MuServer\DB\muchila-pontos-dados.csv`. Linha de comando: `MuChilaAdmin.exe --dar-pontos <conta> <personagem> <atributo\|master> <quantidade> <arquivo-de-resultado>`. |

- **Mexeu na conta, ela sai do jogo sozinha** (decisão do dono, 28/09/2026). VIP, ban, "Dar pontos", "Zerar habilidades master" e remover item (aba Itens e baú), com a conta online, deslogam a conta **sem perguntar**. VIP e ban valem no próximo login, e os outros precisam dela fora porque o servidor grava o personagem na saída e desfaria a mudança. "Nível e reset..." só manda o personagem para a seleção de personagem (não precisa deslogar a conta).
- **Como o painel desloga** (também no botão "Desconectar jogador selecionado", aba Servidor):
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
  - Botão "Levar à seleção de personagem" (aba Servidor): o mesmo que o jogador escolher "Trocar personagem" no jogo, sem desconectar.
  - Também: resets pedidos pelo site com o jogo aberto, bônus por tempo, avisos "enviar agora", [Passe dos Mapas](#passe-dos-mapas-acima-do-nível-400) (`vigia-passe-mapas.ligado`) e Magic Backpack (`vigia-mochila.ligado`).
- Para publicar depois de mudar o código: feche o painel e rode `.\tools\Publicar-Painel.ps1`. Ele fecha o vigia, publica em `C:\MuServer\MuChilaAdmin`, cria as chaves `.ligado` que faltarem, refaz o atalho da pasta Inicializar, liga o vigia e mostra a sonda.

## Bônus de EXP e drop

Aba **Bônus** do painel. Desde 28/09/2026 quem liga e desliga é o **vigia** (`MuChilaAdmin --vigia-reset`), não mais o `BonusManager.dat` do kit.
O BonusManager era caixa-preta: não mostrava se o bônus tinha começado, não deixava cancelar um ativo, e cada agendamento pedia Reload Event, que reinicia a contagem do Blood Castle.

- **Como funciona:** os bônus ficam em `C:\MuServer\MuChilaAdmin\bonus.json`. A cada 5 s o vigia confere:
  - começou → grava em cada `GameServer*\DATA\GameServerInfo - Common.dat` a taxa do plano × multiplicador (`AddExperienceRate_AL0..3`, `AddMasterExperienceRate_AL0..3`, `ItemDropRate_AL0..3`) e dá **Reload Common**;
  - terminou ou foi cancelado → devolve a taxa original e dá Reload Common.
- **Vários ao mesmo tempo** somam: x2 + x1,5 = x2,5.
- **Taxa mudada à mão durante um bônus:** o valor novo vira a taxa normal e não é sobrescrito na volta.
- **Avisos na tela** (pelo `Notice.txt`): "Começou: ... até HH:mm" no início, lembrete "Bônus ativo" a cada 5 minutos e "Terminou..." no fim (ou "foi encerrado", se cancelado).
- **Início:** agora, ou numa data e hora escolhida. **Cancelar** vale a qualquer momento, inclusive com o bônus ativo.
- **O vigia precisa estar rodando.** A aba avisa em vermelho quando ele está parado, e agendar um bônus liga o vigia.
- Durante um bônus, a página Informações do site mostra as taxas com o bônus, porque lê o `Common.dat`.
- As linhas antigas do painel no `BonusManager.dat` (vagas 3–9, `//MuChilaAdmin`) não são mais criadas e podem ser apagadas à mão.
- Testes: `MuChilaAdmin.exe --testar-bonus <saída>` com `MUCHILA_ROOT` apontando para uma cópia (9 checagens).

## Itens e baú

- **Formato:** cada item ocupa 16 bytes em `Character.Inventory` (237 posições; 0–11 equipado) e em `warehouse.Items` (240 posições: 0–119 baú, 120–239 baú estendido). O layout, conferido em inventários reais, está descrito no topo de `tools\MuChilaAdmin\Items.cs`.
- **MuEditor:** é da Season 8 e **não deve salvar** inventários do S14, porque pode apagar o inventário expandido e o baú estendido. Use a aba **Itens e baú** para ver e remover.
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
- **Comandos de jogador:** `/addstr`, `/addagi`, `/addvit`, `/addene` e `/addcmd` com a quantidade de pontos; `/reset`, `/pkclear`, `/post`.
  O modo automático (`/addstr auto 1`) segue a sintaxe do MuEmu e ainda não foi testado.

## Eventos

Não há comando para iniciar evento na hora. Tudo vem das agendas em `Data\Event\*.dat`.
Bloco 0 = `Index  Year  Month  Day  DoW  Hour  Minute  Second`, onde `*` significa qualquer valor.

- **Invasões** (`InvasionManager.dat`): 0 Underworld, 1 Red Dragon, 2 Golden, 3 White Wizard, 4 Ano Novo, 5 Páscoa, 6 Verão, 7 Christmas, 8 Medusa, 9 Demônios invocados, 10 Ovos.
  - **Golden:** a cada **10 minutos** (00, 10, 20, 30, 40 e 50), com duração de **540 s**, para não sobrepor.
  - **Red Dragon:** 0:15, 4:15, 8:15, 12:15, 16:15 e 18:32.
- **Disparar uma vez:** use a aba **Eventos** do painel Mu Chila Admin. Ela grava uma linha com data exata no `.dat` do evento, marcada com `//MuChilaAdmin`, e manda Reload Event para o GameServer e o Castle Siege.
  - Invasões começam no minuto seguinte (padrão de 1 minuto).
  - Blood Castle, Devil Square, Chaos Castle e Illusion Temple têm sala de espera e avisam 5 minutos antes. Por isso o padrão é 6 minutos.
  - Castle Deep e Moss Merchant também podem ser disparados.
  - As linhas antigas continuam no arquivo e não voltam a disparar, porque têm ano, mês e dia fixos. "Limpar disparos já executados" remove as que passaram há mais de 30 minutos.
  - Na primeira alteração do dia, o painel salva uma cópia `.bak-AAAAMMDD` do arquivo.
  - À mão: adicione uma linha como `1  2026  9  23  *  21  5  0` antes do `end` do bloco de agenda e use Reload Event. O primeiro número é o índice da invasão e só existe no `InvasionManager.dat`.
- **Conferir se nasceu:** o título da janela do GameServer mostra `MonsterCount`, que sobe quando a invasão aparece.
- **Testado em 28/09/2026 no servidor de testes:** uma linha com data completa (`2 2026 9 28 * 0 9 30`) disparou a dourada na hora (+65 monstros). Ou seja, o agendamento do painel funciona.
  - Cada invasão **sorteia o mapa** do seu grupo no bloco 2 (o Red Dragon cai em Lorencia, Devias ou Noria). Pode acontecer longe de quem está olhando.
  - **Blood Castle, Devil Square, Chaos Castle e Illusion Temple** só acontecem se alguém entrar com o ingresso na abertura. Sem ninguém, o log mostra "Not enough users" e o evento fecha vazio.
  - **Todo Reload Event reinicia a contagem** do Blood Castle e do Chaos Castle ("Sync Start Time" no log), e os jogadores recebem de novo o aviso de que o Blood Castle vai começar. Por isso esse aviso aparece a cada disparo feito pelo painel.

## Itens novos

Aba **Itens novos** do painel (`tools\MuChilaAdmin\NewItems.cs`, desde 28/09/2026). O item novo copia o item base e troca:

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

Aba **Avisos** do painel, usando o `Data\Util\Notice.txt` do kit. O GameServer não tem opção de aviso no menu.

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

**Pelo painel (testes):** aba **VIP e contas → "Nível e reset..."**. Define nível (1–400) ou master level (0–600), dá EXP (sobe os níveis que ela der), ou faz Reset, Master Reset e Supreme Reset num personagem. Usa a mesma fila (`Tipo` `nivel`, `exp`, `mlevel`, ou os do site, com o valor em `Valor`) e a procedure `MuChila_AjustarPersonagem`. Com o jogo aberto, o personagem vai para a seleção e a mudança vale ao entrar de novo. Subindo de nível, soma os pontos da regra do jogo (5 ou 7 por nível, +1 da 3ª classe; 1 ponto master por master level). Os resets seguem os mesmos requisitos do site.

## Passe dos Mapas (acima do nível 400)

Decisão do dono (28/09/2026): Nixies Lake, Deep Dungeon 1–5, Swamp of Darkness e Kubera Mine 1–5 (nível mínimo acima de 400) só com o passe. Ferea e Swamp of Calmness pedem exatamente 400 e ficam livres.

- **Banco** (`DB\1 - Querys\MuChila-PasseMapas.sql`): o passe é por **conta** (`dbo.MuChila_PasseMapas.Expira`), com histórico em `MuChila_PasseMapasLog`. A lista de mapas fica em `MuChila_PasseMapasLista`; para incluir ou tirar um mapa, basta mudar essa tabela, e o vigia relê a cada minuto. `MuChila_PasseAdicionar @Conta, @Segundos, @Origem` soma ao passe ativo (ou começa agora).
- **No jogo (WCoin):** Gold Channel Ticket da Cash Shop, de 1, 3, 7 ou 30 dias. Ao usar o ticket guardado na loja, o GameServer manda o JoinServer rodar `WZ_SetAccountLevel`, que foi ajustada para somar ao passe (nível 0 do produto) sem mexer no VIP. Preços e textos: `tools\Loja-PasseMapas.ps1` (`-Restaurar` volta ao kit).
- **No site (PIX):** Loja → "Passe dos Mapas" (pacotes `passe-1/3/7/30` em `muchila.pacotes.json`, R$ 9/18/27/45). Vale na hora, sem sair do jogo.
- **Quem cobra:** o vigia (`vigia-passe-mapas.ligado`) confere a cada 1 s. Quem estiver num mapa da lista sem passe vai para a seleção de personagem e, 2 s depois de sair do jogo, é posto em Lorencia (área do gate 17) no banco. Se reentrar antes, é mandado de novo. Se a consulta ao banco falhar, ninguém é tirado.
- **Dar ou tirar passe (testes):** aba **Passe dos Mapas** do painel. Para testar o vencimento, dê alguns minutos, entre num mapa da lista e espere. Pela linha de comando: `MuChilaAdmin.exe --testar-passe <saida>` (conta descartável, criada e apagada pelo teste).
- Não há mensagem na tela explicando a saída (o GameServer é fechado). A regra está na página Informações e na Loja do site.

## Evolução de classe (missões; `/change` desligado)

**DW, DK, Elfa e Summoner já nascem na 2ª classe** (gatilho `TR_MuChila_ClasseInicial` no banco), porque o cliente S14 mostra a básica e a 2ª com o mesmo nome.
Itens e skills marcados com `2` nas colunas de classe do `Item.txt` exigem a 2ª classe (ex.: a foice Brova/"Beuroba" exige Blade Knight).
Desde 27/09/2026 o **`/change` está desligado** para todas as contas (`CommandChangeEnable_AL0..3 = 0` em `GameServerInfo - Command.dat`; quem tentar recebe a mensagem 81 "Você não tem permissão para usar /change").
A 3ª e a 4ª classe saem pelas **missões** de `Data\Quest\Quest.txt` (nível mínimo 150): índices 0–2 levam à 2ª classe, 3 é a do combo (DK e GL), 4–6 à 3ª e 7–9 à 4ª. A recompensa que evolui é o tipo 4 (2ª), 16 (3ª) e 32 (4ª) em `QuestReward.txt`.
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

Pelo painel, use a aba **Drops → Por monstro / por item**. Teste sem salvar nada, com foto da aba: `MuChilaAdmin.exe --testar-drops-visao <saida> <foto.png>`.

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
- Se um dia quiser mudar, há dois caminhos. (a) Esconder a aba inteira pelo painel (aba Loja de Cash → esconder; os pacotes ficam guardados em `Data\CashShop\muchila-cash-oculto.txt`). (b) Dar (P) em mais eventos, na mesma tabela `CustomRewardCashShopPoint.txt` (Devil Square, Chaos Castle, Illusion Temple...), e deixar na aba (P) só consumíveis.
- Os pacotes do passe e dos cartões em (P) já estão escondidos: passe e cartões só por W Coin (C).

**Itens que saíram das lojas** (issue #32, 29/09/2026). O pedido era tirar do market e dos NPCs o que não se vende nem se equipa. Os 281 itens à venda foram cruzados com o `Item.txt` (quem equipa), o `ItemMove.txt` (`AllowSell`) e as 7 permissões do cliente (`item_*.bmd`, ver [Itens que não podem ser largados](#itens-que-não-podem-ser-largados); a 5ª é vender ao NPC).

- **Ficaram** os consumíveis que não vendem mas têm uso: selos, pergaminhos, poções elite, frutas de reset, tickets, talismãs e cartões de classe.
- **Saíram** os que o cliente não conhece (modelo e nome "Empty": item invisível, que não equipa, não vende e não usa):
  - Hanzo: 13,166 (não existe nem no servidor) e 14,125 Package Box;
  - Liaman: 13,95 Gladiator's Honor;
  - Loja de cash: 13,20 Wizard Ring (pacotes 24:34 e 31:35 escondidos).
- Nenhum deles cai de monstro nem sai de caixa. Antes de pôr um item novo à venda, confira se ele tem modelo no cliente.

### Taxas e opções (painel)

Aba **Taxas e opções** (29/09/2026). Clique na célula e digite o valor novo (ela fica amarela). Depois clique em **Salvar e aplicar**, e o painel:

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

### Adicionar e remover itens da loja de cash

Painel, aba **Loja de Cash** (29/09/2026).

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
- **Botões:** JOGAR, **Verificar integridade** e Configurações. A verificação normal confia no tamanho e na data de cada arquivo e é rápida. A de integridade recalcula o SHA-1 de todos e baixa de novo o que estiver corrompido.
- **Configurações (painel da direita):**
  - resolução (a tabela de 11 do cliente; avisa as maiores que a tela), tela cheia, idioma (Português/English) e volume (0–10). Ficam no registro que o `main.exe` lê, `HKCU\Software\Webzen\Mu\Config` (`DisplayDeviceModeIndex`, `FullScreenMode`, `LangSelection`, `VolumeLevel`), e são gravadas de novo ao clicar em JOGAR;
  - pasta do jogo (Alterar...) e "fechar o launcher ao abrir o jogo" (`%APPDATA%\MuChila\launcher.ini`).
- Espanhol não é oferecido: os itens novos do Mu Chila só existem em `Eng` e `Por`.
- **Código e testes:** `MuChilaLauncher.cs` tem a lógica (pasta, verificar, baixar, abrir o jogo); `LauncherUi.cs`, a janela (C# 5, desenhada em GDI+).
  - `--verificar <saida> <pasta>` e `--integridade <saida> <pasta>`: silenciosos.
  - `--foto <saida.png> <pasta>`: fotos das telas, sem rede.

## Versionar alterações

```powershell
.\tools\Sincronizar-Servidor.ps1        # copia C:\MuServer para a pasta 3 do repositório
git add -A; git commit -m "descrição"; git push
```

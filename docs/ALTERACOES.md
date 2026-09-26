# Alterações em relação ao kit original

Comparação entre `1 - MuServer Season 14 - Aprendiz Mu Online` (kit original) e
`3 - MuServer Mu Chila (servidor configurado)` / `2 - Cliente Season 14 Full`. Feito em 23/09/2026.

## Servidor (`C:\MuServer`)

| Arquivo | Alteração | Motivo |
|---|---|---|
| `ConnectServer\ServerList.dat` | `TEUIPAQUI` → `"26.139.39.123"`; nomes → `Mu Chila`, `Mu Chila 2`, `Mu Chila 3`, `Mu Chila BattleCore`, `Mu Chila CastleSiege` | IP do Radmin e nome do servidor |
| `Data\MapServerInfo.dat` | `STEUIPAQUI` → `S26.139.39.123` | IP para troca de mapa entre servidores; o "S" é obrigatório |
| `GameServer*\DATA\GameServerInfo - Common.dat` | `ServerName` → `Mu Chila`, `Mu Chila CastleSiege`, `Mu Chila BattleCore` | Nome do servidor. `CustomerName` não foi alterado |
| `GameServer*\DATA\GameServerInfo - Common.dat` | `EventInventoryExpire` 31/12/2015 → 31/12/2037 | A data vencida bloqueava pegar e mover caixas de evento |
| `Data\Lang\English.xml` | Mensagens 495 e 581 → "Welcome to Mu Chila..." | Boas-vindas |
| `Data\Event\InvasionManager.dat` | Invasão Golden (índice 2) a cada 10 min; duração 600 → 540 s | Pedido: dragões dourados a cada 10 minutos |
| `Data\Item\Item.txt` | `DropItem` 1 → 0 em 19 caixas da seção 14 (7591–7625) | O servidor não abre essas caixas; os prêmios via EventItemBag não funcionaram e foram removidos |
| `Data\Item\Item.txt` | `DropItem` 1 → 0 no Rune Mage Character Card (14,487) | Cartões de personagem não têm uso neste servidor |
| `Data\Item\Item.txt` | `DropItem` 1 → 0 em 21 equipamentos que o cliente marca como intransferíveis (Legendary Rune 125/Bound, Rune Sphinx, Storm Jahad...) | Ficavam presos no inventário de quem pegava |
| `GameServer*\DATA\GameServerInfo - Common.dat` | `HelperActiveLevel` 80 → 1 (nos três GameServers) | Issue #8: depois do `/reset` o personagem volta ao nível 1 e o MU Helper ficava bloqueado até o 80 |
| `GameServer*\DATA\GameServerInfo - Common.dat` | `MasterSkillTreeMaxLevel` 330 → 600 (nos três GameServers); nível total máximo 730 → 1000 | Os melhores itens pedem nível total 800–1000 (Wings of Flame God 800, Soul Sword 900, Blue Eye 1000) e ficavam inalcançáveis |
| `GameServer*\DATA\GameServerInfo - Common.dat` | Por tipo de conta (Free/VIP1/VIP2/VIP3): experiência e experiência master 2000x → 100/300/850/2000x; drop 100% → 50/80/110/150% | Planos definidos pelo dono em 25/09/2026 (ver SITE.md). Antes o VIP não tinha vantagem nenhuma |
| `GameServer*\DATA\GameServerInfo - Custom.dat` | `CustomAttackEnable_AL1..3` 0 → 1 | O VIP perdia o ataque automático que a conta comum tem |
| `GameServer*\DATA\GameServerInfo - Command.dat` | `CommandPostSellyLevel_AL1` → `CommandPostSellLevel_AL1` | Erro de digitação do kit: o VIP 1 ficava sem o nível mínimo do /postsell |
| `Data\Move\Gate.txt` | Portão 17 (chegada em Lorencia): X 133–151 → 133–143 | Issue #9: a área incluía o miolo da fonte (X 145–150, Y 126–129), que o servidor marca como livre e o cliente não deixa sair |
| `Data\Item\Item.txt` | `DropItem` 1 → 0 em Frost Soul (14,449) e Soul Anvil (14,450) | Issue #10: nada no servidor usa esses itens e o cliente não deixa vender nem largar |
| `Data\EventItemBag\009 - Box of Kundun 3.txt`, `187 - Devil’s Boxl.txt`, `188 - High Devil’s Box.txt` | Removidos os itens (7,23) e (7,76), que não existem no cliente nem no `Item.txt` | Issue #5: a caixa gerava um item fantasma, que aparecia como "+7" sem nome |
| `Data\Item\ItemValue.txt` | Joias e pacotes de joias (Bless, Soul, Life, Creation, Chaos, Guardian: 24 linhas) com o valor ÷ 3; "Bundle of Jewel of Soul 3" passa do nível 0 (repetido) para o 2 | Issue #2: o servidor cobrava e pagava 3× o preço que o cliente mostra |
| `Data\Item\ItemDrop.txt` | Box of Kundun em Kalima (1%): K1–2 +1, K3 +2, K4 +3, K5 +4, K6–7 +5 | O kit não tinha drop de Box of Kundun |
| `Data\EventItemBag\010 - Box of Kundun 4.txt`, `011 - Box of Kundun 5.txt` | Além das joias e tickets (agora ~1/3 das aberturas), dão sets e armas de todas as classes, **sem os 4 melhores de cada classe**. +5: os 2 melhores sets e as 2 melhores armas permitidos de cada classe, +2 a +6. +4: os 2 seguintes, +0 a +4. Sorte e opção sorteadas; sem excelente. Gerado por `tools\Montar-CaixasKundun.py` | 26/09/2026, pedido do dono: as caixas só davam joias e tickets |
| `Data\Monster\Monster.txt` | Deep Dungeon (767–778), Swamp of Darkness (786–794) e Kubera Mine (810–816): `ItemRate/MoneyRate/MaxItemLevel` 0/0/0 → 250/33/6 (os valores do Nix) | 25/09/2026: esses 28 monstros (níveis 160–400) não dropavam nada, nem zen; já vinha assim no kit |
| `Data\Item\ItemDrop.txt` | As 22 regras genéricas com taxa (joias de Chaos, Bless, Soul, Life e Creation, Sphere, pergaminhos e chaves de evento, Spirit, Sign of the Lord, caixas seladas, Card Piece...) ganharam cópia para monstros de nível 151–400, com a mesma taxa | As regras do kit iam só até o nível 150 |
| `Data\Lang\Portuguese.xml` (novo) + `Data\LangManager.xml` | 548 mensagens do servidor em português (arquivo em Windows-1252, que é o que o cliente lê); os idiomas 0 e 2 apontam para ele | Pedido: jogo em português (os personagens usam o idioma 0) |
| `Data\Lang\Portuguese.xml` e `Data\Lang\English.xml` | 17 avisos de invasão (IDs 192–200, 202–207, 210, 211) copiados de `<Message>` para `<InvacionMsg>` | O servidor procura ali os avisos do `InvasionManager.dat`; o jogo mostrava "Could not find message 194/197/200" |
| Cliente: `Idioma - Portugues.reg` / `Idioma - Ingles.reg` (novos) | Trocam o `LangSelection` do cliente | O cliente já tem textos `Por` oficiais |
| `Data\Monster\MonsterSetBase\*.xml` (43 mapas) | Spawns de SPOT/MONSTER refeitos a partir do kit (`tools\Distribuir-Spawns.py`): grupos de 3 espalhados por igual (k-means) e pontos de farm de 12 monstros, mesmo total de monstros do kit (o GameServer só usa os índices 0–9169 para monstros). Raklion (+10 tiles de área) e Nars (mapa todo) com área maior; cerca de 13% dos monstros de mapas onde ninguém caça (Deep Dungeon 1–5, Swamp of Darkness, Kalima 1–7) foram para os mapas de caça (Raklion, Nars, Kanturu 1, Tarkan, Karutan 1, Kubera Mine 1, Atlans) | 26/09/2026: o agrupamento de 23/09 (grupos de 5 no "ponto mais distante") deixava o meio dos mapas vazio; pedido: respawn equilibrado e pontos de farm |
| `Startup - Iniciar Server.lnk` | Aponta para `C:\MuServer\2 - Ligar Servidor\Ligar Servidor.exe` | Apontava para uma pasta de Downloads de outro PC |
| `Criar Conta.bat` / `Criar Conta.ps1` | Novos | Criar conta de jogo pelo ODBC |
| `Cliente Season 14 - main.lnk` | Novo | Atalho para o cliente |
| `DB\1 - Querys\Correcoes (Gremory e RestoreItem)\` | Novos: `GremoryCase_MuDevs.sql`, `RestoreItem_MuDevs.sql` | Objetos que faltavam no backup |
| `1 - MuEditor\config.ini` | `SERVER` `(local)` → `.\MUONLINE`, `PORT` `1433` → `61764` (nas duas seções) | O kit apontava para a instância padrão do SQL; o banco está na instância nomeada `MUONLINE` |
| `GameServerNonPvP\` (novo, 26/09/2026) | Cópia do `GameServer` com `ServerName = Mu Chila Non-PvP`, `ServerCode = 21`, `ServerPort = 55902`, `NonPK = 1`, `IsArcaWarServer = 0` | Pedido: um servidor sem PvP, como sub-servidor do Mu Chila no cliente (Mu Chila-2; ver OPERACAO) |
| `ConnectServer\ServerList.dat` | Linha 21 "Mu Chila 2" (reservada pelo kit, nunca usada) → "Mu Chila Non-PvP", porta 55902, `PasiveServer` 0 → 1 | Servidor Non-PvP na lista; `PasiveServer = 1` faz o cliente mostrar "Non-PvP" em vez de "PvP" |
| `Data\MapServerInfo.dat` | Servidor 21 (porta 55902) hospedando todos os mapas (`InitSetVal` 0 → 1); 48 linhas "21 → 19" para os mapas de evento e cerco, iguais às do 20 | Eventos no Castle Siege também para quem vem do Non-PvP |
| `2 - Ligar Servidor\Configuration\bor_StartUp.xml` | + `GameServerNonPvP\Game Server S14.exe` (o arquivo é somente leitura; a marca foi mantida) | O launcher liga os dois GameServers |
| `MuChilaAdmin\` + `Mu Chila Admin.lnk` (novos) | Painel de administração compilado de `tools\MuChilaAdmin` (24/09/2026) | Pedido: disparar eventos, dar VIP, banir e controlar o servidor por um programa |

## Banco de dados (`.\MUONLINE`)

- `MuOnlineS14` e `BattleCore` restaurados dos `.bak` do kit.
- Scripts da pasta `Querys` aplicados (lista em INSTALACAO §4). `BanMac` foi criado no `MuOnlineS14`, sem o `USE [MuOnlineIP]`.
- `MuCastle_DATA`: datas do cerco atualizadas.
- Gremory Case e Restore Item: tabelas e procedimentos criados, também no `BattleCore`.
- Gatilho `TR_MuChila_ClasseInicial` (DW, DK, Elfa e Summoner nascem na 2ª classe) e +1 de classe nos personagens que estavam na básica (24/09/2026).
- `DefaultClassType.Inventory` (inventário inicial de cada classe): tirados os Wizard's Ring +1 e +2 (Warrior's Ring e Champion's Ring), que todo personagem novo ganhava presos ao personagem e sem efeito (issue #11, 24/09/2026). Personagens já criados continuam com eles. Backup em `C:\MuServer\DB\backup-caixas-DefaultClassType-inventario-*.csv`.
- Caixas sem suporte removidas de inventários, inventários de evento e baús (backup em `C:\MuServer\DB\backup-caixas-*.csv`).
- Site (25/09/2026, ver [SITE.md](SITE.md)): login SQL `muchila_site` (leitura, escrita e criação de tabelas no `MuOnlineS14`), 18 tabelas `WEBENGINE_*` do instalador do WebEngine e a tabela `MUCHILA_PEDIDOS` da loja (com `zen`/`zen_entregue_em` para o Zen do VIP) e a tarefa "Mu Chila - Zen do VIP" na `WEBENGINE_CRON`. A entrega do Zen cria a linha do baú (`warehouse`) de quem nunca abriu o baú.
- Contas criadas pelos jogadores (ex.: `dodo`) ficam só no banco. **Não há backup do banco neste repositório**, porque ele teria as senhas dos jogadores.

## ODBC

- DSNs de usuário de **32 bits** `MuOnlineS14` e `BattleCore`, com `Server=(local)\MUONLINE` e Trusted Connection. Os `.reg` do kit não foram usados.

## Cliente (`2 - Cliente Season 14 Full`)

| Arquivo | Alteração |
|---|---|
| `Config - Dev.ini` | `IpAddress = 26.139.39.123`; `WindowName = Mu Chila` |
| `Data\Local\ServerList.bmd`, `Data\Local\{Eng,Por,Spn}\serverlist_*.bmd` | Grupo 1: "Helheim" → "Mu Chila" |
| `Data\Local\{Eng,Por,Spn}\item_*.bmd` | Frost Soul (7617) e Soul Anvil (7618): permissões `0111000` → `0111110` (iguais às do Jewel of Bless), com a soma de verificação recalculada (`tools\Liberar-ItemCliente.ps1`). Issue #10: agora podem ser vendidos. Vai no pacote de patch. |

Formato dos `.bmd`: registros de 41 bytes (`WORD índice` + `nome[32]` + 7 bytes). Cada registro tem XOR próprio com a chave `FC CF AB`.

## Site (`C:\MuServer\Site`, novo em 25/09/2026)

WebEngine CMS 1.2.7 + código do Mu Chila (loja de VIP e cash em modo de teste, agenda real de eventos), Apache 2.4.68 e PHP 8.5.11. Detalhes, segurança e como recriar: [SITE.md](SITE.md).

26/09/2026:

- **Página Informações:** refeita com os valores reais do servidor (planos, Chaos Machine, reset, comandos).
- **Downloads:** cliente, patch e LEIA-ME, servidos pelo Apache em `/arquivos/`.
- **Desligados:** reset pelo site, comprar zen, votar e "esqueci a senha".

## Máquina do dono (fora do repositório)

- Registro `HKCU\Software\Webzen\Mu\Config`: `DisplayDeviceModeIndex = 9`, `FullScreenMode = 0` (1920x1080 em janela).
- Atalho "Mu Chila - Vigia" na pasta Inicializar do usuário (`MuChilaAdmin.exe --vigia-reset`, 25/09/2026): corrige a checagem de ataques do GameServer/Castle Siege (issue #12) sempre que eles ligam. Sem ele rodando, as skills evoluídas voltam a travar depois de um reinício.
- Site: regra do firewall "Mu Chila - Site (porta 80, so Radmin)" (entrada TCP 80 só de 26.0.0.0/8, só para o `httpd.exe` do site) e tarefa agendada "Mu Chila\Site - rankings" (a cada minuto). Módulo Python `capstone` (desmontador, usado nas análises do GameServer): `python -m pip uninstall capstone` para remover.
- Ferramenta de diagnóstico Frida instalada no Python do usuário. Para remover: `python -m pip uninstall frida`.

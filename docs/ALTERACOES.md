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
| `Data\Item\ItemDrop.txt` | Box of Kundun em Kalima (1%): K1–2 +1, K3 +2, K4 +3, K5 +4, K6–7 +5 | O kit não tinha drop de Box of Kundun |
| `Data\Lang\Portuguese.xml` (novo) + `Data\LangManager.xml` | 548 mensagens do servidor em português (arquivo em Windows-1252, que é o que o cliente lê); os idiomas 0 e 2 apontam para ele | Pedido: jogo em português (os personagens usam o idioma 0) |
| `Data\Lang\Portuguese.xml` e `Data\Lang\English.xml` | 17 avisos de invasão (IDs 192–200, 202–207, 210, 211) copiados de `<Message>` para `<InvacionMsg>` | O servidor procura ali os avisos do `InvasionManager.dat`; o jogo mostrava "Could not find message 194/197/200" |
| Cliente: `Idioma - Portugues.reg` / `Idioma - Ingles.reg` (novos) | Trocam o `LangSelection` do cliente | O cliente já tem textos `Por` oficiais |
| `Data\Monster\MonsterSetBase\*.xml` (43 mapas) | Spawns de SPOT/MONSTER viram 1.676 grupos de 5+ em chão livre (`tools\Agrupar-Spawns.ps1`) | Pedido: monstros agrupados em vez de espalhados |
| `Startup - Iniciar Server.lnk` | Aponta para `C:\MuServer\2 - Ligar Servidor\Ligar Servidor.exe` | Apontava para uma pasta de Downloads de outro PC |
| `Criar Conta.bat` / `Criar Conta.ps1` | Novos | Criar conta de jogo pelo ODBC |
| `Cliente Season 14 - main.lnk` | Novo | Atalho para o cliente |
| `DB\1 - Querys\Correcoes (Gremory e RestoreItem)\` | Novos: `GremoryCase_MuDevs.sql`, `RestoreItem_MuDevs.sql` | Objetos que faltavam no backup |
| `1 - MuEditor\config.ini` | `SERVER` `(local)` → `.\MUONLINE`, `PORT` `1433` → `61764` (nas duas seções) | O kit apontava para a instância padrão do SQL; o banco está na instância nomeada `MUONLINE` |
| `MuChilaAdmin\` + `Mu Chila Admin.lnk` (novos) | Painel de administração compilado de `tools\MuChilaAdmin` (24/09/2026) | Pedido: disparar eventos, dar VIP, banir e controlar o servidor por um programa |

## Banco de dados (`.\MUONLINE`)

- `MuOnlineS14` e `BattleCore` restaurados dos `.bak` do kit.
- Scripts da pasta `Querys` aplicados (lista em INSTALACAO §4). `BanMac` foi criado no `MuOnlineS14`, sem o `USE [MuOnlineIP]`.
- `MuCastle_DATA`: datas do cerco atualizadas.
- Gremory Case e Restore Item: tabelas e procedimentos criados, também no `BattleCore`.
- Gatilho `TR_MuChila_ClasseInicial` (DW, DK, Elfa e Summoner nascem na 2ª classe) e +1 de classe nos personagens que estavam na básica (24/09/2026).
- Caixas sem suporte removidas de inventários, inventários de evento e baús (backup em `C:\MuServer\DB\backup-caixas-*.csv`).
- Contas criadas pelos jogadores (ex.: `dodo`) ficam só no banco. **Não há backup do banco neste repositório**, porque ele teria as senhas dos jogadores.

## ODBC

- DSNs de usuário de **32 bits** `MuOnlineS14` e `BattleCore`, com `Server=(local)\MUONLINE` e Trusted Connection. Os `.reg` do kit não foram usados.

## Cliente (`2 - Cliente Season 14 Full`)

| Arquivo | Alteração |
|---|---|
| `Config - Dev.ini` | `IpAddress = 26.139.39.123`; `WindowName = Mu Chila` |
| `Data\Local\ServerList.bmd`, `Data\Local\{Eng,Por,Spn}\serverlist_*.bmd` | Grupo 1: "Helheim" → "Mu Chila" |

Formato dos `.bmd`: registros de 41 bytes (`WORD índice` + `nome[32]` + 7 bytes). Cada registro tem XOR próprio com a chave `FC CF AB`.

## Máquina do dono (fora do repositório)

- Registro `HKCU\Software\Webzen\Mu\Config`: `DisplayDeviceModeIndex = 9`, `FullScreenMode = 0` (1920x1080 em janela).
- Ferramenta de diagnóstico Frida instalada no Python do usuário. Para remover: `python -m pip uninstall frida`.

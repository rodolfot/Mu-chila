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
- **Causa (conferida na memória do GameServer):** o personagem tinha as habilidades master 301 "Add Defense Success Rate PvP", 325 "Add Attack Success Rate" e 347 "Add Attack Success Rate PvP" com **nível 0**. O mínimo é 1 (`MasterSkillTree.txt`). Com nível 0, o bônus de cada uma sai como -2³¹ e é somado justamente a esses três atributos. A base calculada estava certa (3.923).
  - Não é a 4ª classe abaixo do 400 nem o Master Level: a mamaeupo está assim e acerta normalmente, com as mesmas habilidades no nível 9.
  - Visto no Quest1 depois de um `/reset` feito com 3 pontos de master gastos. Como as habilidades ficaram no nível 0 ainda não foi reproduzido.
- **Solução:** com a conta offline, apagar as habilidades master do personagem (`MasterSkillTree.MasterSkill`: todas as entradas de 3 bytes voltam a `FF 00 FF`). Feito no Quest1 em 24/09/2026, com backup em `C:\MuServer\DB\backup-caixas-Quest1-master-antes-de-zerar.csv`. Depois disso: acerto 3.923, acerto PvP 8.484, defesa PvP 1.006.
- **Atenção:** ao mexer em `MasterLevel`/`MasterPoint` no banco, apague também as habilidades master. Senão elas ficam aprendidas sem pontos que as sustentem.

### GameServer trava ao carregar e o cliente não mostra o servidor (24/09/2026)
- **Sintoma:** depois de um Reload EventItemBag, o GameServer parou de responder. Ao religar, o log parou em `Event loaded successfully` e o ConnectServer não recebeu o `GameServer online`. No cliente, o botão do servidor não aparecia.
- **Causa:** dois arquivos novos em `Data\EventItemBag` (caixas Season Level Reward A/B) foram gravados com o texto literal `` `t `` no lugar de TAB, por erro no script que os criou. O carregador do MuDevs não trata a linha malformada e fica preso nela.
- **Solução:** `EventItemBagManager.txt` voltou ao backup, os dois arquivos foram renomeados para `.desativado-*` e o GameServer e o Castle Siege foram religados. As caixas Season Level Reward continuam sem conteúdo, como no kit.
- **Regra:** depois de mexer em qualquer arquivo de `Data`, confira no LOG se aparece o `loaded successfully` daquele item antes de dar o assunto por encerrado.

### Mensagens do servidor com acento aparecem como "invasÃ£o", "comeÃ§ou"
- **Causa:** o `Portuguese.xml` estava em UTF-8. O servidor repassa os bytes do texto sem converter, e o cliente lê em Windows-1252, então cada letra acentuada (2 bytes em UTF-8) vira dois caracteres.
- **Solução:** o arquivo foi salvo em **Windows-1252**, mantendo o cabeçalho `encoding="utf-8"`. Conferido na memória do GameServer: as 505 mensagens carregam, com os acentos em 1 byte (`ã` = E3).
- Numa sessão anterior a documentação atribuía o "Could not find message" ao Windows-1252. Estava errado: aquele erro era a seção `<InvacionMsg>` (item acima).

### Caixas (Chicken Box, Earring Box...) não sobem do chão nem descem para o chão
- **Causa:** as caixas de evento ficam no **Inventário de Evento**. No `GameServerInfo - Common.dat`, `EventInventoryExpireYear/Month/Day` vinha como **31/12/2015**; com a data vencida, o servidor trata esse inventário como fechado e recusa, sem mensagem, pegar ou mover esses itens.
- **Solução:** validade em **31/12/2037** nos três GameServers, aplicada com Reload Common. Talvez seja preciso sair e entrar no personagem para o cliente mostrar o inventário de evento. O ano 2037 é o limite seguro para executáveis 32 bits antigos, por causa do problema de 2038.

## Pendências

### Confirmar as caixas no jogo
- Testar se Chicken Box e Earring Box sobem do chão depois da correção do Inventário de Evento.
- Testar se, ao abrir, dão o item inicial +6.

### Personagem novo aparece como "Blade Knight" no cliente, mas é Dark Knight no servidor
- **Causa:** o cliente S14 mostra a classe básica e a 2ª classe com o mesmo nome (16 e 17 aparecem ambos como "Blade Knight"). O servidor, porém, cria o personagem na básica (+0). Com isso, o cliente oferecia itens e skills de 2ª classe que o servidor recusava ("Não pode vestir o item").
- **Solução:**
  - Gatilho `TR_MuChila_ClasseInicial` na tabela `Character`: DW, DK, Elfa e Summoner já nascem na 2ª classe, com +1, igual ao `/change`. O script é `DB\1 - Querys\Correcoes (Gremory e RestoreItem)\ClasseInicial_2aClasse.sql`.
  - Os personagens que ainda estavam na básica (MalocoBR, Yololo, asas, Quest4) subiram +1.
  - MG, DL e RF não precisam. Grow Lancer e Rune Wizard não foram alterados, por falta de confirmação; se aparecer o mesmo sintoma, basta usar `/change`.

### Item comprado não pode ser usado / habilidades não funcionam em algumas contas (provável causa única)
- **Caso da mamaeupo:**
  - a "Beuroba +2" é a **Brova** do servidor (item 3,11; o cliente usa outro nome);
  - ela exige a **2ª classe de Dark Knight** (Blade Knight): o marcador `2` na coluna DK do `Item.txt`;
  - a mamaeupo continua **Dark Knight básico** (classe 16) no nível 249.
- **Padrão geral:** todos os personagens novos estão na classe básica (dodo 16, Maloco 48, MalocoBR 0). Os das contas do kit, onde "funciona", já estão evoluídos.
- **Solução:** digitar **`/change`** no chat para evoluir (grátis, até a 3ª classe). Veja OPERACAO.md.
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

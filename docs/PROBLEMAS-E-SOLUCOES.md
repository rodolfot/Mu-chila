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
- **Causa:** 19 caixas (Earring of Wrath Box, [Speed] Earring Box, Ruud Box, Gift Box, Mastery Box...) estão marcadas como drop de monstro no `Item.txt`, mas não tinham conteúdo no `EventItemBagManager.txt`.
- **Solução:** entradas 236 a 254 com sacos de item inicial de classe +6. Aguardando alguém abrir uma caixa no jogo para confirmar.

## Pendências

### Caixa no chão que não dá para pegar
- **Sintomas:** a "Earring of Wrath Box (Left)" dropada por um monstro morto pelo próprio jogador não é pega. Não aparece mensagem, e outros itens são pegos normalmente.
- **O que foi verificado:**
  - o GameServer não registra falha ao pegar item;
  - o item é normal (1x1, sem restrição no `ItemMove`, não empilhável);
  - o inventário tem espaço;
  - o jogador já tinha uma caixa igual no inventário.
- **Próximo teste:** abrir a caixa do inventário (agora elas têm conteúdo) e tentar pegar outra caixa igual.

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

# Operação do dia a dia

## Ligar e desligar

- **Ligar:** Radmin VPN conectado → `C:\MuServer\Startup - Iniciar Server.lnk` → iniciar. Espere todas as linhas ficarem verdes (amarelo = inicializando).
- **Desligar com jogadores online:** menu `File` do GameServer → "1/3/5 Minute(s) Server Close". Os jogadores recebem aviso.
- O launcher **não** religa servidores que caírem nem inicia sozinho quando é aberto.
- **Site** (cadastro, rankings, loja): `C:\MuServer\Site\Ligar Site.bat` / `Desligar Site.bat`; endereço `http://26.139.39.123` pelo Radmin. Tudo sobre ele em [SITE.md](SITE.md).

## Painel Mu Chila Admin

Atalho: `C:\MuServer\Mu Chila Admin.lnk` (programa em `C:\MuServer\MuChilaAdmin`, código em `tools\MuChilaAdmin`).
Precisa do .NET 10 Desktop Runtime e deve rodar na máquina do servidor, porque conversa com as janelas dos servidores e com o SQL local.

| Aba | O que faz |
|---|---|
| **Servidor** | Mostra os 6 processos, os jogadores online (conta, personagem, IP) e o título do GameServer (jogadores e monstros). Atualiza a cada 5 s. |
| | Botões: iniciar e parar tudo pelo launcher, desconectar todos os jogadores (os personagens são salvos), abrir o MuEditor e o launcher. |
| | "Recarregar sem reiniciar": manda o Reload escolhido para o GameServer **e** o Castle Siege ao mesmo tempo. |
| **Eventos** | Escolha o evento e em quantos minutos ele começa, e clique em "Disparar evento". Veja [Eventos](#eventos). |
| **VIP e contas** | Lista as contas com nível, validade, ban, status e personagens. Aplica VIP 1–3 por N dias, remove VIP, bane e desbane. |
| | "Zerar habilidades master": escolhe um personagem da conta, apaga a árvore master, devolve os pontos (1 por Master Level) e tira os poderes master da lista de habilidades (`MagicList`). Os melhorados voltam à habilidade normal (ex.: 330 Twisting Slash Improved → 41 Twisting Slash), seguindo a coluna `ReplaceSkill` do `MasterSkillTree.txt`. Serve também para quem mostra "Suces de Atq" negativo na janela (C); ver PROBLEMAS-E-SOLUCOES. |

- VIP e ban valem no **próximo login** da conta. Se a conta estiver online, o painel pergunta se quer **forçar o logout** para valer na hora.
- **Logout forçado** (perguntas acima e botão "Desconectar jogador selecionado", na aba Servidor): o MuDevs FREE não tem comando para desconectar um jogador só, então o painel derruba a conexão de rede do jogador com o GameServer (ou o Castle Siege). Para o servidor é como se o jogo tivesse fechado: ele salva o personagem e registra a saída.
  - Precisa de permissão de administrador: o Windows pergunta na hora.
  - A conexão é achada pelo IP gravado em `MEMB_STAT`. Contas online com o **mesmo IP** também caem, e o painel avisa antes.
  - "Zerar habilidades master" com a conta online derruba o jogador **antes** de mexer, porque o servidor grava o personagem na saída e desfaria a alteração.
  - Linha de comando: `MuChilaAdmin.exe --forcar-logout <conta> <arquivo-de-resultado>`.
- VIP = `MEMB_INFO.AccountLevel` (1–3) + `AccountExpireDate`. Os benefícios de cada nível ficam nas linhas `*_AL1`, `*_AL2` e `*_AL3` do `GameServerInfo - Common.dat`.
- Ban = `MEMB_INFO.bloc_code = 1`.
- Teste rápido sem abrir a janela: `C:\MuServer\MuChilaAdmin\MuChilaAdmin.exe --teste C:\temp\teste.txt`. O arquivo lista banco, servidores, GameServer e agendas; o código de saída é o número de falhas.
- **Vigia** (`MuChilaAdmin.exe --vigia-reset`, processo sem janela, uma instância só): liga com o Windows (atalho na pasta Inicializar) e quando o painel abre; o painel mostra "Vigia: ligado/DESLIGADO". Ele:
  - corrige a checagem de ataques do MuDevs no GameServer e no Castle Siege sempre que encontra um servidor novo (issue #12, ver PROBLEMAS-E-SOLUCOES). **Se o vigia não estiver rodando quando o GameServer religar, as skills evoluídas voltam a travar;**
  - depois do `/reset`, leva o jogador à seleção de personagem (issue #7) — **desligado** até ser testado; para ligar, criar o arquivo vazio `C:\MuServer\MuChilaAdmin\vigia-reset-selecao.ligado`.
  - Registro: `C:\MuServer\MuChilaAdmin\vigia.log`. Conferir sem mexer em nada: `MuChilaAdmin.exe --vigia-sondar C:\temp\vigia.txt`.
  - Botão "Levar à seleção de personagem" (aba Servidor): o mesmo que o jogador escolher "Trocar personagem" no jogo, sem desconectar.
- Para recompilar depois de mudar o código:

  ```powershell
  dotnet publish .\tools\MuChilaAdmin\MuChilaAdmin.csproj -c Release -r win-x64 --self-contained false -o C:\MuServer\MuChilaAdmin
  ```

  Feche o painel antes, porque o `.exe` fica travado enquanto ele está aberto.

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

Os arquivos da pasta `Data` são compartilhados pelo GameServer e pelo Castle Siege. Recarregue os dois (`-Servidor CastleSiege`).
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

## Evolução de classe (`/change`)

**DW, DK, Elfa e Summoner já nascem na 2ª classe** (gatilho `TR_MuChila_ClasseInicial` no banco), porque o cliente S14 mostra a básica e a 2ª com o mesmo nome. As próximas evoluções continuam sendo manuais.
Itens e skills marcados com `2` nas colunas de classe do `Item.txt` exigem a 2ª classe (ex.: a foice Brova/"Beuroba" exige Blade Knight).
Para evoluir, digite **`/change`** no chat. É grátis e vai até a 3ª classe (`CommandChangeLimit = 3` em `GameServerInfo - Command.dat`).
Cada uso sobe uma classe e responde "You successfully evolved".

## Spawn de monstros em grupos

Os spawns dos mapas de caça foram reorganizados em **grupos de 5 ou mais** do mesmo monstro, num quadrado de 5×5 tiles:
- **Números:** 1.676 grupos, com o total de monstros praticamente igual (8.895 → 8.899).
- **Posições:** os grupos ficam nas mesmas regiões onde cada monstro já nascia, sempre em chão livre (validado pelo arquivo `Data\Terrain\TerrainN.att`).
- **Ficaram como estavam:**
  - chefes e raros, com 1 ou 2 no mapa ou respawn de 10 minutos ou mais (Kundun, Erohim, Balrog...);
  - armadilhas;
  - Crywolf e o Refúgio de Balgass.
- **Aplicar mudanças no ar:** Reload Monster (recria todos os monstros e limpa invasões em andamento).
- **Refazer a partir dos arquivos originais do kit:** `.\tools\Agrupar-Spawns.ps1` (sem `-Apply` só simula; com `-Apply` grava, com backup).

## Drops de monstros

`Data\Item\ItemDrop.txt` tem uma linha por regra: `Index Level Grade Option0..6 Duration MapNumber MonsterClass MonsterLevelMin MonsterLevelMax DropRate`.
O `DropRate` é **por milhão** (1000 = 0,1% por monstro morto). Depois de editar, use Reload Item.

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

## Versionar alterações

```powershell
.\tools\Sincronizar-Servidor.ps1        # copia C:\MuServer para a pasta 3 do repositório
git add -A; git commit -m "descrição"; git push
```

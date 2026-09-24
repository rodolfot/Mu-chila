# Operação do dia a dia

## Ligar e desligar

- **Ligar:** Radmin VPN conectado → `C:\MuServer\Startup - Iniciar Server.lnk` → iniciar. Espere todas as linhas ficarem verdes (amarelo = inicializando).
- **Desligar com jogadores online:** menu `File` do GameServer → "1/3/5 Minute(s) Server Close". Os jogadores recebem aviso.
- O launcher **não** religa servidores que caírem nem inicia sozinho quando é aberto.

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

- **Invasões** (`InvasionManager.dat`): 0 Underworld, 1 Red Dragon, 2 Golden, 3 White Wizard, 7 Christmas, 8 Medusa.
  - **Golden:** a cada **10 minutos** (00, 10, 20, 30, 40 e 50), com duração de **540 s**, para não sobrepor.
  - **Red Dragon:** 0:15, 4:15, 8:15, 12:15, 16:15 e 18:32.
- **Disparar uma vez:** adicione uma linha com data exata (ex.: `1  2026  9  23  *  21  5  0`) e Reload Event.
- **Conferir se nasceu:** o título da janela do GameServer mostra `MonsterCount`, que sobe quando a invasão aparece.

## Caixas

As caixas abertas com o botão direito estão em `Data\EventItemBagManager.txt` (código do item → saco) e `Data\EventItemBag\NNN - Nome.txt` (itens possíveis).
As 19 caixas que caíam de monstros sem conteúdo (entradas 236 a 254) agora dão **1 item inicial de classe +6**, sorteado entre 40 armas e peças de armadura.
Depois de editar, use Reload EventItemBag.

## Idioma (português)

- **Servidor:** as mensagens do servidor estão em `Data\Lang\Portuguese.xml`, ativado no `Data\LangManager.xml` (English `Enable="0"`, Portuguese `Enable="1"`). Aplique com Reload Common.
  - O arquivo é gravado em **Windows-1252**, embora o cabeçalho diga utf-8; é assim que os acentos chegam certos ao cliente. Edite mantendo essa codificação.
  - Os comandos (`/move`, `/post`...) continuam com o nome original.
- **Cliente:** registro `HKCU\Software\Webzen\Mu\Config` → `LangSelection = Por`. Os arquivos `Idioma - Portugues.reg` e `Idioma - Ingles.reg` na pasta do cliente fazem a troca com dois cliques.
  - O cliente passa a carregar `Data\Local\Por\*.bmd`: missões, dicas de skill, ajuda e opções de conjunto já vêm traduzidas.
  - Os nomes e dicas de itens continuam em inglês, porque `item_por` e `itemtooltip_por` são cópias do inglês.
  - O texto da interface fica no `Data\Lang.mpr`, que é criptografado.

## Evolução de classe (`/change`)

Personagens novos começam na classe básica e **não evoluem sozinhos**.
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

`Data\Item\ItemMove.txt` define `AllowDrop/AllowSell/AllowTrade/AllowVault` por item.
O kit proíbe largar no chão **asas, joias, pedras de refino e talismãs** (`AllowDrop = 0`). Para liberar, troque para `1` e use Reload Item.

## Resolução do cliente

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

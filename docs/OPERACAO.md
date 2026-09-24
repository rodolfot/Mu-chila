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

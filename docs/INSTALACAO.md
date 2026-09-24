# Instalação do zero

Passo a passo para montar o servidor Mu Chila numa máquina Windows nova. Siga na ordem.
Cada passo explica o que o kit original faz de errado e o que usar no lugar.

## 1. Requisitos

- Windows 10/11.
- **SQL Server** com Windows Authentication. Este servidor usa o SQL Server 2025 Express, instância `MUONLINE`.
  O guia do kit recomenda o 2008 R2, mas os backups restauram sem problema em versões novas.
- Drivers ODBC "SQL Server" (já vêm no Windows).
- Radmin VPN, se amigos forem jogar remotamente.

## 2. Pastas

1. Copie `3 - MuServer Mu Chila (servidor configurado)` para **`C:\MuServer`**. O launcher e os atalhos usam esse caminho fixo.
2. Confira que `C:\MuServer\Startup - Iniciar Server.lnk` aponta para
   `C:\MuServer\2 - Ligar Servidor\Ligar Servidor.exe`. O atalho original do kit apontava para uma pasta de Downloads de outro PC.

## 3. Bancos de dados

Restaure os dois backups do kit (SQL 2008 R2) na instância. Os nomes lógicos dos arquivos são `MuOnline_Data` e `MuOnline_Log`:

```sql
RESTORE DATABASE [MuOnlineS14] FROM DISK = N'C:\MuServer\DB\MuOnlineS14.bak'
  WITH MOVE N'MuOnline_Data' TO N'<pasta DATA>\MuOnlineS14.mdf',
       MOVE N'MuOnline_Log'  TO N'<pasta DATA>\MuOnlineS14_1.ldf', RECOVERY;
RESTORE DATABASE [BattleCore] FROM DISK = N'C:\MuServer\DB\BattleCore.bak'
  WITH MOVE N'MuOnline_Data' TO N'<pasta DATA>\BattleCore.mdf',
       MOVE N'MuOnline_Log'  TO N'<pasta DATA>\BattleCore_1.ldf', RECOVERY;
```

## 4. Scripts (pasta `DB\1 - Querys`)

Rode com `sqlcmd -S .\MUONLINE -E -d <banco> -I -f 1252 -i <arquivo>`:

| Script | Banco |
|---|---|
| `EffectList`, `GiftNewbies`, `RuudMoney`, `TotalPKCount` | `MuOnlineS14` **e** `BattleCore` (os dois têm a tabela `Character`) |
| `Golden Archer`, `Hunting\*`, `BattleCore\*` | `MuOnlineS14` |
| `BanMac` | `MuOnlineS14`, **removendo a linha `USE [MuOnlineIP]`**, porque esse banco não existe no kit |
| `Correcoes (Gremory e RestoreItem)\*.sql` | `MuOnlineS14` e `BattleCore`. São correções: o backup veio sem esses objetos |

Os procedimentos `BattleCore*` usam nomes completos (`[BattleCore].[dbo]...`) e são chamados pelo DataServer principal. Por isso vão no `MuOnlineS14`.

Depois, **atualize as datas do Castle Siege**. O backup vem com o cerco marcado para 2020, e o `Castle Siege Server.exe` fecha com `CheckSync ... iEVENT_END_DATE_NUM <= iTODAY_DATE_NUM`:

```sql
UPDATE MuOnlineS14.dbo.MuCastle_DATA
SET SIEGE_START_DATE = CAST(GETDATE() AS date),
    SIEGE_END_DATE   = DATEADD(day, 7, CAST(GETDATE() AS date));
```

## 5. ODBC (32 bits)

Os executáveis do servidor são **32 bits**, então os DSNs precisam existir na plataforma 32 bits.
**Não use os `.reg` da pasta `0 - ODBC REGISTRO`**, porque têm três erros:
- criam o DSN com o nome `MuOnline`, mas o servidor procura `MuOnlineS14`;
- um deles grava no registro de 64 bits;
- usam `Server=(local)`, que não funciona com instância nomeada.

Crie os DSNs assim:

```powershell
foreach ($n in 'MuOnlineS14','BattleCore') {
  Add-OdbcDsn -Name $n -DriverName 'SQL Server' -DsnType User -Platform '32-bit' `
    -SetPropertyValue @('Server=(local)\MUONLINE', "Database=$n", 'Trusted_Connection=Yes')
}
```

Com PowerShell de administrador, prefira `-DsnType System`. O DSN de usuário só funciona para o usuário do Windows que roda os servidores.

## 6. IPs

Há dois tipos de endereço:

- **Entre os servidores** (JoinServer, DataServer, GameServer): continuam em `127.0.0.1`
  (`GameServerInfo - Common.dat`, `JoinServer.ini`, `AllowableIpList.txt`).
- **O que o cliente recebe:** é o IP que os jogadores alcançam. Hoje é o do Radmin, `26.139.39.123`, em três lugares:

| Arquivo | Formato |
|---|---|
| `ConnectServer\ServerList.dat` | entre aspas: `"26.139.39.123"` |
| `Data\MapServerInfo.dat` | **com uma letra na frente**: `S26.139.39.123` |
| Cliente: `Config - Dev.ini` → `IpAddress` | `26.139.39.123` |

Duas regras não óbvias:
- **Nunca use `127.0.0.1` para o cliente.** O `main.exe` deste kit se recusa a conectar nesse IP exato, sem tentar. Para jogar só na própria máquina, use `127.0.0.2`.
- **O "S" do `MapServerInfo.dat` é obrigatório.** O GameServer descarta o primeiro caractere desse campo. Sem o "S", quem troca para um mapa de outro servidor (Castle Siege, eventos) é mandado para `27.0.0.2`, e o cliente trava.

## 7. Primeira execução

1. Abra o launcher e inicie. A ordem já está em `2 - Ligar Servidor\Configuration\bor_StartUp.xml`:
   ConnectServer → DataServer → DataServer BattleCore → JoinServer → Castle Siege → GameServer.
   O `BattleCore Server` vem desligado de propósito; ele é opcional.
2. Confira os logs em `<servidor>\LOG\<data>.txt`. Não pode haver `FAIL`, e o DataServer não pode registrar `[QueryManager] State (42000)`.
3. Portas usadas: TCP 44405 (ConnectServer), 55901 (GameServer), 55919 (Castle Siege), 55960/55961 (DataServers), 55970 (JoinServer), UDP 55557.

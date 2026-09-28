/* ============================================================================
   Mu Chila - Passe dos Mapas Exclusivos (Paid Channel / Gold Channel Ticket)
   ----------------------------------------------------------------------------
   Decisao do dono (28/09/2026): os mapas acima do nivel 400 (Nixies Lake, Deep
   Dungeon 1-5, Swamp of Darkness, Kubera Mine 1-5) so com o ticket; validade de
   acordo com o ticket (1, 3, 7 ou 30 dias); so por WCoin ou dinheiro no site.

   Como o passe chega na conta:
     - Loja de cash do jogo (WCoin): ao USAR o "Gold Channel Ticket" (item 13,124)
       guardado na loja, o GameServer manda o JoinServer rodar
       EXEC WZ_SetAccountLevel '<conta>', <nivel do produto>, <duracao em segundos>
       (unico uso desse pacote no GameServer; conferido no binario em 28/09/2026).
       No kit isso TROCAVA O VIP da conta: os tickets tem nivel 0, entao um VIP que
       usasse o ticket virava Free. Aqui a procedure passa a somar a duracao ao
       passe; nivel 1..3 continua sendo VIP (fica livre para vender VIP por WCoin).
     - Loja do site (PIX): dbo.MuChila_PasseAdicionar.
   Quem cobra: o vigia (MuChilaAdmin.exe --vigia-reset, arquivo
   vigia-passe-mapas.ligado) manda para a selecao de personagem quem estiver num
   mapa da lista sem passe ativo e, fora do jogo, poe o personagem em Lorencia.

   Idempotente. Para voltar a WZ_SetAccountLevel original: bloco ORIGINAL no fim.
   ============================================================================ */
USE MuOnlineS14;
GO
SET NOCOUNT ON;
GO

/* 1) Passe por CONTA (vale para todos os personagens e servidores) */
IF OBJECT_ID('dbo.MuChila_PasseMapas') IS NULL
    CREATE TABLE dbo.MuChila_PasseMapas (
        Conta      varchar(10) NOT NULL PRIMARY KEY,
        Expira     datetime    NOT NULL,
        Atualizado datetime    NOT NULL CONSTRAINT DF_MuChila_PasseMapas_Atualizado DEFAULT GETDATE());
GO
IF OBJECT_ID('dbo.MuChila_PasseMapasLog') IS NULL
    CREATE TABLE dbo.MuChila_PasseMapasLog (
        Id           int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Conta        varchar(10) NOT NULL,
        Segundos     int         NOT NULL,
        Origem       varchar(60) NOT NULL,
        ExpiraAntes  datetime    NULL,
        ExpiraDepois datetime    NOT NULL,
        Criado       datetime    NOT NULL CONSTRAINT DF_MuChila_PasseMapasLog_Criado DEFAULT GETDATE());
GO

/* 2) Mapas que exigem o passe (nivel minimo acima de 400 no Move.txt/Gate.txt). O vigia rele a cada minuto:
      para incluir/tirar um mapa basta mudar esta tabela. Ferea (112) e Swamp of Calmness (56) pedem exatamente 400: fora. */
IF OBJECT_ID('dbo.MuChila_PasseMapasLista') IS NULL
    CREATE TABLE dbo.MuChila_PasseMapasLista (Mapa int NOT NULL PRIMARY KEY, Nome varchar(40) NOT NULL);
GO
INSERT dbo.MuChila_PasseMapasLista (Mapa, Nome)
SELECT v.Mapa, v.Nome FROM (VALUES
    (113, 'Nixies Lake'),
    (116, 'Deep Dungeon 1'), (117, 'Deep Dungeon 2'), (118, 'Deep Dungeon 3'), (119, 'Deep Dungeon 4'), (120, 'Deep Dungeon 5'),
    (122, 'Swamp of Darkness'),
    (123, 'Kubera Mine 1'), (124, 'Kubera Mine 2'), (125, 'Kubera Mine 3'), (126, 'Kubera Mine 4'), (127, 'Kubera Mine 5')
) AS v (Mapa, Nome)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MuChila_PasseMapasLista l WHERE l.Mapa = v.Mapa);
GO

/* 3) Soma tempo ao passe: passe ainda ativo -> soma ao vencimento; vencido ou novo -> comeca agora.
      Retorno: 0 ok, 1 conta nao existe ou tempo invalido. */
CREATE OR ALTER PROCEDURE dbo.MuChila_PasseAdicionar
    @Conta varchar(10), @Segundos int, @Origem varchar(60)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @Segundos IS NULL OR @Segundos <= 0 OR NOT EXISTS (SELECT 1 FROM dbo.MEMB_INFO WHERE memb___id = @Conta) RETURN 1;
    BEGIN TRAN;
    DECLARE @antes datetime = (SELECT Expira FROM dbo.MuChila_PasseMapas WITH (UPDLOCK, HOLDLOCK) WHERE Conta = @Conta);
    DECLARE @depois datetime = DATEADD(second, @Segundos, CASE WHEN @antes > GETDATE() THEN @antes ELSE GETDATE() END);
    IF @antes IS NULL
        INSERT dbo.MuChila_PasseMapas (Conta, Expira, Atualizado) VALUES (@Conta, @depois, GETDATE());
    ELSE
        UPDATE dbo.MuChila_PasseMapas SET Expira = @depois, Atualizado = GETDATE() WHERE Conta = @Conta;
    INSERT dbo.MuChila_PasseMapasLog (Conta, Segundos, Origem, ExpiraAntes, ExpiraDepois) VALUES (@Conta, @Segundos, ISNULL(@Origem, '?'), @antes, @depois);
    COMMIT;
    RETURN 0;
END
GO

/* 4) WZ_SetAccountLevel (chamada pelo JoinServer quando o jogador usa o ticket da loja de cash).
      Sem SELECT: o JoinServer so executa e fecha o cursor; depois o jogo rele o VIP com WZ_GetAccountLevel. */
ALTER PROCEDURE [dbo].[WZ_SetAccountLevel]
    @Account varchar(10),
    @AccountLevel int,
    @AccountExpireTime int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- Mu Chila: nivel fora de 1..3 (os tickets da loja tem nivel 0) = Passe dos Mapas; o VIP da conta nao muda
    IF @AccountLevel IS NULL OR @AccountLevel NOT BETWEEN 1 AND 3
    BEGIN
        EXEC dbo.MuChila_PasseAdicionar @Conta = @Account, @Segundos = @AccountExpireTime, @Origem = 'loja de cash (ticket)';
        RETURN;
    END
    -- VIP: mesmo nivel ainda ativo soma ao vencimento; senao comeca agora (o original somava a um vencimento ja passado)
    UPDATE dbo.MEMB_INFO
       SET AccountExpireDate = DATEADD(second, @AccountExpireTime,
               CASE WHEN AccountLevel = @AccountLevel AND AccountExpireDate > GETDATE() THEN AccountExpireDate ELSE GETDATE() END),
           AccountLevel = @AccountLevel
     WHERE memb___id = @Account;
END
GO

/* 5) Site (login muchila_site): consulta o passe e entrega o comprado por PIX */
IF USER_ID('muchila_site') IS NOT NULL
BEGIN
    GRANT EXECUTE ON dbo.MuChila_PasseAdicionar TO muchila_site;
    GRANT SELECT ON dbo.MuChila_PasseMapas TO muchila_site;
    GRANT SELECT ON dbo.MuChila_PasseMapasLista TO muchila_site;
    GRANT SELECT ON dbo.MuChila_PasseMapasLog TO muchila_site;
END
GO

/* ORIGINAL do kit (para voltar: rode este ALTER; as tabelas do passe podem ficar)
ALTER Procedure [dbo].[WZ_SetAccountLevel]
@Account varchar(10),
@AccountLevel int,
@AccountExpireTime int
AS
BEGIN
SET NOCOUNT ON
SET XACT_ABORT ON
DECLARE @CurrentAccountLevel int
DECLARE @CurrentAccountExpireDate smalldatetime
SELECT @CurrentAccountLevel=AccountLevel,@CurrentAccountExpireDate=AccountExpireDate FROM MEMB_INFO WHERE memb___id=@Account
IF(@CurrentAccountLevel = @AccountLevel)
BEGIN
	SET @CurrentAccountLevel = @CurrentAccountLevel
	SET @CurrentAccountExpireDate = DATEADD(second,@AccountExpireTime,@CurrentAccountExpireDate)
END
ELSE
BEGIN
	SET @CurrentAccountLevel = @AccountLevel
	SET @CurrentAccountExpireDate = DATEADD(second,@AccountExpireTime,getdate())
END
UPDATE MEMB_INFO SET AccountLevel=@CurrentAccountLevel,AccountExpireDate=@CurrentAccountExpireDate WHERE memb___id=@Account
SET NOCOUNT OFF
SET XACT_ABORT OFF
END
*/

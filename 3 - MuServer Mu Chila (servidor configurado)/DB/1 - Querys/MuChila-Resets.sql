/* ============================================================================
   Mu Chila - Sistema avancado de resets (parte do BANCO)
   ----------------------------------------------------------------------------
   Contexto: o GameServer e um binario fechado (MuDevs). Reset e Master Reset sao
   feitos DENTRO do jogo (comandos do kit). Este script cuida do que o jogo NAO faz:
     - o contador e o procedimento do SUPREME RESET (feito pelo site, offline);
     - o armazenamento e a entrega de CREDITOS do site (recompensa dos resets);
     - a reconciliacao que credita por Master Reset feito no jogo.

   >>> RODE PRIMEIRO NUMA COPIA DE TESTE DO BANCO. <<<
   Idempotente: pode rodar de novo sem duplicar. Alvo: banco MuOnlineS14.
   ============================================================================ */
USE MuOnlineS14;
GO
SET NOCOUNT ON;
GO

/* 1) Contador de Supreme Resets no personagem (Reset e Master ja existem: ResetCount, MasterResetCount) */
IF COL_LENGTH('dbo.Character', 'SupremeResetCount') IS NULL
    ALTER TABLE dbo.[Character] ADD SupremeResetCount INT NOT NULL CONSTRAINT DF_Character_SupremeResetCount DEFAULT (0);
GO

/* 2) Creditos do site (WebEngine) - a config estava vazia. Guardamos por CONTA. */
IF OBJECT_ID('dbo.MuChila_Creditos') IS NULL
    CREATE TABLE dbo.MuChila_Creditos (AccountID varchar(10) NOT NULL PRIMARY KEY, Creditos int NOT NULL DEFAULT (0));
GO
-- registra no WebEngine onde ficam os creditos (para a loja/painel do site enxergarem)
IF NOT EXISTS (SELECT 1 FROM dbo.WEBENGINE_CREDITS_CONFIG WHERE config_title = 'Creditos Mu Chila')
    INSERT dbo.WEBENGINE_CREDITS_CONFIG (config_title, config_database, config_table, config_credits_col, config_user_col, config_user_col_id, config_checkonline, config_display)
    VALUES ('Creditos Mu Chila', 'MuOnlineS14', 'MuChila_Creditos', 'Creditos', 'AccountID', 'AccountID', 0, 1);
GO

/* 3) Base de atributos por classe (para o Supreme devolver os stats ao valor inicial).
   IMPORTANTE: confira estes valores criando UM personagem novo de cada classe e comparando
   (SELECT Class,Strength,Dexterity,Vitality,Energy,Leadership FROM Character WHERE Name='novo').
   A chave e a FAMILIA da classe = (Class/16)*16. Os personagens tem codigos com a evolucao
   somada (16=Dark Knight, 17=Blade Knight, 18=Blade Master...), mas a base e a mesma da familia. */
IF OBJECT_ID('dbo.MuChila_BaseStats') IS NOT NULL DROP TABLE dbo.MuChila_BaseStats;  -- recria com a chave por familia
CREATE TABLE dbo.MuChila_BaseStats (Familia tinyint NOT NULL PRIMARY KEY, Strength int, Dexterity int, Vitality int, Energy int, Leadership int);
GO
MERGE dbo.MuChila_BaseStats AS d
USING (VALUES
    (0,  18,18,15,30,0),    -- Dark Wizard  (1=Soul Master, 2=Grand Master)
    (16, 28,20,25,10,0),    -- Dark Knight  (17=Blade Knight, 18=Blade Master)
    (32, 22,25,20,15,0),    -- Fairy Elf    (33, 34)
    (48, 26,26,26,20,0),    -- Magic Gladiator (49, 50)
    (64, 26,20,20,15,25),   -- Dark Lord    (65, 66)
    (80, 21,21,18,23,0),    -- Summoner     (81, 82)
    (96, 32,27,25,20,0),    -- Rage Fighter (97, 98, 99)
    (112,29,27,25,20,0),    -- Grow Lancer  (CONFIRA)
    (128,26,26,26,20,0)     -- classe 128 (CONFIRA a familia e a base criando um char novo)
) AS s(Familia,Strength,Dexterity,Vitality,Energy,Leadership)
ON d.Familia = s.Familia
WHEN NOT MATCHED THEN INSERT (Familia,Strength,Dexterity,Vitality,Energy,Leadership)
    VALUES (s.Familia,s.Strength,s.Dexterity,s.Vitality,s.Energy,s.Leadership);
GO

/* 4) SUPREME RESET (feito pelo site, com o personagem/conta OFFLINE).
   Requisitos: Level==400, MasterLevel==600, os 4 atributos >= @MaxStat.
   Acao: zera tudo (Level->1, MasterLevel->0, skill tree, pontos), ResetCount->0,
         credita @Coins e incrementa SupremeResetCount. Tudo numa transacao.
   Retorna: 0=ok; 1=personagem inexistente; 2=conta online; 3=Level!=400;
            4=MasterLevel!=600; 5=atributos abaixo do maximo; 6=classe sem base cadastrada. */
IF OBJECT_ID('dbo.MuChila_SupremeReset') IS NOT NULL DROP PROCEDURE dbo.MuChila_SupremeReset;
GO
CREATE PROCEDURE dbo.MuChila_SupremeReset
    @Name varchar(10),
    @Coins int,          -- recompensa em creditos (o site define o valor)
    @MaxStat int = 32767 -- "valor maximo permitido"; ajuste ao teto real do servidor
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Acc varchar(10), @Class tinyint, @Level int, @ML int,
            @s int,@a int,@v int,@e int;

    SELECT @Acc=AccountID, @Class=Class, @Level=cLevel,
           @s=Strength,@a=Dexterity,@v=Vitality,@e=Energy
      FROM dbo.[Character] WHERE Name=@Name;
    IF @Acc IS NULL RETURN 1;

    -- conta precisa estar deslogada (o GameServer sobrescreve o banco enquanto online)
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id=@Acc AND ConnectStat=1) RETURN 2;

    SELECT @ML = MasterLevel FROM dbo.MasterSkillTree WHERE Name=@Name;
    SET @ML = ISNULL(@ML,0);

    IF @Level <> 400 RETURN 3;
    IF @ML    <> 600 RETURN 4;
    IF @s < @MaxStat OR @a < @MaxStat OR @v < @MaxStat OR @e < @MaxStat RETURN 5;
    IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_BaseStats WHERE Familia=(@Class/16)*16) RETURN 6;

    BEGIN TRAN;
      -- atributos de volta ao inicial da FAMILIA da classe + zera pontos e resets normais
      UPDATE c SET c.cLevel=1, c.LevelUpPoint=0, c.ResetCount=0,
                   c.Strength=b.Strength, c.Dexterity=b.Dexterity,
                   c.Vitality=b.Vitality, c.Energy=b.Energy, c.Leadership=b.Leadership,
                   c.SupremeResetCount=c.SupremeResetCount+1
        FROM dbo.[Character] c JOIN dbo.MuChila_BaseStats b ON b.Familia=(c.Class/16)*16
       WHERE c.Name=@Name;

      -- master: nivel, exp, pontos e a arvore de skills zerados
      UPDATE dbo.MasterSkillTree
         SET MasterLevel=0, MasterExperience=0, MasterPoint=0, MasterSkill=0x
       WHERE Name=@Name;

      -- recompensa em creditos do site
      IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_Creditos WHERE AccountID=@Acc)
          INSERT dbo.MuChila_Creditos(AccountID,Creditos) VALUES(@Acc,0);
      UPDATE dbo.MuChila_Creditos SET Creditos=Creditos+@Coins WHERE AccountID=@Acc;
    COMMIT TRAN;
    RETURN 0;
END;
GO

/* 4a) RESET NORMAL exato do requisito A (opcional, pelo site/offline), com a formula reset*300.
   O /reset do JOGO continua funcionando, mas da pontos fixos; use este se quiser os pontos
   escalaveis (1o reset=300, 2o=600, ...). Requisito: Level==400.
   Acao: Level->1, atributos->base da familia, LevelUpPoint = (novo ResetCount)*300, ResetCount++.
   Retorno: 0=ok; 1=inexistente; 2=conta online; 3=Level!=400; 6=classe sem base cadastrada. */
IF OBJECT_ID('dbo.MuChila_Reset') IS NOT NULL DROP PROCEDURE dbo.MuChila_Reset;
GO
CREATE PROCEDURE dbo.MuChila_Reset
    @Name varchar(10),
    @PontosPorReset int = 300
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Acc varchar(10), @Class tinyint, @Level int, @Novo int;
    SELECT @Acc=AccountID, @Class=Class, @Level=cLevel FROM dbo.[Character] WHERE Name=@Name;
    IF @Acc IS NULL RETURN 1;
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id=@Acc AND ConnectStat=1) RETURN 2;
    IF @Level <> 400 RETURN 3;
    IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_BaseStats WHERE Familia=(@Class/16)*16) RETURN 6;

    BEGIN TRAN;
      UPDATE c SET c.cLevel=1,
                   c.ResetCount=c.ResetCount+1,
                   c.LevelUpPoint=(c.ResetCount+1)*@PontosPorReset,   -- pontos escalaveis: N*300
                   c.Strength=b.Strength, c.Dexterity=b.Dexterity,
                   c.Vitality=b.Vitality, c.Energy=b.Energy, c.Leadership=b.Leadership
        FROM dbo.[Character] c JOIN dbo.MuChila_BaseStats b ON b.Familia=(c.Class/16)*16
       WHERE c.Name=@Name;
    COMMIT TRAN;
    RETURN 0;
END;
GO

/* 4b) MASTER RESET exato do requisito B (feito pelo site, offline).
   Requisito: MasterLevel==600. Acao: MasterLevel->0 (recomeca o master), limpa a arvore de
   skills e os pontos master, credita @Coins e incrementa MasterResetCount. NAO mexe no Level
   normal nem no ResetCount. (Obs.: o "Master Reset" do kit no jogo e um grand-reset por
   contagem de resets normais, coisa diferente; por isso fazemos este aqui, que bate o seu B.)
   Retorno: 0=ok; 1=inexistente; 2=conta online; 4=MasterLevel!=600. */
IF OBJECT_ID('dbo.MuChila_MasterReset') IS NOT NULL DROP PROCEDURE dbo.MuChila_MasterReset;
GO
CREATE PROCEDURE dbo.MuChila_MasterReset
    @Name varchar(10),
    @Coins int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Acc varchar(10), @ML int;
    SELECT @Acc=AccountID FROM dbo.[Character] WHERE Name=@Name;
    IF @Acc IS NULL RETURN 1;
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id=@Acc AND ConnectStat=1) RETURN 2;
    SELECT @ML = MasterLevel FROM dbo.MasterSkillTree WHERE Name=@Name;
    IF ISNULL(@ML,0) <> 600 RETURN 4;

    BEGIN TRAN;
      UPDATE dbo.MasterSkillTree
         SET MasterLevel=0, MasterExperience=0, MasterPoint=0, MasterSkill=0x
       WHERE Name=@Name;
      UPDATE dbo.[Character] SET MasterResetCount=MasterResetCount+1 WHERE Name=@Name;
      IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_Creditos WHERE AccountID=@Acc)
          INSERT dbo.MuChila_Creditos(AccountID,Creditos) VALUES(@Acc,0);
      UPDATE dbo.MuChila_Creditos SET Creditos=Creditos+@Coins WHERE AccountID=@Acc;
    COMMIT TRAN;
    RETURN 0;
END;
GO

/* 5) Recompensa do MASTER RESET (feito no jogo): o comando do kit incrementa
   MasterResetCount no banco. Esta procedure credita os que ainda nao foram pagos.
   Rode pelo cron do site (a cada minuto), so com a conta offline, igual ao Zen pendente. */
IF OBJECT_ID('dbo.MuChila_MasterResetPago') IS NULL
    CREATE TABLE dbo.MuChila_MasterResetPago (Name varchar(10) NOT NULL PRIMARY KEY, PagoAte int NOT NULL DEFAULT(0));
GO
IF OBJECT_ID('dbo.MuChila_CreditarMasterResets') IS NOT NULL DROP PROCEDURE dbo.MuChila_CreditarMasterResets;
GO
CREATE PROCEDURE dbo.MuChila_CreditarMasterResets
    @CoinsPorReset int   -- creditos por Master Reset (o site define)
AS
BEGIN
    SET NOCOUNT ON;
    -- personagens com master resets ainda nao pagos, cuja conta esta offline
    ;WITH pend AS (
        SELECT c.Name, c.AccountID, c.MasterResetCount,
               ISNULL(p.PagoAte,0) AS PagoAte
          FROM dbo.[Character] c
          LEFT JOIN dbo.MuChila_MasterResetPago p ON p.Name=c.Name
          JOIN dbo.MEMB_STAT s ON s.memb___id=c.AccountID AND s.ConnectStat=0
         WHERE c.MasterResetCount > ISNULL(p.PagoAte,0)
    )
    SELECT Name, AccountID, MasterResetCount, PagoAte,
           (MasterResetCount-PagoAte)*@CoinsPorReset AS Creditar
      INTO #pg FROM pend;

    BEGIN TRAN;
      INSERT dbo.MuChila_Creditos(AccountID,Creditos)
        SELECT DISTINCT AccountID,0 FROM #pg
         WHERE AccountID NOT IN (SELECT AccountID FROM dbo.MuChila_Creditos);
      UPDATE cr SET cr.Creditos = cr.Creditos + x.Total
        FROM dbo.MuChila_Creditos cr
        JOIN (SELECT AccountID, SUM(Creditar) Total FROM #pg GROUP BY AccountID) x ON x.AccountID=cr.AccountID;
      MERGE dbo.MuChila_MasterResetPago AS d
      USING (SELECT Name, MasterResetCount FROM #pg) AS s ON d.Name=s.Name
      WHEN MATCHED THEN UPDATE SET PagoAte=s.MasterResetCount
      WHEN NOT MATCHED THEN INSERT(Name,PagoAte) VALUES(s.Name,s.MasterResetCount);
    COMMIT TRAN;
    DROP TABLE #pg;
END;
GO

/* 6) Permissoes: o usuario do site precisa poder EXECUTAR as procedures (o acesso as tabelas
   dentro delas vem por ownership chaining, mesmo dono dbo). Ajuste o nome se o login for outro. */
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'muchila_site')
BEGIN
    GRANT EXECUTE ON dbo.MuChila_Reset             TO [muchila_site];
    GRANT EXECUTE ON dbo.MuChila_MasterReset       TO [muchila_site];
    GRANT EXECUTE ON dbo.MuChila_SupremeReset      TO [muchila_site];
    GRANT EXECUTE ON dbo.MuChila_CreditarMasterResets TO [muchila_site];
    GRANT SELECT, INSERT, UPDATE ON dbo.MuChila_Creditos TO [muchila_site];
    GRANT SELECT ON dbo.MuChila_BaseStats TO [muchila_site];
    PRINT 'permissoes concedidas a muchila_site';
END
ELSE PRINT 'AVISO: usuario muchila_site nao existe neste banco; ajuste o GRANT.';
GO

PRINT 'MuChila-Resets.sql aplicado. Confira dbo.MuChila_BaseStats antes de usar o Supreme em personagem real.';
GO

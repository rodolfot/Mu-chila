-- Mu Chila: tabelas e procedimento das páginas do site de 01/10/2026 (Contate-nos, Comprar Zen, Resetar Skill-Tree).
-- Reaplicável (Instalar-Modulos.ps1). Precisa do DB\1 - Querys\MuChila-Resets.sql já aplicado (usa dbo.MuChila_LimparSkillsMaster
-- e a tabela dbo.MuChila_MasterResetBackup).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- Contate-nos: o servidor não tem e-mail; a mensagem fica aqui e o dono lê e responde pela aba "Site" do Mu Chila Admin.
-- A resposta aparece para o jogador na própria página de contato (conta logada).
IF OBJECT_ID('dbo.MUCHILA_CONTATO') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_CONTATO (
        id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_CONTATO PRIMARY KEY,
        criado          DATETIME       NOT NULL CONSTRAINT DF_MUCHILA_CONTATO_criado DEFAULT GETDATE(),
        conta           VARCHAR(10)    NULL,                 -- conta logada (NULL = visitante)
        contato         NVARCHAR(120)  NULL,                 -- e-mail/Discord/WhatsApp informado pelo visitante
        assunto         NVARCHAR(80)   NOT NULL,
        mensagem        NVARCHAR(2000) NOT NULL,
        ip              VARCHAR(45)    NULL,
        status          VARCHAR(12)    NOT NULL CONSTRAINT DF_MUCHILA_CONTATO_status DEFAULT 'nova',
        resposta        NVARCHAR(2000) NULL,
        respondido_em   DATETIME       NULL,
        CONSTRAINT CK_MUCHILA_CONTATO_status CHECK (status IN ('nova', 'lida', 'respondida', 'arquivada'))
    );
    CREATE INDEX IX_MUCHILA_CONTATO_conta ON dbo.MUCHILA_CONTATO (conta, id DESC);
    CREATE INDEX IX_MUCHILA_CONTATO_status ON dbo.MUCHILA_CONTATO (status, id DESC);
END

-- Comprar Zen: cada compra (Cash -> Zen no baú da conta), com o saldo e o Zen de antes para conferência.
IF OBJECT_ID('dbo.MUCHILA_ZEN_COMPRAS') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_ZEN_COMPRAS (
        id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_ZEN_COMPRAS PRIMARY KEY,
        criado      DATETIME     NOT NULL CONSTRAINT DF_MUCHILA_ZEN_COMPRAS_criado DEFAULT GETDATE(),
        conta       VARCHAR(10)  NOT NULL,
        pacote      VARCHAR(30)  NOT NULL,
        zen         BIGINT       NOT NULL,
        cash        INT          NOT NULL,
        saldo_antes INT          NOT NULL,
        zen_antes   BIGINT       NOT NULL,
        ip          VARCHAR(45)  NULL
    );
    CREATE INDEX IX_MUCHILA_ZEN_COMPRAS_conta ON dbo.MUCHILA_ZEN_COMPRAS (conta, id DESC);
END
GO

/* Resetar Skill-Tree pelo site (substitui o do WebEngine, que apagava a lista INTEIRA de habilidades e devolvia os pontos
   master sem limpar a árvore: o personagem ficava com a árvore aprendida e os pontos de volta).
   Faz o mesmo que o "Zerar habilidades master" do painel: árvore vazia (as mesmas posições, todas FF 00 FF, como o painel
   grava), pontos = Master Level (1 por nível), poderes master tirados da MagicList (a habilidade comum que eles substituíam
   volta) e cobra o Zen do personagem. Backup em dbo.MuChila_MasterResetBackup (o mesmo do Master Reset), para desfazer.
   Conta fora do jogo há 30 s (o servidor grava o personagem ao sair; a mesma folga das outras operações do site).
   Retorno: 0 feito | 1 conta no jogo | 2 personagem não é da conta | 3 sem árvore master | 4 Zen insuficiente | 5 árvore já vazia */
CREATE OR ALTER PROCEDURE dbo.MuChila_LimparArvoreMaster @Conta varchar(10), @Nome varchar(10), @CustoZen int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id = @Conta AND (ConnectStat = 1 OR DATEDIFF(second, DisConnectTM, GETDATE()) < 30))
        BEGIN ROLLBACK; RETURN 1; END;
    DECLARE @money bigint, @ml int, @arvore varbinary(max);
    SELECT @money = Money FROM dbo.[Character] WITH (UPDLOCK, ROWLOCK) WHERE Name = @Nome AND AccountID = @Conta;
    IF @@ROWCOUNT = 0 BEGIN ROLLBACK; RETURN 2; END;
    SELECT @ml = MasterLevel, @arvore = MasterSkill FROM dbo.MasterSkillTree WITH (UPDLOCK, ROWLOCK) WHERE Name = @Nome;
    IF @@ROWCOUNT = 0 OR ISNULL(@ml, 0) < 1 BEGIN ROLLBACK; RETURN 3; END;
    IF ISNULL(@money, 0) < ISNULL(@CustoZen, 0) BEGIN ROLLBACK; RETURN 4; END;
    DECLARE @i int = 0, @n int = ISNULL(DATALENGTH(@arvore), 0) / 3, @aprendidas int = 0, @vazia varbinary(max) = 0x;
    WHILE @i < @n
    BEGIN
        IF SUBSTRING(@arvore, @i * 3 + 1, 3) <> 0xFF00FF SET @aprendidas += 1;
        SET @vazia = @vazia + 0xFF00FF;
        SET @i += 1;
    END;
    IF @aprendidas = 0 BEGIN ROLLBACK; RETURN 5; END;

    INSERT dbo.MuChila_MasterResetBackup (Name, MasterLevel, MasterExperience, MasterPoint, MasterSkill, MagicList)
    SELECT c.Name, m.MasterLevel, m.MasterExperience, m.MasterPoint, m.MasterSkill, c.MagicList
      FROM dbo.[Character] c JOIN dbo.MasterSkillTree m ON m.Name = c.Name WHERE c.Name = @Nome;
    UPDATE dbo.MasterSkillTree SET MasterSkill = @vazia, MasterPoint = MasterLevel WHERE Name = @Nome;
    UPDATE dbo.[Character] SET MagicList = dbo.MuChila_LimparSkillsMaster(MagicList), Money = Money - ISNULL(@CustoZen, 0)
     WHERE Name = @Nome AND AccountID = @Conta;
    COMMIT;
    RETURN 0;
END;
GO

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'muchila_site')
BEGIN
    GRANT EXECUTE ON dbo.MuChila_LimparArvoreMaster TO [muchila_site];
    GRANT SELECT, INSERT, UPDATE ON dbo.MUCHILA_CONTATO TO [muchila_site];
    GRANT SELECT, INSERT ON dbo.MUCHILA_ZEN_COMPRAS TO [muchila_site];
    PRINT 'permissoes das paginas do site concedidas a muchila_site';
END
ELSE PRINT 'AVISO: usuario muchila_site nao existe neste banco; ajuste o GRANT.';

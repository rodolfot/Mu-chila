-- Mu Chila: compras da loja de itens do site (usercp/lojaitens), pagas com Cash (CashShopData.WCoinC). Reaplicável.
-- Cada linha = um item colocado no baú da conta: o débito do Cash, o item e esta linha entram na mesma transação.
-- hex = os 16 bytes do item como ficou no baú (serial do WZ_GetItemSerial), para conferir/rastrear depois.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF OBJECT_ID('dbo.MUCHILA_LOJAITENS_COMPRAS') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_LOJAITENS_COMPRAS (
        id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_LOJAITENS_COMPRAS PRIMARY KEY,
        conta        VARCHAR(10)    NOT NULL,
        item_id      VARCHAR(12)    NOT NULL,              -- "seção-tipo" do Item.txt
        descricao    NVARCHAR(150)  NOT NULL,              -- "Dragon Helm +15 +28, Sorte, Excelente (6)"
        preco        INT            NOT NULL,              -- Cash cobrado
        saldo_antes  INT            NOT NULL,
        posicao      INT            NOT NULL,              -- 0-119 baú, 120-239 baú estendido
        serial       BIGINT         NOT NULL,
        hex          CHAR(32)       NOT NULL,
        criado       DATETIME       NOT NULL CONSTRAINT DF_MUCHILA_LOJAITENS_COMPRAS_criado DEFAULT GETDATE(),
        ip           VARCHAR(45)    NULL
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MUCHILA_LOJAITENS_COMPRAS_conta')
    CREATE INDEX IX_MUCHILA_LOJAITENS_COMPRAS_conta ON dbo.MUCHILA_LOJAITENS_COMPRAS (conta, id DESC);

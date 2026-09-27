-- Mu Chila: loja entre jogadores (issue #22). Itens do baú e personagens vendidos por dinheiro real (PIX do
-- Mercado Pago com split: 90% direto na conta do vendedor, 10% de taxa da loja). Rodar no MuOnlineS14; reaplicável.
--
-- Custódia (é o que impede duplicar): ao anunciar, o item SAI do baú do vendedor e fica só aqui (coluna item);
-- o personagem sai das vagas da conta e passa para a conta de custódia (MUCHILAMKT). Na venda vai para o comprador;
-- ao cancelar, volta para o vendedor.
--
-- anúncio: ativo -> reservado (há um PIX em aberto) -> vendido | ativo -> cancelado
-- pedido : pendente -> pago (dinheiro confirmado) -> entregue | pendente -> expirado | cancelado | falhou
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID('dbo.MUCHILA_MERCADO_ANUNCIOS') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_MERCADO_ANUNCIOS (
        id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_MERCADO_ANUNCIOS PRIMARY KEY,
        tipo             VARCHAR(10)    NOT NULL,              -- item | personagem
        vendedor         VARCHAR(10)    NOT NULL,
        titulo           NVARCHAR(100)  NOT NULL,              -- nome do item ou do personagem
        detalhes         NVARCHAR(400)  NULL,                  -- texto mostrado no anúncio (nível, opções, classe...)
        item             VARBINARY(16)  NULL,                  -- bytes do item em custódia
        item_index       INT            NULL,                  -- seção*512 + tipo
        personagem       VARCHAR(10)    NULL,                  -- personagem em custódia
        valor            DECIMAL(10,2)  NOT NULL,
        status           VARCHAR(12)    NOT NULL CONSTRAINT DF_MUCHILA_MERCADO_ANUNCIOS_status DEFAULT 'ativo',
        pedido_id        INT            NULL,                  -- pedido que reservou / comprou
        comprador        VARCHAR(10)    NULL,
        criado           DATETIME       NOT NULL CONSTRAINT DF_MUCHILA_MERCADO_ANUNCIOS_criado DEFAULT GETDATE(),
        fechado_em       DATETIME       NULL,                  -- vendido ou cancelado
        obs              NVARCHAR(1000) NULL,
        CONSTRAINT CK_MUCHILA_MERCADO_ANUNCIOS_tipo CHECK (tipo IN ('item', 'personagem')),
        CONSTRAINT CK_MUCHILA_MERCADO_ANUNCIOS_status CHECK (status IN ('ativo', 'reservado', 'vendido', 'cancelado')),
        CONSTRAINT CK_MUCHILA_MERCADO_ANUNCIOS_conteudo CHECK ((tipo = 'item' AND item IS NOT NULL) OR (tipo = 'personagem' AND personagem IS NOT NULL))
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MUCHILA_MERCADO_ANUNCIOS_status')
    CREATE INDEX IX_MUCHILA_MERCADO_ANUNCIOS_status ON dbo.MUCHILA_MERCADO_ANUNCIOS (status, criado DESC);
-- um personagem só pode estar em um anúncio aberto por vez
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MUCHILA_MERCADO_ANUNCIOS_personagem')
    CREATE UNIQUE INDEX UX_MUCHILA_MERCADO_ANUNCIOS_personagem ON dbo.MUCHILA_MERCADO_ANUNCIOS (personagem)
        WHERE personagem IS NOT NULL AND status IN ('ativo', 'reservado');

IF OBJECT_ID('dbo.MUCHILA_MERCADO_PEDIDOS') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_MERCADO_PEDIDOS (
        id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_MERCADO_PEDIDOS PRIMARY KEY,
        anuncio_id       INT            NOT NULL,
        comprador        VARCHAR(10)    NOT NULL,
        vendedor         VARCHAR(10)    NOT NULL,
        descricao        NVARCHAR(100)  NOT NULL,
        valor            DECIMAL(10,2)  NOT NULL,
        taxa             DECIMAL(10,2)  NOT NULL,              -- parte da loja (application_fee do Mercado Pago)
        status           VARCHAR(12)    NOT NULL CONSTRAINT DF_MUCHILA_MERCADO_PEDIDOS_status DEFAULT 'pendente',
        provedor         VARCHAR(20)    NOT NULL,              -- simulado | mercadopago
        provedor_id      VARCHAR(64)    NULL,
        pix_copia_cola   NVARCHAR(1000) NULL,
        qr_base64        VARCHAR(MAX)   NULL,
        simulado_status  VARCHAR(12)    NULL,
        criado           DATETIME       NOT NULL CONSTRAINT DF_MUCHILA_MERCADO_PEDIDOS_criado DEFAULT GETDATE(),
        expira           DATETIME       NOT NULL,
        pago_em          DATETIME       NULL,
        entregue_em      DATETIME       NULL,
        obs              NVARCHAR(1000) NULL,
        ip               VARCHAR(45)    NULL,
        CONSTRAINT CK_MUCHILA_MERCADO_PEDIDOS_status CHECK (status IN ('pendente', 'pago', 'entregue', 'cancelado', 'expirado', 'falhou'))
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MUCHILA_MERCADO_PEDIDOS_comprador')
    CREATE INDEX IX_MUCHILA_MERCADO_PEDIDOS_comprador ON dbo.MUCHILA_MERCADO_PEDIDOS (comprador, criado DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MUCHILA_MERCADO_PEDIDOS_provedor')
    CREATE UNIQUE INDEX UX_MUCHILA_MERCADO_PEDIDOS_provedor ON dbo.MUCHILA_MERCADO_PEDIDOS (provedor, provedor_id) WHERE provedor_id IS NOT NULL;

-- vendedores: ligação da conta do jogo com a conta do Mercado Pago (OAuth). Tokens criptografados (AES-256-GCM,
-- chave em Site\config-local\mercadopago.json). "simulado" = ligação de teste, sem Mercado Pago de verdade.
IF OBJECT_ID('dbo.MUCHILA_MERCADO_VENDEDORES') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_MERCADO_VENDEDORES (
        conta            VARCHAR(10)    NOT NULL CONSTRAINT PK_MUCHILA_MERCADO_VENDEDORES PRIMARY KEY,
        mp_user_id       VARCHAR(40)    NULL,
        access_token     VARCHAR(MAX)   NULL,
        refresh_token    VARCHAR(MAX)   NULL,
        token_expira     DATETIME       NULL,
        simulado         BIT            NOT NULL CONSTRAINT DF_MUCHILA_MERCADO_VENDEDORES_simulado DEFAULT 0,
        conectado_em     DATETIME       NULL,
        oauth_state      VARCHAR(64)    NULL,
        oauth_state_em   DATETIME       NULL
    );
END

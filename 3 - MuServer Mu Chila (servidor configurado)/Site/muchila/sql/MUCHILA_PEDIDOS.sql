-- Mu Chila: pedidos da loja do site (VIP e cash). Rodar uma vez no banco MuOnlineS14.
-- Os valores do pacote (VIP, dias, cash, preço) são copiados para o pedido: mudar o preço depois não altera pedidos antigos.
-- status: pendente -> entregue | cancelado | expirado | falhou (pago, mas a entrega deu erro: o painel admin reentrega)
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- exigido pelo índice filtrado (o sqlcmd deixa desligado)
IF OBJECT_ID('dbo.MUCHILA_PEDIDOS') IS NULL
BEGIN
    CREATE TABLE dbo.MUCHILA_PEDIDOS (
        id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MUCHILA_PEDIDOS PRIMARY KEY,
        conta            VARCHAR(10)    NOT NULL,
        pacote           VARCHAR(30)    NOT NULL,
        tipo             VARCHAR(10)    NOT NULL,              -- vip | cash
        descricao        NVARCHAR(100)  NOT NULL,
        valor            DECIMAL(10,2)  NOT NULL,
        vip_nivel        INT            NULL,
        vip_dias         INT            NULL,
        cash             INT            NULL,
        status           VARCHAR(12)    NOT NULL CONSTRAINT DF_MUCHILA_PEDIDOS_status DEFAULT 'pendente',
        provedor         VARCHAR(20)    NOT NULL,              -- simulado | mercadopago
        provedor_id      VARCHAR(64)    NULL,                  -- id do pagamento no provedor
        pix_copia_cola   NVARCHAR(1000) NULL,
        qr_base64        VARCHAR(MAX)   NULL,
        simulado_status  VARCHAR(12)    NULL,                  -- só no provedor simulado: aprovado | recusado
        criado           DATETIME       NOT NULL CONSTRAINT DF_MUCHILA_PEDIDOS_criado DEFAULT GETDATE(),
        expira           DATETIME       NOT NULL,
        entregue_em      DATETIME       NULL,
        entregue_por     VARCHAR(40)    NULL,                  -- webhook, simulacao, admin:<conta>
        obs              NVARCHAR(1000) NULL,
        ip               VARCHAR(45)    NULL,
        CONSTRAINT CK_MUCHILA_PEDIDOS_tipo CHECK (tipo IN ('vip', 'cash')),
        CONSTRAINT CK_MUCHILA_PEDIDOS_status CHECK (status IN ('pendente', 'entregue', 'cancelado', 'expirado', 'falhou'))
    );
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MUCHILA_PEDIDOS_conta')
    CREATE INDEX IX_MUCHILA_PEDIDOS_conta ON dbo.MUCHILA_PEDIDOS (conta, criado DESC);
-- o mesmo pagamento do provedor nunca vale para dois pedidos
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MUCHILA_PEDIDOS_provedor')
    CREATE UNIQUE INDEX UX_MUCHILA_PEDIDOS_provedor ON dbo.MUCHILA_PEDIDOS (provedor, provedor_id) WHERE provedor_id IS NOT NULL;

-- 25/09/2026: Zen de bônus do VIP, entregue no baú (warehouse.Money) só com a conta fora do jogo;
-- zen_entregue_em nulo com zen > 0 = aguardando a conta sair (o servidor regrava o baú aberto ao fechar)
IF COL_LENGTH('dbo.MUCHILA_PEDIDOS', 'zen') IS NULL
    ALTER TABLE dbo.MUCHILA_PEDIDOS ADD zen BIGINT NULL, zen_entregue_em DATETIME NULL;
-- (a tarefa "Mu Chila - Zen do VIP" do agendador do WebEngine é registrada pelo Instalar-Modulos.ps1, que calcula o MD5 do arquivo)

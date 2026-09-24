-- Restore Item (recompra de itens vendidos) para o kit Season 14 (MuDevs).
-- Estrutura e logica dos procedimentos originais da Webzen (banco de referencia Season 13).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.T_RestoreItem_Inventory', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[T_RestoreItem_Inventory](
		[AccountID] [varchar](10) NOT NULL,
		[Name] [varchar](10) NOT NULL,
		[RestoreInven] [varbinary](150) NOT NULL,
		[DbVersion] [tinyint] NULL,
		CONSTRAINT [PK_T_RestoreItem_Inventory_Name_AccountID] PRIMARY KEY CLUSTERED ([AccountID] ASC, [Name] ASC)
	) ON [PRIMARY]
END
GO

-- Carrega o inventario de restauracao; cria um vazio (0xFF = slot livre) se o personagem ainda nao tiver.
CREATE OR ALTER PROCEDURE [dbo].[WZ_GetLoadRestoreInventory]
	@AccountID VARCHAR(10),
	@Name VARCHAR(10)
AS
SET NOCOUNT ON
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED

DECLARE @RestoreInven VARBINARY(150)

SELECT @RestoreInven = RestoreInven
FROM dbo.T_RestoreItem_Inventory
WHERE AccountID = @AccountID
	AND [Name] = @Name

IF @@ROWCOUNT = 0
BEGIN
	SET @RestoreInven = CAST(REPLICATE(CHAR(255), 150) AS VARBINARY(150))

	INSERT INTO dbo.T_RestoreItem_Inventory (AccountID, [Name], RestoreInven, DbVersion)
	VALUES (@AccountID, @Name, @RestoreInven, 3)
END

SELECT @RestoreInven
GO

-- Salva o inventario de restauracao; cria o registro se ainda nao existir.
CREATE OR ALTER PROCEDURE [dbo].[WZ_SetSaveRestoreInventory]
	@AccountID VARCHAR(10),
	@Name VARCHAR(10),
	@RestoreInven VARBINARY(150)
AS
SET NOCOUNT ON
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED

UPDATE dbo.T_RestoreItem_Inventory
SET RestoreInven = @RestoreInven,
	DbVersion = 3
WHERE AccountID = @AccountID
	AND [Name] = @Name

IF @@ROWCOUNT = 0
BEGIN
	SET @RestoreInven = CAST(REPLICATE(CHAR(255), 150) AS VARBINARY(150))

	INSERT INTO dbo.T_RestoreItem_Inventory (AccountID, [Name], RestoreInven, DbVersion)
	VALUES (@AccountID, @Name, @RestoreInven, 3)
END
GO

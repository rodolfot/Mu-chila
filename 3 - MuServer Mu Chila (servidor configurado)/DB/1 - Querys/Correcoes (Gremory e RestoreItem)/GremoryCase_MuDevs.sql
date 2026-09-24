-- Gremory Case para o kit Season 14 (MuDevs), adaptado do IGCN S9.5 (IGC_GremoryCase_*).
-- A tabela tem o mesmo esquema da dbo.GremoryCase que ja existe no banco BattleCore do kit.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.GremoryCase', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[GremoryCase](
		[AccountID] [varchar](10) NOT NULL,
		[Name] [varchar](10) NULL,
		[StorageType] [tinyint] NOT NULL,
		[RewardSource] [smallint] NOT NULL,
		[AuthCode] [int] NOT NULL,
		[ItemGUID] [smallint] NOT NULL,
		[ItemID] [int] NOT NULL,
		[ItemLevel] [tinyint] NOT NULL,
		[ItemDurability] [smallint] NOT NULL,
		[ItemOp1] [tinyint] NOT NULL,
		[ItemOp2] [tinyint] NOT NULL,
		[ItemOp3] [tinyint] NOT NULL,
		[ItemExcOption] [smallint] NOT NULL,
		[ItemSetOption] [smallint] NOT NULL,
		[ItemSocketCount] [tinyint] NOT NULL,
		[ItemMainAttribute] [tinyint] NOT NULL,
		[ItemMuunEvoItemType] [smallint] NOT NULL,
		[ItemMuunEvoItemIndex] [smallint] NOT NULL,
		[ReceiveDate] [bigint] NOT NULL,
		[ExpireDate] [bigint] NOT NULL
	) ON [PRIMARY]
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GremoryCaseGetItemList]
	@szAccountID varchar(10),
	@szName varchar(10)
AS
BEGIN
	SET NOCOUNT ON;

	SELECT * FROM GremoryCase WHERE AccountID = @szAccountID AND ((Name = @szName AND StorageType = 2) OR StorageType = 1) ORDER BY StorageType ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GremoryCaseAddItem]
	@szAccountID varchar(10),
	@szName	varchar(10),
	@StorageType tinyint,
	@RewardSource tinyint,
	@ItemID smallint,
	@ItemLevel tinyint,
	@ItemDurability smallint,
	@ItemOp1 tinyint,
	@ItemOp2 tinyint,
	@ItemOp3 tinyint,
	@ItemExcOption smallint,
	@ItemSetOption smallint,
	@ItemSocketCount tinyint,
	@ItemMainAttribute tinyint,
	@ItemMuunEvoItemType smallint,
	@ItemMuunEvoItemIndex smallint,
	@ReceiveDate bigint,
	@ExpireDate bigint
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @ItemsInStorage int
	SET @ItemsInStorage = (SELECT COUNT(*) FROM GremoryCase WHERE AccountID = @szAccountID AND ((Name = @szName AND StorageType = 2) OR StorageType = 1))

	WHILE (@ItemsInStorage >= 50)
	BEGIN
		DELETE FROM GremoryCase WHERE AccountID = @szAccountID AND ((Name = @szName AND StorageType = 2) OR StorageType = 1) AND ReceiveDate =
		(SELECT MIN(ReceiveDate) FROM GremoryCase WHERE AccountID = @szAccountID AND ((Name = @szName AND StorageType = 2) OR StorageType = 1))
		SET @ItemsInStorage = @ItemsInStorage - 1
	END

	DECLARE @FreeItemGUID int
	SET @FreeItemGUID = 0

	WHILE EXISTS (SELECT * FROM GremoryCase WHERE AccountID = @szAccountID AND ((Name = @szName AND StorageType = 2) OR StorageType = 1) AND ItemGUID = @FreeItemGUID)
	BEGIN
		SET @FreeItemGUID = @FreeItemGUID + 1
	END

	DECLARE @FreeAuthCode int
	SET @FreeAuthCode = ROUND(((2000000000 - 1 - 1) * RAND() + 1), 0)

	WHILE EXISTS (SELECT * FROM GremoryCase WHERE AuthCode = @FreeAuthCode)
	BEGIN
		SET @FreeAuthCode = ROUND(((2000000000 - 1 - 1) * RAND() + 1), 0)
	END

	INSERT INTO GremoryCase (AccountID, Name, StorageType, RewardSource, ItemGUID, AuthCode, ItemID, ItemLevel, ItemDurability, ItemOp1, ItemOp2, ItemOp3, ItemExcOption, ItemSetOption, ItemSocketCount, ItemMainAttribute, ItemMuunEvoItemType, ItemMuunEvoItemIndex, ReceiveDate, ExpireDate) VALUES
	(@szAccountID, @szName, @StorageType, @RewardSource, @FreeItemGUID, @FreeAuthCode, @ItemID, @ItemLevel, @ItemDurability, @ItemOp1, @ItemOp2, @ItemOp3, @ItemExcOption, @ItemSetOption, @ItemSocketCount, @ItemMainAttribute, @ItemMuunEvoItemType, @ItemMuunEvoItemIndex, @ReceiveDate, @ExpireDate)

	SELECT @FreeItemGUID AS ItemGUID, @FreeAuthCode AS AuthCode
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GremoryCaseCheckUseItem]
	@ItemID smallint,
	@ItemGUID int,
	@AuthCode int
AS
BEGIN
	SET NOCOUNT ON;

	IF EXISTS (SELECT * FROM GremoryCase WHERE ItemID = @ItemID AND ItemGUID = @ItemGUID AND AuthCode = @AuthCode)
		SELECT 1 AS ResultCode
	ELSE
		SELECT 0 AS ResultCode
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GremoryCaseDeleteItem]
	@ItemID smallint,
	@ItemGUID int,
	@AuthCode int
AS
BEGIN
	SET NOCOUNT ON;

	DELETE FROM GremoryCase WHERE ItemID = @ItemID AND ItemGUID = @ItemGUID AND AuthCode = @AuthCode
END
GO

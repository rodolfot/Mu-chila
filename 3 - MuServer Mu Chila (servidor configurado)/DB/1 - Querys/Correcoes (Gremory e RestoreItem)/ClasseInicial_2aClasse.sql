-- Mu Chila: personagens novos de DW, DK, Elfa e Summoner nascem na 2a classe (+1), igual ao /change.
-- O cliente S14 ja mostra essas classes como 2a classe desde a criacao; sem isto o servidor
-- recusava itens e skills de 2a classe que o cliente oferecia.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER TRIGGER [dbo].[TR_MuChila_ClasseInicial] ON [dbo].[Character]
AFTER INSERT
AS
BEGIN
	SET NOCOUNT ON;

	UPDATE c
	SET c.Class = c.Class + 1
	FROM dbo.Character c
	JOIN inserted i ON i.Name = c.Name
	WHERE i.Class IN (0, 16, 32, 80)   -- Dark Wizard, Dark Knight, Fairy Elf, Summoner (classe basica)
END
GO

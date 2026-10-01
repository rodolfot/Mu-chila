-- Mu Chila: personagens novos de DW, DK, Elfa e Summoner nascem na 2a classe (+1), igual ao /change, e ja com as duas
-- missoes da 2a classe concluidas (indices 0 e 1 do Data\Quest\Quest.txt), com os 20 pontos que elas dao (10 + 10,
-- Data\Quest\QuestReward.txt).
-- Por que a classe +1 (24/09/2026): o cliente S14 mostra a basica e a 2a classe com o mesmo nome, e sem isto o servidor
-- recusava itens e skills de 2a classe que o cliente oferecia.
-- Por que as missoes (issue #35, 01/10/2026): so a classe +1 deixava a missao da 2a classe pendente. O personagem aparecia
-- como Blade Knight/Soul Master, mas tinha de fazer a evolucao para seguir a corrente de missoes (2a -> 3a -> 4a classe).
-- Os personagens do kit que ja sao 2a classe (ex.: Mage, Quest3) tem essas duas missoes concluidas.
-- Character.Quest: 2 bits por missao, missao 0 nos bits mais baixos do 1o byte; 3 = nao comecada, 2 = concluida
-- (0xFA no 1o byte = missoes 0 e 1 concluidas, 2 e 3 nao comecadas). A missao 2 ("Hero Status") continua por fazer.
-- Para os personagens criados antes desta correcao: ClasseInicial_MissoesDa2aClasse_existentes.sql (uma vez).
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
	SET c.Class = c.Class + 1,
	    c.LevelUpPoint = c.LevelUpPoint + 20,
	    c.Quest = CAST(CAST((CAST(SUBSTRING(q.Quest, 1, 1) AS tinyint) & 240) | 10 AS tinyint) AS varbinary(1)) + SUBSTRING(q.Quest, 2, 49)
	FROM dbo.Character c
	JOIN inserted i ON i.Name = c.Name
	CROSS APPLY (SELECT ISNULL(c.Quest, CAST(REPLICATE(CHAR(255), 50) AS varbinary(50))) AS Quest) q
	WHERE i.Class IN (0, 16, 32, 80)   -- Dark Wizard, Dark Knight, Fairy Elf, Summoner (classe basica)
END
GO

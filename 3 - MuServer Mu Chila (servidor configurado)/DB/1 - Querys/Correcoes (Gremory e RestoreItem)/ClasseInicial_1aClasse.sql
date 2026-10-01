-- Mu Chila (issue #35, 01/10/2026): personagens novos de DW, DK, Elfa e Summoner nascem na 1a classe (Dark Wizard,
-- Dark Knight, Fairy Elf, Summoner), como no kit, e so viram 2a classe fazendo a missao (Data\Quest\Quest.txt, indices
-- 0 e 1, nivel 150; a recompensa tipo 2 do QuestReward.txt muda a classe).
-- Remove o gatilho TR_MuChila_ClasseInicial, que de 24/09 a 01/10/2026 fazia esses personagens nascerem na 2a classe.
-- Os personagens criados nesse periodo: ..\MuChila-Issue35-VoltarPara1aClasse-ver.sql e -aplicar.sql (uma vez).
-- Atencao: o cliente S14 em portugues mostra o nome da 2a classe tambem na 1a (ex.: "Blade Knight" no Dark Knight).
IF OBJECT_ID('dbo.TR_MuChila_ClasseInicial') IS NOT NULL
    DROP TRIGGER dbo.TR_MuChila_ClasseInicial;

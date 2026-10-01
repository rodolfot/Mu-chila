-- Mu Chila (issue #35, 01/10/2026): rodar UMA vez, depois de aplicar o ClasseInicial_2aClasse.sql novo.
-- Personagens na 2a classe de DW, DK, Elfa e Summoner (Soul Master, Blade Knight, Muse Elf, Bloody Summoner) sem as
-- missoes da 2a classe concluidas: os criados pelo gatilho antigo (so classe +1) e os evoluidos pelo /change quando ele
-- existia. Marca as missoes 0 e 1 como concluidas e da os 10 pontos de cada missao que faltava (o mesmo que ganhariam
-- fazendo a missao). Quem ja fez as missoes nao muda.
--
-- Rodar com o servidor DESLIGADO, ou ao menos com essas contas fora do jogo: o GameServer grava o personagem ao sair e
-- desfaria a mudanca. O script pula as contas online e lista quem ficou de fora (rode de novo depois).
SET NOCOUNT ON;

DECLARE @Vazio varbinary(50) = CAST(REPLICATE(CHAR(255), 50) AS varbinary(50));

-- o que vai mudar (antes de mudar)
SELECT c.AccountID AS Conta, c.Name AS Personagem, c.Class AS Classe, c.cLevel AS Nivel, c.LevelUpPoint AS PontosAntes,
       f.Pontos AS PontosDados, CONVERT(varchar(4), SUBSTRING(f.Q, 1, 1), 2) AS QuestByte0Antes,
       CASE WHEN s.ConnectStat = 1 THEN 'ONLINE: pulado' ELSE 'corrigir' END AS Situacao
FROM dbo.Character c
CROSS APPLY (SELECT ISNULL(c.Quest, @Vazio) AS Q) q
CROSS APPLY (SELECT CAST(SUBSTRING(q.Q, 1, 1) AS tinyint) AS B) b
CROSS APPLY (SELECT q.Q, b.B, (CASE WHEN (b.B & 3) <> 2 THEN 10 ELSE 0 END) + (CASE WHEN (b.B & 12) <> 8 THEN 10 ELSE 0 END) AS Pontos) f
LEFT JOIN dbo.MEMB_STAT s ON s.memb___id = c.AccountID
WHERE c.Class IN (1, 17, 33, 81) AND f.Pontos > 0
ORDER BY c.AccountID, c.Name;

BEGIN TRAN;
UPDATE c
SET c.LevelUpPoint = c.LevelUpPoint + f.Pontos,
    c.Quest = CAST(CAST((f.B & 240) | 10 AS tinyint) AS varbinary(1)) + SUBSTRING(f.Q, 2, 49)
FROM dbo.Character c
CROSS APPLY (SELECT ISNULL(c.Quest, @Vazio) AS Q) q
CROSS APPLY (SELECT CAST(SUBSTRING(q.Q, 1, 1) AS tinyint) AS B) b
CROSS APPLY (SELECT q.Q, b.B, (CASE WHEN (b.B & 3) <> 2 THEN 10 ELSE 0 END) + (CASE WHEN (b.B & 12) <> 8 THEN 10 ELSE 0 END) AS Pontos) f
WHERE c.Class IN (1, 17, 33, 81) AND f.Pontos > 0
  AND NOT EXISTS (SELECT 1 FROM dbo.MEMB_STAT s WHERE s.memb___id = c.AccountID AND s.ConnectStat = 1);
PRINT CONCAT(@@ROWCOUNT, ' personagem(ns) corrigido(s).');
COMMIT;

-- Mu Chila (issue #35, 01/10/2026): SO MOSTRA quem volta para a 1a classe. Nao muda nada.
-- Depois de conferir a lista, rode o MuChila-Issue35-VoltarPara1aClasse-aplicar.sql com o servidor DESLIGADO.
--
-- Quem entra (DW, DK, Elfa e Summoner na 2a classe que nao fizeram a missao da 2a classe):
--   A) na 2a classe sem as missoes 0 e 1 concluidas: nasceram na 2a classe pelo gatilho (24/09 a 01/10), subiram +1
--      a mao em 24/09 ou pelo /change (desligado desde 27/09). Volta a classe; as missoes ficam como estao.
--   B) criados em 01/10/2026 ou depois com as missoes 0 e 1 marcadas: o gatilho de 01/10 marcava as missoes e dava 20
--      pontos sem o jogador fazer nada. Volta a classe, desmarca as missoes e tira os 20 pontos (os que ainda nao foram
--      distribuidos; o que ja foi para os atributos fica).
--   C) igual ao B, mas ja na 1a classe (ex.: personagem de teste mudado a mao): desmarca as missoes e tira os 20 pontos.
-- Quem ja passou para a 3a ou 4a classe nao muda (fez a missao seguinte).
-- Character.Quest: 2 bits por missao, missao 0 nos bits mais baixos do 1o byte (2 = concluida, 3 = nao comecada).
SET NOCOUNT ON;
DECLARE @Vazio varbinary(50) = CAST(REPLICATE(CHAR(255), 50) AS varbinary(50));
DECLARE @GatilhoNovo datetime = '20261001';

SELECT c.AccountID AS Conta, c.Name AS Personagem, c.Class AS ClasseAgora,
       CASE WHEN x.Caso IN ('A', 'B') THEN c.Class - 1 ELSE c.Class END AS ClasseDepois,
       c.cLevel AS Nivel, c.ResetCount AS Resets, CONVERT(varchar(16), c.MDate, 120) AS Criado,
       c.LevelUpPoint AS PontosAgora,
       CASE WHEN x.Caso IN ('B', 'C') THEN CASE WHEN c.LevelUpPoint >= 20 THEN c.LevelUpPoint - 20 ELSE 0 END ELSE c.LevelUpPoint END AS PontosDepois,
       CONVERT(varchar(4), SUBSTRING(q.Q, 1, 1), 2) AS Missoes,
       CASE x.Caso WHEN 'A' THEN 'A: 2a classe sem a missao' WHEN 'B' THEN 'B: gatilho de 01/10' ELSE 'C: missao marcada pelo gatilho de 01/10' END AS Motivo,
       CASE WHEN s.ConnectStat = 1 THEN 'ONLINE: sera pulado' ELSE 'muda' END AS Situacao
FROM dbo.Character c
CROSS APPLY (SELECT ISNULL(c.Quest, @Vazio) AS Q) q
CROSS APPLY (SELECT CAST(SUBSTRING(q.Q, 1, 1) AS tinyint) AS B) b
CROSS APPLY (SELECT CASE
    WHEN c.Class IN (1, 17, 33, 81) AND (b.B & 15) <> 10 THEN 'A'
    WHEN c.Class IN (1, 17, 33, 81) AND (b.B & 15) = 10 AND c.MDate >= @GatilhoNovo THEN 'B'
    WHEN c.Class IN (0, 16, 32, 80) AND (b.B & 15) = 10 AND c.MDate >= @GatilhoNovo THEN 'C' END AS Caso) x
LEFT JOIN dbo.MEMB_STAT s ON s.memb___id = c.AccountID
WHERE x.Caso IS NOT NULL
ORDER BY c.AccountID, c.Name;

SELECT CASE WHEN OBJECT_ID('dbo.TR_MuChila_ClasseInicial') IS NULL THEN 'gatilho TR_MuChila_ClasseInicial: ja removido'
            ELSE 'gatilho TR_MuChila_ClasseInicial: ainda existe (o -aplicar remove)' END AS Gatilho;

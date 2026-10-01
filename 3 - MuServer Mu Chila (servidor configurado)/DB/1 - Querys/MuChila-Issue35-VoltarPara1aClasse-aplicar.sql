-- Mu Chila (issue #35, 01/10/2026): APLICA a volta para a 1a classe e remove o gatilho TR_MuChila_ClasseInicial
-- (personagens novos passam a nascer na 1a classe). Regras e lista: MuChila-Issue35-VoltarPara1aClasse-ver.sql.
--
-- Rodar com o servidor DESLIGADO (o GameServer grava o personagem ao sair e desfaria a mudanca). Contas online sao
-- puladas e aparecem na lista; rode de novo depois que sairem. Rodar de novo nao muda quem ja voltou.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @Vazio varbinary(50) = CAST(REPLICATE(CHAR(255), 50) AS varbinary(50));
DECLARE @GatilhoNovo datetime = '20261001';

SELECT c.Name, c.AccountID, q.Q, b.B, x.Caso, CASE WHEN s.ConnectStat = 1 THEN 1 ELSE 0 END AS Online
INTO #alvo
FROM dbo.Character c
CROSS APPLY (SELECT ISNULL(c.Quest, @Vazio) AS Q) q
CROSS APPLY (SELECT CAST(SUBSTRING(q.Q, 1, 1) AS tinyint) AS B) b
CROSS APPLY (SELECT CASE
    WHEN c.Class IN (1, 17, 33, 81) AND (b.B & 15) <> 10 THEN 'A'
    WHEN c.Class IN (1, 17, 33, 81) AND (b.B & 15) = 10 AND c.MDate >= @GatilhoNovo THEN 'B'
    WHEN c.Class IN (0, 16, 32, 80) AND (b.B & 15) = 10 AND c.MDate >= @GatilhoNovo THEN 'C' END AS Caso) x
LEFT JOIN dbo.MEMB_STAT s ON s.memb___id = c.AccountID
WHERE x.Caso IS NOT NULL;

SELECT a.AccountID AS Conta, a.Name AS Personagem, c.Class AS ClasseAntes,
       CASE WHEN a.Caso IN ('A', 'B') THEN c.Class - 1 ELSE c.Class END AS ClasseDepois, a.Caso AS Caso,
       CASE WHEN a.Online = 1 THEN 'ONLINE: pulado (rode de novo depois)' ELSE 'mudou' END AS Situacao
FROM #alvo a JOIN dbo.Character c ON c.Name = a.Name
ORDER BY a.AccountID, a.Name;

BEGIN TRAN;
UPDATE c
SET c.Class = CASE WHEN a.Caso IN ('A', 'B') THEN c.Class - 1 ELSE c.Class END,
    c.LevelUpPoint = CASE WHEN a.Caso IN ('B', 'C') THEN CASE WHEN c.LevelUpPoint >= 20 THEN c.LevelUpPoint - 20 ELSE 0 END ELSE c.LevelUpPoint END,
    c.Quest = CASE WHEN a.Caso IN ('B', 'C') THEN CAST(CAST((a.B & 240) | 15 AS tinyint) AS varbinary(1)) + SUBSTRING(a.Q, 2, 49) ELSE c.Quest END
FROM dbo.Character c
JOIN #alvo a ON a.Name = c.Name
WHERE a.Online = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.MEMB_STAT s WHERE s.memb___id = c.AccountID AND s.ConnectStat = 1);
DECLARE @n int = @@ROWCOUNT;
IF OBJECT_ID('dbo.TR_MuChila_ClasseInicial') IS NOT NULL
    DROP TRIGGER dbo.TR_MuChila_ClasseInicial;
COMMIT;

PRINT CONCAT(@n, ' personagem(ns) mudado(s). Gatilho TR_MuChila_ClasseInicial removido: os personagens novos nascem na 1a classe.');
DROP TABLE #alvo;

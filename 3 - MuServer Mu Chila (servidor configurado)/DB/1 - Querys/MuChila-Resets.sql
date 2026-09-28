/* ============================================================================
   Mu Chila - Sistema avancado de resets (parte do BANCO)
   ----------------------------------------------------------------------------
   Contexto: o GameServer e um binario fechado (MuDevs). Reset e Master Reset sao
   feitos DENTRO do jogo (comandos do kit). Este script cuida do que o jogo NAO faz:
     - o contador e o procedimento do SUPREME RESET (feito pelo site, offline);
     - o armazenamento e a entrega de CREDITOS do site (recompensa dos resets);
     - a reconciliacao que credita por Master Reset feito no jogo.

   >>> RODE PRIMEIRO NUMA COPIA DE TESTE DO BANCO. <<<
   Idempotente: pode rodar de novo sem duplicar. Alvo: banco MuOnlineS14.
   ============================================================================ */
USE MuOnlineS14;
GO
SET NOCOUNT ON;
GO

/* 1) Contador de Supreme Resets no personagem (Reset e Master ja existem: ResetCount, MasterResetCount) */
IF COL_LENGTH('dbo.Character', 'SupremeResetCount') IS NULL
    ALTER TABLE dbo.[Character] ADD SupremeResetCount INT NOT NULL CONSTRAINT DF_Character_SupremeResetCount DEFAULT (0);
GO

/* 2) Creditos do site (WebEngine) - a config estava vazia. Guardamos por CONTA. */
IF OBJECT_ID('dbo.MuChila_Creditos') IS NULL
    CREATE TABLE dbo.MuChila_Creditos (AccountID varchar(10) NOT NULL PRIMARY KEY, Creditos int NOT NULL DEFAULT (0));
GO
-- registra no WebEngine onde ficam os creditos (para a loja/painel do site enxergarem)
IF NOT EXISTS (SELECT 1 FROM dbo.WEBENGINE_CREDITS_CONFIG WHERE config_title = 'Creditos Mu Chila')
    INSERT dbo.WEBENGINE_CREDITS_CONFIG (config_title, config_database, config_table, config_credits_col, config_user_col, config_user_col_id, config_checkonline, config_display)
    VALUES ('Creditos Mu Chila', 'MuOnlineS14', 'MuChila_Creditos', 'Creditos', 'AccountID', 'AccountID', 0, 1);
GO

/* 3) Base de atributos por classe (para o Supreme devolver os stats ao valor inicial).
   IMPORTANTE: confira estes valores criando UM personagem novo de cada classe e comparando
   (SELECT Class,Strength,Dexterity,Vitality,Energy,Leadership FROM Character WHERE Name='novo').
   A chave e a FAMILIA da classe = (Class/16)*16. Os personagens tem codigos com a evolucao
   somada (16=Dark Knight, 17=Blade Knight, 18=Blade Master...), mas a base e a mesma da familia. */
IF OBJECT_ID('dbo.MuChila_BaseStats') IS NOT NULL DROP TABLE dbo.MuChila_BaseStats;  -- recria com a chave por familia
CREATE TABLE dbo.MuChila_BaseStats (Familia tinyint NOT NULL PRIMARY KEY, Strength int, Dexterity int, Vitality int, Energy int, Leadership int);
GO
MERGE dbo.MuChila_BaseStats AS d
USING (VALUES
    (0,  18,18,15,30,0),    -- Dark Wizard  (1=Soul Master, 2=Grand Master)
    (16, 28,20,25,10,0),    -- Dark Knight  (17=Blade Knight, 18=Blade Master)
    (32, 22,25,20,15,0),    -- Fairy Elf    (33, 34)
    (48, 26,26,26,20,0),    -- Magic Gladiator (49, 50)
    (64, 26,20,20,15,25),   -- Dark Lord    (65, 66)
    (80, 21,21,18,23,0),    -- Summoner     (81, 82)
    (96, 32,27,25,20,0),    -- Rage Fighter (97, 98, 99)
    (112,29,27,25,20,0),    -- Grow Lancer  (CONFIRA)
    (128,26,26,26,20,0)     -- classe 128 (CONFIRA a familia e a base criando um char novo)
) AS s(Familia,Strength,Dexterity,Vitality,Energy,Leadership)
ON d.Familia = s.Familia
WHEN NOT MATCHED THEN INSERT (Familia,Strength,Dexterity,Vitality,Energy,Leadership)
    VALUES (s.Familia,s.Strength,s.Dexterity,s.Vitality,s.Energy,s.Leadership);
GO

/* 4) SUPREME RESET (feito pelo site, com o personagem/conta OFFLINE).
   Requisitos: Level==400, MasterLevel==600, os 4 atributos >= @MaxStat.
   Acao: zera tudo (Level->1, MasterLevel->0, skill tree, pontos), ResetCount->0,
         credita @Coins e incrementa SupremeResetCount. Tudo numa transacao.
   Retorna: 0=ok; 1=personagem inexistente; 2=conta online; 3=Level!=400;
            4=MasterLevel!=600; 5=atributos abaixo do maximo; 6=classe sem base cadastrada. */
IF OBJECT_ID('dbo.MuChila_SupremeReset') IS NOT NULL DROP PROCEDURE dbo.MuChila_SupremeReset;
GO
CREATE PROCEDURE dbo.MuChila_SupremeReset
    @Name varchar(10),
    @Coins int,          -- recompensa em creditos (o site define o valor)
    @MaxStat int = 32767 -- "valor maximo permitido"; ajuste ao teto real do servidor
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Acc varchar(10), @Class tinyint, @Level int, @ML int,
            @s int,@a int,@v int,@e int;

    SELECT @Acc=AccountID, @Class=Class, @Level=cLevel,
           @s=Strength,@a=Dexterity,@v=Vitality,@e=Energy
      FROM dbo.[Character] WHERE Name=@Name;
    IF @Acc IS NULL RETURN 1;

    -- conta precisa estar deslogada (o GameServer sobrescreve o banco enquanto online)
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id=@Acc AND ConnectStat=1) RETURN 2;

    SELECT @ML = MasterLevel FROM dbo.MasterSkillTree WHERE Name=@Name;
    SET @ML = ISNULL(@ML,0);

    IF @Level <> 400 RETURN 3;
    IF @ML    <> 600 RETURN 4;
    IF @s < @MaxStat OR @a < @MaxStat OR @v < @MaxStat OR @e < @MaxStat RETURN 5;
    IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_BaseStats WHERE Familia=(@Class/16)*16) RETURN 6;

    BEGIN TRAN;
      -- atributos de volta ao inicial da FAMILIA da classe + zera pontos e resets normais
      UPDATE c SET c.cLevel=1, c.LevelUpPoint=0, c.ResetCount=0,
                   c.Strength=b.Strength, c.Dexterity=b.Dexterity,
                   c.Vitality=b.Vitality, c.Energy=b.Energy, c.Leadership=b.Leadership,
                   c.SupremeResetCount=c.SupremeResetCount+1
        FROM dbo.[Character] c JOIN dbo.MuChila_BaseStats b ON b.Familia=(c.Class/16)*16
       WHERE c.Name=@Name;

      -- master: nivel, exp, pontos e a arvore de skills zerados
      UPDATE dbo.MasterSkillTree
         SET MasterLevel=0, MasterExperience=0, MasterPoint=0, MasterSkill=0x
       WHERE Name=@Name;

      -- recompensa em creditos do site
      IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_Creditos WHERE AccountID=@Acc)
          INSERT dbo.MuChila_Creditos(AccountID,Creditos) VALUES(@Acc,0);
      UPDATE dbo.MuChila_Creditos SET Creditos=Creditos+@Coins WHERE AccountID=@Acc;
    COMMIT TRAN;
    RETURN 0;
END;
GO

/* 4a) RESET NORMAL exato do requisito A (opcional, pelo site/offline), com a formula reset*300.
   O /reset do JOGO continua funcionando, mas da pontos fixos; use este se quiser os pontos
   escalaveis (1o reset=300, 2o=600, ...). Requisito: Level==400.
   Acao: Level->1, atributos->base da familia, LevelUpPoint = (novo ResetCount)*300, ResetCount++.
   Retorno: 0=ok; 1=inexistente; 2=conta online; 3=Level!=400; 6=classe sem base cadastrada. */
IF OBJECT_ID('dbo.MuChila_Reset') IS NOT NULL DROP PROCEDURE dbo.MuChila_Reset;
GO
CREATE PROCEDURE dbo.MuChila_Reset
    @Name varchar(10),
    @PontosPorReset int = 300
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Acc varchar(10), @Class tinyint, @Level int, @Novo int;
    SELECT @Acc=AccountID, @Class=Class, @Level=cLevel FROM dbo.[Character] WHERE Name=@Name;
    IF @Acc IS NULL RETURN 1;
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id=@Acc AND ConnectStat=1) RETURN 2;
    IF @Level <> 400 RETURN 3;
    IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_BaseStats WHERE Familia=(@Class/16)*16) RETURN 6;

    BEGIN TRAN;
      UPDATE c SET c.cLevel=1,
                   c.ResetCount=c.ResetCount+1,
                   c.LevelUpPoint=(c.ResetCount+1)*@PontosPorReset,   -- pontos escalaveis: N*300
                   c.Strength=b.Strength, c.Dexterity=b.Dexterity,
                   c.Vitality=b.Vitality, c.Energy=b.Energy, c.Leadership=b.Leadership
        FROM dbo.[Character] c JOIN dbo.MuChila_BaseStats b ON b.Familia=(c.Class/16)*16
       WHERE c.Name=@Name;
    COMMIT TRAN;
    RETURN 0;
END;
GO

/* 4a) Skills master na lista de habilidades (issue #27): o Master Reset limpava a arvore master
   (MasterSkill) mas os poderes aprendidos continuavam na MagicList do personagem.
   MuChila_MasterSkillOriginal: habilidade da arvore master -> habilidade comum que ela substitui
   (ex.: 330 Twisting Slash Improved -> 41 Twisting Slash); NULL = passiva/sem original (so sai da lista).
   Gerada de Data\Skill\MasterSkillTree.txt com a mesma regra do painel (Accounts.ResetMagicList):
   coluna ReplaceSkill seguida em cadeia ate uma habilidade que nao e da arvore master. */
IF OBJECT_ID('dbo.MuChila_MasterSkillOriginal') IS NOT NULL DROP TABLE dbo.MuChila_MasterSkillOriginal;
CREATE TABLE dbo.MuChila_MasterSkillOriginal (Skill int NOT NULL PRIMARY KEY, Original int NULL);
GO
INSERT dbo.MuChila_MasterSkillOriginal (Skill, Original) VALUES
    (300, NULL),
    (301, NULL),
    (302, NULL),
    (303, NULL),
    (305, NULL),
    (306, NULL),
    (307, NULL),
    (309, NULL),
    (746, NULL),
    (310, NULL),
    (312, NULL),
    (313, NULL),
    (315, NULL),
    (316, NULL),
    (317, NULL),
    (318, NULL),
    (319, NULL),
    (320, NULL),
    (375, NULL),
    (634, NULL),
    (377, NULL),
    (637, NULL),
    (636, NULL),
    (325, NULL),
    (378, 5),
    (379, 3),
    (380, 233),
    (381, 14),
    (382, 13),
    (383, 233),
    (385, 9),
    (386, NULL),
    (334, NULL),
    (387, 38),
    (388, 10),
    (338, NULL),
    (389, 7),
    (390, 2),
    (341, NULL),
    (391, 39),
    (392, 40),
    (743, NULL),
    (495, NULL),
    (642, NULL),
    (497, NULL),
    (347, NULL),
    (397, NULL),
    (398, NULL),
    (399, NULL),
    (400, NULL),
    (401, NULL),
    (402, NULL),
    (403, 16),
    (357, NULL),
    (358, NULL),
    (359, NULL),
    (404, 16),
    (405, NULL),
    (362, NULL),
    (406, 16),
    (407, NULL),
    (366, NULL),
    (367, NULL),
    (368, NULL),
    (639, NULL),
    (369, NULL),
    (641, NULL),
    (372, NULL),
    (370, NULL),
    (640, NULL),
    (371, NULL),
    (322, NULL),
    (624, NULL),
    (623, NULL),
    (324, NULL),
    (626, NULL),
    (625, NULL),
    (326, 22),
    (327, 23),
    (328, 19),
    (329, 20),
    (330, 41),
    (331, 42),
    (335, NULL),
    (336, 43),
    (337, 232),
    (339, 43),
    (345, NULL),
    (344, NULL),
    (631, NULL),
    (346, NULL),
    (348, NULL),
    (349, NULL),
    (350, NULL),
    (351, NULL),
    (352, NULL),
    (353, NULL),
    (354, NULL),
    (355, NULL),
    (356, 48),
    (360, 48),
    (361, NULL),
    (363, 48),
    (364, NULL),
    (628, NULL),
    (630, NULL),
    (629, NULL),
    (410, NULL),
    (643, NULL),
    (412, NULL),
    (646, NULL),
    (645, NULL),
    (413, 26),
    (414, 24),
    (415, NULL),
    (416, 52),
    (417, 27),
    (418, 24),
    (419, NULL),
    (420, 28),
    (421, NULL),
    (422, 28),
    (423, 27),
    (424, 51),
    (425, NULL),
    (411, 235),
    (426, NULL),
    (428, NULL),
    (429, NULL),
    (430, NULL),
    (427, NULL),
    (432, NULL),
    (651, NULL),
    (433, NULL),
    (434, NULL),
    (652, NULL),
    (435, NULL),
    (436, NULL),
    (437, NULL),
    (438, NULL),
    (439, NULL),
    (440, NULL),
    (441, 77),
    (442, NULL),
    (443, NULL),
    (648, NULL),
    (650, NULL),
    (649, NULL),
    (476, NULL),
    (664, NULL),
    (663, NULL),
    (478, NULL),
    (666, NULL),
    (665, NULL),
    (479, 22),
    (480, 3),
    (481, 41),
    (482, 56),
    (483, 5),
    (484, 13),
    (485, NULL),
    (486, 14),
    (487, 9),
    (488, NULL),
    (490, 55),
    (489, 7),
    (492, 236),
    (493, 55),
    (496, 237),
    (668, NULL),
    (669, NULL),
    (506, NULL),
    (505, NULL),
    (670, NULL),
    (507, NULL),
    (673, NULL),
    (672, NULL),
    (508, 61),
    (509, 60),
    (510, NULL),
    (511, 64),
    (512, 62),
    (513, NULL),
    (515, 64),
    (517, 64),
    (518, 78),
    (519, 65),
    (520, 78),
    (522, 64),
    (523, 238),
    (526, NULL),
    (527, NULL),
    (528, NULL),
    (529, NULL),
    (530, NULL),
    (531, NULL),
    (532, NULL),
    (533, NULL),
    (534, NULL),
    (538, NULL),
    (535, NULL),
    (539, NULL),
    (536, NULL),
    (675, NULL),
    (676, NULL),
    (446, NULL),
    (447, NULL),
    (656, NULL),
    (774, 223),
    (775, 224),
    (776, 225),
    (455, 215),
    (777, 225),
    (454, 219),
    (456, 230),
    (778, 225),
    (457, NULL),
    (458, 214),
    (459, 221),
    (460, 222),
    (772, 221),
    (773, 222),
    (461, NULL),
    (465, NULL),
    (466, NULL),
    (467, NULL),
    (468, NULL),
    (469, 218),
    (770, 289),
    (471, NULL),
    (470, 218),
    (771, 289),
    (473, NULL),
    (658, NULL),
    (660, NULL),
    (659, NULL),
    (578, NULL),
    (579, NULL),
    (580, NULL),
    (581, NULL),
    (583, NULL),
    (584, NULL),
    (585, NULL),
    (587, NULL),
    (588, NULL),
    (590, NULL),
    (591, NULL),
    (593, NULL),
    (594, NULL),
    (595, NULL),
    (596, NULL),
    (597, NULL),
    (598, NULL),
    (549, NULL),
    (550, NULL),
    (680, NULL),
    (599, NULL),
    (551, 260),
    (552, 261),
    (744, 270),
    (554, 260),
    (555, 261),
    (745, 270),
    (600, NULL),
    (557, NULL),
    (558, 262),
    (559, 263),
    (601, NULL),
    (560, 264),
    (602, NULL),
    (563, 263),
    (564, 265),
    (565, NULL),
    (567, NULL),
    (603, NULL),
    (568, NULL),
    (569, 268),
    (571, NULL),
    (572, 268),
    (573, 267),
    (604, NULL),
    (605, NULL),
    (606, NULL),
    (607, NULL),
    (608, NULL),
    (609, NULL),
    (610, NULL),
    (611, NULL),
    (612, NULL),
    (682, NULL),
    (613, NULL),
    (616, NULL),
    (614, NULL),
    (683, NULL),
    (615, NULL),
    (685, NULL),
    (713, NULL),
    (686, NULL),
    (716, NULL),
    (715, NULL),
    (687, 271),
    (688, 276),
    (689, NULL),
    (690, 271),
    (691, 276),
    (693, 273),
    (692, 274),
    (696, 279),
    (699, 277),
    (695, 274),
    (698, 279),
    (700, NULL),
    (701, NULL),
    (702, NULL),
    (703, 272),
    (704, NULL),
    (705, NULL),
    (706, 272),
    (707, NULL),
    (708, 278),
    (709, 278),
    (710, 278),
    (718, NULL),
    (711, NULL),
    (712, NULL),
    (719, NULL),
    (754, NULL),
    (758, NULL),
    (755, NULL),
    (759, NULL),
    (756, NULL),
    (748, 283),
    (749, 283),
    (765, 286),
    (768, 287),
    (766, 286),
    (769, 287),
    (750, 284),
    (751, 284),
    (752, NULL),
    (753, NULL),
    (761, NULL),
    (763, NULL),
    (762, NULL);
GO

/* MagicList: 3 bytes por habilidade (indice baixo, nivel, indice alto); FF 00 FF = vazio. Cada habilidade
   master sai; a comum que ela substituia volta no mesmo lugar, nivel 0, se o personagem ainda nao a tiver. */
IF OBJECT_ID('dbo.MuChila_LimparSkillsMaster') IS NOT NULL DROP FUNCTION dbo.MuChila_LimparSkillsMaster;
GO
CREATE FUNCTION dbo.MuChila_LimparSkillsMaster (@m varbinary(max))
RETURNS varbinary(max)
AS
BEGIN
    DECLARE @n int = ISNULL(DATALENGTH(@m), 0) / 3, @i int = 0, @out varbinary(max) = 0x;
    DECLARE @slot varbinary(3), @skill int, @orig int;
    DECLARE @tem TABLE (Skill int PRIMARY KEY);
    WHILE @i < @n
    BEGIN
        SET @slot = SUBSTRING(@m, @i * 3 + 1, 3);
        IF @slot <> 0xFF00FF
        BEGIN
            SET @skill = CAST(SUBSTRING(@slot, 1, 1) AS int) + CAST(SUBSTRING(@slot, 3, 1) AS int) * 256;
            IF NOT EXISTS (SELECT 1 FROM @tem WHERE Skill = @skill) INSERT @tem VALUES (@skill);
        END;
        SET @i += 1;
    END;
    SET @i = 0;
    WHILE @i < @n
    BEGIN
        SET @slot = SUBSTRING(@m, @i * 3 + 1, 3);
        IF @slot <> 0xFF00FF
        BEGIN
            SET @skill = CAST(SUBSTRING(@slot, 1, 1) AS int) + CAST(SUBSTRING(@slot, 3, 1) AS int) * 256;
            IF EXISTS (SELECT 1 FROM dbo.MuChila_MasterSkillOriginal WHERE Skill = @skill)
            BEGIN
                SET @orig = (SELECT Original FROM dbo.MuChila_MasterSkillOriginal WHERE Skill = @skill);
                DELETE @tem WHERE Skill = @skill;
                IF @orig IS NOT NULL AND NOT EXISTS (SELECT 1 FROM @tem WHERE Skill = @orig)
                BEGIN
                    SET @slot = CAST(CAST(@orig % 256 AS tinyint) AS binary(1)) + 0x00 + CAST(CAST(@orig / 256 AS tinyint) AS binary(1));
                    INSERT @tem VALUES (@orig);
                END
                ELSE SET @slot = 0xFF00FF;
            END;
        END;
        SET @out = @out + @slot;
        SET @i += 1;
    END;
    IF ISNULL(DATALENGTH(@m), 0) > @n * 3 SET @out = @out + SUBSTRING(@m, @n * 3 + 1, DATALENGTH(@m) - @n * 3);
    RETURN @out;
END;
GO

-- copia do que o Master Reset apaga (arvore, pontos, lista de habilidades), para desfazer se precisar
IF OBJECT_ID('dbo.MuChila_MasterResetBackup') IS NULL
    CREATE TABLE dbo.MuChila_MasterResetBackup (Id int IDENTITY PRIMARY KEY, Quando datetime NOT NULL DEFAULT (GETDATE()),
        Name varchar(10) NOT NULL, MasterLevel int, MasterExperience bigint, MasterPoint int, MasterSkill varbinary(max), MagicList varbinary(max));
GO

/* 4b) MASTER RESET exato do requisito B (feito pelo site, offline).
   Requisito: MasterLevel==600. Acao: MasterLevel->0 (recomeca o master), limpa a arvore de
   skills e os pontos master, tira os poderes master da lista de habilidades (4a), credita @Coins
   e incrementa MasterResetCount. Guarda o que apagou em MuChila_MasterResetBackup. NAO mexe no Level
   normal nem no ResetCount. (Obs.: o "Master Reset" do kit no jogo e um grand-reset por
   contagem de resets normais, coisa diferente; por isso fazemos este aqui, que bate o seu B.)
   Retorno: 0=ok; 1=inexistente; 2=conta online; 4=MasterLevel!=600. */
IF OBJECT_ID('dbo.MuChila_MasterReset') IS NOT NULL DROP PROCEDURE dbo.MuChila_MasterReset;
GO
CREATE PROCEDURE dbo.MuChila_MasterReset
    @Name varchar(10),
    @Coins int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Acc varchar(10), @ML int;
    SELECT @Acc=AccountID FROM dbo.[Character] WHERE Name=@Name;
    IF @Acc IS NULL RETURN 1;
    IF EXISTS (SELECT 1 FROM dbo.MEMB_STAT WHERE memb___id=@Acc AND ConnectStat=1) RETURN 2;
    SELECT @ML = MasterLevel FROM dbo.MasterSkillTree WHERE Name=@Name;
    IF ISNULL(@ML,0) <> 600 RETURN 4;

    DECLARE @novo int;
    BEGIN TRAN;
      INSERT dbo.MuChila_MasterResetBackup (Name, MasterLevel, MasterExperience, MasterPoint, MasterSkill, MagicList)
      SELECT c.Name, m.MasterLevel, m.MasterExperience, m.MasterPoint, m.MasterSkill, c.MagicList
        FROM dbo.[Character] c JOIN dbo.MasterSkillTree m ON m.Name = c.Name WHERE c.Name=@Name;
      UPDATE dbo.MasterSkillTree
         SET MasterLevel=0, MasterExperience=0, MasterPoint=0, MasterSkill=0x
       WHERE Name=@Name;
      UPDATE dbo.[Character]
         SET MasterResetCount=MasterResetCount+1, MagicList=dbo.MuChila_LimparSkillsMaster(MagicList)
       WHERE Name=@Name;
      SELECT @novo = MasterResetCount FROM dbo.[Character] WHERE Name=@Name;
      IF NOT EXISTS (SELECT 1 FROM dbo.MuChila_Creditos WHERE AccountID=@Acc)
          INSERT dbo.MuChila_Creditos(AccountID,Creditos) VALUES(@Acc,0);
      UPDATE dbo.MuChila_Creditos SET Creditos=Creditos+@Coins WHERE AccountID=@Acc;
      -- marca este master reset como JA PAGO, para o cron de reconciliacao nao creditar de novo
      MERGE dbo.MuChila_MasterResetPago AS d
      USING (SELECT @Name AS Name, @novo AS PagoAte) AS s ON d.Name=s.Name
      WHEN MATCHED THEN UPDATE SET PagoAte=s.PagoAte
      WHEN NOT MATCHED THEN INSERT(Name,PagoAte) VALUES(s.Name,s.PagoAte);
    COMMIT TRAN;
    RETURN 0;
END;
GO

/* 5) Recompensa do MASTER RESET (feito no jogo): o comando do kit incrementa
   MasterResetCount no banco. Esta procedure credita os que ainda nao foram pagos.
   Rode pelo cron do site (a cada minuto), so com a conta offline, igual ao Zen pendente. */
IF OBJECT_ID('dbo.MuChila_MasterResetPago') IS NULL
    CREATE TABLE dbo.MuChila_MasterResetPago (Name varchar(10) NOT NULL PRIMARY KEY, PagoAte int NOT NULL DEFAULT(0));
GO
IF OBJECT_ID('dbo.MuChila_CreditarMasterResets') IS NOT NULL DROP PROCEDURE dbo.MuChila_CreditarMasterResets;
GO
CREATE PROCEDURE dbo.MuChila_CreditarMasterResets
    @CoinsPorReset int   -- creditos por Master Reset (o site define)
AS
BEGIN
    SET NOCOUNT ON;
    -- personagens com master resets ainda nao pagos, cuja conta esta offline
    ;WITH pend AS (
        SELECT c.Name, c.AccountID, c.MasterResetCount,
               ISNULL(p.PagoAte,0) AS PagoAte
          FROM dbo.[Character] c
          LEFT JOIN dbo.MuChila_MasterResetPago p ON p.Name=c.Name
          JOIN dbo.MEMB_STAT s ON s.memb___id=c.AccountID AND s.ConnectStat=0
         WHERE c.MasterResetCount > ISNULL(p.PagoAte,0)
    )
    SELECT Name, AccountID, MasterResetCount, PagoAte,
           (MasterResetCount-PagoAte)*@CoinsPorReset AS Creditar
      INTO #pg FROM pend;

    BEGIN TRAN;
      INSERT dbo.MuChila_Creditos(AccountID,Creditos)
        SELECT DISTINCT AccountID,0 FROM #pg
         WHERE AccountID NOT IN (SELECT AccountID FROM dbo.MuChila_Creditos);
      UPDATE cr SET cr.Creditos = cr.Creditos + x.Total
        FROM dbo.MuChila_Creditos cr
        JOIN (SELECT AccountID, SUM(Creditar) Total FROM #pg GROUP BY AccountID) x ON x.AccountID=cr.AccountID;
      MERGE dbo.MuChila_MasterResetPago AS d
      USING (SELECT Name, MasterResetCount FROM #pg) AS s ON d.Name=s.Name
      WHEN MATCHED THEN UPDATE SET PagoAte=s.MasterResetCount
      WHEN NOT MATCHED THEN INSERT(Name,PagoAte) VALUES(s.Name,s.MasterResetCount);
    COMMIT TRAN;
    DROP TABLE #pg;
END;
GO

/* 6) Permissoes: o usuario do site precisa poder EXECUTAR as procedures (o acesso as tabelas
   dentro delas vem por ownership chaining, mesmo dono dbo). Ajuste o nome se o login for outro. */
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'muchila_site')
BEGIN
    GRANT EXECUTE ON dbo.MuChila_Reset             TO [muchila_site];
    GRANT EXECUTE ON dbo.MuChila_MasterReset       TO [muchila_site];
    GRANT EXECUTE ON dbo.MuChila_SupremeReset      TO [muchila_site];
    GRANT EXECUTE ON dbo.MuChila_CreditarMasterResets TO [muchila_site];
    GRANT SELECT, INSERT, UPDATE ON dbo.MuChila_Creditos TO [muchila_site];
    GRANT SELECT ON dbo.MuChila_BaseStats TO [muchila_site];
    PRINT 'permissoes concedidas a muchila_site';
END
ELSE PRINT 'AVISO: usuario muchila_site nao existe neste banco; ajuste o GRANT.';
GO

PRINT 'MuChila-Resets.sql aplicado. Confira dbo.MuChila_BaseStats antes de usar o Supreme em personagem real.';
GO

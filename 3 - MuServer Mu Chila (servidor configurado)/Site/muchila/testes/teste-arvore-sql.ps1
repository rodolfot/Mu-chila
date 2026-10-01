# Testa o Resetar Skill-Tree do site (dbo.MuChila_LimparArvoreMaster, muchila\sql\MUCHILA_SITE.sql) no tempdb, com tabelas simuladas:
# não mexe no MuOnlineS14 (cria as tabelas no tempdb, roda os 12 casos e apaga tudo no fim). Usa a função real do
# DB\1 - Querys\MuChila-Resets.sql. No servidor: .\teste-arvore-sql.ps1   Em outro PC com SQL Server: .\teste-arvore-sql.ps1 -Servidor .
param([string]$Servidor = '.\MUONLINE')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path   # a pasta do servidor (C:\MuServer)
$utf8 = New-Object Text.UTF8Encoding($false)
function Sql([string]$texto, [string]$nome) {
    $f = Join-Path $env:TEMP "arvore-$nome.sql"
    [IO.File]::WriteAllText($f, $texto, $utf8)
    $o = sqlcmd -S $Servidor -E -C -d tempdb -b -h -1 -W -f 65001 -i $f 2>&1
    $c = $LASTEXITCODE
    Remove-Item $f
    if ($c -ne 0) { throw "falhou ($nome): $($o -join ' | ')" }
    $o
}
$limpeza = @"
IF OBJECT_ID('dbo.MuChila_LimparArvoreMaster') IS NOT NULL DROP PROCEDURE dbo.MuChila_LimparArvoreMaster;
IF OBJECT_ID('dbo.MuChila_LimparSkillsMaster') IS NOT NULL DROP FUNCTION dbo.MuChila_LimparSkillsMaster;
IF OBJECT_ID('dbo.MuChila_MasterSkillOriginal') IS NOT NULL DROP TABLE dbo.MuChila_MasterSkillOriginal;
IF OBJECT_ID('dbo.MuChila_MasterResetBackup') IS NOT NULL DROP TABLE dbo.MuChila_MasterResetBackup;
IF OBJECT_ID('dbo.MasterSkillTree') IS NOT NULL DROP TABLE dbo.MasterSkillTree;
IF OBJECT_ID('dbo.[Character]') IS NOT NULL DROP TABLE dbo.[Character];
IF OBJECT_ID('dbo.MEMB_STAT') IS NOT NULL DROP TABLE dbo.MEMB_STAT;
IF OBJECT_ID('dbo.MUCHILA_CONTATO') IS NOT NULL DROP TABLE dbo.MUCHILA_CONTATO;
IF OBJECT_ID('dbo.MUCHILA_ZEN_COMPRAS') IS NOT NULL DROP TABLE dbo.MUCHILA_ZEN_COMPRAS;
"@
try {
    Sql $limpeza 'limpar' | Out-Null
    # tabelas simuladas (só as colunas que o procedimento usa) e a função real do MuChila-Resets.sql
    Sql @"
CREATE TABLE dbo.MEMB_STAT (memb___id varchar(10) PRIMARY KEY, ConnectStat tinyint, DisConnectTM datetime);
CREATE TABLE dbo.[Character] (Name varchar(10) PRIMARY KEY, AccountID varchar(10), Money int, MagicList varbinary(max));
CREATE TABLE dbo.MasterSkillTree (Name varchar(10) PRIMARY KEY, MasterLevel int, MasterExperience bigint, MasterPoint int, MasterSkill varbinary(max));
CREATE TABLE dbo.MuChila_MasterResetBackup (Id int IDENTITY PRIMARY KEY, Quando datetime NOT NULL DEFAULT (GETDATE()),
    Name varchar(10) NOT NULL, MasterLevel int, MasterExperience bigint, MasterPoint int, MasterSkill varbinary(max), MagicList varbinary(max));
CREATE TABLE dbo.MuChila_MasterSkillOriginal (Skill int PRIMARY KEY, Original int NULL);
INSERT dbo.MuChila_MasterSkillOriginal VALUES (330, 41), (752, NULL);
"@ 'tabelas' | Out-Null
    $resets = [IO.File]::ReadAllText("$repo\DB\1 - Querys\MuChila-Resets.sql", [Text.Encoding]::UTF8)
    $i = $resets.IndexOf('CREATE FUNCTION dbo.MuChila_LimparSkillsMaster'); $f = $resets.IndexOf("`nGO", $i)
    Sql $resets.Substring($i, $f - $i) 'funcao' | Out-Null
    Sql ([IO.File]::ReadAllText("$repo\Site\muchila\sql\MUCHILA_SITE.sql", [Text.Encoding]::UTF8)) 'site' | Out-Null

    # personagem: árvore com 2 aprendidas em 4 posições; MagicList com 330 (Twisting Slash Improved), 752 (sem original) e 41 de outro lugar não
    $saida = Sql @"
SET NOCOUNT ON;
INSERT dbo.[Character] VALUES ('Teste1', 'contaA', 500000, 0x4A0001 + 0xF00202 + 0xFF00FF), ('SemArvore', 'contaA', 9000000, 0x);
INSERT dbo.MasterSkillTree VALUES ('Teste1', 10, 0, 0, 0x2C0114 + 0xFF00FF + 0x4A0A01 + 0xFF00FF);
DECLARE @r int;
DECLARE @t table (n int IDENTITY, caso varchar(60), r int, esperado int);
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaA', 'Teste1', 1000000; INSERT @t (caso, r, esperado) VALUES ('zen insuficiente', @r, 4);
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaB', 'Teste1', 0;        INSERT @t (caso, r, esperado) VALUES ('personagem de outra conta', @r, 2);
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaA', 'SemArvore', 0;     INSERT @t (caso, r, esperado) VALUES ('sem arvore master', @r, 3);
INSERT dbo.MEMB_STAT VALUES ('contaA', 1, NULL);
UPDATE dbo.[Character] SET Money = 2000000 WHERE Name = 'Teste1';
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaA', 'Teste1', 1000000; INSERT @t (caso, r, esperado) VALUES ('conta no jogo', @r, 1);
UPDATE dbo.MEMB_STAT SET ConnectStat = 0, DisConnectTM = DATEADD(second, -10, GETDATE());
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaA', 'Teste1', 1000000; INSERT @t (caso, r, esperado) VALUES ('saiu ha 10 s', @r, 1);
UPDATE dbo.MEMB_STAT SET DisConnectTM = DATEADD(second, -60, GETDATE());
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaA', 'Teste1', 1000000; INSERT @t (caso, r, esperado) VALUES ('saiu ha 60 s: zera', @r, 0);
EXEC @r = dbo.MuChila_LimparArvoreMaster 'contaA', 'Teste1', 1000000; INSERT @t (caso, r, esperado) VALUES ('de novo: arvore ja vazia', @r, 5);
SELECT CASE WHEN r = esperado THEN 'OK    ' ELSE 'FALHA ' END + caso + ' (retorno ' + CAST(r AS varchar) + ')' FROM @t ORDER BY n;
SELECT CASE WHEN m.MasterSkill = 0xFF00FFFF00FFFF00FFFF00FF THEN 'OK    ' ELSE 'FALHA ' END + 'arvore: 4 posicoes FF00FF (' + CONVERT(varchar(100), m.MasterSkill, 2) + ')' FROM dbo.MasterSkillTree m WHERE Name = 'Teste1';
SELECT CASE WHEN MasterPoint = 10 THEN 'OK    ' ELSE 'FALHA ' END + 'pontos = master level (' + CAST(MasterPoint AS varchar) + ')' FROM dbo.MasterSkillTree WHERE Name = 'Teste1';
SELECT CASE WHEN Money = 1000000 THEN 'OK    ' ELSE 'FALHA ' END + 'zen cobrado uma vez (' + CAST(Money AS varchar) + ')' FROM dbo.[Character] WHERE Name = 'Teste1';
SELECT CASE WHEN MagicList = 0x290000 + 0xFF00FF + 0xFF00FF THEN 'OK    ' ELSE 'FALHA ' END + 'MagicList: 330 vira 41, 752 sai (' + CONVERT(varchar(100), MagicList, 2) + ')' FROM dbo.[Character] WHERE Name = 'Teste1';
SELECT CASE WHEN COUNT(*) = 1 AND MAX(CONVERT(varchar(100), MasterSkill, 2)) = '2C0114FF00FF4A0A01FF00FF' THEN 'OK    ' ELSE 'FALHA ' END + 'backup da arvore antiga (' + CAST(COUNT(*) AS varchar) + ' linha)' FROM dbo.MuChila_MasterResetBackup;
"@ 'casos'
    $saida | Where-Object { $_ -match '^(OK|FALHA)' }
    $falhas = @($saida | Where-Object { $_ -match '^FALHA' }).Count
    "resultado: $(@($saida | Where-Object { $_ -match '^OK' }).Count) ok, $falhas falha(s)"
} finally {
    Sql $limpeza 'limpar' | Out-Null
    $sobrou = (sqlcmd -S $Servidor -E -C -d tempdb -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.objects WHERE name IN ('MuChila_LimparArvoreMaster',
        'MuChila_LimparSkillsMaster', 'MuChila_MasterSkillOriginal', 'MuChila_MasterResetBackup', 'MasterSkillTree', 'Character', 'MEMB_STAT',
        'MUCHILA_CONTATO', 'MUCHILA_ZEN_COMPRAS')") -join ''
    "tempdb limpo: sobraram $sobrou objeto(s) do teste"
}
if ($falhas -or $sobrou -ne '0') { exit 1 }

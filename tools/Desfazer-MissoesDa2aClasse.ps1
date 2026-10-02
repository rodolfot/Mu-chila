# Issue #35 (01/10/2026): desfaz o ClasseInicial_MissoesDa2aClasse_existentes.sql (rodado na Etapa 6 da implantação de
# 01/10), que marcou como concluídas as missões da 2ª classe (0 e 1) dos personagens de 2ª classe que não as tinham feito
# e deu 10 pontos por missão. Pela decisão do dono, esses personagens voltam para a 1ª classe e fazem a missão.
# Quem foi marcado sai da comparação com um backup de antes (D:\MuServerBackup\DB\MuOnlineS14-*.bak, do Backup-Banco.ps1).
# O backup é restaurado como MuChila_Antes35 só para comparar e é apagado no fim. Sem -Backup, tenta do mais novo para o
# mais antigo e usa o primeiro que mostra alguém marcado (um backup de depois da marcação não mostra ninguém).
# Volta: classe -1 (Blade Knight → Dark Knight etc.), as missões 0 e 1 como estavam no backup e menos os pontos que o
# script deu (só os ainda não distribuídos). Quem passou para a 3ª classe depois não muda.
# Uso (com o servidor DESLIGADO para -Aplicar: o GameServer grava o personagem ao sair e desfaria a mudança):
#   .\Desfazer-MissoesDa2aClasse.ps1            só mostra
#   .\Desfazer-MissoesDa2aClasse.ps1 -Aplicar   muda (pula contas online; rode de novo depois que saírem)
param(
    [switch]$Aplicar,
    [string]$Backup,
    [string]$Instancia = '.\MUONLINE',
    [string]$Banco = 'MuOnlineS14',
    [string]$PastaBackups = 'D:\MuServerBackup\DB'
)
$ErrorActionPreference = 'Stop'
$antes = 'MuChila_Antes35'

function Sql([string]$consulta, [string]$bd = 'master') {
    $r = sqlcmd -S $Instancia -E -C -I -b -d $bd -h -1 -W -s '|' -Q "SET NOCOUNT ON; $consulta"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd falhou: $($r -join ' ')" }
    @($r | Where-Object { $_ -and $_.Trim() })
}
function ApagarCopia {
    Sql "IF DB_ID('$antes') IS NOT NULL BEGIN ALTER DATABASE [$antes] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$antes]; END" | Out-Null
}
function Restaurar([string]$arq) {
    ApagarCopia
    $pasta = @(Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))")[0].Trim().TrimEnd('\')
    $n = 0
    $mover = foreach ($linha in Sql "RESTORE FILELISTONLY FROM DISK = N'$arq'") {
        $c = $linha -split '\|'; $n++
        "MOVE N'$($c[0])' TO N'$pasta\${antes}_$n.$(if ($c[2] -eq 'L') { 'ldf' } else { 'mdf' })'"
    }
    Sql "RESTORE DATABASE [$antes] FROM DISK = N'$arq' WITH $($mover -join ', '), RECOVERY, REPLACE" | Out-Null
}

# personagens que no backup estavam na 2ª classe sem as missões 0 e 1 e agora, na mesma classe, têm as duas concluídas
$selecao = @"
FROM dbo.Character c
JOIN [$antes].dbo.Character o ON o.Name = c.Name AND o.AccountID = c.AccountID
CROSS APPLY (SELECT CAST(SUBSTRING(ISNULL(o.Quest, 0xFF), 1, 1) AS tinyint) AS B) ob
CROSS APPLY (SELECT ISNULL(c.Quest, CAST(REPLICATE(CHAR(255), 50) AS varbinary(50))) AS Q) q
CROSS APPLY (SELECT CAST(SUBSTRING(q.Q, 1, 1) AS tinyint) AS B) nb
CROSS APPLY (SELECT (CASE WHEN (ob.B & 3) <> 2 THEN 10 ELSE 0 END) + (CASE WHEN (ob.B & 12) <> 8 THEN 10 ELSE 0 END) AS Pontos,
                    CAST((nb.B & 240) | (ob.B & 15) AS tinyint) AS BDepois) p
WHERE o.Class IN (1, 17, 33, 81) AND c.Class = o.Class AND (ob.B & 15) <> 10 AND (nb.B & 15) = 10
"@
$online = "EXISTS (SELECT 1 FROM dbo.MEMB_STAT s WHERE s.memb___id = c.AccountID AND s.ConnectStat = 1)"

if (@(Sql "SELECT CASE WHEN OBJECT_ID('dbo.TR_MuChila_ClasseInicial') IS NULL THEN 0 ELSE 1 END" $Banco)[0] -eq '1') {
    Write-Warning 'o gatilho TR_MuChila_ClasseInicial ainda existe: rode também o DB\1 - Querys\MuChila-Issue35-VoltarPara1aClasse-aplicar.sql'
}
$candidatos = if ($Backup) { @(Get-Item $Backup) }
              else { @(Get-ChildItem $PastaBackups -Filter "$Banco-*.bak" | Sort-Object LastWriteTime -Descending | Select-Object -First 6) }
if (-not $candidatos) { throw "nenhum backup $Banco-*.bak em $PastaBackups (indique um com -Backup)" }

try {
    $usado = $null
    foreach ($f in $candidatos) {
        Restaurar $f.FullName
        $n = [int]@(Sql "SELECT COUNT(*) $selecao" $Banco)[0]
        "{0} ({1:dd/MM/yyyy HH:mm}): {2} personagem(ns) com as missões marcadas depois dele" -f $f.Name, $f.LastWriteTime, $n
        if ($n -gt 0) { $usado = $f; break }
    }
    if (-not $usado) { "`nNenhum backup mostra personagem marcado: nada a desfazer."; return }

    $lista = foreach ($linha in Sql @"
SELECT c.AccountID, c.Name, c.Class, c.Class - 1, c.cLevel, c.ResetCount, c.LevelUpPoint,
       CASE WHEN c.LevelUpPoint >= p.Pontos THEN c.LevelUpPoint - p.Pontos ELSE 0 END,
       CONVERT(varchar(4), CAST(ob.B AS binary(1)), 2), CONVERT(varchar(4), CAST(nb.B AS binary(1)), 2),
       CONVERT(varchar(4), CAST(p.BDepois AS binary(1)), 2), CASE WHEN $online THEN 1 ELSE 0 END
$selecao
ORDER BY c.AccountID, c.Name
"@ $Banco) {
        $c = $linha -split '\|'
        [pscustomobject]@{
            Conta = $c[0]; Personagem = $c[1]; Classe = "$($c[2]) -> $($c[3])"; Nivel = $c[4]; Resets = $c[5]
            Pontos = "$($c[6]) -> $($c[7])"; 'Missoes (backup/agora/depois)' = "$($c[8]) / $($c[9]) / $($c[10])"
            Situacao = if ($c[11] -eq '1') { 'ONLINE: pulado' } elseif ($Aplicar) { 'mudou' } else { 'muda' }
        }
    }
    "`nComparado com $($usado.Name):"
    $lista | Format-Table -AutoSize | Out-String -Width 220

    if (-not $Aplicar) { 'Nada foi mudado. Para aplicar (servidor desligado): .\Desfazer-MissoesDa2aClasse.ps1 -Aplicar'; return }
    $mudou = @(Sql @"
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE c
SET c.Class = c.Class - 1,
    c.LevelUpPoint = CASE WHEN c.LevelUpPoint >= p.Pontos THEN c.LevelUpPoint - p.Pontos ELSE 0 END,
    c.Quest = CAST(p.BDepois AS varbinary(1)) + SUBSTRING(q.Q, 2, 49)
$selecao AND NOT $online;
SELECT @@ROWCOUNT;
COMMIT;
"@ $Banco)[0]
    "$mudou personagem(ns) voltaram para a 1ª classe."
    if (@($lista | Where-Object Situacao -like 'ONLINE*').Count) { 'Os que estavam online ficaram de fora: rode de novo depois que saírem.' }
}
finally { ApagarCopia }

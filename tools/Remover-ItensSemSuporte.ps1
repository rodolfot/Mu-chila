# Remove do inventario, do inventario de evento e do bau os itens que o servidor MuDevs FREE nao suporta:
# caixas que nao abrem e cartoes de personagem (a criacao de classe nao depende deles aqui).
# So mexe em contas desconectadas ha pelo menos 1 minuto (o GameServer sobrescreveria o inventario de quem esta online).
# Os dados originais ficam salvos em C:\MuServer\DB\backup-caixas-<data>.csv (fora do repositorio) antes de qualquer alteracao.
param(
    [string[]]$Contas,          # vazio = todas as contas offline
    [int[]]$Codigos,            # vazio = lista padrao abaixo; informe para remover itens especificos (ex.: -Codigos 5757)
    [switch]$Simular
)

# Ruud/Earring/Gift/Mastery/Chicken Box etc. (secao 14): "Unknown box identifier" no GameServer
$codigos = @(7591, 7592, 7593, 7594, 7595, 7596, 7609, 7610, 7611, 7613, 7614, 7615, 7616, 7619, 7620, 7622, 7623, 7624, 7625,
             7259, 7337, 7449, 7655)   # Summoner, RageFighter, Grow Lancer e Rune Mage Character Card
if ($Codigos) { $codigos = $Codigos }

function Clear-CaixaSlots([byte[]]$blob) {
    $removidos = @()
    for ($s = 0; $s -lt [Math]::Floor($blob.Length / 16); $s++) {
        $o = $s * 16
        if ($blob[$o] -eq 0xFF -and $blob[$o + 7] -eq 0xFF -and $blob[$o + 9] -eq 0xFF) { continue }   # slot vazio
        $idx = $blob[$o] -bor (($blob[$o + 7] -band 0x80) -shl 1) -bor (($blob[$o + 9] -band 0xF0) -shl 5)
        if ($codigos -contains $idx) {
            for ($j = 0; $j -lt 16; $j++) { $blob[$o + $j] = 0xFF }
            $removidos += "slot $s = item $idx"
        }
    }
    , $removidos
}

$conn = New-Object System.Data.Odbc.OdbcConnection('DSN=MuOnlineS14')
$conn.Open()
$backup = "C:\MuServer\DB\backup-caixas-{0}.csv" -f (Get-Date -Format 'yyyyMMdd-HHmmss')
$linhasBackup = New-Object System.Collections.Generic.List[string]
$linhasBackup.Add('tabela;conta;nome;hex_original')
try {
    $q = $conn.CreateCommand()
    $q.CommandText = "SELECT memb___id FROM MEMB_STAT WHERE ConnectStat = 0 AND DATEDIFF(SECOND, DisConnectTM, GETDATE()) >= 60 " +
                     "UNION SELECT memb___id FROM MEMB_INFO WHERE memb___id NOT IN (SELECT memb___id FROM MEMB_STAT)"
    $r = $q.ExecuteReader(); $offline = @(); while ($r.Read()) { $offline += $r.GetString(0) }; $r.Close()
    $alvo = if ($Contas) { $Contas | Where-Object { $offline -contains $_ } } else { $offline }
    foreach ($c in $Contas) { if ($offline -notcontains $c) { Write-Host "PULADA (online ou desconectou ha menos de 1 min): $c" -ForegroundColor Yellow } }

    # Sel devolve (conta, chave, blob); Upd recebe (blob, chave). EventInventory so tem Name; warehouse e por conta.
    $fontes = @(
        @{ Tabela = 'Character'; Sel = 'SELECT AccountID, Name, Inventory FROM Character WHERE AccountID = ?'; Upd = 'UPDATE Character SET Inventory = ? WHERE Name = ?' },
        @{ Tabela = 'EventInventory'; Sel = 'SELECT c.AccountID, e.Name, e.Items FROM EventInventory e JOIN Character c ON c.Name = e.Name WHERE c.AccountID = ?'; Upd = 'UPDATE EventInventory SET Items = ? WHERE Name = ?' },
        @{ Tabela = 'warehouse'; Sel = 'SELECT AccountID, AccountID, Items FROM warehouse WHERE AccountID = ?'; Upd = 'UPDATE warehouse SET Items = ? WHERE AccountID = ?' }
    )
    foreach ($conta in $alvo) {
        foreach ($f in $fontes) {
            $sel = $conn.CreateCommand(); $sel.CommandText = $f.Sel; [void]$sel.Parameters.AddWithValue('a', $conta)
            $rd = $sel.ExecuteReader(); $rows = @()
            while ($rd.Read()) { if (-not $rd.IsDBNull(2)) { $rows += , @($rd.GetString(0), $rd.GetString(1), [byte[]]$rd[2]) } }
            $rd.Close()
            foreach ($row in $rows) {
                $orig = [BitConverter]::ToString($row[2]).Replace('-', '')
                $rem = Clear-CaixaSlots $row[2]
                if (-not $rem.Count) { continue }
                Write-Host ("{0,-10} {1,-12} {2,-15} {3}" -f $conta, $row[1], $f.Tabela, ($rem -join ', '))
                if ($Simular) { continue }
                $linhasBackup.Add("$($f.Tabela);$conta;$($row[1]);$orig")
                $up = $conn.CreateCommand(); $up.CommandText = $f.Upd
                [void]$up.Parameters.Add('b', [System.Data.Odbc.OdbcType]::VarBinary, $row[2].Length); $up.Parameters[0].Value = $row[2]
                [void]$up.Parameters.AddWithValue('k', $row[1])
                [void]$up.ExecuteNonQuery()
            }
        }
    }
}
finally {
    $conn.Close()
    if (-not $Simular -and $linhasBackup.Count -gt 1) { [IO.File]::WriteAllLines($backup, $linhasBackup); Write-Host "Backup dos dados originais: $backup" }
}

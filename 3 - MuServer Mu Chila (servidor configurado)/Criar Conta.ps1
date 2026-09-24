# Cria uma conta de jogo no banco MuOnlineS14 (tabela MEMB_INFO) usando o ODBC "MuOnlineS14".
# Senha em texto puro: o JoinServer esta com MD5Encryption = 0.

function Read-Field([string]$prompt, [int]$max, [string]$default = '') {
    while ($true) {
        $label = if ($default) { "$prompt [$default]" } else { $prompt }
        $v = Read-Host $label
        if (-not $v) { $v = $default }
        if ($v -match "^[A-Za-z0-9]{4,$max}$") { return $v }
        Write-Host "  Use de 4 a $max letras/numeros, sem espacos ou simbolos." -ForegroundColor Yellow
    }
}

Write-Host "=== Criar conta - MU Season 14 ===" -ForegroundColor Cyan
$login = Read-Field 'Login' 10
$senha = Read-Field 'Senha' 10
$code  = Read-Host 'Codigo pessoal de 7 digitos (usado para apagar personagem) [1111111]'
if ($code -notmatch '^\d{7}$') { $code = '1111111' }

$conn = New-Object System.Data.Odbc.OdbcConnection('DSN=MuOnlineS14')
try {
    $conn.Open()
    $check = $conn.CreateCommand()
    $check.CommandText = 'SELECT COUNT(*) FROM MEMB_INFO WHERE memb___id = ?'
    [void]$check.Parameters.AddWithValue('id', $login)
    if ([int]$check.ExecuteScalar() -gt 0) { throw "A conta '$login' ja existe." }

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "INSERT INTO MEMB_INFO (memb___id, memb__pwd, memb_name, sno__numb, mail_addr, bloc_code, ctl1_code, appl_days) VALUES (?, ?, ?, ?, ?, '0', '0', GETDATE())"
    [void]$cmd.Parameters.AddWithValue('id', $login)
    [void]$cmd.Parameters.AddWithValue('pwd', $senha)
    [void]$cmd.Parameters.AddWithValue('name', $login)
    [void]$cmd.Parameters.AddWithValue('sno', "111111$code")   # o MU confere os ultimos 7 digitos
    [void]$cmd.Parameters.AddWithValue('mail', "$login@local")
    [void]$cmd.ExecuteNonQuery()
    Write-Host "`nConta criada: login '$login' / senha '$senha' / codigo pessoal $code" -ForegroundColor Green
}
catch {
    Write-Host "`nErro: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    $conn.Close()
}
Read-Host "`nEnter para fechar"

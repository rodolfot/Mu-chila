# Decifra o Logs\Error.log do cliente (XOR com chave de 16 bytes) e mostra as ultimas linhas.
# Util para ver "[Connect to Server] ip address = ..." e "Failed to connect".
param(
    [string]$Arquivo = (Join-Path $PSScriptRoot '..\2 - Cliente Season 14 Full\Logs\Error.log'),
    [int]$Linhas = 60
)

$chave = [byte[]](0x7C, 0xBD, 0x81, 0x9F, 0x3D, 0x93, 0xE2, 0x56, 0x2A, 0x73, 0xD2, 0x3E, 0xF2, 0x83, 0x95, 0xBF)

# O cliente mantem o arquivo aberto enquanto roda; abre com compartilhamento de leitura/escrita.
$fs = [IO.File]::Open($Arquivo, 'Open', 'Read', 'ReadWrite')
try {
    $dados = New-Object byte[] $fs.Length
    [void]$fs.Read($dados, 0, $dados.Length)
}
finally { $fs.Close() }

for ($i = 0; $i -lt $dados.Length; $i++) { $dados[$i] = $dados[$i] -bxor $chave[$i % 16] }
[Text.Encoding]::Latin1.GetString($dados) -split "`r?`n" |
    Where-Object { $_ -notmatch '^[-#\s]*$' } |
    Select-Object -Last $Linhas

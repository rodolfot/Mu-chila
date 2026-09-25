# Muda as permissoes (vender, largar, guardar...) de itens no cliente: Data\Local\{Eng,Por,Spn}\item_*.bmd.
#
# Formato do item_*.bmd deste cliente S14:
#   4 bytes = quantidade de itens; registros de 672 bytes (XOR FC CF AB, recomecando em cada registro);
#   codigo do item nos bytes 0-1 do registro; permissoes nos bytes 661-667 (7 flags 0/1);
#   4 bytes finais = soma de verificacao. O cliente recusa o arquivo se a soma nao bater ("File corrupted").
# Soma (lida do main.exe, funcao 0x50E0C2, chave 0xE2F1, somente sobre os registros):
#   res = chave << 9; para cada DWORD na posicao p: se (p/4 + chave) par -> res ^= dw, senao res += dw;
#   a cada 16 bytes: res ^= (chave + res) >> ((p/4) % 8 + 1)
#
# Exemplo (Frost Soul e Soul Anvil com as permissoes do Jewel of Bless):
#   .\tools\Liberar-ItemCliente.ps1 -Codigos 7617, 7618 -Permissoes 0111110
param(
    [Parameter(Mandatory)][int[]]$Codigos,
    [ValidatePattern('^[01]{7}$')][string]$Permissoes = '0111110',
    [string]$Cliente = (Join-Path $PSScriptRoot '..\2 - Cliente Season 14 Full'),
    [switch]$Simular
)
$ErrorActionPreference = 'Stop'
if (-not ('MuBmd' -as [type])) {
Add-Type -TypeDefinition @"
public static class MuBmd {
    public static uint CheckSum(byte[] b, int start, int length, ushort key) {
        uint res = (uint)key << 9;
        for (int p = 0; p <= length - 4; p += 4) {
            uint dw = System.BitConverter.ToUInt32(b, start + p);
            if (((p / 4) + key) % 2 == 0) res ^= dw; else res += dw;
            if (p % 16 == 0) res ^= (uint)((key + res) >> ((p / 4) % 8 + 1));
        }
        return res;
    }
}
"@ }
$chave = [uint16]0xE2F1; $xor = [byte[]](0xFC, 0xCF, 0xAB); $tam = 672; $posPerm = 661
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$arquivos = Get-ChildItem (Join-Path $Cliente 'Data\Local') -Recurse -File | Where-Object { $_.Name -match '^item_\w+\.bmd$' }
if (-not $arquivos) { throw "Nenhum item_*.bmd em $Cliente\Data\Local" }
foreach ($f in $arquivos) {
    $b = [IO.File]::ReadAllBytes($f.FullName)
    $n = [BitConverter]::ToInt32($b, 0)
    if ($b.Length -ne 4 + $n * $tam + 4) { throw "$($f.Name): tamanho inesperado ($($b.Length) bytes para $n registros)" }
    $corpo = $b.Length - 8
    if ([MuBmd]::CheckSum($b, 4, $corpo, $chave) -ne [BitConverter]::ToUInt32($b, $b.Length - 4)) { throw "$($f.Name): a soma atual nao bate com o algoritmo conhecido; nada foi alterado" }
    $faltam = [Collections.Generic.List[int]]$Codigos
    for ($r = 0; $r -lt $n; $r++) {
        $o = 4 + $r * $tam
        $cod = ($b[$o] -bxor $xor[0]) -bor (($b[$o + 1] -bxor $xor[1]) -shl 8)
        if ($Codigos -notcontains $cod) { continue }
        $antes = -join (0..6 | ForEach-Object { $b[$o + $posPerm + $_] -bxor $xor[($posPerm + $_) % 3] })
        for ($i = 0; $i -lt 7; $i++) { $b[$o + $posPerm + $i] = [byte]([int][string]$Permissoes[$i] -bxor $xor[($posPerm + $i) % 3]) }
        "{0}: item {1} ({2},{3}) permissoes {4} -> {5}" -f $f.Name, $cod, [Math]::Floor($cod / 512), ($cod % 512), $antes, $Permissoes
        [void]$faltam.Remove($cod)
    }
    if ($faltam.Count) { throw "$($f.Name): codigos nao encontrados: $($faltam -join ', '); nada foi alterado" }
    [BitConverter]::GetBytes([MuBmd]::CheckSum($b, 4, $corpo, $chave)).CopyTo($b, $b.Length - 4)
    if ($Simular) { "$($f.Name): SIMULACAO, nada gravado"; continue }
    Copy-Item $f.FullName "$($f.FullName).bak-$stamp"
    [IO.File]::WriteAllBytes($f.FullName, $b)
    $v = [IO.File]::ReadAllBytes($f.FullName)
    "{0}: gravado; soma recalculada {1}" -f $f.Name, $(if ([MuBmd]::CheckSum($v, 4, $corpo, $chave) -eq [BitConverter]::ToUInt32($v, $v.Length - 4)) { 'OK' } else { 'ERRADA' })
}

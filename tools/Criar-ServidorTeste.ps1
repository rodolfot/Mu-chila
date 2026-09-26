# Monta (ou refaz) o GameServer de TESTES em C:\MuServerTeste: cópia do GameServer e da pasta Data, isolada dos servidores reais.
# Serve para validar formatos novos (BonusManager, lojas, caixas...) antes de mexer em C:\MuServer: em 26/09/2026 uma linha de
# teste no BonusManager.dat derrubou os dois GameServers reais na recarga.
#   - código 39, porta 55939, nome "Mu Chila Teste";
#   - DataServer, JoinServer e ConnectServer apontam para a porta 55999 (não há nada lá): ninguém entra, ele não aparece na lista,
#     não grava no banco e, se cair, não leva nada junto;
#   - lê a própria cópia da Data (o GameServer procura ..\Data), então mudanças de teste não chegam aos servidores reais.
# Uso: .\Criar-ServidorTeste.ps1 [-Refazer]   (depois: & 'C:\MuServerTeste\GameServerTeste\Game Server S14.exe', na pasta dele)
#      Para testar: altere o arquivo em C:\MuServerTeste\Data e use .\Recarregar-Teste.ps1 -Item <Event|Shop|...>
param([switch]$Refazer)
$ErrorActionPreference = 'Stop'
$raiz = 'C:\MuServerTeste'
if ((Test-Path $raiz) -and -not $Refazer) { throw "$raiz já existe (use -Refazer para copiar de novo a Data e o GameServer)" }
if (Get-Process 'Game Server S14' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$raiz\*" }) { throw 'Feche o GameServer de testes antes.' }

robocopy 'C:\MuServer\Data' "$raiz\Data" /MIR /COPY:DAT /R:1 /W:1 /NFL /NDL /NP /XF '*.bak-*' | Out-Null
robocopy 'C:\MuServer\GameServer' "$raiz\GameServerTeste" /MIR /COPY:DAT /R:1 /W:1 /NFL /NDL /NP /XD LOG CONNECT_LOG HACK_LOG /XF '*.dmp' '*.bak-*' | Out-Null
foreach ($p in 'LOG', 'CONNECT_LOG', 'HACK_LOG') { New-Item -ItemType Directory "$raiz\GameServerTeste\$p" -Force | Out-Null }

$enc = [Text.Encoding]::GetEncoding(1252)
$common = "$raiz\GameServerTeste\DATA\GameServerInfo - Common.dat"
$t = [IO.File]::ReadAllText($common, $enc)
$valores = [ordered]@{ ServerName = 'Mu Chila Teste'; ServerCode = '39'; ServerPort = '55939'; IsArcaWarServer = '0'
                       DataServerPort = '55999'; JoinServerPort = '55999'; ConnectServerPort = '55999' }
foreach ($k in $valores.Keys) {
    $novo = [regex]::Replace($t, "(?m)^($k\s*=\s*)[^\r\n]*", { param($m) $m.Groups[1].Value + $valores[$k] })
    if ($novo -eq $t -and $t -notmatch "(?m)^$k\s*=\s*$([regex]::Escape($valores[$k]))\s*$") { throw "$k não encontrado em $common" }
    $t = $novo
}
[IO.File]::WriteAllText($common, $t, $enc)

# o 39 hospeda todos os mapas (só na cópia de testes)
$msi = "$raiz\Data\MapServerInfo.dat"
$m = [IO.File]::ReadAllText($msi, $enc)
if ($m -notmatch '(?m)^39\s') {
    $m = [regex]::Replace($m, '(?m)^(19\s+0\s+0\s+\S+\s+55919[^\r\n]*)(\r?\n)', { param($x) $x.Groups[1].Value + $x.Groups[2].Value + "39`t       0`t        1`t     S26.139.39.123`t       55939" + $x.Groups[2].Value }, 1)
    if ($m -notmatch '(?m)^39\s') { throw 'Não consegui incluir o servidor 39 no MapServerInfo.dat de testes' }
    [IO.File]::WriteAllText($msi, $m, $enc)
}
"Servidor de testes pronto em $raiz (código 39, isolado). Ligue: Start-Process '$raiz\GameServerTeste\Game Server S14.exe' -WorkingDirectory '$raiz\GameServerTeste'"

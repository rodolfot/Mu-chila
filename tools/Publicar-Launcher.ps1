# Publica a atualização do cliente para o Launcher (issue #23).
#   1) compila tools\MuChilaLauncher\MuChilaLauncher.cs (csc do .NET Framework 4.x, que todo Windows 10/11 tem);
#   2) espelha a pasta do cliente do repositório em C:\MuServer\Cliente para amigos\launcher\files
#      (servida pelo Apache em /arquivos/launcher/files/), sem os arquivos do jogador (option.ini, Logs, Temp...);
#   3) gera launcher\manifest.txt (SHA-1 + tamanho + caminho). Usa um cache para só recalcular o que mudou.
# Rode sempre que mudar algo no cliente (ex.: loja de cash, item.bmd, serverlist). O jogador só abre o launcher.
param(
    [string]$Cliente = (Join-Path $PSScriptRoot '..\2 - Cliente Season 14 Full'),
    [string]$Destino = 'C:\MuServer\Cliente para amigos\launcher'
)
$ErrorActionPreference = 'Stop'
$Cliente = (Resolve-Path $Cliente).Path

# 1) compila
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = Join-Path $PSScriptRoot 'MuChilaLauncher\MuChilaLauncher.cs'
$exe = Join-Path $PSScriptRoot 'MuChilaLauncher\MuChilaLauncher.exe'
& $csc /nologo /target:winexe /optimize+ /codepage:65001 /out:$exe /r:System.Windows.Forms.dll /r:System.Drawing.dll $src
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o launcher' }
"compilado: $exe"

# 2) espelha os arquivos do cliente (arquivos do jogador ficam de fora e nunca são sobrescritos)
$files = Join-Path $Destino 'files'
New-Item -ItemType Directory -Force $files | Out-Null
robocopy $Cliente $files /MIR /R:1 /W:1 /NFL /NDL /NP /NJH /NJS `
    /XD Logs Temp ScreenShots `
    /XF option.ini desktop.ini '*.log' '*.bak-*' '*.download' launcher-cache.txt launcher.ini launcher-update.bat MuChilaLauncher.exe | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy falhou ($LASTEXITCODE)" }
Copy-Item $exe (Join-Path $files 'MuChilaLauncher.exe') -Force
Copy-Item $exe (Join-Path (Split-Path $Destino) 'MuChilaLauncher.exe') -Force   # download direto na página
"arquivos espelhados em $files"

# 3) manifesto (com cache: tamanho + data -> hash)
$cacheFile = Join-Path $Destino 'manifest-cache.txt'
$cache = @{}
if (Test-Path $cacheFile) { foreach ($l in [IO.File]::ReadAllLines($cacheFile)) { $c = $l.Split("`t"); if ($c.Count -eq 4) { $cache[$c[0]] = $c } } }
$sha = [Security.Cryptography.SHA1]::Create()
$linhas = New-Object System.Collections.Generic.List[string]
$novoCache = New-Object System.Collections.Generic.List[string]
$total = 0L; $recalculados = 0
$base = $files.TrimEnd('\') + '\'
foreach ($f in Get-ChildItem $files -Recurse -File) {
    $rel = $f.FullName.Substring($base.Length).Replace('\', '/')
    $ticks = $f.LastWriteTimeUtc.Ticks.ToString()
    $c = $cache[$rel]
    if ($c -and $c[1] -eq $f.Length.ToString() -and $c[2] -eq $ticks) { $hash = $c[3] }
    else {
        $s = [IO.File]::OpenRead($f.FullName)
        try { $hash = -join ($sha.ComputeHash($s) | ForEach-Object { $_.ToString('x2') }) } finally { $s.Dispose() }
        $recalculados++
    }
    $linhas.Add("$hash`t$($f.Length)`t$rel")
    $novoCache.Add("$rel`t$($f.Length)`t$ticks`t$hash")
    $total += $f.Length
}
$versao = Get-Date -Format 'yyyy-MM-dd HH:mm'
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $Destino 'manifest.txt'), "MUCHILA-MANIFEST 1`nversao=$versao`n" + ($linhas -join "`n") + "`n", $utf8)
[IO.File]::WriteAllLines($cacheFile, $novoCache, $utf8)
"manifesto: $($linhas.Count) arquivos, $([math]::Round($total/1GB,2)) GB, $recalculados hash(es) recalculado(s), versao $versao"

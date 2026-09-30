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
$dir = Join-Path $PSScriptRoot 'MuChilaLauncher'
$exe = Join-Path $dir 'MuChilaLauncher.exe'
# lógica (MuChilaLauncher.cs) + janela (LauncherUi.cs); artes do MU embutidas como recurso e ícone do Dark Knight
& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$exe" "/win32icon:$dir\icone.ico" `
    "/resource:$dir\arte1.jpg,arte1.jpg" "/resource:$dir\arte2.jpg,arte2.jpg" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll `
    "$dir\MuChilaLauncher.cs" "$dir\LauncherUi.cs" "$dir\Diagnostico.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o launcher' }
"compilado: $exe"

# 1b) programas obrigatórios do Windows (Diagnostico.cs): o launcher baixa primeiro daqui (<ServerUrl>/requisitos/) e só
#     depois da Microsoft. Baixa uma vez; se a Microsoft falhar, o launcher do jogador tenta direto nela e mostra os links.
$req = Join-Path $Destino 'requisitos'
New-Item -ItemType Directory -Force $req | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
foreach ($r in @(
    @{ Nome = 'vcredist_x86_2013.exe';      Url = 'https://download.visualstudio.microsoft.com/download/pr/10912113/5da66ddebb0ad32ebd4b922fd82e8e25/vcredist_x86.exe' },
    @{ Nome = 'directx_Jun2010_redist.exe'; Url = 'https://download.microsoft.com/download/8/4/A/84A35BF1-DAFE-4AE8-82AF-AD2AE20B6B14/directx_Jun2010_redist.exe' })) {
    $f = Join-Path $req $r.Nome
    if (Test-Path $f) { continue }
    try { (New-Object Net.WebClient).DownloadFile($r.Url, "$f.download"); Move-Item "$f.download" $f -Force; "requisito baixado: $($r.Nome)" }
    catch { Remove-Item "$f.download" -ErrorAction SilentlyContinue; Write-Warning "não consegui baixar $($r.Nome): $($_.Exception.Message)" }
}

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

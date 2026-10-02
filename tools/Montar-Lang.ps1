# Monta o Data\Lang.mpr do cliente com os textos editados em tools\Lang (issue #41: jogo em português).
# O Lang.mpr guarda os textos do jogo por idioma (por\Text(por).txt, por\MUQuest(por).txt, eng\..., spn\...): ZIP com senha,
# XOR por cima e uma soma no fim que o jogo confere (ver tools\MuChilaAdmin.Core\Jogo\LangPack.cs). Cada arquivo de tools\Lang
# entra no lugar do mesmo caminho do pacote; o resto fica como estava. Os textos são Windows-1252 (acentos ok).
#
# Para editar um texto que ainda não está em tools\Lang: extraia o pacote, copie o arquivo para tools\Lang\<idioma>\ e edite:
#   MuChilaAdmin.exe --lang-extrair "<cliente>\Data\Lang.mpr" C:\temp\lang C:\temp\lang.txt
# Depois: .\tools\Montar-Lang.ps1, teste o jogo pela pasta do cliente e mande aos jogadores com o Publicar-Launcher.ps1.
# Uso: .\Montar-Lang.ps1 [-Cliente <pasta do cliente>]
param([string]$Cliente = (Join-Path $PSScriptRoot '..\2 - Cliente Season 14 Full'))
$ErrorActionPreference = 'Stop'
$mpr = (Resolve-Path (Join-Path $Cliente 'Data\Lang.mpr')).Path
$textos = Join-Path $PSScriptRoot 'Lang'
$saida = Join-Path $env:TEMP 'montar-lang.txt'
$publicado = 'C:\MuServer\MuChilaAdmin\MuChilaAdmin.exe'
$args2 = @('--lang-montar', $mpr, $textos, $mpr, $saida)
if (Test-Path $publicado) { $p = Start-Process $publicado -ArgumentList ($args2 | ForEach-Object { "`"$_`"" }) -Wait -PassThru; $codigo = $p.ExitCode }
else { dotnet run --project (Join-Path $PSScriptRoot 'MuChilaAdmin\MuChilaAdmin.csproj') -v q -- @args2; $codigo = $LASTEXITCODE }
Get-Content $saida
if ($codigo -ne 0) { throw 'Lang.mpr não foi montado (o original ficou como estava)' }

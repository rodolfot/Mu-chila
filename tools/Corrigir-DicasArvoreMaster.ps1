# Corrige as dicas da árvore master em português (issue #36) e o nível máximo da habilidade 470 no cliente.
#
# O que estava errado (conferido em 01/10/2026 comparando servidor, cliente e as dicas em inglês):
#  1. 11 habilidades vão só até o nível 10 (servidor e cliente concordam: Data\Skill\MasterSkillTree.txt e
#     Data\Local\masterskilltreedata.bmd), mas a dica em português dizia "%d/20". O jogador via "10/20" e o jogo
#     recusava subir ("Você não pode aumentar mais níveis"). A 662 tinha o contrário ("/10" com máximo 20).
#  2. Dicas que somam pontos (ex.: Defense Increase: "Defense increases by %d") estavam em português como
#     porcentagem ("Defesa aumenta em %0.2f%%"): os +445 de defesa apareciam como "445.27%".
#  3. Descrições em português quebradas ou com um número a mais que o jogo não manda (392 a 395, 529, 574, 577),
#     e duas que perderam o "%" (415, 517).
#  4. Habilidade 470: o cliente deixava ir até 20, o servidor só até 10. O cliente passa a parar em 10 (o servidor manda).
#
# Só troca texto (os marcadores %d/%0.2f continuam os mesmos: o tipo do número que o jogo passa não muda) e o byte do
# nível máximo da 470. Confere o texto antigo antes de trocar (rodar de novo não muda nada) e recalcula a soma de
# verificação dos .bmd (chave 0x2BC1). Backup .bak-* ao lado de cada arquivo.
# Depois: .\tools\Publicar-Launcher.ps1 para os jogadores receberem.
# Uso: .\tools\Corrigir-DicasArvoreMaster.ps1 [-Cliente <pasta do cliente>] [-SoConferir]
param(
    [string]$Cliente = (Join-Path (Split-Path $PSScriptRoot -Parent) '2 - Cliente Season 14 Full'),
    [switch]$SoConferir
)
$ErrorActionPreference = 'Stop'
$chave = [byte[]](0xFC, 0xCF, 0xAB)
$cp = [Text.Encoding]::GetEncoding(1252)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

Add-Type -TypeDefinition @'
public static class SomaBmd {
    // GenerateCheckSum2 da Webzen sobre os bytes cifrados (masterskilltreedata e masterskilltooltip usam a chave 0x2BC1)
    public static uint Calc(byte[] buf, int len, uint key) {
        uint res = key << 9;
        for (int w = 0; w < len / 4; w++) {
            uint t = System.BitConverter.ToUInt32(buf, w * 4);
            if ((w + key) % 2 == 0) res ^= t; else res += t;
            if (w % 4 == 0) res ^= (key + res) >> (w % 8 + 1);
        }
        return res;
    }
    // registros de tamanho fixo, XOR recomeçando a cada registro
    public static void Xor(byte[] buf, int len, int registro, byte[] k) {
        for (int i = 0; i < len; i++) buf[i] ^= k[(i % registro) % 3];
    }
}
'@

function Abrir($arquivo, $registro) {
    $b = [IO.File]::ReadAllBytes($arquivo)
    $n = $b.Length - 4
    if ($n % $registro -ne 0) { throw "${arquivo}: tamanho inesperado ($($b.Length) bytes)" }
    if ([SomaBmd]::Calc($b, $n, 0x2BC1) -ne [BitConverter]::ToUInt32($b, $n)) { throw "${arquivo}: soma de verificação não confere (arquivo de outra versão?). Nada foi mudado." }
    [SomaBmd]::Xor($b, $n, $registro, $chave)
    return ,$b
}

function Gravar($arquivo, [byte[]]$b, $registro) {
    $n = $b.Length - 4
    [SomaBmd]::Xor($b, $n, $registro, $chave)
    [BitConverter]::GetBytes([SomaBmd]::Calc($b, $n, 0x2BC1)).CopyTo($b, $n)
    Copy-Item $arquivo "$arquivo.bak-$stamp"
    [IO.File]::WriteAllBytes($arquivo, $b)
    "gravado: $arquivo (backup .bak-$stamp)"
}

# ---------------------------------------------------------------- dicas em português
$dicas = Join-Path $Cliente 'Data\Local\Por\masterskilltooltip_por.bmd'
$R = 808                         # registro: id (4) + ícone? (2) + linha do rank (64) + descrição (256) + pontos (96) + requisitos
$campoRank = @(6, 64); $campoDesc = @(70, 256)
$t = Abrir $dicas $R
function Texto($o, $tam) { ($cp.GetString($t, $o, $tam) -split "`0")[0] }
function PorTexto($o, $tam, [string]$novo) {
    $bytes = $cp.GetBytes($novo)
    if ($bytes.Length -ge $tam) { throw "texto grande demais para o campo ($($bytes.Length) de $($tam - 1) bytes): $novo" }
    [Array]::Clear($t, $o, $tam); [Array]::Copy($bytes, 0, $t, $o, $bytes.Length)
}
$reg = @{}; for ($i = 0; $i -lt (($t.Length - 4) / $R); $i++) { $id = [BitConverter]::ToInt32($t, $i * $R); if ($id -ne 0) { $reg[$id] = $i * $R } }

$mudou = 0; $avisos = @()
function Trocar($id, $campo, [scriptblock]$regra, $porque) {
    if (-not $reg.ContainsKey($id)) { $script:avisos += "habilidade $id não está no arquivo"; return }
    $o = $reg[$id] + $campo[0]; $antes = Texto $o $campo[1]
    $depois = & $regra $antes
    if ($null -eq $depois) { $script:avisos += "habilidade ${id}: texto diferente do esperado, não mexi ($antes)"; return }
    if ($depois -ceq $antes) { return }   # já corrigida
    "  $id ($porque):`n     antes: $antes`n    depois: $depois"
    if (-not $SoConferir) { PorTexto $o $campo[1] $depois }
    $script:mudou++
}

"== Dicas em português ($dicas)"
# 1. máximo 10 de verdade (servidor e cliente), dica dizia 20 — e a 662 ao contrário
foreach ($id in 353, 400, 418, 425, 426, 427, 430, 432, 438, 468, 470, 695) {
    Trocar $id $campoRank { param($s) if ($s -match '%d/10') { $s } elseif ($s -match '%d/20') { $s -replace '%d/20', '%d/10' } else { $null } } 'máximo 10'
}
Trocar 662 $campoRank { param($s) if ($s -match '%d/20') { $s } elseif ($s -match '%d/10') { $s -replace '%d/10', '%d/20' } else { $null } } 'máximo 20'

# 2. somam pontos (em inglês: "increases by %d"), não porcentagem
foreach ($id in 309, 322, 375, 412, 447, 478, 549, 550) {
    Trocar $id $campoDesc { param($s) if ($s -match '%0\.2f%%') { $s -replace '%0\.2f%%', '%0.2f' } elseif ($s -match '%0\.2f') { $s } else { $null } } 'pontos, não %'
}

# 3. faltava o "%" (em inglês: "%d%%" e "%0.2f%%")
Trocar 415 $campoDesc { param($s) if ($s -match 'em %d%%\.') { $s } elseif ($s -match 'em %d\.$') { $s -replace 'em %d\.$', 'em %d%%.' } else { $null } } 'faltava o %'
Trocar 517 $campoDesc { param($s) if ($s -match 'em %d%%\.') { $s } elseif ($s -match 'em %d\.$') { $s -replace 'em %d\.$', 'em %d%%.' } else { $null } } 'faltava o %'

# 4. descrições quebradas ou com um número a mais (o jogo mandaria lixo no lugar do número que não existe)
$novas = [ordered]@{
    392 = @('Grau da Habilidade', "O dano da habilidade Nova aumenta em %d.");
    393 = @('Grau da Habilidade', "O alvo fica imóvel por 3 segundos com %0.2f%% de chance.");
    394 = @('Grau da Habilidade', "A habilidade Meteoro tem %0.2f%% de chance de atordoar o alvo por 2 segundos.");
    395 = @('Grau da Habilidade', "Aprende 'Aqua Blast'.#Causa dano no alvo, com chance de deixá-lo imóvel por 3 segundos. Recarga: 5 segundos.");
    529 = @('a velocidade de Ataque aumenta em %d', "Enquanto estiver equipando Cetros, o poder de ataque do Espírito Negro aumenta em %d.");
    574 = @('aumentam sua taxa de defesa em %d', "A cada %d pontos de vida, sua taxa de defesa aumenta em 1.");
    577 = @('diminuir a taxa de sucesso', "A habilidade Ignorar Defesa tem chance de aumentar sua taxa de sucesso de defesa em %0.2f%%.")
}
# (OrderedDictionary com chave número: $novas[392] seria a posição 392, por isso o GetEnumerator)
foreach ($par in $novas.GetEnumerator()) {
    $id = $par.Key; $marca = $par.Value[0]; $nova = $par.Value[1]
    Trocar $id $campoDesc { param($s) if ($s -ceq $nova) { $s } elseif ($s.Contains($marca)) { $nova } else { $null } }.GetNewClosure() 'descrição'
}
if ($mudou -gt 0 -and -not $SoConferir) { Gravar $dicas $t $R } elseif ($mudou -eq 0) { "  nada a mudar (já corrigido)" }

# ---------------------------------------------------------------- árvore do cliente: 470 até o nível 10
"`n== Árvore master do cliente (masterskilltreedata.bmd)"
$arvore = Join-Path $Cliente 'Data\Local\masterskilltreedata.bmd'
$a = Abrir $arvore 24
$n470 = 0
for ($o = 0; $o -lt $a.Length - 4; $o += 24) {
    if ([BitConverter]::ToInt32($a, $o + 16) -ne 470) { continue }
    if ($a[$o + 6] -eq 10) { continue }
    "  470: nível máximo $($a[$o + 6]) -> 10 (o servidor só deixa até 10)"
    $a[$o + 6] = 10; $n470++
}
if ($n470 -gt 0 -and -not $SoConferir) { Gravar $arvore $a 24 } elseif ($n470 -eq 0) { "  nada a mudar (já corrigido)" }

foreach ($x in $avisos) { Write-Warning $x }
if ($SoConferir) { "`n(-SoConferir: nada foi gravado)" } else { "`nPronto. Para os jogadores receberem: .\tools\Publicar-Launcher.ps1" }

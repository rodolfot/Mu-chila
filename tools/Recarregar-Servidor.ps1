# Aciona um item do menu "Reload" de um servidor em execucao, sem reinicia-lo.
# Exemplos:
#   .\Recarregar-Servidor.ps1 -Servidor GameServer -Item Event
#   .\Recarregar-Servidor.ps1 -Servidor CastleSiege -Item EventItemBag
#   .\Recarregar-Servidor.ps1 -Servidor ConnectServer -Item ServerList
param(
    [Parameter(Mandatory)][ValidateSet('GameServer', 'CastleSiege', 'ConnectServer')][string]$Servidor,
    [Parameter(Mandatory)][ValidateSet('CashShop', 'ChaosMix', 'Character', 'Command', 'Common', 'Custom', 'Event',
        'EventItemBag', 'Hack', 'Item', 'Monster', 'Move', 'Quest', 'Shop', 'Skill', 'Util', 'ServerList')][string]$Item
)

# IDs dos itens de menu dos executaveis MuDevs (lidos da propria janela dos servidores)
$gameServerIds = @{
    CashShop = 32776; ChaosMix = 32777; Character = 32778; Command = 32779; Common = 32780; Custom = 32781
    Event = 32782; EventItemBag = 32783; Hack = 32784; Item = 32785; Monster = 32786; Move = 32787
    Quest = 32788; Shop = 32789; Skill = 32790; Util = 32791
}
$processos = @{ GameServer = 'Game Server S14'; CastleSiege = 'Castle Siege Server'; ConnectServer = 'ConnectServer' }

if ($Servidor -eq 'ConnectServer') {
    if ($Item -ne 'ServerList') { throw 'O ConnectServer so recarrega a ServerList.' }
    $id = 32771
}
elseif ($Item -eq 'ServerList') { throw 'ServerList so existe no ConnectServer.' }
else { $id = $gameServerIds[$Item] }

Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class MuWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  // A janela aberta pelo launcher nao e a "janela principal" do processo; procura a que tem menu.
  public static IntPtr FindMenuWindow(int pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && GetMenu(h) != IntPtr.Zero) { found = h; return false; } return true; }, IntPtr.Zero);
    return found;
  }
}
"@

$proc = Get-Process -Name $processos[$Servidor] -ErrorAction Stop
$janela = [MuWin]::FindMenuWindow($proc.Id)
if ($janela -eq [IntPtr]::Zero) { throw "Janela do $Servidor nao encontrada." }
[void][MuWin]::PostMessage($janela, 0x0111, [IntPtr]$id, [IntPtr]::Zero)   # WM_COMMAND
Write-Host "Reload $Item enviado ao $Servidor. Confira a linha 'loaded successfully' no LOG do servidor."

# Recarrega um item SÓ no GameServer de testes (processos em C:\MuServerTeste, ver Criar-ServidorTeste.ps1). Nunca toca nos servidores reais.
param([Parameter(Mandatory)][ValidateSet('CashShop', 'Event', 'Shop', 'Common', 'Item', 'Monster', 'EventItemBag', 'Util', 'Move')][string]$Item)
$ids = @{ CashShop = 32776; Common = 32780; Event = 32782; EventItemBag = 32783; Item = 32785; Monster = 32786; Move = 32787; Shop = 32789; Util = 32791 }
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class MuWinTeste {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  public static IntPtr FindMenuWindow(int pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && GetMenu(h) != IntPtr.Zero) { found = h; return false; } return true; }, IntPtr.Zero);
    return found;
  }
}
"@
$alvos = @(Get-Process 'Game Server S14' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like 'C:\MuServerTeste\*' })
if (-not $alvos) { throw 'GameServer de testes não está rodando' }
foreach ($p in $alvos) {
    $h = [MuWinTeste]::FindMenuWindow($p.Id)
    if ($h -eq [IntPtr]::Zero) { throw "janela do teste (pid $($p.Id)) não encontrada" }
    [void][MuWinTeste]::PostMessage($h, 0x0111, [IntPtr]$ids[$Item], [IntPtr]::Zero)
    "Reload $Item enviado ao GameServer de testes (pid $($p.Id))"
}

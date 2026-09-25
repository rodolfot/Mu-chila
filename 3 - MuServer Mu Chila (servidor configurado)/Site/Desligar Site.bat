@echo off
rem Desliga o site do Mu Chila. So fecha o Apache de C:\MuServer\Site (o Apache do EDB PEM, porta 8080, nao e mexido).
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$p = Get-Process httpd -ErrorAction SilentlyContinue | Where-Object { $_.Path -like 'C:\MuServer\Site\*' };" ^
  "if (-not $p) { 'O site ja estava desligado.' } else { $p | Stop-Process -Force; 'Site desligado.' }"
timeout /t 3 >nul

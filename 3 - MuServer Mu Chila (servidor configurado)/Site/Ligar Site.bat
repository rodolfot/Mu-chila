@echo off
rem Liga o site do Mu Chila (Apache + PHP, porta 80) em segundo plano. Acesso: http://26.139.39.123 (Radmin) ou http://localhost
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "if (Get-Process httpd -ErrorAction SilentlyContinue | Where-Object { $_.Path -like 'C:\MuServer\Site\*' }) { 'O site ja esta ligado.'; exit }" ^
  "Start-Process 'C:\MuServer\Site\apache\bin\httpd.exe' -WorkingDirectory 'C:\MuServer\Site\apache' -WindowStyle Hidden;" ^
  "Start-Sleep 2;" ^
  "if (Get-Process httpd -ErrorAction SilentlyContinue | Where-Object { $_.Path -like 'C:\MuServer\Site\*' }) { 'Site ligado: http://localhost e http://26.139.39.123' } else { 'O site nao ligou; veja C:\MuServer\Site\logs\apache-erro.log' }"
timeout /t 4 >nul

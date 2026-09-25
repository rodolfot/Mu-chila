@echo off
title Mu Chila - Resolucao do jogo
echo.
echo  Mu Chila - Resolucao do jogo
echo  =============================
echo  Feche o jogo antes de continuar.
echo.
echo   1 = 1024 x 768        5 = 1600 x 900
echo   2 = 1280 x 720        6 = 1680 x 1050
echo   3 = 1280 x 800        7 = 1920 x 1080
echo   4 = 1440 x 900        8 = 800 x 600
echo.
choice /c 12345678 /n /m "Escolha a resolucao (1-8): "
set R=%errorlevel%
if %R%==1 set IDX=1
if %R%==2 set IDX=3
if %R%==3 set IDX=4
if %R%==4 set IDX=10
if %R%==5 set IDX=7
if %R%==6 set IDX=8
if %R%==7 set IDX=9
if %R%==8 set IDX=0
echo.
choice /c JT /n /m "J = janela, T = tela cheia: "
if %errorlevel%==1 (set FS=0) else (set FS=1)
reg add "HKCU\Software\Webzen\Mu\Config" /v DisplayDeviceModeIndex /t REG_DWORD /d %IDX% /f >nul
reg add "HKCU\Software\Webzen\Mu\Config" /v FullScreenMode /t REG_DWORD /d %FS% /f >nul
echo.
echo  Pronto. Abra o jogo de novo.
echo  A lista de resolucoes vem da placa de video: se a tela ficar diferente
echo  da escolhida, rode este arquivo de novo e tente a opcao vizinha.
echo.
pause

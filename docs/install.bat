@echo off
rem 操作性改善アプリを入れます（GitHub から本体を取ってきて、このパソコンに置き、起動します）
echo 操作性改善アプリを入れています。少しお待ちください...
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
"%PS%" -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = 3072; Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/kusaka-daichi/sousa-kakucho-app/stable/installer/install.ps1?t=' + [DateTime]::UtcNow.Ticks) -OutFile (Join-Path $env:TEMP 'sousa-kaizen-install.ps1')"
if errorlevel 1 goto ng
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\sousa-kaizen-install.ps1"
if errorlevel 1 exit /b 1
echo 入れました。アプリの画面が開くまで、少しお待ちください。
timeout /t 4 >nul
exit /b 0
:ng
echo.
echo ダウンロードできませんでした。インターネットにつながっているか確かめて、もう一度開いてください。
pause
exit /b 1

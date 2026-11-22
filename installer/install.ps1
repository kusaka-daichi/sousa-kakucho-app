# 操作性改善アプリのインストール（ダウンロードした「操作性改善アプリ_インストール.bat」から呼ばれる）
# ・GitHub の配る用の版（stable）から本体を取ってきて、%LOCALAPPDATA%\操作性改善アプリ\9_AI作業用 に置く
# ・アプリを起動する（デスクトップのアイコンと、パソコン起動時の自動起動はアプリが自分で作る）
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Net.ServicePointManager]::SecurityProtocol = 3072
$base  = 'https://raw.githubusercontent.com/kusaka-daichi/sousa-kakucho-app/stable/app/'
$dest  = Join-Path $env:LOCALAPPDATA '操作性改善アプリ\9_AI作業用'
$tmp   = Join-Path $env:TEMP ('sousa-kaizen-' + [guid]::NewGuid().ToString('N'))
$files = @('app.html', 'app.ico', 'app.ps1', 'version.txt', 'app.cs')
try {
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $wc = New-Object System.Net.WebClient
    $wc.Headers.Add('User-Agent', 'sousa-kaizen-installer')
    foreach ($f in $files) { $wc.DownloadFile($base + $f + '?t=' + [DateTime]::UtcNow.Ticks, (Join-Path $tmp $f)) }
    # 全部そろってから入れる（途中で失敗したら何も変えない）
    foreach ($f in $files) { Copy-Item -LiteralPath (Join-Path $tmp $f) -Destination (Join-Path $dest $f) -Force }
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    $ps  = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $app = Join-Path $dest 'app.ps1'
    Start-Process -FilePath $ps -WorkingDirectory $dest -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-WindowStyle', 'Hidden', '-File', ('"' + $app + '"'), '/open')
}
catch {
    [System.Windows.Forms.MessageBox]::Show("インストールできませんでした。`n`n" + $_.Exception.Message, '操作性改善アプリ') | Out-Null
    exit 1
}

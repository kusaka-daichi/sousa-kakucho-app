# 操作性拡張アプリのインストール
# ・このフォルダのファイルを、ふだん目に入らない場所（%LOCALAPPDATA%\操作性拡張アプリ）へ入れる
# ・アプリを起動する（デスクトップのアイコンと、パソコン起動時の自動起動はアプリが自分で作る）
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$src  = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Join-Path $env:LOCALAPPDATA '操作性拡張アプリ'
$dest = Join-Path $root '9_AI作業用'
try {
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    foreach ($f in @('app.html', 'app.ico', 'app.ps1', 'version.txt', 'app.cs')) {
        $to = Join-Path $dest $f
        Copy-Item -LiteralPath (Join-Path $src $f) -Destination $to -Force
        try { Unblock-File -LiteralPath $to } catch {}
    }
    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $app = Join-Path $dest 'app.ps1'
    Start-Process -FilePath $ps -WorkingDirectory $dest -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-WindowStyle', 'Hidden', '-File', ('"' + $app + '"'), '/open')
    [System.Windows.Forms.MessageBox]::Show("操作性拡張アプリを入れました。`n`n次からは、デスクトップの「操作性拡張アプリ」から開けます。`n新しい版が出たら、自動で更新されます。`n`nダウンロードしたファイルとこのフォルダは、消してかまいません。", '操作性拡張アプリ') | Out-Null
}
catch {
    [System.Windows.Forms.MessageBox]::Show("インストールできませんでした。`n`n" + $_.Exception.Message, '操作性拡張アプリ') | Out-Null
}

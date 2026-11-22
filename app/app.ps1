# 操作性改善アプリ 起動用（app.cs をその場で組み立てて動かす。exe は作らない）
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
$sys = Split-Path -Parent $MyInvocation.MyCommand.Path
try {
    $code = [IO.File]::ReadAllText((Join-Path $sys "app.cs"), [Text.Encoding]::UTF8)
    Add-Type -TypeDefinition $code -ReferencedAssemblies System.Windows.Forms, System.Drawing
    [SousaKaizen.Program]::Run($sys, [string[]]$args)
}
catch {
    $msg = $_.Exception.ToString()
    try { Add-Content -Path (Join-Path $sys "log.txt") -Value ((Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "  起動エラー: " + $msg) -Encoding UTF8 } catch {}
    [System.Windows.Forms.MessageBox]::Show($msg, "操作性改善アプリを起動できませんでした") | Out-Null
}

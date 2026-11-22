# 操作性改善アプリ 起動用（app.cs をその場で組み立てて動かす。exe は作らない）
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
$sys = Split-Path -Parent $MyInvocation.MyCommand.Path
# もう動いているときは、組み立てずにすぐ「画面を開いて」と伝えて終わる（待ち時間をなくす）
if (-not ($args -contains '/restart')) {
    $ev = $null
    try { $ev = [System.Threading.EventWaitHandle]::OpenExisting('Local\SousaKaizenApp_v1_open') } catch {}
    if ($ev -ne $null) {
        if ($args -contains '/open') {
            try {
                Add-Type -Namespace SousaKaizenStart -Name Fg -MemberDefinition '[DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);'
                [void][SousaKaizenStart.Fg]::AllowSetForegroundWindow(-1)
            } catch {}
            [void]$ev.Set()
        }
        exit
    }
}
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

// 操作性改善アプリ（自作版。一時期の名前：操作性拡張アプリ）
// ・AutoHotkey を使わず、Windows に最初から入っている部品（PowerShell と .NET）だけで動く
// ・app.ps1 がこのファイルをその場で組み立てて起動する（exe は作らない）
// ・機能：1 画面入れ替え／2 スクショ／3 後ろを触る／4 マニュアル作成。アプリ画面のスイッチでオン・オフ（最初は全部オフ）
// ・このファイル（app.cs）や app.html を上書きすると、2秒ほどで自動で新しい版に切り替わる
// ・Windows 標準の C#（古い版）で組み立てるので、新しい書き方（$"..." や ?. など）は使わない
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace SousaKaizen
{
    // ================================================================
    //  Windows の部品の呼び出し
    // ================================================================
    public static class W
    {
        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        public delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data);

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public int vkCode; public int scanCode; public int flags; public int time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public int mouseData; public int flags; public int time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }
        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT rcCaret;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, int threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);
        [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(int threadId, ref GUITHREADINFO info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rc);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc fn, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc fn, IntPtr data);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out int key, out byte alpha, out int flags);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, int key, byte alpha, int flags);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, int flags);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT pt);
        [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetMenuItemRect(IntPtr hwnd, IntPtr menu, int item, out RECT rc);
        [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(int threadId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ToUnicodeEx(int vk, int scan, byte[] state, StringBuilder buf, int size, int flags, IntPtr hkl);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, int flags, IntPtr extra);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern int GetDpiForSystem();
        [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, int affinity);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rc, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
        [DllImport("imm32.dll")] public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT { public int length, flags, showCmd; public POINT ptMin, ptMax; public RECT rcNormal; }
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, int flags);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);
        [DllImport("user32.dll")] public static extern IntPtr BeginDeferWindowPos(int n);
        [DllImport("user32.dll")] public static extern IntPtr DeferWindowPos(IntPtr h, IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, int flags);
        [DllImport("user32.dll")] public static extern bool EndDeferWindowPos(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr ctx);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int type, out int dpiX, out int dpiY);

        // 画面1つ分の情報
        public class Mon { public IntPtr h; public Rectangle Full, Work; public bool Primary; public int Dpi; }
        public static List<Mon> MonitorsEx()
        {
            List<Mon> list = new List<Mon>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data)
            {
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(hMon, ref mi))
                {
                    Mon m = new Mon();
                    m.h = hMon;
                    m.Full = Rectangle.FromLTRB(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                    m.Work = Rectangle.FromLTRB(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);
                    m.Primary = (mi.dwFlags & 1) != 0;
                    int dx = 96, dy;
                    try { GetDpiForMonitor(hMon, 0, out dx, out dy); } catch { }
                    m.Dpi = dx;
                    list.Add(m);
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }
        // ウィンドウの拡大率への対応（0=対応していない 1=全体で1つ 2=画面ごと -1=不明）
        public static int Awareness(IntPtr hwnd)
        {
            try { return GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(hwnd)); }
            catch { return -1; }
        }

        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bi, int usage, out IntPtr bits, IntPtr section, int offset);
        [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr prev);
        [DllImport("gdi32.dll")]
        public static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, int rop);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetPrivateProfileString(string sec, string key, string def, StringBuilder sb, int size, string file);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool WritePrivateProfileString(string sec, string key, string val, string file);

        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const int WS_VISIBLE = 0x10000000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;
        public const int LWA_ALPHA = 2;

        public static string ClassOf(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        public static string TitleOf(IntPtr hwnd)
        {
            int n = GetWindowTextLength(hwnd);
            if (n <= 0) return "";
            StringBuilder sb = new StringBuilder(n + 2);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        public static string ProcessNameOf(IntPtr hwnd)
        {
            try
            {
                int pid;
                GetWindowThreadProcessId(hwnd, out pid);
                return Process.GetProcessById(pid).ProcessName + ".exe";
            }
            catch { return ""; }
        }
        public static bool IsDesktopOrTaskbar(string cls)
        {
            return cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
        }
        // 見えている枠の範囲（ウィンドウのまわりの透明な縁や影を除く）
        public static RECT FrameRect(IntPtr hwnd)
        {
            RECT r;
            if (DwmGetWindowAttribute(hwnd, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                GetWindowRect(hwnd, out r);
            return r;
        }
        // 2つの画面で拡大率が違っても座標がずれないようにする（今の処理だけ）
        public static void PerMonitorDpi()
        {
            try { SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch { }
        }
        public static Rectangle VirtualScreen()
        {
            return new Rectangle(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        }
        // 動かしてよい普通のウィンドウかどうか
        public static bool IsNormalWindow(IntPtr hwnd)
        {
            try
            {
                if ((GetWindowLong(hwnd, GWL_STYLE) & WS_VISIBLE) == 0) return false;
                if ((GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return false;
                if (GetWindowTextLength(hwnd) <= 0) return false;
                if (IsDesktopOrTaskbar(ClassOf(hwnd))) return false;
                int cloaked = 0;
                DwmGetWindowAttribute(hwnd, 14, out cloaked, 4);
                if (cloaked != 0) return false;
                return true;
            }
            catch { return false; }
        }
        // 見えているウィンドウを手前から順に
        public static List<IntPtr> TopWindows()
        {
            List<IntPtr> list = new List<IntPtr>();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if ((GetWindowLong(h, GWL_STYLE) & WS_VISIBLE) != 0) list.Add(h);
                return true;
            }, IntPtr.Zero);
            return list;
        }
        public static List<Rectangle> Monitors(bool work)
        {
            List<Rectangle> list = new List<Rectangle>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data)
            {
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(hMon, ref mi))
                {
                    RECT r = work ? mi.rcWork : mi.rcMonitor;
                    list.Add(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }
        // 点がどの画面にあるか（1から。どれでもなければ0）
        public static int MonitorOf(int x, int y, List<Rectangle> areas)
        {
            for (int i = 0; i < areas.Count; i++)
                if (x >= areas[i].Left && x < areas[i].Right && y >= areas[i].Top && y < areas[i].Bottom) return i + 1;
            return 0;
        }
        // 透明度を変える（null＝透かしをやめる）
        public static void SetAlpha(IntPtr hwnd, int? alpha)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (alpha == null)
            {
                SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                SetWindowLong(hwnd, GWL_EXSTYLE, ex & ~WS_EX_LAYERED);
                return;
            }
            if ((ex & WS_EX_LAYERED) == 0) SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
            if (!SetLayeredWindowAttributes(hwnd, 0, (byte)alpha.Value, LWA_ALPHA))
                throw new Exception("透明度を変えられませんでした");
        }
        // 今の透明度（透かしていなければ null）
        public static int? GetAlpha(IntPtr hwnd)
        {
            int key, flags; byte a;
            if ((GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_LAYERED) == 0) return null;
            if (GetLayeredWindowAttributes(hwnd, out key, out a, out flags) && (flags & LWA_ALPHA) != 0) return a;
            return null;
        }
        public static void SendKey(int vk, bool extended)
        {
            int f = extended ? 1 : 0;
            keybd_event((byte)vk, 0, f, IntPtr.Zero);
            keybd_event((byte)vk, 0, f | 2, IntPtr.Zero);
        }
        public static bool KeyDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
    }

    // ================================================================
    //  設定（どの機能を使うか）。AutoHotkey 版と同じファイルを使う
    // ================================================================
    public static class Settings
    {
        public static readonly string[] Order = new string[] { "swap", "shot", "peek", "manual" };
        public static readonly Dictionary<string, bool> Feat = new Dictionary<string, bool>();
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "操作性改善アプリ"); } }
        public static string Ini { get { return Path.Combine(Dir, "設定.ini"); } }

        public static void Load()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
            foreach (string k in Order) Feat[k] = Read("features", k, "0") == "1";
        }
        public static bool On(string k) { bool v; return Feat.TryGetValue(k, out v) && v; }
        public static void Set(string k, bool on)
        {
            Feat[k] = on;
            Write("features", k, on ? "1" : "0");
        }
        public static string Read(string sec, string key, string def)
        {
            StringBuilder sb = new StringBuilder(256);
            W.GetPrivateProfileString(sec, key, def, sb, sb.Capacity, Ini);
            return sb.ToString();
        }
        public static void Write(string sec, string key, string val)
        {
            try { W.WritePrivateProfileString(sec, key, val, Ini); } catch { }
        }
        public static string StateString()
        {
            List<string> parts = new List<string>();
            foreach (string k in Order) parts.Add(k + "=" + (On(k) ? "1" : "0"));
            return string.Join(";", parts.ToArray());
        }
    }

    // ================================================================
    //  起動
    // ================================================================
    public static class Program
    {
        public static string SysDir;      // 9_AI作業用
        public static string RootDir;     // 操作性改善アプリ
        static Mutex mutex;
        public const string AppName = "操作性改善アプリ";

        // app.ps1 から呼ばれる
        public static void Run(string sysDir, string[] args)
        {
            SysDir = sysDir;
            RootDir = Path.GetDirectoryName(sysDir);
            W.PerMonitorDpi();
            bool restart = Has(args, "/restart");
            bool open = Has(args, "/open");

            bool created;
            mutex = new Mutex(true, "Local\\SousaKaizenApp_v1", out created);
            if (!created)
            {
                if (restart)
                {
                    try { created = mutex.WaitOne(15000); } catch (AbandonedMutexException) { created = true; }
                }
                if (!created)
                {
                    // もう動いているので、そちらに「画面を開いて」と伝えて終わる
                    try
                    {
                        EventWaitHandle ev = EventWaitHandle.OpenExisting("Local\\SousaKaizenApp_v1_open");
                        try { W.AllowSetForegroundWindow(-1); } catch { }   // 動いている方が画面を手前に出せるようにする
                        if (open) ev.Set();
                    }
                    catch { }
                    return;
                }
            }
            try
            {
                Application.EnableVisualStyles();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("エラー: " + e.Exception); };
                Application.Run(new Host(open, restart));
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }

        static bool Has(string[] args, string a)
        {
            if (args == null) return false;
            foreach (string s in args) if (string.Equals(s, a, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(SysDir, "log.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n", Encoding.UTF8); }
            catch { }
        }

        // この版の番号（version.txt）
        public static string Version
        {
            get
            {
                try { return File.ReadAllText(Path.Combine(SysDir, "version.txt")).Trim(); }
                catch { return "0.0.0"; }
            }
        }

        public static string PowerShellExe
        {
            get { return Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"); }
        }
        public static string PsArgs(string extra)
        {
            return "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + Path.Combine(SysDir, "app.ps1") + "\"" + (extra == "" ? "" : " " + extra);
        }
    }

    // ================================================================
    //  本体（見えない窓）。キーの見張り・トレイアイコン・自動更新
    // ================================================================
    public class Host : Form
    {
        public static Host I;
        NotifyIcon tray;
        IntPtr kbHook = IntPtr.Zero, msHook = IntPtr.Zero;
        W.HookProc kbProc, msProc;
        System.Windows.Forms.Timer updTimer;
        EventWaitHandle openEvent;
        public SettingsForm Settings;
        public Icon AppIcon;
        bool startOpen;

        public Host(bool open, bool restart)
        {
            I = this;
            startOpen = open;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Text = Program.AppName + " (本体)";
            SousaKaizen.Settings.Load();
            AppIcon = MakeIcon();
            CreateHandle();
            Start(restart);
        }

        // 見えない窓のまま動かす
        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated) CreateHandle();
            base.SetVisibleCore(false);
        }

        void Start(bool restart)
        {
            // 後ろを触る：押している時間を見るタイマー
            peekHoldTimer = new System.Windows.Forms.Timer();
            peekHoldTimer.Interval = 200;
            peekHoldTimer.Tick += delegate
            {
                peekHoldTimer.Stop();
                if (peekKeyDown && !peekStarted)
                {
                    peekStarted = true;
                    Peek.Start();
                    peekKeepTimer.Start();
                }
            };
            peekKeepTimer = new System.Windows.Forms.Timer();
            peekKeepTimer.Interval = 100;
            peekKeepTimer.Tick += delegate { Peek.Keep(); };

            // キーとマウスの見張りは専用の流れで動かす（本体が重い処理をしていても、キー入力を止めないため）
            kbProc = KeyboardProc;
            msProc = MouseProc;
            Thread ht = new Thread(delegate()
            {
                W.PerMonitorDpi();
                IntPtr mod = W.GetModuleHandle(null);
                kbHook = W.SetWindowsHookEx(13, kbProc, mod, 0);
                if (kbHook == IntPtr.Zero) Program.Log("キーの見張りを始められませんでした: " + Marshal.GetLastWin32Error());
                msHook = W.SetWindowsHookEx(14, msProc, mod, 0);
                Application.Run();
            });
            ht.IsBackground = true;
            ht.Start();

            // トレイアイコン
            tray = new NotifyIcon();
            tray.Icon = AppIcon;
            tray.Text = Program.AppName;
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem(Program.AppName + "を開く");
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);
            openItem.Click += delegate { OpenSettings(); };
            menu.Items.Add(openItem);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem quit = new ToolStripMenuItem("終了");
            quit.Click += delegate { Application.Exit(); };
            menu.Items.Add(quit);
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { OpenSettings(); };
            tray.Visible = true;

            // もう1回起動されたら画面を開く
            openEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\SousaKaizenApp_v1_open");
            Thread t = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        openEvent.WaitOne();
                        BeginInvoke(new MethodInvoker(OpenSettings));
                    }
                    catch { return; }
                }
            });
            t.IsBackground = true;
            t.Start();

            // 自動更新
            Updater.Init();
            Online.Init();
            updTimer = new System.Windows.Forms.Timer();
            updTimer.Interval = 2000;
            updTimer.Tick += delegate { Updater.Check(); };
            updTimer.Start();

            Application.ApplicationExit += delegate { Cleanup(); };

            BeginInvoke(new MethodInvoker(delegate
            {
                try { Setup.EnsureShortcuts(); } catch (Exception e) { Program.Log("ショートカット: " + e.Message); }
                if (!restart) Setup.CheckAutoHotkey();
                bool reopen = restart && SousaKaizen.Settings.Read("app", "reopen", "0") == "1";
                if (reopen) SousaKaizen.Settings.Write("app", "reopen", "0");
                if (startOpen || reopen) OpenSettings();
            }));
        }

        void Cleanup()
        {
            try { Peek.End(); } catch { }
            try { if (kbHook != IntPtr.Zero) W.UnhookWindowsHookEx(kbHook); } catch { }
            try { if (msHook != IntPtr.Zero) W.UnhookWindowsHookEx(msHook); } catch { }
            kbHook = IntPtr.Zero; msHook = IntPtr.Zero;
            try { tray.Visible = false; tray.Dispose(); } catch { }
        }

        public void OpenSettings()
        {
            try
            {
                if (Settings == null || Settings.IsDisposed) Settings = new SettingsForm();
                Settings.Show();
                if (Settings.WindowState == FormWindowState.Minimized) Settings.WindowState = FormWindowState.Normal;
                // どの画面にも見えていなければ、メインの画面の真ん中へ
                bool seen = false;
                foreach (Screen sc in Screen.AllScreens)
                {
                    Rectangle r = Rectangle.Intersect(sc.WorkingArea, Settings.Bounds);
                    if (r.Width >= 120 && r.Height >= 80) { seen = true; break; }
                }
                if (!seen)
                {
                    Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                    Settings.Location = new Point(wa.Left + Math.Max(0, (wa.Width - Settings.Width) / 2), wa.Top + Math.Max(0, (wa.Height - Settings.Height) / 2));
                }
                // ほかのウィンドウの後ろに隠れないように、いったん一番手前にしてから戻す
                Settings.TopMost = true;
                Settings.TopMost = false;
                Settings.Activate();
                Program.Log("アプリ画面を開きました（" + Settings.Bounds + (seen ? "" : "・画面の外にあったので真ん中へ") + "）");
            }
            catch (Exception e)
            {
                Program.Log("アプリ画面: " + e);
                MessageBox.Show("アプリ画面を開けませんでした。\n" + e.Message, Program.AppName);
            }
        }

        // 機能のオン・オフが変わった
        public void OnFeatureChanged(string key, bool on)
        {
            if (key == "manual" && !on && Manual.Rec) Manual.Toggle();   // 記録中にオフにしたら、記録を終えて手順書を作る
            if (key == "peek" && !on && peekStarted)
            {
                peekStarted = false;
                peekKeepTimer.Stop();
                Peek.ForceEnd();
            }
        }

        // アイコン：緑の地に、白と紺の角丸が少しずれて重なる形（app.ico。なければその場で描く）
        static Icon MakeIcon()
        {
            try
            {
                string ico = Path.Combine(Program.SysDir, "app.ico");
                if (File.Exists(ico)) return new Icon(ico, SystemInformation.SmallIconSize);
            }
            catch { }
            Bitmap b = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (GraphicsPath p = Rounded(new Rectangle(1, 1, 30, 30), 7))
                using (SolidBrush br = new SolidBrush(Color.FromArgb(52, 199, 89)))
                    g.FillPath(br, p);
                using (GraphicsPath p = Rounded(new Rectangle(7, 7, 12, 12), 4))
                using (SolidBrush br = new SolidBrush(Color.White))
                    g.FillPath(br, p);
                using (GraphicsPath p = Rounded(new Rectangle(13, 13, 12, 12), 4))
                using (SolidBrush br = new SolidBrush(Color.FromArgb(217, 31, 58, 104)))
                    g.FillPath(br, p);
            }
            return Icon.FromHandle(b.GetHicon());
        }
        public static GraphicsPath Rounded(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ------------------------------------------------------------
        //  キーの見張り
        //  ・自分で送ったキー（目印付き）は見ない
        //  ・時間のかかる処理は BeginInvoke で後回しにする（見張りを止めないため）
        // ------------------------------------------------------------
        int lastDownVk = 0;                    // 最後に押したキー（2回押しの判定用）
        int swapTap = 0, manTap = 0, manTapVk = 0;
        bool prtHeld = false, rAltHeld2 = false, enterBlocked = false;
        // 無変換（後ろを触る）
        bool peekPhys = false;                         // 見張り側：無変換を押しているか
        bool peekKeyDown = false, peekStarted = false; // 本体側
        System.Windows.Forms.Timer peekHoldTimer, peekKeepTimer;

        void PeekDown()
        {
            if (peekKeyDown) return;
            peekKeyDown = true;
            peekStarted = false;
            peekHoldTimer.Stop();
            peekHoldTimer.Start();
        }
        void PeekUp()
        {
            peekKeyDown = false;
            peekHoldTimer.Stop();
            if (peekStarted)
            {
                peekStarted = false;
                peekKeepTimer.Stop();
                Peek.End();
            }
            else W.SendKey(VK_NONCONVERT, false);      // 短く押しただけなら、普段の無変換キーとして送る
        }

        const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_RMENU = 0xA5, VK_SNAPSHOT = 0x2C, VK_NONCONVERT = 0x1D, VK_RETURN = 0x0D;

        IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    W.KBDLLHOOKSTRUCT k = (W.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(W.KBDLLHOOKSTRUCT));
                    bool injected = (k.flags & 0x10) != 0;
                    int msg = wParam.ToInt32();
                    bool down = msg == 0x100 || msg == 0x104;
                    if (!injected && OnKey(k.vkCode, k.scanCode, k.flags, down)) return new IntPtr(1);
                }
                catch (Exception e) { Program.Log("キー: " + e.Message); }
            }
            return W.CallNextHookEx(kbHook, nCode, wParam, lParam);
        }

        IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int msg = wParam.ToInt32();
                    if (msg == 0x201 || msg == 0x204 || msg == 0x207 || msg == 0x20B)
                    {
                        W.MSLLHOOKSTRUCT m = (W.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(W.MSLLHOOKSTRUCT));
                        if ((m.flags & 1) == 0)
                        {
                            lastDownVk = msg == 0x201 ? 1 : msg == 0x204 ? 2 : 4;
                            if (Manual.Rec && (msg == 0x201 || msg == 0x204))
                            {
                                string kind = msg == 0x201 ? "左クリック" : "右クリック";
                                int x = m.pt.X, y = m.pt.Y, t = Environment.TickCount;
                                IntPtr hw = W.WindowFromPoint(m.pt);
                                BeginInvoke(new MethodInvoker(delegate { Manual.OnClick(kind, x, y, hw, t); }));
                            }
                        }
                    }
                }
                catch (Exception e) { Program.Log("マウス: " + e.Message); }
            }
            return W.CallNextHookEx(msHook, nCode, wParam, lParam);
        }

        // true を返すと、そのキーは他のソフトに渡さない
        bool OnKey(int vk, int sc, int flags, bool down)
        {
            bool tapped = !down && lastDownVk == vk;     // そのキーだけを押して離した
            if (down) lastDownVk = vk;

            // 記録中のキー（Enter・PrintScreen 以外）は手順に書く
            if (Manual.Rec && down && vk != VK_RETURN && vk != VK_SNAPSHOT && vk != 0xE8)
            {
                Manual.KeySnapshot ks = Manual.Snap(vk, sc);
                BeginInvoke(new MethodInvoker(delegate { Manual.OnKeyDown(ks); }));
            }

            // 1. 画面入れ替え：左Ctrlだけを素早く2回
            if (vk == VK_LCONTROL && tapped && SousaKaizen.Settings.On("swap"))
            {
                int now = Environment.TickCount;
                if (swapTap != 0 && now - swapTap < 400)
                {
                    swapTap = 0;
                    BeginInvoke(new MethodInvoker(Swap.Run));
                }
                else swapTap = now;
            }
            else if (vk == VK_LCONTROL && !down) swapTap = 0;         // 左Ctrl+C などのときは数え直す

            // 4. マニュアル作成：右Ctrl または 右Alt を素早く2回
            if ((vk == VK_RCONTROL || vk == VK_RMENU) && SousaKaizen.Settings.On("manual"))
            {
                if (vk == VK_RMENU && down && !rAltHeld2)
                {
                    rAltHeld2 = true;
                    // 右Altだけを押して離したときに、メニューバーが反応しないようにする
                    BeginInvoke(new MethodInvoker(delegate { W.SendKey(0xE8, false); }));
                }
                if (!down && vk == VK_RMENU) rAltHeld2 = false;
                if (tapped)
                {
                    int now = Environment.TickCount;
                    if (manTap != 0 && manTapVk == vk && now - manTap < 400)
                    {
                        manTap = 0;
                        BeginInvoke(new MethodInvoker(Manual.Toggle));
                    }
                    else { manTap = now; manTapVk = vk; }
                }
                else if (!down) manTap = 0;                             // 右Ctrl+C などのときは数え直す
            }

            // 2. スクショ／記録中は「1枚追加」
            if (vk == VK_SNAPSHOT)
            {
                if (Manual.Rec)
                {
                    if (down && !prtHeld) BeginInvoke(new MethodInvoker(Manual.AddActive));
                    prtHeld = down;
                    return true;
                }
                if (SousaKaizen.Settings.On("shot") || prtHeld)
                {
                    if (down && !prtHeld) BeginInvoke(new MethodInvoker(Shot.Run));
                    prtHeld = down;
                    return true;
                }
            }

            // 3. 後ろを触る：無変換を押している間（判定と処理は本体の側で行う）
            if (vk == VK_NONCONVERT && (SousaKaizen.Settings.On("peek") || peekPhys))
            {
                if (down)
                {
                    if (!peekPhys) { peekPhys = true; BeginInvoke(new MethodInvoker(PeekDown)); }
                }
                else
                {
                    peekPhys = false;
                    BeginInvoke(new MethodInvoker(PeekUp));
                }
                return true;
            }

            // 記録中の Enter：Enter が効く前の画面を撮ってから送り直す
            if (vk == VK_RETURN && Manual.Rec)
            {
                if (down)
                {
                    if (enterBlocked) return true;            // 押しっぱなしの繰り返しは送らない
                    enterBlocked = true;
                    bool numpad = (flags & 1) != 0;
                    string mods = Manual.ModsNow(true);
                    BeginInvoke(new MethodInvoker(delegate { Manual.OnEnter(numpad, mods); }));
                    return true;
                }
                if (enterBlocked) { enterBlocked = false; return true; }
            }
            if (vk == VK_RETURN && !down && enterBlocked) { enterBlocked = false; return true; }
            return false;
        }

    }

    // ================================================================
    //  小さなお知らせ（1.5秒だけマウスの近くに出す）
    // ================================================================
    public class Notice : Form
    {
        static Notice cur;
        System.Windows.Forms.Timer timer;

        public static void Show(string msg)
        {
            try
            {
                if (cur != null && !cur.IsDisposed) cur.Close();
                cur = new Notice(msg);
                cur.Show();
            }
            catch { }
        }

        Notice(string msg)
        {
            float s = W.GetDpiForSystem() / 96f;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(255, 255, 225);
            Label l = new Label();
            l.AutoSize = true;
            l.Text = msg;
            l.Font = new Font("Meiryo UI", 12f * s, GraphicsUnit.Pixel);
            l.ForeColor = Color.Black;
            l.Padding = new Padding((int)(6 * s), (int)(3 * s), (int)(6 * s), (int)(3 * s));
            Controls.Add(l);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Point p = Cursor.Position;
            Location = new Point(p.X + (int)(16 * s), p.Y + (int)(16 * s));
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1500;
            timer.Tick += delegate { timer.Stop(); Close(); };
            timer.Start();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8 | 0x20;   // 前に出さない・小窓・最前面・クリックを通す
                cp.Style |= unchecked((int)0x00800000);          // 細い枠
                return cp;
            }
        }
    }

    // ================================================================
    //  1. 画面入れ替え：左右2画面のウィンドウを全部入れ替える
    // ================================================================
    public static class Swap
    {
        const int SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_ASYNC = 0x4000;
        class Item
        {
            public IntPtr hwnd; public bool max, min; public int src, dst; public string proc; public int aware;
            public Rectangle before, target; public Padding edge;
        }

        // どのパソコンでも同じ考え方で動く入れ替え
        // ・画面が2つなら左右を入れ替え、3つ以上なら1つずつ隣の画面へ回す（左から順に並べて考える）
        // ・位置は「その画面の中のどの辺りにあったか」の割合を保つ。大きさはソフト本来のまま（入りきらないときだけ縮める）
        // ・最大化はいったん戻さずに最大化のまま移す。最小化中のものも、戻したときに反対の画面に出るようにしておく
        // ・普通のウィンドウは全部まとめて一度に動かす（1つずつ動いて見えないように）
        // ・拡大率の違う画面へ移ったときの大きさは、ソフトと Windows に任せる（無理に広げると中が灰色になるため）
        public static void Run()
        {
            W.PerMonitorDpi();
            List<W.Mon> mons = W.MonitorsEx();
            if (mons.Count < 2)
            {
                Notice.Show("画面が1つなので入れ替えられません");
                return;
            }
            mons.Sort(delegate(W.Mon a, W.Mon b) { return a.Full.Left != b.Full.Left ? a.Full.Left.CompareTo(b.Full.Left) : a.Full.Top.CompareTo(b.Full.Top); });
            List<Rectangle> full = new List<Rectangle>();
            foreach (W.Mon m in mons) full.Add(m.Full);
            W.Mon prim = mons[0];
            foreach (W.Mon m in mons) if (m.Primary) prim = m;

            List<Item> items = new List<Item>();
            foreach (IntPtr hwnd in W.TopWindows())
            {
                if (!W.IsNormalWindow(hwnd)) continue;
                try
                {
                    Item it = new Item();
                    it.hwnd = hwnd; it.min = W.IsIconic(hwnd); it.max = !it.min && W.IsZoomed(hwnd);
                    Rectangle rc;
                    if (it.min)
                    {
                        // 最小化中は「戻したときの位置」で、どの画面のものかを決める
                        W.WINDOWPLACEMENT wp = Placement(hwnd);
                        rc = FromWorkspace(wp.rcNormal, prim);
                    }
                    else
                    {
                        W.RECT r; W.GetWindowRect(hwnd, out r);
                        rc = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                        // ウィンドウのまわりの見えない縁（影の部分）の太さ。画面に収めるときはこの分をはみ出してよい
                        W.RECT fr = W.FrameRect(hwnd);
                        it.edge = new Padding(Math.Max(0, fr.Left - r.Left), Math.Max(0, fr.Top - r.Top), Math.Max(0, r.Right - fr.Right), Math.Max(0, r.Bottom - fr.Bottom));
                    }
                    int src = W.MonitorOf(rc.Left + rc.Width / 2, rc.Top + rc.Height / 2, full);
                    if (src == 0) src = Nearest(rc, full);
                    if (src == 0) continue;
                    it.src = src - 1;
                    it.dst = (it.src + 1) % mons.Count;
                    it.before = rc;
                    it.proc = W.ProcessNameOf(hwnd); it.aware = W.Awareness(hwnd);
                    items.Add(it);
                }
                catch { }
            }

            // 1) 最大化・最小化：画面には出さずに「戻したときの位置」を書き換える
            foreach (Item it in items)
            {
                if (!it.max && !it.min) continue;
                try
                {
                    W.WINDOWPLACEMENT wp = Placement(it.hwnd);
                    Rectangle to = Place(FromWorkspace(wp.rcNormal, prim), mons[it.src].Work, mons[it.dst].Work);
                    wp.rcNormal = ToWorkspace(to, prim);
                    if (it.max) { wp.showCmd = 3; wp.flags = 0; }             // 最大化のまま反対の画面へ
                    else { wp.showCmd = 7; wp.flags = wp.flags & 2; }         // 最小化のまま（戻すと反対の画面に出る）
                    W.SetWindowPlacement(it.hwnd, ref wp);
                }
                catch { }
            }

            // 2) 普通のウィンドウ：全部まとめて一度に動かす
            List<Item> normals = new List<Item>();
            foreach (Item it in items)
            {
                if (it.max || it.min) continue;
                it.target = Target(it, it.before.Size, mons[it.src], mons[it.dst]);
                normals.Add(it);
            }
            bool batched = false;
            if (normals.Count > 0)
            {
                try
                {
                    IntPtr h = W.BeginDeferWindowPos(normals.Count);
                    foreach (Item it in normals)
                    {
                        if (h == IntPtr.Zero) break;
                        int f = SWP_NOZORDER | SWP_NOACTIVATE | (it.target.Size == it.before.Size ? SWP_NOSIZE : 0);
                        h = W.DeferWindowPos(h, it.hwnd, IntPtr.Zero, it.target.Left, it.target.Top, it.target.Width, it.target.Height, f);
                    }
                    if (h != IntPtr.Zero) batched = W.EndDeferWindowPos(h);
                }
                catch { batched = false; }
                if (!batched)
                {
                    // まとめて動かせないウィンドウ（管理者として動いているソフトなど）があったときは、1つずつ動かす
                    foreach (Item it in normals)
                    {
                        try
                        {
                            int f = SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNC | (it.target.Size == it.before.Size ? SWP_NOSIZE : 0);
                            W.SetWindowPos(it.hwnd, IntPtr.Zero, it.target.Left, it.target.Top, it.target.Width, it.target.Height, f);
                        }
                        catch { }
                    }
                }
            }

            // 3) 少し待って仕上げ：拡大率に合わせて大きさが変わったものを画面に収め直す。最大化が移っていなければやり直す
            Thread.Sleep(150);
            StringBuilder log = new StringBuilder();
            foreach (W.Mon m in mons) log.AppendLine("画面 " + m.Full + " 作業領域 " + m.Work + " 拡大率 " + (m.Dpi * 100 / 96) + "%" + (m.Primary ? " メイン" : ""));
            log.AppendLine(batched ? "（まとめて移動）" : "（1つずつ移動）");
            foreach (Item it in items)
            {
                string result = "";
                try
                {
                    W.Mon d = mons[it.dst];
                    W.RECT r; W.GetWindowRect(it.hwnd, out r);
                    Rectangle now = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    int on = W.MonitorOf(now.Left + now.Width / 2, now.Top + now.Height / 2, full) - 1;
                    if (it.max)
                    {
                        if (on != it.dst || !W.IsZoomed(it.hwnd))
                        {
                            // うまく移らなかったソフトは、元に戻す → 移す → 最大化 の順でやり直す
                            W.ShowWindow(it.hwnd, 9);
                            W.GetWindowRect(it.hwnd, out r);
                            Rectangle nr = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                            if (W.MonitorOf(nr.Left + nr.Width / 2, nr.Top + nr.Height / 2, full) - 1 != it.dst)
                            {
                                Rectangle to = Place(nr, mons[it.src].Work, d.Work);
                                W.SetWindowPos(it.hwnd, IntPtr.Zero, to.Left, to.Top, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSIZE);
                            }
                            W.ShowWindow(it.hwnd, 3);
                            result = "やり直し";
                        }
                    }
                    else if (!it.min)
                    {
                        // 移した先で大きさが変わったソフトは、その大きさで置き直す（真ん中・くっついている端をそろえる）
                        Rectangle fit = Target(it, now.Size, mons[it.src], d);
                        if (fit != now)
                        {
                            W.SetWindowPos(it.hwnd, IntPtr.Zero, fit.Left, fit.Top, fit.Width, fit.Height, SWP_NOZORDER | SWP_NOACTIVATE);
                            result = "画面に収め直した";
                        }
                    }
                    W.GetWindowRect(it.hwnd, out r);
                    log.AppendLine(it.proc + " 対応" + it.aware + (it.max ? " 最大化" : "") + (it.min ? " 最小化" : "") + "  画面" + (it.src + 1) + "→" + (it.dst + 1)
                        + "  前 " + it.before + " → 後 " + Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom) + " " + result);
                }
                catch (Exception e) { log.AppendLine(it.proc + " エラー " + e.Message); }
            }
            // 調子の悪いソフトを調べるための記録（毎回上書き。ウィンドウの題名は残さない）
            try { File.WriteAllText(Path.Combine(Program.SysDir, "入れ替え記録.txt"), log.ToString(), Encoding.UTF8); } catch { }
        }

        static W.WINDOWPLACEMENT Placement(IntPtr hwnd)
        {
            W.WINDOWPLACEMENT wp = new W.WINDOWPLACEMENT();
            wp.length = Marshal.SizeOf(typeof(W.WINDOWPLACEMENT));
            if (!W.GetWindowPlacement(hwnd, ref wp)) throw new Exception("位置を読めません");
            return wp;
        }
        // 「元に戻したときの位置」は、メインの画面の作業領域の左上からの位置で入っている
        static Rectangle FromWorkspace(W.RECT r, W.Mon prim)
        {
            return Rectangle.FromLTRB(r.Left + prim.Work.Left - prim.Full.Left, r.Top + prim.Work.Top - prim.Full.Top,
                                      r.Right + prim.Work.Left - prim.Full.Left, r.Bottom + prim.Work.Top - prim.Full.Top);
        }
        static W.RECT ToWorkspace(Rectangle r, W.Mon prim)
        {
            W.RECT o;
            o.Left = r.Left - (prim.Work.Left - prim.Full.Left); o.Top = r.Top - (prim.Work.Top - prim.Full.Top);
            o.Right = r.Right - (prim.Work.Left - prim.Full.Left); o.Bottom = r.Bottom - (prim.Work.Top - prim.Full.Top);
            return o;
        }
        // どの画面にも中心がないとき（はみ出しているとき）は、一番重なっている画面
        static int Nearest(Rectangle r, List<Rectangle> full)
        {
            int best = 0, area = 0;
            for (int i = 0; i < full.Count; i++)
            {
                Rectangle x = Rectangle.Intersect(r, full[i]);
                if (x.Width * x.Height > area) { area = x.Width * x.Height; best = i + 1; }
            }
            return best;
        }

        // 普通のウィンドウの移し先を決める（size は移した先での大きさ）
        // ・画面の端にくっついていたら、移し先でも同じ端にくっつける（左半分・右半分に寄せたウィンドウなど）
        // ・そうでなければ、ウィンドウの真ん中が、移し先の画面でも同じ割合の場所に来るようにする
        static Rectangle Target(Item it, Size size, W.Mon sm, W.Mon dm)
        {
            const int tol = 12;                       // これ以内なら「端にくっついている」とみなす
            Rectangle s = sm.Work, d = dm.Work, b = it.before;
            Padding e = it.edge;
            // 見えている枠（影を除いた部分）で端との距離を測る
            int fl = b.Left + e.Left, ft = b.Top + e.Top, fr = b.Right - e.Right, fb = b.Bottom - e.Bottom;
            bool aL = Math.Abs(fl - s.Left) <= tol, aR = Math.Abs(fr - s.Right) <= tol;
            bool aT = Math.Abs(ft - s.Top) <= tol, aB = Math.Abs(fb - s.Bottom) <= tol;
            int w = size.Width, h = size.Height, x, y;
            if (aL) x = d.Left - e.Left;
            else if (aR) x = d.Right + e.Right - w;
            else
            {
                double cx = (b.Left + b.Width / 2.0 - s.Left) / s.Width;
                x = d.Left + (int)Math.Round(cx * d.Width - w / 2.0);
            }
            if (aT) y = d.Top - e.Top;
            else if (aB) y = d.Bottom + e.Bottom - h;
            else
            {
                double cy = (b.Top + b.Height / 2.0 - s.Top) / s.Height;
                y = d.Top + (int)Math.Round(cy * d.Height - h / 2.0);
            }
            return Fit(new Rectangle(x, y, w, h), Grow(d, e));
        }

        // 元の画面での位置の割合を保って、移し先での位置を決める。大きさは変えない（入りきらないときだけ縮める）
        static Rectangle Place(Rectangle r, Rectangle s, Rectangle d)
        {
            int nx = d.Left + (int)Math.Round((r.Left - s.Left) * (double)d.Width / s.Width);
            int ny = d.Top + (int)Math.Round((r.Top - s.Top) * (double)d.Height / s.Height);
            return Fit(new Rectangle(nx, ny, r.Width, r.Height), d);
        }

        static Rectangle Grow(Rectangle r, Padding p)
        {
            return Rectangle.FromLTRB(r.Left - p.Left, r.Top - p.Top, r.Right + p.Right, r.Bottom + p.Bottom);
        }

        // 画面からはみ出さないように収める
        static Rectangle Fit(Rectangle r, Rectangle d)
        {
            int w = Math.Min(r.Width, d.Width), h = Math.Min(r.Height, d.Height);
            int x = Math.Max(d.Left, Math.Min(r.Left, d.Right - w));
            int y = Math.Max(d.Top, Math.Min(r.Top, d.Bottom - h));
            return new Rectangle(x, y, w, h);
        }
    }

    // ================================================================
    //  2. スクショ：今のウィンドウだけを PNG で保存
    // ================================================================
    public static class Shot
    {
        public static void Run()
        {
            W.PerMonitorDpi();
            IntPtr hwnd = W.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) { Notice.Show("撮れるウィンドウがありません"); return; }
            Rectangle rc;
            if (W.IsDesktopOrTaskbar(W.ClassOf(hwnd)))
                rc = W.VirtualScreen();                       // デスクトップ・タスクバーのときは2画面まとめて全体
            else
            {
                W.RECT r = W.FrameRect(hwnd);
                rc = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            }
            if (rc.Width <= 0 || rc.Height <= 0) { Notice.Show("撮れるウィンドウがありません"); return; }

            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "スクショ");
            Directory.CreateDirectory(dir);
            string bas = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            string path = bas + ".png";
            int n = 2;
            while (File.Exists(path)) path = bas + "_" + (n++) + ".png";
            try
            {
                using (Bitmap b = Capture(rc)) b.Save(path, ImageFormat.Png);
                Notice.Show("保存しました：" + Path.GetFileName(path));
            }
            catch (Exception e)
            {
                Program.Log("スクショ: " + e.Message);
                Notice.Show("保存できませんでした");
            }
        }

        // 画面の指定した範囲を画像として取り込む（重ねて表示している窓も写るように CAPTUREBLT を付ける）
        public static Bitmap Capture(Rectangle rc)
        {
            IntPtr hdcS = W.GetDC(IntPtr.Zero);
            IntPtr hdcM = W.CreateCompatibleDC(hdcS);
            IntPtr hbm = W.CreateCompatibleBitmap(hdcS, rc.Width, rc.Height);
            IntPtr obm = W.SelectObject(hdcM, hbm);
            try
            {
                W.BitBlt(hdcM, 0, 0, rc.Width, rc.Height, hdcS, rc.Left, rc.Top, 0x40CC0020);
                W.SelectObject(hdcM, obm);
                using (Bitmap tmp = Image.FromHbitmap(hbm))
                    return new Bitmap(tmp);
            }
            finally
            {
                W.DeleteObject(hbm);
                W.DeleteDC(hdcM);
                W.ReleaseDC(IntPtr.Zero, hdcS);
            }
        }
    }

    // ================================================================
    //  3. 後ろを触る：無変換を押している間、両方の画面のウィンドウを透かす
    //     画面ごとに一番手前はうっすら、2枚目から後ろは完全に透明。クリックは後ろへ通す
    //     透かせないもの（独自の透かし方・透かしを外すもの・同じソフトの小窓）は押している間だけ隠す
    // ================================================================
    public static class Peek
    {
        public const int AlphaFront = 50;    // 一番手前の濃さ（0=完全に透明 ～ 255=透かさない）
        public const int AlphaBack = 0;      // 2枚目から後ろの濃さ

        class Item { public IntPtr hwnd; public int? alpha; public int ex; public bool changed; public bool hidden; }
        static List<Item> list = new List<Item>();

        public static void Start()
        {
            W.PerMonitorDpi();
            End();
            List<Rectangle> areas = W.Monitors(false);
            Dictionary<int, int> count = new Dictionary<int, int>();
            HashSet<IntPtr> done = new HashSet<IntPtr>();
            HashSet<int> pids = new HashSet<int>();
            List<Rectangle> rects = new List<Rectangle>();
            bool failed = false;
            List<IntPtr> wins = W.TopWindows();

            // (1) 普通のウィンドウを手前から順に透かす
            foreach (IntPtr hwnd in wins)
            {
                if (hwnd == Host.I.Handle) continue;
                if (!W.IsNormalWindow(hwnd)) continue;
                try
                {
                    if (W.IsIconic(hwnd)) continue;
                    W.RECT r; W.GetWindowRect(hwnd, out r);
                    int mon = W.MonitorOf((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2, areas);
                    int c; count.TryGetValue(mon, out c); c++; count[mon] = c;
                    bool first = mon != 0 && c == 1;
                    Item it = new Item();
                    it.hwnd = hwnd;
                    it.ex = W.GetWindowLong(hwnd, W.GWL_EXSTYLE);
                    it.alpha = W.GetAlpha(hwnd);
                    list.Add(it);
                    done.Add(hwnd);
                    int pid; W.GetWindowThreadProcessId(hwnd, out pid); pids.Add(pid);
                    rects.Add(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
                    if ((it.ex & W.WS_EX_LAYERED) != 0 && it.alpha == null)
                    {
                        Hide(it);       // 独自の透かし方をしているので、押している間だけ隠す
                        continue;
                    }
                    it.changed = true;
                    W.SetAlpha(hwnd, first ? AlphaFront : AlphaBack);
                    W.SetWindowLong(hwnd, W.GWL_EXSTYLE, W.GetWindowLong(hwnd, W.GWL_EXSTYLE) | W.WS_EX_TRANSPARENT);
                }
                catch { failed = true; }
            }

            // (2) 同じソフトが上に重ねている小窓も、押している間だけ隠す
            foreach (IntPtr hwnd in wins)
            {
                if (done.Contains(hwnd) || hwnd == Host.I.Handle) continue;
                try
                {
                    int pid; W.GetWindowThreadProcessId(hwnd, out pid);
                    if (!pids.Contains(pid)) continue;
                    if (W.IsDesktopOrTaskbar(W.ClassOf(hwnd))) continue;
                    W.RECT r; W.GetWindowRect(hwnd, out r);
                    Rectangle rr = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    if (rr.Width <= 0 || rr.Height <= 0) continue;
                    bool hit = false;
                    foreach (Rectangle s in rects) if (s.IntersectsWith(rr)) { hit = true; break; }
                    if (!hit) continue;
                    Item it = new Item();
                    it.hwnd = hwnd;
                    list.Add(it);
                    done.Add(hwnd);
                    Hide(it);
                }
                catch { failed = true; }
            }
            if (failed) Notice.Show("透かせないウィンドウがありました");
        }

        // 押している間、透かしが外されていないか見る（ソフトが自分で元に戻したら隠す）
        public static void Keep()
        {
            foreach (Item it in list)
            {
                if (it.hidden || !it.changed) continue;
                try
                {
                    if ((W.GetWindowLong(it.hwnd, W.GWL_EXSTYLE) & W.WS_EX_TRANSPARENT) == 0) Hide(it);
                }
                catch { }
            }
        }

        static void Hide(Item it)
        {
            W.ShowWindow(it.hwnd, 0);
            it.hidden = true;
        }

        public static void End()
        {
            List<Item> l = list;
            list = new List<Item>();
            foreach (Item it in l)
            {
                if (!W.IsWindow(it.hwnd)) continue;
                try
                {
                    if (it.hidden) W.ShowWindow(it.hwnd, 8);      // 前に出さずに表示し直す
                    if (it.changed)
                    {
                        if ((it.ex & W.WS_EX_TRANSPARENT) == 0)
                            W.SetWindowLong(it.hwnd, W.GWL_EXSTYLE, W.GetWindowLong(it.hwnd, W.GWL_EXSTYLE) & ~W.WS_EX_TRANSPARENT);
                        W.SetAlpha(it.hwnd, it.alpha);
                    }
                }
                catch { }
            }
        }
        public static void ForceEnd() { try { End(); } catch { } }
    }

    // ================================================================
    //  4. マニュアル作成
    //     記録中は、クリックしたウィンドウを撮って赤丸を付ける。同じ画面での続けてのクリックは1枚にまとめる
    //     打った文字・押したキーも記録する。終わると手順書（HTML）を作って開く
    //     保存先：デスクトップ「自動マニュアル作成」\yyyy-MM-dd_HHmm\手順書.html
    // ================================================================
    public static class Manual
    {
        public static bool Rec;
        static string dir;
        class Step { public string file, pre, kind, where; }
        class Tok { public string t, s; public int n; }
        class Part { public string t, s, kind; public int i; }
        class Grab { public Bitmap bmp; public string where; }
        class Sample { public byte[] buf; public int sw, sh, vx, vy; }
        class Page
        {
            public IntPtr root; public Grab g; public List<Point> marks = new List<Point>(); public List<Part> parts = new List<Part>();
            public Sample samp; public int lx, ly, t0; public string lastKind;
        }
        static List<Step> steps = new List<Step>();
        static List<Tok> tok = new List<Tok>();
        static bool imeComp;
        static Page page;
        static Badge badge;
        const double DiffPct = 0.3;     // 画面の何％が変わったら「動いた」とみなすか（クリックした所のまわりは数えない）

        public static void Toggle()
        {
            if (!Rec)
            {
                string bas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "自動マニュアル作成", DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
                dir = bas;
                int n = 2;
                while (Directory.Exists(dir)) dir = bas + "_" + (n++);
                Directory.CreateDirectory(dir);
                steps = new List<Step>(); tok = new List<Tok>(); imeComp = false; page = null;
                Rec = true;
                badge = new Badge();
                badge.Show();
                badge.SetCount(0);
                return;
            }
            Rec = false;
            W.PerMonitorDpi();
            SettlePage();
            if (HasText())
            {
                try
                {
                    IntPtr a = W.GetForegroundWindow();
                    if (a != IntPtr.Zero) Store(GrabWin(a, new List<Point>()), "", TokText());
                }
                catch { }
                tok = new List<Tok>();
            }
            try { badge.Close(); } catch { }
            badge = null;
            if (steps.Count == 0)
            {
                try { Directory.Delete(dir); } catch { }
                Notice.Show("記録した手順がないので終了しました");
                return;
            }
            Notice.Show("手順書を作っています…");
            try
            {
                string path = WriteHtml();
                Process.Start(path);
            }
            catch (Exception e)
            {
                Program.Log("手順書: " + e);
                MessageBox.Show("手順書を作れませんでした。\n" + e.Message, Program.AppName);
            }
        }

        // ---------- キーボード ----------
        public class KeySnapshot
        {
            public int vk, sc; public string mods; public bool shift; public string ch; public IntPtr fg;
        }

        public static string ModsNow(bool withShift)
        {
            string m = (W.KeyDown(0x11) ? "Ctrl+" : "") + (W.KeyDown(0x12) ? "Alt+" : "") + ((W.KeyDown(0x5B) || W.KeyDown(0x5C)) ? "Win+" : "");
            if (withShift && W.KeyDown(0x10)) m += "Shift+";
            return m;
        }

        // キーを押した瞬間の様子を控える（文字への変換もここで）
        public static KeySnapshot Snap(int vk, int sc)
        {
            KeySnapshot k = new KeySnapshot();
            k.vk = vk; k.sc = sc;
            k.mods = ModsNow(false);
            k.shift = W.KeyDown(0x10);
            k.fg = W.GetForegroundWindow();
            k.ch = "";
            if (k.mods == "")
            {
                try
                {
                    byte[] st = new byte[256];
                    if (k.shift) { st[0x10] = 0x80; st[0xA0] = 0x80; }
                    if ((W.GetKeyState(0x14) & 1) != 0) st[0x14] = 1;
                    int pid;
                    int tid = W.GetWindowThreadProcessId(k.fg, out pid);
                    IntPtr hkl = W.GetKeyboardLayout(tid);
                    StringBuilder sb = new StringBuilder(8);
                    int r = W.ToUnicodeEx(vk, sc, st, sb, sb.Capacity, 4, hkl);
                    if (r > 0) k.ch = sb.ToString(0, Math.Min(r, sb.Length));
                }
                catch { }
            }
            return k;
        }

        static bool ImeOn(IntPtr fg)
        {
            try
            {
                IntPtr ime = W.ImmGetDefaultIMEWnd(fg);
                if (ime == IntPtr.Zero) return false;
                IntPtr res;
                W.SendMessageTimeout(ime, 0x283, new IntPtr(5), IntPtr.Zero, 2, 200, out res);
                return res != IntPtr.Zero;
            }
            catch { return false; }
        }

        static bool IsPassword(IntPtr fg)
        {
            try
            {
                int pid;
                int tid = W.GetWindowThreadProcessId(fg, out pid);
                W.GUITHREADINFO gi = new W.GUITHREADINFO();
                gi.cbSize = Marshal.SizeOf(typeof(W.GUITHREADINFO));
                if (!W.GetGUIThreadInfo(tid, ref gi) || gi.hwndFocus == IntPtr.Zero) return false;
                return W.ClassOf(gi.hwndFocus).IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0
                    && (W.GetWindowLong(gi.hwndFocus, W.GWL_STYLE) & 0x20) != 0;
            }
            catch { return false; }
        }

        static void PushText(string type, string ch)
        {
            if (tok.Count > 0 && tok[tok.Count - 1].t == type) tok[tok.Count - 1].s += ch;
            else { Tok t = new Tok(); t.t = type; t.s = ch; t.n = 1; tok.Add(t); }
        }
        static void PushKey(string label)
        {
            if (tok.Count > 0 && tok[tok.Count - 1].t == "key" && tok[tok.Count - 1].s == label) tok[tok.Count - 1].n++;
            else { Tok t = new Tok(); t.t = "key"; t.s = label; t.n = 1; tok.Add(t); }
        }

        static string KeyName(int vk)
        {
            switch (vk)
            {
                case 0x09: return "Tab";
                case 0x1B: return "Escape";
                case 0x2E: return "Delete";
                case 0x2D: return "Insert";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PgUp";
                case 0x22: return "PgDn";
                case 0x26: return "Up";
                case 0x28: return "Down";
                case 0x25: return "Left";
                case 0x27: return "Right";
                case 0x08: return "Backspace";
                case 0x5D: return "AppsKey";
                case 0x20: return "Space";
            }
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            if ((vk >= 0x41 && vk <= 0x5A) || (vk >= 0x30 && vk <= 0x39)) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "Numpad" + (vk - 0x60);
            return "";
        }

        public static void OnKeyDown(KeySnapshot k)
        {
            if (!Rec) return;
            int vk = k.vk;
            // 記録しないキー（Ctrl・Shift・Alt・Win・CapsLock・NumLock・日本語入力の切り替えなど）
            if (vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C
                || vk == 0x14 || vk == 0x90 || vk == 0xF3 || vk == 0xF4 || vk == 0x19 || vk == 0x15 || vk == 0x1C || vk == 0x1D
                || vk == 0xF0 || vk == 0xF1 || vk == 0xF2 || vk == 0xE5) return;
            string name = KeyName(vk);
            if (imeComp)
            {
                // 変換中の Space・矢印・BackSpace などは記録しない
                if (name == "Escape") imeComp = false;
                if (name == "Space" || name == "Backspace" || name == "Up" || name == "Down" || name == "Left"
                    || name == "Right" || name == "Escape" || name == "Tab") return;
            }
            string mods = k.mods;
            if (mods == "" && name == "Backspace" && tok.Count > 0)
            {
                Tok t = tok[tok.Count - 1];
                if (t.t != "key" && t.s != "")
                {
                    t.s = t.s.Substring(0, t.s.Length - 1);     // 打ち間違いの修正は文字から消す
                    if (t.s == "") tok.RemoveAt(tok.Count - 1);
                    return;
                }
            }
            bool isF = name.Length >= 2 && name[0] == 'F' && char.IsDigit(name[1]);
            bool rec = name == "Tab" || name == "Escape" || name == "Delete" || name == "Insert" || name == "Home" || name == "End"
                || name == "PgUp" || name == "PgDn" || name == "Up" || name == "Down" || name == "Left" || name == "Right"
                || name == "Backspace" || name == "AppsKey";
            if (mods == "" && !isF && !rec)
            {
                // 普通の文字キー
                string ch = k.ch;
                if (ch == "" || ch[0] < 0x20) return;
                if (imeComp && ch == " ") return;              // 変換の Space
                if (IsPassword(k.fg)) PushText("pw", "●");
                else if (ImeOn(k.fg)) { imeComp = true; PushText("ime", ch); }   // 変換中（この後の Enter は確定とみなす）
                else PushText("text", ch);
                return;
            }
            if (name == "") return;
            string label;
            if (name == "Escape") label = "Esc";
            else if (name == "Up") label = "↑";
            else if (name == "Down") label = "↓";
            else if (name == "Left") label = "←";
            else if (name == "Right") label = "→";
            else if (name == "PgUp") label = "PageUp";
            else if (name == "PgDn") label = "PageDown";
            else if (name == "Backspace") label = "BackSpace";
            else label = name;
            if (k.shift && mods != "") mods += "Shift+";
            PushKey(mods + label);
        }

        static bool HasText()
        {
            foreach (Tok t in tok) if (t.t != "key" && t.t != "raw") return true;
            return false;
        }

        static string TokText()
        {
            StringBuilder s = new StringBuilder();
            foreach (Tok t in tok)
            {
                string p;
                if (t.t == "text") p = "「" + t.s + "」と入力";
                else if (t.t == "ime") p = "日本語を入力（ローマ字: " + t.s + "）";
                else if (t.t == "raw") p = t.s;
                else if (t.t == "pw") p = "（パスワード欄に入力）";
                else p = t.n > 1 ? t.s + "×" + t.n : t.s;
                if (s.Length > 0) s.Append(" → ");
                s.Append(p);
            }
            return s.ToString();
        }

        // Enter：押す前の画面を撮ってから Enter を送る
        public static void OnEnter(bool numpad, string mods)
        {
            if (!Rec) { W.SendKey(0x0D, numpad); return; }
            if (imeComp)                       // 日本語の確定
            {
                imeComp = false;
                W.SendKey(0x0D, numpad);
                return;
            }
            if (mods != "" || !HasText())
            {
                PushKey(mods + "Enter");
                W.SendKey(0x0D, numpad);
                return;
            }
            Grab g = null;
            try
            {
                W.PerMonitorDpi();
                SettlePage();
                g = GrabWin(W.GetForegroundWindow(), new List<Point>());   // Enter が効く前の画面
            }
            catch { }
            W.SendKey(0x0D, numpad);
            try { Store(g, "Enter", TokText()); } catch { }
            tok = new List<Tok>();
        }

        // ---------- マウス ----------
        public static void OnClick(string kind, int mx, int my, IntPtr hw, int t)
        {
            if (!Rec) return;
            try
            {
                W.PerMonitorDpi();
                IntPtr root = W.GetAncestor(hw, 2);
                if (root == IntPtr.Zero) root = hw;
                if (root == IntPtr.Zero) return;
                if (badge != null && root == badge.Handle) return;
                if (IsTitleBar(root, mx, my)) return;             // 手前に出すだけのクリックは記録しない
                Page p = page;
                if (p != null && kind == "左クリック" && p.lastKind == "左クリック" && t - p.t0 < 400
                    && Math.Abs(mx - p.lx) < 8 && Math.Abs(my - p.ly) < 8)
                {
                    p.parts[p.parts.Count - 1].kind = "ダブルクリック";   // ダブルクリックは1つにまとめる
                    p.lastKind = "ダブルクリック";
                    return;
                }
                Sample now = TakeSample();
                bool moved = p != null && Diff(p.samp, now, p.lx, p.ly);
                if (p != null && !moved && root == p.root)
                {
                    // 画面が動かないまま同じウィンドウをクリック → 同じ1枚に赤丸を足す
                    p.marks.Add(new Point(mx, my));
                    if (tok.Count > 0)
                    {
                        Part tp = new Part(); tp.t = "tok"; tp.s = TokText(); p.parts.Add(tp);
                        tok = new List<Tok>();
                    }
                    Part cp = new Part(); cp.t = "click"; cp.i = p.marks.Count; cp.kind = kind; p.parts.Add(cp);
                    Grab g = GrabWin(root, p.marks);              // 今の画面に撮り直して、赤丸を全部描き直す
                    if (g != null)
                    {
                        if (p.g != null) p.g.bmp.Dispose();
                        p.g = g;
                    }
                    p.samp = now; p.lx = mx; p.ly = my; p.t0 = t; p.lastKind = kind;
                }
                else
                {
                    if (p != null) ClosePage(moved);              // 前の画面を1枚として確定（または捨てる）
                    List<Point> marks = new List<Point>();
                    marks.Add(new Point(mx, my));
                    Grab g = GrabWin(root, marks);
                    if (g != null)
                    {
                        Page np = new Page();
                        np.root = root; np.g = g; np.marks = marks; np.samp = now; np.lx = mx; np.ly = my; np.t0 = t; np.lastKind = kind;
                        if (tok.Count > 0)
                        {
                            Part tp = new Part(); tp.t = "tok"; tp.s = TokText(); np.parts.Add(tp);
                            tok = new List<Tok>();
                        }
                        Part cp = new Part(); cp.t = "click"; cp.i = 1; cp.kind = kind; np.parts.Add(cp);
                        page = np;
                    }
                }
            }
            catch (Exception e) { Program.Log("記録（クリック）: " + e.Message); }
        }

        // タイトルバー（中身より上で、メニューバーと右上のボタン以外）をクリックしたか
        static bool IsTitleBar(IntPtr hwnd, int x, int y)
        {
            try
            {
                W.RECT fr = W.FrameRect(hwnd);
                W.POINT pt = new W.POINT();
                W.ClientToScreen(hwnd, ref pt);
                int capB = pt.Y;                                   // 中身の上端
                IntPtr hm = W.GetMenu(hwnd);
                if (hm != IntPtr.Zero)
                {
                    W.RECT r;
                    if (W.GetMenuItemRect(hwnd, hm, 0, out r) && r.Top > fr.Top && r.Top < capB) capB = r.Top;
                }
                if (y < fr.Top || y >= capB || x < fr.Left || x >= fr.Right) return false;
                return x < fr.Right - (capB - fr.Top) * 5;        // 右上のボタンの辺りは記録する
            }
            catch { return false; }
        }

        // 開いている1枚を閉じる。画面が動いた／赤丸が2つ以上 → 手順として残す。それ以外 → 写真は捨てて説明だけ次へ回す
        static void ClosePage(bool moved)
        {
            Page p = page;
            page = null;
            if (p == null) return;
            if (moved || p.marks.Count >= 2) Store(p.g, "", PageCaption(p));
            else
            {
                if (p.g != null) p.g.bmp.Dispose();
                Tok t = new Tok(); t.t = "raw"; t.s = PageCaption(p); t.n = 1;
                tok.Insert(0, t);
            }
        }

        // Enter・PrintScreen・終了の前に、開いている1枚を今の画面で決める
        static void SettlePage()
        {
            if (page == null) return;
            W.PerMonitorDpi();
            ClosePage(Diff(page.samp, TakeSample(), page.lx, page.ly));
        }

        static string PageCaption(Page p)
        {
            bool multi = p.marks.Count >= 2;
            StringBuilder s = new StringBuilder();
            foreach (Part it in p.parts)
            {
                string txt = it.t == "tok" ? it.s : (multi ? Circ(it.i) : "") + it.kind;
                if (s.Length > 0) s.Append(" → ");
                s.Append(txt);
            }
            return s.ToString();
        }
        static string Circ(int i) { return i <= 20 ? ((char)(0x245F + i)).ToString() : "(" + i + ")"; }   // ①〜⑳

        // 画面全体を 1/8 に縮めて取り込む（動いたかの比較用）
        static Sample TakeSample()
        {
            Rectangle v = W.VirtualScreen();
            Sample s = new Sample();
            s.sw = Math.Max(1, v.Width / 8); s.sh = Math.Max(1, v.Height / 8); s.vx = v.X; s.vy = v.Y;
            W.BITMAPINFOHEADER bi = new W.BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = s.sw; bi.biHeight = -s.sh; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr hdcS = W.GetDC(IntPtr.Zero);
            IntPtr hdcM = W.CreateCompatibleDC(hdcS);
            IntPtr bits;
            IntPtr hbm = W.CreateDIBSection(hdcM, ref bi, 0, out bits, IntPtr.Zero, 0);
            IntPtr obm = W.SelectObject(hdcM, hbm);
            W.SetStretchBltMode(hdcM, 4);
            W.SetBrushOrgEx(hdcM, 0, 0, IntPtr.Zero);
            W.StretchBlt(hdcM, 0, 0, s.sw, s.sh, hdcS, v.X, v.Y, v.Width, v.Height, 0x40CC0020);
            s.buf = new byte[s.sw * s.sh * 4];
            Marshal.Copy(bits, s.buf, 0, s.buf.Length);
            W.SelectObject(hdcM, obm);
            W.DeleteObject(hbm);
            W.DeleteDC(hdcM);
            W.ReleaseDC(IntPtr.Zero, hdcS);
            return s;
        }

        // 2つの取り込みを比べて、クリックした所のまわり以外が DiffPct ％以上変わっていたら true
        static bool Diff(Sample a, Sample b, int mx, int my)
        {
            if (a == null || b == null) return true;
            if (a.sw != b.sw || a.sh != b.sh) return true;
            int ex = (mx - a.vx) / 8, ey = (my - a.vy) / 8, r = 8;     // まわり約64ピクセルは数えない
            double need = Math.Max(20, a.sw * a.sh * DiffPct / 100);
            int cnt = 0;
            for (int y = 0; y < a.sh; y++)
            {
                bool nearY = Math.Abs(y - ey) <= r;
                int off = y * a.sw * 4;
                for (int x = 0; x < a.sw; x++)
                {
                    if (nearY && Math.Abs(x - ex) <= r) continue;
                    int o = off + x * 4;
                    int d = Math.Abs(a.buf[o] - b.buf[o]) + Math.Abs(a.buf[o + 1] - b.buf[o + 1]) + Math.Abs(a.buf[o + 2] - b.buf[o + 2]);
                    if (d > 40 && ++cnt >= need) return true;
                }
            }
            return false;
        }

        public static void AddActive()
        {
            if (!Rec) return;
            try
            {
                W.PerMonitorDpi();
                SettlePage();
                IntPtr a = W.GetForegroundWindow();
                if (a != IntPtr.Zero)
                {
                    Store(GrabWin(a, new List<Point>()), "画面を記録", TokText());
                    tok = new List<Tok>();
                }
            }
            catch (Exception e) { Program.Log("記録（PrintScreen）: " + e.Message); }
        }

        // 画面を取り込む（クリックした所に赤丸。2つ以上なら番号も）
        static Grab GrabWin(IntPtr hwnd, List<Point> marks)
        {
            if (hwnd == IntPtr.Zero) return null;
            W.RECT fr = W.FrameRect(hwnd);
            Rectangle rc = Rectangle.FromLTRB(fr.Left, fr.Top, fr.Right, fr.Bottom);
            IntPtr info = hwnd;
            string cls = W.ClassOf(hwnd);
            if (cls == "#32768" || cls == "ComboLBox")      // 開いたメニュー・一覧は元の画面と合わせて撮る
            {
                IntPtr a = W.GetForegroundWindow();
                if (a != IntPtr.Zero && a != hwnd)
                {
                    W.RECT ra = W.FrameRect(a);
                    rc = Rectangle.Union(rc, Rectangle.FromLTRB(ra.Left, ra.Top, ra.Right, ra.Bottom));
                    info = a;
                }
            }
            rc = Rectangle.Intersect(rc, W.VirtualScreen());
            if (rc.Width < 10 || rc.Height < 10) return null;
            Bitmap b = Shot.Capture(rc);
            if (marks.Count > 0)
            {
                using (Graphics g = Graphics.FromImage(b))
                using (Pen pen = new Pen(Color.Red, 4))
                using (SolidBrush red = new SolidBrush(Color.Red))
                using (Font f = new Font("Meiryo UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat())
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    bool multi = marks.Count >= 2;
                    for (int i = 0; i < marks.Count; i++)
                    {
                        int cx = marks[i].X - rc.Left, cy = marks[i].Y - rc.Top, rr = 22;
                        g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2);
                        if (multi)
                        {
                            int bx = cx + 20, by = cy - 20, br = 13;      // 右上に赤い丸と白い番号
                            g.FillEllipse(red, bx - br, by - br, br * 2, br * 2);
                            g.DrawString((i + 1).ToString(), f, Brushes.White, new RectangleF(bx - br, by - br, br * 2, br * 2), sf);
                        }
                    }
                }
            }
            Grab gr = new Grab();
            gr.bmp = b;
            string title = W.TitleOf(info);
            gr.where = title != "" ? title : W.ProcessNameOf(info);
            return gr;
        }

        // 取り込んだ画面をPNGにして手順に追加（pre＝それまでに打った内容）
        static void Store(Grab g, string kind, string pre)
        {
            if (g == null) return;
            string file = (steps.Count + 1).ToString("000") + ".png";
            try { g.bmp.Save(Path.Combine(dir, file), ImageFormat.Png); }
            catch { g.bmp.Dispose(); return; }
            g.bmp.Dispose();
            Step s = new Step(); s.file = file; s.pre = pre; s.kind = kind; s.where = g.where;
            steps.Add(s);
            if (badge != null) badge.SetCount(steps.Count);
        }

        static string Esc(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        static string WriteHtml()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Step s in steps)
            {
                string act = s.pre;
                if (s.kind != "") act += (act == "" ? "" : " → ") + s.kind;
                string where = s.where != "" ? "<span class=\"where\">（" + Esc(s.where) + "）</span>" : "";
                sb.Append("<section class=\"step\"><div class=\"head\"><span class=\"no\"></span>");
                sb.Append("<span class=\"auto\"><span class=\"act\">" + Esc(act) + "</span>" + where + "</span>");
                sb.Append("<button class=\"del\" title=\"この手順を消す\">×</button></div>");
                sb.Append("<div class=\"desc\" contenteditable=\"true\"></div>");
                sb.Append("<img src=\"data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(dir, s.file))) + "\"></section>\n");
            }
            string html = ManualTemplate.Html.Replace("{{DATE}}", DateTime.Now.ToString("yyyy年M月d日")).Replace("{{STEPS}}", sb.ToString());
            string path = Path.Combine(dir, "手順書.html");
            File.WriteAllText(path, html, new UTF8Encoding(true));
            return path;
        }

        // ---------- 記録中の目印（右上の赤い帯。スクショには写らない） ----------
        class Badge : Form
        {
            Label lab;
            public Badge()
            {
                float s = W.GetDpiForSystem() / 96f;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.FromArgb(192, 0, 0);
                Opacity = 0.9;
                lab = new Label();
                lab.AutoSize = true;
                lab.ForeColor = Color.White;
                lab.Font = new Font("Meiryo UI", 13f * s, FontStyle.Bold, GraphicsUnit.Pixel);
                lab.Padding = new Padding((int)(10 * s), (int)(5 * s), (int)(10 * s), (int)(5 * s));
                Controls.Add(lab);
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Rectangle wa = Screen.PrimaryScreen.Bounds;
                Location = new Point(wa.Right - (int)(420 * s), wa.Top + (int)(8 * s));
            }
            public void SetCount(int n)
            {
                lab.Text = "● 記録中 " + n + "枚　（右Ctrl か 右Alt を2回で終了）";
            }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x08000000 | 0x80 | 0x8 | 0x20;
                    return cp;
                }
            }
            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                try { W.SetWindowDisplayAffinity(Handle, 0x11); } catch { }   // スクショに写さない
            }
        }
    }

    // 手順書（HTML）のひな形
    public static class ManualTemplate
    {
        public const string Html = @"<!DOCTYPE html>
<html lang=""ja""><head><meta charset=""utf-8"">
<title>手順書</title>
<style>
body{font-family:""Meiryo UI"",""Yu Gothic UI"",sans-serif;max-width:1000px;margin:24px auto;padding:0 16px;color:#222;counter-reset:st}
h1{font-size:24px;border-bottom:3px solid #1f3a5f;padding-bottom:6px;outline:none}
h1:empty:before{content:""（ここに手順書の題名を入力）"";color:#999}
.meta{color:#666;font-size:13px}
.bar{position:sticky;top:0;background:#f4f6f9;border:1px solid #d5dbe3;padding:8px 12px;margin:12px 0 20px;border-radius:6px;font-size:13px;z-index:5}
.bar button{font-size:13px;padding:4px 12px;margin-right:6px;cursor:pointer}
.step{margin:0 0 28px;page-break-inside:avoid;counter-increment:st}
.head{display:flex;align-items:center;gap:10px;margin-bottom:6px}
.no:before{content:""手順 "" counter(st);background:#1f3a5f;color:#fff;font-weight:bold;padding:2px 10px;border-radius:4px;white-space:nowrap}
.auto{flex:1;font-size:14px}
.act{color:#1f3a5f;font-weight:bold}
.where{color:#888;font-size:12px;margin-left:6px}
.del{border:1px solid #ccc;background:#fff;color:#999;cursor:pointer;border-radius:4px}
.del:hover{color:#c00;border-color:#c00}
.desc{min-height:1.6em;padding:6px 8px;border:1px dashed #bbb;background:#fafafa;margin-bottom:8px;font-size:15px;outline:none}
.desc:empty:before{content:""ここに説明を書く（クリックで入力）"";color:#aaa}
.step img{max-width:100%;border:1px solid #ccc;box-shadow:0 1px 4px rgba(0,0,0,.15)}
@media print{.bar,.del{display:none}.desc{border:none;background:none;padding:0}.desc:empty{display:none}h1:empty{display:none}}
</style></head><body>
<h1 contenteditable=""true""></h1>
<div class=""meta"">作成日 {{DATE}}　／　全 <span id=""cnt""></span> 手順</div>
<div class=""bar"">
<button id=""save"">書いた内容を保存</button><button onclick=""window.print()"">印刷・PDF</button>
<span id=""msg"">グレーの点線の欄に説明を書けます。いらない手順は × で消せます。</span>
</div>
{{STEPS}}
<script>
function cnt(){document.getElementById(""cnt"").textContent=document.querySelectorAll("".step"").length;}
cnt();
document.addEventListener(""click"",function(e){
 if(e.target.classList.contains(""del"")&&confirm(""この手順を消しますか？"")){e.target.closest("".step"").remove();cnt();}
});
var h1=document.querySelector(""h1"");
h1.addEventListener(""input"",function(){document.title=h1.textContent.trim()||""手順書"";});
document.getElementById(""save"").addEventListener(""click"",async function(){
 var html=""<!DOCTYPE html>\n""+document.documentElement.outerHTML;
 var name=(h1.textContent.trim()||""手順書"")+"".html"";
 var msg=document.getElementById(""msg"");
 try{
  if(window.showSaveFilePicker){
   var fh=await showSaveFilePicker({suggestedName:name,types:[{description:""HTML"",accept:{""text/html"":["".html""]}}]});
   var w=await fh.createWritable();await w.write(html);await w.close();
  }else{
   var a=document.createElement(""a"");a.href=URL.createObjectURL(new Blob([html],{type:""text/html""}));a.download=name;a.click();
  }
  msg.textContent=""保存しました。"";
 }catch(err){msg.textContent=""保存をやめました。"";}
});
</script>
</body></html>";
    }

    // ================================================================
    //  5. アプリ画面（スイッチの画面）。中身は app.html
    // ================================================================
    public class SettingsForm : Form
    {
        WebBrowser web;
        System.Windows.Forms.Timer poll;
        string last = "";

        public SettingsForm()
        {
            float s = W.GetDpiForSystem() / 96f;
            Text = Program.AppName;
            Icon = Host.I.AppIcon;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size((int)(520 * s), (int)(660 * s));
            KeyPreview = true;
            KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };

            // IE の部品を新しい表示方法（IE11）で動かす設定（このPCの自分のユーザーだけ）
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                    k.SetValue(Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName), 11001, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { }

            web = new WebBrowser();
            web.Dock = DockStyle.Fill;
            web.ScriptErrorsSuppressed = true;
            web.IsWebBrowserContextMenuEnabled = false;
            web.WebBrowserShortcutsEnabled = false;
            web.AllowWebBrowserDrop = false;
            web.DocumentCompleted += delegate
            {
                // 画面の拡大率（125%・150%など）に合わせて中身も大きくする
                try
                {
                    object ax = web.ActiveXInstance;
                    ax.GetType().InvokeMember("ExecWB", BindingFlags.InvokeMethod, null, ax,
                        new object[] { 63, 2, (int)Math.Round(s * 100), null });
                }
                catch { }
            };
            Controls.Add(web);
            LoadPage();

            poll = new System.Windows.Forms.Timer();
            poll.Interval = 250;
            poll.Tick += delegate { Poll(); };
            poll.Start();
            FormClosed += delegate { poll.Stop(); };
        }

        public void LoadPage()
        {
            string path = Path.Combine(Program.SysDir, "app.html");
            string html;
            try { html = File.ReadAllText(path, Encoding.UTF8); }
            catch { html = "<html><body>app.html が見つかりません。</body></html>"; }
            last = SousaKaizen.Settings.StateString();
            web.DocumentText = html.Replace("__STATE__", last).Replace("__VERSION__", Program.Version);
        }

        // 画面のスイッチが切り替わったかを見る（0.25秒ごと）
        void Poll()
        {
            string s;
            try
            {
                if (web.Document == null) return;
                object r = web.Document.InvokeScript("getState");
                s = r as string;
            }
            catch { return; }
            if (string.IsNullOrEmpty(s) || s == last || s == "__STATE__") return;
            last = s;
            foreach (string part in s.Split(';'))
            {
                string[] kv = part.Split('=');
                if (kv.Length != 2 || !SousaKaizen.Settings.Feat.ContainsKey(kv[0])) continue;
                bool on = kv[1] == "1";
                if (SousaKaizen.Settings.On(kv[0]) == on) continue;
                SousaKaizen.Settings.Set(kv[0], on);
                Host.I.OnFeatureChanged(kv[0], on);
            }
        }
    }

    // ================================================================
    //  自動更新：app.cs を上書きしたら組み立て直して入れ替わる。app.html なら画面だけ読み直す
    //  （書き間違いで組み立てられないときは、今の版のまま動き続ける）
    // ================================================================
    public static class Updater
    {
        static DateTime csTime, htmlTime, csFailed;

        static DateTime T(string f) { try { return File.GetLastWriteTimeUtc(Path.Combine(Program.SysDir, f)); } catch { return DateTime.MinValue; } }

        public static void Init() { csTime = T("app.cs"); htmlTime = T("app.html"); }

        public static void Check()
        {
            // 試し用の合図：9_AI作業用 に「試し_入れ替え.txt」「試し_スクショ.txt」が置かれたら、その機能を1回動かして消す
            try
            {
                string sig = Path.Combine(Program.SysDir, "試し_入れ替え.txt");
                if (File.Exists(sig)) { File.Delete(sig); Swap.Run(); }
                string sig2 = Path.Combine(Program.SysDir, "試し_スクショ.txt");
                if (File.Exists(sig2)) { File.Delete(sig2); Shot.Run(); }
                string sig3 = Path.Combine(Program.SysDir, "試し_更新確認.txt");
                if (File.Exists(sig3)) { File.Delete(sig3); Online.CheckNow(true); }
            }
            catch (Exception e) { Program.Log("試し入れ替え: " + e.Message); }

            DateTime h = T("app.html");
            if (h != htmlTime)
            {
                htmlTime = h;
                try
                {
                    if (Host.I.Settings != null && !Host.I.Settings.IsDisposed) Host.I.Settings.LoadPage();
                }
                catch { }
            }
            DateTime c = T("app.cs");
            if (c == csTime || c == csFailed) return;
            if ((DateTime.UtcNow - c).TotalSeconds < 1) return;      // 書き込みが終わるのを待つ
            if (Manual.Rec) return;                                   // マニュアルの記録中は待つ
            string err = TryCompile();
            if (err != null)
            {
                csFailed = c;
                Program.Log("新しい版を組み立てられませんでした:\r\n" + err);
                Notice.Show("新しい版に書き間違いがあるので、今の版のまま動かしています");
                return;
            }
            csTime = c;
            bool open = Host.I.Settings != null && !Host.I.Settings.IsDisposed && Host.I.Settings.Visible;
            Settings.Write("app", "reopen", open ? "1" : "0");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Program.PowerShellExe, Program.PsArgs("/restart"));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Program.SysDir;
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception e) { Program.Log("入れ替え: " + e.Message); }
        }

        // 新しい app.cs が組み立てられるか確かめる（PowerShell の Add-Type と同じ部品を使う）
        static string TryCompile() { return TryCompile(Path.Combine(Program.SysDir, "app.cs")); }
        public static string TryCompile(string path)
        {
            try
            {
                string code = File.ReadAllText(path, Encoding.UTF8);
                using (Microsoft.CSharp.CSharpCodeProvider cp = new Microsoft.CSharp.CSharpCodeProvider())
                {
                    System.CodeDom.Compiler.CompilerParameters p = new System.CodeDom.Compiler.CompilerParameters();
                    p.GenerateInMemory = true;
                    p.ReferencedAssemblies.Add("System.dll");
                    p.ReferencedAssemblies.Add("System.Windows.Forms.dll");
                    p.ReferencedAssemblies.Add("System.Drawing.dll");
                    p.ReferencedAssemblies.Add("System.Core.dll");     // PowerShell の Add-Type と同じものを使う
                    System.CodeDom.Compiler.CompilerResults r = cp.CompileAssemblyFromSource(p, code);
                    if (!r.Errors.HasErrors) return null;
                    StringBuilder sb = new StringBuilder();
                    foreach (System.CodeDom.Compiler.CompilerError e in r.Errors)
                        if (!e.IsWarning) sb.AppendLine(e.Line + "行目: " + e.ErrorText);
                    return sb.ToString();
                }
            }
            catch (Exception e) { return e.Message; }
        }
    }

    // ================================================================
    //  インターネット（GitHub）からの自動更新
    //  ・起動して1分後と、そのあと6時間ごとに、GitHub の「配る用の版」（stable）が新しくなっていないかを見る
    //  ・新しい版があれば、その版のファイルを取ってきて、組み立てられるのを確かめてから入れ替える
    //    （入れ替えると、上の Updater が気づいて新しい版で起動し直す）
    //  ・9_AI作業用 に「開発用.txt」がある場所（作っている人のパソコン）では、勝手に更新しない
    // ================================================================
    public static class Online
    {
        const string Repo = "kusaka-daichi/sousa-kakucho-app";
        const string Branch = "stable";      // 配る用の版。作業中の版（main）は配られない
        static readonly string[] Files = new string[] { "app.html", "app.ico", "app.ps1", "version.txt", "app.cs" };   // app.cs は最後
        static System.Windows.Forms.Timer timer;
        static bool busy;

        static bool DevMode { get { return File.Exists(Path.Combine(Program.SysDir, "開発用.txt")); } }

        public static void Init()
        {
            // 更新したあとの初めての起動なら、お知らせを出す
            try
            {
                string done = Settings.Read("app", "updated", "");
                if (done != "")
                {
                    Settings.Write("app", "updated", "");
                    Host.I.BeginInvoke(new MethodInvoker(delegate { Notice.Show(Program.AppName + " が新しい版（" + done + "）になりました"); }));
                }
            }
            catch { }
            if (DevMode) return;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 60 * 1000;
            timer.Tick += delegate { timer.Interval = 6 * 60 * 60 * 1000; CheckNow(false); };
            timer.Start();
        }

        // onlyCheck = true のときは、見に行った結果を log.txt に書くだけ（入れ替えない）
        public static void CheckNow(bool onlyCheck)
        {
            if (busy) return;
            busy = true;
            Thread th = new Thread(delegate()
            {
                try { Work(onlyCheck); }
                catch (Exception e) { Program.Log("更新の確認: " + e.Message); }
                finally { busy = false; }
            });
            th.IsBackground = true;
            th.Start();
        }

        static WebClient Client()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }   // TLS 1.2
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "sousa-kakucho-app";
            wc.Encoding = Encoding.UTF8;
            return wc;
        }

        static string Raw(string f) { return "https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/app/" + f; }

        static Version V(string s)
        {
            Version v;
            s = (s ?? "").Trim().TrimStart('v', 'V');
            return Version.TryParse(s, out v) ? v : new Version(0, 0, 0);
        }

        static void Work(bool onlyCheck)
        {
            string tag;
            using (WebClient wc = Client())
                tag = wc.DownloadString(Raw("version.txt") + "?t=" + DateTime.UtcNow.Ticks).Trim();
            if (!Regex.IsMatch(tag, "^[0-9]+(\\.[0-9]+){1,3}$")) throw new Exception("最新版の番号が読めませんでした");
            Version latest = V(tag), mine = V(Program.Version);
            if (onlyCheck)
            {
                Program.Log("更新の確認：GitHub につながりました。最新版 " + tag + "／この版 " + Program.Version + (DevMode ? "（開発用なので自動更新はしません）" : ""));
                Host.I.BeginInvoke(new MethodInvoker(delegate { Notice.Show("GitHub につながりました（最新版 " + tag + "）"); }));
                return;
            }
            if (latest <= mine) return;

            // その版のファイルを一時フォルダに取ってくる
            string tmp = Path.Combine(Path.GetTempPath(), "sousa-kakucho-update-" + tag);
            Directory.CreateDirectory(tmp);
            using (WebClient wc = Client())
                foreach (string f in Files)
                    wc.DownloadFile(Raw(f) + "?t=" + DateTime.UtcNow.Ticks, Path.Combine(tmp, f));
            if (V(File.ReadAllText(Path.Combine(tmp, "version.txt"))) != latest) { Program.Log("更新の途中で版が変わったので、次の機会にします"); return; }
            string err = Updater.TryCompile(Path.Combine(tmp, "app.cs"));
            if (err != null) { Program.Log("新しい版（" + tag + "）を組み立てられないので更新しませんでした:\r\n" + err); return; }

            // 本体側で入れ替える（マニュアルの記録中は待つ）
            Host.I.BeginInvoke(new MethodInvoker(delegate { Apply(tmp, latest.ToString()); }));
        }

        static void Apply(string tmp, string ver)
        {
            if (Manual.Rec) { Thread t = new Thread(delegate() { Thread.Sleep(60000); Host.I.BeginInvoke(new MethodInvoker(delegate { Apply(tmp, ver); })); }); t.IsBackground = true; t.Start(); return; }
            try
            {
                Settings.Write("app", "updated", ver);
                foreach (string f in Files)
                    File.Copy(Path.Combine(tmp, f), Path.Combine(Program.SysDir, f), true);   // app.cs を最後に入れ替える → 自動で起動し直す
            }
            catch (Exception e) { Program.Log("更新の入れ替え: " + e.Message); }
        }
    }

    // ================================================================
    //  準備：ショートカット作成・AutoHotkey 版からの切り替え
    // ================================================================
    public static class Setup
    {
        static string IconPath { get { return Path.Combine(Program.SysDir, "app.ico"); } }

        public static void EnsureShortcuts()
        {
            try
            {
                if (!File.Exists(IconPath))
                    using (FileStream fs = File.Create(IconPath)) Host.I.AppIcon.Save(fs);
            }
            catch { }
            string desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Program.AppName + ".lnk");
            string startup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), Program.AppName + ".lnk");
            // デスクトップ：なければ作る。AutoHotkey 版や別の場所を指していれば作り直す
            if (!File.Exists(desk) || !LinkPointsHere(desk)) MakeLink(desk, Program.PsArgs("/open"));
            // スタートアップ：パソコンを起動したら自動で動く
            if (!File.Exists(startup) || !LinkPointsHere(startup)) MakeLink(startup, Program.PsArgs(""));
            // 一時期の名前（操作性拡張アプリ）のショートカットは片付ける
            foreach (string old in new string[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "操作性拡張アプリ.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "操作性拡張アプリ.lnk") })
            {
                try { if (File.Exists(old) && IsOurOrAhkLink(old)) File.Delete(old); } catch { }
            }
        }

        static string LinkText(string lnk)
        {
            object sh = Shell();
            object s = sh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            return (Get(s, "TargetPath") as string ?? "") + " " + (Get(s, "Arguments") as string ?? "");
        }
        static bool LinkPointsHere(string lnk)
        {
            try { return LinkText(lnk).IndexOf(Path.Combine(Program.SysDir, "app.ps1"), StringComparison.OrdinalIgnoreCase) >= 0; }
            catch { return false; }
        }
        static bool IsOurOrAhkLink(string lnk)
        {
            try
            {
                string t = LinkText(lnk);
                return t.IndexOf("app.ps1", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf(".ahk", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("AutoHotkey", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        static object Shell() { return Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); }
        static object Get(object o, string p) { return o.GetType().InvokeMember(p, BindingFlags.GetProperty, null, o, null); }
        static void Put(object o, string p, object v) { o.GetType().InvokeMember(p, BindingFlags.SetProperty, null, o, new object[] { v }); }

        static void MakeLink(string lnk, string args)
        {
            object sh = Shell();
            object s = sh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Put(s, "TargetPath", Program.PowerShellExe);
            Put(s, "Arguments", args);
            Put(s, "WorkingDirectory", Program.SysDir);
            Put(s, "Description", Program.AppName);
            Put(s, "WindowStyle", 7);
            if (File.Exists(IconPath)) Put(s, "IconLocation", IconPath + ",0");
            s.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, s, null);
        }


        // AutoHotkey 版（緑の H）が動いていたら、止めて切り替えるか聞く
        public static void CheckAutoHotkey()
        {
            List<IntPtr> ahk = new List<IntPtr>();
            W.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (W.ClassOf(h) == "AutoHotkey" && W.TitleOf(h).IndexOf("操作性改善.ahk", StringComparison.OrdinalIgnoreCase) >= 0) ahk.Add(h);
                return true;
            }, IntPtr.Zero);
            List<string> links = AhkStartupLinks();
            if (ahk.Count == 0 && links.Count == 0) return;
            DialogResult r = MessageBox.Show(
                "AutoHotkey 版（緑の「H」）の操作性改善が見つかりました。\n" +
                "両方が動くと、キーが二重に反応してしまいます。\n\n" +
                "AutoHotkey 版を止めて、こちら（自作版）に切り替えますか？\n" +
                "（パソコン起動時の自動起動も外します。ファイルは消さずに残します）",
                Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            foreach (IntPtr h in ahk) W.PostMessage(h, 0x111, new IntPtr(65307), IntPtr.Zero);   // 緑の H の「Exit」と同じ
            string bak = Path.Combine(Settings.Dir, "AutoHotkey版のショートカット");
            foreach (string l in links)
            {
                try
                {
                    Directory.CreateDirectory(bak);
                    string to = Path.Combine(bak, Path.GetFileName(l));
                    if (File.Exists(to)) File.Delete(to);
                    File.Move(l, to);
                }
                catch (Exception e) { Program.Log("自動起動の取り外し: " + e.Message); }
            }
            Notice.Show("AutoHotkey 版を止めて、自作版に切り替えました");
        }

        static List<string> AhkStartupLinks()
        {
            List<string> res = new List<string>();
            try
            {
                foreach (string l in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "*.lnk"))
                {
                    try
                    {
                        object sh = Shell();
                        object s = sh.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { l });
                        string t = (Get(s, "TargetPath") as string ?? "") + " " + (Get(s, "Arguments") as string ?? "");
                        if (t.IndexOf("操作性改善.ahk", StringComparison.OrdinalIgnoreCase) >= 0) res.Add(l);
                    }
                    catch { }
                }
            }
            catch { }
            return res;
        }
    }
}

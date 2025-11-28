using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CalendarLite.Services
{
    /// <summary>
    /// 低级鼠标钩子，拦截点击系统托盘钟表区域，并显示自定义日历。
    /// </summary>
    public class TrayClockInterceptor
    {
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelMouseProc? _proc;
        private Action? _onClockClick;

        public void Start(Action onClockClick)
        {
            _onClockClick = onClockClick;
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            if (_hook == IntPtr.Zero)
            {
                Logger.Warn("TrayClockInterceptor hook install failed");
            }
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 低级鼠标钩子回调：在鼠标左键按下时检测并拦截系统日历，防止原生日历弹出；
        /// 命中后触发自定义日历显示。
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                // 在按下事件拦截，阻止系统原生日历处理该点击
                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONDBLCLK)
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (IsClockHit(data.pt.x, data.pt.y))
                    {
                        try { _onClockClick?.Invoke(); } catch (Exception ex) { Logger.Error("ClockClick handler failed", ex); }
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        /// <summary>
        /// 判断坐标是否在任务栏“日期/时间”控件范围内。
        /// - 优先命中 Windows 10 的 TrayClockWClass 与 Windows 11 的 ClockButton。
        /// - 若无法获取时钟控件句柄，则使用窄幅兜底：托盘右侧 160px 近似为日期区域，避免误触其它图标。
        /// </summary>
        private bool IsInClockRect(int x, int y)
        {
            var tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero) return false;
            var clock = FindWindowEx(tray, IntPtr.Zero, "TrayClockWClass", null);
            if (clock == IntPtr.Zero)
            {
                // Windows 11 可能使用 ClockButton 类名，递归查找
                clock = FindChildByClass(tray, "ClockButton");
            }
            if (clock != IntPtr.Zero && GetWindowRect(clock, out RECT rcClock))
            {
                var hit = x >= rcClock.Left && x <= rcClock.Right && y >= rcClock.Top && y <= rcClock.Bottom;
                if (hit) Logger.Info($"Hit clock rect: {rcClock.Left},{rcClock.Top}-{rcClock.Right},{rcClock.Bottom}");
                return hit;
            }
            // 未找到时钟控件，使用窄幅兜底近似日期区域，尽量避免误触其它托盘图标
            if (GetWindowRect(tray, out RECT rcTray))
            {
                int fallbackWidth = 115; // 日期区域近似宽度
                int rightMargin = 52;    // 右侧通知栏及溢出区预留
                int rightBound = Math.Max(rcTray.Left, rcTray.Right - rightMargin);
                int leftBound = Math.Max(rcTray.Left, rightBound - fallbackWidth);
                var hit = x >= leftBound && x <= rightBound && y >= rcTray.Top && y <= rcTray.Bottom;
                if (hit) Logger.Info($"Hit tray fallback rect: {leftBound},{rcTray.Top}-{rightBound},{rcTray.Bottom}");
                return hit;
            }
            Logger.Warn("Clock control not found and tray rect unavailable");
            return false;
        }

        /// <summary>
        /// 使用 HWND 判定是否命中日期时间按钮；失败时回退到矩形判定。
        /// </summary>
        private bool IsClockHit(int x, int y)
        {
            try
            {
                var pt = new POINT { x = x, y = y };
                var hwnd = WindowFromPoint(pt);
                if (hwnd == IntPtr.Zero) return IsInClockRect(x, y);
                if (IsClockWindowOrAncestor(hwnd))
                {
                    Logger.Info($"Hit clock hwnd: 0x{hwnd.ToInt64():X}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("IsClockHit failed", ex);
                return IsInClockRect(x, y);
            }
        }

        private bool IsClockWindowOrAncestor(IntPtr hwnd)
        {
            IntPtr cur = hwnd;
            var sb = new System.Text.StringBuilder(256);
            while (cur != IntPtr.Zero)
            {
                sb.Clear();
                GetClassName(cur, sb, sb.Capacity);
                var cls = sb.ToString();
                if (cls == "TrayClockWClass" || cls == "ClockButton") return true;
                cur = GetParent(cur);
            }
            return false;
        }

        /// <summary>
        /// 递归查找指定父窗口下类名为 <paramref name="className"/> 的子窗口。
        /// </summary>
        private IntPtr FindChildByClass(IntPtr parent, string className)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(parent, (hwnd, lParam) =>
            {
                var sb = new System.Text.StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                if (sb.ToString() == className)
                {
                    found = hwnd;
                    return false; // stop
                }
                return true; // continue
            }, IntPtr.Zero);
            return found;
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINT pt; public int mouseData; public int flags; public int time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr WindowFromPoint(POINT Point);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);
    }
}
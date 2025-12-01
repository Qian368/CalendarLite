using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CalendarLite.Services
{
    /// <summary>
    /// 外部点击自动关闭服务：当窗口显示后，监听下一次鼠标点击，若点击在窗口之外，则自动隐藏窗口。
    /// </summary>
    public class ClickOutsideCloser
    {
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelMouseProc? _proc;
        private Window? _window;
        private IntPtr _hwnd = IntPtr.Zero;

        /// <summary>
        /// 启动监听，直至捕获到窗口外的点击后自动隐藏并停止监听。
        /// </summary>
        public void Start(Window window)
        {
            _window = window;
            // 获取窗口的句柄
            _hwnd = new WindowInteropHelper(window).Handle;
            // 定义鼠标钩子回调函数
            _proc = HookCallback;
            // 安装全局鼠标钩子
            using var curProcess = Process.GetCurrentProcess();
            // 获取当前进程的主模块句柄
            using var curModule = curProcess.MainModule!;
            // 安装全局鼠标钩子
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            // 检查钩子是否安装成功
            if (_hook == IntPtr.Zero) Logger.Warn("ClickOutsideCloser hook install failed");
        }

        /// <summary>
        /// 停止监听并卸载钩子。
        /// </summary>
        public void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 鼠标低级钩子回调：在按下时判断是否点击到窗口外部，若是则隐藏窗口并移除钩子。
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _window != null)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (_hwnd != IntPtr.Zero && GetWindowRect(_hwnd, out RECT rc))
                    {
                        bool inside = data.pt.x >= rc.Left && data.pt.x <= rc.Right && data.pt.y >= rc.Top && data.pt.y <= rc.Bottom;
                        if (!inside)
                        {
                            try
                            {
                                _window.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    _window.Hide();
                                }));
                                Logger.Info("ClickOutsideCloser: hide window by outside click");
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("ClickOutsideCloser hide failed", ex);
                            }
                            Stop();
                        }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT { public POINT pt; public int mouseData; public int flags; public int time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    }
}
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CalendarLite.Services
{
    /// <summary>
    /// 低级键盘钩子，作为热键注册失败时的兜底方案，监听 Ctrl+Alt+C / Ctrl+Alt+U。
    /// </summary>
    public class LowLevelKeyboardInterceptor
    {
        private IntPtr _hook = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private Action? _onShow;
        private Action? _onImport;
        private bool _ctrlDown, _altDown;

        public void Start(Action onShow, Action onImport)
        {
            _onShow = onShow; _onImport = onImport;
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            if (_hook == IntPtr.Zero) Logger.Warn("LowLevelKeyboardInterceptor hook install failed");
        }

        public void Stop()
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool down = wParam.ToInt32() == WM_KEYDOWN || wParam.ToInt32() == WM_SYSKEYDOWN;
                if (kb.vkCode == VK_CONTROL) _ctrlDown = down;
                if (kb.vkCode == VK_MENU) _altDown = down; // Alt

                if (down && _ctrlDown && _altDown)
                {
                    if (kb.vkCode == 'C') { TryInvoke(_onShow); }
                    if (kb.vkCode == 'U') { TryInvoke(_onImport); }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void TryInvoke(Action? action)
        {
            try { action?.Invoke(); } catch (Exception ex) { Logger.Error("KeyboardInterceptor invoke failed", ex); }
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT { public int vkCode; public int scanCode; public int flags; public int time; public IntPtr dwExtraInfo; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
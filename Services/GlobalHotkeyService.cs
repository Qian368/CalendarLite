using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;

namespace CalendarLite.Services
{
    /// <summary>
    /// 注册全局热键，用于调试或兜底显示窗口。
    /// </summary>
    public class GlobalHotkeyService
    {
        private readonly IntPtr _hwnd;
        private readonly Dictionary<int, Action> _handlers = new();
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_DEFAULT = 0xA110;

        public GlobalHotkeyService(IntPtr hwnd, Action onHotkey)
        {
            _hwnd = hwnd;
            _handlers[HOTKEY_ID_DEFAULT] = onHotkey;
            var vkC = KeyInterop.VirtualKeyFromKey(Key.C);
            var ok = RegisterHotKey(_hwnd, HOTKEY_ID_DEFAULT, MOD_CONTROL | MOD_ALT, vkC);
            DefaultRegistered = ok;
            if (!ok)
            {
                var code = Marshal.GetLastWin32Error();
                Logger.Warn($"RegisterHotKey Ctrl+Alt+C failed, error={code}");
            }
            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
        }

        private void ComponentDispatcher_ThreadFilterMessage(ref System.Windows.Interop.MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY)
            {
                var id = (int)msg.wParam;
                if (_handlers.TryGetValue(id, out var action))
                {
                    try { action(); } catch (Exception ex) { Logger.Error("WM_HOTKEY handler failed", ex); }
                    handled = true;
                }
            }
        }

        /// <summary>
        /// 注册额外热键（例如 Ctrl+Alt+U）。
        /// </summary>
        public void Register(int id, int modifiers, int vk, Action handler)
        {
            _handlers[id] = handler;
            var ok = RegisterHotKey(_hwnd, id, modifiers, vk);
            if (!ok)
            {
                var code = Marshal.GetLastWin32Error();
                Logger.Warn($"RegisterHotKey failed for id={id}, error={code}");
            }
        }

        /// <summary>
        /// 使用 WPF Key 注册热键，内部转换为虚拟键码。
        /// </summary>
        public void RegisterKey(int id, int modifiers, Key key, Action handler)
        {
            var vk = KeyInterop.VirtualKeyFromKey(key);
            Register(id, modifiers, vk, handler);
        }

        public bool DefaultRegistered { get; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    }
}
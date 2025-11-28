using System;
using System.Runtime.InteropServices;

namespace CalendarLite.Services
{
    /// <summary>
    /// 监听系统日期面板弹出/关闭事件的钩子（模拟集成）。
    /// </summary>
    public class SystemPanelHook
    {
        private WinEventDelegate? _showDelegate;
        private WinEventDelegate? _hideDelegate;
        private IntPtr _hShowHook = IntPtr.Zero;
        private IntPtr _hHideHook = IntPtr.Zero;
        private Action? _onShow;
        private Action? _onHide;

        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        /// <summary>
        /// 启动事件监听。
        /// </summary>
        public void Start(Action onShow, Action onHide)
        {
            _onShow = onShow;
            _onHide = onHide;

            _showDelegate = OnShow;
            _hideDelegate = OnHide;

            _hShowHook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, _showDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
            _hHideHook = SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY, IntPtr.Zero, _hideDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        /// <summary>
        /// 停止事件监听。
        /// </summary>
        public void Stop()
        {
            if (_hShowHook != IntPtr.Zero) { UnhookWinEvent(_hShowHook); _hShowHook = IntPtr.Zero; }
            if (_hHideHook != IntPtr.Zero) { UnhookWinEvent(_hHideHook); _hHideHook = IntPtr.Zero; }
        }

        private void OnShow(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                // 简化处理：任何对象显示事件均尝试显示窗口，避免对内部类名依赖。
                _onShow?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("OnShow failed", ex);
            }
        }

        private void OnHide(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                _onHide?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("OnHide failed", ex);
            }
        }
    }
}
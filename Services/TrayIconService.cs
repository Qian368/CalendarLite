using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CalendarLite.Services
{
    /// <summary>
    /// 系统托盘图标服务：创建任务栏右下角图标及右键菜单。
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _showItem;
        private readonly ToolStripMenuItem _exitItem;

        /// <summary>
        /// 初始化托盘图标与菜单（默认应用图标，可后续替换）。
        /// </summary>
        public TrayIconService(Action onShow, Action onExit)
        {
            /// 托盘图标实例，用于显示在任务栏右下角
            _notifyIcon = new NotifyIcon();
            /// 上下文菜单实例，包含显示/退出选项
            _menu = new ContextMenuStrip();
            _showItem = new ToolStripMenuItem(Resources.Messages.TrayShowCalendar);
            _exitItem = new ToolStripMenuItem(Resources.Messages.TrayExit);

            _showItem.Click += (s, e) => SafeInvoke(onShow);
            _exitItem.Click += (s, e) => SafeInvoke(onExit);

            _menu.Items.Add(_showItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_exitItem);

            _notifyIcon.Text = Resources.Messages.TrayTooltip;
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = _menu;
        }

        /// <summary>
        /// 以安全方式调用委托，避免异常吞噬。
        /// </summary>
        private static void SafeInvoke(Action action)
        {
            try { action?.Invoke(); } catch { }
        }

        /// <summary>
        /// 从文件路径设置托盘图标（支持 .ico）。
        /// </summary>
        public bool SetIconFromFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) { try { Logger.Warn("Tray icon path empty"); } catch { } return false; }
                if (!File.Exists(path)) { try { Logger.Warn("Tray icon file not found: " + path); } catch { } return false; }
                using var icon = new Icon(path);
                _notifyIcon.Icon = (Icon)icon.Clone();
                try { Logger.Info("Tray icon file loaded: " + path); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                try { Logger.Warn("Set tray icon failed: " + ex.Message); } catch { }
                return false;
            }
        }

        /// <summary>
        /// 释放托盘图标资源。
        /// </summary>
        public void Dispose()
        {
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _menu.Dispose();
            }
            catch { }
        }
    }
}

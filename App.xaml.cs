using System;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;
using CalendarLite.Services;
using System.Diagnostics;

namespace CalendarLite
{
    /// <summary>
    /// 应用程序入口，初始化主题颜色与日志系统。
    /// </summary>
    public partial class App : Application
    {
        // 经过实践，下面代码在不会被执行
        // 现象：卸载.NET 8.0 后启动发布的应用，Windows 会优先弹出系统级弹窗提示安装.NET 运行时，应用自身的版本检测代码无法执行。
        // 原因：NET 应用发布后会通过apphost.exe前置检测运行时，若缺失则触发系统标准化提示，Windows 会优先触发系统级别的运行时检测机制，直接弹出提示框要求安装所需版本，而不会执行应用本身的入口代码。
        // .NET 8.0 对应的最小 Release 版本号（≥此值即为 8.0+，兼容高版本）
        private const int MinDotNetReleaseVersion = 528040;
        // 微软 .NET 官方下载链接（跳转至 8.0+ 版本）
        private const string DotNetDownloadUrl = "https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0";        
        private TrayIconService? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 第一步：先执行 .NET 8.0+ 运行时检测
            if (!IsDotNet8OrHigherInstalled())
            {
                // 弹出 WPF 风格的提示框（适配你的应用 UI）
                var dialogResult = MessageBox.Show(
                    "未检测到 .NET 8.0 或更高版本运行时，需下载“.NET 运行时 8.0”或更高版本以运行本程序。\n这是运行本程序的必要组件。\n\n点击「是」前往微软官方下载页，安装完成后重新启动本程序。\n点击「否」将退出程序。",
                    "缺少必要组件",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (dialogResult == MessageBoxResult.Yes)
                {
                    // 打开官方下载页（系统默认浏览器，安全可靠）
                    Process.Start(new ProcessStartInfo(DotNetDownloadUrl) { UseShellExecute = true });
                }

                // 未安装运行时，退出应用（避免后续启动报错）
                Shutdown(1);
                return;
            }            

            base.OnStartup(e);

            Logger.Initialize("logs/app.log");
            Logger.Info("App started");

            try
            {
                ApplySystemTheme();
                Logger.Info("Theme applied");
            }
            catch (Exception ex)
            {
                Logger.Error("ApplySystemTheme failed", ex);
            }

            try
            {
                _tray = new TrayIconService(
                    onShow: () => Dispatcher.Invoke(() => (Current.MainWindow as MainWindow)?.ShowFromTray()),
                    onExit: () => Dispatcher.Invoke(() => Current.Shutdown())
                );
                // 尝试从运行目录的上一级 data 目录加载托盘图标
                bool iconOk = false;
                try { iconOk = _tray.SetIconFromFile("data\\icon.ico"); } catch { }
                Logger.Info(iconOk ? "Tray icon initialized with custom icon" : "Tray icon initialized (default icon)");
            }
            catch (Exception ex)
            {
                Logger.Error("Tray icon init failed", ex);
            }
        }

        /// <summary>
        /// 应用退出时，释放托盘图标资源。
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try { _tray?.Dispose(); } catch { }
            base.OnExit(e);
        }

        /// <summary>
        /// 检测是否安装 .NET 8.0 或更高版本（兼容 64/32 位系统）
        /// </summary>
        private bool IsDotNet8OrHigherInstalled()
        {
            // 覆盖 64 位系统、32 位系统、64 位兼容 32 位的所有场景
            string[] registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",          // 64位系统原生路径
                @"SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full" // 32位系统/64位兼容32位路径
            };

            foreach (var path in registryPaths)
            {
                using (var registryKey = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (registryKey == null) continue;

                    // 读取 .NET 运行时的 Release 版本号（官方判断依据）
                    var releaseVersionObj = registryKey.GetValue("Release");
                    if (releaseVersionObj is int releaseVersion)
                    {
                        // ≥8.0 最小阈值 = 安装了 8.0 或更高版本（自动兼容高版本）
                        if (releaseVersion >= MinDotNetReleaseVersion)
                        {
                            return true;
                        }
                    }
                }
            }

            return false; // 未找到 8.0+ 版本
        }

        /// <summary>
        /// 根据系统“浅色/深色”模式设置资源字典颜色。
        /// </summary>
        private void ApplySystemTheme()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                if (key != null)
                {
                    var useLightTheme = (int?)key.GetValue("AppsUseLightTheme") ?? 1;
                    var light = useLightTheme == 1;
                    Resources["IsLightTheme"] = light;
                    if (light)
                    {
                        Resources["PanelBackground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                        Resources["Foreground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));
                        Resources["ForegroundLight"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66));
                        Resources["LunarColor"] = Resources["ForegroundLight"];
                        Resources["ExtraColor"] = Resources["Foreground"];
                    }
                    else
                    {
                        Resources["PanelBackground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));
                        Resources["Foreground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                        Resources["ForegroundLight"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEE, 0xEE, 0xEE));
                        Resources["LunarColor"] = Resources["ForegroundLight"];
                        Resources["ExtraColor"] = Resources["ForegroundLight"];
                    }
                }
                else
                {
                    Resources["IsLightTheme"] = true;
                    Resources["PanelBackground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                }
            }
            catch
            {
                Resources["IsLightTheme"] = true;
                Resources["PanelBackground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            }
        }
    }
}

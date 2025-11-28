using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CalendarLite.Models;
using CalendarLite.Services;
using Msgs = CalendarLite.Resources.Messages;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CalendarLite
{
    /// <summary>
    /// 主窗口，负责时间显示、月份切换和日历渲染。
    /// </summary>
    public partial class MainWindow : Window
    {
        private DateTime _currentMonth;
        private readonly HolidayService _holidayService;
        private readonly DispatcherTimer _timer;
        private TrayClockInterceptor? _interceptor;
        private GlobalHotkeyService? _hotkey;
        private DateTime? _selectedDate;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                _holidayService = new HolidayService("data\\data.json");
                Logger.Info("HolidayService initialized");
            }
            catch (Exception ex)
            {
                Logger.Error("HolidayService init failed", ex);
                MessageBox.Show(Msgs.DataLoadFailed, Msgs.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                _holidayService = new HolidayService(null);
            }

            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            RenderMonth();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            try
            {
                /// 初始化年份选择器，范围从 1900 到 2100
                var years = Enumerable.Range(1900, 201).ToList();
                YearSelect.ItemsSource = years;
                YearSelect.SelectedItem = DateTime.Now.Year;
            }
            catch (Exception ex)
            {
                Logger.Error("Init YearSelect failed", ex);
            }

            _interceptor = new TrayClockInterceptor();
            _interceptor.Start(() => Dispatcher.Invoke(ToggleFromClock));
            Logger.Info("TrayClockInterceptor started");

            Deactivated += (s, e) => Hide();
        }

        /// <summary>
        /// 窗口句柄创建完成后，调整扩展样式以隐藏 Alt+Tab。
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);

            _hotkey = new GlobalHotkeyService(hwnd, () => {
                ShowNearSystemPanel();
                Activate();
            });
            Logger.Info("GlobalHotkeyService initialized");

            _hotkey.RegisterKey(0xA120, GlobalHotkeyService.MOD_CONTROL | GlobalHotkeyService.MOD_ALT, Key.U, () =>
            {
                Dispatcher.Invoke(async () => await ShowImportDialogAsync());
            });

            if (!_hotkey.DefaultRegistered)
            {
                var kb = new LowLevelKeyboardInterceptor();
                kb.Start(() => { Dispatcher.Invoke(() => { ShowNearSystemPanel(); Activate(); }); },
                         () => { Dispatcher.Invoke(async () => await ShowImportDialogAsync()); });
                // 持有引用避免 GC
                _keyboardInterceptor = kb;
            }
        }

        /// <summary>
        /// 定时刷新顶部时间与日期描述。
        /// </summary>
        private void Timer_Tick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            TimeText.Text = now.ToString("HH:mm:ss");
            var show = _selectedDate ?? now.Date;
            DateDescText.Text = $"{show:yyyy年MM月dd日}" + " " + (_holidayService.GetLunar(show) ?? string.Empty);
        }

        /// <summary>
        /// 渲染当前月份日历。
        /// </summary>
        private void RenderMonth()
        {
            MonthTitle.Text = $"{_currentMonth:yyyy年MM月}";
            CalendarItems.ItemsSource = BuildDays(_currentMonth);
        }

        /// <summary>
        /// 生成指定月份的日期数据列表（包含角标与农历信息）。
        /// </summary>
        private List<DayItem> BuildDays(DateTime month)
        {
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

            // 计算从周一开始的偏移
            int offset = ((int)firstDay.DayOfWeek + 6) % 7; // 将周日(0)调整为6
            var items = new List<DayItem>();

            /// <summary>
            /// 构造一个日期单元。
            /// - outside=true 表示来自相邻月份：仅颜色使用浅灰以示区别；其它信息（农历、节气、节日、休班角标）照常显示。
            /// - 默认：相邻月不高亮节日（ExtraForeground 使用浅灰）。如需相邻月也高亮为浅蓝，可将下面对 extraFg 的赋值改为与当月一致（去掉 outside 分支的浅灰逻辑）。
            /// </summary>
            DayItem BuildItem(DateTime date, bool outside)
            {
                var isToday = date.Date == DateTime.Now.Date;
                string? lunarShort = _holidayService.GetLunarDayShort(date);
                string? solarTerm = _holidayService.GetSolarTerm(date);
                string? traditional = _holidayService.GetTraditionalHoliday(date);
                string? international = _holidayService.GetInternationalHoliday(date);

                string? extra = traditional ?? international ?? solarTerm ?? lunarShort;
                // outside 情况下，说明文字使用浅灰。若希望相邻月节日同样浅蓝高亮，可改为不区分 outside。
                Brush extraFg = (Brush)Application.Current.Resources[outside ? "OutsideMonth-Foreground" : "ExtraColor"];
                var extraWeight = FontWeights.Normal;
                if (!outside && (!string.IsNullOrEmpty(traditional) || !string.IsNullOrEmpty(international)))
                {
                    extraFg = (Brush)Application.Current.Resources["HolidayTextLightBlue"];
                }
                else if (!string.IsNullOrEmpty(solarTerm))
                {
                    extraWeight = FontWeights.Bold;
                }

                string badgeText = string.Empty;
                Brush badgeColor = Brushes.Transparent;
                if (_holidayService.IsHoliday(date))
                {
                    badgeText = Msgs.HolidayBadge;
                    badgeColor = (Brush)Application.Current.Resources["HolidayBadgeColor"];
                }
                else if (_holidayService.IsWorkday(date))
                {
                    badgeText = Msgs.WorkdayBadge;
                    badgeColor = (Brush)Application.Current.Resources["WorkdayBadgeColor"];
                }

                return new DayItem
                {
                    // 允许相邻月可点击：始终设置 Date=date，点击时在 Day_Click 中判断跳转到对应月份。
                    Date = date,
                    Day = date.Day.ToString(CultureInfo.InvariantCulture),
                    DayForeground = outside
                        ? (Brush)Application.Current.Resources["OutsideMonth-Foreground"]
                        : (Brush)Application.Current.Resources[((date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) ? "Weekend-Foreground" : "Foreground")],
                    Lunar = string.Empty,
                    ExtraInfo = extra ?? string.Empty,
                    ExtraForeground = extraFg,
                    ExtraFontWeight = extraWeight,
                    BadgeText = badgeText,
                    BadgeColor = badgeColor,
                    BorderBrush = outside
                        ? Brushes.Transparent
                        : ((_selectedDate.HasValue && _selectedDate.Value.Date == date.Date)
                            ? (Brush)Application.Current.Resources["TodayBorder"]
                            : (isToday ? (Brush)Application.Current.Resources["TodayBorder"] : Brushes.Transparent)),
                    BorderThickness = outside
                        ? new System.Windows.Thickness(0)
                        : ((_selectedDate.HasValue && _selectedDate.Value.Date == date.Date)
                            ? new System.Windows.Thickness(2)
                            : (isToday ? new System.Windows.Thickness(1) : new System.Windows.Thickness(0)))
                };
            }

            if (offset > 0)
            {
                var prev = firstDay.AddMonths(-1);
                int prevDays = DateTime.DaysInMonth(prev.Year, prev.Month);
                for (int i = offset - 1; i >= 0; i--)
                {
                    int dayNum = prevDays - i;
                    var date = new DateTime(prev.Year, prev.Month, dayNum);
                    items.Add(BuildItem(date, true));
                }
            }

            for (int d = 1; d <= daysInMonth; d++)
            {
                var date = new DateTime(month.Year, month.Month, d);
                var isToday = date.Date == DateTime.Now.Date;

                string? lunarShort = _holidayService.GetLunarDayShort(date);
                string? solarTerm = _holidayService.GetSolarTerm(date);
                string? traditional = _holidayService.GetTraditionalHoliday(date);
                string? international = _holidayService.GetInternationalHoliday(date);

                string? extra = traditional ?? international ?? solarTerm ?? lunarShort;
                Brush extraFg = (Brush)Application.Current.Resources["ExtraColor"];
                var extraWeight = FontWeights.Normal;
                if (!string.IsNullOrEmpty(traditional) || !string.IsNullOrEmpty(international))
                {
                    extraFg = (Brush)Application.Current.Resources["HolidayTextLightBlue"];
                }
                else if (!string.IsNullOrEmpty(solarTerm))
                {
                    extraWeight = FontWeights.Bold;
                }

                string badgeText = string.Empty;
                Brush badgeColor = Brushes.Transparent;
                if (_holidayService.IsHoliday(date))
                {
                    badgeText = Msgs.HolidayBadge;
                    badgeColor = (Brush)Application.Current.Resources["HolidayBadgeColor"];
                }
                else if (_holidayService.IsWorkday(date))
                {
                    badgeText = Msgs.WorkdayBadge;
                    badgeColor = (Brush)Application.Current.Resources["WorkdayBadgeColor"];
                }

                var item = new DayItem
                {
                    Date = date,
                    Day = d.ToString(CultureInfo.InvariantCulture),
                    DayForeground = (Brush)Application.Current.Resources[((date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) ? "Weekend-Foreground" : "Foreground")],
                    Lunar = string.Empty,
                    ExtraInfo = extra ?? string.Empty,
                    ExtraForeground = extraFg,
                    ExtraFontWeight = extraWeight,
                    BadgeText = badgeText,
                    BadgeColor = badgeColor,
                    BorderBrush = (_selectedDate.HasValue && _selectedDate.Value.Date == date.Date)
                        ? (Brush)Application.Current.Resources["TodayBorder"]
                        : (isToday ? (Brush)Application.Current.Resources["TodayBorder"] : Brushes.Transparent),
                    BorderThickness = (_selectedDate.HasValue && _selectedDate.Value.Date == date.Date)
                        ? new System.Windows.Thickness(2)
                        : (isToday ? new System.Windows.Thickness(1) : new System.Windows.Thickness(0))
                };

                items.Add(item);
            }

            int total = items.Count;
            int tail = total % 7 == 0 ? 0 : (7 - (total % 7));
            if (tail > 0)
            {
                var next = firstDay.AddMonths(1);
                for (int i = 1; i <= tail; i++)
                {
                    var date = new DateTime(next.Year, next.Month, i);
                    items.Add(BuildItem(date, true));
                }
            }

            return items;
        }

        /// <summary>
        /// 切换到上一月。
        /// </summary>
        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            JumpSelectByMonths(-1);
        }

        /// <summary>
        /// 切换到下一月。
        /// </summary>
        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            JumpSelectByMonths(1);
        }

        /// <summary>
        /// 跳转回今天所在月份，并选中今天。
        /// </summary>
        private void Today_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var today = DateTime.Now.Date;
                _currentMonth = new DateTime(today.Year, today.Month, 1);
                _selectedDate = today;
                RenderMonth();
                DateDescText.Text = $"{today:yyyy年MM月dd日}" + " " + (_holidayService.GetLunar(today) ?? string.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error("Today_Click failed", ex);
            }
        }

        /// <summary>
        /// 将窗口显示在系统日期面板附近（右下角偏移）。
        /// </summary>
        private void ShowNearSystemPanel()
        {
            // 优先贴近系统托盘钟表位置
            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _selectedDate = DateTime.Now.Date;
            RenderMonth();
            var tray = FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero)
            {
                var clock = FindWindowEx(tray, IntPtr.Zero, "TrayClockWClass", null);
                if (clock == IntPtr.Zero)
                {
                    clock = FindChildByClass(tray, "ClockButton");
                }
                RECT rc;
                if (clock != IntPtr.Zero && GetWindowRect(clock, out rc))
                {
                    Left = rc.Right - Width;
                    Top = rc.Top - Height;
                    Logger.Info($"ShowNearSystemPanel at clock: {Left},{Top}");
                }
                else if (GetWindowRect(tray, out rc))
                {
                    Left = rc.Right - Width;
                    Top = rc.Top - Height;
                    Logger.Info($"ShowNearSystemPanel at tray: {Left},{Top}");
                }
                else
                {
                    var screen = SystemParameters.WorkArea;
                    Left = screen.Right - Width - 8;
                    Top = screen.Bottom - Height - 8;
                    Logger.Info($"ShowNearSystemPanel fallback RB: {Left},{Top}");
                }
            }
            else
            {
                var screen = SystemParameters.WorkArea;
                Left = screen.Right - Width - 8;
                Top = screen.Bottom - Height - 8;
                Logger.Info($"ShowNearSystemPanel no tray RB: {Left},{Top}");
            }
            Show();
            Activate();
            var hwnd = new WindowInteropHelper(this).Handle;
            SetForegroundWindow(hwnd);
            try
            {
                // 显示后启动一次性外部点击关闭监听
                _outsideCloser?.Stop();
                _outsideCloser = new ClickOutsideCloser();
                _outsideCloser.Start(this);
            }
            catch (Exception ex)
            {
                Logger.Error("Start ClickOutsideCloser failed", ex);
            }
        }

        /// <summary>
        /// 由托盘菜单触发显示窗口（靠近系统日期面板）。
        /// </summary>
        public void ShowFromTray()
        {
            try { ShowNearSystemPanel(); }
            catch (Exception ex) { Logger.Error("ShowFromTray failed", ex); }
        }

        /// <summary>
        /// 由任务栏时钟点击触发的切换逻辑：若当前窗口可见则隐藏，否则在任务栏附近显示。
        /// </summary>
        private void ToggleFromClock()
        {
            try
            {
                if (IsVisible)
                {
                    _outsideCloser?.Stop();
                    Hide();
                }
                else
                {
                    ShowNearSystemPanel();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ToggleFromClock failed", ex);
            }
        }

        /// <summary>
        /// 显示导入对话框并处理各种导入方式（包括本地文件导入）
        /// </summary>
        private async Task ShowImportDialogAsync()
        {
            var dlg = new ImportUrlWindow { Owner = this, Title = Msgs.ImportTitle };
            bool? result = null;
            try { result = dlg.ShowDialog(); }
            catch (Exception ex) { Logger.Error("ShowDialog ImportUrlWindow failed", ex); }

            if (result == true && dlg.BatchMode)
            {
                // 批量导入逻辑（已有）
                await BatchImportAsync(dlg.BatchStartYear);
                try { _holidayService.Reload(); } catch (Exception ex) { Logger.Error("Reload after batch import failed", ex); }
                RenderMonth();
            }
            else if (result == true && !string.IsNullOrEmpty(dlg.Url))
            {
                // URL导入逻辑（已有）
                var importer = new ImportService();
                var ok = await importer.ImportFromUrlAsync(dlg.Url!, "data\\data.json");
                HandleImportResult(ok);
            }
            // 新增：处理本地文件导入
            else if (result == true && dlg.IsLocalImport)
            {
                var importer = new ImportService();
                // 假设dlg.LocalFilePath存储了选择的本地文件路径
                var ok = await importer.ImportFromLocalFileAsync(dlg.LocalFilePath, "data\\data.json");
                HandleImportResult(ok);
            }
        }

        /// <summary>
        /// 统一处理导入结果（刷新数据+界面）
        /// </summary>
        private void HandleImportResult(bool isSuccess)
        {
            if (isSuccess)
            {
                try 
                { 
                    _holidayService.Reload(); // 重新加载data.json数据
                } 
                catch (Exception ex) 
                { 
                    Logger.Error("Reload after import failed", ex); 
                }
                RenderMonth(); // 刷新日历显示
                MessageBox.Show(Msgs.ImportSuccess, Msgs.ImportTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(Msgs.ImportNoData, Msgs.ImportTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        /// <summary>
        /// 批量导入：从指定年份开始，逐年请求，直到遇到“无数据”终止，并汇总结果。
        /// </summary>
        private async Task BatchImportAsync(int startYear)
        {
            var importer = new ImportService();
            int y = Math.Max(1900, startYear);
            int success = 0;
            int total = 0;
            int stopYear = y;
            int consecutiveFailures = 0; // 连续失败计数

            // 新增：记录成功和失败的年份列表
            var successfulYears = new List<int>();
            var failedYears = new List<int>();
            var firstSuccessYear = -1;
            var lastSuccessYear = -1;

            while (1900 <= y && y <= 2100)
            {
                total++;
                string url = $"https://timor.tech/api/holiday/year/{y}";
                bool ok = false;
                try
                {
                    ok = await importer.ImportFromUrlAsync(url, "data\\data.json");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Batch import year {y} failed", ex);
                    ok = false;
                }
                if (!ok)
                {
                    consecutiveFailures++; // 增加连续失败计数
                    stopYear = y;
                    failedYears.Add(y); // 记录失败年份
                    
                    // 关键改进：连续3年失败才停止，避免单次网络问题误判
                    if (consecutiveFailures >= 3)
                    {
                        Logger.Info($"连续{consecutiveFailures}年导入失败，停止批量导入");
                        break;
                    }
                }
                else
                {
                    consecutiveFailures = 0; // 重置连续失败计数
                    success++;
                    successfulYears.Add(y); // 记录成功年份
                    // 更新首尾成功年份
                    if (firstSuccessYear == -1) firstSuccessYear = y;
                    lastSuccessYear = y;
                }
                y--;
                await Task.Delay(350);
            }

            // 关键改进：在最后处理时去掉最后3个失败年份（边界检测年份）
            var FinalFailedYears = failedYears.Count > 3 
                ? failedYears.Take(failedYears.Count - 3).ToList() 
                : failedYears;

            string? msg;
            if ( success > 0 && success == startYear - stopYear - 2)
            {
                msg = string.Format(Msgs.BatchImportAllSuccess, startYear, stopYear + 3);
            }
            else if (success == 0)
            {
                msg = string.Format(Msgs.BatchImportNoData, stopYear + 3);
            }
            // 部分导入成功的情况
            else if (success > 0 && success < startYear - stopYear - 2)
            {
                var successfulYearsStr = successfulYears.Count > 20 
                    ? $"{string.Join("、", successfulYears.Take(20))}...等{successfulYears.Count}个年份"
                    : string.Join("、", successfulYears);
                    
                var failedYearsStr = FinalFailedYears.Count > 20 
                    ? $"{string.Join("、", FinalFailedYears.Take(20))}...等{FinalFailedYears.Count}个年份"
                    : string.Join("、", FinalFailedYears);
                
                msg = string.Format(Msgs.BatchImportPartialSuccess, 
                    firstSuccessYear, lastSuccessYear, successfulYearsStr, failedYearsStr);                
            }
            else 
            {
                msg = string.Format(Msgs.BatchImportSummary, total - consecutiveFailures, success, stopYear + 3);
            }
            MessageBox.Show(msg, Msgs.BatchImportTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 导入按钮点击事件，触发导入对话框。
        /// </summary>
        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            await ShowImportDialogAsync();
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        private static IntPtr FindChildByClass(IntPtr parent, string className)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(parent, (hwnd, lParam) =>
            {
                var sb = new System.Text.StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                if (sb.ToString() == className)
                {
                    found = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }
        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetForegroundWindow(IntPtr hWnd);
        private LowLevelKeyboardInterceptor? _keyboardInterceptor;
        private ClickOutsideCloser? _outsideCloser;
        /// <summary>
        /// 日期单元格点击事件：选中对应日期并高亮显示。
        /// </summary>
        private void Day_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Border b && b.DataContext is DayItem di && di.Date.HasValue)
                {
                    var clicked = di.Date.Value.Date;
                    // 若点击的是相邻月日期，则切换当前月份为该日期所在月
                    if (clicked.Year != _currentMonth.Year || clicked.Month != _currentMonth.Month)
                    {
                        _currentMonth = new DateTime(clicked.Year, clicked.Month, 1);
                    }
                    _selectedDate = clicked;
                    RenderMonth();
                    // 更新顶部日期描述为选中日期
                    DateDescText.Text = $"{clicked:yyyy年MM月dd日}" + " " + (_holidayService.GetLunar(clicked) ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Day_Click failed", ex);
            }
        }
        /// <summary>
        /// 年份选择变更，跳转到选定年份的当前月份。
        /// </summary>
        private void YearSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (YearSelect.SelectedItem is int y)
                {
                    var baseDay = _selectedDate?.Day ?? DateTime.Now.Day;
                    var m = _currentMonth.Month;
                    var maxDay = DateTime.DaysInMonth(y, m);
                    var day = Math.Min(baseDay, maxDay);
                    var newSel = new DateTime(y, m, day);
                    _selectedDate = newSel;
                    _currentMonth = new DateTime(y, m, 1);
                    RenderMonth();
                    DateDescText.Text = $"{newSel:yyyy年MM月dd日}" + " " + (_holidayService.GetLunar(newSel) ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("YearSelect_SelectionChanged failed", ex);
            }
        }

        private void JumpSelectByMonths(int delta)
        {
            var anchor = _selectedDate ?? DateTime.Now.Date;
            var targetMonthFirst = new DateTime(anchor.Year, anchor.Month, 1).AddMonths(delta);
            var maxDay = DateTime.DaysInMonth(targetMonthFirst.Year, targetMonthFirst.Month);
            var day = Math.Min(anchor.Day, maxDay);
            var newSel = new DateTime(targetMonthFirst.Year, targetMonthFirst.Month, day);
            _selectedDate = newSel;
            _currentMonth = new DateTime(newSel.Year, newSel.Month, 1);
            RenderMonth();
            DateDescText.Text = $"{newSel:yyyy年MM月dd日}" + " " + (_holidayService.GetLunar(newSel) ?? string.Empty);
        }
    }
}

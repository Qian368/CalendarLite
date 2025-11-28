using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CalendarLite.Models;
using Lunar;

namespace CalendarLite.Services
{
    /// <summary>
    /// 节假日/农历/节气数据读取与查询服务。
    /// </summary>
    public class HolidayService
    {
        private readonly CalendarDataRoot _data = new CalendarDataRoot();
        private readonly string? _filePath;

        public HolidayService(string? filePath)
        {
            _filePath = filePath;
            if (!string.IsNullOrEmpty(_filePath))
            {
                Load();
            }
        }

        /// <summary>
        /// 加载 JSON 数据文件到内存。
        /// </summary>
        private void Load()
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            try
            {
                var path = _filePath;
                if (!File.Exists(path)) { Logger.Warn($"Data file not found: {path}"); return; }
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var root = JsonSerializer.Deserialize<Dictionary<string, CalendarYearData>>(json, options);
                if (root != null)
                {
                    foreach (var kv in root)
                    {
                        _data[kv.Key] = kv.Value;
                    }
                }
                // 额外数据文件：合并 24 节气、传统节日
                MergeDictFrom(Path.Combine("data", "solar_terms.json"), (year, dict) =>
                {
                    if (!_data.TryGetValue(year, out var y)) { y = new CalendarYearData(); _data[year] = y; }
                    y.solar_terms = MergeStringDict(y.solar_terms, dict);
                });
                MergeDictFrom(Path.Combine("data", "traditional_holidays.json"), (year, dict) =>
                {
                    if (!_data.TryGetValue(year, out var y)) { y = new CalendarYearData(); _data[year] = y; }
                    y.traditional_holidays = MergeStringDict(y.traditional_holidays, dict);
                });
                MergeDictFrom(Path.Combine("data", "international_holidays.json"), (year, dict) =>
                {
                    if (!_data.TryGetValue(year, out var y)) { y = new CalendarYearData(); _data[year] = y; }
                    y.international_holidays = MergeStringDict(y.international_holidays, dict);
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Load data.json failed", ex);
            }
        }

        /// <summary>
        /// 重新加载数据文件与扩展字典，及时应用最新的节假日/节气/传统节日。
        /// </summary>
        public void Reload()
        {
            try
            {
                _data.Clear();
                Load();
            }
            catch (Exception ex)
            {
                Logger.Error("Reload data failed", ex);
            }
        }

        /// <summary>
        /// 判断指定日期是否为法定节假日。
        /// </summary>
        public bool IsHoliday(DateTime date)
        {
            var y = date.Year.ToString();
            if (!_data.TryGetValue(y, out var year)) return false;
            return year.holidays?.Contains(date.ToString("yyyy-MM-dd")) == true;
        }

        /// <summary>
        /// 判断指定日期是否为调休补班。
        /// </summary>
        public bool IsWorkday(DateTime date)
        {
            var y = date.Year.ToString();
            if (!_data.TryGetValue(y, out var year)) return false;
            return year.workdays?.Contains(date.ToString("yyyy-MM-dd")) == true;
        }

        /// <summary>
        /// 获取指定日期的农历描述。
        /// </summary>
        public string? GetLunar(DateTime date)
        {
            try
            {
                var solar = new Solar(date.Year, date.Month, date.Day);
                var lunar = solar.Lunar;
                var text = lunar.ToString();
                if (string.IsNullOrEmpty(text)) text = lunar.FullString;
                // 提取“月+日”简短形式，例如："二零二五年九月三十" -> "九月三十"
                var token = text.Split(' ').FirstOrDefault() ?? text;
                var idx = token.IndexOf('年');
                if (idx >= 0 && idx + 1 < token.Length) token = token[(idx + 1)..];
                return token;
            }
            catch (Exception ex)
            {
                Logger.Error("Lunar calc failed", ex);
                var y = date.Year.ToString();
                if (_data.TryGetValue(y, out var year))
                {
                    if (year.lunar != null && year.lunar.TryGetValue(date.ToString("yyyy-MM-dd"), out var v)) return v;
                }
                return null;
            }
        }

        /// <summary>
        /// 从指定路径加载 {year:{date:name}} 结构并按年份合并到当前数据字典。
        /// </summary>
        private void MergeDictFrom(string path, Action<string, Dictionary<string, string>> assign)
        {
            try
            {
                if (!File.Exists(path)) return;
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var root = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, options);
                if (root == null) return;
                foreach (var kv in root)
                {
                    assign(kv.Key, kv.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"MergeDictFrom failed: {path}", ex);
            }
        }

        /// <summary>
        /// 合并两个字符串字典（后者覆盖前者同键）。
        /// </summary>
        private static Dictionary<string, string> MergeStringDict(Dictionary<string, string>? a, Dictionary<string, string>? b)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (a != null) foreach (var kv in a) result[kv.Key] = kv.Value;
            if (b != null) foreach (var kv in b) result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>
        /// 获取指定日期的节气名称。
        /// </summary>
        public string? GetSolarTerm(DateTime date)
        {
            var key = date.ToString("yyyy-MM-dd");
            var y = date.Year.ToString();
            if (_data.TryGetValue(y, out var year))
            {
                if (year.solar_terms?.TryGetValue(key, out var v) == true) return v;
            }
            // 回退：使用 lunar-csharp 计算当日节气名（如当日恰逢节气）
            try
            {
                var solar = new Solar(date.Year, date.Month, date.Day);
                var lunar = solar.Lunar;
                var prop = lunar.GetType().GetProperty("JieQi");
                if (prop != null)
                {
                    var val = prop.GetValue(lunar) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
                var method = lunar.GetType().GetMethod("GetJieQi");
                if (method != null)
                {
                    var val = method.Invoke(lunar, null) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GetSolarTerm fallback failed", ex);
            }
            return null;
        }

        /// <summary>
        /// 获取指定日期的传统节日名称。
        /// </summary>
        public string? GetTraditionalHoliday(DateTime date)
        {
            // ❶ 优先通过农历计算判断春节等传统节日（完全不依赖JSON）
            try
            {
                var solar = new Solar(date.Year, date.Month, date.Day);
                var lunar = solar.Lunar;
                
                // ❶ 去掉反射，直接访问公开属性（根据你的lunar-csharp版本调整属性名）
                // 注意：如果你的库版本中属性名还是 Month/Day，就用 lunar.Month / lunar.Day
                // 大部分新版本库用 LunarMonth / LunarDay，优先试这个！
                int m = lunar.Month; // 替换原反射的 Month 属性
                int d = lunar.Day;   // 替换原反射的 Day 属性

                // 其他传统节日的农历判断（按农历月份先后排序）
                if (m == 1 && d == 1) return "春节"; // 正月初一（核心节日，补充到最前面）
                if (m == 1 && d == 15) return "元宵节"; // 正月十五
                if (m == 2 && d == 2) return "龙抬头"; // 二月初二
                if (m == 3 && d == 3) return "上巳节"; // 三月初三（传统踏青节）
                if (m == 4 && d == 8) return "浴佛节"; // 四月初八（佛诞节）
                if (m == 5 && d == 5) return "端午节"; // 五月初五
                if (m == 6 && d == 6) return "晒红节"; // 六月初六（晒谱节）
                if (m == 7 && d == 7) return "七夕节"; // 七月初七（乞巧节）
                if (m == 7 && d == 15) return "中元节"; // 七月十五（鬼节）
                if (m == 8 && d == 15) return "中秋节"; // 八月十五
                if (m == 9 && d == 9) return "重阳节"; // 九月初九
                if (m == 10 && d == 15) return "下元节"; // 十月十五（水官节）
                if (m == 12 && d == 8) return "腊八节"; // 十二月初八
                if (m == 12 && d == 23) return "北方小年"; // 腊月二十三（北方小年，部分地区二十四）
                if (m == 12 && d == 23) return "南方小年"; // 腊月二十三（北方小年，部分地区二十四）

                // 除夕判断（农历十二月最后一天，次日为正月初一）
                var nextDay = date.AddDays(1);
                var nextLunar = new Solar(nextDay.Year, nextDay.Month, nextDay.Day).Lunar;
                int nextM = nextLunar.Month;
                int nextD = nextLunar.Day;
                if (nextM == 1 && nextD == 1)
                    return "除夕"; // 腊月最后一天
                
            }
            catch (Exception ex)
            {
                Logger.Error("GetTraditionalHoliday fallback failed", ex);
            }

            // ❶ 获取当前日期的年份（作为JSON中第一层键的key，比如"2026"）
            var yearKey = date.Year.ToString();

            // ❷ 从内存数据中尝试获取该年份的节日数据（来自data.json）
            // _data 是加载JSON后存储数据的容器（CalendarDataRoot类型，本质是字典）
            // yearData 是该年份的所有传统节日数据（来自data.json）
            if (_data.TryGetValue(yearKey, out var yearData) && yearData.traditional_holidays != null)
            {
                // ❸ 生成当前日期的字符串key（格式：yyyy-MM-dd，比如"2026-02-17"）
                var dateKey = date.ToString("yyyy-MM-dd");

                // ❹ 尝试从该年份的传统节日字典中查询当前日期对应的节日名
                // 同时过滤掉纯农历日标签（如"初一"，避免和农历计算重复）
                if (yearData.traditional_holidays.TryGetValue(dateKey, out var holidayName) 
                    && !IsLunarDayLabel(holidayName))
                {
                    // ❺ 如果查询到有效节日名，返回该名称
                    return holidayName;
                }
            }

            // ❻ 若JSON中无有效配置，返回null（表示未命中）
            return null;
        }

        /// <summary>
        /// 判断名称是否为农历“日”短文本（如：初二、十一、廿三、三十）。
        /// </summary>
        private static bool IsLunarDayLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();
            return name == "初一" || name == "初二" || name == "初三" || name == "初四" || name == "初五" || name == "初六" || name == "初七" || name == "初八" || name == "初九" || name == "初十"
                || name == "十一" || name == "十二" || name == "十三" || name == "十四" || name == "十五" || name == "十六" || name == "十七" || name == "十八" || name == "十九"
                || name == "二十"
                || name == "廿一" || name == "廿二" || name == "廿三" || name == "廿四" || name == "廿五" || name == "廿六" || name == "廿七" || name == "廿八" || name == "廿九"
                || name == "三十";
        }

        public string? GetInternationalHoliday(DateTime date)
        {
            var key = date.ToString("yyyy-MM-dd");
                var year = date.Year.ToString();
                // 优先从JSON配置获取（用户自定义节日覆盖默认）
                if (_data.TryGetValue(year, out var yearData) 
                    && yearData.international_holidays?.TryGetValue(key, out var customHoliday) == true)
                {
                    return customHoliday;
                }

                // 1. 固定日期节日（按月份排序）
                if (date.Month == 1)
                {
                    if (date.Day == 1) return "元旦";
                    if (date.Day == 26) return "国际海关日";
                }
                else if (date.Month == 2)
                {
                    if (date.Day == 2) return "世界湿地日";
                    if (date.Day == 14) return "情人节";
                    if (date.Day == 21) return "国际母语日";
                }
                else if (date.Month == 3)
                {
                    if (date.Day == 1) return "国际海豹日";
                    if (date.Day == 8) return "妇女节";
                    if (date.Day == 12) return "植树节";
                    if (date.Day == 14) return "国际警察日";
                    if (date.Day == 22) return "世界水日";
                }
                else if (date.Month == 4)
                {
                    if (date.Day == 1) return "愚人节";
                    if (date.Day == 22) return "世界地球日";
                    if (date.Day == 23) return "世界读书日";
                }
                else if (date.Month == 5)
                {
                    if (date.Day == 1) return "国际劳动节";
                    if (date.Day == 4) return "青年节";
                    if (date.Day == 12) return "国际护士节";
                    if (date.Day == 31) return "世界无烟日";
                }
                else if (date.Month == 6)
                {
                    if (date.Day == 1) return "国际儿童节";
                    if (date.Day == 5) return "世界环境日";
                    if (date.Day == 23) return "国际奥林匹克日";
                }
                else if (date.Month == 7)
                {
                    if (date.Day == 1) return "建党节";
                    if (date.Day == 11) return "世界人口日";
                }
                else if (date.Month == 8)
                {
                    if (date.Day == 1) return "建军节";
                    if (date.Day == 12) return "国际青年节";
                }
                else if (date.Month == 9)
                {
                    if (date.Day == 10) return "教师节";
                    if (date.Day == 27) return "世界旅游日";
                }
                else if (date.Month == 10)
                {
                    if (date.Day == 1) return "国庆节";
                    if (date.Day == 16) return "世界粮食日";
                }
                else if (date.Month == 11)
                {
                    if (date.Day == 17) return "国际大学生节";
                }
                else if (date.Month == 12)
                {
                    if (date.Day == 1) return "世界艾滋病日";
                    if (date.Day == 24) return "平安夜";
                    if (date.Day == 25) return "圣诞节";
                    if (date.Day == 31) return "跨年夜";
                }

                // 2. 按“第N个星期X”计算的节日（提取通用方法，减少重复代码）
                // 5月第二个星期日：母亲节
                if (date.Month == 5 && IsNthWeekdayOfMonth(date, 2, DayOfWeek.Sunday))
                    return "母亲节";

                // 6月第三个星期日：父亲节
                if (date.Month == 6 && IsNthWeekdayOfMonth(date, 3, DayOfWeek.Sunday))
                    return "父亲节";

                // 11月第四个星期四：感恩节（美国）
                if (date.Month == 11 && IsNthWeekdayOfMonth(date, 4, DayOfWeek.Thursday))
                    return "感恩节";

                return null;
        }

        /// <summary>
        /// 通用工具方法：判断日期是否为当月第N个星期X（如“第2个星期日”）
        /// </summary>
        /// <param name="date">待判断日期</param>
        /// <param name="n">第N个（1-5，5表示最后一个）</param>
        /// <param name="weekday">星期几</param>
        private bool IsNthWeekdayOfMonth(DateTime date, int n, DayOfWeek weekday)
        {
            if (n < 1 || n > 5) return false;

            // 计算当月第一天是星期几
            var firstDay = new DateTime(date.Year, date.Month, 1);
            // 当月第一天到目标星期几的偏移量（如第一天是周三，要找周日，偏移量为4）
            int offset = ((int)weekday - (int)firstDay.DayOfWeek + 7) % 7;
            // 当月第一个目标星期几的日期
            var firstTarget = firstDay.AddDays(offset);

            DateTime targetDate;
            if (n == 5)
            {
                // 第5个：取第4个的下一周，若超过当月则为最后一个
                targetDate = firstTarget.AddDays(28); // 4*7=28
                if (targetDate.Month != date.Month)
                    targetDate = firstTarget.AddDays(21); // 若第5个超出月份，则取第4个
            }
            else
            {
                // 第1-4个：直接加(n-1)*7天
                targetDate = firstTarget.AddDays((n - 1) * 7);
            }

            return date.Date == targetDate.Date;
        }

        /// <summary>
        /// 获取指定日期的农历“日”短文本（如：初一、初十、十一、廿三、三十）。
        /// </summary>
        public string GetLunarDayShort(DateTime date)
        {
            try
            {
                var solar = new Solar(date.Year, date.Month, date.Day);
                var lunar = solar.Lunar;
                var prop = lunar.GetType().GetProperty("Day");
                if (prop != null)
                {
                    var dayObj = prop.GetValue(lunar);
                    if (dayObj is int d && d >= 1 && d <= 30)
                    {
                        return ToChineseLunarDay(d);
                    }
                }
                // 解析 ToString() 中的“月”后文本
                var token = lunar.ToString();
                var idxMonth = token.IndexOf('月');
                if (idxMonth >= 0 && idxMonth + 1 < token.Length)
                {
                    var after = token[(idxMonth + 1)..];
                    var end = after.IndexOf(' ');
                    if (end > 0) after = after[..end];
                    return after.Trim();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GetLunarDayShort failed", ex);
            }
            return string.Empty;
        }

        private static string ToChineseLunarDay(int d)
        {
            string[] nums = { "一","二","三","四","五","六","七","八","九","十" };
            if (d <= 10) return "初" + nums[d - 1];
            if (d < 20) return "十" + nums[d - 10 - 1]; // 11..19
            if (d == 20) return "二十";
            if (d < 30) return "廿" + nums[d - 20 - 1]; // 21..29
            return "三十";
        }
    }
}

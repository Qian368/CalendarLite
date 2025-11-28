using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace CalendarLite.Services
{
    /// <summary>
    /// 提供从 URL 或本地文件获取节假日 JSON 并写入本地 data.json 的服务（仅处理休假和补班信息）。
    /// </summary>
    public class ImportService
    {
        /// <summary>
        /// 从指定 URL 下载 JSON 并保存到目标路径；成功返回 true（仅处理休假和补班）。
        /// </summary>
        public async Task<bool> ImportFromUrlAsync(string url, string targetPath)
        {
            const int maxRetries = 3;
            const int baseTimeoutSeconds = 30;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        Logger.Warn("Import URL invalid: " + url);
                        return false;
                    }

                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(baseTimeoutSeconds);
                    
                    Logger.Info($"Import attempt {attempt}/{maxRetries} for URL: {url}");
                    var json = await client.GetStringAsync(uri);

                    return await ProcessHolidayJson(json, targetPath);
                }
                catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
                {
                    Logger.Warn($"Import attempt {attempt} failed, retrying... Error: {ex.Message}");
                    
                    // 指数退避策略：等待时间随重试次数增加
                    var delayMs = 1000 * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    Logger.Error($"ImportFromUrlAsync failed after {attempt} attempts", ex);
                    return false;
                }
            }
            
            Logger.Error($"ImportFromUrlAsync failed after {maxRetries} attempts", new Exception("All retry attempts exhausted"));
            return false;
        }

        // 判断是否为可重试的异常（如网络问题、超时等）
        private static bool IsRetryableException(Exception ex)
        {
            return ex is HttpRequestException 
                || ex is TaskCanceledException 
                || ex is TimeoutException
                || (ex.InnerException is SocketException);
        }

        /// <summary>
        /// 从本地文件读取 JSON 并保存到目标路径；成功返回 true（仅处理休假和补班）。
        /// </summary>
        public async Task<bool> ImportFromLocalFileAsync(string localFilePath, string targetPath)
        {
            try
            {
                if (!File.Exists(localFilePath))
                {
                    Logger.Warn("Local file not found: " + localFilePath);
                    return false;
                }

                var json = await File.ReadAllTextAsync(localFilePath);
                return await ProcessHolidayJson(json, targetPath);
            }
            catch (Exception ex)
            {
                Logger.Error("ImportFromLocalFileAsync failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 核心处理逻辑：解析JSON中的休假和补班信息，合并到本地data.json
        /// </summary>
        private async Task<bool> ProcessHolidayJson(string json, string targetPath)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    Logger.Warn("Import JSON root is not object");
                    return false;
                }

                string? yearStr = null;
                var holidays = new List<string>(); // 休假日期
                var workdays = new List<string>(); // 补班日期

                // 解析JSON中的holiday对象（兼容timor.tech格式）
                if (doc.RootElement.TryGetProperty("holiday", out var holidayObj) && holidayObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in holidayObj.EnumerateObject())
                    {
                        var elem = prop.Value;
                        if (elem.ValueKind != JsonValueKind.Object) continue;

                        // 提取日期
                        string? date = elem.TryGetProperty("date", out var dProp) ? dProp.GetString() : null;
                        if (string.IsNullOrEmpty(date) || !DateTime.TryParse(date, out var dt))
                            continue;

                        // 提取是否为休假（holiday=true为休假，false为补班）
                        bool isHoliday = elem.TryGetProperty("holiday", out var hProp) && hProp.GetBoolean();

                        // 收集数据
                        if (isHoliday) holidays.Add(date);
                        else workdays.Add(date);

                        // 记录年份（用于合并到对应年份的数据中）
                        if (yearStr == null)
                            yearStr = dt.Year.ToString();
                    }
                }
                // 兼容按年份直接组织的JSON结构（如{"2024": {"holidays": [...], "workdays": [...]}}）
                else
                {
                    foreach (var yearProp in doc.RootElement.EnumerateObject())
                    {
                        yearStr = yearProp.Name;
                        if (!int.TryParse(yearStr, out _)) continue;

                        var yearNode = yearProp.Value;
                        // 提取休假日期
                        if (yearNode.TryGetProperty("holidays", out var hElem) && hElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in hElem.EnumerateArray())
                            {
                                var date = item.GetString();
                                if (!string.IsNullOrEmpty(date)) holidays.Add(date);
                            }
                        }
                        // 提取补班日期
                        if (yearNode.TryGetProperty("workdays", out var wElem) && wElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in wElem.EnumerateArray())
                            {
                                var date = item.GetString();
                                if (!string.IsNullOrEmpty(date)) workdays.Add(date);
                            }
                        }
                    }
                }

                // 校验数据有效性
                if (holidays.Count == 0 && workdays.Count == 0)
                {
                    Logger.Warn("No holiday/workday data found in JSON");
                    return false;
                }
                if (string.IsNullOrEmpty(yearStr))
                    yearStr = DateTime.Now.Year.ToString(); // 默认为当前年份

                // 读取现有数据并合并
                var existingData = LoadExistingData(targetPath);
                if (!existingData.TryGetValue(yearStr, out var yearData))
                {
                    yearData = new Models.CalendarYearData();
                    existingData[yearStr] = yearData;
                }

                // 合并休假日期（去重+排序）
                var holidaySet = new HashSet<string>(yearData.holidays ?? new List<string>());
                foreach (var d in holidays) holidaySet.Add(d);
                yearData.holidays = holidaySet.OrderBy(x => x).ToList();

                // 合并补班日期（去重+排序）
                var workdaySet = new HashSet<string>(yearData.workdays ?? new List<string>());
                foreach (var d in workdays) workdaySet.Add(d);
                yearData.workdays = workdaySet.OrderBy(x => x).ToList();

                // 移除传统节日名称相关字段（如果不需要）
                yearData.traditional_holidays = null; // 或保留空字典

                // 保存合并后的数据
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(targetPath, JsonSerializer.Serialize(existingData, options));
                Logger.Info($"Import succeeded: Year={yearStr}, Holidays+={holidays.Count}, Workdays+={workdays.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("ProcessHolidayJson failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 加载本地已有的节假日数据（用于合并）
        /// </summary>
        private Dictionary<string, Models.CalendarYearData> LoadExistingData(string targetPath)
        {
            if (!File.Exists(targetPath))
                return new Dictionary<string, Models.CalendarYearData>();

            try
            {
                var json = File.ReadAllText(targetPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<Dictionary<string, Models.CalendarYearData>>(json, options)
                       ?? new Dictionary<string, Models.CalendarYearData>();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load existing data", ex);
                return new Dictionary<string, Models.CalendarYearData>();
            }
        }
    }
}
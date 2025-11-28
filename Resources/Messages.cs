namespace CalendarLite.Resources
{
    /// <summary>
    /// 集中管理用户可见的文本字符串（中文）。
    /// </summary>
    public static class Messages
    {
        public const string PrevMonth = "上一月";
        public const string NextMonth = "下一月";
        public const string HolidayBadge = "休";
        public const string WorkdayBadge = "班";
        public const string ErrorTitle = "错误";
        public const string DataLoadFailed = "数据文件加载失败，请检查 data.json。";
        public const string ImportTitle = "导入节假日数据";
        public const string ImportYearPrompt = "请输入年份（如 2025）";
        public const string ImportSuccess = "导入成功，已更新节假日数据。";
        public const string ImportFailed = "导入失败，请检查 URL 或网络。";
        public const string YearInvalid = "年份无效，请输入正确的年份（1900-2100）。";
        public const string ImportNoData = "该年份未获取到节假日数据。";
        public const string ImportButton = "导入";
        public const string TodayButton = "今天";
        public const string BatchImportButton = "联网一键导入";
        public const string BatchImportTitle = "批量导入节假日";
        
        public const string BatchImportSummary = "共导入 {0} 年；成功 {1} 年，无数据终止于 {2} 年。";
        public const string BatchImportAllSuccess = "全部导入成功，从 {0} 年开始至 {1} 年。";
        public const string BatchImportNoData = "导入失败，无数据：{0}";
        public const string BatchImportPartialSuccess = 
            "部分导入成功，从 {0} 年开始至 {1} 年。\n\n导入成功的年份为：{2}\n因网络等原因导入失败的年份为：{3}\n\n请尝试手动输入失败年份点击“确定”导入。";

        public const string TrayTooltip = "自制日历";
        public const string TrayShowCalendar = "显示日历";
        public const string TrayExit = "退出";
        public const string SelectLocalJsonFile = "选择本地 JSON 文件";
        public const string LocalImportSuccess = "本地文件导入成功，已更新节假日数据。";
        public const string LocalImportFailed = "本地文件导入失败，请检查文件格式。";
    }
}

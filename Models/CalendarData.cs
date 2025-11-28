using System;
using System.Collections.Generic;

namespace CalendarLite.Models
{
    /// <summary>
    /// JSON 数据的根结构，按年份组织。
    /// </summary>
    public class CalendarDataRoot : Dictionary<string, CalendarYearData> { }

    /// <summary>
    /// 单个年份的数据结构。
    /// </summary>
    public class CalendarYearData
    {
        public List<string>? holidays { get; set; }
        public List<string>? workdays { get; set; }
        public Dictionary<string, string>? lunar { get; set; }
        public Dictionary<string, string>? solar_terms { get; set; }
        public Dictionary<string, string>? traditional_holidays { get; set; }
        public Dictionary<string, string>? international_holidays { get; set; }
    }
}
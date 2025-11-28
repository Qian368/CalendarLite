using System;
using System.Windows;
using System.Windows.Media;

namespace CalendarLite.Models
{
    /// <summary>
    /// 单个日期单元格的绑定数据。
    /// </summary>
    public class DayItem
    {
        /// <summary>
        /// 对应的公历日期；为空表示占位单元格。
        /// </summary>
        public DateTime? Date { get; set; }
        public string Day { get; set; } = string.Empty;
        public Brush DayForeground { get; set; } = Brushes.Transparent;
        public string Lunar { get; set; } = string.Empty;
        public string ExtraInfo { get; set; } = string.Empty;
        public Brush ExtraForeground { get; set; } = Brushes.Transparent;
        public FontWeight ExtraFontWeight { get; set; } = FontWeights.Normal;
        public string BadgeText { get; set; } = string.Empty;
        public Brush BadgeColor { get; set; } = Brushes.Transparent;
        public Brush BorderBrush { get; set; } = Brushes.Transparent;
        public System.Windows.Thickness BorderThickness { get; set; } = new System.Windows.Thickness(0);

        /// <summary>
        /// 返回一个空白占位单元格。
        /// </summary>
        public static DayItem Empty => new DayItem();
    }
}
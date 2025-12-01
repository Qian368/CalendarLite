using System;
using System.Globalization;
using System.Windows.Markup;
using System.Windows.Media;

namespace CalendarLite.Resources
{
    /// <summary>
    /// 十六进制颜色解析扩展，兼容 #RRGGBB、#AARRGGBB（ARGB）以及插件常见的 #RRGGBBAA（RGBA）。
    /// - 默认自动识别 6 位与 8 位；8 位优先按 RGBA 解析以适配常见插件；
    /// - 可通过 Format 指定 "argb" 或 "rgba" 强制顺序；
    /// - 用法示例：Color="{conv:HexColor Value=#3efc48a9}" 或 Color="{conv:HexColor Value=#FF1842FD, Format=argb}"。
    /// </summary>
    public class HexColorExtension : MarkupExtension
    {
        /// <summary>
        /// 输入的十六进制颜色字符串，如 #RRGGBB、#AARRGGBB 或 #RRGGBBAA。
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 可选格式标记："argb" 或 "rgba"。不设置时 8 位默认按 RGBA 解析，6 位按不透明处理。
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// 生成解析后的 Color 对象。异常时返回不透明黑色以保证界面可用。
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Value)) return Colors.Black;
                var s = Value.Trim();
                if (!s.StartsWith("#")) s = "#" + s;

                // 去掉井号
                var hex = s.Substring(1);
                if (hex.Length == 6)
                {
                    // #RRGGBB → ARGB(FF, RR, GG, BB)
                    byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(0xFF, r, g, b);
                }
                else if (hex.Length == 8)
                {
                    var fmt = (Format ?? "rgba").ToLowerInvariant();
                    if (fmt == "argb")
                    {
                        // #AARRGGBB
                        byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                        byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                        byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                        return Color.FromArgb(a, r, g, b);
                    }
                    else
                    {
                        // 默认 RGBA：#RRGGBBAA
                        byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                        byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                        byte a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                        return Color.FromArgb(a, r, g, b);
                    }
                }

                // 其它长度不支持，回退为黑色
                return Colors.Black;
            }
            catch
            {
                return Colors.Black;
            }
        }
    }
}


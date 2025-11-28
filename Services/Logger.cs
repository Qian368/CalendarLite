using System;
using System.IO;

namespace CalendarLite.Services
{
    /// <summary>
    /// 简单文件日志记录器。
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _path = Path.Combine("logs", "app.log");

        /// <summary>
        /// 初始化日志文件路径。
        /// </summary>
        public static void Initialize(string path)
        {
            try
            {
                _path = path;
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch { }
        }

        /// <summary>
        /// 记录信息级日志。
        /// </summary>
        public static void Info(string message)
        {
            Write("INFO", message);
        }

        /// <summary>
        /// 记录警告级日志。
        /// </summary>
        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        /// <summary>
        /// 记录错误级日志，包含异常。
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message + " | " + ex);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n");
                }
            }
            catch { }
        }

        internal static void Error(string v)
        {
            throw new NotImplementedException();
        }
    }
}
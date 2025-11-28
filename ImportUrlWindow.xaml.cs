using System;
using System.Windows;
using Msgs = CalendarLite.Resources.Messages;
using CalendarLite.Services;

namespace CalendarLite
{
    /// <summary>
    /// 年份输入对话框：根据用户输入的年份拼接固定数据源 URL。
    /// </summary>
    public partial class ImportUrlWindow : Window
    {
        public string? Url { get; private set; }
        public bool BatchMode { get; private set; }
        public int BatchStartYear { get; private set; }

        public ImportUrlWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 确定提交：校验年份并生成 timor.tech 固定格式 URL。
        /// </summary>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var text = YearBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || !int.TryParse(text, out var year) || year < 1900 || year > 2100)
            {
                MessageBox.Show(Msgs.YearInvalid, Msgs.ImportTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Url = $"https://timor.tech/api/holiday/year/{year}";
            DialogResult = true;
        }

        /// <summary>
        /// 取消对话框。
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// 是否选择了本地文件导入。
        /// </summary>
        public bool IsLocalImport { get; private set; } = false;
        /// <summary>
        /// 本地文件路径（如果选择了本地导入）。
        /// </summary>  
        public string LocalFilePath { get; private set; } = string.Empty;

        private void LocalImport_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = Msgs.SelectLocalJsonFile,
            };

            if (openFileDialog.ShowDialog() == true)
            {
                IsLocalImport = true;// 选择了本地文件导入
                LocalFilePath = openFileDialog.FileName;// 记录本地文件路径
                DialogResult = true; // 关闭对话框并返回结果
            }
        }

        /// <summary>
        /// 批量导入：从今年开始逐年尝试导入，直到无数据年份。
        /// </summary>
        private void Batch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BatchMode = true;
                BatchStartYear = DateTime.Now.Year;
                
                // 添加确认对话框
                var result = MessageBox.Show(
                    $"确定要批量导入从本年 {DateTime.Now.Year} 年到最早有节假日安排年份的数据吗？",
                    $"确认从https://timor.tech/api/holiday/year/{{y}}批量导入",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Batch_Click failed", ex);
                MessageBox.Show(Msgs.ImportFailed, Msgs.ImportTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }                
    }
}
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SIAT.TSET;

namespace SIAT
{
    // 状态到颜色转换器
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? status = value as string;
            return status switch
            {
                "合格" => Brushes.Green,
                "不合格" => Brushes.Red,
                "待测" => Brushes.Gray,
                _ => Brushes.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 行号转换器
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Controls.DataGridRow row)
            {
                return row.GetIndex() + 1;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 状态到文本转换器
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TestStepStatus status)
            {
                return status switch
                {
                    TestStepStatus.Passed => "PASS",
                    TestStepStatus.Failed => "FAIL",
                    TestStepStatus.Pending => "-",
                    TestStepStatus.Running => "-",
                    TestStepStatus.Skipped => "-",
                    _ => "-"
                };
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 测试步骤状态到颜色转换器
    public class TestStepStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TestStepStatus status)
            {
                return status switch
                {
                    TestStepStatus.Passed => Brushes.Green,
                    TestStepStatus.Failed => Brushes.Red,
                    TestStepStatus.Running => Brushes.Blue,
                    TestStepStatus.Pending => Brushes.Gray,
                    TestStepStatus.Skipped => Brushes.Yellow,
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
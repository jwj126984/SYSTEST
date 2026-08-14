using System;
using System.Globalization;
using System.Windows.Data;

namespace SIAT.TSET
{
    /// <summary>
    /// 将展开状态转换为符号的转换器
    /// </summary>
    public class ExpandedToSymbolConverter : IValueConverter
    {
        public static ExpandedToSymbolConverter Instance { get; } = new ExpandedToSymbolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "−" : "+";
            }
            return "+";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
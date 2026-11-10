using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpectroscopicAnalysis.Host.Converters;

/// <summary>bool → Visibility 转换器；Inverse=true 时反转（空态显隐等场景）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>true 时反转转换结果。</summary>
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (Inverse)
        {
            flag = !flag;
        }
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

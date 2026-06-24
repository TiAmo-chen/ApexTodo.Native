using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ApexTodo.Windows.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (parameter?.ToString() == "Invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DueTimeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dueAt)
            return string.Empty;

        var isCompleted = parameter is string s && s == "completed";

        if (isCompleted)
            return $"截止 {dueAt:MM-dd}";

        var span = dueAt - DateTime.Now;

        if (span.TotalSeconds <= 0)
        {
            span = -span;
            if (span.TotalDays >= 1)
                return $"逾期 {(int)span.TotalDays}天{span.Hours}小时";
            if (span.TotalHours >= 1)
                return $"逾期 {(int)span.TotalHours}小时{span.Minutes}分钟";
            return $"逾期 {(int)span.TotalMinutes}分钟";
        }
        else
        {
            if (span.TotalDays >= 1)
                return $"还剩 {(int)span.TotalDays}天{span.Hours}小时";
            if (span.TotalHours >= 1)
                return $"还剩 {(int)span.TotalHours}小时{span.Minutes}分钟";
            return $"还剩 {(int)span.TotalMinutes}分钟";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DueTimeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dueAt)
            return Brushes.Transparent;

        var isCompleted = parameter is string s && s == "completed";
        if (isCompleted)
            return new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));

        var span = dueAt - DateTime.Now;

        if (span.TotalSeconds <= 0)
            return new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
        if (span.TotalMinutes < 60)
            return new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87));
        if (span.TotalHours < 24)
            return new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF));

        return new SolidColorBrush(Color.FromRgb(0xC0, 0xCC, 0xD6));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

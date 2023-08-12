using System;
using System.Globalization;
using System.Windows.Data;

namespace YouTubeDownloadTool;

internal sealed class IsNullConverter : IValueConverter
{
    public static IsNullConverter Instance { get; } = new();
    private IsNullConverter() { }

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

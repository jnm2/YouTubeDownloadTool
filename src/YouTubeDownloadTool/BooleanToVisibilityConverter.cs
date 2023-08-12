using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YouTubeDownloadTool;

internal sealed class BooleanToVisibilityConverter : IValueConverter
{
    public static BooleanToVisibilityConverter CollapsedWhenFalse { get; } = new(whenTrue: Visibility.Visible, whenFalse: Visibility.Collapsed);
    public static BooleanToVisibilityConverter CollapsedWhenTrue { get; } = new(whenFalse: Visibility.Collapsed, whenTrue: Visibility.Visible);
    public static BooleanToVisibilityConverter HiddenWhenFalse { get; } = new(whenTrue: Visibility.Visible, whenFalse: Visibility.Hidden);
    public static BooleanToVisibilityConverter HiddenWhenTrue { get; } = new(whenFalse: Visibility.Hidden, whenTrue: Visibility.Visible);

    private readonly object whenFalse, whenTrue;

    private BooleanToVisibilityConverter(object whenFalse, object whenTrue)
    {
        this.whenFalse = whenFalse;
        this.whenTrue = whenTrue;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? whenTrue : whenFalse;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return whenTrue.Equals(value);
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HalimLabs.Converters;

public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = value is null
                    || (value is string s && string.IsNullOrWhiteSpace(s))
                    || (value is int i && i == 0)
                    || (value is System.Collections.ICollection c && c.Count == 0);

        if (Invert)
            empty = !empty;

        return empty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

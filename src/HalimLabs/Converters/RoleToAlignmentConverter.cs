using System.Globalization;
using System.Windows;
using System.Windows.Data;
using HalimLabs.Models;

namespace HalimLabs.Converters;

public sealed class RoleToAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChatRole.User)
            return HorizontalAlignment.Right;
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

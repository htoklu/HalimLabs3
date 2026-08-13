using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HalimLabs.Models;

namespace HalimLabs.Converters;

public sealed class RoleToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush UserBrush = Create("#2B5278");
    private static readonly SolidColorBrush AssistantBrush = Create("#2A2A2E");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ChatRole.User ? UserBrush : AssistantBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Create(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}

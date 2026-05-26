using System;
using System.Globalization;
using System.Windows.Data;

namespace BabyShop;

public sealed class CurrencyLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var raw = value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "0 MDL";
        }

        return raw
            .Replace("₽", "MDL", StringComparison.Ordinal)
            .Replace("в‚Ѕ", "MDL", StringComparison.Ordinal)
            .Replace("RUB", "MDL", StringComparison.OrdinalIgnoreCase)
            .Replace("руб.", "MDL", StringComparison.OrdinalIgnoreCase)
            .Replace("руб", "MDL", StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value ?? string.Empty;
    }
}

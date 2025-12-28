using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kresti4kHelper.Converters;

public sealed class CountToStarGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var numericValue = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var starValue = Math.Max(0, numericValue);

        return new GridLength(starValue, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
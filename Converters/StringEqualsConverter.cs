using System;
using System.Globalization;
using System.Windows.Data;

namespace Kresti4kHelper.Converters;

public sealed class StringEqualsConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values.Length < 2)
		{
			return false;
		}

		var left = values[0]?.ToString();
		var right = values[1]?.ToString();
		return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
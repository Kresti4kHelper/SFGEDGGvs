using System;
using System.Globalization;
using System.Windows.Data;

namespace Kresti4kHelper.Converters;

public sealed class IndexOffsetConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is int number)
		{
			return number + 1;
		}

		if (int.TryParse(value?.ToString(), out var parsed))
		{
			return parsed + 1;
		}

		return value ?? 0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
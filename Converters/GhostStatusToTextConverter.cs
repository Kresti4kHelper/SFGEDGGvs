using System;
using System.Globalization;
using System.Windows.Data;
using Kresti4kHelper.Models;

namespace Kresti4kHelper.Converters;

public sealed class GhostStatusToTextConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value switch
		{
			GhostStatus.Unknown => "ме бшапюм",
			GhostStatus.Identified => "нопедекхк",
			GhostStatus.Rejected => "ме нопедекхк",
			_ => ""
		};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
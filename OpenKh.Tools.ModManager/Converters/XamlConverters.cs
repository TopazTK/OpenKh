using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Converters
{
    public class BooleanValueMapper : IValueConverter
    {
        public object? IfTrue { get; set; }
        public object? IfFalse { get; set; }
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b && b ? IfTrue : IfFalse;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class MultiBooleanCheckerConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return false;

            return values.All(x => x is bool) && values.All(x => (bool)x);
        }
    }

    public class IsListNotEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IEnumerable<object?>)
                return false;

            var _fetchValue = value as ObservableCollection<object?>;
            var _fetchCount = _fetchValue.Count();

            return _fetchCount != 0;
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class PropertyNotEqualConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count != 2 || values[0] == null || values[1] == null)
                return false;

            var _fetchFirst = (values[0] is Enum) ? System.Convert.ToInt32(values[0]) : values[0];
            var _fetchSecond = (values[1] is Enum) ? System.Convert.ToInt32(values[1]) : values[1];

            return _fetchFirst is int a && _fetchSecond is int b && a != b;
        }
    }
    public class PropertyIsEqualConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count != 2 || values[0] == null || values[1] == null)
                return false;

            var _fetchFirst = (values[0] is Enum) ? System.Convert.ToInt32(values[0]) : values[0];
            var _fetchSecond = (values[1] is Enum) ? System.Convert.ToInt32(values[1]) : values[1];

            return _fetchFirst is int a && _fetchSecond is int b && a == b;
        }
    }

    public class NonNullPropertyConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return false;

            return values[0] != null && values[1] is bool b && b;
        }
    }
}

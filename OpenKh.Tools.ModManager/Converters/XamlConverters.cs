using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
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
}

using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OGVColorCatcher.Services
{
    public sealed class DecimalPlacesConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2) return "-";

            var v = values[0];
            if (v is null) return "-";

            var decimalsObj = values[1];
            var decimals = 0;

            if (decimalsObj is int i) decimals = i;
            else if (decimalsObj is double d) decimals = (int)d;
            else if (decimalsObj is string s && int.TryParse(s, out var parsed)) decimals = parsed;

            if (!TryToDouble(v, culture, out var number))
                return v.ToString();

            return number.ToString($"F{Math.Max(0, decimals)}", culture);
        }

        private static bool TryToDouble(object v, CultureInfo culture, out double result)
        {
            switch (v)
            {
                case double d: result = d; return true;
                case float f: result = f; return true;
                case decimal m: result = (double)m; return true;
                case int i: result = i; return true;
                case long l: result = l; return true;
                case string s:
                    return double.TryParse(s, NumberStyles.Any, culture, out result);
                default:
                    try
                    {
                        result = System.Convert.ToDouble(v, culture);
                        return true;
                    }
                    catch
                    {
                        result = 0;
                        return false;
                    }
            }
        }
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace PicSerial
{
    [ValueConversion(typeof(double), typeof(int))]
    public class WidthToColumnsConverter : IValueConverter
    {
        // Approximate thumbnail total width (including margins). Adjust as needed.
        private const double ColumnWidth = 160.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 1;

            if (value is double width)
            {
                // Ensure at least one column
                int cols = Math.Max(1, (int)Math.Floor(width / ColumnWidth));
                return cols;
            }

            // Try to handle other numeric types gracefully
            if (double.TryParse(value.ToString(), out double parsed))
            {
                int cols = Math.Max(1, (int)Math.Floor(parsed / ColumnWidth));
                return cols;
            }

            return 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

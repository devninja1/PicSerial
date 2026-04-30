using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PicSerial
{
    public class WidthToColumnsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Assume each thumbnail cell is ~150px wide
                int columns = Math.Max(1, (int)(width / 160));

                // Clamp to number of items if available
                if (Application.Current.MainWindow is MainWindow main &&
                    main.ThumbnailList.Items.Count > 0)
                {
                    int itemCount = main.ThumbnailList.Items.Count;
                    columns = Math.Min(columns, itemCount);
                    if (itemCount < columns) columns = itemCount; // clamp
                }
                return columns;
            }
            return 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

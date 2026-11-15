using System.Globalization;

namespace SyncConfiguration.Convertors
{
    public class PercentageToScreenWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var percentage = (byte)value;
            var percentageAsDecimal = (decimal)percentage / 100;
            // Get screen dimensions
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            double screenWidth = displayInfo.Width / displayInfo.Density;
            double screenHeight = displayInfo.Height / displayInfo.Density;

            var width = System.Convert.ToDecimal(screenWidth) * percentageAsDecimal;

            return width;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

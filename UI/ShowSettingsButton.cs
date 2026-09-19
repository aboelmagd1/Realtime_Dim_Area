using System;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework.Contracts;

namespace GeoMetrics.UI
{
    /// <summary>Button in the ribbon that opens the Settings Dock Pane.</summary>
    internal class ShowSettingsButton : Button
    {
        public ShowSettingsButton()
        {
            try
            {
                LargeImage = new BitmapImage(new Uri("pack://application:,,,/GeoMetrics;component/Images/GeoMetricsSettings_32.png", UriKind.Absolute));
                SmallImage = new BitmapImage(new Uri("pack://application:,,,/GeoMetrics;component/Images/GeoMetricsSettings_16.png", UriKind.Absolute));
            }
            catch
            {
                // Fallback to Config.daml declaration if pack URI cannot be resolved
            }
        }

        protected override void OnClick() => DimensionSettingsPaneViewModel.Show();
    }
}

using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework.Contracts;

namespace GeoMetrics.UI
{
    /// <summary>
    /// Toggle button on the ribbon for GeoMetrics monitoring.
    /// Synchronizes bidirectionally with DimensionSettings.IsEnabled and provides
    /// direct ON / OFF control from the top ribbon bar with dynamic icon switching.
    /// </summary>
    internal class DimensionToggleButton : Button
    {
        private static ImageSource _onLargeImage;
        private static ImageSource _onSmallImage;
        private static ImageSource _offLargeImage;
        private static ImageSource _offSmallImage;
        private static bool _imagesLoaded;

        public DimensionToggleButton()
        {
            EnsureImagesLoaded();

            var settings = Module1.Current?.Settings;
            if (settings != null)
            {
                IsChecked = settings.IsEnabled;
                UpdateAppearance(settings.IsEnabled);

                // Synchronize with settings pane changes
                settings.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(Models.DimensionSettings.IsEnabled))
                    {
                        IsChecked = settings.IsEnabled;
                        UpdateAppearance(settings.IsEnabled);
                    }
                };
            }
            else
            {
                IsChecked = false;
                UpdateAppearance(false);
            }
        }

        private static void EnsureImagesLoaded()
        {
            if (_imagesLoaded) return;
            try
            {
                _onLargeImage = new BitmapImage(new Uri("pack://application:,,,/GeoMetrics;component/Images/GeoMetricsToggle_ON_32.png", UriKind.Absolute));
                _onSmallImage = new BitmapImage(new Uri("pack://application:,,,/GeoMetrics;component/Images/GeoMetricsToggle_ON_16.png", UriKind.Absolute));
                _offLargeImage = new BitmapImage(new Uri("pack://application:,,,/GeoMetrics;component/Images/GeoMetricsToggle_OFF_32.png", UriKind.Absolute));
                _offSmallImage = new BitmapImage(new Uri("pack://application:,,,/GeoMetrics;component/Images/GeoMetricsToggle_OFF_16.png", UriKind.Absolute));
            }
            catch
            {
                // Graceful fallback to default DAML icons if pack URIs are unavailable
            }
            _imagesLoaded = true;
        }

        protected override void OnClick()
        {
            bool newState = !IsChecked;
            IsChecked = newState;

            var settings = Module1.Current?.Settings;
            if (settings != null)
            {
                settings.IsEnabled = newState;
            }
            else
            {
                var engine = Module1.Current?.Engine;
                if (newState)
                    engine?.Enable();
                else
                    engine?.Disable();
            }

            UpdateAppearance(newState);
        }

        protected override void OnUpdate()
        {
            var isEnabled = Module1.Current?.Settings?.IsEnabled ?? false;
            if (IsChecked != isEnabled)
            {
                IsChecked = isEnabled;
                UpdateAppearance(isEnabled);
            }
            Enabled = true;
        }

        private void UpdateAppearance(bool isEnabled)
        {
            Caption = isEnabled ? "GeoMetrics (ON)" : "GeoMetrics (OFF)";
            Tooltip = isEnabled
                ? "GeoMetrics is ON. Measurements update live during editing and selection. Click to turn OFF."
                : "GeoMetrics is OFF. Click to turn ON real-time measurements.";

            if (isEnabled)
            {
                if (_onLargeImage != null) LargeImage = _onLargeImage;
                if (_onSmallImage != null) SmallImage = _onSmallImage;
            }
            else
            {
                if (_offLargeImage != null) LargeImage = _offLargeImage;
                if (_offSmallImage != null) SmallImage = _offSmallImage;
            }
        }
    }
}

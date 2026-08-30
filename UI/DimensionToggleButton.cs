using ArcGIS.Desktop.Framework.Contracts;

namespace GeoMetrics.UI
{
    /// <summary>
    /// Toggle button on the ribbon for GeoMetrics monitoring.
    /// Synchronizes bidirectionally with DimensionSettings.IsEnabled and provides
    /// direct ON / OFF control from the top ribbon bar.
    /// </summary>
    internal class DimensionToggleButton : Button
    {
        public DimensionToggleButton()
        {
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
        }
    }
}

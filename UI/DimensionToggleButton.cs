using ArcGIS.Desktop.Framework.Contracts;

namespace DimensionOverlay.UI
{
    /// <summary>
    /// Toggle button on the ribbon for Dynamic Dimensions monitoring.
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
                IsChecked = true;
                UpdateAppearance(true);
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
            var isEnabled = Module1.Current?.Settings?.IsEnabled ?? true;
            if (IsChecked != isEnabled)
            {
                IsChecked = isEnabled;
                UpdateAppearance(isEnabled);
            }
            Enabled = true;
        }

        private void UpdateAppearance(bool isEnabled)
        {
            Caption = isEnabled ? "Dynamic Dimensions (ON)" : "Dynamic Dimensions (OFF)";
            Tooltip = isEnabled
                ? "Dynamic Dimensions is ON. Dimensions update live during editing and selection. Click to turn OFF."
                : "Dynamic Dimensions is OFF. Click to turn ON real-time dimensions.";
        }
    }
}

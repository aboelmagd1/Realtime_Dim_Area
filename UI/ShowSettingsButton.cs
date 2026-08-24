using ArcGIS.Desktop.Framework.Contracts;

namespace DimensionOverlay.UI
{
    /// <summary>Button in the ribbon that opens the Settings Dock Pane.</summary>
    internal class ShowSettingsButton : Button
    {
        protected override void OnClick() => DimensionSettingsPaneViewModel.Show();
    }
}

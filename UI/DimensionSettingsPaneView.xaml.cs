using System;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework;

namespace GeoMetrics.UI
{
    public partial class DimensionSettingsPaneView : UserControl
    {
        public DimensionSettingsPaneView()
        {
            InitializeComponent();
            ApplyThemeColors();
            Loaded += (s, e) => ApplyThemeColors();
            IsVisibleChanged += (s, e) => { if (IsVisible) ApplyThemeColors(); };
        }

        private void ApplyThemeColors()
        {
            try
            {
                // In Dark Theme or High Contrast, ensure pure crisp white text across all controls
                bool isDark = FrameworkApplication.ApplicationTheme == ApplicationTheme.Dark ||
                              FrameworkApplication.ApplicationTheme == ApplicationTheme.HighContrast;

                if (isDark)
                {
                    var whiteBrush = new SolidColorBrush(Colors.White);
                    whiteBrush.Freeze();

                    var offWhiteBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    offWhiteBrush.Freeze();

                    var darkBgBrush = new SolidColorBrush(Color.FromRgb(42, 42, 42));
                    darkBgBrush.Freeze();

                    var hoverBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68));
                    hoverBrush.Freeze();

                    var borderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                    borderBrush.Freeze();

                    Resources["Esri_TextPrimaryBrush"] = whiteBrush;
                    Resources["Esri_TextSecondaryBrush"] = offWhiteBrush;
                    Resources["DropdownTextBrush"] = whiteBrush;
                    Resources["DropdownBackgroundBrush"] = darkBgBrush;
                    Resources["DropdownHighlightBrush"] = hoverBrush;
                    Resources["DropdownBorderBrush"] = borderBrush;
                }
                else
                {
                    var blackBrush = new SolidColorBrush(Colors.Black);
                    blackBrush.Freeze();

                    var lightBgBrush = new SolidColorBrush(Colors.White);
                    lightBgBrush.Freeze();

                    var lightHoverBrush = new SolidColorBrush(Color.FromRgb(230, 240, 250));
                    lightHoverBrush.Freeze();

                    var lightBorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                    lightBorderBrush.Freeze();

                    Resources["Esri_TextPrimaryBrush"] = blackBrush;
                    Resources["Esri_TextSecondaryBrush"] = blackBrush;
                    Resources["DropdownTextBrush"] = blackBrush;
                    Resources["DropdownBackgroundBrush"] = lightBgBrush;
                    Resources["DropdownHighlightBrush"] = lightHoverBrush;
                    Resources["DropdownBorderBrush"] = lightBorderBrush;
                }
            }
            catch
            {
                // Safe for design-time
            }
        }
    }
}

using System.ComponentModel;
using ArcGIS.Desktop.Mapping;

namespace DimensionOverlay.Models
{
    public enum MeasurementMethod
    {
        [Description("Automatic (Geographic → Geodesic, Projected → Planar)")]
        Automatic,
        [Description("Planar (use CRS linear units)")]
        Planar,
        [Description("Geodesic (always in meters)")]
        Geodesic
    }

    public enum DisplayUnitOption
    {
        [Description("Layer Units (native CRS)")]   LayerNative,
        [Description("Meters (m)")]                 Meters,
        [Description("Feet (ft)")]                  Feet,
        [Description("US Survey Feet (ft)")]        US_Survey_Feet,
        [Description("Kilometers (km)")]            Kilometers,
        [Description("Miles (mi)")]                 Miles
    }

    public enum DimensionStyleOption
    {
        [Description("Numbers Only (Clean Text)")]     Numbers_Only,
        [Description("CAD Standard (Lines & Ticks)")] CAD_Standard,
        [Description("Minimal (Dim Line Only)")]       Minimal,
        [Description("High Contrast")]                 High_Contrast
    }

    public enum TextColorOption
    {
        [Description("Black")]        Black,
        [Description("Dark Blue")]    Blue,
        [Description("Red")]          Red,
        [Description("Dark Green")]   Green,
        [Description("Vivid Orange")] Orange,
        [Description("White")]        White,
        [Description("Yellow")]       Yellow,
        [Description("Cyan")]         Cyan
    }

    /// <summary>
    /// All user-configurable settings for the Dynamic Dimension Overlay.
    /// Implements INotifyPropertyChanged so the settings pane binds reactively.
    /// </summary>
    public sealed class DimensionSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Master ON / OFF Toggle Switch ─────────────────────────────────────────
        private bool _isEnabled = false;
        /// <summary>Master switch: turns real-time dimension monitoring ON or OFF (Default: OFF).</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    Raise(nameof(IsEnabled));
                    System.Diagnostics.Trace.WriteLine($"[DIM] Overlay Enabled = {_isEnabled}");
                    if (_isEnabled)
                        Module1.Current?.Engine?.Enable();
                    else
                        Module1.Current?.Engine?.Disable();
                }
            }
        }

        // ── Service Layer Support ─────────────────────────────────────────────────
        private bool _applyToServiceLayers = false;
        /// <summary>
        /// Whether Dynamic Dimension Overlay is allowed to work on ArcGIS service-based layers (Default: OFF).
        /// </summary>
        public bool ApplyToServiceLayers
        {
            get => _applyToServiceLayers;
            set
            {
                if (_applyToServiceLayers != value)
                {
                    _applyToServiceLayers = value;
                    Raise(nameof(ApplyToServiceLayers));
                    System.Diagnostics.Trace.WriteLine($"[DIM] ApplyToServiceLayers = {_applyToServiceLayers}");
                    Module1.Current?.Engine?.RefreshSelectionDisplay();
                }
            }
        }

        // ── Optional target layer ─────────────────────────────────────────────────
        private FeatureLayer _targetLayer;
        /// <summary>When set, the monitor loads features from this layer only.</summary>
        public FeatureLayer TargetLayer
        {
            get => _targetLayer;
            set
            {
                _targetLayer = value;
                Raise(nameof(TargetLayer));
                System.Diagnostics.Trace.WriteLine($"[DIM] Target Layer = {(_targetLayer != null ? _targetLayer.Name : "None")}");
                Module1.Current?.Engine?.RefreshSelectionDisplay();
            }
        }

        // ── Display toggles ───────────────────────────────────────────────────────
        private bool _showSegmentLength = true;
        public bool ShowSegmentLength
        {
            get => _showSegmentLength;
            set { _showSegmentLength = value; Raise(nameof(ShowSegmentLength)); }
        }

        private bool _showArea = true;
        public bool ShowArea
        {
            get => _showArea;
            set { _showArea = value; Raise(nameof(ShowArea)); }
        }

        private bool _showPerimeter = false;
        public bool ShowPerimeter
        {
            get => _showPerimeter;
            set { _showPerimeter = value; Raise(nameof(ShowPerimeter)); }
        }

        private bool _showBearings;
        public bool ShowBearings
        {
            get => _showBearings;
            set { _showBearings = value; Raise(nameof(ShowBearings)); }
        }

        private bool _showVertexAngles = false;
        /// <summary>Show measured angle between consecutive segments at vertices (Default: OFF).</summary>
        public bool ShowVertexAngles
        {
            get => _showVertexAngles;
            set { _showVertexAngles = value; Raise(nameof(ShowVertexAngles)); }
        }

        private bool _showAreaDifference;
        public bool ShowAreaDifference
        {
            get => _showAreaDifference;
            set { _showAreaDifference = value; Raise(nameof(ShowAreaDifference)); }
        }

        private bool _showToleranceStatus;
        public bool ShowToleranceStatus
        {
            get => _showToleranceStatus;
            set { _showToleranceStatus = value; Raise(nameof(ShowToleranceStatus)); }
        }

        // ── Measurement method ────────────────────────────────────────────────────
        private MeasurementMethod _method = MeasurementMethod.Automatic;
        public MeasurementMethod Method
        {
            get => _method;
            set { _method = value; Raise(nameof(Method)); }
        }

        private DisplayUnitOption _displayUnit = DisplayUnitOption.Meters;
        public DisplayUnitOption DisplayUnit
        {
            get => _displayUnit;
            set
            {
                _displayUnit = value;
                Raise(nameof(DisplayUnit));
                System.Diagnostics.Trace.WriteLine($"[DIM] Display Unit = {_displayUnit}");
            }
        }

        private int _precision = 2;
        /// <summary>Number of decimal places for all displayed values.</summary>
        public int Precision
        {
            get => _precision;
            set { _precision = value; Raise(nameof(Precision)); }
        }

        private double _areaTolerance = 5.0;
        /// <summary>Tolerance threshold in % for the QC area-difference check.</summary>
        public double AreaTolerance
        {
            get => _areaTolerance;
            set { _areaTolerance = value; Raise(nameof(AreaTolerance)); }
        }

        // ── Rendering & Style (Default = Numbers_Only) ────────────────────────────
        private DimensionStyleOption _dimensionStyle = DimensionStyleOption.Numbers_Only;
        public DimensionStyleOption DimensionStyle
        {
            get => _dimensionStyle;
            set
            {
                _dimensionStyle = value;
                Raise(nameof(DimensionStyle));
                System.Diagnostics.Trace.WriteLine($"[DIM] Style = {_dimensionStyle}");
            }
        }

        private TextColorOption _textColor = TextColorOption.Black;
        public TextColorOption TextColor
        {
            get => _textColor;
            set { _textColor = value; Raise(nameof(TextColor)); }
        }

        private double _offsetPixels = 14.0;
        /// <summary>Perpendicular pixel distance from the segment to the dimension label.</summary>
        public double OffsetPixels
        {
            get => _offsetPixels;
            set { _offsetPixels = value; Raise(nameof(OffsetPixels)); }
        }

        // ── Live status (set by engine — read-only from UI) ───────────────────────
        private string _crsName = "—";
        public string CrsName
        {
            get => _crsName;
            set { _crsName = value; Raise(nameof(CrsName)); }
        }

        private int _wkid;
        public int Wkid
        {
            get => _wkid;
            set { _wkid = value; Raise(nameof(Wkid)); }
        }

        private string _resolvedMethod = "—";
        public string ResolvedMethod
        {
            get => _resolvedMethod;
            set { _resolvedMethod = value; Raise(nameof(ResolvedMethod)); }
        }
    }
}

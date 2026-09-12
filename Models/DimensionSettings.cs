using System;
using System.ComponentModel;
using ArcGIS.Desktop.Mapping;

namespace GeoMetrics.Models
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
    /// All user-configurable settings for GeoMetrics.
    /// Implements INotifyPropertyChanged so the settings pane binds reactively.
    /// </summary>
    public sealed class DimensionSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Master ON / OFF Toggle Switch ─────────────────────────────────────────
        private bool _isEnabled = false;
        /// <summary>Master switch: turns real-time geometry measurements ON or OFF (Default: OFF).</summary>
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
        /// Whether GeoMetrics is allowed to work on ArcGIS service-based layers (Default: OFF).
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

        // ── Multi-Feature Selection ──────────────────────────────────────────────
        private bool _multiFeatureEnabled = false;
        /// <summary>
        /// When enabled, GeoMetrics measures and annotates all selected features instead of only the first one (Default: OFF).
        /// </summary>
        public bool MultiFeatureEnabled
        {
            get => _multiFeatureEnabled;
            set
            {
                if (_multiFeatureEnabled != value)
                {
                    _multiFeatureEnabled = value;
                    Raise(nameof(MultiFeatureEnabled));
                    System.Diagnostics.Trace.WriteLine($"[DIM] MultiFeatureEnabled = {_multiFeatureEnabled}");
                    Module1.Current?.Engine?.RefreshSelectionDisplay();
                }
            }
        }

        private bool _multiFeaturePolygonInside = true;
        /// <summary>
        /// When multi-feature selection is enabled, draws polygon segment dimensions inside the polygon interior (Default: ON).
        /// </summary>
        public bool MultiFeaturePolygonInside
        {
            get => _multiFeaturePolygonInside;
            set
            {
                if (_multiFeaturePolygonInside != value)
                {
                    _multiFeaturePolygonInside = value;
                    Raise(nameof(MultiFeaturePolygonInside));
                    System.Diagnostics.Trace.WriteLine($"[DIM] MultiFeaturePolygonInside = {_multiFeaturePolygonInside}");
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private int _maxFeaturesLimit = 50;
        /// <summary>
        /// Maximum number of selected features to measure simultaneously (Default: 50).
        /// </summary>
        public int MaxFeaturesLimit
        {
            get => _maxFeaturesLimit;
            set
            {
                int clamped = System.Math.Clamp(value, 1, 500);
                if (_maxFeaturesLimit != clamped)
                {
                    _maxFeaturesLimit = clamped;
                    Raise(nameof(MaxFeaturesLimit));
                    System.Diagnostics.Trace.WriteLine($"[DIM] MaxFeaturesLimit = {_maxFeaturesLimit}");
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
            set
            {
                if (_showSegmentLength != value)
                {
                    _showSegmentLength = value;
                    Raise(nameof(ShowSegmentLength));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showArea = true;
        public bool ShowArea
        {
            get => _showArea;
            set
            {
                if (_showArea != value)
                {
                    _showArea = value;
                    Raise(nameof(ShowArea));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showPerimeter = false;
        public bool ShowPerimeter
        {
            get => _showPerimeter;
            set
            {
                if (_showPerimeter != value)
                {
                    _showPerimeter = value;
                    Raise(nameof(ShowPerimeter));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showBearings;
        public bool ShowBearings
        {
            get => _showBearings;
            set
            {
                if (_showBearings != value)
                {
                    _showBearings = value;
                    Raise(nameof(ShowBearings));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showVertexAngles = false;
        /// <summary>Show measured angle between consecutive segments at vertices (Default: OFF).</summary>
        public bool ShowVertexAngles
        {
            get => _showVertexAngles;
            set
            {
                if (_showVertexAngles != value)
                {
                    _showVertexAngles = value;
                    Raise(nameof(ShowVertexAngles));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showVertexCoordinates = false;
        /// <summary>Show X and Y coordinates at vertices (Default: OFF).</summary>
        public bool ShowVertexCoordinates
        {
            get => _showVertexCoordinates;
            set
            {
                if (_showVertexCoordinates != value)
                {
                    _showVertexCoordinates = value;
                    Raise(nameof(ShowVertexCoordinates));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private int _coordinatePrecision = 4;
        /// <summary>Number of decimal places for vertex coordinates (Default: 4).</summary>
        public int CoordinatePrecision
        {
            get => _coordinatePrecision;
            set
            {
                int clamped = System.Math.Clamp(value, 0, 8);
                if (_coordinatePrecision != clamped)
                {
                    _coordinatePrecision = clamped;
                    Raise(nameof(CoordinatePrecision));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showAreaDifference;
        public bool ShowAreaDifference
        {
            get => _showAreaDifference;
            set
            {
                if (_showAreaDifference != value)
                {
                    _showAreaDifference = value;
                    Raise(nameof(ShowAreaDifference));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showToleranceStatus;
        public bool ShowToleranceStatus
        {
            get => _showToleranceStatus;
            set { _showToleranceStatus = value; Raise(nameof(ShowToleranceStatus)); }
        }

        private bool _showViewportHud = false;
        /// <summary>Show visible vertices and segments count HUD in the viewport top-left (Default: OFF).</summary>
        public bool ShowViewportHud
        {
            get => _showViewportHud;
            set
            {
                if (_showViewportHud != value)
                {
                    _showViewportHud = value;
                    Raise(nameof(ShowViewportHud));
                    System.Diagnostics.Trace.WriteLine($"[DIM] Viewport HUD Enabled = {_showViewportHud}");
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private bool _showHiddenDimensionsWarning = false;
        /// <summary>
        /// Show warning in the viewport top-right when some segments in the viewport are not displayed due to zoom or length (Default: OFF).
        /// </summary>
        public bool ShowHiddenDimensionsWarning
        {
            get => _showHiddenDimensionsWarning;
            set
            {
                if (_showHiddenDimensionsWarning != value)
                {
                    _showHiddenDimensionsWarning = value;
                    Raise(nameof(ShowHiddenDimensionsWarning));
                    System.Diagnostics.Trace.WriteLine($"[DIM] ShowHiddenDimensionsWarning = {_showHiddenDimensionsWarning}");
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
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
            set
            {
                if (_precision != value)
                {
                    _precision = value;
                    Raise(nameof(Precision));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
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
                if (_dimensionStyle != value)
                {
                    _dimensionStyle = value;
                    Raise(nameof(DimensionStyle));
                    System.Diagnostics.Trace.WriteLine($"[DIM] Style = {_dimensionStyle}");
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private TextColorOption _textColor = TextColorOption.Black;
        public TextColorOption TextColor
        {
            get => _textColor;
            set
            {
                if (_textColor != value)
                {
                    _textColor = value;
                    Raise(nameof(TextColor));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
        }

        private double _offsetPixels = 14.0;
        /// <summary>Perpendicular pixel distance from the segment to the dimension label.</summary>
        public double OffsetPixels
        {
            get => _offsetPixels;
            set
            {
                if (Math.Abs(_offsetPixels - value) > 0.001)
                {
                    _offsetPixels = value;
                    Raise(nameof(OffsetPixels));
                    Module1.Current?.Engine?.RefreshOverlayLayout();
                }
            }
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

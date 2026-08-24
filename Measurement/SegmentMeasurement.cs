using ArcGIS.Core.Geometry;

namespace DimensionOverlay.Measurement
{
    /// <summary>
    /// Immutable snapshot of all measured data for a single boundary segment.
    /// Produced by GeometryMeasurementService and consumed by DimensionRenderer.
    /// </summary>
    public sealed class SegmentMeasurement
    {
        /// <summary>Start vertex of the segment (in the layer's CRS).</summary>
        public MapPoint Start { get; }

        /// <summary>End vertex of the segment (in the layer's CRS).</summary>
        public MapPoint End { get; }

        /// <summary>
        /// Segment length in native CRS units (meters for geodesic,
        /// CRS linear unit for planar). Used for sorting major/minor segments.
        /// </summary>
        public double NativeLength { get; }

        /// <summary>Length converted to the user's chosen display unit.</summary>
        public double DisplayLength { get; }

        /// <summary>Abbreviation to render next to the number ("m", "ft", "km", …).</summary>
        public string UnitAbbrev { get; }

        /// <summary>Bearing in degrees 0–360 clockwise from north. Null when not requested.</summary>
        public double? Bearing { get; }

        public SegmentMeasurement(
            MapPoint start, MapPoint end,
            double nativeLength, double displayLength, string unitAbbrev,
            double? bearing = null)
        {
            Start         = start;
            End           = end;
            NativeLength  = nativeLength;
            DisplayLength = displayLength;
            UnitAbbrev    = unitAbbrev;
            Bearing       = bearing;
        }
    }
}

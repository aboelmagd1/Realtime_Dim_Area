using ArcGIS.Core.Geometry;

namespace GeoMetrics.Measurement
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

        /// <summary>True if this segment is a curve (e.g. Arc, Elliptic Arc, Cubic Bezier).</summary>
        public bool IsCurve { get; }

        /// <summary>True geometric midpoint along the segment curve path.</summary>
        public MapPoint MidPoint { get; }

        /// <summary>Tangent orientation angle in degrees (-90 to +90) at the segment midpoint for readable label orientation.</summary>
        public double? TangentAngle { get; }

        /// <summary>Central / sweep angle in degrees for circular or elliptic arcs. Null for straight lines or beziers.</summary>
        public double? CentralAngle { get; }

        /// <summary>Underlying segment geometry type (Line, EllipticArc, Bezier, etc.).</summary>
        public SegmentType SegmentType { get; }

        public SegmentMeasurement(
            MapPoint start, MapPoint end,
            double nativeLength, double displayLength, string unitAbbrev,
            double? bearing = null,
            bool isCurve = false,
            MapPoint midPoint = null,
            double? tangentAngle = null,
            double? centralAngle = null,
            SegmentType segmentType = SegmentType.Line)
        {
            Start         = start;
            End           = end;
            NativeLength  = nativeLength;
            DisplayLength = displayLength;
            UnitAbbrev    = unitAbbrev;
            Bearing       = bearing;
            IsCurve       = isCurve;
            MidPoint      = midPoint;
            TangentAngle  = tangentAngle;
            CentralAngle  = centralAngle;
            SegmentType   = segmentType;
        }
    }
}

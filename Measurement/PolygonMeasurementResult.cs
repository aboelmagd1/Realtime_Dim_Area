using System.Collections.Generic;
using ArcGIS.Core.Geometry;

namespace DimensionOverlay.Measurement
{
    /// <summary>
    /// Represents an angle measurement at a vertex between two consecutive segments.
    /// </summary>
    public sealed class VertexAngleMeasurement
    {
        public MapPoint Vertex { get; }
        public double AngleDegrees { get; }
        public MapPoint PrevPoint { get; }
        public MapPoint NextPoint { get; }

        public VertexAngleMeasurement(MapPoint vertex, double angleDegrees, MapPoint prevPoint, MapPoint nextPoint)
        {
            Vertex = vertex;
            AngleDegrees = angleDegrees;
            PrevPoint = prevPoint;
            NextPoint = nextPoint;
        }
    }

    /// <summary>
    /// Immutable snapshot of all measurement data for a polygon sketch.
    /// Produced by GeometryMeasurementService and passed to DimensionOverlayManager.
    /// </summary>
    public sealed class PolygonMeasurementResult
    {
        /// <summary>Source polygon geometry for dynamic viewport-aware label placement.</summary>
        public Polygon SourcePolygon { get; }

        /// <summary>All boundary segments across all polygon parts.</summary>
        public List<SegmentMeasurement> Segments { get; }

        /// <summary>All measured vertex angles between consecutive segments.</summary>
        public List<VertexAngleMeasurement> VertexAngles { get; }

        /// <summary>Total area converted to the user's display unit.</summary>
        public double DisplayArea { get; }

        /// <summary>Area unit abbreviation ("m²", "ft²", "km²", …).</summary>
        public string AreaUnitAbbrev { get; }

        /// <summary>Total perimeter (sum of all ring lengths) in display units.</summary>
        public double DisplayPerimeter { get; }

        /// <summary>Linear unit abbreviation for perimeter label.</summary>
        public string LinearUnitAbbrev { get; }

        /// <summary>
        /// Guaranteed interior point for the area label.
        /// Uses GeometryEngine.LabelPoint (handles concave shapes), falls back to centroid.
        /// </summary>
        public MapPoint InteriorLabelPoint { get; }

        // ── Optional QC fields ────────────────────────────────────────────────────

        /// <summary>Original area (before this edit session) in display units. Null if QC is disabled.</summary>
        public double? OriginalDisplayArea { get; set; }

        /// <summary>True/false within-tolerance status. Null if not computed.</summary>
        public bool? WithinTolerance { get; set; }

        public PolygonMeasurementResult(
            Polygon sourcePolygon,
            List<SegmentMeasurement> segments,
            double displayArea, string areaUnitAbbrev,
            double displayPerimeter, string linearUnitAbbrev,
            MapPoint interiorLabelPoint,
            List<VertexAngleMeasurement> vertexAngles = null)
        {
            SourcePolygon      = sourcePolygon;
            Segments           = segments;
            DisplayArea        = displayArea;
            AreaUnitAbbrev     = areaUnitAbbrev;
            DisplayPerimeter   = displayPerimeter;
            LinearUnitAbbrev   = linearUnitAbbrev;
            InteriorLabelPoint = interiorLabelPoint;
            VertexAngles       = vertexAngles ?? new List<VertexAngleMeasurement>();
        }
    }

    /// <summary>
    /// Immutable snapshot of all measurement data for a polyline sketch.
    /// </summary>
    public sealed class PolylineMeasurementResult
    {
        public Polyline SourcePolyline { get; }
        public List<SegmentMeasurement> Segments { get; }
        public List<VertexAngleMeasurement> VertexAngles { get; }
        public double DisplayTotalLength { get; }
        public string LinearUnitAbbrev { get; }

        public PolylineMeasurementResult(
            Polyline sourcePolyline,
            List<SegmentMeasurement> segments,
            double displayTotalLength,
            string linearUnitAbbrev,
            List<VertexAngleMeasurement> vertexAngles = null)
        {
            SourcePolyline     = sourcePolyline;
            Segments           = segments;
            DisplayTotalLength = displayTotalLength;
            LinearUnitAbbrev   = linearUnitAbbrev;
            VertexAngles       = vertexAngles ?? new List<VertexAngleMeasurement>();
        }
    }
}

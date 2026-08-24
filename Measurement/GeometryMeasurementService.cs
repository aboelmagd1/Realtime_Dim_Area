using System;
using System.Collections.Generic;
using ArcGIS.Core.Geometry;
using DimensionOverlay.Models;
using DimensionOverlay.Utilities;

namespace DimensionOverlay.Measurement
{
    /// <summary>
    /// Pure static geometry measurement service.
    ///
    /// All methods are MCT-safe (called from QueuedTask.Run in DimensionEngine).
    /// No UI dependencies. No state. Thread-safe.
    ///
    /// Coordinate System Rules:
    ///   - Measurements always use the sketch geometry's own SpatialReference.
    ///   - Geographic CRS (IsGeographic == true) → geodesic measurement.
    ///   - Projected CRS → planar measurement using the CRS linear unit.
    ///   - "Automatic" method follows the above rules automatically.
    ///   - The map's spatial reference is NEVER used here.
    /// </summary>
    public static class GeometryMeasurementService
    {
        // ── Public API ────────────────────────────────────────────────────────────

        public static bool ResolveGeodesic(MeasurementMethod method, SpatialReference sr)
        {
            return method switch
            {
                MeasurementMethod.Geodesic  => true,
                MeasurementMethod.Planar    => false,
                _                          => sr != null && sr.IsGeographic  // Automatic
            };
        }

        /// <summary>
        /// Measures all segments, area, and perimeter of a polygon sketch.
        /// Handles multipart polygons and concave shapes correctly.
        /// Returns null for empty/null input.
        /// </summary>
        public static PolygonMeasurementResult MeasurePolygon(Polygon polygon, DimensionSettings settings)
        {
            if (polygon == null || polygon.IsEmpty) return null;

            var sr       = polygon.SpatialReference;
            bool geodesic = ResolveGeodesic(settings.Method, sr);

            string nativeLinAbbrev  = GetNativeLinearAbbrev(sr, geodesic);
            string nativeAreaAbbrev = nativeLinAbbrev + "\u00B2";

            // ── Area ─────────────────────────────────────────────────────────────
            double nativeArea = geodesic
                ? GeometryEngine.Instance.GeodesicArea(polygon)
                : Math.Abs(GeometryEngine.Instance.Area(polygon));

            var (displayArea, areaAbbrev) =
                UnitConverter.ConvertArea(nativeArea, nativeAreaAbbrev, settings.DisplayUnit);

            // ── Perimeter ─────────────────────────────────────────────────────────
            double nativePerim = geodesic
                ? GeometryEngine.Instance.GeodesicLength(polygon)
                : GeometryEngine.Instance.Length(polygon);

            var (displayPerim, linearAbbrev) =
                UnitConverter.ConvertLength(nativePerim, nativeLinAbbrev, settings.DisplayUnit);

            // ── Interior label point ──────────────────────────────────────────────
            MapPoint interior = TryGetLabelPoint(polygon);

            // ── Segments (all parts) ─────────────────────────────────────────────
            var segments = BuildSegments(polygon.Parts, geodesic, nativeLinAbbrev, settings);

            // ── Assemble result ───────────────────────────────────────────────────
            var result = new PolygonMeasurementResult(
                polygon,
                segments,
                displayArea,  areaAbbrev,
                displayPerim, linearAbbrev,
                interior);

            return result;
        }

        /// <summary>
        /// Measures all segments and total length of a polyline sketch.
        /// Returns null for empty/null input.
        /// </summary>
        public static PolylineMeasurementResult MeasurePolyline(Polyline polyline, DimensionSettings settings)
        {
            if (polyline == null || polyline.IsEmpty) return null;

            var sr        = polyline.SpatialReference;
            bool geodesic = ResolveGeodesic(settings.Method, sr);
            string nativeLinAbbrev = GetNativeLinearAbbrev(sr, geodesic);

            double nativeTotal = geodesic
                ? GeometryEngine.Instance.GeodesicLength(polyline)
                : GeometryEngine.Instance.Length(polyline);

            var (displayTotal, linearAbbrev) =
                UnitConverter.ConvertLength(nativeTotal, nativeLinAbbrev, settings.DisplayUnit);

            var segments = BuildSegments(polyline.Parts, geodesic, nativeLinAbbrev, settings);

            return new PolylineMeasurementResult(polyline, segments, displayTotal, linearAbbrev);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static List<SegmentMeasurement> BuildSegments(
            ReadOnlyPartCollection parts,
            bool geodesic,
            string nativeLinAbbrev,
            DimensionSettings settings)
        {
            var segments = new List<SegmentMeasurement>();

            foreach (var part in parts)
            {
                // Collect all vertex points for this part
                var pts = new List<MapPoint>(part.Count + 1);
                foreach (var seg in part)
                    pts.Add(seg.StartPoint);
                if (part.Count > 0)
                    pts.Add(part[part.Count - 1].EndPoint);

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];

                    double nativeLen = MeasureDistance(a, b, geodesic);
                    var (displayLen, abbrev) = UnitConverter.ConvertLength(
                        nativeLen, nativeLinAbbrev, settings.DisplayUnit);

                    double? bearing = settings.ShowBearings ? ComputeBearing(a, b) : (double?)null;

                    segments.Add(new SegmentMeasurement(a, b, nativeLen, displayLen, abbrev, bearing));
                }
            }

            return segments;
        }

        private static double MeasureDistance(MapPoint a, MapPoint b, bool geodesic)
        {
            if (!geodesic)
                return GeometryEngine.Instance.Distance(a, b);

            // Geodesic: build a 2-point polyline and call GeodesicLength
            var tmp = PolylineBuilderEx.CreatePolyline(new[] { a, b }, a.SpatialReference);
            return GeometryEngine.Instance.GeodesicLength(tmp);
        }

        private static double ComputeBearing(MapPoint a, MapPoint b)
        {
            double deg = Math.Atan2(b.X - a.X, b.Y - a.Y) * (180.0 / Math.PI);
            return deg < 0 ? deg + 360.0 : deg;
        }

        private static MapPoint TryGetLabelPoint(Polygon polygon)
        {
            try
            {
                var lp = GeometryEngine.Instance.LabelPoint(polygon);
                if (lp != null) return lp;
            }
            catch { /* fall through */ }

            return GeometryEngine.Instance.Centroid(polygon);
        }

        private static string GetNativeLinearAbbrev(SpatialReference sr, bool geodesic)
        {
            if (geodesic || sr == null) return "m"; // GeodesicLength always returns meters

            var unitName = sr.Unit?.Name?.ToLowerInvariant() ?? "meter";
            if (unitName.Contains("foot") || unitName.Contains("feet")) return "ft";
            if (unitName.Contains("meter") || unitName.Contains("metre")) return "m";
            return "m";
        }
    }
}

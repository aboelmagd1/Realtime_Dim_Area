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

            // ── Vertex Angles ─────────────────────────────────────────────────────
            var angles = BuildVertexAngles(polygon.Parts, isPolygon: true);

            // ── Assemble result ───────────────────────────────────────────────────
            var result = new PolygonMeasurementResult(
                polygon,
                segments,
                displayArea,  areaAbbrev,
                displayPerim, linearAbbrev,
                interior,
                angles);

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
            var angles = BuildVertexAngles(polyline.Parts, isPolygon: false);

            return new PolylineMeasurementResult(polyline, segments, displayTotal, linearAbbrev, angles);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static List<VertexAngleMeasurement> BuildVertexAngles(ReadOnlyPartCollection parts, bool isPolygon)
        {
            var angles = new List<VertexAngleMeasurement>();

            foreach (var part in parts)
            {
                var pts = new List<MapPoint>(part.Count + 1);
                foreach (var seg in part)
                    pts.Add(seg.StartPoint);
                if (part.Count > 0)
                    pts.Add(part[part.Count - 1].EndPoint);

                if (pts.Count < 3) continue;

                if (isPolygon)
                {
                    int k = pts.Count;
                    // If polygon ring is closed (first pt == last pt), remove redundant last pt
                    if (Math.Abs(pts[0].X - pts[k - 1].X) < 1e-7 && Math.Abs(pts[0].Y - pts[k - 1].Y) < 1e-7)
                    {
                        k--;
                    }

                    if (k < 3) continue;

                    for (int i = 0; i < k; i++)
                    {
                        var a = pts[(i - 1 + k) % k];
                        var v = pts[i];
                        var b = pts[(i + 1) % k];

                        double? angle = CalculateAngle(a, v, b);
                        if (angle.HasValue)
                        {
                            angles.Add(new VertexAngleMeasurement(v, angle.Value, a, b));
                        }
                    }
                }
                else
                {
                    // Polyline: interior vertices only
                    for (int i = 1; i < pts.Count - 1; i++)
                    {
                        var a = pts[i - 1];
                        var v = pts[i];
                        var b = pts[i + 1];

                        double? angle = CalculateAngle(a, v, b);
                        if (angle.HasValue)
                        {
                            angles.Add(new VertexAngleMeasurement(v, angle.Value, a, b));
                        }
                    }
                }
            }

            return angles;
        }

        private static double? CalculateAngle(MapPoint a, MapPoint v, MapPoint b)
        {
            if (a == null || v == null || b == null) return null;

            double dx1 = a.X - v.X, dy1 = a.Y - v.Y;
            double dx2 = b.X - v.X, dy2 = b.Y - v.Y;

            double len1 = Math.Sqrt((dx1 * dx1) + (dy1 * dy1));
            double len2 = Math.Sqrt((dx2 * dx2) + (dy2 * dy2));

            if (len1 < 1e-7 || len2 < 1e-7) return null;

            double dot = (dx1 * dx2) + (dy1 * dy2);
            double cosVal = Math.Clamp(dot / (len1 * len2), -1.0, 1.0);
            double deg = Math.Acos(cosVal) * (180.0 / Math.PI);

            // Exclude straight-line angles close to 180 degrees (within 1.0 degree tolerance)
            if (Math.Abs(deg - 180.0) < 1.0 || deg < 0.5)
            {
                return null;
            }

            return deg;
        }

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

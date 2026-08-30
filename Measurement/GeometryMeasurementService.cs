using System;
using System.Collections.Generic;
using ArcGIS.Core.Geometry;
using GeoMetrics.Models;
using GeoMetrics.Utilities;

namespace GeoMetrics.Measurement
{
    /// <summary>
    /// Pure static geometry measurement service.
    ///
    /// All methods are MCT-safe (called from QueuedTask.Run in DimensionEngine).
    /// No UI dependencies. No state. Thread-safe.
    ///
    /// Coordinate System Rules:
    ///   - Measurements always use the sketch geometry's own SpatialReference.
    ///   - Geographic CRS (IsGeographic == true) → geodesic measurement in meters.
    ///   - Projected CRS → planar measurement using the CRS linear unit.
    ///   - "Automatic" method follows the above rules automatically.
    ///   - Segments and endpoints preserve the geometry's SpatialReference so overlays align perfectly.
    /// </summary>
    public static class GeometryMeasurementService
    {
        // ── Public API ────────────────────────────────────────────────────────────

        public static bool ResolveGeodesic(MeasurementMethod method, SpatialReference sr, SpatialReference mapSr = null)
        {
            if (method == MeasurementMethod.Geodesic) return true;
            if (method == MeasurementMethod.Planar)
            {
                if (sr != null && sr.IsGeographic)
                {
                    // If map is projected, Planar mode can measure in map's projected CRS
                    return mapSr == null || mapSr.IsGeographic;
                }
                return false;
            }
            // Automatic mode: GCS uses Geodesic, Projected uses Planar
            return sr != null && sr.IsGeographic;
        }

        /// <summary>
        /// Measures all segments, area, and perimeter of a polygon sketch.
        /// Handles multipart polygons, concave shapes, and geographic CRS correctly.
        /// Follows ArcGIS Pro Measure tool calculation standards (Geodesic on ellipsoid / Planar on projected CRS).
        /// Returns null for empty/null input.
        /// </summary>
        public static PolygonMeasurementResult MeasurePolygon(Polygon polygon, DimensionSettings settings, SpatialReference mapSr = null)
        {
            if (polygon == null || polygon.IsEmpty) return null;

            var sr = polygon.SpatialReference ?? mapSr;
            bool isGcs = sr != null && sr.IsGeographic;

            Polygon polyToMeasure = polygon;
            SpatialReference measureSr = sr;

            bool geodesic;
            if (settings.Method == MeasurementMethod.Geodesic)
            {
                geodesic = true;
            }
            else if (settings.Method == MeasurementMethod.Planar)
            {
                if (isGcs && mapSr != null && !mapSr.IsGeographic)
                {
                    // Planar on GCS with projected map: project to map's projected CRS (matches ArcGIS Pro Measure tool)
                    try
                    {
                        polyToMeasure = GeometryEngine.Instance.Project(polygon, mapSr) as Polygon ?? polygon;
                        measureSr = polyToMeasure.SpatialReference ?? mapSr;
                        geodesic = false;
                    }
                    catch
                    {
                        geodesic = true;
                    }
                }
                else if (isGcs)
                {
                    geodesic = true;
                }
                else
                {
                    geodesic = false;
                }
            }
            else
            {
                geodesic = isGcs;
            }

            string nativeLinAbbrev  = GetNativeLinearAbbrev(measureSr, geodesic);
            string nativeAreaAbbrev = nativeLinAbbrev + "\u00B2";

            // ── Area ─────────────────────────────────────────────────────────────
            double nativeArea;
            try
            {
                nativeArea = geodesic
                    ? GeometryEngine.Instance.GeodesicArea(polyToMeasure)
                    : Math.Abs(GeometryEngine.Instance.Area(polyToMeasure));
            }
            catch
            {
                nativeArea = Math.Abs(GeometryEngine.Instance.Area(polyToMeasure));
            }

            var (displayArea, areaAbbrev) =
                UnitConverter.ConvertArea(nativeArea, nativeAreaAreaAbbrev(nativeAreaAbbrev), settings.DisplayUnit);

            // ── Perimeter ─────────────────────────────────────────────────────────
            double nativePerim;
            try
            {
                nativePerim = geodesic
                    ? GeometryEngine.Instance.GeodesicLength(polyToMeasure)
                    : GeometryEngine.Instance.Length(polyToMeasure);
            }
            catch
            {
                nativePerim = GeometryEngine.Instance.Length(polyToMeasure);
            }

            var (displayPerim, linearAbbrev) =
                UnitConverter.ConvertLength(nativePerim, nativeLinAbbrev, settings.DisplayUnit);

            // ── Interior label point ──────────────────────────────────────────────
            MapPoint interior = TryGetLabelPoint(polygon);
            if (interior != null && interior.SpatialReference == null && sr != null)
            {
                interior = MapPointBuilderEx.CreateMapPoint(interior.X, interior.Y, sr);
            }

            // ── Segments (all parts) ─────────────────────────────────────────────
            var segments = BuildSegments(polygon.Parts, sr, geodesic, nativeLinAbbrev, settings, mapSr);

            // ── Vertex Angles ─────────────────────────────────────────────────────
            var angles = BuildVertexAngles(polygon.Parts, sr, isPolygon: true);

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
        /// Follows ArcGIS Pro Measure tool calculation standards.
        /// Returns null for empty/null input.
        /// </summary>
        public static PolylineMeasurementResult MeasurePolyline(Polyline polyline, DimensionSettings settings, SpatialReference mapSr = null)
        {
            if (polyline == null || polyline.IsEmpty) return null;

            var sr        = polyline.SpatialReference ?? mapSr;
            bool isGcs    = sr != null && sr.IsGeographic;

            Polyline lineToMeasure = polyline;
            SpatialReference measureSr = sr;

            bool geodesic;
            if (settings.Method == MeasurementMethod.Geodesic)
            {
                geodesic = true;
            }
            else if (settings.Method == MeasurementMethod.Planar)
            {
                if (isGcs && mapSr != null && !mapSr.IsGeographic)
                {
                    try
                    {
                        lineToMeasure = GeometryEngine.Instance.Project(polyline, mapSr) as Polyline ?? polyline;
                        measureSr = lineToMeasure.SpatialReference ?? mapSr;
                        geodesic = false;
                    }
                    catch
                    {
                        geodesic = true;
                    }
                }
                else if (isGcs)
                {
                    geodesic = true;
                }
                else
                {
                    geodesic = false;
                }
            }
            else
            {
                geodesic = isGcs;
            }

            string nativeLinAbbrev = GetNativeLinearAbbrev(measureSr, geodesic);

            double nativeTotal;
            try
            {
                nativeTotal = geodesic
                    ? GeometryEngine.Instance.GeodesicLength(lineToMeasure)
                    : GeometryEngine.Instance.Length(lineToMeasure);
            }
            catch
            {
                nativeTotal = GeometryEngine.Instance.Length(lineToMeasure);
            }

            var (displayTotal, linearAbbrev) =
                UnitConverter.ConvertLength(nativeTotal, nativeLinAbbrev, settings.DisplayUnit);

            var segments = BuildSegments(polyline.Parts, sr, geodesic, nativeLinAbbrev, settings, mapSr);
            var angles = BuildVertexAngles(polyline.Parts, sr, isPolygon: false);

            return new PolylineMeasurementResult(polyline, segments, displayTotal, linearAbbrev, angles);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static string nativeAreaAreaAbbrev(string defaultAbbrev) => defaultAbbrev ?? "m\u00B2";

        private static List<VertexAngleMeasurement> BuildVertexAngles(ReadOnlyPartCollection parts, SpatialReference sr, bool isPolygon)
        {
            var angles = new List<VertexAngleMeasurement>();
            if (parts == null) return angles;

            foreach (var part in parts)
            {
                var pts = new List<MapPoint>(part.Count + 1);
                foreach (var seg in part)
                {
                    var sp = seg.StartPoint;
                    if (sp.SpatialReference == null && sr != null)
                        sp = MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sr);
                    pts.Add(sp);
                }
                if (part.Count > 0)
                {
                    var ep = part[part.Count - 1].EndPoint;
                    if (ep.SpatialReference == null && sr != null)
                        ep = MapPointBuilderEx.CreateMapPoint(ep.X, ep.Y, sr);
                    pts.Add(ep);
                }

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

                        double? angle = CalculateAngle(a, v, b, sr);
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

                        double? angle = CalculateAngle(a, v, b, sr);
                        if (angle.HasValue)
                        {
                            angles.Add(new VertexAngleMeasurement(v, angle.Value, a, b));
                        }
                    }
                }
            }

            return angles;
        }

        private static double? CalculateAngle(MapPoint a, MapPoint v, MapPoint b, SpatialReference sr)
        {
            if (a == null || v == null || b == null) return null;

            double dx1 = a.X - v.X, dy1 = a.Y - v.Y;
            double dx2 = b.X - v.X, dy2 = b.Y - v.Y;

            // In geographic CRS, scale dx by cos(latitude) so vertex angle reflects true ground shape
            var ptSr = a.SpatialReference ?? v.SpatialReference ?? sr;
            if (ptSr != null && ptSr.IsGeographic)
            {
                double latRad = (v.Y * Math.PI) / 180.0;
                double cosLat = Math.Max(0.01, Math.Cos(latRad));
                dx1 *= cosLat;
                dx2 *= cosLat;
            }

            double len1 = Math.Sqrt((dx1 * dx1) + (dy1 * dy1));
            double len2 = Math.Sqrt((dx2 * dx2) + (dy2 * dy2));

            if (len1 < 1e-12 || len2 < 1e-12) return null;

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
            SpatialReference sr,
            bool geodesic,
            string nativeLinAbbrev,
            DimensionSettings settings,
            SpatialReference mapSr = null)
        {
            var segments = new List<SegmentMeasurement>();
            if (parts == null) return segments;

            foreach (var part in parts)
            {
                // Collect all vertex points for this part and ensure SpatialReference is preserved
                var pts = new List<MapPoint>(part.Count + 1);
                foreach (var seg in part)
                {
                    var sp = seg.StartPoint;
                    if (sp.SpatialReference == null && sr != null)
                        sp = MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sr);
                    pts.Add(sp);
                }
                if (part.Count > 0)
                {
                    var ep = part[part.Count - 1].EndPoint;
                    if (ep.SpatialReference == null && sr != null)
                        ep = MapPointBuilderEx.CreateMapPoint(ep.X, ep.Y, sr);
                    pts.Add(ep);
                }

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];

                    double nativeLen = MeasureDistance(a, b, geodesic, sr, mapSr);
                    var (displayLen, abbrev) = UnitConverter.ConvertLength(
                        nativeLen, nativeLinAbbrev, settings.DisplayUnit);

                    double? bearing = settings.ShowBearings ? ComputeBearing(a, b, sr) : (double?)null;

                    segments.Add(new SegmentMeasurement(a, b, nativeLen, displayLen, abbrev, bearing));
                }
            }

            return segments;
        }

        public static double MeasureDistance(
            MapPoint a, MapPoint b, bool geodesic, SpatialReference fallbackSr = null, SpatialReference mapSr = null)
        {
            if (a == null || b == null) return 0.0;

            var sr = a.SpatialReference ?? b.SpatialReference ?? fallbackSr;
            bool isGeo = sr != null && sr.IsGeographic;

            if (geodesic || (isGeo && (mapSr == null || mapSr.IsGeographic)))
            {
                // Geodesic measurement on the ellipsoid (in meters)
                if (sr != null)
                {
                    try
                    {
                        var tmp = PolylineBuilderEx.CreatePolyline(new[] { a, b }, sr);
                        double len = GeometryEngine.Instance.GeodesicLength(tmp);
                        if (!double.IsNaN(len) && len >= 0) return len;
                    }
                    catch
                    {
                        // Fallback to GreatCircle
                    }
                }

                // Accurate spherical Haversine / WGS84 geodesic fallback
                return GreatCircleDistance(a.X, a.Y, b.X, b.Y);
            }

            // Planar measurement
            if (isGeo && mapSr != null && !mapSr.IsGeographic)
            {
                try
                {
                    var ptA = GeometryEngine.Instance.Project(a, mapSr) as MapPoint ?? a;
                    var ptB = GeometryEngine.Instance.Project(b, mapSr) as MapPoint ?? b;
                    return GeometryEngine.Instance.Distance(ptA, ptB);
                }
                catch { }
            }

            try
            {
                return GeometryEngine.Instance.Distance(a, b);
            }
            catch
            {
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>
        /// Computes spherical great-circle distance in meters between two (Longitude, Latitude) points in degrees.
        /// Uses WGS84 semi-major axis (6,378,137.0 m).
        /// </summary>
        public static double GreatCircleDistance(double lon1, double lat1, double lon2, double lat2)
        {
            const double R = 6378137.0; // Earth radius in meters
            double dLat = (lat2 - lat1) * (Math.PI / 180.0);
            double dLon = (lon2 - lon1) * (Math.PI / 180.0);
            double rLat1 = lat1 * (Math.PI / 180.0);
            double rLat2 = lat2 * (Math.PI / 180.0);

            double sinDLat = Math.Sin(dLat / 2.0);
            double sinDLon = Math.Sin(dLon / 2.0);
            double h = (sinDLat * sinDLat) + (Math.Cos(rLat1) * Math.Cos(rLat2) * sinDLon * sinDLon);
            double c = 2.0 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(Math.Max(0.0, 1.0 - h)));
            return R * c;
        }

        private static double ComputeBearing(MapPoint a, MapPoint b, SpatialReference fallbackSr = null)
        {
            var sr = a.SpatialReference ?? b.SpatialReference ?? fallbackSr;
            if (sr != null && sr.IsGeographic)
            {
                // Great-circle initial forward azimuth in degrees (0-360)
                double lat1 = a.Y * (Math.PI / 180.0);
                double lat2 = b.Y * (Math.PI / 180.0);
                double dLon = (b.X - a.X) * (Math.PI / 180.0);

                double y = Math.Sin(dLon) * Math.Cos(lat2);
                double x = (Math.Cos(lat1) * Math.Sin(lat2)) - (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));
                double brng = Math.Atan2(y, x) * (180.0 / Math.PI);
                return (brng + 360.0) % 360.0;
            }
            else
            {
                double deg = Math.Atan2(b.X - a.X, b.Y - a.Y) * (180.0 / Math.PI);
                return deg < 0 ? deg + 360.0 : deg;
            }
        }

        private static MapPoint TryGetLabelPoint(Polygon polygon)
        {
            try
            {
                var lp = GeometryEngine.Instance.LabelPoint(polygon);
                if (lp != null && !lp.IsEmpty) return lp;
            }
            catch { /* fall through */ }

            try
            {
                var c = GeometryEngine.Instance.Centroid(polygon);
                if (c != null && !c.IsEmpty) return c;
            }
            catch { /* fall through */ }

            return polygon.Extent?.Center;
        }

        private static string GetNativeLinearAbbrev(SpatialReference sr, bool geodesic)
        {
            if (geodesic || sr == null || sr.IsGeographic) return "m"; // GeodesicLength always returns meters

            var unitName = sr.Unit?.Name?.ToLowerInvariant() ?? "meter";
            if (unitName.Contains("foot") || unitName.Contains("feet")) return "ft";
            if (unitName.Contains("meter") || unitName.Contains("metre")) return "m";
            return "m";
        }
    }
}


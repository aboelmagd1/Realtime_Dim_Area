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
            var segments = BuildSegments(polygon.Parts, sr, geodesic, nativeLinAbbrev, settings, mapSr, isPolygon: true);

            // ── Vertex Angles ─────────────────────────────────────────────────────
            var angles = BuildVertexAngles(polygon.Parts, sr, isPolygon: true, settings);

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

            var segments = BuildSegments(polyline.Parts, sr, geodesic, nativeLinAbbrev, settings, mapSr, isPolygon: false);
            var angles = BuildVertexAngles(polyline.Parts, sr, isPolygon: false, settings);

            return new PolylineMeasurementResult(polyline, segments, displayTotal, linearAbbrev, angles);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static string nativeAreaAreaAbbrev(string defaultAbbrev) => defaultAbbrev ?? "m\u00B2";

        private static List<VertexAngleMeasurement> BuildVertexAngles(ReadOnlyPartCollection parts, SpatialReference sr, bool isPolygon, DimensionSettings settings = null)
        {
            var angles = new List<VertexAngleMeasurement>();
            if (parts == null) return angles;

            foreach (var part in parts)
            {
                if (part == null || part.Count == 0) continue;

                int n = part.Count;

                if (isPolygon)
                {
                    if (n < 3) continue;

                    for (int i = 0; i < n; i++)
                    {
                        var segPrev = part[(i - 1 + n) % n];
                        var segNext = part[i];
                        if (segPrev == null || segNext == null) continue;

                        var v = segNext.StartPoint ?? segPrev.EndPoint;
                        if (v == null) continue;
                        if (v.SpatialReference == null && sr != null)
                            v = MapPointBuilderEx.CreateMapPoint(v.X, v.Y, sr);

                        var (dx1, dy1, prevPt) = GetIncomingTangentVector(segPrev, v, sr);
                        var (dx2, dy2, nextPt) = GetOutgoingTangentVector(segNext, v, sr);

                        double? angle = CalculateAngleFromVectors(dx1, dy1, dx2, dy2, v, sr, settings);
                        if (angle.HasValue)
                        {
                            angles.Add(new VertexAngleMeasurement(v, angle.Value, prevPt, nextPt));
                        }
                    }
                }
                else
                {
                    // Polyline: interior vertices only between consecutive segments
                    for (int i = 1; i < n; i++)
                    {
                        var segPrev = part[i - 1];
                        var segNext = part[i];
                        if (segPrev == null || segNext == null) continue;

                        var v = segNext.StartPoint ?? segPrev.EndPoint;
                        if (v == null) continue;
                        if (v.SpatialReference == null && sr != null)
                            v = MapPointBuilderEx.CreateMapPoint(v.X, v.Y, sr);

                        var (dx1, dy1, prevPt) = GetIncomingTangentVector(segPrev, v, sr);
                        var (dx2, dy2, nextPt) = GetOutgoingTangentVector(segNext, v, sr);

                        double? angle = CalculateAngleFromVectors(dx1, dy1, dx2, dy2, v, sr, settings);
                        if (angle.HasValue)
                        {
                            angles.Add(new VertexAngleMeasurement(v, angle.Value, prevPt, nextPt));
                        }
                    }
                }
            }

            return angles;
        }

        private static (double dx, double dy, MapPoint refPt) GetIncomingTangentVector(Segment seg, MapPoint v, SpatialReference sr)
        {
            var vSr = v.SpatialReference ?? sr;
            if (seg.IsCurve)
            {
                try
                {
                    // Query tangent at end of incoming segment (t = 1.0)
                    var tangent = GeometryEngine.Instance.QueryTangent(seg, SegmentExtensionType.NoExtension, 1.0, AsRatioOrLength.AsRatio, 10.0);
                    if (tangent != null)
                    {
                        // Vector pointing backwards from v into the curve
                        double dx = tangent.StartPoint.X - tangent.EndPoint.X;
                        double dy = tangent.StartPoint.Y - tangent.EndPoint.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 1e-9)
                        {
                            var pt = MapPointBuilderEx.CreateMapPoint(v.X + (dx / len), v.Y + (dy / len), vSr);
                            return (dx, dy, pt);
                        }
                    }
                }
                catch { }
            }

            // Fallback to straight vector from v to seg.StartPoint
            var sp = seg.StartPoint;
            double fdx = (sp != null ? sp.X : v.X) - v.X;
            double fdy = (sp != null ? sp.Y : v.Y) - v.Y;
            var refP = sp != null ? (sp.SpatialReference == null && vSr != null ? MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, vSr) : sp) : v;
            return (fdx, fdy, refP);
        }

        private static (double dx, double dy, MapPoint refPt) GetOutgoingTangentVector(Segment seg, MapPoint v, SpatialReference sr)
        {
            var vSr = v.SpatialReference ?? sr;
            if (seg.IsCurve)
            {
                try
                {
                    // Query tangent at start of outgoing segment (t = 0.0)
                    var tangent = GeometryEngine.Instance.QueryTangent(seg, SegmentExtensionType.NoExtension, 0.0, AsRatioOrLength.AsRatio, 10.0);
                    if (tangent != null)
                    {
                        // Vector pointing forwards from v along the curve
                        double dx = tangent.EndPoint.X - tangent.StartPoint.X;
                        double dy = tangent.EndPoint.Y - tangent.StartPoint.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 1e-9)
                        {
                            var pt = MapPointBuilderEx.CreateMapPoint(v.X + (dx / len), v.Y + (dy / len), vSr);
                            return (dx, dy, pt);
                        }
                    }
                }
                catch { }
            }

            // Fallback to straight vector from v to seg.EndPoint
            var ep = seg.EndPoint;
            double fdx = (ep != null ? ep.X : v.X) - v.X;
            double fdy = (ep != null ? ep.Y : v.Y) - v.Y;
            var refP = ep != null ? (ep.SpatialReference == null && vSr != null ? MapPointBuilderEx.CreateMapPoint(ep.X, ep.Y, vSr) : ep) : v;
            return (fdx, fdy, refP);
        }

        private static double? ComputeVertexAngleDegrees(
            double dx1, double dy1, double dx2, double dy2, MapPoint v, SpatialReference sr)
        {
            if (v == null) return null;

            // In geographic CRS, scale dx by cos(latitude) so vertex angle reflects true ground shape
            var ptSr = v.SpatialReference ?? sr;
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
            return Math.Acos(cosVal) * (180.0 / Math.PI);
        }

        private static double? CalculateAngleFromVectors(
            double dx1, double dy1, double dx2, double dy2, MapPoint v, SpatialReference sr, DimensionSettings settings = null)
        {
            var deg = ComputeVertexAngleDegrees(dx1, dy1, dx2, dy2, v, sr);
            if (!deg.HasValue) return null;

            double tolerance = (settings != null && settings.MergeCollinearSegments)
                ? Math.Max(1.0, settings.CollinearAngleTolerance)
                : 1.0;

            // Exclude straight-line angles and smooth tangent transitions close to 180 degrees
            if (Math.Abs(deg.Value - 180.0) <= tolerance || deg.Value < 0.5)
            {
                return null;
            }

            return deg.Value;
        }

        public static double? CalculateAngle(MapPoint a, MapPoint v, MapPoint b, SpatialReference sr, DimensionSettings settings = null)
        {
            if (a == null || v == null || b == null) return null;

            double dx1 = a.X - v.X, dy1 = a.Y - v.Y;
            double dx2 = b.X - v.X, dy2 = b.Y - v.Y;

            return CalculateAngleFromVectors(dx1, dy1, dx2, dy2, v, sr, settings);
        }

        private static List<SegmentMeasurement> BuildSegments(
            ReadOnlyPartCollection parts,
            SpatialReference sr,
            bool geodesic,
            string nativeLinAbbrev,
            DimensionSettings settings,
            SpatialReference mapSr = null,
            bool isPolygon = false)
        {
            var segments = new List<SegmentMeasurement>();
            if (parts == null) return segments;

            bool isGcs = sr != null && sr.IsGeographic;
            bool planarOnGcsWithProjectedMap = !geodesic && isGcs && mapSr != null && !mapSr.IsGeographic;

            foreach (var part in parts)
            {
                if (part == null) continue;

                var rawPartSegments = new List<SegmentMeasurement>();

                foreach (var seg in part)
                {
                    if (seg == null) continue;

                    var sp = seg.StartPoint;
                    if (sp.SpatialReference == null && sr != null)
                        sp = MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, sr);

                    var ep = seg.EndPoint;
                    if (ep.SpatialReference == null && sr != null)
                        ep = MapPointBuilderEx.CreateMapPoint(ep.X, ep.Y, sr);

                    bool isCurve = seg.IsCurve;

                    // ── 1. Calculate true segment length (planar or geodesic) ─────────
                    double nativeLen = 0.0;

                    if (geodesic)
                    {
                        try
                        {
                            var segPoly = PolylineBuilderEx.CreatePolyline(seg, sr);
                            nativeLen = GeometryEngine.Instance.GeodesicLength(segPoly);
                        }
                        catch
                        {
                            nativeLen = 0.0;
                        }

                        if (double.IsNaN(nativeLen) || nativeLen <= 0.0)
                        {
                            if (isCurve)
                            {
                                if (isGcs)
                                {
                                    // GCS units are angular degrees: convert arc length to meters along great circle
                                    nativeLen = seg.Length * 111319.49079327357;
                                }
                                else
                                {
                                    nativeLen = seg.Length;
                                }
                            }
                            else
                            {
                                nativeLen = MeasureDistance(sp, ep, true, sr, mapSr);
                            }
                        }
                    }
                    else if (planarOnGcsWithProjectedMap)
                    {
                        // Planar measurement for GCS feature projected into map's projected CRS
                        try
                        {
                            var segPoly = PolylineBuilderEx.CreatePolyline(seg, sr);
                            var projPoly = GeometryEngine.Instance.Project(segPoly, mapSr) as Polyline;
                            nativeLen = GeometryEngine.Instance.Length(projPoly ?? segPoly);
                        }
                        catch
                        {
                            nativeLen = seg.Length;
                        }

                        if (double.IsNaN(nativeLen) || nativeLen <= 0.0)
                        {
                            nativeLen = MeasureDistance(sp, ep, false, sr, mapSr);
                        }
                    }
                    else
                    {
                        // Planar measurement in native CRS: seg.Length returns true curve arc length!
                        nativeLen = seg.Length;
                        if (double.IsNaN(nativeLen) || nativeLen <= 0.0)
                        {
                            nativeLen = MeasureDistance(sp, ep, false, sr, mapSr);
                        }
                    }

                    var (displayLen, abbrev) = UnitConverter.ConvertLength(
                        nativeLen, nativeLinAbbrev, settings.DisplayUnit);

                    // ── 2. Midpoint & Tangent Angle for label placement ───────────────
                    MapPoint midPoint = null;
                    double? tangentAngle = null;

                    if (isCurve)
                    {
                        try
                        {
                            // Query midpoint at 50% along the curve
                            midPoint = GeometryEngine.Instance.QueryPoint(seg, SegmentExtensionType.NoExtension, 0.5, AsRatioOrLength.AsRatio);
                            if (midPoint != null && midPoint.SpatialReference == null && sr != null)
                            {
                                midPoint = MapPointBuilderEx.CreateMapPoint(midPoint.X, midPoint.Y, sr);
                            }

                            // Query tangent line segment at 50% along curve
                            var tangent = GeometryEngine.Instance.QueryTangent(seg, SegmentExtensionType.NoExtension, 0.5, AsRatioOrLength.AsRatio, 10.0);
                            if (tangent != null)
                            {
                                double tdx = tangent.EndPoint.X - tangent.StartPoint.X;
                                double tdy = tangent.EndPoint.Y - tangent.StartPoint.Y;
                                double deg = Math.Atan2(tdy, tdx) * (180.0 / Math.PI);
                                if (deg > 90.0) deg -= 180.0;
                                else if (deg < -90.0) deg += 180.0;
                                tangentAngle = deg;
                            }
                        }
                        catch
                        {
                            // Fallback handled below
                        }
                    }

                    if (midPoint == null)
                    {
                        midPoint = MapPointBuilderEx.CreateMapPoint((sp.X + ep.X) * 0.5, (sp.Y + ep.Y) * 0.5, sp.SpatialReference ?? sr);
                    }

                    if (!tangentAngle.HasValue)
                    {
                        double dx = ep.X - sp.X;
                        double dy = ep.Y - sp.Y;
                        if (isGcs)
                        {
                            double latRad = (((sp.Y + ep.Y) * 0.5) * Math.PI) / 180.0;
                            dx *= Math.Max(0.01, Math.Cos(latRad));
                        }
                        double deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                        if (deg > 90.0) deg -= 180.0;
                        else if (deg < -90.0) deg += 180.0;
                        tangentAngle = deg;
                    }

                    // ── 3. Central Angle (for circular / elliptic arcs) ───────────────
                    double? centralAngle = null;
                    if (seg is EllipticArcSegment arcSeg)
                    {
                        try
                        {
                            centralAngle = Math.Abs(arcSeg.CentralAngle) * (180.0 / Math.PI);
                        }
                        catch
                        {
                            centralAngle = null;
                        }
                    }

                    // ── 4. Bearing ───────────────────────────────────────────────────
                    double? bearing = settings.ShowBearings ? ComputeBearing(sp, ep, sr) : (double?)null;

                    rawPartSegments.Add(new SegmentMeasurement(
                        sp, ep,
                        nativeLen, displayLen, abbrev,
                        bearing,
                        isCurve,
                        midPoint,
                        tangentAngle,
                        centralAngle,
                        seg.SegmentType));
                }

                if (settings != null && settings.MergeCollinearSegments && rawPartSegments.Count > 1)
                {
                    var mergedPartSegments = MergeCollinearSegments(
                        rawPartSegments,
                        sr,
                        nativeLinAbbrev,
                        settings,
                        isGcs,
                        isPolygon);
                    segments.AddRange(mergedPartSegments);
                }
                else
                {
                    segments.AddRange(rawPartSegments);
                }
            }

            return segments;
        }

        private static List<SegmentMeasurement> MergeCollinearSegments(
            List<SegmentMeasurement> rawSegments,
            SpatialReference sr,
            string nativeLinAbbrev,
            DimensionSettings settings,
            bool isGcs,
            bool isPolygon)
        {
            if (rawSegments == null || rawSegments.Count <= 1)
                return rawSegments ?? new List<SegmentMeasurement>();

            double tolerance = settings?.CollinearAngleTolerance ?? 1.0;
            var merged = new List<SegmentMeasurement>();
            var currentGroup = new List<SegmentMeasurement> { rawSegments[0] };

            for (int i = 1; i < rawSegments.Count; i++)
            {
                var candidate = rawSegments[i];
                var prevInGroup = currentGroup[^1];

                bool canMerge = false;

                // Only straight line segments can be merged (not curves)
                if (!prevInGroup.IsCurve && !candidate.IsCurve &&
                    prevInGroup.End != null && candidate.Start != null)
                {
                    var v = prevInGroup.End;
                    var groupStart = currentGroup[0].Start;
                    var candEnd = candidate.End;

                    if (groupStart != null && candEnd != null)
                    {
                        // Vector from shared vertex v backwards to group start
                        double dx1 = groupStart.X - v.X;
                        double dy1 = groupStart.Y - v.Y;

                        // Vector from shared vertex v forwards to candidate end
                        double dx2 = candEnd.X - v.X;
                        double dy2 = candEnd.Y - v.Y;

                        double? angle = ComputeVertexAngleDegrees(dx1, dy1, dx2, dy2, v, sr);

                        // If group already has multiple items, also verify local angle at vertex v
                        bool localOk = true;
                        if (currentGroup.Count > 1)
                        {
                            double ldx1 = prevInGroup.Start.X - v.X;
                            double ldy1 = prevInGroup.Start.Y - v.Y;
                            double? localAngle = ComputeVertexAngleDegrees(ldx1, ldy1, dx2, dy2, v, sr);
                            localOk = localAngle.HasValue && Math.Abs(localAngle.Value - 180.0) <= tolerance;
                        }

                        if (angle.HasValue && Math.Abs(angle.Value - 180.0) <= tolerance && localOk)
                        {
                            canMerge = true;
                        }
                    }
                }

                if (canMerge)
                {
                    currentGroup.Add(candidate);
                }
                else
                {
                    merged.Add(CombineSegmentGroup(currentGroup, sr, nativeLinAbbrev, settings, isGcs));
                    currentGroup = new List<SegmentMeasurement> { candidate };
                }
            }

            if (currentGroup.Count > 0)
            {
                merged.Add(CombineSegmentGroup(currentGroup, sr, nativeLinAbbrev, settings, isGcs));
            }

            // For closed polygons: check if the last merged segment and first merged segment meet at ~180° across closure
            if (isPolygon && merged.Count >= 3)
            {
                var first = merged[0];
                var last = merged[^1];

                if (!first.IsCurve && !last.IsCurve &&
                    first.Start != null && last.End != null && first.End != null && last.Start != null)
                {
                    double gap = Math.Sqrt(Math.Pow(last.End.X - first.Start.X, 2) + Math.Pow(last.End.Y - first.Start.Y, 2));
                    if (gap < 1e-4)
                    {
                        var v = first.Start;
                        double dx1 = last.Start.X - v.X;
                        double dy1 = last.Start.Y - v.Y;
                        double dx2 = first.End.X - v.X;
                        double dy2 = first.End.Y - v.Y;

                        double? closureAngle = ComputeVertexAngleDegrees(dx1, dy1, dx2, dy2, v, sr);
                        if (closureAngle.HasValue && Math.Abs(closureAngle.Value - 180.0) <= tolerance)
                        {
                            var wrapGroup = new List<SegmentMeasurement> { last, first };
                            var wrapped = CombineSegmentGroup(wrapGroup, sr, nativeLinAbbrev, settings, isGcs, last.Start, first.End);
                            merged[0] = wrapped;
                            merged.RemoveAt(merged.Count - 1);
                        }
                    }
                }
            }

            return merged;
        }

        private static SegmentMeasurement CombineSegmentGroup(
            List<SegmentMeasurement> group,
            SpatialReference sr,
            string nativeLinAbbrev,
            DimensionSettings settings,
            bool isGcs,
            MapPoint explicitStart = null,
            MapPoint explicitEnd = null)
        {
            if (group == null || group.Count == 0) return null;
            if (group.Count == 1 && explicitStart == null && explicitEnd == null) return group[0];

            var start = explicitStart ?? group[0].Start;
            var end = explicitEnd ?? group[^1].End;

            double totalNativeLen = 0.0;
            foreach (var s in group)
            {
                totalNativeLen += s.NativeLength;
            }

            var (displayLen, abbrev) = UnitConverter.ConvertLength(
                totalNativeLen, nativeLinAbbrev, settings.DisplayUnit);

            MapPoint midPoint = null;
            if (start != null && end != null)
            {
                midPoint = MapPointBuilderEx.CreateMapPoint((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5, start.SpatialReference ?? sr);
            }

            double? tangentAngle = null;
            if (start != null && end != null)
            {
                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                if (isGcs)
                {
                    double latRad = (((start.Y + end.Y) * 0.5) * Math.PI) / 180.0;
                    dx *= Math.Max(0.01, Math.Cos(latRad));
                }
                double deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                if (deg > 90.0) deg -= 180.0;
                else if (deg < -90.0) deg += 180.0;
                tangentAngle = deg;
            }

            double? bearing = (settings.ShowBearings && start != null && end != null)
                ? ComputeBearing(start, end, sr)
                : (double?)null;

            return new SegmentMeasurement(
                start,
                end,
                totalNativeLen,
                displayLen,
                abbrev,
                bearing,
                isCurve: false,
                midPoint: midPoint,
                tangentAngle: tangentAngle,
                centralAngle: null,
                segmentType: SegmentType.Line);
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


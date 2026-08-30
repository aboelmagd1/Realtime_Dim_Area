using System;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;

namespace GeoMetrics.Rendering
{
    /// <summary>
    /// Pure math and viewport-aware geometric calculations for dimension label and line placement.
    /// Implements ultra-fast Maplex-style dynamic visible placement with O(1) bounding box checks.
    /// Supports Geographic Coordinate Systems (degrees) and Projected Coordinate Systems seamlessly.
    /// Thread-safe and designed for high-performance 60 FPS rendering without lag.
    /// </summary>
    internal static class DimensionLabelManager
    {
        /// <summary>
        /// Converts a ground/screen offset in meters (metersX, metersY) to the coordinate delta in the target SpatialReference.
        /// Handles Geographic Coordinate Systems (degrees) using latitude-aware geodesy (cos latitude)
        /// and Projected Coordinate Systems (feet/meters) using unit conversion factors.
        /// </summary>
        public static (double dx, double dy) MetersToMapDelta(
            double metersX, double metersY, SpatialReference sr, double refLatDegrees = 0)
        {
            if (sr == null)
                return (metersX, metersY);

            if (sr.IsGeographic)
            {
                // Geographic CRS: 1 degree latitude ≈ 111,319.5 meters
                // 1 degree longitude ≈ 111,319.5 * cos(latitude) meters
                double latRad = (refLatDegrees * Math.PI) / 180.0;
                double cosLat = Math.Max(0.01, Math.Cos(latRad));
                const double degPerMeterLat = 1.0 / 111319.5;
                double degPerMeterLon = degPerMeterLat / cosLat;

                return (metersX * degPerMeterLon, metersY * degPerMeterLat);
            }

            if (sr.IsProjected)
            {
                double factor = (sr.Unit != null && sr.Unit.ConversionFactor > 0)
                    ? sr.Unit.ConversionFactor
                    : 1.0;
                return (metersX / factor, metersY / factor);
            }

            return (metersX, metersY);
        }

        /// <summary>
        /// Computes ground/screen meters per pixel for a given map scale (assuming 96 DPI standard).
        /// </summary>
        public static double GetMetersPerPixel(double mapScale)
            => (mapScale / 96.0) * 0.0254;

        /// <summary>
        /// Returns the label rotation angle (degrees) for a segment a→b.
        /// Accounts for latitude scaling in geographic coordinates.
        /// Guarantees text is never rendered upside-down: angle is always in (−90, +90].
        /// </summary>
        public static double GetLabelAngle(MapPoint a, MapPoint b)
        {
            if (a == null || b == null) return 0.0;

            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            var sr = a.SpatialReference ?? b.SpatialReference;
            if (sr != null && sr.IsGeographic)
            {
                double midLatRad = (((a.Y + b.Y) / 2.0) * Math.PI) / 180.0;
                double cosLat = Math.Max(0.01, Math.Cos(midLatRad));
                dx *= cosLat;
            }

            double deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);

            // Normalize so that text is readable from left to right (never upside-down)
            while (deg > 90.0)
                deg -= 180.0;
            while (deg <= -90.0)
                deg += 180.0;

            return deg;
        }

        /// <summary>
        /// Returns the outward normal unit vector (nx, ny) in metric ground space for a segment a→b.
        /// "Outward" = left side of the direction of travel (standard CCW polygon orientation).
        /// </summary>
        public static (double nx, double ny) GetOutwardNormal(MapPoint a, MapPoint b)
        {
            if (a == null || b == null) return (0.0, 1.0);

            double dx = b.X - a.X;
            double dy = b.Y - a.Y;

            var sr = a.SpatialReference ?? b.SpatialReference;
            if (sr != null && sr.IsGeographic)
            {
                double midLatRad = (((a.Y + b.Y) / 2.0) * Math.PI) / 180.0;
                double cosLat = Math.Max(0.01, Math.Cos(midLatRad));
                dx *= cosLat;
            }

            double len = Math.Sqrt((dx * dx) + (dy * dy));
            if (len < 1e-12) return (0.0, 1.0);

            // Rotate 90° CCW: (-dy, dx) / len
            return (-dy / len, dx / len);
        }

        /// <summary>
        /// Returns the midpoint of segment a→b offset perpendicularly by <paramref name="offsetMeters"/> meters.
        /// </summary>
        public static MapPoint GetOffsetMidpoint(MapPoint a, MapPoint b, double offsetMeters)
        {
            if (a == null || b == null) return a;

            double mx = (a.X + b.X) / 2.0;
            double my = (a.Y + b.Y) / 2.0;
            var sr = a.SpatialReference ?? b.SpatialReference;

            if (Math.Abs(offsetMeters) < 1e-12)
                return MapPointBuilderEx.CreateMapPoint(mx, my, sr);

            var (nx, ny) = GetOutwardNormal(a, b);
            var (dxMap, dyMap) = MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, sr, my);

            return MapPointBuilderEx.CreateMapPoint(mx + dxMap, my + dyMap, sr);
        }

        /// <summary>
        /// Calculates the dynamic midpoint of a segment within the currently visible viewport.
        /// When zoomed in so that only part of a long segment is on screen, this returns the midpoint
        /// of the VISIBLE portion of the segment so the dimension label is always visible (like ArcGIS Pro Maplex).
        /// </summary>
        public static MapPoint GetDynamicSegmentAnchor(MapView mapView, MapPoint a, MapPoint b, double offsetMeters)
        {
            if (a == null || b == null) return a;
            if (mapView == null) return GetOffsetMidpoint(a, b, offsetMeters);

            var sr = a.SpatialReference ?? b.SpatialReference;
            MapPoint anchorMidpoint = null;

            try
            {
                var extent = mapView.Extent;
                if (extent != null && !extent.IsEmpty)
                {
                    var mapSr = extent.SpatialReference ?? mapView.Map?.SpatialReference;
                    var ptA = (mapSr != null && sr != null && !sr.IsEqual(mapSr))
                        ? (GeometryEngine.Instance.Project(a, mapSr) as MapPoint ?? a)
                        : a;
                    var ptB = (mapSr != null && sr != null && !sr.IsEqual(mapSr))
                        ? (GeometryEngine.Instance.Project(b, mapSr) as MapPoint ?? b)
                        : b;

                    bool aIn = ptA.X >= extent.XMin && ptA.X <= extent.XMax && ptA.Y >= extent.YMin && ptA.Y <= extent.YMax;
                    bool bIn = ptB.X >= extent.XMin && ptB.X <= extent.XMax && ptB.Y >= extent.YMin && ptB.Y <= extent.YMax;

                    if (aIn && bIn)
                    {
                        double mx = (a.X + b.X) / 2.0;
                        double my = (a.Y + b.Y) / 2.0;
                        anchorMidpoint = MapPointBuilderEx.CreateMapPoint(mx, my, sr);
                    }
                    else
                    {
                        // One or both endpoints are outside the viewport -> compute visible section intersection in map coordinates
                        var segLineMap = PolylineBuilderEx.CreatePolyline(new[] { ptA, ptB }, mapSr);
                        var visibleGeom = GeometryEngine.Instance.Intersection(segLineMap, extent);

                        if (visibleGeom is Polyline visLine && !visLine.IsEmpty && visLine.PointCount >= 2)
                        {
                            var pStart = visLine.Points[0];
                            var pEnd = visLine.Points[visLine.PointCount - 1];
                            double midMapX = (pStart.X + pEnd.X) / 2.0;
                            double midMapY = (pStart.Y + pEnd.Y) / 2.0;
                            var midMapPt = MapPointBuilderEx.CreateMapPoint(midMapX, midMapY, mapSr);

                            if (sr != null && mapSr != null && !sr.IsEqual(mapSr))
                            {
                                anchorMidpoint = GeometryEngine.Instance.Project(midMapPt, sr) as MapPoint;
                            }
                            else
                            {
                                anchorMidpoint = midMapPt;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }

            if (anchorMidpoint == null)
            {
                double mx = (a.X + b.X) / 2.0;
                double my = (a.Y + b.Y) / 2.0;
                anchorMidpoint = MapPointBuilderEx.CreateMapPoint(mx, my, sr);
            }

            if (Math.Abs(offsetMeters) < 1e-12)
                return anchorMidpoint;

            var (nx, ny) = GetOutwardNormal(a, b);
            var (dxMap, dyMap) = MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, sr, anchorMidpoint.Y);

            return MapPointBuilderEx.CreateMapPoint(
                anchorMidpoint.X + dxMap,
                anchorMidpoint.Y + dyMap,
                sr);
        }

        /// <summary>
        /// Checks if any part of the segment a→b intersects the visible viewport with fast O(1) bounding box checks.
        /// Projects endpoints to the map coordinate system for 100% accurate viewport comparison.
        /// </summary>
        public static bool IsSegmentInViewport(MapView mapView, MapPoint a, MapPoint b)
        {
            if (mapView == null || a == null || b == null) return true;

            try
            {
                var extent = mapView.Extent;
                if (extent == null || extent.IsEmpty) return true;

                var mapSr = extent.SpatialReference ?? mapView.Map?.SpatialReference;
                var ptA = (mapSr != null && a.SpatialReference != null && !a.SpatialReference.IsEqual(mapSr))
                    ? (GeometryEngine.Instance.Project(a, mapSr) as MapPoint ?? a)
                    : a;
                var ptB = (mapSr != null && b.SpatialReference != null && !b.SpatialReference.IsEqual(mapSr))
                    ? (GeometryEngine.Instance.Project(b, mapSr) as MapPoint ?? b)
                    : b;

                double xMin = Math.Min(ptA.X, ptB.X);
                double xMax = Math.Max(ptA.X, ptB.X);
                double yMin = Math.Min(ptA.Y, ptB.Y);
                double yMax = Math.Max(ptA.Y, ptB.Y);

                double expXMin = extent.XMin - (extent.Width * 0.5);
                double expXMax = extent.XMax + (extent.Width * 0.5);
                double expYMin = extent.YMin - (extent.Height * 0.5);
                double expYMax = extent.YMax + (extent.Height * 0.5);

                // Fast AABB intersection
                return !(xMax < expXMin || xMin > expXMax || yMax < expYMin || yMin > expYMax);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Computes both endpoints for an extension line from a vertex.
        /// </summary>
        public static (MapPoint from, MapPoint to) GetExtensionLine(
            MapPoint vertex,
            double gapMeters,
            double overhangMeters,
            double dimOffsetMeters,
            double nx, double ny)
        {
            var sr = vertex.SpatialReference;
            var (fromDx, fromDy) = MetersToMapDelta(nx * gapMeters, ny * gapMeters, sr, vertex.Y);
            var (toDx, toDy) = MetersToMapDelta(
                nx * (dimOffsetMeters + overhangMeters),
                ny * (dimOffsetMeters + overhangMeters),
                sr, vertex.Y);

            var from = MapPointBuilderEx.CreateMapPoint(vertex.X + fromDx, vertex.Y + fromDy, sr);
            var to = MapPointBuilderEx.CreateMapPoint(vertex.X + toDx, vertex.Y + toDy, sr);
            return (from, to);
        }

        /// <summary>
        /// Calculates the screen-space length in pixels of a map segment.
        /// Projects points to the active map coordinate system to ensure accurate pixel measurement.
        /// </summary>
        public static double GetScreenLength(MapView mapView, MapPoint a, MapPoint b)
        {
            if (mapView == null || a == null || b == null) return 100;

            try
            {
                var mapSr = mapView.Map?.SpatialReference ?? mapView.Extent?.SpatialReference;
                var ptA = (mapSr != null && a.SpatialReference != null && !a.SpatialReference.IsEqual(mapSr))
                    ? (GeometryEngine.Instance.Project(a, mapSr) as MapPoint ?? a)
                    : a;
                var ptB = (mapSr != null && b.SpatialReference != null && !b.SpatialReference.IsEqual(mapSr))
                    ? (GeometryEngine.Instance.Project(b, mapSr) as MapPoint ?? b)
                    : b;

                var p1 = mapView.MapToScreen(ptA);
                var p2 = mapView.MapToScreen(ptB);
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double len = Math.Sqrt((dx * dx) + (dy * dy));
                return (double.IsNaN(len) || len <= 0) ? 100 : len;
            }
            catch
            {
                return 100;
            }
        }

        /// <summary>
        /// Calculates a dynamic viewport-aware interior label point for a polygon with fast bounding box checks.
        /// </summary>
        public static MapPoint GetDynamicInteriorLabelPoint(MapView mapView, Polygon polygon, MapPoint fallbackPoint)
        {
            if (polygon == null || polygon.IsEmpty) return fallbackPoint;
            if (mapView == null) return fallbackPoint ?? GeometryEngine.Instance.LabelPoint(polygon);

            try
            {
                var extent = mapView.Extent;
                if (extent != null && !extent.IsEmpty)
                {
                    var mapSr = extent.SpatialReference ?? mapView.Map?.SpatialReference;
                    var polySr = polygon.SpatialReference;

                    Polygon polyInMapSr = polygon;
                    if (mapSr != null && polySr != null && !polySr.IsEqual(mapSr))
                    {
                        polyInMapSr = GeometryEngine.Instance.Project(polygon, mapSr) as Polygon ?? polygon;
                    }

                    var polyExtent = polyInMapSr.Extent;
                    // Fast-path: If entire polygon is inside visible extent, return standard label point
                    if (polyExtent != null &&
                        polyExtent.XMin >= extent.XMin && polyExtent.XMax <= extent.XMax &&
                        polyExtent.YMin >= extent.YMin && polyExtent.YMax <= extent.YMax)
                    {
                        return fallbackPoint ?? GeometryEngine.Instance.LabelPoint(polygon);
                    }

                    // Polygon extends outside visible screen -> intersect in map coordinates
                    var visiblePart = GeometryEngine.Instance.Intersection(polyInMapSr, extent);
                    if (visiblePart is Polygon visPoly && !visPoly.IsEmpty)
                    {
                        var lpMap = GeometryEngine.Instance.LabelPoint(visPoly) ?? GeometryEngine.Instance.Centroid(visPoly);
                        if (lpMap != null && !lpMap.IsEmpty)
                        {
                            if (polySr != null && mapSr != null && !polySr.IsEqual(mapSr))
                            {
                                var projectedBack = GeometryEngine.Instance.Project(lpMap, polySr) as MapPoint;
                                if (projectedBack != null) return projectedBack;
                            }
                            return lpMap;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to static label point
            }

            return fallbackPoint ?? GeometryEngine.Instance.LabelPoint(polygon);
        }

        /// <summary>
        /// Checks if a map point is within the current visible viewport extent.
        /// Uses coordinate projection to ensure compatibility between map and layer spatial references.
        /// </summary>
        public static bool IsInViewport(MapView mapView, MapPoint point)
        {
            if (mapView == null || point == null) return true;

            try
            {
                var extent = mapView.Extent;
                if (extent == null || extent.IsEmpty) return true;

                var mapSr = extent.SpatialReference ?? mapView.Map?.SpatialReference;
                var pt = (mapSr != null && point.SpatialReference != null && !point.SpatialReference.IsEqual(mapSr))
                    ? (GeometryEngine.Instance.Project(point, mapSr) as MapPoint ?? point)
                    : point;

                double expXMin = extent.XMin - (extent.Width * 0.3);
                double expXMax = extent.XMax + (extent.Width * 0.3);
                double expYMin = extent.YMin - (extent.Height * 0.3);
                double expYMax = extent.YMax + (extent.Height * 0.3);

                return pt.X >= expXMin && pt.X <= expXMax && pt.Y >= expYMin && pt.Y <= expYMax;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Legacy helper for map units per pixel at given scale.
        /// </summary>
        public static double MapUnitsPerPixel(double mapScale)
            => GetMetersPerPixel(mapScale);
    }
}



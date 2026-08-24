using System;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;

namespace DimensionOverlay.Rendering
{
    /// <summary>
    /// Pure math and viewport-aware geometric calculations for dimension label and line placement.
    /// Implements ultra-fast Maplex-style dynamic visible placement with O(1) bounding box checks.
    /// Thread-safe and designed for high-performance 60 FPS rendering without lag.
    /// </summary>
    internal static class DimensionLabelManager
    {
        /// <summary>
        /// Returns the label rotation angle (degrees) for a segment a→b.
        /// Guarantees text is never rendered upside-down: angle is always in (−90, +90].
        /// </summary>
        public static double GetLabelAngle(MapPoint a, MapPoint b)
        {
            if (a == null || b == null) return 0.0;

            double dx  = b.X - a.X;
            double dy  = b.Y - a.Y;
            double deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);

            // Normalize so that text is readable from left to right (never upside-down)
            while (deg > 90.0)
                deg -= 180.0;
            while (deg <= -90.0)
                deg += 180.0;

            return deg;
        }

        /// <summary>
        /// Returns the outward normal unit vector (nx, ny) for a segment a→b.
        /// "Outward" = left side of the direction of travel (standard CCW polygon orientation).
        /// </summary>
        public static (double nx, double ny) GetOutwardNormal(MapPoint a, MapPoint b)
        {
            if (a == null || b == null) return (0.0, 1.0);

            double dx  = b.X - a.X;
            double dy  = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-12) return (0.0, 1.0);

            // Rotate 90° CCW: (-dy, dx) / len
            return (-dy / len, dx / len);
        }

        /// <summary>
        /// Returns the midpoint of segment a→b offset perpendicularly by <paramref name="offsetMapUnits"/>.
        /// </summary>
        public static MapPoint GetOffsetMidpoint(MapPoint a, MapPoint b, double offsetMapUnits)
        {
            if (a == null || b == null) return a;

            double mx = (a.X + b.X) / 2.0;
            double my = (a.Y + b.Y) / 2.0;

            if (Math.Abs(offsetMapUnits) < 1e-12)
                return MapPointBuilderEx.CreateMapPoint(mx, my, a.SpatialReference);

            var (nx, ny) = GetOutwardNormal(a, b);
            return MapPointBuilderEx.CreateMapPoint(
                mx + (nx * offsetMapUnits),
                my + (ny * offsetMapUnits),
                a.SpatialReference);
        }

        /// <summary>
        /// Calculates the dynamic midpoint of a segment within the currently visible viewport.
        /// When zoomed in so that only part of a long segment is on screen, this returns the midpoint
        /// of the VISIBLE portion of the segment so the dimension label is always visible (like ArcGIS Pro Maplex).
        /// </summary>
        public static MapPoint GetDynamicSegmentAnchor(MapView mapView, MapPoint a, MapPoint b, double offsetMapUnits)
        {
            if (a == null || b == null) return a;
            if (mapView == null) return GetOffsetMidpoint(a, b, offsetMapUnits);

            MapPoint anchorMidpoint = null;

            try
            {
                var extent = mapView.Extent;
                if (extent != null && !extent.IsEmpty)
                {
                    var sr = a.SpatialReference;
                    var extSr = extent.SpatialReference;

                    Envelope testExtent = extent;
                    if (extSr != null && sr != null && !extSr.IsEqual(sr))
                    {
                        testExtent = GeometryEngine.Instance.Project(extent, sr) as Envelope ?? extent;
                    }

                    // Fast-path O(1): If both endpoints are inside the viewport, use regular midpoint instantly
                    bool aIn = a.X >= testExtent.XMin && a.X <= testExtent.XMax && a.Y >= testExtent.YMin && a.Y <= testExtent.YMax;
                    bool bIn = b.X >= testExtent.XMin && b.X <= testExtent.XMax && b.Y >= testExtent.YMin && b.Y <= testExtent.YMax;

                    if (aIn && bIn)
                    {
                        double mx = (a.X + b.X) / 2.0;
                        double my = (a.Y + b.Y) / 2.0;
                        anchorMidpoint = MapPointBuilderEx.CreateMapPoint(mx, my, sr);
                    }
                    else
                    {
                        // One or both endpoints are outside the viewport -> compute visible section intersection
                        var segLine = PolylineBuilderEx.CreatePolyline(new[] { a, b }, sr);
                        var visibleGeom = GeometryEngine.Instance.Intersection(segLine, testExtent);

                        if (visibleGeom is Polyline visLine && !visLine.IsEmpty && visLine.PointCount >= 2)
                        {
                            var pStart = visLine.Points[0];
                            var pEnd = visLine.Points[visLine.PointCount - 1];
                            double mx = (pStart.X + pEnd.X) / 2.0;
                            double my = (pStart.Y + pEnd.Y) / 2.0;
                            anchorMidpoint = MapPointBuilderEx.CreateMapPoint(mx, my, sr);
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
                anchorMidpoint = MapPointBuilderEx.CreateMapPoint(mx, my, a.SpatialReference);
            }

            if (Math.Abs(offsetMapUnits) < 1e-12)
                return anchorMidpoint;

            var (nx, ny) = GetOutwardNormal(a, b);
            return MapPointBuilderEx.CreateMapPoint(
                anchorMidpoint.X + (nx * offsetMapUnits),
                anchorMidpoint.Y + (ny * offsetMapUnits),
                a.SpatialReference);
        }

        /// <summary>
        /// Checks if any part of the segment a→b intersects the visible viewport with fast O(1) bounding box checks.
        /// </summary>
        public static bool IsSegmentInViewport(MapView mapView, MapPoint a, MapPoint b)
        {
            if (mapView == null || a == null || b == null) return true;

            try
            {
                var extent = mapView.Extent;
                if (extent == null || extent.IsEmpty) return true;

                var sr = extent.SpatialReference;
                var segSr = a.SpatialReference;

                Envelope testExtent = extent;
                if (sr != null && segSr != null && !sr.IsEqual(segSr))
                {
                    testExtent = GeometryEngine.Instance.Project(extent, segSr) as Envelope ?? extent;
                }

                double xMin = Math.Min(a.X, b.X);
                double xMax = Math.Max(a.X, b.X);
                double yMin = Math.Min(a.Y, b.Y);
                double yMax = Math.Max(a.Y, b.Y);

                double expXMin = testExtent.XMin - (testExtent.Width * 0.3);
                double expXMax = testExtent.XMax + (testExtent.Width * 0.3);
                double expYMin = testExtent.YMin - (testExtent.Height * 0.3);
                double expYMax = testExtent.YMax + (testExtent.Height * 0.3);

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
            double gapMapUnits,
            double overhangMapUnits,
            double dimOffsetMapUnits,
            double nx, double ny)
        {
            var sr = vertex.SpatialReference;
            var from = MapPointBuilderEx.CreateMapPoint(
                vertex.X + (nx * gapMapUnits),
                vertex.Y + (ny * gapMapUnits),
                sr);
            var to = MapPointBuilderEx.CreateMapPoint(
                vertex.X + (nx * (dimOffsetMapUnits + overhangMapUnits)),
                vertex.Y + (ny * (dimOffsetMapUnits + overhangMapUnits)),
                sr);
            return (from, to);
        }

        /// <summary>
        /// Calculates the screen-space length in pixels of a map segment.
        /// </summary>
        public static double GetScreenLength(MapView mapView, MapPoint a, MapPoint b)
        {
            if (mapView == null || a == null || b == null) return 100;

            try
            {
                var p1 = mapView.MapToScreen(a);
                var p2 = mapView.MapToScreen(b);
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double len = Math.Sqrt((dx * dx) + (dy * dy));
                return double.IsNaN(len) ? 100 : len;
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
                    var polySr = polygon.SpatialReference;
                    var extSr = extent.SpatialReference;

                    Envelope testExtent = extent;
                    if (extSr != null && polySr != null && !extSr.IsEqual(polySr))
                    {
                        testExtent = GeometryEngine.Instance.Project(extent, polySr) as Envelope ?? extent;
                    }

                    var polyExtent = polygon.Extent;
                    // Fast-path: If entire polygon is inside visible extent, return standard label point
                    if (polyExtent != null &&
                        polyExtent.XMin >= testExtent.XMin && polyExtent.XMax <= testExtent.XMax &&
                        polyExtent.YMin >= testExtent.YMin && polyExtent.YMax <= testExtent.YMax)
                    {
                        return fallbackPoint ?? GeometryEngine.Instance.LabelPoint(polygon);
                    }

                    // Polygon extends outside visible screen -> intersect to find visible portion
                    var visiblePart = GeometryEngine.Instance.Intersection(polygon, testExtent);
                    if (visiblePart is Polygon visPoly && !visPoly.IsEmpty)
                    {
                        var lp = GeometryEngine.Instance.LabelPoint(visPoly);
                        if (lp != null && !lp.IsEmpty)
                        {
                            return lp;
                        }

                        var c = GeometryEngine.Instance.Centroid(visPoly);
                        if (c != null && !c.IsEmpty)
                        {
                            return c;
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
                if (extent == null) return true;

                var sr = extent.SpatialReference;
                var pt = (sr != null && point.SpatialReference != null && !point.SpatialReference.IsEqual(sr))
                    ? GeometryEngine.Instance.Project(point, sr) as MapPoint ?? point
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
        /// Computes map units per pixel for a given map scale (assuming 96 DPI standard).
        /// </summary>
        public static double MapUnitsPerPixel(double mapScale)
            => (mapScale / 96.0) * 0.0254;
    }
}

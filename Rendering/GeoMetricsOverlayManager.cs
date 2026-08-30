using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using GeoMetrics.Measurement;
using GeoMetrics.Models;
using GeoMetrics.Utilities;

namespace GeoMetrics.Rendering
{
    /// <summary>
    /// Centralized manager for GeoMetrics overlay rendering and lifecycle.
    ///
    /// ARCHITECTURE:
    /// - Separates GEOMETRY MEASUREMENT from VIEWPORT LAYOUT.
    /// - Geometry measurements are cached in CachedGeometryMeasurements in MAP coordinates.
    /// - Viewport changes (pan/zoom/camera) reuse cached measurements and recalculate
    ///   screen-space anchor positions, dynamic interior label points (for edge zoom),
    ///   dynamic segment midpoints along visible line sections, screen-pixel offsets, rotation angles, and visibility.
    /// - Handles all MapView overlay graphic handles cleanly without leaking.
    /// </summary>
    internal sealed class GeoMetricsOverlayManager : IDisposable
    {
        private readonly MapView _mapView;
        private readonly List<IDisposable> _handles = new();
        private readonly CachedGeometryMeasurements _cachedMeasurements = new();

        private const double MinScreenSegmentLengthPx = 25.0;
        private const double FarScale = 50_000;
        private const double MedScale = 10_000;

        public GeoMetricsOverlayManager(MapView mapView)
        {
            _mapView = mapView ?? throw new ArgumentNullException(nameof(mapView));
        }

        public CachedGeometryMeasurements CachedMeasurements => _cachedMeasurements;

        public bool HasCachedData => _cachedMeasurements.HasData;

        // ── Clear API ─────────────────────────────────────────────────────────────

        public void Clear()
        {
            lock (_handles)
            {
                foreach (var h in _handles)
                {
                    try { h?.Dispose(); } catch { }
                }
                _handles.Clear();
            }
            _cachedMeasurements.Clear();
        }

        public void ClearGraphicsOnly()
        {
            lock (_handles)
            {
                foreach (var h in _handles)
                {
                    try { h?.Dispose(); } catch { }
                }
                _handles.Clear();
            }
        }

        // ── Measurement Ingestion (Called when Geometry changes) ──────────────────

        public void UpdateFromPolygon(PolygonMeasurementResult result, DimensionSettings settings)
        {
            if (result == null || _mapView == null)
            {
                Clear();
                return;
            }

            _cachedMeasurements.Clear();
            _cachedMeasurements.GeometryType = GeometryType.Polygon;
            _cachedMeasurements.SourcePolygon = result.SourcePolygon;
            _cachedMeasurements.PolygonResult = result;
            _cachedMeasurements.SpatialReference = result.InteriorLabelPoint?.SpatialReference ?? result.SourcePolygon?.SpatialReference;

            // 1. Area Item
            if (result.InteriorLabelPoint != null)
            {
                _cachedMeasurements.Items.Add(new DimensionItem
                {
                    Role = DimensionItemRole.Area,
                    Value = result.DisplayArea,
                    AnchorPoint = result.InteriorLabelPoint,
                    Angle = 0,
                    IsVisible = settings.ShowArea,
                    UnitAbbrev = result.AreaUnitAbbrev
                });
            }

            // 2. Segment Items
            if (result.Segments != null)
            {
                for (int i = 0; i < result.Segments.Count; i++)
                {
                    var seg = result.Segments[i];
                    _cachedMeasurements.Items.Add(new DimensionItem
                    {
                        Role = DimensionItemRole.Segment,
                        Value = seg.DisplayLength,
                        StartPoint = seg.Start,
                        EndPoint = seg.End,
                        AnchorPoint = DimensionLabelManager.GetOffsetMidpoint(seg.Start, seg.End, 0),
                        Angle = DimensionLabelManager.GetLabelAngle(seg.Start, seg.End),
                        IsVisible = settings.ShowSegmentLength,
                        Bearing = seg.Bearing,
                        UnitAbbrev = seg.UnitAbbrev,
                        SegmentIndex = i
                    });
                }
            }

            // 3. Perimeter Item
            if (result.InteriorLabelPoint != null)
            {
                _cachedMeasurements.Items.Add(new DimensionItem
                {
                    Role = DimensionItemRole.Perimeter,
                    Value = result.DisplayPerimeter,
                    AnchorPoint = result.InteriorLabelPoint,
                    Angle = 0,
                    IsVisible = settings.ShowPerimeter,
                    UnitAbbrev = result.LinearUnitAbbrev
                });
            }

            // 4. Vertex Angles
            if (result.VertexAngles != null)
            {
                foreach (var va in result.VertexAngles)
                {
                    _cachedMeasurements.Items.Add(new DimensionItem
                    {
                        Role = DimensionItemRole.VertexAngle,
                        Value = va.AngleDegrees,
                        AnchorPoint = va.Vertex,
                        StartPoint = va.PrevPoint,
                        EndPoint = va.NextPoint,
                        Angle = 0,
                        IsVisible = settings.ShowVertexAngles
                    });
                }
            }

            // 5. Area Difference (Before & After Edit)
            if (settings.ShowAreaDifference &&
                result.InteriorLabelPoint != null &&
                result.OriginalDisplayArea.HasValue)
            {
                var lines = new List<string>();
                double orig = result.OriginalDisplayArea.Value;
                double curr = result.DisplayArea;
                double diff = curr - orig;
                double pct  = orig > 0 ? (diff / orig) * 100.0 : 0;
                string sign = diff >= 0 ? "+" : "";

                lines.Add("Before: " + UnitConverter.FormatArea(orig, settings.Precision, result.AreaUnitAbbrev));
                lines.Add("Current: " + UnitConverter.FormatArea(curr, settings.Precision, result.AreaUnitAbbrev));
                lines.Add($"\u0394: {sign}{UnitConverter.FormatArea(Math.Abs(diff), settings.Precision, result.AreaUnitAbbrev)} ({sign}{pct:F1}%)");

                _cachedMeasurements.Items.Add(new DimensionItem
                {
                    Role = DimensionItemRole.Qc,
                    DisplayText = string.Join("\n", lines),
                    AnchorPoint = result.InteriorLabelPoint,
                    Angle = 0,
                    IsVisible = true
                });
            }

            // 6. Vertex Coordinates
            if (result.SourcePolygon != null && result.SourcePolygon.Parts != null)
            {
                foreach (var part in result.SourcePolygon.Parts)
                {
                    var pts = new List<MapPoint>();
                    var polySr = result.SourcePolygon.SpatialReference ?? _cachedMeasurements.SpatialReference;
                    foreach (var seg in part)
                    {
                        var sp = seg.StartPoint;
                        if (sp.SpatialReference == null && polySr != null)
                            sp = MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, polySr);
                        pts.Add(sp);
                    }
                    int k = pts.Count;
                    if (k > 0 && Math.Abs(pts[0].X - pts[k - 1].X) < 1e-7 && Math.Abs(pts[0].Y - pts[k - 1].Y) < 1e-7)
                        k--;

                    for (int i = 0; i < k; i++)
                    {
                        var v = pts[i];
                        var a = pts[(i - 1 + k) % k];
                        var b = pts[(i + 1) % k];
                        _cachedMeasurements.Items.Add(new DimensionItem
                        {
                            Role = DimensionItemRole.VertexCoordinate,
                            AnchorPoint = v,
                            StartPoint = a,
                            EndPoint = b,
                            Angle = 0,
                            IsVisible = settings.ShowVertexCoordinates
                        });
                    }
                }
            }

            // Execute viewport-aware layout and rendering
            UpdateLayout(settings);
        }

        public void UpdateFromPolyline(PolylineMeasurementResult result, DimensionSettings settings)
        {
            if (result == null || _mapView == null)
            {
                Clear();
                return;
            }

            _cachedMeasurements.Clear();
            _cachedMeasurements.GeometryType = GeometryType.Polyline;
            _cachedMeasurements.SourcePolyline = result.SourcePolyline;
            _cachedMeasurements.PolylineResult = result;

            if (result.Segments != null)
            {
                for (int i = 0; i < result.Segments.Count; i++)
                {
                    var seg = result.Segments[i];
                    _cachedMeasurements.Items.Add(new DimensionItem
                    {
                        Role = DimensionItemRole.Segment,
                        Value = seg.DisplayLength,
                        StartPoint = seg.Start,
                        EndPoint = seg.End,
                        AnchorPoint = DimensionLabelManager.GetOffsetMidpoint(seg.Start, seg.End, 0),
                        Angle = DimensionLabelManager.GetLabelAngle(seg.Start, seg.End),
                        IsVisible = settings.ShowSegmentLength,
                        Bearing = seg.Bearing,
                        UnitAbbrev = seg.UnitAbbrev,
                        SegmentIndex = i
                    });
                }
            }

            if (result.VertexAngles != null)
            {
                foreach (var va in result.VertexAngles)
                {
                    _cachedMeasurements.Items.Add(new DimensionItem
                    {
                        Role = DimensionItemRole.VertexAngle,
                        Value = va.AngleDegrees,
                        AnchorPoint = va.Vertex,
                        StartPoint = va.PrevPoint,
                        EndPoint = va.NextPoint,
                        Angle = 0,
                        IsVisible = settings.ShowVertexAngles
                    });
                }
            }

            if (result.SourcePolyline != null && result.SourcePolyline.Parts != null)
            {
                foreach (var part in result.SourcePolyline.Parts)
                {
                    var pts = new List<MapPoint>();
                    foreach (var seg in part)
                    {
                        var sp = seg.StartPoint;
                        if (sp.SpatialReference == null && result.SourcePolyline.SpatialReference != null)
                            sp = MapPointBuilderEx.CreateMapPoint(sp.X, sp.Y, result.SourcePolyline.SpatialReference);
                        pts.Add(sp);
                    }
                    if (part.Count > 0)
                    {
                        var ep = part[part.Count - 1].EndPoint;
                        if (ep.SpatialReference == null && result.SourcePolyline.SpatialReference != null)
                            ep = MapPointBuilderEx.CreateMapPoint(ep.X, ep.Y, result.SourcePolyline.SpatialReference);
                        pts.Add(ep);
                    }

                    for (int i = 0; i < pts.Count; i++)
                    {
                        var v = pts[i];
                        var a = i > 0 ? pts[i - 1] : null;
                        var b = i < pts.Count - 1 ? pts[i + 1] : null;
                        _cachedMeasurements.Items.Add(new DimensionItem
                        {
                            Role = DimensionItemRole.VertexCoordinate,
                            AnchorPoint = v,
                            StartPoint = a,
                            EndPoint = b,
                            Angle = 0,
                            IsVisible = settings.ShowVertexCoordinates
                        });
                    }
                }
            }

            UpdateLayout(settings);
        }

        // ── Viewport Change Handler (Called when MapView zooms, pans, navigates) ──

        public void UpdateForViewpoint(DimensionSettings settings)
        {
            if (!_cachedMeasurements.HasData || _mapView == null) return;
            UpdateLayout(settings);
        }

        // ── Viewport-Aware Layout & Drawing ───────────────────────────────────────

        private void UpdateLayout(DimensionSettings settings)
        {
            ClearGraphicsOnly();

            if (!_cachedMeasurements.HasData || _mapView == null) return;

            double mapScale = _mapView.Camera?.Scale ?? 5000;
            double mpp = DimensionLabelManager.GetMetersPerPixel(mapScale);

            bool farZoom = mapScale > FarScale;
            bool medZoom = !farZoom && mapScale > MedScale;

            var (textColor, haloColor) = ResolveTextAndHaloColor(settings);
            double baseOffsetMeters = Math.Max(6.0, settings.OffsetPixels) * mpp;

            // 1. Process Area Item (Priority 1) with Dynamic Viewport-Aware Interior Placement
            var areaItem = _cachedMeasurements.Items.FirstOrDefault(i => i.Role == DimensionItemRole.Area);
            MapPoint dynamicAreaAnchor = areaItem?.AnchorPoint;

            if (areaItem != null && settings.ShowArea)
            {
                // If zoomed in, calculate interior label point inside the visible part of the polygon
                if (_cachedMeasurements.SourcePolygon != null)
                {
                    dynamicAreaAnchor = DimensionLabelManager.GetDynamicInteriorLabelPoint(
                        _mapView, _cachedMeasurements.SourcePolygon, areaItem.AnchorPoint);
                }

                if (dynamicAreaAnchor != null && DimensionLabelManager.IsInViewport(_mapView, dynamicAreaAnchor))
                {
                    string areaText = UnitConverter.FormatArea(areaItem.Value, settings.Precision, areaItem.UnitAbbrev);
                    PlaceText(dynamicAreaAnchor, areaText, 0, DimensionItemRole.Area, textColor, haloColor);
                }
            }

            if (farZoom)
            {
                // In far zoom, only show area label to avoid visual clutter
                return;
            }

            // 2. Process Segment Items with Dynamic Visible-Section Anchoring (Maplex "Offset straight / Best position")
            var segmentItems = _cachedMeasurements.Items
                .Where(i => i.Role == DimensionItemRole.Segment)
                .ToList();

            if (settings.ShowSegmentLength && segmentItems.Count > 0)
            {
                IEnumerable<DimensionItem> activeSegments = segmentItems;
                if (medZoom)
                {
                    int take = Math.Max(4, segmentItems.Count / 3);
                    activeSegments = segmentItems
                        .OrderByDescending(s => s.Value)
                        .Take(take);
                }

                foreach (var segItem in activeSegments)
                {
                    var a = segItem.StartPoint;
                    var b = segItem.EndPoint;
                    if (a == null || b == null) continue;

                    // Check if any portion of the segment is in the visible viewport
                    if (!DimensionLabelManager.IsSegmentInViewport(_mapView, a, b))
                    {
                        continue;
                    }

                    // Screen length check
                    double screenLen = DimensionLabelManager.GetScreenLength(_mapView, a, b);
                    if (screenLen < MinScreenSegmentLengthPx)
                    {
                        continue;
                    }

                    // Calculate normalized angle & dynamic anchor on the visible section of the line
                    double angleDeg = DimensionLabelManager.GetLabelAngle(a, b);
                    var (nx, ny) = DimensionLabelManager.GetOutwardNormal(a, b);
                    double offsetMeters = baseOffsetMeters;

                    // Dynamic anchor: if zoomed in, places label at the midpoint of the VISIBLE piece of the segment
                    var dynamicSegAnchor = DimensionLabelManager.GetDynamicSegmentAnchor(_mapView, a, b, offsetMeters);

                    // Format text based on style (Numbers_Only shows NUMBER + UNIT)
                    string segText = UnitConverter.FormatLength(segItem.Value, settings.Precision, segItem.UnitAbbrev);

                    if (segItem.Bearing.HasValue && settings.ShowBearings)
                        segText += $"  ({UnitConverter.FormatBearing(segItem.Bearing.Value)})";

                    // Render based on style
                    if (settings.DimensionStyle == DimensionStyleOption.Numbers_Only)
                    {
                        PlaceText(dynamicSegAnchor, segText, angleDeg, DimensionItemRole.Segment, textColor, haloColor);
                    }
                    else
                    {
                        // CAD Standard / Minimal / High Contrast
                        double gapMeters = 3.0 * mpp;
                        double overhgMeters = 4.0 * mpp;
                        double tickMeters = 5.0 * mpp;

                        var aSr = a.SpatialReference;
                        var bSr = b.SpatialReference;

                        var (aOffDx, aOffDy) = DimensionLabelManager.MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, aSr, a.Y);
                        var (bOffDx, bOffDy) = DimensionLabelManager.MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, bSr, b.Y);

                        var aOff = MapPointBuilderEx.CreateMapPoint(a.X + aOffDx, a.Y + aOffDy, aSr);
                        var bOff = MapPointBuilderEx.CreateMapPoint(b.X + bOffDx, b.Y + bOffDy, bSr);

                        var lineColor = StyleLineColor(settings);
                        var dimSym = SymbolFactory.Instance.ConstructLineSymbol(lineColor, 1.4);
                        var extSym = SymbolFactory.Instance.ConstructLineSymbol(lineColor, 0.8);

                        // Dimension line A' ── B'
                        AddLine(aOff, bOff, dimSym);

                        // Extension lines (skipped in Minimal style)
                        if (settings.DimensionStyle != DimensionStyleOption.Minimal)
                        {
                            var (e1f, e1t) = DimensionLabelManager.GetExtensionLine(a, gapMeters, overhgMeters, offsetMeters, nx, ny);
                            var (e2f, e2t) = DimensionLabelManager.GetExtensionLine(b, gapMeters, overhgMeters, offsetMeters, nx, ny);
                            AddLine(e1f, e1t, extSym);
                            AddLine(e2f, e2t, extSym);
                        }

                        // Diagonal slash ticks at endpoints
                        AddSlashTick(aOff, a, b, tickMeters, dimSym);
                        AddSlashTick(bOff, a, b, tickMeters, dimSym);

                        // Dimension text at dynamic midpoint
                        PlaceText(dynamicSegAnchor, segText, angleDeg, DimensionItemRole.Segment, textColor, haloColor);
                    }
                }
            }

            // 3. Process Perimeter Item (Priority 3)
            var perimItem = _cachedMeasurements.Items.FirstOrDefault(i => i.Role == DimensionItemRole.Perimeter);
            var perimAnchor = dynamicAreaAnchor ?? perimItem?.AnchorPoint;

            if (perimItem != null && settings.ShowPerimeter && perimAnchor != null)
            {
                var (pDx, pDy) = DimensionLabelManager.MetersToMapDelta(0, -14.0 * mpp, perimAnchor.SpatialReference, perimAnchor.Y);
                var pt = MapPointBuilderEx.CreateMapPoint(
                    perimAnchor.X + pDx,
                    perimAnchor.Y + pDy,
                    perimAnchor.SpatialReference);

                if (DimensionLabelManager.IsInViewport(_mapView, pt))
                {
                    string perimText = (settings.DimensionStyle == DimensionStyleOption.Numbers_Only)
                        ? UnitConverter.FormatLength(perimItem.Value, settings.Precision, perimItem.UnitAbbrev)
                        : "P: " + UnitConverter.FormatLength(perimItem.Value, settings.Precision, perimItem.UnitAbbrev);

                    PlaceText(pt, perimText, 0, DimensionItemRole.Perimeter, textColor, haloColor);
                }
            }

            // 4. Process QC Item (Priority 4)
            var qcItem = _cachedMeasurements.Items.FirstOrDefault(i => i.Role == DimensionItemRole.Qc);
            var qcAnchor = dynamicAreaAnchor ?? qcItem?.AnchorPoint;

            if (qcItem != null && qcAnchor != null && !string.IsNullOrEmpty(qcItem.DisplayText))
            {
                double offsetY = (settings.ShowPerimeter ? 30.0 : 16.0) * mpp;
                var (qcDx, qcDy) = DimensionLabelManager.MetersToMapDelta(0, -offsetY, qcAnchor.SpatialReference, qcAnchor.Y);
                var pt = MapPointBuilderEx.CreateMapPoint(
                    qcAnchor.X + qcDx,
                    qcAnchor.Y + qcDy,
                    qcAnchor.SpatialReference);

                if (DimensionLabelManager.IsInViewport(_mapView, pt))
                {
                    PlaceText(pt, qcItem.DisplayText, 0, DimensionItemRole.Qc, textColor, haloColor);
                }
            }

            // 5. Process Vertex Angle Items (Priority 5)
            if (settings.ShowVertexAngles)
            {
                var angleItems = _cachedMeasurements.Items
                    .Where(i => i.Role == DimensionItemRole.VertexAngle)
                    .ToList();

                foreach (var ai in angleItems)
                {
                    var v = ai.AnchorPoint;
                    var a = ai.StartPoint;
                    var b = ai.EndPoint;
                    if (v == null || a == null || b == null) continue;

                    if (!DimensionLabelManager.IsInViewport(_mapView, v)) continue;

                    // Calculate interior bisector direction for clean label placement
                    double dx1 = a.X - v.X, dy1 = a.Y - v.Y;
                    double dx2 = b.X - v.X, dy2 = b.Y - v.Y;

                    var vSr = v.SpatialReference;
                    if (vSr != null && vSr.IsGeographic)
                    {
                        double latRad = (v.Y * Math.PI) / 180.0;
                        double cosLat = Math.Max(0.01, Math.Cos(latRad));
                        dx1 *= cosLat;
                        dx2 *= cosLat;
                    }

                    double l1 = Math.Sqrt((dx1 * dx1) + (dy1 * dy1));
                    double l2 = Math.Sqrt((dx2 * dx2) + (dy2 * dy2));

                    MapPoint labelPt = v;
                    if (l1 > 1e-7 && l2 > 1e-7)
                    {
                        double u1x = dx1 / l1, u1y = dy1 / l1;
                        double u2x = dx2 / l2, u2y = dy2 / l2;
                        double bx = u1x + u2x, by = u1y + u2y;
                        double blen = Math.Sqrt((bx * bx) + (by * by));
                        if (blen > 1e-6)
                        {
                            double offsetMeters = Math.Max(12.0, settings.OffsetPixels * 0.9) * mpp;
                            var (vaDx, vaDy) = DimensionLabelManager.MetersToMapDelta(
                                (bx / blen) * offsetMeters,
                                (by / blen) * offsetMeters,
                                vSr, v.Y);

                            labelPt = MapPointBuilderEx.CreateMapPoint(
                                v.X + vaDx,
                                v.Y + vaDy,
                                vSr);
                        }
                    }

                    string angleText = UnitConverter.FormatAngle(ai.Value, settings.Precision);
                    PlaceText(labelPt, angleText, 0, DimensionItemRole.VertexAngle, textColor, haloColor);
                }
            }

            // 6. Process Vertex Coordinate Items (Priority 6)
            if (settings.ShowVertexCoordinates)
            {
                var coordItems = _cachedMeasurements.Items
                    .Where(i => i.Role == DimensionItemRole.VertexCoordinate)
                    .ToList();

                foreach (var ci in coordItems)
                {
                    var v = ci.AnchorPoint;
                    if (v == null) continue;

                    if (!DimensionLabelManager.IsInViewport(_mapView, v)) continue;

                    var a = ci.StartPoint;
                    var b = ci.EndPoint;
                    var vSr = v.SpatialReference ?? _cachedMeasurements.SpatialReference ?? _mapView.Map?.SpatialReference;
                    bool isGeo = vSr != null && vSr.IsGeographic;

                    MapPoint labelPt = v;
                    double offsetMeters = Math.Max(16.0, settings.OffsetPixels * 1.1) * mpp;

                    if (a != null && b != null)
                    {
                        // Corner vertex: use exterior bisector direction (opposite to interior)
                        double dx1 = a.X - v.X, dy1 = a.Y - v.Y;
                        double dx2 = b.X - v.X, dy2 = b.Y - v.Y;

                        if (isGeo)
                        {
                            double latRad = (v.Y * Math.PI) / 180.0;
                            double cosLat = Math.Max(0.01, Math.Cos(latRad));
                            dx1 *= cosLat;
                            dx2 *= cosLat;
                        }

                        double l1 = Math.Sqrt((dx1 * dx1) + (dy1 * dy1));
                        double l2 = Math.Sqrt((dx2 * dx2) + (dy2 * dy2));

                        if (l1 > 1e-7 && l2 > 1e-7)
                        {
                            double u1x = dx1 / l1, u1y = dy1 / l1;
                            double u2x = dx2 / l2, u2y = dy2 / l2;
                            double bx = u1x + u2x, by = u1y + u2y;
                            double blen = Math.Sqrt((bx * bx) + (by * by));
                            if (blen > 1e-6)
                            {
                                // Exterior direction: -bx, -by
                                var (vcDx, vcDy) = DimensionLabelManager.MetersToMapDelta(
                                    (-bx / blen) * offsetMeters,
                                    (-by / blen) * offsetMeters,
                                    vSr, v.Y);

                                labelPt = MapPointBuilderEx.CreateMapPoint(v.X + vcDx, v.Y + vcDy, vSr);
                            }
                        }
                    }
                    else if (a != null || b != null)
                    {
                        // Endpoint of a polyline: offset normal to the adjacent segment
                        var other = a ?? b;
                        var (nx, ny) = DimensionLabelManager.GetOutwardNormal(v, other);
                        var (vcDx, vcDy) = DimensionLabelManager.MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, vSr, v.Y);
                        labelPt = MapPointBuilderEx.CreateMapPoint(v.X + vcDx, v.Y + vcDy, vSr);
                    }
                    else
                    {
                        var (vcDx, vcDy) = DimensionLabelManager.MetersToMapDelta(offsetMeters * 0.707, offsetMeters * 0.707, vSr, v.Y);
                        labelPt = MapPointBuilderEx.CreateMapPoint(v.X + vcDx, v.Y + vcDy, vSr);
                    }

                    int p = settings.CoordinatePrecision;
                    string coordText = isGeo
                        ? $"X: {v.X.ToString($"F{p}", System.Globalization.CultureInfo.InvariantCulture)}°\nY: {v.Y.ToString($"F{p}", System.Globalization.CultureInfo.InvariantCulture)}°"
                        : $"X: {v.X.ToString($"F{p}", System.Globalization.CultureInfo.InvariantCulture)}\nY: {v.Y.ToString($"F{p}", System.Globalization.CultureInfo.InvariantCulture)}";

                    PlaceText(labelPt, coordText, 0, DimensionItemRole.VertexCoordinate, textColor, haloColor);
                }
            }
        }

        // ── Graphics Primitives ───────────────────────────────────────────────────

        private void AddLine(MapPoint from, MapPoint to, CIMLineSymbol sym)
        {
            try
            {
                var sr = from.SpatialReference ?? to.SpatialReference ?? _mapView.Map?.SpatialReference;
                var geom = PolylineBuilderEx.CreatePolyline(new[] { from, to }, sr);
                var mapSr = _mapView.Map?.SpatialReference;
                if (mapSr != null && sr != null && !sr.IsEqual(mapSr))
                {
                    try
                    {
                        geom = GeometryEngine.Instance.Project(geom, mapSr) as Polyline ?? geom;
                    }
                    catch { }
                }

                var handle = _mapView.AddOverlay(geom, sym.MakeSymbolReference());
                if (handle != null)
                {
                    lock (_handles) _handles.Add(handle);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] GeoMetricsOverlayManager.AddLine error: {ex.Message}");
            }
        }

        private void AddSlashTick(MapPoint offsetPt, MapPoint segA, MapPoint segB, double halfLenMeters, CIMLineSymbol sym)
        {
            double dx = segB.X - segA.X, dy = segB.Y - segA.Y;
            var sr = offsetPt.SpatialReference ?? segA.SpatialReference;
            if (sr != null && sr.IsGeographic)
            {
                double midLatRad = (((segA.Y + segB.Y) / 2.0) * Math.PI) / 180.0;
                double cosLat = Math.Max(0.01, Math.Cos(midLatRad));
                dx *= cosLat;
            }

            double len = Math.Sqrt((dx * dx) + (dy * dy));
            if (len < 1e-12) return;

            double ux = dx / len, uy = dy / len;
            var (nx, ny) = DimensionLabelManager.GetOutwardNormal(segA, segB);

            double txMeters = (ux + nx) * 0.7071067811865476 * halfLenMeters;
            double tyMeters = (uy + ny) * 0.7071067811865476 * halfLenMeters;

            var (txMap, tyMap) = DimensionLabelManager.MetersToMapDelta(txMeters, tyMeters, sr, offsetPt.Y);

            var t1 = MapPointBuilderEx.CreateMapPoint(offsetPt.X + txMap, offsetPt.Y + tyMap, sr);
            var t2 = MapPointBuilderEx.CreateMapPoint(offsetPt.X - txMap, offsetPt.Y - tyMap, sr);
            AddLine(t1, t2, sym);
        }

        private void PlaceText(
            MapPoint at, string text, double angleDeg,
            DimensionItemRole role,
            CIMColor textColor,
            CIMColor haloColor)
        {
            if (at == null || string.IsNullOrWhiteSpace(text)) return;

            try
            {
                var mapSr = _mapView.Map?.SpatialReference;
                var ptSr = at.SpatialReference ?? mapSr;
                var ptToDraw = (mapSr != null && ptSr != null && !ptSr.IsEqual(mapSr))
                    ? (GeometryEngine.Instance.Project(at, mapSr) as MapPoint ?? at)
                    : at;

                if (ptToDraw.SpatialReference == null && mapSr != null)
                {
                    ptToDraw = MapPointBuilderEx.CreateMapPoint(ptToDraw.X, ptToDraw.Y, mapSr);
                }

                var (fontSize, bold) = role switch
                {
                    DimensionItemRole.Area             => (11.5, true),
                    DimensionItemRole.Segment          => (10.0, true),
                    DimensionItemRole.Perimeter        => (9.5,  false),
                    DimensionItemRole.VertexAngle      => (9.0,  false),
                    DimensionItemRole.VertexCoordinate => (8.5,  true),
                    DimensionItemRole.Qc               => (9.5,  true),
                    _                                  => (9.5,  false)
                };

                var sym = SymbolFactory.Instance.ConstructTextSymbol(
                    textColor, fontSize, "Segoe UI", bold ? "Bold" : "Regular");
                sym.Angle               = angleDeg;
                sym.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Center;
                sym.VerticalAlignment   = ArcGIS.Core.CIM.VerticalAlignment.Center;
                sym.HaloSize            = 2.2;
                sym.HaloSymbol          = SymbolFactory.Instance.ConstructPolygonSymbol(haloColor);

                var textGraphic = new CIMTextGraphic
                {
                    Text   = text,
                    Symbol = sym.MakeSymbolReference(),
                    Shape  = ptToDraw
                };

                var handle = _mapView.AddOverlay(textGraphic);
                if (handle != null)
                {
                    lock (_handles) _handles.Add(handle);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] GeoMetricsOverlayManager.PlaceText error: {ex.Message}");
            }
        }

        // ── Style & Color Resolvers ────────────────────────────────────────────────

        private static (CIMColor text, CIMColor halo) ResolveTextAndHaloColor(DimensionSettings s)
        {
            var text = s.TextColor switch
            {
                TextColorOption.Blue   => (CIMColor)ColorFactory.Instance.CreateRGBColor(0, 70, 200),
                TextColorOption.Red    => (CIMColor)ColorFactory.Instance.CreateRGBColor(210, 20, 20),
                TextColorOption.Green  => (CIMColor)ColorFactory.Instance.CreateRGBColor(0, 130, 40),
                TextColorOption.Orange => (CIMColor)ColorFactory.Instance.CreateRGBColor(240, 90, 0),
                TextColorOption.White  => (CIMColor)ColorFactory.Instance.WhiteRGB,
                TextColorOption.Yellow => (CIMColor)ColorFactory.Instance.CreateRGBColor(255, 220, 0),
                TextColorOption.Cyan   => (CIMColor)ColorFactory.Instance.CreateRGBColor(0, 210, 230),
                _                      => (CIMColor)ColorFactory.Instance.BlackRGB
            };

            var halo = (s.TextColor == TextColorOption.White || s.TextColor == TextColorOption.Yellow || s.TextColor == TextColorOption.Cyan)
                ? (CIMColor)ColorFactory.Instance.BlackRGB
                : (CIMColor)ColorFactory.Instance.WhiteRGB;

            return (text, halo);
        }

        private static CIMColor StyleLineColor(DimensionSettings s)
        {
            return s.DimensionStyle switch
            {
                DimensionStyleOption.High_Contrast => ColorFactory.Instance.CreateRGBColor(0, 220, 220),
                DimensionStyleOption.Minimal       => ColorFactory.Instance.CreateRGBColor(120, 120, 120),
                _                                  => ColorFactory.Instance.CreateRGBColor(255, 100, 0)
            };
        }

        public void Dispose() => Clear();
    }
}

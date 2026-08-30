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
    /// Owns all temporary <see cref="MapView"/> overlay handles.
    ///
    /// CRITICAL THREADING RULE:
    ///   All SymbolFactory, ColorFactory, GeometryBuilderEx, and MapView.AddOverlay
    ///   calls MUST execute inside QueuedTask (MCT thread).
    /// </summary>
    internal sealed class DimensionRenderer : IDisposable
    {
        private readonly MapView _mapView;
        private readonly List<IDisposable> _handles = new();

        private const double FarScale = 50_000;
        private const double MedScale = 10_000;

        public DimensionRenderer(MapView mapView)
        {
            _mapView = mapView ?? throw new ArgumentNullException(nameof(mapView));
        }

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
        }

        // ── Main Draw API (Executed inside QueuedTask / MCT thread) ───────────────

        public void DrawPolygon(PolygonMeasurementResult result, DimensionSettings settings, double mapScale)
        {
            Clear();
            if (result == null || _mapView == null) return;

            double mpp = DimensionLabelManager.GetMetersPerPixel(mapScale);

            bool farZoom = mapScale > FarScale;
            bool medZoom = !farZoom && mapScale > MedScale;

            int drawnGraphics = 0;
            var (textColor, haloColor) = ResolveTextAndHaloColor(settings);

            // 1. Area Label (Placed at interior label point)
            if (settings.ShowArea && result.InteriorLabelPoint != null)
            {
                string areaText = UnitConverter.FormatArea(
                    result.DisplayArea, settings.Precision, result.AreaUnitAbbrev);
                PlaceText(result.InteriorLabelPoint, areaText, 0, TextRole.Area, textColor, haloColor);
                drawnGraphics++;
            }

            if (farZoom)
            {
                Trace.WriteLine($"[DIM] Far zoom ({mapScale:F0}): Only area label rendered.");
                return;
            }

            // 2. Segment Dimension Labels & Lines
            if (settings.ShowSegmentLength && result.Segments != null && result.Segments.Count > 0)
            {
                IEnumerable<SegmentMeasurement> segs = result.Segments;

                if (medZoom)
                {
                    int take = Math.Max(4, result.Segments.Count / 3);
                    segs = result.Segments
                        .OrderByDescending(s => s.NativeLength)
                        .Take(take);
                }

                foreach (var seg in segs)
                {
                    DrawSegment(seg, settings, mpp, textColor, haloColor);
                    drawnGraphics++;
                }
            }

            // 3. Perimeter Label
            if (settings.ShowPerimeter && result.InteriorLabelPoint != null)
            {
                string text = (settings.DimensionStyle == DimensionStyleOption.Numbers_Only)
                    ? UnitConverter.FormatLength(result.DisplayPerimeter, settings.Precision, result.LinearUnitAbbrev)
                    : "P: " + UnitConverter.FormatLength(result.DisplayPerimeter, settings.Precision, result.LinearUnitAbbrev);

                var (pDx, pDy) = DimensionLabelManager.MetersToMapDelta(0, -14.0 * mpp, result.InteriorLabelPoint.SpatialReference, result.InteriorLabelPoint.Y);
                var pt = MapPointBuilderEx.CreateMapPoint(
                    result.InteriorLabelPoint.X + pDx,
                    result.InteriorLabelPoint.Y + pDy,
                    result.InteriorLabelPoint.SpatialReference);

                PlaceText(pt, text, 0, TextRole.Perimeter, textColor, haloColor);
                drawnGraphics++;
            }

            // 4. Vertex Angles
            if (settings.ShowVertexAngles && result.VertexAngles != null)
            {
                foreach (var va in result.VertexAngles)
                {
                    DrawVertexAngle(va, settings, mpp, textColor, haloColor);
                    drawnGraphics++;
                }
            }

            // 5. QC Panel
            if ((settings.ShowAreaDifference || settings.ShowToleranceStatus) &&
                 result.InteriorLabelPoint != null &&
                (result.OriginalDisplayArea.HasValue || result.WithinTolerance.HasValue))
            {
                DrawQcPanel(result, settings, mpp, textColor, haloColor);
                drawnGraphics++;
            }

            Trace.WriteLine($"[DIM] DimensionRenderer: {drawnGraphics} elements added to MapView overlay (scale 1:{mapScale:F0}).");
        }

        public void DrawPolyline(PolylineMeasurementResult result, DimensionSettings settings, double mapScale)
        {
            Clear();
            if (result == null || _mapView == null) return;

            double mpp = DimensionLabelManager.GetMetersPerPixel(mapScale);
            if (mapScale > FarScale) return;

            var (textColor, haloColor) = ResolveTextAndHaloColor(settings);

            if (settings.ShowSegmentLength && result.Segments != null)
            {
                foreach (var seg in result.Segments)
                    DrawSegment(seg, settings, mpp, textColor, haloColor);
            }

            if (settings.ShowVertexAngles && result.VertexAngles != null)
            {
                foreach (var va in result.VertexAngles)
                    DrawVertexAngle(va, settings, mpp, textColor, haloColor);
            }
        }

        private void DrawVertexAngle(
            VertexAngleMeasurement va,
            DimensionSettings settings,
            double mpp,
            CIMColor textColor,
            CIMColor haloColor)
        {
            var v = va.Vertex;
            var a = va.PrevPoint;
            var b = va.NextPoint;
            if (v == null || a == null || b == null) return;

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

            double l1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            double l2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

            MapPoint labelPt = v;
            if (l1 > 1e-7 && l2 > 1e-7)
            {
                double u1x = dx1 / l1, u1y = dy1 / l1;
                double u2x = dx2 / l2, u2y = dy2 / l2;
                double bx = u1x + u2x, by = u1y + u2y;
                double blen = Math.Sqrt(bx * bx + by * by);
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

            string text = UnitConverter.FormatAngle(va.AngleDegrees, settings.Precision);
            PlaceText(labelPt, text, 0, TextRole.VertexAngle, textColor, haloColor);
        }

        // ── Segment Dimension (CAD vs Numbers Only) ────────────────────────────────

        private void DrawSegment(
            SegmentMeasurement seg,
            DimensionSettings settings,
            double mpp,
            CIMColor textColor,
            CIMColor haloColor)
        {
            var a = seg.Start;
            var b = seg.End;
            if (a == null || b == null) return;

            var (nx, ny)  = DimensionLabelManager.GetOutwardNormal(a, b);
            double offsetMeters = Math.Max(6.0, settings.OffsetPixels) * mpp;

            // ── Style: Numbers Only (Clean Text, No Dimension Lines) ───────────────
            if (settings.DimensionStyle == DimensionStyleOption.Numbers_Only)
            {
                // Place text neatly offset from the segment midpoint
                var midOffset = DimensionLabelManager.GetOffsetMidpoint(a, b, offsetMeters);
                double angleDeg = DimensionLabelManager.GetLabelAngle(a, b);

                string text = UnitConverter.FormatLength(
                    seg.DisplayLength, settings.Precision, seg.UnitAbbrev);
                if (seg.Bearing.HasValue)
                    text += $"  ({UnitConverter.FormatBearing(seg.Bearing.Value)})";

                PlaceText(midOffset, text, angleDeg, TextRole.Segment, textColor, haloColor);
                return;
            }

            // ── Style: CAD Standard / Minimal / High Contrast ─────────────────────
            double gapMeters    = 3.0 * mpp;
            double overhgMeters = 4.0 * mpp;
            double tickMeters   = 5.0 * mpp;

            var aSr = a.SpatialReference;
            var bSr = b.SpatialReference;

            var (aOffDx, aOffDy) = DimensionLabelManager.MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, aSr, a.Y);
            var (bOffDx, bOffDy) = DimensionLabelManager.MetersToMapDelta(nx * offsetMeters, ny * offsetMeters, bSr, b.Y);

            var aOff = MapPointBuilderEx.CreateMapPoint(a.X + aOffDx, a.Y + aOffDy, aSr);
            var bOff = MapPointBuilderEx.CreateMapPoint(b.X + bOffDx, b.Y + bOffDy, bSr);

            var lineColor = StyleLineColor(settings);
            var dimSym = SymbolFactory.Instance.ConstructLineSymbol(lineColor, 1.4);
            var extSym = SymbolFactory.Instance.ConstructLineSymbol(lineColor, 0.8);

            // 1. Dimension line A' ── B'
            AddLine(aOff, bOff, dimSym);

            // 2. Extension lines (skipped in Minimal style)
            if (settings.DimensionStyle != DimensionStyleOption.Minimal)
            {
                var (e1f, e1t) = DimensionLabelManager.GetExtensionLine(a, gapMeters, overhgMeters, offsetMeters, nx, ny);
                var (e2f, e2t) = DimensionLabelManager.GetExtensionLine(b, gapMeters, overhgMeters, offsetMeters, nx, ny);
                AddLine(e1f, e1t, extSym);
                AddLine(e2f, e2t, extSym);
            }

            // 3. Diagonal slash ticks at endpoints
            AddSlashTick(aOff, a, b, tickMeters, dimSym);
            AddSlashTick(bOff, a, b, tickMeters, dimSym);

            // 4. Dimension text label at midpoint
            var mid = DimensionLabelManager.GetOffsetMidpoint(aOff, bOff, 0);
            double ang = DimensionLabelManager.GetLabelAngle(a, b);

            string label = UnitConverter.FormatLength(
                seg.DisplayLength, settings.Precision, seg.UnitAbbrev);
            if (seg.Bearing.HasValue)
                label += $"  ({UnitConverter.FormatBearing(seg.Bearing.Value)})";

            PlaceText(mid, label, ang, TextRole.Segment, textColor, haloColor);
        }

        // ── QC Panel ──────────────────────────────────────────────────────────────

        private void DrawQcPanel(
            PolygonMeasurementResult result,
            DimensionSettings settings,
            double mpp,
            CIMColor textColor,
            CIMColor haloColor)
        {
            var lines = new List<string>();

            if (settings.ShowAreaDifference && result.OriginalDisplayArea.HasValue)
            {
                double orig = result.OriginalDisplayArea.Value;
                double curr = result.DisplayArea;
                double diff = curr - orig;
                double pct  = orig > 0 ? (diff / orig) * 100.0 : 0;
                string sign = diff >= 0 ? "+" : "";

                lines.Add("Orig: " + UnitConverter.FormatArea(orig, settings.Precision, result.AreaUnitAbbrev));
                lines.Add("Curr: " + UnitConverter.FormatArea(curr, settings.Precision, result.AreaUnitAbbrev));
                lines.Add($"\u0394 {sign}{UnitConverter.FormatArea(Math.Abs(diff), settings.Precision, result.AreaUnitAbbrev)} ({sign}{pct:F1}%)");
            }

            if (settings.ShowToleranceStatus && result.WithinTolerance.HasValue)
                lines.Add(result.WithinTolerance.Value ? "\u2713 Within Tolerance" : "\u26A0 Exceeds Tolerance");

            if (lines.Count == 0) return;

            double offsetY = (settings.ShowPerimeter ? 30.0 : 16.0) * mpp;
            var (qcDx, qcDy) = DimensionLabelManager.MetersToMapDelta(0, -offsetY, result.InteriorLabelPoint.SpatialReference, result.InteriorLabelPoint.Y);
            var pt = MapPointBuilderEx.CreateMapPoint(
                result.InteriorLabelPoint.X + qcDx,
                result.InteriorLabelPoint.Y + qcDy,
                result.InteriorLabelPoint.SpatialReference);

            PlaceText(pt, string.Join("\n", lines), 0, TextRole.Qc, textColor, haloColor);
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
                Trace.WriteLine($"[DIM] AddLine error: {ex.Message}");
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

        // ── Text Placement ─────────────────────────────────────────────────────────

        private enum TextRole { Area, Segment, Perimeter, VertexAngle, Qc }

        private void PlaceText(
            MapPoint at, string text, double angleDeg,
            TextRole role,
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
                    TextRole.Area        => (11.5, true),
                    TextRole.Segment     => (10.0, true),
                    TextRole.Perimeter   => (9.5,  false),
                    TextRole.VertexAngle => (9.0,  false),
                    TextRole.Qc          => (9.5,  true),
                    _                    => (9.5,  false)
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
                Trace.WriteLine($"[DIM] PlaceText error: {ex.Message}");
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

            // If text is white or yellow, use black halo for high contrast; otherwise white halo
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


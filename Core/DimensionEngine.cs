using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DimensionOverlay.Measurement;
using DimensionOverlay.Rendering;
using DimensionOverlay.Utilities;

namespace DimensionOverlay.Core
{
    /// <summary>
    /// PASSIVE real-time dimension monitoring engine.
    ///
    /// ARCHITECTURE:
    /// - Purely event-driven (SketchModifiedEvent, MapViewCameraChangedEvent, MapSelectionChangedEvent).
    /// - Fully exception-isolated: zero exceptions ever escape to ArcGIS Pro.
    /// - Lock-free latest-frame coalescing ensures smooth 60 FPS performance during vertex dragging.
    /// - Viewport changes reuse cached measurements without recalculating geometry.
    /// - Automatically excludes HTTP/HTTPS service layers.
    /// </summary>
    internal sealed class DimensionEngine : IDisposable
    {
        private bool _isEnabled;
        private bool _disposed;
        private bool _isSketchActive;

        private DimensionOverlayManager _overlayManager;
        private MapView _overlayView;

        private int _lastSelectionHash;

        // Frame-coalescing state for live vertex drag (lock-free)
        private volatile Geometry _latestSketchGeometry;
        private volatile MapView _latestSketchMapView;
        private int _isProcessingLiveSketch;

        // Frame-coalescing state for camera navigation (pan/zoom)
        private volatile MapView _latestCameraMapView;
        private int _isProcessingCameraChange;

        public DimensionEngine()
        {
            Trace.WriteLine("[DIM] DimensionEngine created (Pure event-driven mode).");
        }

        // ── Enable / Disable ──────────────────────────────────────────────────────

        public void Enable()
        {
            try
            {
                if (_isEnabled) return;
                _isEnabled = true;
                _isSketchActive = false;
                _lastSelectionHash = 0;
                Trace.WriteLine("[DIM] Overlay Enabled = true");
                RefreshSelectionDisplay();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] Enable error: {ex.Message}");
            }
        }

        public void Disable()
        {
            try
            {
                if (!_isEnabled) return;
                _isEnabled = false;
                _isSketchActive = false;
                _lastSelectionHash = 0;
                ClearOverlays();
                Trace.WriteLine("[DIM] Overlay Enabled = false");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] Disable error: {ex.Message}");
            }
        }

        public bool IsEnabled => _isEnabled;

        // ── Primary Live Update (Continuous Vertex Dragging) ───────────────────────

        public void OnLiveSketchModified(Geometry currentSketch, MapView mapView, bool isUndo)
        {
            try
            {
                if (!_isEnabled || _disposed || currentSketch == null || currentSketch.IsEmpty || mapView == null)
                    return;

                var settings = Module1.Current?.Settings;
                if (settings == null) return;

                if (settings.TargetLayer != null && LayerHelper.IsHttpServiceLayer(settings.TargetLayer))
                {
                    return;
                }

                _isSketchActive = true;
                _latestSketchGeometry = currentSketch;
                _latestSketchMapView = mapView;

                if (Interlocked.CompareExchange(ref _isProcessingLiveSketch, 1, 0) == 0)
                {
                    ProcessLatestSketchAsync();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnLiveSketchModified error: {ex.Message}");
            }
        }

        private async void ProcessLatestSketchAsync()
        {
            try
            {
                while (_isEnabled && !_disposed)
                {
                    var sketch = _latestSketchGeometry;
                    var view = _latestSketchMapView;
                    if (sketch == null || view == null) break;

                    _latestSketchGeometry = null;

                    await QueuedTask.Run(() =>
                    {
                        try
                        {
                            if (!_isEnabled || _disposed) return;
                            EnsureOverlayManager(view);
                            var settings = Module1.Current?.Settings;
                            if (settings == null) return;

                            var sr = sketch.SpatialReference;
                            if (sr != null)
                            {
                                settings.CrsName = sr.Name ?? "Unknown";
                                settings.Wkid = sr.Wkid;
                                bool geo = GeometryMeasurementService.ResolveGeodesic(settings.Method, sr);
                                settings.ResolvedMethod = geo ? "Geodesic" : "Planar";
                            }

                            if (sketch is Polygon poly)
                            {
                                var polyResult = GeometryMeasurementService.MeasurePolygon(poly, settings);
                                _overlayManager.UpdateFromPolygon(polyResult, settings);
                            }
                            else if (sketch is Polyline line)
                            {
                                var lineResult = GeometryMeasurementService.MeasurePolyline(line, settings);
                                _overlayManager.UpdateFromPolyline(lineResult, settings);
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[DIM] ProcessLatestSketch inner error: {ex.Message}");
                        }
                    });

                    if (_latestSketchGeometry == null)
                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] ProcessLatestSketchAsync outer error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingLiveSketch, 0);

                if (_latestSketchGeometry != null && _isEnabled && !_disposed)
                {
                    if (Interlocked.CompareExchange(ref _isProcessingLiveSketch, 1, 0) == 0)
                    {
                        ProcessLatestSketchAsync();
                    }
                }
            }
        }

        public void OnSketchFinished()
        {
            try
            {
                _isSketchActive = false;
                _lastSelectionHash = 0;
                _latestSketchGeometry = null;
                Task.Delay(150).ContinueWith(_ => RefreshSelectionDisplay());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnSketchFinished error: {ex.Message}");
            }
        }

        // ── Viewport / Camera Navigation Update (Zoom, Pan, Navigate) ─────────────

        public void OnViewpointChanged(MapView mapView)
        {
            try
            {
                if (!_isEnabled || _disposed || mapView == null) return;
                _latestCameraMapView = mapView;

                if (Interlocked.CompareExchange(ref _isProcessingCameraChange, 1, 0) == 0)
                {
                    ProcessLatestCameraChangeAsync();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnViewpointChanged error: {ex.Message}");
            }
        }

        private async void ProcessLatestCameraChangeAsync()
        {
            try
            {
                while (_isEnabled && !_disposed)
                {
                    var view = _latestCameraMapView;
                    if (view == null) break;
                    _latestCameraMapView = null;

                    await QueuedTask.Run(() =>
                    {
                        try
                        {
                            if (!_isEnabled || _disposed) return;

                            EnsureOverlayManager(view);
                            var settings = Module1.Current?.Settings;
                            if (settings == null) return;

                            if (_overlayManager.HasCachedData)
                            {
                                _overlayManager.UpdateForViewpoint(settings);
                            }
                            else
                            {
                                RefreshSelectionDisplayInternal(view);
                            }
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine($"[DIM] ProcessLatestCameraChange inner error: {ex.Message}");
                        }
                    });

                    if (_latestCameraMapView == null)
                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] ProcessLatestCameraChangeAsync outer error: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingCameraChange, 0);

                if (_latestCameraMapView != null && _isEnabled && !_disposed)
                {
                    if (Interlocked.CompareExchange(ref _isProcessingCameraChange, 1, 0) == 0)
                    {
                        ProcessLatestCameraChangeAsync();
                    }
                }
            }
        }

        // ── Situation A (Initial Display on Selection / Idle Observation) ─────────

        public void RefreshSelectionDisplay()
        {
            try
            {
                if (!_isEnabled || _disposed || _isSketchActive) return;
                var mapView = MapView.Active;
                if (mapView == null) return;

                QueuedTask.Run(() =>
                {
                    try
                    {
                        RefreshSelectionDisplayInternal(mapView);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[DIM] RefreshSelectionDisplay QueuedTask error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] RefreshSelectionDisplay error: {ex.Message}");
            }
        }

        private void RefreshSelectionDisplayInternal(MapView mapView)
        {
            if (!_isEnabled || _disposed || _isSketchActive || mapView == null) return;

            try
            {
                var map = mapView.Map;
                if (map == null) return;

                var settings = Module1.Current?.Settings;
                if (settings == null) return;

                FeatureLayer target = settings.TargetLayer;

                if (target == null)
                {
                    target = map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .Where(l => !LayerHelper.IsHttpServiceLayer(l))
                        .FirstOrDefault(l => (l.ShapeType == esriGeometryType.esriGeometryPolygon ||
                                             l.ShapeType == esriGeometryType.esriGeometryPolyline) &&
                                            (l.GetSelection()?.GetCount() ?? 0) > 0);
                }
                else if (LayerHelper.IsHttpServiceLayer(target))
                {
                    return;
                }

                if (target == null)
                {
                    if (_lastSelectionHash != 0)
                    {
                        _lastSelectionHash = 0;
                        _overlayManager?.Clear();
                    }
                    return;
                }

                var sel = target.GetSelection();
                if (sel == null || sel.GetCount() == 0)
                {
                    if (_lastSelectionHash != 0)
                    {
                        _lastSelectionHash = 0;
                        _overlayManager?.Clear();
                    }
                    return;
                }

                var oids = sel.GetObjectIDs();
                if (oids == null || !oids.Any())
                {
                    if (_lastSelectionHash != 0)
                    {
                        _lastSelectionHash = 0;
                        _overlayManager?.Clear();
                    }
                    return;
                }

                long firstOid = oids.First();
                using var cursor = sel.Search(null);
                if (cursor != null && cursor.MoveNext() && cursor.Current is Feature feat)
                {
                    var selectedGeom = feat.GetShape();
                    if (selectedGeom != null && !selectedGeom.IsEmpty)
                    {
                        int hash = (int)(firstOid * 397) ^ (selectedGeom is Multipart mp ? mp.PointCount : 0);
                        if (hash != _lastSelectionHash || !_overlayManager.HasCachedData)
                        {
                            _lastSelectionHash = hash;
                            RenderGeometryDirectInternal(selectedGeom, mapView);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] RefreshSelectionDisplayInternal error: {ex.Message}");
            }
        }

        private void RenderGeometryDirectInternal(Geometry geom, MapView mapView)
        {
            var settings = Module1.Current?.Settings;
            if (settings == null || geom == null || geom.IsEmpty || mapView == null) return;

            try
            {
                EnsureOverlayManager(mapView);

                var sr = geom.SpatialReference;
                if (sr != null)
                {
                    settings.CrsName = sr.Name ?? "Unknown";
                    settings.Wkid = sr.Wkid;
                    bool geo = GeometryMeasurementService.ResolveGeodesic(settings.Method, sr);
                    settings.ResolvedMethod = geo ? "Geodesic" : "Planar";
                }

                if (geom is Polygon poly)
                {
                    var polyResult = GeometryMeasurementService.MeasurePolygon(poly, settings);
                    _overlayManager.UpdateFromPolygon(polyResult, settings);
                }
                else if (geom is Polyline line)
                {
                    var lineResult = GeometryMeasurementService.MeasurePolyline(line, settings);
                    _overlayManager.UpdateFromPolyline(lineResult, settings);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] RenderGeometryDirectInternal error: {ex.Message}");
            }
        }

        // ── Overlay Cleanup ───────────────────────────────────────────────────────

        public void ClearOverlays()
        {
            _lastSelectionHash = 0;
            _isSketchActive = false;
            _latestSketchGeometry = null;
            _latestCameraMapView = null;

            try
            {
                QueuedTask.Run(() =>
                {
                    try { _overlayManager?.Clear(); } catch { }
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] ClearOverlays error: {ex.Message}");
            }
        }

        private void EnsureOverlayManager(MapView view)
        {
            if (_overlayManager == null || _overlayView != view)
            {
                try { _overlayManager?.Dispose(); } catch { }
                _overlayManager = new DimensionOverlayManager(view);
                _overlayView = view;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _latestSketchGeometry = null;
            _latestCameraMapView = null;

            try
            {
                QueuedTask.Run(() =>
                {
                    try { _overlayManager?.Dispose(); } catch { }
                    _overlayManager = null;
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] Dispose error: {ex.Message}");
            }
            Trace.WriteLine("[DIM] DimensionEngine disposed.");
        }
    }
}

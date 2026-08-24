using System;
using System.Diagnostics;
using ArcGIS.Core.Events;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;

namespace DimensionOverlay.Core
{
    /// <summary>
    /// Subscribes directly to ArcGIS Pro mapping, camera, and sketch events.
    /// All event handlers are guarded with try/catch to ensure absolute host application stability.
    /// </summary>
    internal sealed class EditingMonitor
    {
        private readonly DimensionEngine _engine;

        private SubscriptionToken _sketchModifiedToken;
        private SubscriptionToken _sketchCompletedToken;
        private SubscriptionToken _sketchCanceledToken;
        private SubscriptionToken _selectionChangedToken;
        private SubscriptionToken _mapViewChangedToken;
        private SubscriptionToken _cameraChangedToken;

        public EditingMonitor(DimensionEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public void Start()
        {
            try
            {
                // Primary live event: fires on every vertex move
                _sketchModifiedToken   = SketchModifiedEvent.Subscribe(OnSketchModified);
                _sketchCompletedToken  = SketchCompletedEvent.Subscribe(OnSketchCompleted);
                _sketchCanceledToken   = SketchCanceledEvent.Subscribe(OnSketchCanceled);

                // Viewport & camera navigation events (Zoom, Pan, Rotate)
                _cameraChangedToken    = MapViewCameraChangedEvent.Subscribe(OnMapViewCameraChanged);

                // Initial display & context change events
                _selectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
                _mapViewChangedToken   = ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);

                Trace.WriteLine("[DIM] EditingMonitor: All events subscribed successfully.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] EditingMonitor.Start error: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                Unsubscribe(ref _sketchModifiedToken,   SketchModifiedEvent.Unsubscribe);
                Unsubscribe(ref _sketchCompletedToken,  SketchCompletedEvent.Unsubscribe);
                Unsubscribe(ref _sketchCanceledToken,   SketchCanceledEvent.Unsubscribe);
                Unsubscribe(ref _cameraChangedToken,    MapViewCameraChangedEvent.Unsubscribe);
                Unsubscribe(ref _selectionChangedToken, MapSelectionChangedEvent.Unsubscribe);
                Unsubscribe(ref _mapViewChangedToken,   ActiveMapViewChangedEvent.Unsubscribe);

                Trace.WriteLine("[DIM] EditingMonitor: Unsubscribed from all events.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] EditingMonitor.Stop error: {ex.Message}");
            }
        }

        // ── Primary Live Editing Event Handler ────────────────────────────────────

        private void OnSketchModified(SketchModifiedEventArgs args)
        {
            try
            {
                if (args == null || args.IsUndo) return;

                var currentSketch = args.CurrentSketch;
                if (currentSketch == null || currentSketch.IsEmpty) return;

                var mapView = args.MapView ?? MapView.Active;
                if (mapView == null) return;

                _engine.OnLiveSketchModified(currentSketch, mapView, args.IsUndo);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnSketchModified error: {ex.Message}");
            }
        }

        private void OnSketchCompleted(SketchCompletedEventArgs args)
        {
            try
            {
                Trace.WriteLine("[DIM] SketchCompletedEvent received.");
                _engine.OnSketchFinished();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnSketchCompleted error: {ex.Message}");
            }
        }

        private void OnSketchCanceled(SketchCanceledEventArgs args)
        {
            try
            {
                Trace.WriteLine("[DIM] SketchCanceledEvent received.");
                _engine.OnSketchFinished();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnSketchCanceled error: {ex.Message}");
            }
        }

        // ── Camera Navigation (Zoom, Pan) Handler ─────────────────────────────────

        private void OnMapViewCameraChanged(MapViewCameraChangedEventArgs args)
        {
            try
            {
                var mapView = args?.MapView ?? MapView.Active;
                if (mapView == null) return;

                _engine.OnViewpointChanged(mapView);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnMapViewCameraChanged error: {ex.Message}");
            }
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            try
            {
                _engine.RefreshSelectionDisplay();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnMapSelectionChanged error: {ex.Message}");
            }
        }

        private void OnActiveMapViewChanged(ActiveMapViewChangedEventArgs args)
        {
            try
            {
                _engine.RefreshSelectionDisplay();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] OnActiveMapViewChanged error: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void Unsubscribe(ref SubscriptionToken token, Action<SubscriptionToken> unsub)
        {
            if (token == null) return;
            try { unsub(token); } catch { }
            token = null;
        }
    }
}

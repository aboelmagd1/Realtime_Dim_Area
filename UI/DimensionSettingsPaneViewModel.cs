using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using DimensionOverlay.Models;
using DimensionOverlay.Utilities;

namespace DimensionOverlay.UI
{
    /// <summary>
    /// ViewModel for the Settings Dock Pane.
    /// Exposes the shared DimensionSettings to the XAML view and
    /// keeps the layer combo box in sync with the active map (excluding HTTP/HTTPS service layers).
    /// Thread-safe: all layer inspections run on QueuedTask and update the UI collection safely.
    /// </summary>
    internal class DimensionSettingsPaneViewModel : DockPane
    {
        private const string PaneId = "DimensionOverlay_UI_DimensionSettingsPane";

        protected DimensionSettingsPaneViewModel()
        {
            RefreshLayersAsync();
            ActiveMapViewChangedEvent.Subscribe(_ => RefreshLayersAsync());
            LayersAddedEvent.Subscribe(_ => RefreshLayersAsync());
            LayersRemovedEvent.Subscribe(_ => RefreshLayersAsync());
        }

        internal static void Show()
        {
            try
            {
                var pane = FrameworkApplication.DockPaneManager.Find(PaneId);
                if (pane == null) return;
                pane.Activate();
            }
            catch { }
        }

        // ── Bindings ──────────────────────────────────────────────────────────────

        /// <summary>Shared settings singleton — all controls bind directly to this.</summary>
        public DimensionSettings Settings => Module1.Current?.Settings;

        /// <summary>Feature layers (polygon + polyline) in the active map, excluding web services.</summary>
        public ObservableCollection<FeatureLayer> Layers { get; } = new();

        public MeasurementMethod[] Methods { get; } =
        {
            MeasurementMethod.Automatic,
            MeasurementMethod.Planar,
            MeasurementMethod.Geodesic
        };

        public DisplayUnitOption[] Units { get; } =
        {
            DisplayUnitOption.LayerNative,
            DisplayUnitOption.Meters,
            DisplayUnitOption.Feet,
            DisplayUnitOption.US_Survey_Feet,
            DisplayUnitOption.Kilometers,
            DisplayUnitOption.Miles
        };

        public DimensionStyleOption[] Styles { get; } =
        {
            DimensionStyleOption.CAD_Standard,
            DimensionStyleOption.Numbers_Only,
            DimensionStyleOption.Minimal,
            DimensionStyleOption.High_Contrast
        };

        public TextColorOption[] TextColors { get; } =
        {
            TextColorOption.Black,
            TextColorOption.Blue,
            TextColorOption.Red,
            TextColorOption.Green,
            TextColorOption.Orange,
            TextColorOption.White,
            TextColorOption.Yellow,
            TextColorOption.Cyan
        };

        // ── Helpers ───────────────────────────────────────────────────────────────

        private async void RefreshLayersAsync()
        {
            try
            {
                var validLayers = await QueuedTask.Run<List<FeatureLayer>>(() =>
                {
                    try
                    {
                        var map = MapView.Active?.Map;
                        if (map == null) return new List<FeatureLayer>();

                        return map.GetLayersAsFlattenedList()
                            .OfType<FeatureLayer>()
                            .Where(l => (l.ShapeType == esriGeometryType.esriGeometryPolygon ||
                                         l.ShapeType == esriGeometryType.esriGeometryPolyline) &&
                                        !LayerHelper.IsHttpServiceLayer(l))
                            .ToList();
                    }
                    catch
                    {
                        return new List<FeatureLayer>();
                    }
                });

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Layers.Clear();
                    if (validLayers != null)
                    {
                        foreach (var layer in validLayers)
                        {
                            Layers.Add(layer);
                        }
                    }
                });
            }
            catch
            {
                // Ignore any UI dispatch errors during pane initialization or teardown
            }
        }
    }
}

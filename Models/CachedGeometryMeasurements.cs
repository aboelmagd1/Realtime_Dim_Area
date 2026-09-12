using System.Collections.Generic;
using ArcGIS.Core.Geometry;
using GeoMetrics.Measurement;

namespace GeoMetrics.Models
{
    /// <summary>
    /// Holds the cached geometric measurements calculated from a sketch or selected feature.
    /// This structure is retained across viewport navigation (pan, zoom) so geometric measurements
    /// are not recalculated when the camera changes.
    /// </summary>
    public class CachedGeometryMeasurements
    {
        public GeometryType GeometryType { get; set; }

        public SpatialReference SpatialReference { get; set; }

        public Polygon SourcePolygon
        {
            get => SourcePolygons.Count > 0 ? SourcePolygons[0] : null;
            set
            {
                SourcePolygons.Clear();
                if (value != null) SourcePolygons.Add(value);
            }
        }

        public Polyline SourcePolyline
        {
            get => SourcePolylines.Count > 0 ? SourcePolylines[0] : null;
            set
            {
                SourcePolylines.Clear();
                if (value != null) SourcePolylines.Add(value);
            }
        }

        public List<Polygon> SourcePolygons { get; } = new();

        public List<Polyline> SourcePolylines { get; } = new();

        public int FeatureCount { get; set; }

        public PolygonMeasurementResult PolygonResult { get; set; }

        public PolylineMeasurementResult PolylineResult { get; set; }

        public List<DimensionItem> Items { get; } = new();
        public List<MapPoint> Vertices { get; } = new();

        public Envelope Extent { get; set; }

        public void Clear()
        {
            SourcePolygons.Clear();
            SourcePolylines.Clear();
            PolygonResult = null;
            PolylineResult = null;
            Items.Clear();
            Vertices.Clear();
            Extent = null;
            FeatureCount = 0;
        }

        public bool HasData => Items.Count > 0;
    }
}

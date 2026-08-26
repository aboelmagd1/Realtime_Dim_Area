using ArcGIS.Core.Geometry;

namespace DimensionOverlay.Models
{
    public enum DimensionItemRole
    {
        Area = 1,
        Segment = 2,
        Perimeter = 3,
        Qc = 4,
        VertexAngle = 5
    }

    /// <summary>
    /// Represents an individual viewport-aware dimension item (segment dimension, area label, etc.)
    /// Keeps measurement value and geometry in map coordinates while tracking viewport-dependent visibility and layout.
    /// </summary>
    public class DimensionItem
    {
        public DimensionItemRole Role { get; set; } = DimensionItemRole.Segment;

        public double Value { get; set; }

        public string DisplayText { get; set; }

        public MapPoint StartPoint { get; set; }

        public MapPoint EndPoint { get; set; }

        public MapPoint AnchorPoint { get; set; }

        public double Angle { get; set; }

        public bool IsVisible { get; set; } = true;

        public int Priority => (int)Role;

        public double? Bearing { get; set; }

        public string UnitAbbrev { get; set; }

        public int SegmentIndex { get; set; }
    }
}

using System.Collections.Generic;
using ArcGIS.Core.Geometry;

namespace GeoMetrics.Models
{
    /// <summary>One measured segment of the sketch (polygon ring or polyline part).</summary>
    public class SegmentDimension
    {
        public MapPoint Start { get; set; }
        public MapPoint End { get; set; }
        public MapPoint OffsetStart { get; set; }
        public MapPoint OffsetEnd { get; set; }
        public double Length { get; set; }
        public double ConvertedLength { get; set; }
        public string DisplayUnitAbbrev { get; set; }
        /// <summary>Bearing in degrees, 0-360, clockwise from north. Null if not computed.</summary>
        public double? Bearing { get; set; }
        /// <summary>Interior angle at the End vertex, in degrees. Null if not computed / not applicable.</summary>
        public double? AngleAtEndVertex { get; set; }
    }

    /// <summary>
    /// Full snapshot of everything the graphic manager needs to draw for the
    /// currently-edited feature. Produced fresh on every sketch modification —
    /// cheap value object, no ArcGIS handles held longer than one redraw.
    /// </summary>
    public class DimensionResult
    {
        public List<SegmentDimension> Segments { get; set; } = new List<SegmentDimension>();
        public double? Area { get; set; }
        public double? ConvertedArea { get; set; }
        public double Perimeter { get; set; }
        public double ConvertedPerimeter { get; set; }
        public MapPoint InteriorLabelPoint { get; set; }

        public double? OriginalArea { get; set; }
        public double? ConvertedOriginalArea { get; set; }
        public double? AreaDifference =>
            (Area.HasValue && OriginalArea.HasValue) ? Area.Value - OriginalArea.Value : (double?)null;
        public double? ConvertedAreaDifference =>
            (ConvertedArea.HasValue && ConvertedOriginalArea.HasValue) ? ConvertedArea.Value - ConvertedOriginalArea.Value : (double?)null;
        public double? AreaDifferencePercent =>
            (AreaDifference.HasValue && OriginalArea.HasValue && OriginalArea.Value != 0)
                ? (AreaDifference.Value / OriginalArea.Value) * 100.0
                : (double?)null;

        public bool? WithinTolerance { get; set; }

        public string LinearUnitAbbreviation { get; set; } = "m";
        public string AreaUnitAbbreviation { get; set; } = "m\u00B2";
        public bool IsGeodesic { get; set; }
    }
}

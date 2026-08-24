using System;
using System.Diagnostics;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Mapping;

namespace DimensionOverlay.Utilities
{
    /// <summary>
    /// Utility methods for inspecting layers, data sources, and connection properties.
    /// Filters out HTTP/HTTPS service-based layers (e.g. FeatureServer, MapServer, WFS).
    /// Safe to call on MCT or UI thread (defensive inspection).
    /// </summary>
    internal static class LayerHelper
    {
        /// <summary>
        /// Checks if a layer is backed by an HTTP/HTTPS web service.
        /// </summary>
        public static bool IsHttpServiceLayer(Layer layer)
        {
            if (layer == null) return false;

            try
            {
                // 1. Check URI / ConnectionURI
                string uri = layer.URI ?? string.Empty;
                if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("FeatureServer", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("MapServer", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("WFSServer", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("WMS", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 2. Check CIM Data Connection
                var dataConn = layer.GetDataConnection();
                if (dataConn != null)
                {
                    if (dataConn is CIMStandardDataConnection stdConn)
                    {
                        string connStr = stdConn.WorkspaceConnectionString ?? string.Empty;
                        if (connStr.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                            connStr.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
                            connStr.Contains("URL=", StringComparison.OrdinalIgnoreCase) ||
                            (connStr.Contains("SERVER=", StringComparison.OrdinalIgnoreCase) && connStr.Contains("http", StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    else if (dataConn is CIMFeatureDatasetDataConnection dsConn)
                    {
                        string ds = dsConn.FeatureDataset ?? string.Empty;
                        if (ds.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            ds.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] LayerHelper.IsHttpServiceLayer check error: {ex.Message}");
            }

            return false;
        }
    }
}

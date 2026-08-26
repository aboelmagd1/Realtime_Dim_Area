using System;
using System.Diagnostics;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Mapping;

namespace DimensionOverlay.Utilities
{
    /// <summary>
    /// Utility methods for inspecting layers, data sources, and connection properties.
    /// Accurately identifies ArcGIS service-based layers (FeatureServer, MapServer, Hosted Feature Layers,
    /// ArcGIS Online / Enterprise Portal services, WFS, WMS) while correctly classifying local
    /// File Geodatabase, Mobile Geodatabase, Shapefiles, and Enterprise Geodatabase direct connections.
    /// Safe to call on MCT or UI thread (defensive inspection).
    /// </summary>
    internal static class LayerHelper
    {
        /// <summary>
        /// Checks if a layer is backed by an ArcGIS web/remote service.
        /// </summary>
        public static bool IsServiceLayer(Layer layer)
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
                    uri.Contains("WMSServer", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("WMS", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("OGC", StringComparison.OrdinalIgnoreCase) ||
                    uri.Contains("/rest/services/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 2. Check CIM Data Connection
                var dataConn = layer.GetDataConnection();
                if (dataConn != null)
                {
                    if (dataConn is CIMStandardDataConnection stdConn)
                    {
                        var wfStr = stdConn.WorkspaceFactory.ToString();
                        if (stdConn.WorkspaceFactory == WorkspaceFactory.FeatureService ||
                            wfStr.IndexOf("Service", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            wfStr.IndexOf("WFS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            wfStr.IndexOf("WMS", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }

                        string connStr = stdConn.WorkspaceConnectionString ?? string.Empty;
                        if (connStr.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                            connStr.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
                            connStr.Contains("URL=", StringComparison.OrdinalIgnoreCase) ||
                            connStr.Contains("FeatureServer", StringComparison.OrdinalIgnoreCase) ||
                            connStr.Contains("MapServer", StringComparison.OrdinalIgnoreCase) ||
                            (connStr.Contains("SERVER=", StringComparison.OrdinalIgnoreCase) && connStr.Contains("http", StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    else if (dataConn is CIMFeatureDatasetDataConnection dsConn)
                    {
                        string ds = dsConn.FeatureDataset ?? string.Empty;
                        if (ds.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            ds.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            ds.Contains("FeatureServer", StringComparison.OrdinalIgnoreCase) ||
                            ds.Contains("MapServer", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    else if (dataConn is CIMServiceConnection ||
                             dataConn is CIMVectorTileDataConnection)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DIM] LayerHelper.IsServiceLayer check error: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Backwards compatibility alias for IsServiceLayer.
        /// </summary>
        public static bool IsHttpServiceLayer(Layer layer) => IsServiceLayer(layer);
    }
}

using System;
using System.Globalization;
using GeoMetrics.Models;

namespace GeoMetrics.Utilities
{
    /// <summary>
    /// Pure static utility for unit conversion and number formatting.
    /// Thread-safe (no state). MCT-safe.
    /// </summary>
    public static class UnitConverter
    {
        // Conversion factors (native → target)
        private const double FtPerM        = 3.2808398950131233;
        private const double UsFtPerM      = 3.2808333333333334;  // US Survey Foot
        private const double KmPerM        = 0.001;
        private const double MiPerM        = 0.0006213711922373340;
        private const double FtSqPerMSq    = FtPerM * FtPerM;
        private const double UsFtSqPerMSq  = UsFtPerM * UsFtPerM;
        private const double KmSqPerMSq    = KmPerM * KmPerM;
        private const double MiSqPerMSq    = MiPerM * MiPerM;

        // ── Length conversion ─────────────────────────────────────────────────────

        /// <summary>
        /// Converts a length from its native unit (identified by <paramref name="nativeAbbrev"/>)
        /// to the user's chosen display unit.
        /// </summary>
        /// <param name="nativeValue">Value in the native CRS unit.</param>
        /// <param name="nativeAbbrev">"m" or "ft" — from GeometryMeasurementService.</param>
        /// <param name="target">User's chosen display unit.</param>
        /// <returns>(converted value, display abbreviation)</returns>
        public static (double value, string abbrev) ConvertLength(
            double nativeValue, string nativeAbbrev, DisplayUnitOption target)
        {
            if (target == DisplayUnitOption.LayerNative)
                return (nativeValue, nativeAbbrev);

            double meters = ToMeters(nativeValue, nativeAbbrev);

            return target switch
            {
                DisplayUnitOption.Meters        => (meters,                "m"),
                DisplayUnitOption.Feet          => (meters * FtPerM,       "ft"),
                DisplayUnitOption.US_Survey_Feet => (meters * UsFtPerM,    "ft"),
                DisplayUnitOption.Kilometers    => (meters * KmPerM,       "km"),
                DisplayUnitOption.Miles         => (meters * MiPerM,       "mi"),
                _                              => (nativeValue, nativeAbbrev)
            };
        }

        // ── Area conversion ───────────────────────────────────────────────────────

        public static (double value, string abbrev) ConvertArea(
            double nativeSqValue, string nativeAbbrev, DisplayUnitOption target)
        {
            if (target == DisplayUnitOption.LayerNative)
                return (nativeSqValue, nativeAbbrev);

            double sqMeters = ToSqMeters(nativeSqValue, nativeAbbrev);

            return target switch
            {
                DisplayUnitOption.Meters        => (sqMeters,               "m\u00B2"),
                DisplayUnitOption.Feet          => (sqMeters * FtSqPerMSq,  "ft\u00B2"),
                DisplayUnitOption.US_Survey_Feet => (sqMeters * UsFtSqPerMSq, "ft\u00B2"),
                DisplayUnitOption.Kilometers    => (sqMeters * KmSqPerMSq,  "km\u00B2"),
                DisplayUnitOption.Miles         => (sqMeters * MiSqPerMSq,  "mi\u00B2"),
                _                              => (nativeSqValue, nativeAbbrev)
            };
        }

        // ── Formatting ────────────────────────────────────────────────────────────

        public static string FormatNumber(double value, int precision)
            => value.ToString("N" + Math.Max(0, precision), CultureInfo.InvariantCulture);

        public static string FormatLength(double value, int precision, string unitAbbrev)
            => value.ToString("N" + Math.Max(0, precision), CultureInfo.InvariantCulture) + " " + unitAbbrev;

        public static string FormatArea(double value, int precision, string unitAbbrev)
            => value.ToString("N" + Math.Max(0, precision), CultureInfo.InvariantCulture) + " " + unitAbbrev;

        public static string FormatBearing(double degrees)
            => degrees.ToString("N1", CultureInfo.InvariantCulture) + "\u00B0";

        public static string FormatAngle(double degrees, int precision)
            => degrees.ToString("N" + Math.Max(0, precision), CultureInfo.InvariantCulture) + "\u00B0";

        // ── Private helpers ───────────────────────────────────────────────────────

        private static double ToMeters(double val, string abbrev)
        {
            var a = (abbrev ?? "m").ToLowerInvariant().Trim();
            if (a.StartsWith("ft", StringComparison.Ordinal)) return val / FtPerM;
            if (a.StartsWith("km", StringComparison.Ordinal)) return val / KmPerM;
            if (a.StartsWith("mi", StringComparison.Ordinal)) return val / MiPerM;
            return val; // already meters
        }

        private static double ToSqMeters(double val, string abbrev)
        {
            var a = (abbrev ?? "m²").ToLowerInvariant().Trim();
            if (a.StartsWith("ft")) return val / FtSqPerMSq;
            if (a.StartsWith("km")) return val / KmSqPerMSq;
            if (a.StartsWith("mi")) return val / MiSqPerMSq;
            return val;
        }
    }
}

using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

internal static class CliFormat
{
    public static string FormatNumber(long value) => value.ToString("N0").Replace(',', '_');

    public static string FormatUnsignedNumber(ulong value) => value.ToString("N0").Replace(',', '_');

    public static string FormatOptionalNumber(long? value) => value.HasValue ? FormatNumber(value.Value) : "none";

    public static string FormatDecimal(double value) => value.ToString("N2", CultureInfo.InvariantCulture).Replace(',', '_');

    public static string FormatCompactDouble(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    public static double Ratio(long numerator, long denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;

    public static string FormatPercent(double value) =>
        (value * 100d).ToString("0.###", CultureInfo.InvariantCulture) + "%";

    public static double MegabytesPerSecond(long bytes, TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0
            ? 0
            : bytes / 1_000_000d / elapsed.TotalSeconds;

    public static double PerSecond(long count, TimeSpan elapsed) =>
        elapsed.TotalSeconds <= 0
            ? 0
            : count / elapsed.TotalSeconds;
}

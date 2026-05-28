using System.Text.Json;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Product;

public sealed partial class RadarPulseProductFileRunHistoryStore
{
    private void ThrowIfBlocked()
    {
        if (!string.IsNullOrEmpty(firstBlockingReason))
        {
            throw new InvalidOperationException(firstBlockingReason);
        }
    }

    private void Block(
        string reason,
        int rejectedCount)
    {
        firstBlockingReason = reason;
        rejectedRunCount = rejectedCount;
        runsById.Clear();
        runOrder.Clear();
    }

    private static bool AreSameRecord(
        RadarPulseProductRunDetail left,
        RadarPulseProductRunDetail right) =>
        string.Equals(
            JsonSerializer.Serialize(left, JsonOptions),
            JsonSerializer.Serialize(right, JsonOptions),
            StringComparison.Ordinal);

    private static string ResolveStoragePath(
        string storagePath,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            error = "Product run history path is required.";
            return string.Empty;
        }

        try
        {
            var fullPath = Path.GetFullPath(storagePath);
            if (Directory.Exists(fullPath))
            {
                error = "Product run history path must be a file path, not a directory.";
                return fullPath;
            }

            error = string.Empty;
            return fullPath;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Product run history path is invalid: {exception.Message}";
            return storagePath;
        }
    }

    private sealed record PersistedHistoryFile(
        int SchemaVersion,
        IReadOnlyList<RadarPulseProductRunDetail> Runs);
}

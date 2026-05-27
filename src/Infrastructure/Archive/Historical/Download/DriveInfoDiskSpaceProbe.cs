namespace RadarPulse.Infrastructure.Archive;

public sealed class DriveInfoDiskSpaceProbe : IDiskSpaceProbe
{
    public long GetAvailableBytes(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException($"Cannot determine drive root for '{path}'.");
        }

        try
        {
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new InvalidOperationException($"Cannot determine available disk space for '{path}'.", ex);
        }
    }
}

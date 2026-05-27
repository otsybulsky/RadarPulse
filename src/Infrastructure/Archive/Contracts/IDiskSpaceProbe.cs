namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Abstraction for probing available disk space before archive downloads.
/// </summary>
public interface IDiskSpaceProbe
{
    /// <summary>
    /// Gets available bytes for the storage volume that contains the supplied path.
    /// </summary>
    long GetAvailableBytes(string path);
}

using RadarPulse.Domain.Archive;

namespace RadarPulse.Application.Archive;

/// <summary>
/// Client abstraction for discovering and downloading historical archive files.
/// </summary>
public interface IHistoricalArchiveClient
{
    /// <summary>
    /// Builds a manifest for the requested archive date and radar selection.
    /// </summary>
    Task<HistoricalArchiveManifest> BuildManifestAsync(
        HistoricalArchiveRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads one manifest file into the supplied destination stream.
    /// </summary>
    Task DownloadFileAsync(
        HistoricalArchiveFile file,
        Stream destination,
        CancellationToken cancellationToken);
}

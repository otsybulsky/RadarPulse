using RadarPulse.Domain.Archive;

namespace RadarPulse.Application.Archive;

public interface IHistoricalArchiveClient
{
    Task<HistoricalArchiveManifest> BuildManifestAsync(
        HistoricalArchiveRequest request,
        CancellationToken cancellationToken);
}

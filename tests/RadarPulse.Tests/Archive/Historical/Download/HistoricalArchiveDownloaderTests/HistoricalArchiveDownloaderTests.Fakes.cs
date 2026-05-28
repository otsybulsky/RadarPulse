using System.Text.Json;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Tests.Archive;

public sealed partial class HistoricalArchiveDownloaderTests
{
    private sealed class FakeHistoricalArchiveClient(params byte[] content) : IHistoricalArchiveClient
    {
        private readonly byte[] _content = content.Length == 0 ? [1, 2, 3, 4] : content;

        public int DownloadCallCount { get; private set; }

        public Task<HistoricalArchiveManifest> BuildManifestAsync(
            HistoricalArchiveRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task DownloadFileAsync(
            HistoricalArchiveFile file,
            Stream destination,
            CancellationToken cancellationToken)
        {
            DownloadCallCount++;
            await destination.WriteAsync(_content, cancellationToken);
        }
    }

    private sealed class FakeDiskSpaceProbe(long availableBytes) : IDiskSpaceProbe
    {
        public long GetAvailableBytes(string path) => availableBytes;
    }
}

using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    public sealed class ArchiveOwnedRadarEventBatchConsumerResourceLease : IDisposable
    {
        private readonly object sync = new();
        private readonly ArchiveOwnedRadarEventBatchQueueingPublisher publisher;
        private readonly RadarProcessingRetainedBatchResource resource;
        private readonly long pressurePayloadBytes;
        private RadarProcessingRetainedPayloadReleaseResult? releaseResult;

        internal ArchiveOwnedRadarEventBatchConsumerResourceLease(
            ArchiveOwnedRadarEventBatchQueueingPublisher publisher,
            RadarProcessingRetainedBatchResource resource,
            long pressurePayloadBytes)
        {
            this.publisher = publisher;
            this.resource = resource;
            this.pressurePayloadBytes = pressurePayloadBytes;
        }

        /// <summary>
        /// Releases the retained resource once and returns the release result.
        /// </summary>
        public RadarProcessingRetainedPayloadReleaseResult Release()
        {
            lock (sync)
            {
                releaseResult ??= publisher.ReleaseConsumerResource(resource, pressurePayloadBytes);
                return releaseResult;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Release();
        }
    }
}

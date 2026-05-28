using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    private sealed record RetainedResourceEntry(
        RadarProcessingRetainedBatchResource Resource,
        long PressurePayloadBytes);

    private RadarProcessingRetainedPayloadReleaseResult ReleaseConsumerResource(
        RadarProcessingRetainedBatchResource resource,
        long pressurePayloadBytes)
    {
        try
        {
            var release = resource.Release();
            RecordReleaseResult(release);
            return release;
        }
        finally
        {
            retainedResourcePressureRecorder.RemoveActive(pressurePayloadBytes);
        }
    }

    private void TrackRetainedResource(
        RadarProcessingQueuedBatchSequence sequence,
        RadarProcessingRetainedBatchResource resource,
        long pressurePayloadBytes)
    {
        lock (sync)
        {
            resource.TransferToQueue();
            retainedResources.Add(sequence.Value, new RetainedResourceEntry(resource, pressurePayloadBytes));
        }

        retainedResourcePressureRecorder.AddPending(pressurePayloadBytes);
    }
}

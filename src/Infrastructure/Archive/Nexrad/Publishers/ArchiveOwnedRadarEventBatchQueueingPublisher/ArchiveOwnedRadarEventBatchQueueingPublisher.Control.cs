using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    public void CompleteAdding() => queue.Close();

    /// <summary>
    /// Creates a queueing provider result from current enqueue and retention telemetry.
    /// </summary>
    public RadarProcessingArchiveQueuedProviderResult CreateResult() =>
        new(GetEnqueueResultsSnapshot(), CreateQueueTelemetrySummary(), CreateRetentionTelemetrySummary());

    /// <summary>
    /// Acquires the retained payload resource associated with a queued batch sequence for consumer ownership.
    /// </summary>
    public ArchiveOwnedRadarEventBatchConsumerResourceLease AcquireConsumerResourceLease(
        RadarProcessingQueuedBatchSequence sequence)
    {
        RetainedResourceEntry entry;
        lock (sync)
        {
            if (!retainedResources.Remove(sequence.Value, out entry!))
            {
                throw new InvalidOperationException($"No retained resource was found for queued provider sequence {sequence.Value}.");
            }
        }

        entry.Resource.TransferToConsumer();
        retainedResourcePressureRecorder.MovePendingToActive(entry.PressurePayloadBytes);
        return new ArchiveOwnedRadarEventBatchConsumerResourceLease(
            this,
            entry.Resource,
            entry.PressurePayloadBytes);
    }

    /// <summary>
    /// Acquires and releases the retained payload resource for a queued batch sequence.
    /// </summary>
    public RadarProcessingRetainedPayloadReleaseResult ReleaseConsumerResource(
        RadarProcessingQueuedBatchSequence sequence)
    {
        using var lease = AcquireConsumerResourceLease(sequence);
        return lease.Release();
    }

    /// <summary>
    /// Releases all retained resources that were enqueued but not yet acquired by a consumer.
    /// </summary>
    public RadarProcessingRetainedResourceCleanupResult ReleasePendingResources()
    {
        RetainedResourceEntry[] pending;
        lock (sync)
        {
            pending = retainedResources.Values.ToArray();
            retainedResources.Clear();
        }

        var releaseResults = new List<RadarProcessingRetainedPayloadReleaseResult>(pending.Length);
        foreach (var entry in pending)
        {
            var release = entry.Resource.Release();
            releaseResults.Add(release);
            RecordReleaseResult(release);
            retainedResourcePressureRecorder.RemovePending(entry.PressurePayloadBytes);
        }

        return new RadarProcessingRetainedResourceCleanupResult(releaseResults);
    }

    /// <summary>
    /// Consumer-owned lease for a retained archive batch resource.
}

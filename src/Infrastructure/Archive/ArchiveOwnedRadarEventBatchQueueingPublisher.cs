using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly bool ownsQueue;
    private readonly RadarProcessingRetainedPayloadFactory retainedPayloadFactory;
    private readonly RadarProcessingRetainedPayloadOptions retainedPayloadOptions;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly Dictionary<long, RetainedResourceEntry> retainedResources = [];
    private readonly RadarProcessingRetainedResourcePressureRecorder retainedResourcePressureRecorder = new();
    private long retentionAttemptCount;
    private long retainedBatchCount;
    private long retentionUnsupportedStrategyCount;
    private long retentionFailedCopyCount;
    private long retentionCanceledCount;
    private long retentionInvalidInputCount;
    private long retainedEventCount;
    private long retainedPayloadBytes;
    private long retainedPayloadValueCount;
    private long retainedAllocatedBytes;
    private long retainedPoolRentCount;
    private long retainedPoolReturnCount;
    private long retainedPoolMissCount;
    private long retainedEventPoolRentCount;
    private long retainedEventPoolReturnCount;
    private long retainedEventPoolMissCount;
    private long retainedPayloadPoolRentCount;
    private long retainedPayloadPoolReturnCount;
    private long retainedPayloadPoolMissCount;
    private TimeSpan totalRetentionTime;
    private long releaseAttemptCount;
    private long releasedBatchCount;
    private long alreadyReleasedBatchCount;
    private long releaseFailedCount;
    private long releaseNotRequiredCount;
    private TimeSpan totalReleaseTime;
    private bool disposed;

    public ArchiveOwnedRadarEventBatchQueueingPublisher(
        RadarProcessingProviderQueueOptions? queueOptions = null,
        RadarProcessingRetainedPayloadOptions? retainedPayloadOptions = null,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null)
        : this(
            new RadarProcessingOwnedBatchQueue(queueOptions),
            ownsQueue: true,
            retainedPayloadOptions,
            retainedPayloadFactory)
    {
    }

    public ArchiveOwnedRadarEventBatchQueueingPublisher(
        RadarProcessingOwnedBatchQueue queue,
        bool ownsQueue = false,
        RadarProcessingRetainedPayloadOptions? retainedPayloadOptions = null,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null)
    {
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this.ownsQueue = ownsQueue;
        this.retainedPayloadOptions = retainedPayloadOptions ?? RadarProcessingRetainedPayloadOptions.Default;
        this.retainedPayloadFactory = retainedPayloadFactory ?? new RadarProcessingRetainedPayloadFactory();
    }

    public RadarProcessingOwnedBatchQueue Queue => queue;

    public IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> EnqueueResults
    {
        get
        {
            lock (sync)
            {
                return Array.AsReadOnly(enqueueResults.ToArray());
            }
        }
    }

    public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(batch);

        if (cancellationToken.IsCancellationRequested)
        {
            var canceled = RadarProcessingQueuedBatchEnqueueResult.Canceled();
            RecordEnqueueResult(canceled);
            throw new OperationCanceledException(cancellationToken);
        }

        var stateRejection = TryCreateStateRejection();
        if (stateRejection is not null)
        {
            RecordEnqueueResult(stateRejection);
            throw CreateRejectedPublishException(stateRejection, cancellationToken);
        }

        var retention = retainedPayloadFactory.Retain(batch, retainedPayloadOptions, cancellationToken);
        RecordRetentionResult(retention);
        if (!retention.IsSuccessful)
        {
            throw CreateRejectedRetentionException(retention, cancellationToken);
        }

        var enqueue = queue
            .EnqueueAsync(
                retention.Batch!,
                retention.Elapsed,
                retention.AllocatedBytes,
                cancellationToken,
                queuedBatch => TrackRetainedResource(
                    queuedBatch.Sequence,
                    retention.Resource!,
                    queuedBatch.PayloadBytes))
            .AsTask()
            .GetAwaiter()
            .GetResult();

        RecordEnqueueResult(enqueue);
        if (!enqueue.IsAccepted)
        {
            if (retention.Resource is not null)
            {
                RecordReleaseResult(retention.Resource.Release());
            }

            throw CreateRejectedPublishException(enqueue, cancellationToken);
        }
    }

    public void CompleteAdding() => queue.Close();

    public RadarProcessingArchiveQueuedProviderResult CreateResult() =>
        new(GetEnqueueResultsSnapshot(), CreateQueueTelemetrySummary(), CreateRetentionTelemetrySummary());

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

    public RadarProcessingRetainedPayloadReleaseResult ReleaseConsumerResource(
        RadarProcessingQueuedBatchSequence sequence)
    {
        using var lease = AcquireConsumerResourceLease(sequence);
        return lease.Release();
    }

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

        public RadarProcessingRetainedPayloadReleaseResult Release()
        {
            lock (sync)
            {
                releaseResult ??= publisher.ReleaseConsumerResource(resource, pressurePayloadBytes);
                return releaseResult;
            }
        }

        public void Dispose()
        {
            Release();
        }
    }

    public void Dispose()
    {
        bool shouldDispose;
        lock (sync)
        {
            shouldDispose = !disposed;
            disposed = true;
        }

        if (shouldDispose && ownsQueue)
        {
            queue.Dispose();
        }

        ReleasePendingResources();
    }

    private bool IsDisposed
    {
        get
        {
            lock (sync)
            {
                return disposed;
            }
        }
    }

    private RadarProcessingQueuedBatchEnqueueResult[] GetEnqueueResultsSnapshot()
    {
        lock (sync)
        {
            return enqueueResults.ToArray();
        }
    }

    private void RecordEnqueueResult(
        RadarProcessingQueuedBatchEnqueueResult result)
    {
        lock (sync)
        {
            enqueueResults.Add(result);
        }
    }

    private void RecordRetentionResult(
        RadarProcessingRetainedPayloadRetentionResult result)
    {
        lock (sync)
        {
            retentionAttemptCount++;
            switch (result.Status)
            {
                case RadarProcessingRetainedPayloadRetentionStatus.Succeeded:
                    retainedBatchCount++;
                    retainedEventCount = checked(retainedEventCount + result.EventCount);
                    retainedPayloadBytes = checked(retainedPayloadBytes + result.PayloadBytes);
                    retainedPayloadValueCount = checked(retainedPayloadValueCount + result.PayloadValueCount);
                    retainedAllocatedBytes = checked(retainedAllocatedBytes + result.AllocatedBytes);
                    retainedPoolRentCount = checked(retainedPoolRentCount + result.PoolRentCount);
                    retainedPoolMissCount = checked(retainedPoolMissCount + result.PoolMissCount);
                    retainedEventPoolRentCount = checked(retainedEventPoolRentCount + result.EventPoolRentCount);
                    retainedEventPoolMissCount = checked(retainedEventPoolMissCount + result.EventPoolMissCount);
                    retainedPayloadPoolRentCount = checked(retainedPayloadPoolRentCount + result.PayloadPoolRentCount);
                    retainedPayloadPoolMissCount = checked(retainedPayloadPoolMissCount + result.PayloadPoolMissCount);
                    totalRetentionTime += result.Elapsed;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.UnsupportedStrategy:
                    retentionUnsupportedStrategyCount++;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.FailedCopy:
                    retentionFailedCopyCount++;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.Canceled:
                    retentionCanceledCount++;
                    break;

                case RadarProcessingRetainedPayloadRetentionStatus.InvalidInput:
                    retentionInvalidInputCount++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }
        }
    }

    private void RecordReleaseResult(
        RadarProcessingRetainedPayloadReleaseResult result)
    {
        lock (sync)
        {
            releaseAttemptCount++;
            totalReleaseTime += result.Elapsed;
            retainedPoolReturnCount = checked(retainedPoolReturnCount + result.PoolReturnCount);
            retainedEventPoolReturnCount = checked(retainedEventPoolReturnCount + result.EventPoolReturnCount);
            retainedPayloadPoolReturnCount = checked(retainedPayloadPoolReturnCount + result.PayloadPoolReturnCount);
            switch (result.Status)
            {
                case RadarProcessingRetainedPayloadReleaseStatus.Released:
                    releasedBatchCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased:
                    alreadyReleasedBatchCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.Failed:
                    releaseFailedCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.NotRequired:
                    releaseNotRequiredCount++;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }
        }
    }

    private RadarProcessingProviderQueueTelemetrySummary CreateQueueTelemetrySummary() =>
        queue.CreateTelemetrySummary().WithRetainedResourcePressure(
            retainedResourcePressureRecorder.CreateSummary());

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

    private RadarProcessingRetainedPayloadTelemetrySummary CreateRetentionTelemetrySummary()
    {
        lock (sync)
        {
            return new RadarProcessingRetainedPayloadTelemetrySummary(
                retainedPayloadOptions.Strategy,
                retentionAttemptCount,
                retainedBatchCount,
                retentionUnsupportedStrategyCount,
                retentionFailedCopyCount,
                retentionCanceledCount,
                retentionInvalidInputCount,
                retainedEventCount,
                retainedPayloadBytes,
                retainedPayloadValueCount,
                retainedAllocatedBytes,
                totalRetentionTime,
                transferCount: retainedBatchCount,
                poolRentCount: retainedPoolRentCount,
                poolReturnCount: retainedPoolReturnCount,
                poolMissCount: retainedPoolMissCount,
                releaseAttemptCount: releaseAttemptCount,
                releasedBatchCount: releasedBatchCount,
                alreadyReleasedBatchCount: alreadyReleasedBatchCount,
                releaseFailedCount: releaseFailedCount,
                releaseNotRequiredCount: releaseNotRequiredCount,
                totalReleaseTime: totalReleaseTime,
                eventPoolRentCount: retainedEventPoolRentCount,
                eventPoolReturnCount: retainedEventPoolReturnCount,
                eventPoolMissCount: retainedEventPoolMissCount,
                payloadPoolRentCount: retainedPayloadPoolRentCount,
                payloadPoolReturnCount: retainedPayloadPoolReturnCount,
                payloadPoolMissCount: retainedPayloadPoolMissCount);
        }
    }

    private RadarProcessingQueuedBatchEnqueueResult? TryCreateStateRejection()
    {
        if (queue.IsFaulted)
        {
            return RadarProcessingQueuedBatchEnqueueResult.Faulted(
                message: "Owned archive provider queue is faulted.");
        }

        if (queue.IsClosed || queue.IsDisposed)
        {
            return RadarProcessingQueuedBatchEnqueueResult.Closed(
                message: "Owned archive provider queue is closed.");
        }

        return null;
    }

    private static Exception CreateRejectedPublishException(
        RadarProcessingQueuedBatchEnqueueResult result,
        CancellationToken cancellationToken)
    {
        if (result.Status == RadarProcessingQueuedBatchEnqueueStatus.Canceled)
        {
            return new OperationCanceledException(cancellationToken);
        }

        var message = string.IsNullOrEmpty(result.Message)
            ? $"Archive queued provider enqueue was rejected with status {result.Status}."
            : $"Archive queued provider enqueue was rejected with status {result.Status}: {result.Message}";
        return new InvalidOperationException(message);
    }

    private static Exception CreateRejectedRetentionException(
        RadarProcessingRetainedPayloadRetentionResult result,
        CancellationToken cancellationToken)
    {
        if (result.Status == RadarProcessingRetainedPayloadRetentionStatus.Canceled)
        {
            return new OperationCanceledException(cancellationToken);
        }

        var message = string.IsNullOrEmpty(result.Message)
            ? $"Archive queued provider retention was rejected with status {result.Status}."
            : $"Archive queued provider retention was rejected with status {result.Status}: {result.Message}";
        return new InvalidOperationException(message);
    }
}

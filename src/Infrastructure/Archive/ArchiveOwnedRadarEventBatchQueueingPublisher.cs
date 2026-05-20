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
    private readonly Dictionary<long, RadarProcessingRetainedBatchResource> retainedResources = [];
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
                cancellationToken)
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

        TrackRetainedResource(enqueue.Sequence!.Value, retention.Resource!);
    }

    public void CompleteAdding() => queue.Close();

    public RadarProcessingArchiveQueuedProviderResult CreateResult() =>
        new(GetEnqueueResultsSnapshot(), queue.CreateTelemetrySummary(), CreateRetentionTelemetrySummary());

    public RadarProcessingRetainedPayloadReleaseResult ReleaseConsumerResource(
        RadarProcessingQueuedBatchSequence sequence)
    {
        RadarProcessingRetainedBatchResource resource;
        lock (sync)
        {
            if (!retainedResources.Remove(sequence.Value, out resource!))
            {
                throw new InvalidOperationException($"No retained resource was found for queued provider sequence {sequence.Value}.");
            }
        }

        resource.TransferToConsumer();
        var release = resource.Release();
        RecordReleaseResult(release);
        return release;
    }

    public RadarProcessingRetainedResourceCleanupResult ReleasePendingResources()
    {
        RadarProcessingRetainedBatchResource[] pending;
        lock (sync)
        {
            pending = retainedResources.Values.ToArray();
            retainedResources.Clear();
        }

        var cleanup = RadarProcessingRetainedResourceCleanupResult.ReleaseAll(pending);
        foreach (var release in cleanup.ReleaseResults)
        {
            RecordReleaseResult(release);
        }

        return cleanup;
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

    private void TrackRetainedResource(
        RadarProcessingQueuedBatchSequence sequence,
        RadarProcessingRetainedBatchResource resource)
    {
        lock (sync)
        {
            resource.TransferToQueue();
            retainedResources.Add(sequence.Value, resource);
        }
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
                releaseAttemptCount: releaseAttemptCount,
                releasedBatchCount: releasedBatchCount,
                alreadyReleasedBatchCount: alreadyReleasedBatchCount,
                releaseFailedCount: releaseFailedCount,
                releaseNotRequiredCount: releaseNotRequiredCount,
                totalReleaseTime: totalReleaseTime);
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

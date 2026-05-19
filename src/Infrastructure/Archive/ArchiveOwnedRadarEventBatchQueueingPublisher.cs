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
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private bool disposed;

    public ArchiveOwnedRadarEventBatchQueueingPublisher(
        RadarProcessingProviderQueueOptions? queueOptions = null)
        : this(new RadarProcessingOwnedBatchQueue(queueOptions), ownsQueue: true)
    {
    }

    public ArchiveOwnedRadarEventBatchQueueingPublisher(
        RadarProcessingOwnedBatchQueue queue,
        bool ownsQueue = false)
    {
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this.ownsQueue = ownsQueue;
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

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var snapshotStarted = Stopwatch.GetTimestamp();
        var owned = batch.ToOwnedSnapshot();
        var ownedSnapshotTime = Stopwatch.GetElapsedTime(snapshotStarted);
        var ownedSnapshotAllocatedBytes = RadarProcessingBenchmarkAllocationSnapshot
            .Capture()
            .DeltaSince(allocationBefore);

        var enqueue = queue
            .EnqueueAsync(
                owned,
                ownedSnapshotTime,
                ownedSnapshotAllocatedBytes,
                cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        RecordEnqueueResult(enqueue);
        if (!enqueue.IsAccepted)
        {
            throw CreateRejectedPublishException(enqueue, cancellationToken);
        }
    }

    public void CompleteAdding() => queue.Close();

    public RadarProcessingArchiveQueuedProviderResult CreateResult() =>
        new(GetEnqueueResultsSnapshot(), queue.CreateTelemetrySummary());

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
}

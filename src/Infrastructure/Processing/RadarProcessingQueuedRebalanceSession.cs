using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingQueuedRebalanceSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
    private readonly bool ownsQueue;
    private readonly bool ownsAsyncRebalanceSession;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private TimeSpan totalDrainTime;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    public RadarProcessingQueuedRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingProviderQueueOptions? queueOptions = null)
        : this(
            rebalanceSession,
            new RadarProcessingOwnedBatchQueue(queueOptions),
            CreateAsyncRebalanceSessionIfNeeded(rebalanceSession),
            ownsQueue: true,
            ownsAsyncRebalanceSession: RequiresAsyncRebalanceSession(rebalanceSession))
    {
    }

    public RadarProcessingQueuedRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOwnedBatchQueue queue,
        RadarProcessingAsyncRebalanceSession? asyncRebalanceSession = null,
        bool ownsQueue = false,
        bool ownsAsyncRebalanceSession = false)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        ArgumentNullException.ThrowIfNull(queue);

        if (rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncRebalanceSession);
            if (!ReferenceEquals(rebalanceSession, asyncRebalanceSession.RebalanceSession))
            {
                throw new ArgumentException(
                    "Queued async rebalance requires the async rebalance session to wrap the supplied rebalance session.",
                    nameof(asyncRebalanceSession));
            }
        }
        else if (asyncRebalanceSession is not null)
        {
            throw new ArgumentException(
                "Queued synchronous rebalance must not carry an async rebalance session.",
                nameof(asyncRebalanceSession));
        }

        this.rebalanceSession = rebalanceSession;
        this.queue = queue;
        this.asyncRebalanceSession = asyncRebalanceSession;
        this.ownsQueue = ownsQueue;
        this.ownsAsyncRebalanceSession = ownsAsyncRebalanceSession;
    }

    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    public RadarProcessingCore Core => rebalanceSession.Core;

    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    public RadarProcessingOwnedBatchQueue Queue => queue;

    public async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var result = await queue.EnqueueAsync(
            batch,
            ownedSnapshotTime,
            ownedSnapshotAllocatedBytes,
            cancellationToken).ConfigureAwait(false);
        RecordEnqueueResult(result);
        return result;
    }

    public void CompleteAdding() => queue.Close();

    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        MarkFaulted(message);
    }

    public async ValueTask<RadarProcessingQueuedSessionResult> DrainAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var started = Stopwatch.GetTimestamp();
        try
        {
            while (true)
            {
                var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                switch (dequeue.Status)
                {
                    case RadarProcessingOwnedBatchDequeueStatus.Item:
                        await ProcessDequeuedBatchAsync(dequeue.Batch!, cancellationToken).ConfigureAwait(false);
                        break;

                    case RadarProcessingOwnedBatchDequeueStatus.Closed:
                        AddDrainTime(started);
                        return CreateSessionResult(GetTerminalStatus(), GetTerminalMessage());

                    case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                        MarkFaulted(dequeue.Message);
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Faulted, dequeue.Message);

                    case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                        MarkCanceled();
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");

                    case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                        AddDrainTime(started);
                        return CreateSessionResult(RadarProcessingQueuedSessionStatus.Disposed, "Queued rebalance queue was disposed.");

                    default:
                        RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                        throw new ArgumentOutOfRangeException(nameof(dequeue));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceled();
            AddDrainTime(started);
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        bool shouldDispose;
        lock (sync)
        {
            shouldDispose = !disposed;
            disposed = true;
        }

        if (!shouldDispose)
        {
            return;
        }

        if (ownsAsyncRebalanceSession && asyncRebalanceSession is not null)
        {
            await asyncRebalanceSession.DisposeAsync().ConfigureAwait(false);
        }

        if (ownsQueue)
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

    private async ValueTask ProcessDequeuedBatchAsync(
        RadarProcessingQueuedBatch queuedBatch,
        CancellationToken cancellationToken)
    {
        if (IsFaulted)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                    queuedBatch.Sequence,
                    faultMessage));
            return;
        }

        try
        {
            var rebalanceResult = asyncRebalanceSession is null
                ? rebalanceSession.Process(queuedBatch.Batch, cancellationToken)
                : await asyncRebalanceSession.ProcessAsync(queuedBatch.Batch, cancellationToken).ConfigureAwait(false);

            var processingResult = rebalanceResult.ProcessingResult;
            if (!processingResult.IsValid)
            {
                var message = processingResult.Validation.Message;
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        message,
                        processingResult));
                MarkFaulted(message);
                return;
            }

            if (rebalanceResult.MigrationResult is { Succeeded: false } migrationResult)
            {
                var message = $"Queued rebalance migration failed with state {migrationResult.State}.";
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedMigration(
                        queuedBatch.Sequence,
                        message,
                        rebalanceResult));
                MarkFaulted(message);
                return;
            }

            if (!rebalanceResult.Validation.IsValid)
            {
                var message = rebalanceResult.Validation.Message;
                RecordProcessingResult(
                    RadarProcessingQueuedBatchProcessingResult.FailedValidation(
                        queuedBatch.Sequence,
                        message,
                        processingResult));
                MarkFaulted(message);
                return;
            }

            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Succeeded(
                    queuedBatch.Sequence,
                    rebalanceResult));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceled();
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.Canceled(
                    queuedBatch.Sequence,
                    "Queued rebalance batch was canceled."));
        }
        catch (Exception exception)
        {
            RecordProcessingResult(
                RadarProcessingQueuedBatchProcessingResult.FailedProcessing(
                    queuedBatch.Sequence,
                    exception.Message));
            MarkFaulted(exception.Message);
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

    private void RecordProcessingResult(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        lock (sync)
        {
            processingResults.Add(result);
        }
    }

    private bool IsFaulted
    {
        get
        {
            lock (sync)
            {
                return faulted;
            }
        }
    }

    private void MarkFaulted(string message)
    {
        lock (sync)
        {
            faulted = true;
            faultMessage = message;
        }

        queue.Fault(message);
    }

    private void MarkCanceled()
    {
        lock (sync)
        {
            canceled = true;
        }

        queue.Close();
    }

    private void AddDrainTime(long started)
    {
        lock (sync)
        {
            totalDrainTime += Stopwatch.GetElapsedTime(started);
        }
    }

    private RadarProcessingQueuedSessionStatus GetTerminalStatus()
    {
        lock (sync)
        {
            if (canceled)
            {
                return RadarProcessingQueuedSessionStatus.Canceled;
            }

            return faulted
                ? RadarProcessingQueuedSessionStatus.Faulted
                : RadarProcessingQueuedSessionStatus.Completed;
        }
    }

    private string GetTerminalMessage()
    {
        lock (sync)
        {
            return faulted ? faultMessage : string.Empty;
        }
    }

    private RadarProcessingQueuedSessionResult CreateSessionResult(
        RadarProcessingQueuedSessionStatus status,
        string message)
    {
        RadarProcessingQueuedBatchEnqueueResult[] enqueueSnapshot;
        RadarProcessingQueuedBatchProcessingResult[] processingSnapshot;
        TimeSpan drainTime;
        lock (sync)
        {
            enqueueSnapshot = enqueueResults.ToArray();
            processingSnapshot = processingResults.ToArray();
            drainTime = totalDrainTime;
        }

        return new RadarProcessingQueuedSessionResult(
            status,
            CreateTelemetrySummary(drainTime, processingSnapshot),
            enqueueSnapshot,
            processingSnapshot,
            message,
            rebalanceSession.CurrentTopology.Version);
    }

    private RadarProcessingProviderQueueTelemetrySummary CreateTelemetrySummary(
        TimeSpan drainTime,
        IReadOnlyCollection<RadarProcessingQueuedBatchProcessingResult> processingSnapshot)
    {
        var queueSummary = queue.CreateTelemetrySummary();
        var completed = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded);
        var failed = processingSnapshot.LongCount(static result =>
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedProcessing ||
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedValidation ||
            result.Status == RadarProcessingQueuedBatchProcessingStatus.FailedMigration);
        var canceledCount = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled);
        var skipped = processingSnapshot.LongCount(static result => result.Status == RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault);

        return new RadarProcessingProviderQueueTelemetrySummary(
            queueSummary.OwnedSnapshotCount,
            queueSummary.OwnedSnapshotPayloadBytes,
            queueSummary.OwnedSnapshotAllocatedBytes,
            queueSummary.TotalOwnedSnapshotTime,
            queueSummary.EnqueueAttemptCount,
            queueSummary.EnqueuedBatchCount,
            queueSummary.EnqueueFullCount,
            queueSummary.EnqueueTimedOutCount,
            queueSummary.EnqueueCanceledCount,
            queueSummary.EnqueueClosedCount,
            queueSummary.EnqueueFaultedCount,
            queueSummary.TotalEnqueueWaitTime,
            queueSummary.DequeuedBatchCount,
            completed,
            failed,
            canceledCount,
            skipped,
            drainTime,
            queueSummary.QueueDepthHighWatermark,
            queueSummary.QueuedPayloadBytesHighWatermark);
    }

    private static RadarProcessingAsyncRebalanceSession? CreateAsyncRebalanceSessionIfNeeded(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? new RadarProcessingAsyncRebalanceSession(rebalanceSession)
            : null;
    }

    private static bool RequiresAsyncRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        return rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport;
    }
}

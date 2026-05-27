using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Bounded provider queue that accepts owned radar batches for processing sessions.
/// </summary>
/// <remarks>
/// The queue assigns monotonically increasing provider sequences, enforces the
/// retained payload byte budget, records enqueue/dequeue telemetry, and exposes
/// close, fault, cancellation, and disposal as explicit dequeue outcomes.
/// </remarks>
public sealed class RadarProcessingOwnedBatchQueue : IDisposable
{
    private readonly object sync = new();
    private readonly Channel<RadarProcessingQueuedBatch> channel;
    private readonly RadarProcessingProviderQueueTelemetryRecorder telemetryRecorder;
    private TaskCompletionSource<object?> retainedByteBudgetChanged = CreateRetainedByteBudgetChangedSource();
    private long nextSequence;
    private int pendingCount;
    private long pendingPayloadBytes;
    private bool closed;
    private bool faulted;
    private bool disposed;
    private string faultMessage = string.Empty;
    private long ownedSnapshotCount;
    private long ownedSnapshotEventCount;
    private long ownedSnapshotPayloadBytes;
    private long ownedSnapshotAllocatedBytes;
    private TimeSpan totalOwnedSnapshotTime;
    private long enqueueAttemptCount;
    private long enqueuedBatchCount;
    private long enqueueFullCount;
    private long enqueueTimedOutCount;
    private long enqueueCanceledCount;
    private long enqueueClosedCount;
    private long enqueueFaultedCount;
    private TimeSpan totalEnqueueWaitTime;
    private TimeSpan totalDequeueWaitTime;
    private long dequeuedBatchCount;
    private int queueDepthHighWatermark;
    private long queuedPayloadBytesHighWatermark;

    /// <summary>
    /// Creates a bounded owned-batch queue with the selected provider queue options.
    /// </summary>
    public RadarProcessingOwnedBatchQueue(
        RadarProcessingProviderQueueOptions? options = null)
    {
        Options = options ?? RadarProcessingProviderQueueOptions.Default;
        telemetryRecorder = new RadarProcessingProviderQueueTelemetryRecorder(Options);
        channel = Channel.CreateBounded<RadarProcessingQueuedBatch>(
            new BoundedChannelOptions(Options.Capacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    /// <summary>
    /// Effective capacity, full-mode, timeout, shutdown, and retained-byte settings.
    /// </summary>
    public RadarProcessingProviderQueueOptions Options { get; }

    /// <summary>
    /// Number of accepted queued batches not yet dequeued or canceled.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (sync)
            {
                return pendingCount;
            }
        }
    }

    /// <summary>
    /// Total payload bytes currently retained by accepted queued batches.
    /// </summary>
    public long PendingPayloadBytes
    {
        get
        {
            lock (sync)
            {
                return pendingPayloadBytes;
            }
        }
    }

    /// <summary>
    /// Alias for pending retained payload bytes used by pressure telemetry contracts.
    /// </summary>
    public long PendingRetainedPayloadBytes => PendingPayloadBytes;

    /// <summary>
    /// Indicates whether the queue is closed to new accepted batches.
    /// </summary>
    public bool IsClosed
    {
        get
        {
            lock (sync)
            {
                return closed;
            }
        }
    }

    /// <summary>
    /// Indicates whether the queue has been faulted and will reject further work.
    /// </summary>
    public bool IsFaulted
    {
        get
        {
            lock (sync)
            {
                return faulted;
            }
        }
    }

    /// <summary>
    /// Indicates whether the queue has been disposed and drained.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (sync)
            {
                return disposed;
            }
        }
    }

    /// <summary>
    /// Enqueues an owned batch, optionally waiting according to the configured full mode.
    /// </summary>
    /// <remarks>
    /// The method rejects non-owned batches because queued providers retain input
    /// beyond the caller's immediate stack frame. Accepted batches receive the
    /// next provider sequence and are included in retained-resource telemetry.
    /// </remarks>
    /// <returns>
    /// An accepted result with the queued batch, or a full, timed-out, canceled,
    /// closed, or faulted result that describes why the batch was not accepted.
    /// </returns>
    public ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default,
        Action<RadarProcessingQueuedBatch>? onAccepted = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Lifetime != RadarEventBatchLifetime.Owned)
        {
            throw new ArgumentException("Owned batch queue accepts only owned RadarEventBatch values.", nameof(batch));
        }

        if (ownedSnapshotTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ownedSnapshotTime));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ownedSnapshotAllocatedBytes);

        var started = Stopwatch.GetTimestamp();
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                    Stopwatch.GetElapsedTime(started)));
        }

        var stateRejection = TryGetStateRejection();
        if (stateRejection.HasValue)
        {
            return new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                RecordRejected(
                    stateRejection.Value.Status,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection.Value.Message));
        }

        var oversizedRejection = TryCreateOversizedRetainedByteBudgetMessage(batch.PayloadLength);
        if (oversizedRejection is not null)
        {
            return new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.Full,
                    Stopwatch.GetElapsedTime(started),
                    oversizedRejection));
        }

        return Options.FullMode == RadarProcessingProviderQueueFullMode.ReturnFull
            ? new ValueTask<RadarProcessingQueuedBatchEnqueueResult>(
                TryEnqueueWithoutWaiting(batch, ownedSnapshotTime, ownedSnapshotAllocatedBytes, started, onAccepted))
            : EnqueueWithWaitAsync(
                  batch,
                  ownedSnapshotTime,
                  ownedSnapshotAllocatedBytes,
                  started,
                  cancellationToken,
                  onAccepted);
    }

    /// <summary>
    /// Dequeues the next accepted batch or reports queue termination state.
    /// </summary>
    /// <returns>
    /// An item result when a batch is available; otherwise a closed, canceled,
    /// faulted, or disposed result with no batch attached.
    /// </returns>
    public async ValueTask<RadarProcessingOwnedBatchDequeueResult> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Disposed);
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var batch = await channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            RecordDequeue(batch, Stopwatch.GetElapsedTime(started));

            if (IsDisposed)
            {
                return new RadarProcessingOwnedBatchDequeueResult(
                    RadarProcessingOwnedBatchDequeueStatus.Disposed);
            }

            return new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Item,
                batch);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddDequeueWaitTime(Stopwatch.GetElapsedTime(started));
            return new RadarProcessingOwnedBatchDequeueResult(
                RadarProcessingOwnedBatchDequeueStatus.Canceled);
        }
        catch (ChannelClosedException)
        {
            var dequeueWaitTime = Stopwatch.GetElapsedTime(started);
            lock (sync)
            {
                totalDequeueWaitTime += dequeueWaitTime;
                if (disposed)
                {
                    return new RadarProcessingOwnedBatchDequeueResult(
                        RadarProcessingOwnedBatchDequeueStatus.Disposed);
                }

                return faulted
                    ? new RadarProcessingOwnedBatchDequeueResult(
                        RadarProcessingOwnedBatchDequeueStatus.Faulted,
                        message: faultMessage)
                    : new RadarProcessingOwnedBatchDequeueResult(
                        RadarProcessingOwnedBatchDequeueStatus.Closed);
            }
        }
    }

    /// <summary>
    /// Completes the writer side while allowing accepted batches to drain.
    /// </summary>
    public void Close()
    {
        lock (sync)
        {
            if (disposed || closed)
            {
                return;
            }

            closed = true;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }
    }

    /// <summary>
    /// Closes the queue and removes buffered batches that have not been dequeued.
    /// </summary>
    /// <returns>
    /// The canceled queued batches so the owning session can record per-sequence
    /// cancellation results and release retained resources.
    /// </returns>
    public IReadOnlyList<RadarProcessingQueuedBatch> CancelQueued()
    {
        lock (sync)
        {
            if (disposed)
            {
                return Array.Empty<RadarProcessingQueuedBatch>();
            }

            closed = true;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }

        var canceled = new List<RadarProcessingQueuedBatch>();
        while (channel.Reader.TryRead(out var batch))
        {
            RemovePending(batch, countDequeued: false);
            canceled.Add(batch);
        }

        return canceled.Count == 0
            ? Array.Empty<RadarProcessingQueuedBatch>()
            : Array.AsReadOnly(canceled.ToArray());
    }

    /// <summary>
    /// Marks the queue as faulted, closes writers, and wakes blocked producers or consumers.
    /// </summary>
    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            faulted = true;
            closed = true;
            faultMessage = message;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }
    }

    /// <summary>
    /// Creates a telemetry snapshot for queue ownership, pressure, and wait-time evidence.
    /// </summary>
    public RadarProcessingProviderQueueTelemetrySummary CreateTelemetrySummary()
    {
        var recordedSummary = telemetryRecorder.CreateSummary();
        lock (sync)
        {
            return new RadarProcessingProviderQueueTelemetrySummary(
                ownedSnapshotCount,
                ownedSnapshotPayloadBytes,
                ownedSnapshotAllocatedBytes,
                totalOwnedSnapshotTime,
                enqueueAttemptCount,
                enqueuedBatchCount,
                enqueueFullCount,
                enqueueTimedOutCount,
                enqueueCanceledCount,
                enqueueClosedCount,
                enqueueFaultedCount,
                totalEnqueueWaitTime,
                dequeuedBatchCount,
                completedBatchCount: 0,
                failedBatchCount: 0,
                canceledBatchCount: 0,
                skippedAfterFaultCount: 0,
                totalDrainTime: TimeSpan.Zero,
                queueDepthHighWatermark,
                queuedPayloadBytesHighWatermark,
                recordedSummary.OwnedSnapshotPayloadValueCount,
                recordedSummary.TotalProviderToProcessingLatency,
                recordedSummary.RecentDetails,
                recordedSummary.DroppedRecentDetailCount,
                ownedSnapshotEventCount,
                totalDequeueWaitTime,
                new RadarProcessingRetainedResourcePressureSummary(
                    currentPendingRetainedBatchCount: pendingCount,
                    currentPendingRetainedPayloadBytes: pendingPayloadBytes,
                    pendingRetainedBatchCountHighWatermark: queueDepthHighWatermark,
                    pendingRetainedPayloadBytesHighWatermark: queuedPayloadBytesHighWatermark,
                    combinedRetainedBatchCountHighWatermark: queueDepthHighWatermark,
                    combinedRetainedPayloadBytesHighWatermark: queuedPayloadBytesHighWatermark));
        }
    }

    /// <summary>
    /// Disposes the queue, closes the channel, and drops any remaining buffered batches.
    /// </summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            closed = true;
            channel.Writer.TryComplete();
            SignalRetainedByteBudgetChangedUnsafe();
        }

        while (channel.Reader.TryRead(out var batch))
        {
            RemovePending(batch, countDequeued: false);
        }
    }

    private RadarProcessingQueuedBatchEnqueueResult TryEnqueueWithoutWaiting(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime,
        long allocatedBytes,
        long started,
        Action<RadarProcessingQueuedBatch>? onAccepted)
    {
        lock (sync)
        {
            var stateRejection = TryGetStateRejectionUnsafe();
            if (stateRejection.HasValue)
            {
                return RecordRejectedUnsafe(
                    stateRejection.Value.Status,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection.Value.Message);
            }

            var queuedBatch = CreateQueuedBatchUnsafe(batch, ownedSnapshotTime, allocatedBytes);
            if (!HasRetainedByteCapacityUnsafe(queuedBatch.PayloadBytes))
            {
                return RecordRejectedUnsafe(
                    RadarProcessingQueuedBatchEnqueueStatus.Full,
                    Stopwatch.GetElapsedTime(started),
                    CreateRetainedByteBudgetExhaustedMessageUnsafe(queuedBatch.PayloadBytes));
            }

            if (!channel.Writer.TryWrite(queuedBatch))
            {
                stateRejection = TryGetStateRejectionUnsafe();
                if (stateRejection.HasValue)
                {
                    return RecordRejectedUnsafe(
                        stateRejection.Value.Status,
                        Stopwatch.GetElapsedTime(started),
                        stateRejection.Value.Message);
                }

                return RecordRejectedUnsafe(
                    RadarProcessingQueuedBatchEnqueueStatus.Full,
                    Stopwatch.GetElapsedTime(started));
            }

            var accepted = RecordAcceptedUnsafe(queuedBatch, Stopwatch.GetElapsedTime(started));
            onAccepted?.Invoke(queuedBatch);
            return accepted;
        }
    }

    private async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueWithWaitAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime,
        long allocatedBytes,
        long started,
        CancellationToken cancellationToken,
        Action<RadarProcessingQueuedBatch>? onAccepted)
    {
        using var timeout = Options.EnqueueTimeout.HasValue
            ? new CancellationTokenSource(Options.EnqueueTimeout.Value)
            : null;
        using var linked = timeout is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var waitToken = linked?.Token ?? cancellationToken;

        while (true)
        {
            var stateRejection = TryGetStateRejection();
            if (stateRejection.HasValue)
            {
                return RecordRejected(
                    stateRejection.Value.Status,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection.Value.Message);
            }

            bool canWrite;
            try
            {
                canWrite = await channel.Writer.WaitToWriteAsync(waitToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                    Stopwatch.GetElapsedTime(started));
            }
            catch (OperationCanceledException) when (timeout?.IsCancellationRequested == true)
            {
                return RecordRejected(
                    RadarProcessingQueuedBatchEnqueueStatus.TimedOut,
                    Stopwatch.GetElapsedTime(started));
            }

            if (!canWrite)
            {
                stateRejection = TryGetStateRejection();
                return RecordRejected(
                    stateRejection?.Status ?? RadarProcessingQueuedBatchEnqueueStatus.Closed,
                    Stopwatch.GetElapsedTime(started),
                    stateRejection?.Message ?? string.Empty);
            }

            Task? retainedByteBudgetWait = null;
            lock (sync)
            {
                stateRejection = TryGetStateRejectionUnsafe();
                if (stateRejection.HasValue)
                {
                    return RecordRejectedUnsafe(
                        stateRejection.Value.Status,
                        Stopwatch.GetElapsedTime(started),
                        stateRejection.Value.Message);
                }

                var queuedBatch = CreateQueuedBatchUnsafe(batch, ownedSnapshotTime, allocatedBytes);
                if (!HasRetainedByteCapacityUnsafe(queuedBatch.PayloadBytes))
                {
                    retainedByteBudgetWait = retainedByteBudgetChanged.Task;
                }
                else if (channel.Writer.TryWrite(queuedBatch))
                {
                    var accepted = RecordAcceptedUnsafe(queuedBatch, Stopwatch.GetElapsedTime(started));
                    onAccepted?.Invoke(queuedBatch);
                    return accepted;
                }
            }

            if (retainedByteBudgetWait is not null)
            {
                try
                {
                    await retainedByteBudgetWait.WaitAsync(waitToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return RecordRejected(
                        RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                        Stopwatch.GetElapsedTime(started));
                }
                catch (OperationCanceledException) when (timeout?.IsCancellationRequested == true)
                {
                    return RecordRejected(
                        RadarProcessingQueuedBatchEnqueueStatus.TimedOut,
                        Stopwatch.GetElapsedTime(started),
                        CreateRetainedByteBudgetTimedOutMessage(batch.PayloadLength));
                }

                continue;
            }
        }
    }

    private (RadarProcessingQueuedBatchEnqueueStatus Status, string Message)? TryGetStateRejection()
    {
        lock (sync)
        {
            return TryGetStateRejectionUnsafe();
        }
    }

    private (RadarProcessingQueuedBatchEnqueueStatus Status, string Message)? TryGetStateRejectionUnsafe()
    {
        if (faulted)
        {
            return (RadarProcessingQueuedBatchEnqueueStatus.Faulted, faultMessage);
        }

        if (closed || disposed)
        {
            return (RadarProcessingQueuedBatchEnqueueStatus.Closed, string.Empty);
        }

        return null;
    }

    private RadarProcessingQueuedBatch CreateQueuedBatchUnsafe(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime,
        long allocatedBytes) =>
        new(
            new RadarProcessingQueuedBatchSequence(nextSequence),
            batch,
            ownedSnapshotTime,
            allocatedBytes,
            Stopwatch.GetTimestamp());

    private bool HasRetainedByteCapacityUnsafe(
        long payloadBytes)
    {
        if (!Options.MaxRetainedPayloadBytes.HasValue)
        {
            return true;
        }

        return pendingPayloadBytes <= Options.MaxRetainedPayloadBytes.Value - payloadBytes;
    }

    private string? TryCreateOversizedRetainedByteBudgetMessage(
        long payloadBytes)
    {
        if (!Options.MaxRetainedPayloadBytes.HasValue ||
            payloadBytes <= Options.MaxRetainedPayloadBytes.Value)
        {
            return null;
        }

        return $"Queued batch retained payload bytes {payloadBytes} exceed configured retained payload byte budget {Options.MaxRetainedPayloadBytes.Value}.";
    }

    private string CreateRetainedByteBudgetExhaustedMessageUnsafe(
        long payloadBytes) =>
        Options.MaxRetainedPayloadBytes.HasValue
            ? $"Provider queue retained payload byte budget is exhausted. Pending retained payload bytes: {pendingPayloadBytes}; batch payload bytes: {payloadBytes}; budget: {Options.MaxRetainedPayloadBytes.Value}."
            : string.Empty;

    private string CreateRetainedByteBudgetTimedOutMessage(
        long payloadBytes) =>
        Options.MaxRetainedPayloadBytes.HasValue
            ? $"Timed out waiting for provider queue retained payload byte budget. Batch payload bytes: {payloadBytes}; budget: {Options.MaxRetainedPayloadBytes.Value}."
            : string.Empty;

    private RadarProcessingQueuedBatchEnqueueResult RecordAcceptedUnsafe(
        RadarProcessingQueuedBatch batch,
        TimeSpan enqueueWaitTime)
    {
        nextSequence = checked(nextSequence + 1);
        pendingCount++;
        pendingPayloadBytes = checked(pendingPayloadBytes + batch.PayloadBytes);
        ownedSnapshotCount++;
        ownedSnapshotEventCount = checked(ownedSnapshotEventCount + batch.StreamEventCount);
        ownedSnapshotPayloadBytes = checked(ownedSnapshotPayloadBytes + batch.PayloadBytes);
        ownedSnapshotAllocatedBytes = checked(ownedSnapshotAllocatedBytes + batch.OwnedSnapshotAllocatedBytes);
        totalOwnedSnapshotTime += batch.OwnedSnapshotTime;
        enqueueAttemptCount++;
        enqueuedBatchCount++;
        totalEnqueueWaitTime += enqueueWaitTime;
        queueDepthHighWatermark = Math.Max(queueDepthHighWatermark, pendingCount);
        queuedPayloadBytesHighWatermark = Math.Max(queuedPayloadBytesHighWatermark, pendingPayloadBytes);

        var result = RadarProcessingQueuedBatchEnqueueResult.Accepted(batch, enqueueWaitTime);
        telemetryRecorder.RecordEnqueueResult(result, pendingCount, pendingPayloadBytes);
        return result;
    }

    private RadarProcessingQueuedBatchEnqueueResult RecordRejected(
        RadarProcessingQueuedBatchEnqueueStatus status,
        TimeSpan enqueueWaitTime,
        string message = "")
    {
        lock (sync)
        {
            return RecordRejectedUnsafe(status, enqueueWaitTime, message);
        }
    }

    private RadarProcessingQueuedBatchEnqueueResult RecordRejectedUnsafe(
        RadarProcessingQueuedBatchEnqueueStatus status,
        TimeSpan enqueueWaitTime,
        string message = "")
    {
        enqueueAttemptCount++;
        totalEnqueueWaitTime += enqueueWaitTime;
        switch (status)
        {
            case RadarProcessingQueuedBatchEnqueueStatus.Full:
                enqueueFullCount++;
                var full = RadarProcessingQueuedBatchEnqueueResult.Full(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(full, pendingCount, pendingPayloadBytes);
                return full;

            case RadarProcessingQueuedBatchEnqueueStatus.TimedOut:
                enqueueTimedOutCount++;
                var timedOut = RadarProcessingQueuedBatchEnqueueResult.TimedOut(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(timedOut, pendingCount, pendingPayloadBytes);
                return timedOut;

            case RadarProcessingQueuedBatchEnqueueStatus.Canceled:
                enqueueCanceledCount++;
                var canceled = RadarProcessingQueuedBatchEnqueueResult.Canceled(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(canceled, pendingCount, pendingPayloadBytes);
                return canceled;

            case RadarProcessingQueuedBatchEnqueueStatus.Closed:
                enqueueClosedCount++;
                var closed = RadarProcessingQueuedBatchEnqueueResult.Closed(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(closed, pendingCount, pendingPayloadBytes);
                return closed;

            case RadarProcessingQueuedBatchEnqueueStatus.Faulted:
                enqueueFaultedCount++;
                var faulted = RadarProcessingQueuedBatchEnqueueResult.Faulted(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(faulted, pendingCount, pendingPayloadBytes);
                return faulted;

            default:
                RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(status);
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private void RecordDequeue(
        RadarProcessingQueuedBatch batch,
        TimeSpan dequeueWaitTime)
    {
        RemovePending(batch, countDequeued: true, dequeueWaitTime);
        telemetryRecorder.RecordDequeuedBatch(
            batch,
            batch.EnqueuedTimestamp == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(batch.EnqueuedTimestamp),
            PendingCount,
            PendingPayloadBytes,
            dequeueWaitTime);
    }

    private void AddDequeueWaitTime(
        TimeSpan dequeueWaitTime)
    {
        lock (sync)
        {
            totalDequeueWaitTime += dequeueWaitTime;
        }
    }

    private void RemovePending(
        RadarProcessingQueuedBatch batch,
        bool countDequeued,
        TimeSpan dequeueWaitTime = default)
    {
        lock (sync)
        {
            pendingCount--;
            pendingPayloadBytes -= batch.PayloadBytes;
            if (countDequeued)
            {
                dequeuedBatchCount++;
                totalDequeueWaitTime += dequeueWaitTime;
            }

            SignalRetainedByteBudgetChangedUnsafe();
        }
    }

    private void SignalRetainedByteBudgetChangedUnsafe()
    {
        var changed = retainedByteBudgetChanged;
        retainedByteBudgetChanged = CreateRetainedByteBudgetChangedSource();
        changed.TrySetResult(null);
    }

    private static TaskCompletionSource<object?> CreateRetainedByteBudgetChangedSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

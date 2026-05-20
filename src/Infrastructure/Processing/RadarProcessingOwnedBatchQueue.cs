using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

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

    public RadarProcessingProviderQueueOptions Options { get; }

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

    public long PendingRetainedPayloadBytes => PendingPayloadBytes;

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

    public async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default)
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
            return RecordRejected(
                RadarProcessingQueuedBatchEnqueueStatus.Canceled,
                Stopwatch.GetElapsedTime(started));
        }

        var stateRejection = TryGetStateRejection();
        if (stateRejection.HasValue)
        {
            return RecordRejected(
                stateRejection.Value.Status,
                Stopwatch.GetElapsedTime(started),
                stateRejection.Value.Message);
        }

        var oversizedRejection = TryCreateOversizedRetainedByteBudgetMessage(batch.PayloadLength);
        if (oversizedRejection is not null)
        {
            return RecordRejected(
                RadarProcessingQueuedBatchEnqueueStatus.Full,
                Stopwatch.GetElapsedTime(started),
                oversizedRejection);
        }

        return Options.FullMode == RadarProcessingProviderQueueFullMode.ReturnFull
            ? TryEnqueueWithoutWaiting(batch, ownedSnapshotTime, ownedSnapshotAllocatedBytes, started)
            : await EnqueueWithWaitAsync(batch, ownedSnapshotTime, ownedSnapshotAllocatedBytes, started, cancellationToken)
                .ConfigureAwait(false);
    }

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
        long started)
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

            return RecordAcceptedUnsafe(queuedBatch, Stopwatch.GetElapsedTime(started));
        }
    }

    private async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueWithWaitAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime,
        long allocatedBytes,
        long started,
        CancellationToken cancellationToken)
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
                    return RecordAcceptedUnsafe(queuedBatch, Stopwatch.GetElapsedTime(started));
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

using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingOwnedBatchQueue
{
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
        long payloadBytes) =>
        retainedByteBudget.HasCapacity(pendingPayloadBytes, payloadBytes);

    private string? TryCreateOversizedRetainedByteBudgetMessage(
        long payloadBytes) =>
        retainedByteBudget.TryCreateOversizedMessage(payloadBytes);

    private string CreateRetainedByteBudgetExhaustedMessageUnsafe(
        long payloadBytes) =>
        retainedByteBudget.CreateExhaustedMessage(pendingPayloadBytes, payloadBytes);

    private string CreateRetainedByteBudgetTimedOutMessage(
        long payloadBytes) =>
        retainedByteBudget.CreateTimedOutMessage(payloadBytes);


    private void SignalRetainedByteBudgetChangedUnsafe()
    {
        var changed = retainedByteBudgetChanged;
        retainedByteBudgetChanged = CreateRetainedByteBudgetChangedSource();
        changed.TrySetResult(null);
    }

    private static TaskCompletionSource<object?> CreateRetainedByteBudgetChangedSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingOwnedBatchQueue
{
    private RadarProcessingQueuedBatchEnqueueResult RecordAcceptedUnsafe(
        RadarProcessingQueuedBatch batch,
        TimeSpan enqueueWaitTime)
    {
        nextSequence = checked(nextSequence + 1);
        pendingCount++;
        pendingPayloadBytes = checked(pendingPayloadBytes + batch.PayloadBytes);
        counters.RecordAccepted(batch, enqueueWaitTime, pendingCount, pendingPayloadBytes);

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
        counters.RecordRejected(status, enqueueWaitTime);
        switch (status)
        {
            case RadarProcessingQueuedBatchEnqueueStatus.Full:
                var full = RadarProcessingQueuedBatchEnqueueResult.Full(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(full, pendingCount, pendingPayloadBytes);
                return full;

            case RadarProcessingQueuedBatchEnqueueStatus.TimedOut:
                var timedOut = RadarProcessingQueuedBatchEnqueueResult.TimedOut(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(timedOut, pendingCount, pendingPayloadBytes);
                return timedOut;

            case RadarProcessingQueuedBatchEnqueueStatus.Canceled:
                var canceled = RadarProcessingQueuedBatchEnqueueResult.Canceled(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(canceled, pendingCount, pendingPayloadBytes);
                return canceled;

            case RadarProcessingQueuedBatchEnqueueStatus.Closed:
                var closed = RadarProcessingQueuedBatchEnqueueResult.Closed(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(closed, pendingCount, pendingPayloadBytes);
                return closed;

            case RadarProcessingQueuedBatchEnqueueStatus.Faulted:
                var faulted = RadarProcessingQueuedBatchEnqueueResult.Faulted(enqueueWaitTime, message);
                telemetryRecorder.RecordEnqueueResult(faulted, pendingCount, pendingPayloadBytes);
                return faulted;

            default:
                RadarProcessingQueuedBatchEnqueueResult.EnsureKnownStatus(status);
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}

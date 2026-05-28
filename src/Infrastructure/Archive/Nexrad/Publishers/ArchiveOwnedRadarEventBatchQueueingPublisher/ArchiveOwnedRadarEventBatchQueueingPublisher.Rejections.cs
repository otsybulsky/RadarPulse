using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
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

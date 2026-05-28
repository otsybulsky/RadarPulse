using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    /// <inheritdoc />
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

    /// <summary>
    /// Closes the underlying processing queue to additional batches.
}

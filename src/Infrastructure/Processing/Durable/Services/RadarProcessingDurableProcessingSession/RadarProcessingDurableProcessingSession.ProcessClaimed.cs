using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Computes processing output for a claimed envelope and records the durable state.
    /// </summary>
    public async ValueTask<RadarProcessingDurableQueueOperationResult> ProcessClaimedAsync(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(claimedEnvelope);

        var completion = await ComputeCompletionAsync(claimedEnvelope, cancellationToken)
            .ConfigureAwait(false);
        var operation = ApplyCompletionState(completion);
        if (operation.Status is RadarProcessingDurableQueueOperationStatus.Completed or
            RadarProcessingDurableQueueOperationStatus.Failed or
            RadarProcessingDurableQueueOperationStatus.Canceled)
        {
            AddCompletion(completion);
        }
        else
        {
            completion.Dispose();
        }

        return operation;
    }
}

using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Claims, processes, and commits durable envelopes until no pending work remains.
    /// </summary>
    public async ValueTask<RadarProcessingDurableProcessingSessionResult> DrainAsync(
        string workerId = "durable-local-worker",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(workerId);

        try
        {
            while (!IsFaulted && !IsCanceled)
            {
                var claim = queue.ClaimNext(workerId);
                if (claim.Status == RadarProcessingDurableQueueOperationStatus.Empty)
                {
                    break;
                }

                if (claim.Status != RadarProcessingDurableQueueOperationStatus.Claimed)
                {
                    MarkFaulted(claim.Message);
                    break;
                }

                await ProcessClaimedAsync(claim.ClaimedEnvelope!, cancellationToken)
                    .ConfigureAwait(false);
                CommitReady(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            MarkCanceled();
        }

        return CreateResult();
    }
}

using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingDurableProcessingSession
{
    /// <summary>
    /// Rebuilds pending commit work for envelopes persisted as completed.
    /// </summary>
    /// <returns>The number of completed envelopes staged for ordered commit.</returns>
    public async ValueTask<int> RecoverCompletedAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var recovered = 0;
        foreach (var snapshot in queue.CreateSnapshots())
        {
            if (snapshot.State != RadarProcessingDurableEnvelopeState.Completed)
            {
                continue;
            }

            lock (sync)
            {
                if (faulted || canceled || pendingCompletions.ContainsKey(snapshot.ProviderSequence.Value))
                {
                    continue;
                }
            }

            if (!queue.TryGetQueuedBatch(snapshot.BatchId, out var queuedBatch))
            {
                MarkFaulted($"Durable envelope '{snapshot.BatchId}' was not found during recovery.");
                break;
            }

            var completion = await ComputeCompletionAsync(
                    snapshot.BatchId,
                    queuedBatch!,
                    cancellationToken)
                .ConfigureAwait(false);
            AddCompletion(completion);
            recovered++;
        }

        return recovered;
    }
}

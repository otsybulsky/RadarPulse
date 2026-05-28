using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedRebalanceSession
{
    /// <summary>
    /// Computes rebalance batches concurrently while committing results by provider sequence.
    /// </summary>
    /// <remarks>
    /// Ordered commit may recompute stale deltas when an earlier committed
    /// migration changes topology before a later batch is published.
    /// </remarks>
    public async ValueTask<RadarProcessingQueuedSessionResult> DrainOrderedConcurrentAsync(
        RadarProcessingOrderedConcurrencyOptions? orderedOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var effectiveOptions = orderedOptions ?? RadarProcessingOrderedConcurrencyOptions.Default;
        if (effectiveOptions.IsSequential)
        {
            return await DrainAsync(cancellationToken).ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        using var activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var active = new List<OrderedConcurrentRebalanceBatchWork>(effectiveOptions.ActiveBatchCapacity);
        var completed = new List<OrderedConcurrentRebalanceBatchCompletion>(effectiveOptions.ActiveBatchCapacity);
        var inputClosed = false;
        var nextPublishSequence = -1L;

        try
        {
            while (!inputClosed || active.Count > 0 || completed.Count > 0)
            {
                while (!inputClosed && active.Count < effectiveOptions.ActiveBatchCapacity)
                {
                    var dequeue = await queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                    switch (dequeue.Status)
                    {
                        case RadarProcessingOwnedBatchDequeueStatus.Item:
                            var queuedBatch = dequeue.Batch!;
                            if (nextPublishSequence < 0)
                            {
                                nextPublishSequence = queuedBatch.Sequence.Value;
                            }

                            if (IsFaulted)
                            {
                                completed.Add(CreateSkippedAfterFaultCompletion(queuedBatch));
                            }
                            else
                            {
                                active.Add(StartOrderedConcurrentBatch(queuedBatch, activeCancellation.Token));
                            }

                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Closed:
                            inputClosed = true;
                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Faulted:
                            MarkFaulted(dequeue.Message);
                            inputClosed = true;
                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Canceled:
                            activeCancellation.Cancel();
                            MarkCanceledAndRecordQueued();
                            inputClosed = true;
                            break;

                        case RadarProcessingOwnedBatchDequeueStatus.Disposed:
                            AddDrainTime(started);
                            return CreateSessionResult(
                                RadarProcessingQueuedSessionStatus.Disposed,
                                "Queued rebalance queue was disposed.");

                        default:
                            RadarProcessingOwnedBatchDequeueResult.EnsureKnownStatus(dequeue.Status);
                            throw new ArgumentOutOfRangeException(nameof(dequeue));
                    }
                }

                PublishReadyOrderedCompletions(completed, ref nextPublishSequence, activeCancellation);
                if (active.Count == 0)
                {
                    if (inputClosed && completed.Count == 0)
                    {
                        break;
                    }

                    continue;
                }

                var completedTask = await Task.WhenAny(active.Select(static item => item.Task)).ConfigureAwait(false);
                var activeIndex = FindActiveWorkIndex(active, completedTask);
                var activeWork = active[activeIndex];
                active.RemoveAt(activeIndex);
                completed.Add(await activeWork.Task.ConfigureAwait(false));
                PublishReadyOrderedCompletions(completed, ref nextPublishSequence, activeCancellation);
            }

            AddDrainTime(started);
            return CreateSessionResult(GetTerminalStatus(), GetTerminalMessage());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activeCancellation.Cancel();
            await CompleteAndDiscardActiveWorkAsync(active).ConfigureAwait(false);
            MarkCanceledAndRecordQueued();
            AddDrainTime(started);
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued rebalance drain was canceled.");
        }
        finally
        {
            DisposeCompleted(completed);
        }
    }
}

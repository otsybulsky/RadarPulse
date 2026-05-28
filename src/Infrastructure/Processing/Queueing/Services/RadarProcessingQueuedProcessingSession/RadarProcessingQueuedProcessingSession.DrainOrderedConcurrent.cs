using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
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
        var active = new List<OrderedConcurrentBatchWork>(effectiveOptions.ActiveBatchCapacity);
        var completed = new List<OrderedConcurrentBatchCompletion>(effectiveOptions.ActiveBatchCapacity);
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
                                "Queued processing queue was disposed.");

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
            return CreateSessionResult(RadarProcessingQueuedSessionStatus.Canceled, "Queued processing drain was canceled.");
        }
        finally
        {
            DisposeCompleted(completed);
        }
    }

    /// <summary>
    /// Computes handler deltas concurrently and merges them in provider sequence order.
    /// </summary>
    /// <remarks>
    /// This path requires a mergeable handler output contract and preserves the
    /// same ordered commit semantics as the standard ordered concurrent drain.
}

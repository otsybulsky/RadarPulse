using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingQueuedProcessingSession
{
    private void PublishReadyOrderedCompletions(
        List<OrderedConcurrentBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation)
    {
        if (nextPublishSequence < 0)
        {
            return;
        }

        while (true)
        {
            var index = FindCompletionIndex(completed, nextPublishSequence);
            if (index < 0)
            {
                return;
            }

            var completion = completed[index];
            completed.RemoveAt(index);
            try
            {
                var result = IsFaulted
                    ? RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        completion.Sequence,
                        faultMessage)
                    : completion.Commit(core, activeCancellation.Token);
                RecordProcessingResult(result);
                nextPublishSequence++;

                if (IsFailedProcessingStatus(result.Status))
                {
                    MarkFaulted(result.Message);
                    activeCancellation.Cancel();
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    activeCancellation.Cancel();
                    MarkCanceledAndRecordQueued();
                }
            }
            finally
            {
                completion.Dispose();
            }
        }
    }

    private void PublishReadyHandlerDeltaMergeCompletions(
        List<OrderedConcurrentBatchCompletion> completed,
        ref long nextPublishSequence,
        CancellationTokenSource activeCancellation,
        IReadOnlyDictionary<string, RadarProcessingHandlerDeltaMergeCoordinator> handlerMergeCoordinators)
    {
        if (nextPublishSequence < 0)
        {
            return;
        }

        while (true)
        {
            var index = FindCompletionIndex(completed, nextPublishSequence);
            if (index < 0)
            {
                return;
            }

            var completion = completed[index];
            completed.RemoveAt(index);
            try
            {
                var result = IsFaulted
                    ? RadarProcessingQueuedBatchProcessingResult.SkippedAfterFault(
                        completion.Sequence,
                        faultMessage)
                    : completion.Commit(core, activeCancellation.Token, handlerMergeCoordinators);
                RecordProcessingResult(result);
                nextPublishSequence++;

                if (IsFailedProcessingStatus(result.Status))
                {
                    MarkFaulted(result.Message);
                    activeCancellation.Cancel();
                }
                else if (result.Status == RadarProcessingQueuedBatchProcessingStatus.Canceled)
                {
                    activeCancellation.Cancel();
                    MarkCanceledAndRecordQueued();
                }
            }
            finally
            {
                completion.Dispose();
            }
        }
    }

}

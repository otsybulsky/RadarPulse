using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Coordinates out-of-order batch completions into provider-sequence publication order.
/// </summary>
/// <remarks>
/// Once a terminal failure has published, successful later results are held back
/// while cancellation or skipped-after-fault results may still publish to
/// complete observable session evidence.
/// </remarks>
public sealed class RadarProcessingOrderedResultCoordinator
{
    private readonly object sync = new();
    private readonly SortedDictionary<long, RadarProcessingQueuedBatchProcessingResult> pending = [];
    private long nextPublishSequence;
    private bool terminalFailurePublished;

    /// <summary>
    /// Next provider sequence required for publication.
    /// </summary>
    public long NextPublishSequence
    {
        get
        {
            lock (sync)
            {
                return nextPublishSequence;
            }
        }
    }

    /// <summary>
    /// Number of completed results waiting for their publish sequence.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (sync)
            {
                return pending.Count;
            }
        }
    }

    /// <summary>
    /// Indicates whether a terminal failure has already been published.
    /// </summary>
    public bool HasPublishedTerminalFailure
    {
        get
        {
            lock (sync)
            {
                return terminalFailurePublished;
            }
        }
    }

    /// <summary>
    /// Indicates whether a successful result is blocked behind a published terminal failure.
    /// </summary>
    public bool IsBlockedByTerminalFailure
    {
        get
        {
            lock (sync)
            {
                return terminalFailurePublished &&
                       pending.TryGetValue(nextPublishSequence, out var result) &&
                       result.Status == RadarProcessingQueuedBatchProcessingStatus.Succeeded;
            }
        }
    }

    /// <summary>
    /// Adds a completion and returns any now-publishable results in order.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> Complete(
        RadarProcessingQueuedBatchProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (sync)
        {
            var sequence = result.Sequence.Value;
            if (sequence < nextPublishSequence)
            {
                throw new InvalidOperationException(
                    $"Queued processing sequence {sequence} has already been published.");
            }

            if (!pending.TryAdd(sequence, result))
            {
                throw new InvalidOperationException(
                    $"Queued processing sequence {sequence} has already completed.");
            }

            return PublishAvailableUnsafe();
        }
    }

    private IReadOnlyList<RadarProcessingQueuedBatchProcessingResult> PublishAvailableUnsafe()
    {
        List<RadarProcessingQueuedBatchProcessingResult>? published = null;
        while (pending.TryGetValue(nextPublishSequence, out var result))
        {
            if (!CanPublishAfterTerminalFailure(result))
            {
                break;
            }

            pending.Remove(nextPublishSequence);
            nextPublishSequence++;
            published ??= [];
            published.Add(result);

            if (IsTerminalFailure(result.Status))
            {
                terminalFailurePublished = true;
            }
        }

        return published is null
            ? Array.Empty<RadarProcessingQueuedBatchProcessingResult>()
            : Array.AsReadOnly(published.ToArray());
    }

    private bool CanPublishAfterTerminalFailure(
        RadarProcessingQueuedBatchProcessingResult result) =>
        !terminalFailurePublished ||
        result.Status is RadarProcessingQueuedBatchProcessingStatus.Canceled or
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault;

    private static bool IsTerminalFailure(
        RadarProcessingQueuedBatchProcessingStatus status) =>
        status is RadarProcessingQueuedBatchProcessingStatus.FailedProcessing or
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation or
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration;
}

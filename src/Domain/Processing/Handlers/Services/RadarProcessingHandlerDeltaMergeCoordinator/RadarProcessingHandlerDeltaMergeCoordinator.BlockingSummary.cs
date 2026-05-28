namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingHandlerDeltaMergeCoordinator
{
    private IReadOnlyList<RadarProcessingHandlerDeltaValue> CreateMergedValuesSnapshot() =>
        accumulator is null
            ? mergedValues
            : CopyValues(accumulator.CreateMergedValuesSnapshot());

    private RadarProcessingQueuedBatchSequence? CreateFirstBlockingSequence()
    {
        if (permanentBlockingSequence is not null)
        {
            return permanentBlockingSequence;
        }

        if (pendingBySequence.Count == 0)
        {
            return null;
        }

        return nextProviderSequence;
    }

    private string CreateFirstBlockingReason()
    {
        if (permanentBlockingReason.Length != 0)
        {
            return permanentBlockingReason;
        }

        return pendingBySequence.Count == 0
            ? string.Empty
            : $"Waiting for handler delta at provider sequence {nextProviderSequence.Value}.";
    }

    private void Block(
        RadarProcessingQueuedBatchSequence sequence,
        string reason)
    {
        permanentBlockingSequence = sequence;
        permanentBlockingReason = string.IsNullOrWhiteSpace(reason)
            ? "Handler delta merge is blocked."
            : reason;
    }
}

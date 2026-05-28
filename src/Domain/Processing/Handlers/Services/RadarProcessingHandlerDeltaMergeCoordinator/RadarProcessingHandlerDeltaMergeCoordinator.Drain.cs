namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingHandlerDeltaMergeCoordinator
{
    private string ValidateDelta(
        RadarProcessingHandlerDelta delta)
    {
        if (!string.Equals(delta.HandlerName, merger.HandlerName, StringComparison.Ordinal))
        {
            return $"Handler delta name '{delta.HandlerName}' does not match merger '{merger.HandlerName}'.";
        }

        if (!string.Equals(delta.HandlerContractVersion, merger.HandlerContractVersion, StringComparison.Ordinal))
        {
            return "Handler delta contract version does not match the merger contract version.";
        }

        if (delta.SchemaVersion != RadarProcessingHandlerDelta.CurrentSchemaVersion)
        {
            return "Handler delta schema version is not supported by the merge coordinator.";
        }

        return string.Empty;
    }

    private DrainReadyDeltasResult DrainReadyDeltas()
    {
        var appliedCount = 0;
        IReadOnlyList<RadarProcessingHandlerDeltaValue> appliedValues =
            Array.Empty<RadarProcessingHandlerDeltaValue>();
        List<RadarProcessingHandlerDeltaValue>? combinedAppliedValues = null;
        while (pendingBySequence.Remove(nextProviderSequence.Value, out var delta))
        {
            if (appliedCount > 0 &&
                appliedValues.Count != 0 &&
                combinedAppliedValues is null)
            {
                combinedAppliedValues = new List<RadarProcessingHandlerDeltaValue>(appliedValues.Count);
                combinedAppliedValues.AddRange(appliedValues);
                appliedValues = Array.Empty<RadarProcessingHandlerDeltaValue>();
            }

            IReadOnlyList<RadarProcessingHandlerDeltaValue> deltaAppliedValues;
            try
            {
                deltaAppliedValues = MergeDelta(delta);
            }
            catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
            {
                Block(delta.ProviderSequence, exception.Message);
                break;
            }

            if (deltaAppliedValues.Count != 0)
            {
                if (combinedAppliedValues is null)
                {
                    appliedValues = deltaAppliedValues;
                }
                else
                {
                    combinedAppliedValues.AddRange(deltaAppliedValues);
                }
            }

            completedById.Add(delta.DeltaId, delta);
            appliedCount++;
            nextProviderSequence = nextProviderSequence.Next();
        }

        return new DrainReadyDeltasResult(
            appliedCount,
            combinedAppliedValues ?? appliedValues);
    }

    private IReadOnlyList<RadarProcessingHandlerDeltaValue> MergeDelta(
        RadarProcessingHandlerDelta delta)
    {
        if (accumulator is not null)
        {
            return accumulator.Merge(delta);
        }

        mergedValues = CopyValues(merger.Merge(mergedValues, delta));
        return mergedValues;
    }
}

namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingHandlerDeltaMergeCoordinator
{
    private static IReadOnlyList<RadarProcessingHandlerDeltaValue> CopyValues(
        IReadOnlyList<RadarProcessingHandlerDeltaValue>? values) =>
        values is null || values.Count == 0
            ? Array.Empty<RadarProcessingHandlerDeltaValue>()
            : Array.AsReadOnly(values.ToArray());

    private static bool AreEquivalent(
        RadarProcessingHandlerDelta first,
        RadarProcessingHandlerDelta second) =>
        string.Equals(first.HandlerName, second.HandlerName, StringComparison.Ordinal) &&
        string.Equals(first.HandlerContractVersion, second.HandlerContractVersion, StringComparison.Ordinal) &&
        first.ProviderSequence == second.ProviderSequence &&
        first.DurableBatchId == second.DurableBatchId &&
        first.EventCount == second.EventCount &&
        first.SourceCount == second.SourceCount &&
        first.PayloadValueCount == second.PayloadValueCount &&
        first.InputChecksum == second.InputChecksum &&
        first.DeltaId == second.DeltaId &&
        first.SchemaVersion == second.SchemaVersion &&
        first.Values.SequenceEqual(second.Values);

    private readonly record struct DrainReadyDeltasResult(
        int AppliedDeltaCount,
        IReadOnlyList<RadarProcessingHandlerDeltaValue> AppliedValues);
}

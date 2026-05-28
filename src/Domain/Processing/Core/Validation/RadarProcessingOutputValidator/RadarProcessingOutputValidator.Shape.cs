namespace RadarPulse.Domain.Processing;

public static partial class RadarProcessingOutputValidator
{
    private static void EnsureSnapshotShape(
        IReadOnlyList<RadarSourceProcessingSnapshot> snapshots,
        string paramName)
    {
        for (var sourceId = 0; sourceId < snapshots.Count; sourceId++)
        {
            if (snapshots[sourceId].SourceId != sourceId)
            {
                throw new ArgumentException(
                    "Processing snapshots must be ordered by SourceId.",
                    paramName);
            }
        }
    }

    private static RadarProcessingValidationResult Invalid(
        RadarProcessingValidationError error,
        int sourceId,
        int eventIndex,
        string message) =>
        RadarProcessingValidationResult.Invalid(error, sourceId, eventIndex, message);
}

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Compares partition state snapshots around a rebalance owner handoff.
/// </summary>
public static class RadarProcessingStateHandoffValidator
{
    /// <summary>
    /// Validates that ownership handoff did not mutate partition processing state.
    /// </summary>
    /// <returns>
    /// Valid result when the partition identity, source range, counters, and checksum
    /// lanes match; otherwise the first mismatch category.
    /// </returns>
    public static RadarProcessingStateHandoffValidationResult Validate(
        RadarProcessingPartitionStateSnapshot beforeSnapshot,
        RadarProcessingPartitionStateSnapshot afterSnapshot)
    {
        ArgumentNullException.ThrowIfNull(beforeSnapshot);
        ArgumentNullException.ThrowIfNull(afterSnapshot);

        if (beforeSnapshot.PartitionId != afterSnapshot.PartitionId)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.PartitionIdMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.SourceIdStart != afterSnapshot.SourceIdStart ||
            beforeSnapshot.SourceIdEndExclusive != afterSnapshot.SourceIdEndExclusive)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.SourceRangeMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.ActiveSourceCount != afterSnapshot.ActiveSourceCount)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.ActiveSourceCountMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.ProcessedEventCount != afterSnapshot.ProcessedEventCount)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.ProcessedEventCountMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.ProcessedPayloadValueCount != afterSnapshot.ProcessedPayloadValueCount)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.ProcessedPayloadValueCountMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.RawValueChecksum != afterSnapshot.RawValueChecksum)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.RawValueChecksumMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.Checksum.ProcessingChecksum != afterSnapshot.Checksum.ProcessingChecksum)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.ProcessingChecksumMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.Checksum.LastMessageTimestampChecksum !=
            afterSnapshot.Checksum.LastMessageTimestampChecksum)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.LastMessageTimestampChecksumMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        if (beforeSnapshot.Checksum.HandlerSnapshotChecksum != afterSnapshot.Checksum.HandlerSnapshotChecksum)
        {
            return Invalid(
                RadarProcessingStateHandoffValidationError.HandlerSnapshotChecksumMismatch,
                beforeSnapshot,
                afterSnapshot);
        }

        return RadarProcessingStateHandoffValidationResult.Valid(
            beforeSnapshot,
            afterSnapshot);
    }

    private static RadarProcessingStateHandoffValidationResult Invalid(
        RadarProcessingStateHandoffValidationError error,
        RadarProcessingPartitionStateSnapshot beforeSnapshot,
        RadarProcessingPartitionStateSnapshot afterSnapshot) =>
        RadarProcessingStateHandoffValidationResult.Invalid(
            error,
            beforeSnapshot,
            afterSnapshot);
}

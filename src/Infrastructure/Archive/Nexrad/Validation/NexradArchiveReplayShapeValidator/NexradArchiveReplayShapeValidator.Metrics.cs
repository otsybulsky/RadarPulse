using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeValidator
{
    private static ArchiveTwoReplayShapeValidationMetrics ToValidationMetrics(
        ArchiveTwoReplayShapeBenchmarkResult result) =>
        new(
            result.CompressedRecordsPerIteration,
            result.CompressedBytesPerIteration,
            result.DecompressedBytesPerIteration,
            result.EventsPerIteration,
            result.ValidEventsPerIteration,
            result.BelowThresholdEventsPerIteration,
            result.RangeFoldedEventsPerIteration,
            result.ClutterFilterNotAppliedEventsPerIteration,
            result.PointClutterFilterAppliedEventsPerIteration,
            result.DualPolarizationFilteredEventsPerIteration,
            result.ReservedEventsPerIteration,
            result.UnsupportedEventsPerIteration,
            result.RawValueChecksumPerIteration,
            result.CalibratedValueScaledChecksumPerIteration,
            result.ChronologyChecksumPerIteration);

    private static ArchiveTwoReplayShapeValidationMetrics EmptyMetrics() =>
        new(
            CompressedRecordCount: 0,
            CompressedBytes: 0,
            DecompressedBytes: 0,
            Events: 0,
            ValidEvents: 0,
            BelowThresholdEvents: 0,
            RangeFoldedEvents: 0,
            ClutterFilterNotAppliedEvents: 0,
            PointClutterFilterAppliedEvents: 0,
            DualPolarizationFilteredEvents: 0,
            ReservedEvents: 0,
            UnsupportedEvents: 0,
            RawValueChecksum: 0,
            CalibratedValueScaledChecksum: 0,
            ChronologyChecksum: 0);

    private static string? CompareMetrics(
        ArchiveTwoReplayShapeValidationMetrics sequential,
        ArchiveTwoReplayShapeValidationMetrics parallel)
    {
        if (sequential.CompressedRecordCount != parallel.CompressedRecordCount)
        {
            return $"Compressed record count mismatch: sequential={sequential.CompressedRecordCount}, parallel={parallel.CompressedRecordCount}.";
        }

        if (sequential.CompressedBytes != parallel.CompressedBytes)
        {
            return $"Compressed byte count mismatch: sequential={sequential.CompressedBytes}, parallel={parallel.CompressedBytes}.";
        }

        if (sequential.DecompressedBytes != parallel.DecompressedBytes)
        {
            return $"Decompressed byte count mismatch: sequential={sequential.DecompressedBytes}, parallel={parallel.DecompressedBytes}.";
        }

        if (sequential.Events != parallel.Events)
        {
            return $"Event count mismatch: sequential={sequential.Events}, parallel={parallel.Events}.";
        }

        if (sequential.ValidEvents != parallel.ValidEvents)
        {
            return $"Valid event count mismatch: sequential={sequential.ValidEvents}, parallel={parallel.ValidEvents}.";
        }

        if (sequential.BelowThresholdEvents != parallel.BelowThresholdEvents ||
            sequential.RangeFoldedEvents != parallel.RangeFoldedEvents ||
            sequential.ClutterFilterNotAppliedEvents != parallel.ClutterFilterNotAppliedEvents ||
            sequential.PointClutterFilterAppliedEvents != parallel.PointClutterFilterAppliedEvents ||
            sequential.DualPolarizationFilteredEvents != parallel.DualPolarizationFilteredEvents ||
            sequential.ReservedEvents != parallel.ReservedEvents ||
            sequential.UnsupportedEvents != parallel.UnsupportedEvents)
        {
            return "Status-count mismatch between sequential and parallel replay-shape projection.";
        }

        if (sequential.RawValueChecksum != parallel.RawValueChecksum)
        {
            return $"Raw checksum mismatch: sequential={sequential.RawValueChecksum}, parallel={parallel.RawValueChecksum}.";
        }

        if (sequential.CalibratedValueScaledChecksum != parallel.CalibratedValueScaledChecksum)
        {
            return $"Calibrated checksum mismatch: sequential={sequential.CalibratedValueScaledChecksum}, parallel={parallel.CalibratedValueScaledChecksum}.";
        }

        if (sequential.ChronologyChecksum != parallel.ChronologyChecksum)
        {
            return $"Chronology checksum mismatch: sequential={sequential.ChronologyChecksum}, parallel={parallel.ChronologyChecksum}.";
        }

        return null;
    }
}

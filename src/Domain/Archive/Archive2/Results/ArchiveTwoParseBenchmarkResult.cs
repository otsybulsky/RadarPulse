namespace RadarPulse.Domain.Archive;

public sealed record ArchiveTwoParseBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    bool DecodeMomentValues,
    bool DecodeCalibratedMomentValues,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    int MessagesPerIteration,
    int Type31RadialsPerIteration,
    long EstimatedGateMomentEventsPerIteration,
    long DecodedGateMomentValuesPerIteration,
    ulong DecodedGateMomentValueChecksumPerIteration,
    long CalibratedGateMomentValuesPerIteration,
    long BelowThresholdGateMomentValuesPerIteration,
    long RangeFoldedGateMomentValuesPerIteration,
    long ClutterFilterNotAppliedGateMomentValuesPerIteration,
    long PointClutterFilterAppliedGateMomentValuesPerIteration,
    long DualPolarizationFilteredGateMomentValuesPerIteration,
    long ReservedGateMomentValuesPerIteration,
    long UnsupportedCalibratedGateMomentValuesPerIteration,
    long CalibratedGateMomentValueScaledChecksumPerIteration,
    double MinimumCalibratedGateMomentValuePerIteration,
    double MaximumCalibratedGateMomentValuePerIteration,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalMessages => (long)MessagesPerIteration * Iterations;

    public long TotalType31Radials => (long)Type31RadialsPerIteration * Iterations;

    public long TotalEstimatedGateMomentEvents => EstimatedGateMomentEventsPerIteration * Iterations;

    public long TotalDecodedGateMomentValues => DecodedGateMomentValuesPerIteration * Iterations;

    public long TotalCalibratedGateMomentValues => CalibratedGateMomentValuesPerIteration * Iterations;

    public long TotalBelowThresholdGateMomentValues => BelowThresholdGateMomentValuesPerIteration * Iterations;

    public long TotalRangeFoldedGateMomentValues => RangeFoldedGateMomentValuesPerIteration * Iterations;

    public long TotalClutterFilterNotAppliedGateMomentValues => ClutterFilterNotAppliedGateMomentValuesPerIteration * Iterations;

    public long TotalPointClutterFilterAppliedGateMomentValues => PointClutterFilterAppliedGateMomentValuesPerIteration * Iterations;

    public long TotalDualPolarizationFilteredGateMomentValues => DualPolarizationFilteredGateMomentValuesPerIteration * Iterations;

    public long TotalReservedGateMomentValues => ReservedGateMomentValuesPerIteration * Iterations;

    public long TotalUnsupportedCalibratedGateMomentValues => UnsupportedCalibratedGateMomentValuesPerIteration * Iterations;
}

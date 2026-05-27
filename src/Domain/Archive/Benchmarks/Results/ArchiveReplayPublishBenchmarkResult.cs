namespace RadarPulse.Domain.Archive;

public sealed record ArchiveReplayPublishBenchmarkResult(
    string FilePath,
    string Decompressor,
    int Iterations,
    int WarmupIterations,
    int DegreeOfParallelism,
    long FileSizeBytes,
    int CompressedRecordsPerIteration,
    long CompressedBytesPerIteration,
    long DecompressedBytesPerIteration,
    long PublishedEventsPerIteration,
    long ValidEventsPerIteration,
    long BelowThresholdEventsPerIteration,
    long RangeFoldedEventsPerIteration,
    long ClutterFilterNotAppliedEventsPerIteration,
    long PointClutterFilterAppliedEventsPerIteration,
    long DualPolarizationFilteredEventsPerIteration,
    long ReservedEventsPerIteration,
    long UnsupportedEventsPerIteration,
    long RawValueChecksumPerIteration,
    long CalibratedValueScaledChecksumPerIteration,
    ulong ChronologyChecksumPerIteration,
    TimeSpan Elapsed,
    long AllocatedBytes)
{
    public long TotalCompressedRecords => (long)CompressedRecordsPerIteration * Iterations;

    public long TotalCompressedBytes => CompressedBytesPerIteration * Iterations;

    public long TotalDecompressedBytes => DecompressedBytesPerIteration * Iterations;

    public long TotalPublishedEvents => PublishedEventsPerIteration * Iterations;

    public long TotalValidEvents => ValidEventsPerIteration * Iterations;

    public long TotalBelowThresholdEvents => BelowThresholdEventsPerIteration * Iterations;

    public long TotalRangeFoldedEvents => RangeFoldedEventsPerIteration * Iterations;

    public long TotalClutterFilterNotAppliedEvents => ClutterFilterNotAppliedEventsPerIteration * Iterations;

    public long TotalPointClutterFilterAppliedEvents => PointClutterFilterAppliedEventsPerIteration * Iterations;

    public long TotalDualPolarizationFilteredEvents => DualPolarizationFilteredEventsPerIteration * Iterations;

    public long TotalReservedEvents => ReservedEventsPerIteration * Iterations;

    public long TotalUnsupportedEvents => UnsupportedEventsPerIteration * Iterations;
}

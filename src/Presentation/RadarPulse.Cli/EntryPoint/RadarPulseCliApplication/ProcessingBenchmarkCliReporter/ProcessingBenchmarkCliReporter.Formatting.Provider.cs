using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;
using static CliFormat;

internal static partial class ProcessingBenchmarkCliReporter
{
    public static string FormatProcessingArchiveProviderMode(RadarProcessingArchiveProviderMode providerMode) =>
        providerMode switch
        {
            RadarProcessingArchiveProviderMode.BlockingBorrowed => "blocking-borrowed",
            RadarProcessingArchiveProviderMode.QueuedOwned => "queued-owned",
            _ => providerMode.ToString()
        };

    public static string FormatProcessingProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode providerOverlapMode) =>
        providerOverlapMode switch
        {
            RadarProcessingQueuedProviderOverlapMode.None => "none",
            RadarProcessingQueuedProviderOverlapMode.ProducerConsumer => "producer-consumer",
            _ => providerOverlapMode.ToString()
        };

    public static string FormatProcessingRetentionStrategy(RadarProcessingRetainedPayloadStrategy retentionStrategy) =>
        retentionStrategy switch
        {
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy => "snapshot-copy",
            RadarProcessingRetainedPayloadStrategy.PooledCopy => "pooled-copy",
            RadarProcessingRetainedPayloadStrategy.BuilderTransfer => "builder-transfer",
            _ => retentionStrategy.ToString()
        };

    public static string FormatProcessingBenchmarkApplicableOptionValueSource(
        ProcessingBenchmarkOptionValueSource source,
        bool isApplicable) =>
        isApplicable
            ? FormatProcessingBenchmarkOptionValueSource(source)
            : "not-applicable";

    public static string FormatProcessingBenchmarkOptionValueSource(ProcessingBenchmarkOptionValueSource source) =>
        source switch
        {
            ProcessingBenchmarkOptionValueSource.CurrentDefault => "current-default",
            ProcessingBenchmarkOptionValueSource.Explicit => "explicit",
            ProcessingBenchmarkOptionValueSource.RolloutDefault => "rollout-default",
            _ => source.ToString()
        };

    public static string FormatProviderQueueTelemetryOutput(ProcessingBenchmarkProviderQueueTelemetryOutput output) =>
        output switch
        {
            ProcessingBenchmarkProviderQueueTelemetryOutput.None => "none",
            ProcessingBenchmarkProviderQueueTelemetryOutput.Summary => "summary",
            ProcessingBenchmarkProviderQueueTelemetryOutput.Recent => "recent",
            _ => output.ToString()
        };

    public static string FormatProviderOverlapTelemetryOutput(ProcessingBenchmarkProviderOverlapTelemetryOutput output) =>
        output switch
        {
            ProcessingBenchmarkProviderOverlapTelemetryOutput.None => "none",
            ProcessingBenchmarkProviderOverlapTelemetryOutput.Summary => "summary",
            ProcessingBenchmarkProviderOverlapTelemetryOutput.Recent => "recent",
            _ => output.ToString()
        };

    public static string FormatProcessingProviderQueueRecentDetailKind(RadarProcessingProviderQueueRecentDetailKind kind) =>
        kind switch
        {
            RadarProcessingProviderQueueRecentDetailKind.Enqueue => "enqueue",
            RadarProcessingProviderQueueRecentDetailKind.Dequeue => "dequeue",
            RadarProcessingProviderQueueRecentDetailKind.Processing => "processing",
            _ => kind.ToString()
        };

    public static string FormatProcessingProviderQueueSequence(RadarProcessingQueuedBatchSequence? sequence) =>
        sequence.HasValue
            ? FormatNumber(sequence.Value.Value)
            : "n/a";

    public static string FormatProcessingProviderQueueEnqueueStatus(RadarProcessingQueuedBatchEnqueueStatus? status) =>
        status switch
        {
            RadarProcessingQueuedBatchEnqueueStatus.Accepted => "accepted",
            RadarProcessingQueuedBatchEnqueueStatus.Full => "full",
            RadarProcessingQueuedBatchEnqueueStatus.TimedOut => "timed-out",
            RadarProcessingQueuedBatchEnqueueStatus.Canceled => "canceled",
            RadarProcessingQueuedBatchEnqueueStatus.Closed => "closed",
            RadarProcessingQueuedBatchEnqueueStatus.Faulted => "faulted",
            null => "n/a",
            _ => status.Value.ToString()
        };

    public static string FormatProcessingProviderQueueProcessingStatus(RadarProcessingQueuedBatchProcessingStatus? status) =>
        status switch
        {
            RadarProcessingQueuedBatchProcessingStatus.Succeeded => "succeeded",
            RadarProcessingQueuedBatchProcessingStatus.FailedProcessing => "failed-processing",
            RadarProcessingQueuedBatchProcessingStatus.FailedValidation => "failed-validation",
            RadarProcessingQueuedBatchProcessingStatus.FailedMigration => "failed-migration",
            RadarProcessingQueuedBatchProcessingStatus.Canceled => "canceled",
            RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault => "skipped-after-fault",
            null => "n/a",
            _ => status.Value.ToString()
        };

    public static string FormatProcessingArchiveQueuedOverlapStatus(RadarProcessingArchiveQueuedOverlapStatus status) =>
        status switch
        {
            RadarProcessingArchiveQueuedOverlapStatus.NotStarted => "not-started",
            RadarProcessingArchiveQueuedOverlapStatus.Completed => "completed",
            RadarProcessingArchiveQueuedOverlapStatus.ProducerFailed => "producer-failed",
            RadarProcessingArchiveQueuedOverlapStatus.ConsumerFaulted => "consumer-faulted",
            RadarProcessingArchiveQueuedOverlapStatus.Canceled => "canceled",
            RadarProcessingArchiveQueuedOverlapStatus.Disposed => "disposed",
            _ => status.ToString()
        };

    public static string FormatProcessingQueuedSessionStatus(RadarProcessingQueuedSessionStatus status) =>
        status switch
        {
            RadarProcessingQueuedSessionStatus.NotStarted => "not-started",
            RadarProcessingQueuedSessionStatus.Running => "running",
            RadarProcessingQueuedSessionStatus.Draining => "draining",
            RadarProcessingQueuedSessionStatus.Completed => "completed",
            RadarProcessingQueuedSessionStatus.Faulted => "faulted",
            RadarProcessingQueuedSessionStatus.Canceled => "canceled",
            RadarProcessingQueuedSessionStatus.Disposed => "disposed",
            _ => status.ToString()
        };

    public static string FormatBoolean(bool value) =>
        value ? "yes" : "no";
}

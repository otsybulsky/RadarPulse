using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using static CliFormat;

internal static partial class ProcessingBenchmarkCliReporter
{
    public static void PrintProcessingProviderOverlapTelemetryForArchiveFile(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
    {
        if (!result.HasOverlapTelemetry ||
            overlapTelemetryOutput == ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, overlapTelemetryOutput);
    }

    public static void PrintProcessingProviderOverlapTelemetryForArchiveCache(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
    {
        if (!result.HasOverlapTelemetry ||
            overlapTelemetryOutput == ProcessingBenchmarkProviderOverlapTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderOverlapTelemetrySummary(result.OverlapTelemetry, overlapTelemetryOutput);
    }

    public static void PrintProcessingProviderOverlapTelemetrySummary(
        RadarProcessingArchiveOverlapTelemetrySummary telemetry,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput)
    {
        Console.WriteLine($"Provider overlap telemetry: {FormatProviderOverlapTelemetryOutput(overlapTelemetryOutput)}");
        Console.WriteLine($"Provider overlap retained payload strategy: {FormatProcessingRetentionStrategy(telemetry.RetentionStrategy)}");
        Console.WriteLine($"Provider overlap elapsed ms: {FormatDecimal(telemetry.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap producer active ms: {FormatDecimal(telemetry.ProducerActiveTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap consumer active ms: {FormatDecimal(telemetry.ConsumerActiveTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap shared active ms: {FormatDecimal(telemetry.OverlapElapsed.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap has producer-consumer overlap: {FormatBoolean(telemetry.HasProducerConsumerOverlap)}");
        Console.WriteLine($"Provider overlap has queued-ahead overlap: {FormatBoolean(telemetry.HasQueuedAheadOverlap)}");
        Console.WriteLine($"Provider overlap queue depth high watermark: {FormatNumber(telemetry.QueueDepthHighWatermark)}");
        Console.WriteLine($"Provider overlap retained payload bytes high watermark: {FormatNumber(telemetry.RetainedPayloadBytesHighWatermark)}");
        PrintProcessingRetainedResourcePressureSummary("Provider overlap", telemetry.RetainedResourcePressure);
        Console.WriteLine($"Provider overlap provider blocked ms: {FormatDecimal(telemetry.ProviderBlockedTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap consumer idle ms: {FormatDecimal(telemetry.ConsumerIdleTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap provider-to-processing latency ms: {FormatDecimal(telemetry.TotalProviderToProcessingLatency.TotalMilliseconds)}");
        Console.WriteLine($"Provider overlap retained batches: {FormatNumber(telemetry.RetainedBatchCount)}");
        Console.WriteLine($"Provider overlap retained events: {FormatNumber(telemetry.RetainedEventCount)}");
        Console.WriteLine($"Provider overlap retained payload bytes: {FormatNumber(telemetry.RetainedPayloadBytes)}");
        Console.WriteLine($"Provider overlap retained payload values: {FormatNumber(telemetry.RetainedPayloadValueCount)}");
        Console.WriteLine($"Provider overlap retention allocated bytes: {FormatNumber(telemetry.RetentionAllocatedBytes)}");
        Console.WriteLine("Provider overlap measured allocation counter scope: global");
        Console.WriteLine($"Provider overlap measured allocated bytes: {FormatNumber(telemetry.MeasuredAllocatedBytes)}");
        Console.WriteLine($"Provider overlap unattributed allocated bytes: {FormatNumber(telemetry.UnattributedAllocatedBytes)}");
        Console.WriteLine($"Provider overlap release attempts: {FormatNumber(telemetry.ReleaseAttemptCount)}");
        Console.WriteLine($"Provider overlap released batches: {FormatNumber(telemetry.ReleasedBatchCount)}");
        Console.WriteLine($"Provider overlap release-not-required batches: {FormatNumber(telemetry.ReleaseNotRequiredCount)}");
        Console.WriteLine($"Provider overlap failed releases: {FormatNumber(telemetry.ReleaseFailedCount)}");
    }

    public static bool IsDefaultCandidateFileBenchmarkContour(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
            result.ProviderMode,
            result.QueueCapacity,
            result.ProviderOverlapMode,
            result.RetentionStrategy,
            result.QueueRetainedPayloadBytes,
            result.OverlapConsumerDelay,
            queueTelemetryOutput,
            overlapTelemetryOutput,
            result.ExecutionMode);

    public static bool IsDefaultCandidateCacheBenchmarkContour(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        ProcessingBenchmarkArchiveRebalanceOptions.MatchesDefaultCandidateContour(
            result.ProviderMode,
            result.QueueCapacity,
            result.ProviderOverlapMode,
            result.RetentionStrategy,
            result.QueueRetainedPayloadBytes,
            result.OverlapConsumerDelay,
            queueTelemetryOutput,
            overlapTelemetryOutput,
            result.ExecutionMode);

    public static string FormatProviderOverlapEvidenceContourForFileBenchmark(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        FormatProviderOverlapEvidenceContourCore(
            result.ProviderMode,
            result.ProviderOverlapMode,
            result.OverlapConsumerDelay,
            IsDefaultCandidateFileBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput));

    public static string FormatProviderOverlapEvidenceContourForCacheBenchmark(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput,
        ProcessingBenchmarkProviderOverlapTelemetryOutput overlapTelemetryOutput) =>
        FormatProviderOverlapEvidenceContourCore(
            result.ProviderMode,
            result.ProviderOverlapMode,
            result.OverlapConsumerDelay,
            IsDefaultCandidateCacheBenchmarkContour(result, queueTelemetryOutput, overlapTelemetryOutput));

    public static string FormatProviderOverlapEvidenceContourCore(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        TimeSpan overlapConsumerDelay,
        bool isDefaultCandidateContour) =>
        ProcessingBenchmarkArchiveRebalanceOptions.FormatProviderOverlapEvidenceContour(
            providerMode,
            providerOverlapMode,
            overlapConsumerDelay,
            isDefaultCandidateContour);

    public static string FormatProviderOverlapEvidenceScope(string providerOverlapEvidenceContour) =>
        ProcessingBenchmarkArchiveRebalanceOptions.FormatProviderOverlapEvidenceScope(providerOverlapEvidenceContour);
}

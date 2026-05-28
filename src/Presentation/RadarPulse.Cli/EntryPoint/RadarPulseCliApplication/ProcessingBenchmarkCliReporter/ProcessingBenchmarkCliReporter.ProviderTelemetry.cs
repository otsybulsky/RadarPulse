using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;
using static CliFormat;

internal static partial class ProcessingBenchmarkCliReporter
{
    public static void PrintProcessingProviderQueueTelemetryForArchiveFile(
        RadarProcessingArchiveRebalanceBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
    {
        if (!result.HasQueueTelemetry ||
            queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderQueueTelemetrySummary(
            result.QueueTelemetry,
            result.OwnedSnapshotAllocatedBytesPerPayloadValue,
            queueTelemetryOutput);
    }

    public static void PrintProcessingProviderQueueTelemetryForArchiveCache(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
    {
        if (!result.HasQueueTelemetry ||
            queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.None)
        {
            return;
        }

        PrintProcessingProviderQueueTelemetrySummary(
            result.QueueTelemetry,
            result.OwnedSnapshotAllocatedBytesPerPayloadValue,
            queueTelemetryOutput);
    }

    public static void PrintProcessingProviderRetentionTelemetryForArchiveFile(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        if (!result.HasRetentionTelemetry)
        {
            return;
        }

        PrintProcessingProviderRetentionTelemetrySummary(result.RetentionTelemetry);
    }

    public static void PrintProcessingProviderRetentionTelemetryForArchiveCache(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        if (!result.HasRetentionTelemetry)
        {
            return;
        }

        PrintProcessingProviderRetentionTelemetrySummary(result.RetentionTelemetry);
    }

    public static void PrintProcessingProviderRetentionTelemetrySummary(
        RadarProcessingRetainedPayloadTelemetrySummary telemetry)
    {
        Console.WriteLine("Retained payload telemetry: summary");
        Console.WriteLine("Retained payload allocation counter scope: current-thread");
        Console.WriteLine($"Retained payload strategy: {FormatProcessingRetentionStrategy(telemetry.Strategy)}");
        Console.WriteLine($"Retained payload attempts: {FormatNumber(telemetry.RetentionAttemptCount)}");
        Console.WriteLine($"Retained payload batches: {FormatNumber(telemetry.RetainedBatchCount)}");
        Console.WriteLine($"Retained payload events: {FormatNumber(telemetry.RetainedEventCount)}");
        Console.WriteLine($"Retained payload bytes: {FormatNumber(telemetry.RetainedPayloadBytes)}");
        Console.WriteLine($"Retained payload values: {FormatNumber(telemetry.RetainedPayloadValueCount)}");
        Console.WriteLine($"Retained payload allocated bytes: {FormatNumber(telemetry.AllocatedBytes)}");
        Console.WriteLine($"Retained payload elapsed ms: {FormatDecimal(telemetry.TotalRetentionTime.TotalMilliseconds)}");
        Console.WriteLine($"Retained payload transfers: {FormatNumber(telemetry.TransferCount)}");
        Console.WriteLine($"Retained payload pool rents: {FormatNumber(telemetry.PoolRentCount)}");
        Console.WriteLine($"Retained payload pool returns: {FormatNumber(telemetry.PoolReturnCount)}");
        Console.WriteLine($"Retained payload pool misses: {FormatNumber(telemetry.PoolMissCount)}");
        Console.WriteLine($"Retained event array pool rents: {FormatNumber(telemetry.EventPoolRentCount)}");
        Console.WriteLine($"Retained event array pool returns: {FormatNumber(telemetry.EventPoolReturnCount)}");
        Console.WriteLine($"Retained event array pool misses: {FormatNumber(telemetry.EventPoolMissCount)}");
        Console.WriteLine($"Retained byte array pool rents: {FormatNumber(telemetry.PayloadPoolRentCount)}");
        Console.WriteLine($"Retained byte array pool returns: {FormatNumber(telemetry.PayloadPoolReturnCount)}");
        Console.WriteLine($"Retained byte array pool misses: {FormatNumber(telemetry.PayloadPoolMissCount)}");
        Console.WriteLine($"Retained payload unsupported strategy attempts: {FormatNumber(telemetry.RetentionUnsupportedStrategyCount)}");
        Console.WriteLine($"Retained payload failed copies: {FormatNumber(telemetry.RetentionFailedCopyCount)}");
        Console.WriteLine($"Retained payload canceled retentions: {FormatNumber(telemetry.RetentionCanceledCount)}");
        Console.WriteLine($"Retained payload invalid inputs: {FormatNumber(telemetry.RetentionInvalidInputCount)}");
        Console.WriteLine($"Retained payload release attempts: {FormatNumber(telemetry.ReleaseAttemptCount)}");
        Console.WriteLine($"Retained payload released batches: {FormatNumber(telemetry.ReleasedBatchCount)}");
        Console.WriteLine($"Retained payload already released batches: {FormatNumber(telemetry.AlreadyReleasedBatchCount)}");
        Console.WriteLine($"Retained payload release-not-required batches: {FormatNumber(telemetry.ReleaseNotRequiredCount)}");
        Console.WriteLine($"Retained payload failed releases: {FormatNumber(telemetry.ReleaseFailedCount)}");
        Console.WriteLine($"Retained payload release elapsed ms: {FormatDecimal(telemetry.TotalReleaseTime.TotalMilliseconds)}");
    }

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

    public static void PrintProcessingProviderQueueTelemetrySummary(
        RadarProcessingProviderQueueTelemetrySummary telemetry,
        double ownedSnapshotAllocatedBytesPerPayloadValue,
        ProcessingBenchmarkProviderQueueTelemetryOutput queueTelemetryOutput)
    {
        Console.WriteLine($"Provider queue telemetry: {FormatProviderQueueTelemetryOutput(queueTelemetryOutput)}");
        Console.WriteLine($"Provider queue owned snapshots: {FormatNumber(telemetry.OwnedSnapshotCount)}");
        Console.WriteLine($"Provider queue owned snapshot events: {FormatNumber(telemetry.OwnedSnapshotEventCount)}");
        Console.WriteLine($"Provider queue owned snapshot payload bytes: {FormatNumber(telemetry.OwnedSnapshotPayloadBytes)}");
        Console.WriteLine($"Provider queue owned snapshot payload values: {FormatNumber(telemetry.OwnedSnapshotPayloadValueCount)}");
        Console.WriteLine($"Provider queue owned snapshot elapsed ms: {FormatDecimal(telemetry.TotalOwnedSnapshotTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue owned snapshot allocated bytes: {FormatNumber(telemetry.OwnedSnapshotAllocatedBytes)}");
        Console.WriteLine($"Provider queue owned snapshot allocated bytes / payload value: {FormatDecimal(ownedSnapshotAllocatedBytesPerPayloadValue)}");
        Console.WriteLine($"Provider queue enqueue attempts: {FormatNumber(telemetry.EnqueueAttemptCount)}");
        Console.WriteLine($"Provider queue enqueued batches: {FormatNumber(telemetry.EnqueuedBatchCount)}");
        Console.WriteLine($"Provider queue full batches: {FormatNumber(telemetry.EnqueueFullCount)}");
        Console.WriteLine($"Provider queue timed out batches: {FormatNumber(telemetry.EnqueueTimedOutCount)}");
        Console.WriteLine($"Provider queue canceled enqueue batches: {FormatNumber(telemetry.EnqueueCanceledCount)}");
        Console.WriteLine($"Provider queue closed enqueue batches: {FormatNumber(telemetry.EnqueueClosedCount)}");
        Console.WriteLine($"Provider queue faulted enqueue batches: {FormatNumber(telemetry.EnqueueFaultedCount)}");
        Console.WriteLine($"Provider queue enqueue wait ms: {FormatDecimal(telemetry.TotalEnqueueWaitTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue dequeued batches: {FormatNumber(telemetry.DequeuedBatchCount)}");
        Console.WriteLine($"Provider queue completed batches: {FormatNumber(telemetry.CompletedBatchCount)}");
        Console.WriteLine($"Provider queue failed batches: {FormatNumber(telemetry.FailedBatchCount)}");
        Console.WriteLine($"Provider queue canceled batches: {FormatNumber(telemetry.CanceledBatchCount)}");
        Console.WriteLine($"Provider queue skipped after fault batches: {FormatNumber(telemetry.SkippedAfterFaultCount)}");
        Console.WriteLine($"Provider queue drain ms: {FormatDecimal(telemetry.TotalDrainTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue dequeue wait ms: {FormatDecimal(telemetry.TotalDequeueWaitTime.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue depth high watermark: {FormatNumber(telemetry.QueueDepthHighWatermark)}");
        Console.WriteLine($"Provider queue payload bytes high watermark: {FormatNumber(telemetry.QueuedPayloadBytesHighWatermark)}");
        Console.WriteLine($"Provider queue retained payload bytes high watermark: {FormatNumber(telemetry.RetainedPayloadBytesHighWatermark)}");
        PrintProcessingRetainedResourcePressureSummary("Provider queue", telemetry.RetainedResourcePressure);
        Console.WriteLine($"Provider-to-processing latency ms: {FormatDecimal(telemetry.TotalProviderToProcessingLatency.TotalMilliseconds)}");
        Console.WriteLine($"Provider queue retained recent details: {FormatNumber(telemetry.RetainedRecentDetailCount)}");
        Console.WriteLine($"Provider queue dropped recent details: {FormatNumber(telemetry.DroppedRecentDetailCount)}");

        if (queueTelemetryOutput == ProcessingBenchmarkProviderQueueTelemetryOutput.Recent)
        {
            PrintProcessingProviderQueueRecentDetails(telemetry.RecentDetails);
        }
    }

    public static void PrintProcessingProviderQueueRecentDetails(
        IReadOnlyList<RadarProcessingProviderQueueRecentDetail> recentDetails)
    {
        if (recentDetails.Count == 0)
        {
            Console.WriteLine("Provider queue recent details: (none)");
            return;
        }

        Console.WriteLine("Provider queue recent details:");
        for (var i = 0; i < recentDetails.Count; i++)
        {
            var detail = recentDetails[i];
            Console.WriteLine(
                $"  {FormatNumber(i + 1)}. {FormatProcessingProviderQueueRecentDetailKind(detail.Kind)} " +
                $"sequence {FormatProcessingProviderQueueSequence(detail.Sequence)} " +
                $"enqueue {FormatProcessingProviderQueueEnqueueStatus(detail.EnqueueStatus)} " +
                $"processing {FormatProcessingProviderQueueProcessingStatus(detail.ProcessingStatus)} " +
                $"events {FormatNumber(detail.StreamEventCount)} payload bytes {FormatNumber(detail.PayloadBytes)} " +
                $"payload values {FormatNumber(detail.PayloadValueCount)} queue depth {FormatNumber(detail.QueueDepth)}");
        }
    }

    public static void PrintProcessingRetainedResourcePressureSummary(
        string prefix,
        RadarProcessingRetainedResourcePressureSummary telemetry)
    {
        Console.WriteLine($"{prefix} current pending retained batches: {FormatNumber(telemetry.CurrentPendingRetainedBatchCount)}");
        Console.WriteLine($"{prefix} current pending retained payload bytes: {FormatNumber(telemetry.CurrentPendingRetainedPayloadBytes)}");
        Console.WriteLine($"{prefix} pending retained batches high watermark: {FormatNumber(telemetry.PendingRetainedBatchCountHighWatermark)}");
        Console.WriteLine($"{prefix} pending retained payload bytes high watermark: {FormatNumber(telemetry.PendingRetainedPayloadBytesHighWatermark)}");
        Console.WriteLine($"{prefix} current active retained batches: {FormatNumber(telemetry.CurrentActiveRetainedBatchCount)}");
        Console.WriteLine($"{prefix} current active retained payload bytes: {FormatNumber(telemetry.CurrentActiveRetainedPayloadBytes)}");
        Console.WriteLine($"{prefix} active retained batches high watermark: {FormatNumber(telemetry.ActiveRetainedBatchCountHighWatermark)}");
        Console.WriteLine($"{prefix} active retained payload bytes high watermark: {FormatNumber(telemetry.ActiveRetainedPayloadBytesHighWatermark)}");
        Console.WriteLine($"{prefix} current combined retained batches: {FormatNumber(telemetry.CurrentCombinedRetainedBatchCount)}");
        Console.WriteLine($"{prefix} current combined retained payload bytes: {FormatNumber(telemetry.CurrentCombinedRetainedPayloadBytes)}");
        Console.WriteLine($"{prefix} combined retained batches high watermark: {FormatNumber(telemetry.CombinedRetainedBatchCountHighWatermark)}");
        Console.WriteLine($"{prefix} combined retained payload bytes high watermark: {FormatNumber(telemetry.CombinedRetainedPayloadBytesHighWatermark)}");
    }

}

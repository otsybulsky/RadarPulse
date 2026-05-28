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
    public static void PrintProcessingArchiveRebalanceProviderSelection(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingExecutionMode executionMode,
        ProcessingBenchmarkArchiveRebalanceOptionProvenance provenance,
        bool isDefaultCandidateContour,
        bool isRolloutDefaultExpandedContour,
        bool isExplicitBlockingBorrowedFallback)
    {
        var isQueuedOwned = providerMode == RadarProcessingArchiveProviderMode.QueuedOwned;
        var hasProducerConsumerOverlap =
            isQueuedOwned &&
            providerOverlapMode == RadarProcessingQueuedProviderOverlapMode.ProducerConsumer;
        var isAsyncExecution = executionMode == RadarProcessingExecutionMode.AsyncShardTransport;

        Console.WriteLine($"Provider mode source: {FormatProcessingBenchmarkOptionValueSource(provenance.ProviderMode)}");
        Console.WriteLine($"Provider overlap source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.ProviderOverlapMode, isQueuedOwned)}");
        Console.WriteLine($"Retention strategy source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.RetentionStrategy, isQueuedOwned)}");
        Console.WriteLine($"Provider queue capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueCapacity, isQueuedOwned)}");
        Console.WriteLine($"Worker queue capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueCapacity, isAsyncExecution)}");
        Console.WriteLine($"Provider queue retained byte capacity source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueRetainedPayloadBytes, isQueuedOwned)}");
        Console.WriteLine($"Queue telemetry source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.QueueTelemetry, isQueuedOwned)}");
        Console.WriteLine($"Provider overlap telemetry source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.OverlapTelemetry, hasProducerConsumerOverlap)}");
        Console.WriteLine($"Provider overlap consumer delay source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.OverlapConsumerDelay, hasProducerConsumerOverlap)}");
        Console.WriteLine($"Execution mode source: {FormatProcessingBenchmarkOptionValueSource(provenance.ExecutionMode)}");
        Console.WriteLine($"Worker count source: {FormatProcessingBenchmarkApplicableOptionValueSource(provenance.WorkerCount, isAsyncExecution)}");
        Console.WriteLine($"Provider default rollout contour: {FormatBoolean(isDefaultCandidateContour)}");
        Console.WriteLine($"Provider rollout default expansion: {FormatBoolean(isRolloutDefaultExpandedContour)}");
        Console.WriteLine($"Provider fallback contour: {FormatBoolean(isExplicitBlockingBorrowedFallback)}");
    }

    public static void PrintProcessingArchiveRebalanceAllocationAttribution(
        RadarProcessingRebalanceAllocationSummary allocation,
        long payloadValueCount)
    {
        Console.WriteLine("Allocation attribution: summary");
        Console.WriteLine("Allocation measured counter scope: global");
        Console.WriteLine("Allocation processing callback counter scope: global");
        Console.WriteLine($"Allocation measured bytes: {FormatNumber(allocation.MeasuredAllocatedBytes)}");
        Console.WriteLine($"Allocation processing callback bytes: {FormatNumber(allocation.ProcessingCallbackAllocatedBytes)}");
        Console.WriteLine($"Allocation replay and batch construction bytes: {FormatNumber(allocation.ReplayAndBatchConstructionAllocatedBytes)}");
        Console.WriteLine($"Allocation owned snapshot bytes: {FormatNumber(allocation.OwnedSnapshotAllocatedBytes)}");
        Console.WriteLine($"Allocation processing callback non-owned snapshot bytes: {FormatNumber(allocation.ProcessingCallbackNonOwnedSnapshotAllocatedBytes)}");
        Console.WriteLine($"Allocation includes archive replay and batch construction: {FormatBoolean(allocation.IncludesArchiveReplayAndBatchConstruction)}");
        Console.WriteLine($"Allocation includes CLI formatting: {FormatBoolean(allocation.IncludesCliFormatting)}");
        Console.WriteLine($"Allocation owned snapshot bytes / payload value: {FormatDecimal(allocation.OwnedSnapshotAllocatedBytesPerPayloadValue(payloadValueCount))}");
        Console.WriteLine($"Allocation processing callback non-owned snapshot bytes / payload value: {FormatDecimal(allocation.ProcessingCallbackNonOwnedSnapshotAllocatedBytesPerPayloadValue(payloadValueCount))}");
    }

    public static void PrintProcessingRetainedPayloadPrewarm(
        RadarProcessingRetainedPayloadPrewarmResult prewarm)
    {
        Console.WriteLine($"Retained payload prewarm: {FormatBoolean(prewarm.Applied)}");
        if (!prewarm.Applied)
        {
            return;
        }

        Console.WriteLine($"Retained payload prewarm event count: {FormatNumber(prewarm.EventCount)}");
        Console.WriteLine($"Retained payload prewarm payload bytes: {FormatNumber(prewarm.PayloadBytes)}");
        Console.WriteLine($"Retained payload prewarm batch count: {FormatNumber(prewarm.RetainedBatchCount)}");
        Console.WriteLine($"Retained payload prewarm elapsed ms: {FormatDecimal(prewarm.Elapsed.TotalMilliseconds)}");
        Console.WriteLine($"Retained payload prewarm allocated bytes: {FormatNumber(prewarm.AllocatedBytes)}");
        Console.WriteLine($"Retained payload prewarm retained bytes: {FormatNumber(prewarm.RetainedBytes)}");
    }
}

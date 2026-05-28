using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;
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
}

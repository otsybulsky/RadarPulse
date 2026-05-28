using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;
using static CliFormat;

internal static partial class ProcessingBenchmarkCliReporter
{
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

using System.Globalization;
using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

internal static class ProductPipelineCliWorkflow
{
    public static async Task<int> RunDemoAsync(string[] args)
    {
        var options = ProductPipelineDemoOptions.Parse(args);
        var service = new RadarPulseProductPipelineService();
        var detail = await service.RunSyntheticAsync(
            new RadarPulseProductPipelineSyntheticRunRequest(
                options.RunId,
                options.SourceCount,
                options.BatchCount,
                options.EventsPerBatch,
                options.PartitionCount,
                options.ShardCount,
                options.HandlerSet,
                options.PipelineOptions));

        PrintRunDetail("demo", detail);
        return detail.IsReady ? 0 : 1;
    }

    public static async Task<int> RunArchiveAsync(string[] args)
    {
        var options = ProductPipelineArchiveOptions.Parse(args);
        var service = new RadarPulseProductPipelineService();
        var detail = await service.RunArchiveFileAsync(
            new RadarPulseProductPipelineArchiveFileRunRequest(
                options.RunId,
                options.FilePath,
                options.Parallelism,
                options.PartitionCount,
                options.ShardCount,
                options.Decompressor,
                options.HandlerSet,
                options.PipelineOptions));

        PrintRunDetail("archive-file", detail);
        return detail.IsReady ? 0 : 1;
    }

    private static void PrintRunDetail(
        string workflow,
        RadarPulseProductRunDetail detail)
    {
        Console.WriteLine($"Product pipeline: {workflow}");
        Console.WriteLine($"Run id: {detail.RunId}");
        Console.WriteLine($"Input kind: {FormatInputKind(detail.Summary.Input.Kind)}");
        Console.WriteLine($"Input source: {detail.Summary.Input.Source}");
        Console.WriteLine($"Run state: {FormatRunState(detail.Summary.State)}");
        Console.WriteLine($"Readiness: {(detail.IsReady ? "ready" : "blocked")}");
        Console.WriteLine($"First blocking reason: {FormatEmptyAsNone(detail.Summary.FirstBlockingReason)}");
        Console.WriteLine($"Fallback recommendation: {FormatFallback(detail.Summary.FallbackRecommendation)}");
        Console.WriteLine($"Handler mode: {FormatHandlerMode(detail.Summary.HandlerMode)}");
        Console.WriteLine($"Read model: {(detail.HasReadModel ? "published" : "not-published")}");
        Console.WriteLine($"Input batches: {FormatNumber(detail.Summary.Input.BatchCount)}");
        Console.WriteLine($"Input events: {FormatNumber(detail.Summary.Input.EventCount)}");
        Console.WriteLine($"Batches: {FormatNumber(detail.Summary.BatchCount)}");
        Console.WriteLine($"Sources: {FormatNumber(detail.Summary.SourceCount)}");
        Console.WriteLine($"Accepted batches: {FormatNumber(detail.Summary.AcceptedBatchCount)}");
        Console.WriteLine($"Processed batches: {FormatNumber(detail.Summary.ProcessedBatchCount)}");
        Console.WriteLine($"Committed batches: {FormatNumber(detail.Summary.CommittedBatchCount)}");
        Console.WriteLine($"Processing completeness: {(detail.CapacityEvidence.ProcessingCompletenessPassed ? "succeeded" : "failed")}");
        Console.WriteLine($"Terminal retained batches: {FormatNumber(detail.CapacityEvidence.TerminalRetainedBatchCount)}");
        Console.WriteLine($"Terminal retained payload bytes: {FormatNumber(detail.CapacityEvidence.TerminalRetainedPayloadBytes)}");
        Console.WriteLine($"Elapsed ms: {FormatDecimal(detail.CapacityEvidence.ElapsedMilliseconds)}");
        Console.WriteLine($"Allocated bytes: {FormatNumber(detail.CapacityEvidence.MeasuredAllocatedBytes)}");
        Console.WriteLine($"Configuration contour: {detail.CapacityEvidence.ConfigurationContour}");
        Console.WriteLine($"Warnings: {FormatNumber(detail.Summary.WarningCount)}");
        foreach (var warning in detail.OperatorSummary.Warnings)
        {
            Console.WriteLine($"Warning: {warning}");
        }
    }

    private static string FormatNumber(long value) =>
        value.ToString("N0").Replace(',', '_');

    private static string FormatDecimal(double value) =>
        value.ToString("N2", CultureInfo.InvariantCulture).Replace(',', '_');

    private static string FormatEmptyAsNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value;

    private static string FormatInputKind(RadarPulseProductInputKind kind) =>
        kind switch
        {
            RadarPulseProductInputKind.Synthetic => "synthetic",
            RadarPulseProductInputKind.ArchiveFile => "archive-file",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static string FormatRunState(RadarPulseProductRunState state) =>
        state switch
        {
            RadarPulseProductRunState.NotStarted => "not-started",
            RadarPulseProductRunState.Running => "running",
            RadarPulseProductRunState.Draining => "draining",
            RadarPulseProductRunState.Completed => "completed",
            RadarPulseProductRunState.Stopped => "stopped",
            RadarPulseProductRunState.Blocked => "blocked",
            RadarPulseProductRunState.Failed => "failed",
            RadarPulseProductRunState.Canceled => "canceled",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

    private static string FormatHandlerMode(RadarPulseProductHandlerMode mode) =>
        mode switch
        {
            RadarPulseProductHandlerMode.Auto => "auto",
            RadarPulseProductHandlerMode.HandlerFree => "handler-free",
            RadarPulseProductHandlerMode.MergeableDelta => "mergeable-delta",
            RadarPulseProductHandlerMode.SnapshotSequential => "snapshot-sequential",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    private static string FormatFallback(RadarPulseProductFallbackRecommendation recommendation) =>
        recommendation switch
        {
            RadarPulseProductFallbackRecommendation.None => "none",
            RadarPulseProductFallbackRecommendation.FixConfiguration => "fix-configuration",
            RadarPulseProductFallbackRecommendation.InspectDurableAdapter => "inspect-durable-adapter",
            RadarPulseProductFallbackRecommendation.RecoverClaimedEnvelope => "recover-claimed-envelope",
            RadarPulseProductFallbackRecommendation.RetryOrPoisonEnvelope => "retry-or-poison-envelope",
            RadarPulseProductFallbackRecommendation.QuarantinePoisonEnvelope => "quarantine-poison-envelope",
            RadarPulseProductFallbackRecommendation.CleanupCanceledEnvelope => "cleanup-canceled-envelope",
            RadarPulseProductFallbackRecommendation.ReleaseRetainedResources => "release-retained-resources",
            RadarPulseProductFallbackRecommendation.CompleteOrRecoverUncommittedWork => "complete-or-recover-uncommitted-work",
            RadarPulseProductFallbackRecommendation.ResolveHandlerPosture => "resolve-handler-posture",
            RadarPulseProductFallbackRecommendation.RejectUnsafeFallback => "reject-unsafe-fallback",
            _ => throw new ArgumentOutOfRangeException(nameof(recommendation))
        };
}

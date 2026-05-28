using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public enum ProcessingBenchmarkProviderQueueTelemetryOutput
{
    /// <summary>
    /// Suppresses provider queue telemetry output.
    /// </summary>
    None = 1,

    /// <summary>
    /// Writes aggregate provider queue telemetry.
    /// </summary>
    Summary = 2,

    /// <summary>
    /// Writes aggregate and recent-detail provider queue telemetry.
    /// </summary>
    Recent = 3
}

/// <summary>
/// Selects how provider overlap telemetry is written by processing benchmark CLI commands.
/// </summary>
public enum ProcessingBenchmarkProviderOverlapTelemetryOutput
{
    /// <summary>
    /// Suppresses provider overlap telemetry output.
    /// </summary>
    None = 1,

    /// <summary>
    /// Writes aggregate provider overlap telemetry.
    /// </summary>
    Summary = 2,

    /// <summary>
    /// Writes aggregate and recent-detail provider overlap telemetry.
    /// </summary>
    Recent = 3
}

/// <summary>
/// Identifies where a processing benchmark option value came from.
/// </summary>
public enum ProcessingBenchmarkOptionValueSource
{
    /// <summary>
    /// The option used the current command default.
    /// </summary>
    CurrentDefault = 0,

    /// <summary>
    /// The option was provided explicitly by the operator.
    /// </summary>
    Explicit = 1,

    /// <summary>
    /// The option was expanded from rollout defaults.
    /// </summary>
    RolloutDefault = 2
}

/// <summary>
/// Captures provenance for archive rebalance benchmark options that affect rollout evidence contours.
/// </summary>
public sealed record ProcessingBenchmarkArchiveRebalanceOptionProvenance(
    ProcessingBenchmarkOptionValueSource ProviderMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource ProviderOverlapMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource RetentionStrategy = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueCapacity = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueRetainedPayloadBytes = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource QueueTelemetry = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource OverlapTelemetry = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource OverlapConsumerDelay = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource ExecutionMode = ProcessingBenchmarkOptionValueSource.CurrentDefault,
    ProcessingBenchmarkOptionValueSource WorkerCount = ProcessingBenchmarkOptionValueSource.CurrentDefault)
{
    /// <summary>
    /// Gets provenance where every option used the current command default.
    /// </summary>
    public static ProcessingBenchmarkArchiveRebalanceOptionProvenance CurrentDefaults { get; } = new();
}

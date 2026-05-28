using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Deterministic workload and option bundle for rebalance benchmark scenarios.
/// </summary>
public sealed partial class RadarProcessingSyntheticRebalanceWorkload
{
    private const int RetentionStressBatchCount = 16;
    private const int RetentionStressDecisionLimit = 4;

    private readonly IReadOnlyList<RadarEventBatch> batches;
    private readonly IReadOnlyList<InitialHotPartitionClassification> initialClassifications;

    private RadarProcessingSyntheticRebalanceWorkload(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions coreOptions,
        RadarProcessingPressureOptions pressureOptions,
        RadarProcessingPressureWindowOptions pressureWindowOptions,
        RadarProcessingRebalanceOptions rebalanceOptions,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        RadarEventBatch[] batches,
        long eventsPerIteration,
        long payloadValuesPerIteration,
        long rawValueChecksumPerIteration,
        InitialHotPartitionClassification[] initialClassifications)
    {
        Kind = kind;
        SourceUniverse = sourceUniverse;
        CoreOptions = coreOptions;
        PressureOptions = pressureOptions;
        PressureWindowOptions = pressureWindowOptions;
        RebalanceOptions = rebalanceOptions;
        HardeningOptions = hardeningOptions;
        this.batches = Array.AsReadOnly((RadarEventBatch[])batches.Clone());
        EventsPerIteration = eventsPerIteration;
        PayloadValuesPerIteration = payloadValuesPerIteration;
        RawValueChecksumPerIteration = rawValueChecksumPerIteration;
        this.initialClassifications = Array.AsReadOnly(
            (InitialHotPartitionClassification[])initialClassifications.Clone());
    }

    /// <summary>
    /// Scenario represented by the workload.
    /// </summary>
    public RadarProcessingSyntheticRebalanceWorkloadKind Kind { get; }

    /// <summary>
    /// Source universe used by generated batches.
    /// </summary>
    public RadarSourceUniverse SourceUniverse { get; }

    /// <summary>
    /// Baseline processing core options for the workload.
    /// </summary>
    public RadarProcessingCoreOptions CoreOptions { get; }

    /// <summary>
    /// Pressure scoring options used by rebalance sessions.
    /// </summary>
    public RadarProcessingPressureOptions PressureOptions { get; }

    /// <summary>
    /// Pressure window options used by rebalance sessions.
    /// </summary>
    public RadarProcessingPressureWindowOptions PressureWindowOptions { get; }

    /// <summary>
    /// Rebalance policy options used by the workload.
    /// </summary>
    public RadarProcessingRebalanceOptions RebalanceOptions { get; }

    /// <summary>
    /// Hardening options including validation and telemetry retention.
    /// </summary>
    public RadarProcessingRebalanceHardeningOptions HardeningOptions { get; }

    /// <summary>
    /// Batches processed once per benchmark iteration.
    /// </summary>
    public IReadOnlyList<RadarEventBatch> Batches => batches;

    /// <summary>
    /// Number of sources in the workload universe.
    /// </summary>
    public int SourceCount => SourceUniverse.SourceCount;

    /// <summary>
    /// Partition count used by the workload.
    /// </summary>
    public int PartitionCount => CoreOptions.PartitionCount;

    /// <summary>
    /// Shard count used by the workload.
    /// </summary>
    public int ShardCount => CoreOptions.ShardCount;

    /// <summary>
    /// Batch count processed per iteration.
    /// </summary>
    public long BatchesPerIteration => batches.Count;

    /// <summary>
    /// Event count processed per iteration.
    /// </summary>
    public long EventsPerIteration { get; }

    /// <summary>
    /// Payload value count processed per iteration.
    /// </summary>
    public long PayloadValuesPerIteration { get; }

    /// <summary>
    /// Raw value checksum total per iteration.
    /// </summary>
    public long RawValueChecksumPerIteration { get; }

    /// <summary>
    /// Creates the deterministic workload for a scenario.
    /// </summary>
    public static RadarProcessingSyntheticRebalanceWorkload Create(
        RadarProcessingSyntheticRebalanceWorkloadKind kind) =>
        kind switch
        {
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => CreateBalanced(),
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => CreateSustainedHotShard(),
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => CreateIntrinsicHotPartition(),
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => CreateOscillatingSpike(),
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => CreateCooldownStorm(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry => CreateQuarantineTtlRetry(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear =>
                CreateQuarantineSustainedCoolingClear(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry =>
                CreateQuarantinePressureChangeRetry(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry =>
                CreateQuarantineRetryReentry(),
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear =>
                CreateQuarantineSuccessfulReliefClear(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard => CreateLongNoHotShard(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection =>
                CreateLongCooldownRejection(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection =>
                CreateLongUnsafeTargetRejection(),
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons =>
                CreateLongMixedSkippedReasons(),
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention =>
                CreateCountersOnlyRetention(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    /// <summary>
    /// Creates a rebalance session initialized with workload options and classifications.
    /// </summary>
    public RadarProcessingRebalanceSession CreateSession(
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null)
    {
        var classifier = new RadarProcessingHotPartitionClassifier(PartitionCount);
        foreach (var classification in initialClassifications)
        {
            ApplyClassification(classifier, classification);
        }

        var effectiveHardeningOptions = hardeningOptions ?? HardeningOptions;
        return new RadarProcessingRebalanceSession(
            new RadarProcessingCore(SourceUniverse, CreateCoreOptions(executionMode, asyncExecution)),
            PressureOptions,
            new RadarProcessingPressureWindow(PressureWindowOptions),
            new RadarProcessingRebalancePolicyState(PartitionCount, ShardCount, RebalanceOptions),
            classifier,
            hardeningOptions: effectiveHardeningOptions);
    }

    /// <summary>
    /// Creates processing core options for the requested execution mode.
    /// </summary>
    public RadarProcessingCoreOptions CreateCoreOptions(
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null) =>
        new(
            executionMode,
            PartitionCount,
            ShardCount,
            asyncExecution: asyncExecution);

}

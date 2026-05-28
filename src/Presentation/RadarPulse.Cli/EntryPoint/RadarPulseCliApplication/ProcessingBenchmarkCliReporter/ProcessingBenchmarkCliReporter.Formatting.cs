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
    public static string FormatProcessingMode(RadarProcessingExecutionMode executionMode) =>
        executionMode switch
        {
            RadarProcessingExecutionMode.Sequential => "sequential",
            RadarProcessingExecutionMode.PartitionedBarrier => "partitioned",
            RadarProcessingExecutionMode.AsyncShardTransport => "async",
            _ => executionMode.ToString()
        };

    public static string FormatProcessingHandlerSet(RadarProcessingBenchmarkHandlerSet handlerSet) =>
        handlerSet switch
        {
            RadarProcessingBenchmarkHandlerSet.None => "none",
            RadarProcessingBenchmarkHandlerSet.CounterChecksum => "counter-checksum",
            RadarProcessingBenchmarkHandlerSet.CounterChecksumHeavy => "counter-checksum-heavy",
            _ => handlerSet.ToString()
        };

    public static string FormatProcessingRebalanceWorkload(RadarProcessingSyntheticRebalanceWorkloadKind workloadKind) =>
        workloadKind switch
        {
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced => "balanced",
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard => "hot-shard",
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition => "intrinsic-hot",
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike => "oscillating",
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm => "cooldown-storm",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry => "quarantine-ttl-retry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear =>
                "quarantine-cooling-clear",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry =>
                "quarantine-pressure-change-retry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry =>
                "quarantine-retry-reentry",
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear =>
                "quarantine-successful-relief-clear",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard => "long-no-hot-shard",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection => "long-cooldown-rejection",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection =>
                "long-unsafe-target-rejection",
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons =>
                "long-mixed-skipped-reasons",
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention => "counters-only-retention",
            _ => workloadKind.ToString()
        };

    public static string FormatProcessingRebalanceMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode) =>
        mode switch
        {
            RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance => "static",
            RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly => "sampling",
            RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession => "rebalance-session",
            RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession => "ordered-rebalance-session",
            _ => mode.ToString()
        };

    public static string FormatProcessingValidationProfile(RadarProcessingValidationProfile profile) =>
        profile switch
        {
            RadarProcessingValidationProfile.Off => "off",
            RadarProcessingValidationProfile.Essential => "essential",
            RadarProcessingValidationProfile.Diagnostic => "diagnostic",
            RadarProcessingValidationProfile.Benchmark => "benchmark",
            _ => profile.ToString()
        };

    public static string FormatProcessingRetentionMode(RadarProcessingDiagnosticRetentionMode retentionMode) =>
        retentionMode switch
        {
            RadarProcessingDiagnosticRetentionMode.Counters => "counters",
            RadarProcessingDiagnosticRetentionMode.Recent => "recent",
            RadarProcessingDiagnosticRetentionMode.Diagnostic => "diagnostic",
            _ => retentionMode.ToString()
        };

    public static string FormatProcessingPressureSkewProfile(RadarProcessingPressureSkewProfile profile) =>
        profile switch
        {
            RadarProcessingPressureSkewProfile.None => "none",
            RadarProcessingPressureSkewProfile.HotShard => "hot-shard",
            RadarProcessingPressureSkewProfile.RotatingHotShard => "rotating-hot-shard",
            RadarProcessingPressureSkewProfile.HotPartition => "hot-partition",
            RadarProcessingPressureSkewProfile.TargetStarvation => "target-starvation",
            RadarProcessingPressureSkewProfile.BudgetStorm => "budget-storm",
            _ => profile.ToString()
        };

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

    public static string FormatProcessingRebalanceMoveKind(RadarProcessingRebalanceMoveKind moveKind) =>
        moveKind switch
        {
            RadarProcessingRebalanceMoveKind.DirectHotRelief => "direct-hot-relief",
            RadarProcessingRebalanceMoveKind.ColdEvacuation => "cold-evacuation",
            RadarProcessingRebalanceMoveKind.RoomMakingReserved => "room-making-reserved",
            _ => moveKind.ToString()
        };

    public static string FormatProcessingRebalanceSkippedReasons(
        IReadOnlyList<RadarProcessingRebalanceSkippedReason> skippedReasons) =>
        skippedReasons.Count == 0
            ? "(none)"
            : string.Join(", ", skippedReasons.Select(FormatProcessingRebalanceSkippedReason));

    public static string FormatProcessingRebalanceSkippedReasonCounters(
        IReadOnlyList<RadarProcessingRebalanceSkippedReasonCounter> counters) =>
        counters.Count == 0
            ? "(none)"
            : string.Join(", ", counters.Select(counter =>
                $"{FormatProcessingRebalanceSkippedReason(counter.Reason)}={FormatNumber(counter.Count)}"));

    public static string FormatProcessingRebalanceSkippedReason(RadarProcessingRebalanceSkippedReason reason) =>
        reason switch
        {
            RadarProcessingRebalanceSkippedReason.NoSustainedPressure => "no-sustained-pressure",
            RadarProcessingRebalanceSkippedReason.NoHotShard => "no-hot-shard",
            RadarProcessingRebalanceSkippedReason.NoColdTargetShard => "no-cold-target-shard",
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget =>
                "direct-hot-partition-has-no-safe-target",
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit => "insufficient-projected-benefit",
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm => "target-would-become-warm",
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot => "target-would-become-hot",
            RadarProcessingRebalanceSkippedReason.TargetHeadroomExceeded => "target-headroom-exceeded",
            RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown => "candidate-partition-in-cooldown",
            RadarProcessingRebalanceSkippedReason.CandidatePartitionBelowMinimumResidency =>
                "candidate-partition-below-minimum-residency",
            RadarProcessingRebalanceSkippedReason.SourceShardInCooldown => "source-shard-in-cooldown",
            RadarProcessingRebalanceSkippedReason.TargetShardInCooldown => "target-shard-in-cooldown",
            RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted =>
                "source-shard-move-budget-exhausted",
            RadarProcessingRebalanceSkippedReason.TargetShardReceiveBudgetExhausted =>
                "target-shard-receive-budget-exhausted",
            RadarProcessingRebalanceSkippedReason.GlobalMoveBudgetExhausted => "global-move-budget-exhausted",
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot =>
                "partition-classified-intrinsic-hot",
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined => "partition-quarantined",
            RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit =>
                "cold-evacuation-insufficient-benefit",
            RadarProcessingRebalanceSkippedReason.MigrationValidationFailed => "migration-validation-failed",
            _ => reason.ToString()
        };

}

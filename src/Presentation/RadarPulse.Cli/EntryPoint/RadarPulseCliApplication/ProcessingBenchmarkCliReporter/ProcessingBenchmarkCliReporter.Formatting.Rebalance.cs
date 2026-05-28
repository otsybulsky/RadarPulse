using RadarPulse.Domain.Processing;
using static CliFormat;

internal static partial class ProcessingBenchmarkCliReporter
{
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

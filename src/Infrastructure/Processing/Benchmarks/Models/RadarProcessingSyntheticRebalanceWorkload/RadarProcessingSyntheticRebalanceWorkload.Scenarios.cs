using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkload
{
    private static RadarProcessingSyntheticRebalanceWorkload CreateBalanced() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.Balanced,
            CreateStandardWindowOptions(minimumSampleCount: 1),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 1, 2, 3],
                [0, 1, 2, 3]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateSustainedHotShard() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.SustainedHotShard,
            CreateStandardWindowOptions(minimumSampleCount: 1),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 1, 1],
                [0, 0, 0, 0, 1, 1]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateIntrinsicHotPartition() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.IntrinsicHotPartition,
            CreateStandardWindowOptions(minimumSampleCount: 1),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ],
            [
                new InitialHotPartitionClassification(
                    PartitionId: 1,
                    ShardId: 0,
                    RadarProcessingHotPartitionClassification.Quarantined)
            ]);

    private static RadarProcessingSyntheticRebalanceWorkload CreateOscillatingSpike() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.OscillatingSpike,
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 3,
                minimumSampleCount: 3,
                coldThreshold: 0.0,
                warmExitThreshold: 6.0,
                warmEnterThreshold: 6.5,
                hotExitThreshold: 6.75,
                hotEnterThreshold: 7.0,
                superHotExitThreshold: 12.0,
                superHotEnterThreshold: 14.0),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 0],
                [2, 3],
                [0, 0, 0, 0, 0, 0, 0, 0, 0]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateCooldownStorm() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.CooldownStorm,
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 1,
                minimumSampleCount: 1,
                coldThreshold: 0.0,
                warmExitThreshold: 4.0,
                warmEnterThreshold: 4.5,
                hotExitThreshold: 4.75,
                hotEnterThreshold: 5.0,
                superHotExitThreshold: 9.0,
                superHotEnterThreshold: 10.0),
            new RadarProcessingRebalanceOptions(
                budgetWindowEvaluationCount: 4,
                globalMoveBudgetPerWindow: 1,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 4,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: 4,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: 0.05),
            [
                [0, 0, 0, 0, 1, 1],
                [0, 0, 0, 0, 2, 2]
            ],
            []);

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineTtlRetry() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineTtlRetry,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 1,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineSustainedCoolingClear() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSustainedCoolingClear,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [],
                []
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 10,
                sustainedCoolingSampleCount: 2,
                materialPressureChangeThreshold: 2.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantinePressureChangeRetry() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantinePressureChangeRetry,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0, 0, 0]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 10,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 0.25));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineRetryReentry() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineRetryReentry,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 1,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateQuarantineSuccessfulReliefClear() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.QuarantineSuccessfulReliefClear,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            [
                [0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 1, 1]
            ],
            [
                CreateInitialQuarantine()
            ],
            CreateLifecycleHardeningOptions(
                quarantineTtlEvaluations: 1,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongNoHotShard() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongNoHotShard,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            RepeatBatch(RetentionStressBatchCount, [0, 1, 2, 3]),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongCooldownRejection() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongCooldownRejection,
            CreateImmediateWindowOptions(),
            CreateCooldownRejectionOptions(),
            PrependBatch(
                [0, 0, 0, 0, 1, 1],
                RepeatBatch(RetentionStressBatchCount - 1, [0, 0, 0, 0, 2, 2])),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongUnsafeTargetRejection() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongUnsafeTargetRejection,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            RepeatPattern(
                RetentionStressBatchCount,
                [
                    [0, 0, 0, 0, 0, 0, 0, 0],
                    [1, 1, 1, 1, 1, 1, 1, 1],
                    [2, 2, 2, 2, 2, 2, 2, 2],
                    [3, 3, 3, 3, 3, 3, 3, 3]
                ]),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateLongMixedSkippedReasons() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.LongMixedSkippedReasons,
            CreateImmediateWindowOptions(),
            CreateCooldownRejectionOptions(),
            RepeatPattern(
                RetentionStressBatchCount,
                [
                    [0, 1, 2, 3],
                    [0, 0, 0, 0, 1, 1],
                    [0, 0, 0, 0, 2, 2],
                    [3, 3, 3, 3, 3, 3, 3, 3]
                ]),
            [],
            CreateRetentionStressHardeningOptions());

    private static RadarProcessingSyntheticRebalanceWorkload CreateCountersOnlyRetention() =>
        Create(
            RadarProcessingSyntheticRebalanceWorkloadKind.CountersOnlyRetention,
            CreateImmediateWindowOptions(),
            CreateRelaxedRebalanceOptions(),
            RepeatBatch(RetentionStressBatchCount, [0, 1, 2, 3]),
            [],
            CreateRetentionStressHardeningOptions(RadarProcessingDiagnosticRetentionMode.Counters));

}

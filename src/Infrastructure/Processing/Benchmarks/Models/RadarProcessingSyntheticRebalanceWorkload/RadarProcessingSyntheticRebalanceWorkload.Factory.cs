using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkload
{
    private static RadarProcessingSyntheticRebalanceWorkload Create(
        RadarProcessingSyntheticRebalanceWorkloadKind kind,
        RadarProcessingPressureWindowOptions pressureWindowOptions,
        RadarProcessingRebalanceOptions rebalanceOptions,
        int[][] sourceIdsByBatch,
        InitialHotPartitionClassification[] initialClassifications,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        var sourceUniverse = new RadarSourceUniverse(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: 4,
            rangeBandCount: 1);
        var batches = new RadarEventBatch[sourceIdsByBatch.Length];
        var eventsPerIteration = 0L;
        var payloadValuesPerIteration = 0L;
        var rawValueChecksumPerIteration = 0L;
        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            var batch = CreateBatch(
                sourceUniverse.Version,
                sourceIdsByBatch[batchIndex],
                batchIndex);
            var metrics = RadarEventBatchMetrics.Compute(batch);

            batches[batchIndex] = batch;
            eventsPerIteration = checked(eventsPerIteration + metrics.EventCount);
            payloadValuesPerIteration = checked(payloadValuesPerIteration + metrics.PayloadValueCount);
            rawValueChecksumPerIteration = checked(rawValueChecksumPerIteration + metrics.RawValueChecksum);
        }

        return new RadarProcessingSyntheticRebalanceWorkload(
            kind,
            sourceUniverse,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: 4,
                shardCount: 2),
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0),
            pressureWindowOptions,
            rebalanceOptions,
            hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default,
            batches,
            eventsPerIteration,
            payloadValuesPerIteration,
            rawValueChecksumPerIteration,
            initialClassifications);
    }

    private static RadarProcessingPressureWindowOptions CreateStandardWindowOptions(
        int minimumSampleCount) =>
        new(
            sampleCapacity: Math.Max(2, minimumSampleCount),
            minimumSampleCount: minimumSampleCount,
            coldThreshold: 0.0,
            warmExitThreshold: 4.0,
            warmEnterThreshold: 4.5,
            hotExitThreshold: 4.75,
            hotEnterThreshold: 5.0,
            superHotExitThreshold: 9.0,
            superHotEnterThreshold: 10.0);

    private static RadarProcessingPressureWindowOptions CreateImmediateWindowOptions() =>
        new(
            sampleCapacity: 1,
            minimumSampleCount: 1,
            coldThreshold: 0.0,
            warmExitThreshold: 4.0,
            warmEnterThreshold: 4.5,
            hotExitThreshold: 4.75,
            hotEnterThreshold: 5.0,
            superHotExitThreshold: 9.0,
            superHotEnterThreshold: 10.0);

    private static RadarProcessingRebalanceOptions CreateRelaxedRebalanceOptions() =>
        new(
            budgetWindowEvaluationCount: 4,
            globalMoveBudgetPerWindow: 4,
            sourceShardMoveBudgetPerWindow: 4,
            targetShardReceiveBudgetPerWindow: 4,
            minimumPartitionResidencyEvaluations: 0,
            partitionMoveCooldownEvaluations: 0,
            sourceShardMoveCooldownEvaluations: 0,
            targetShardReceiveCooldownEvaluations: 0,
            minimumProjectedBenefit: 0.05);

    private static RadarProcessingRebalanceOptions CreateCooldownRejectionOptions() =>
        new(
            budgetWindowEvaluationCount: RetentionStressBatchCount * 4,
            globalMoveBudgetPerWindow: 1,
            sourceShardMoveBudgetPerWindow: 4,
            targetShardReceiveBudgetPerWindow: 4,
            minimumPartitionResidencyEvaluations: 0,
            partitionMoveCooldownEvaluations: RetentionStressBatchCount * 4,
            sourceShardMoveCooldownEvaluations: 0,
            targetShardReceiveCooldownEvaluations: 0,
            minimumProjectedBenefit: 0.05);

    private static RadarProcessingRebalanceHardeningOptions CreateLifecycleHardeningOptions(
        int quarantineTtlEvaluations,
        int sustainedCoolingSampleCount,
        double materialPressureChangeThreshold) =>
        new(
            quarantineLifecycle: new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations,
                sustainedCoolingSampleCount,
                materialPressureChangeThreshold));

    private static RadarProcessingRebalanceHardeningOptions CreateRetentionStressHardeningOptions(
        RadarProcessingDiagnosticRetentionMode retentionMode = RadarProcessingDiagnosticRetentionMode.Recent) =>
        new(
            telemetryRetention: new RadarProcessingTelemetryRetentionOptions(
                retentionMode,
                maxRetainedDecisions: RetentionStressDecisionLimit,
                maxRetainedLifecycleTransitions: RetentionStressDecisionLimit,
                maxRetainedAcceptedMoves: RetentionStressDecisionLimit,
                maxRetainedValidationFailures: RetentionStressDecisionLimit));

    private static InitialHotPartitionClassification CreateInitialQuarantine() =>
        new(
            PartitionId: 0,
            ShardId: 0,
            RadarProcessingHotPartitionClassification.Quarantined);

}

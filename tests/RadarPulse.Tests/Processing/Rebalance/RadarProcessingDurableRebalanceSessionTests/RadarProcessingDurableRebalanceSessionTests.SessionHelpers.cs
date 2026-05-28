using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDurableRebalanceSessionTests
{
    private static RadarProcessingDurableBatchId BatchId(
        string value) =>
        new(value);

    private static RadarProcessingRebalanceSession CreateSession(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode executionMode = RadarProcessingExecutionMode.PartitionedBarrier,
        int? shardCount = null,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null)
    {
        var effectiveShardCount = shardCount ?? Math.Min(2, universe.SourceCount);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                executionMode,
                partitionCount: universe.SourceCount,
                shardCount: effectiveShardCount,
                asyncExecution: asyncExecution));

        return new RadarProcessingRebalanceSession(
            core,
            CreatePressureOptions(),
            CreatePressureWindow(),
            CreatePolicyState(universe.SourceCount, effectiveShardCount),
            telemetryRecorder: CreateTelemetryRecorder());
    }

    private static RadarProcessingPressureOptions CreatePressureOptions() =>
        new(
            eventWeight: 1.0,
            payloadValueWeight: 0.0,
            rawValueChecksumWeight: 0.0);

    private static RadarProcessingPressureWindow CreatePressureWindow() =>
        new(
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 2,
                minimumSampleCount: 1,
                coldThreshold: 0.0,
                warmExitThreshold: 4.0,
                warmEnterThreshold: 4.5,
                hotExitThreshold: 4.75,
                hotEnterThreshold: 5.0,
                superHotExitThreshold: 9.0,
                superHotEnterThreshold: 10.0));

    private static RadarProcessingRebalancePolicyState CreatePolicyState(
        int partitionCount,
        int shardCount) =>
        new(
            partitionCount,
            shardCount,
            new RadarProcessingRebalanceOptions(
                budgetWindowEvaluationCount: 4,
                globalMoveBudgetPerWindow: 4,
                sourceShardMoveBudgetPerWindow: 4,
                targetShardReceiveBudgetPerWindow: 4,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: 0,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: 0.05));

    private static RadarProcessingRebalanceTelemetryRecorder CreateTelemetryRecorder() =>
        new(
            new RadarProcessingTelemetryRetentionOptions(
                RadarProcessingDiagnosticRetentionMode.Recent,
                maxRetainedDecisions: 8,
                maxRetainedLifecycleTransitions: 8,
                maxRetainedAcceptedMoves: 8,
                maxRetainedValidationFailures: 8,
                maxRetainedWorkerBatches: 8,
                maxRetainedWorkerFailures: 8));

    private static RadarSourceUniverse CreateUniverse(
        int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
}

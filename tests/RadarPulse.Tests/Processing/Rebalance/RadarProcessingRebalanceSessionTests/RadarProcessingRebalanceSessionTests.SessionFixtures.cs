using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceSessionTests
{
    private static RadarProcessingRebalanceSession CreateSession(
        RadarSourceUniverse universe,
        int shardCount = 2,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount: universe.SourceCount,
                shardCount: shardCount));

        return new RadarProcessingRebalanceSession(
            core,
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0),
            new RadarProcessingPressureWindow(
                new RadarProcessingPressureWindowOptions(
                    sampleCapacity: 2,
                    minimumSampleCount: 1,
                    coldThreshold: 0.0,
                    warmExitThreshold: 4.0,
                    warmEnterThreshold: 4.5,
                    hotExitThreshold: 4.75,
                    hotEnterThreshold: 5.0,
                    superHotExitThreshold: 9.0,
                    superHotEnterThreshold: 10.0)),
            new RadarProcessingRebalancePolicyState(
                universe.SourceCount,
                shardCount: shardCount,
                new RadarProcessingRebalanceOptions(
                    budgetWindowEvaluationCount: 4,
                    globalMoveBudgetPerWindow: 4,
                    sourceShardMoveBudgetPerWindow: 4,
                    targetShardReceiveBudgetPerWindow: 4,
                    minimumPartitionResidencyEvaluations: 0,
                    partitionMoveCooldownEvaluations: 0,
                    sourceShardMoveCooldownEvaluations: 0,
                    targetShardReceiveCooldownEvaluations: 0,
                    minimumProjectedBenefit: 0.05)),
            telemetryRecorder: new RadarProcessingRebalanceTelemetryRecorder(
                new RadarProcessingTelemetryRetentionOptions(
                    RadarProcessingDiagnosticRetentionMode.Recent,
                    maxRetainedDecisions: 8,
                    maxRetainedLifecycleTransitions: 8,
                    maxRetainedAcceptedMoves: 8,
                    maxRetainedValidationFailures: 8)),
            quarantineLifecycleTracker: quarantineLifecycleTracker,
            hardeningOptions: hardeningOptions);
    }

    private static RadarProcessingQuarantineLifecycleTracker CreateLifecycleTracker(
        int partitionCount,
        int quarantineTtlEvaluations) =>
        new(
            partitionCount,
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations,
                sustainedCoolingSampleCount: 3,
                materialPressureChangeThreshold: 1.0));
}

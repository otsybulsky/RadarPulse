using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingAsyncRebalanceSessionTests
{
    private static RadarProcessingRebalanceSession CreateSyncSession(
        RadarSourceUniverse universe,
        int shardCount = 2) =>
        new(
            CreateCore(
                universe,
                RadarProcessingExecutionMode.PartitionedBarrier,
                universe.SourceCount,
                shardCount),
            CreatePressureOptions(),
            CreatePressureWindow(),
            CreatePolicyState(universe.SourceCount, shardCount),
            telemetryRecorder: CreateTelemetryRecorder());

    private static RadarProcessingAsyncRebalanceSession CreateAsyncSession(
        RadarSourceUniverse universe,
        int shardCount = 2,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null)
    {
        var core = CreateCore(
            universe,
            RadarProcessingExecutionMode.AsyncShardTransport,
            universe.SourceCount,
            shardCount,
            asyncExecution ?? new RadarProcessingAsyncExecutionOptions(workerCount: shardCount, queueCapacity: 1),
            handlers);
        return new RadarProcessingAsyncRebalanceSession(
            core,
            CreatePressureOptions(),
            CreatePressureWindow(),
            CreatePolicyState(universe.SourceCount, shardCount),
            telemetryRecorder: CreateTelemetryRecorder());
    }

    private static RadarProcessingCore CreateCore(
        RadarSourceUniverse universe,
        RadarProcessingExecutionMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingAsyncExecutionOptions? asyncExecution = null,
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers = null) =>
        new(
            universe,
            new RadarProcessingCoreOptions(
                mode,
                partitionCount,
                shardCount,
                handlers: handlers,
                asyncExecution: asyncExecution));

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
}

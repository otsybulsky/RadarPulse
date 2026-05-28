using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingDirectHotReliefPlannerTests
{
    private static RadarProcessingRebalancePolicyState CreatePolicyState(
        int partitionCount,
        int shardCount,
        int partitionMoveCooldownEvaluations = 0,
        int globalMoveBudgetPerWindow = 1,
        int sourceShardMoveBudgetPerWindow = 1,
        int targetShardReceiveBudgetPerWindow = 1,
        double minimumProjectedBenefit = 0.05) =>
        new(
            partitionCount,
            shardCount,
            new RadarProcessingRebalanceOptions(
                globalMoveBudgetPerWindow: globalMoveBudgetPerWindow,
                sourceShardMoveBudgetPerWindow: sourceShardMoveBudgetPerWindow,
                targetShardReceiveBudgetPerWindow: targetShardReceiveBudgetPerWindow,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: partitionMoveCooldownEvaluations,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: minimumProjectedBenefit));

    private static RadarProcessingPressureWindow CreateWindow(
        int partitionCount,
        int shardCount,
        int[][] samples)
    {
        var window = new RadarProcessingPressureWindow(
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 2,
                minimumSampleCount: 2,
                coldThreshold: 0.0,
                warmExitThreshold: 6.0,
                warmEnterThreshold: 6.5,
                hotExitThreshold: 6.75,
                hotEnterThreshold: 7.0,
                superHotExitThreshold: 12.0,
                superHotEnterThreshold: 14.0));

        foreach (var sourceIds in samples)
        {
            window.AddSample(CreateSample(partitionCount, shardCount, sourceIds));
        }

        return window;
    }

    private static RadarProcessingPressureSample CreateSample(
        int partitionCount,
        int shardCount,
        int[] sourceIds)
    {
        var universe = CreateUniverse(partitionCount);
        var core = new RadarProcessingCore(
            universe,
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(
            core.Process(CreateEightBitBatch(universe.Version, sourceIds)).Telemetry);

        return RadarProcessingPressureSample.FromTelemetry(
            telemetry,
            new RadarProcessingPressureOptions(
                eventWeight: 1.0,
                payloadValueWeight: 0.0,
                rawValueChecksumWeight: 0.0));
    }

}

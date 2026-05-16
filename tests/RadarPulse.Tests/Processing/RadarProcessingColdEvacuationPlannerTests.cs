using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingColdEvacuationPlannerTests
{
    [Fact]
    public void DirectHotReliefUnsafeAllowsColdEvacuation()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 4);
        classifier.ClassifyQuarantined(
            partitionId: 1,
            shardId: 0,
            evaluationSequence: 0);
        var directPlanner = new RadarProcessingDirectHotReliefPlanner();
        var coldPlanner = new RadarProcessingColdEvacuationPlanner();

        var directDecision = directPlanner.Plan(1, window, policyState, classifier);
        var coldDecision = coldPlanner.Plan(2, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, directDecision.Kind);
        Assert.True(classifier.GetPartition(0).IsIntrinsicHot);
        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, coldDecision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, coldDecision.MoveKind);
        Assert.Equal(1, coldDecision.PartitionId);
        Assert.Equal(0, coldDecision.SourceShardId);
        Assert.Equal(1, coldDecision.TargetShardId);
        Assert.Equal(1.0, coldDecision.ExpectedRelief);
        Assert.Equal(9.0, coldDecision.ProjectedPressure.SourceShardBefore.Value);
        Assert.Equal(8.0, coldDecision.ProjectedPressure.SourceShardAfter.Value);
        Assert.Equal(1.0, coldDecision.ProjectedPressure.TargetShardAfter.Value);
    }

    [Fact]
    public void SmallestUsefulColdPartitionIsSelectedDeterministically()
    {
        var window = CreateWindow(
            partitionCount: 6,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2],
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2]
            ]);
        var policyState = CreatePolicyState(partitionCount: 6, shardCount: 2);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(3, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, decision.MoveKind);
        Assert.Equal(2, decision.PartitionId);
        Assert.Equal(1.0, decision.ExpectedRelief);
    }

    [Fact]
    public void TargetShardRemainsBelowConfiguredHeadroomThreshold()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            targetHeadroomThreshold: 2.0);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(4, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(decision.ProjectedPressure.TargetShardAfter.Value <= policyState.Options.TargetHeadroomThreshold);
    }

    [Fact]
    public void ColdEvacuationIsRejectedWhenProjectedReliefIsTooSmall()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            minimumProjectedBenefit: 2.0);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(5, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, decision.MoveKind);
        Assert.Equal(1.0, decision.ExpectedRelief);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.ColdEvacuationInsufficientBenefit,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit,
            decision.SkippedReasons);
    }

    [Fact]
    public void ColdEvacuationIsRejectedWhenTargetWouldBecomeWarm()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]
            ],
            warmEnterThreshold: 2.0);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(6, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.ColdEvacuation, decision.MoveKind);
        Assert.Equal(1, decision.PartitionId);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeWarm,
            decision.SkippedReasons);
    }

    [Fact]
    public void SourceShardMoveBudgetCapsRepeatedColdEvacuation()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            globalMoveBudgetPerWindow: 4,
            sourceShardMoveBudgetPerWindow: 1,
            targetShardReceiveBudgetPerWindow: 4);
        policyState.RecordAcceptedMove(
            new RadarProcessingRebalanceMovePolicyInput(
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 1,
                projectedBenefit: 1.0,
                targetProjectedPressure: new RadarProcessingPressureScore(1.0)));
        var planner = new RadarProcessingColdEvacuationPlanner();

        var decision = planner.Plan(7, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.SourceShardMoveBudgetExhausted,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.SourceShardMoveBudgetExhausted,
            decision.SkippedReasons);
    }

    private static RadarProcessingRebalancePolicyState CreatePolicyState(
        int partitionCount,
        int shardCount,
        int globalMoveBudgetPerWindow = 1,
        int sourceShardMoveBudgetPerWindow = 1,
        int targetShardReceiveBudgetPerWindow = 1,
        double minimumProjectedBenefit = 0.05,
        double targetHeadroomThreshold = double.MaxValue) =>
        new(
            partitionCount,
            shardCount,
            new RadarProcessingRebalanceOptions(
                globalMoveBudgetPerWindow: globalMoveBudgetPerWindow,
                sourceShardMoveBudgetPerWindow: sourceShardMoveBudgetPerWindow,
                targetShardReceiveBudgetPerWindow: targetShardReceiveBudgetPerWindow,
                minimumPartitionResidencyEvaluations: 0,
                partitionMoveCooldownEvaluations: 0,
                sourceShardMoveCooldownEvaluations: 0,
                targetShardReceiveCooldownEvaluations: 0,
                minimumProjectedBenefit: minimumProjectedBenefit,
                targetHeadroomThreshold: targetHeadroomThreshold));

    private static RadarProcessingPressureWindow CreateWindow(
        int partitionCount,
        int shardCount,
        int[][] samples,
        double warmEnterThreshold = 6.5)
    {
        var warmExitThreshold = Math.Max(0.0, warmEnterThreshold - 0.5);
        var hotExitThreshold = Math.Max(warmEnterThreshold, 6.75);
        var hotEnterThreshold = Math.Max(hotExitThreshold, 7.0);
        var window = new RadarProcessingPressureWindow(
            new RadarProcessingPressureWindowOptions(
                sampleCapacity: 2,
                minimumSampleCount: 2,
                coldThreshold: 0.0,
                warmExitThreshold: warmExitThreshold,
                warmEnterThreshold: warmEnterThreshold,
                hotExitThreshold: hotExitThreshold,
                hotEnterThreshold: hotEnterThreshold,
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

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);

    private static RadarEventBatch CreateEightBitBatch(
        SourceUniverseVersion sourceUniverseVersion,
        int[] sourceIds)
    {
        var events = new RadarStreamEvent[sourceIds.Length];
        var payload = new byte[sourceIds.Length];

        for (var i = 0; i < sourceIds.Length; i++)
        {
            events[i] = CreateEvent(
                sourceIds[i],
                messageTimestampUtcTicks: 100 + i,
                payloadOffset: i);
            payload[i] = (byte)(i + 1);
        }

        return new RadarEventBatch(
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverseVersion,
            events,
            payload);
    }

    private static RadarStreamEvent CreateEvent(
        int sourceId,
        long messageTimestampUtcTicks,
        int payloadOffset) =>
        new(
            sourceId,
            radarOrdinal: 0,
            volumeTimestampUtcTicks: 90,
            messageTimestampUtcTicks,
            sourceRecord: 1,
            sourceMessage: 1,
            radialSequence: payloadOffset,
            elevationSlot: 0,
            azimuthBucket: (ushort)sourceId,
            rangeBand: 0,
            momentId: 0,
            gateStart: 0,
            gateCount: 1,
            wordSize: RadarStreamWordSize.EightBit,
            scale: 1.0f,
            offset: 0.0f,
            statusModel: RadarStreamStatusModel.ArchiveTwoMoment,
            payloadOffset: payloadOffset,
            payloadLength: 1);
}

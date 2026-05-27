using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingDirectHotReliefPlannerTests
{
    [Fact]
    public void SustainedHotShardProducesDirectReliefCandidate()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(1, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(decision.HasAcceptedMove);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Equal(0, decision.SourceShardId);
        Assert.Equal(1, decision.TargetShardId);
        Assert.Equal(4.0, decision.ExpectedRelief);
        Assert.Equal(8.0, decision.ProjectedPressure.SourceShardBefore.Value);
        Assert.Equal(0.0, decision.ProjectedPressure.TargetShardBefore.Value);
        Assert.Equal(4.0, decision.ProjectedPressure.SourceShardAfter.Value);
        Assert.Equal(4.0, decision.ProjectedPressure.TargetShardAfter.Value);
        Assert.Empty(decision.SkippedReasons);
        Assert.Empty(decision.PolicyRejections);
    }

    [Fact]
    public void LargestUsefulPartitionIsSelectedDeterministically()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 1, 1, 1],
                [0, 0, 0, 0, 0, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(2, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Equal(5.0, decision.ProjectedPressure.TargetShardAfter.Value);
        Assert.Equal(3.0, decision.ExpectedRelief);
    }

    [Fact]
    public void CandidateIsRejectedWhenEveryTargetWouldBecomeHot()
    {
        var window = CreateWindow(
            partitionCount: 2,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0, 0, 0]
            ]);
        var policyState = CreatePolicyState(partitionCount: 2, shardCount: 2);
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(3, window, policyState, classifier);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, decision.MoveKind);
        Assert.Equal(0, decision.PartitionId);
        Assert.True(classifier.GetPartition(0).IsIntrinsicHot);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

    [Fact]
    public void IntrinsicHotPartitionIsNotSelectedForDirectMovement()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 4);
        classifier.ClassifyIntrinsicHot(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(31, window, policyState, classifier);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(1, decision.PartitionId);
        Assert.Equal(0, decision.SourceShardId);
        Assert.Equal(1, decision.TargetShardId);
    }

    [Fact]
    public void ClassifiedPartitionsReturnDiagnosticNoActionWhenEveryHotPartitionIsBlocked()
    {
        var window = CreateWindow(
            partitionCount: 2,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0, 0, 0]
            ]);
        var policyState = CreatePolicyState(partitionCount: 2, shardCount: 2);
        var classifier = new RadarProcessingHotPartitionClassifier(partitionCount: 2);
        classifier.ClassifyIntrinsicHot(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(32, window, policyState, classifier);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

    [Fact]
    public void ActiveLifecycleQuarantineIsNotSelectedForDirectMovement()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 4);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(4),
            RadarProcessingPressureBand.Hot,
            RadarProcessingHotPartitionClassification.Quarantined);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            33,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(1, decision.PartitionId);
        Assert.True(lifecycle.GetPartition(0).IsQuarantined);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.MovableHot, lifecycle.GetPartition(1).EffectiveClassification);
    }

    [Fact]
    public void ActiveLifecycleQuarantineReportsExplicitSkippedReasonWhenEveryHotPartitionIsBlocked()
    {
        var window = CreateWindow(
            partitionCount: 2,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0, 0, 0]
            ]);
        var policyState = CreatePolicyState(partitionCount: 2, shardCount: 2);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(partitionCount: 2);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(8),
            RadarProcessingPressureBand.SuperHot,
            RadarProcessingHotPartitionClassification.Quarantined);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            34,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            decision.SkippedReasons);
        Assert.DoesNotContain(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

    [Fact]
    public void RetryEligibleLifecyclePartitionCanBeReconsideredForDirectMovement()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        Advance(policyState, 2);
        var lifecycle = CreateRetryEligibleLifecycle(
            partitionCount: 4,
            partitionId: 0,
            shardId: 0,
            pressure: 4,
            band: RadarProcessingPressureBand.Hot,
            topologyVersion: window.LatestTopologyVersion);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            35,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Equal(RadarProcessingQuarantineEffectiveClassification.MovableHot, lifecycle.GetPartition(0).EffectiveClassification);
        Assert.False(lifecycle.GetPartition(0).HasQuarantineEvidence);
    }

    [Fact]
    public void RetryEligibleLifecyclePartitionReentersQuarantineWhenNoSafeTargetExists()
    {
        var window = CreateWindow(
            partitionCount: 2,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 0, 0],
                [0, 0, 0, 0, 0, 0, 0, 0]
            ]);
        var policyState = CreatePolicyState(partitionCount: 2, shardCount: 2);
        Advance(policyState, 2);
        var lifecycle = CreateRetryEligibleLifecycle(
            partitionCount: 2,
            partitionId: 0,
            shardId: 0,
            pressure: 8,
            band: RadarProcessingPressureBand.SuperHot,
            topologyVersion: window.LatestTopologyVersion);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            36,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.True(lifecycle.GetPartition(0).IsQuarantined);
        Assert.Equal(2, lifecycle.GetPartition(0).QuarantineStartSequence);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.DirectHotPartitionHasNoSafeTarget,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.TargetWouldBecomeHot,
            decision.SkippedReasons);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            decision.SkippedReasons);
        Assert.DoesNotContain(
            RadarProcessingRebalanceSkippedReason.PartitionClassifiedIntrinsicHot,
            decision.SkippedReasons);
    }

    [Fact]
    public void ClearedLifecycleQuarantineDoesNotReportStaleSkippedReason()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        Advance(policyState, 3);
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(
            partitionCount: 4,
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations: 10,
                sustainedCoolingSampleCount: 2,
                materialPressureChangeThreshold: 1.0));
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 0,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(4),
            RadarProcessingPressureBand.Hot,
            RadarProcessingHotPartitionClassification.Quarantined);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 1,
            window.LatestTopologyVersion,
            new RadarProcessingPressureScore(1),
            RadarProcessingPressureBand.Normal,
            RadarProcessingHotPartitionClassification.None);
        lifecycle.RecordEvidence(
            partitionId: 0,
            shardId: 0,
            evaluationSequence: 2,
            window.LatestTopologyVersion,
            RadarProcessingPressureScore.Zero,
            RadarProcessingPressureBand.Cold,
            RadarProcessingHotPartitionClassification.None);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(
            37,
            window,
            policyState,
            quarantineLifecycleTracker: lifecycle);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.DoesNotContain(
            RadarProcessingRebalanceSkippedReason.PartitionQuarantined,
            decision.SkippedReasons);
    }

    [Fact]
    public void CandidateIsRejectedWhenProjectedReliefIsTooSmall()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 0, 0, 1],
                [0, 0, 0, 0, 0, 0, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            minimumProjectedBenefit: 2.0);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(4, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(1.0, decision.ExpectedRelief);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.InsufficientProjectedBenefit,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.InsufficientProjectedBenefit,
            decision.SkippedReasons);
    }

    [Fact]
    public void CandidateIsRejectedDuringCooldown()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(
            partitionCount: 4,
            shardCount: 2,
            partitionMoveCooldownEvaluations: 2,
            globalMoveBudgetPerWindow: 4,
            sourceShardMoveBudgetPerWindow: 4,
            targetShardReceiveBudgetPerWindow: 4);
        policyState.RecordAcceptedMove(
            new RadarProcessingRebalanceMovePolicyInput(
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1,
                projectedBenefit: 4.0,
                targetProjectedPressure: new RadarProcessingPressureScore(4.0)));
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(5, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.RejectedCandidate, decision.Kind);
        Assert.Equal(0, decision.PartitionId);
        Assert.Contains(
            RadarProcessingRebalancePolicyRejection.PartitionInCooldown,
            decision.PolicyRejections);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.CandidatePartitionInCooldown,
            decision.SkippedReasons);
    }

    [Fact]
    public void AcceptedDirectMoveLowersProjectedMaxPressure()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1],
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(6, window, policyState);
        var projectedMax = Math.Max(
            decision.ProjectedPressure.SourceShardAfter.Value,
            decision.ProjectedPressure.TargetShardAfter.Value);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.AcceptedMove, decision.Kind);
        Assert.True(projectedMax < decision.ProjectedPressure.SourceShardBefore.Value);
    }

    [Fact]
    public void IneligibleWindowReturnsNoSustainedPressure()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 0, 0, 1, 1, 1, 1]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(7, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.NoSustainedPressure,
            decision.SkippedReasons);
    }

    [Fact]
    public void EligibleWindowWithoutHotShardReturnsNoHotShard()
    {
        var window = CreateWindow(
            partitionCount: 4,
            shardCount: 2,
            samples:
            [
                [0, 0, 1, 2],
                [0, 0, 1, 2]
            ]);
        var policyState = CreatePolicyState(partitionCount: 4, shardCount: 2);
        var planner = new RadarProcessingDirectHotReliefPlanner();

        var decision = planner.Plan(8, window, policyState);

        Assert.Equal(RadarProcessingRebalanceDecisionKind.NoAction, decision.Kind);
        Assert.Contains(
            RadarProcessingRebalanceSkippedReason.NoHotShard,
            decision.SkippedReasons);
    }

    private static RadarProcessingQuarantineLifecycleTracker CreateRetryEligibleLifecycle(
        int partitionCount,
        int partitionId,
        int shardId,
        double pressure,
        RadarProcessingPressureBand band,
        RadarProcessingTopologyVersion topologyVersion)
    {
        var lifecycle = new RadarProcessingQuarantineLifecycleTracker(
            partitionCount,
            new RadarProcessingQuarantineLifecycleOptions(
                quarantineTtlEvaluations: 2,
                sustainedCoolingSampleCount: 5,
                materialPressureChangeThreshold: 1.0));
        lifecycle.RecordEvidence(
            partitionId,
            shardId,
            evaluationSequence: 0,
            topologyVersion,
            new RadarProcessingPressureScore(pressure),
            band,
            RadarProcessingHotPartitionClassification.Quarantined);
        lifecycle.RecordEvidence(
            partitionId,
            shardId,
            evaluationSequence: 2,
            topologyVersion,
            new RadarProcessingPressureScore(pressure),
            band,
            RadarProcessingHotPartitionClassification.None);

        Assert.True(lifecycle.GetPartition(partitionId).IsRetryEligible);
        return lifecycle;
    }

    private static void Advance(
        RadarProcessingRebalancePolicyState policyState,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            policyState.AdvanceEvaluation();
        }
    }

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

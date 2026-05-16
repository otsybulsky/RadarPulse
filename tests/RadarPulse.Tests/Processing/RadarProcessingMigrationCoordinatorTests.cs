using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingMigrationCoordinatorTests
{
    [Fact]
    public void AcceptedDecisionMigratesPartitionAndPublishesNextTopologyVersion()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var previous = manager.Current;
        var decision = CreateAcceptedDecision(
            previous.Version,
            partitionId: 1,
            sourceShardId: 0,
            targetShardId: 2);
        var coordinator = new RadarProcessingMigrationCoordinator(manager);

        var result = coordinator.Apply(decision);

        Assert.True(result.Succeeded);
        Assert.Equal(RadarProcessingPartitionMigrationState.Published, result.State);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(RadarProcessingMigrationValidationError.None, result.Validation.Error);
        Assert.NotNull(result.Migration);
        Assert.Equal(decision.DecisionId, result.Migration.DecisionId);
        Assert.Equal(RadarProcessingRebalanceMoveKind.DirectHotRelief, result.Migration.MoveKind);
        Assert.Equal(previous.Version, result.PreviousTopologyVersion);
        Assert.Equal(previous.Version.Next(), result.CurrentTopologyVersion);
        Assert.Equal(previous.Version.Next(), manager.Current.Version);
        Assert.Equal(2, manager.Current.GetShardIdForPartition(1));
        Assert.Equal(0, previous.GetShardIdForPartition(1));
    }

    [Fact]
    public void StaleDecisionIsRejectedWithoutPublishingTopology()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var initial = manager.Current;
        var staleDecision = CreateAcceptedDecision(
            initial.Version,
            partitionId: 0,
            sourceShardId: 0,
            targetShardId: 1);
        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                initial.Version,
                partitionId: 1,
                sourceShardId: 0,
                targetShardId: 2));
        var current = manager.Current;
        var coordinator = new RadarProcessingMigrationCoordinator(manager);

        var result = coordinator.Apply(staleDecision);

        Assert.False(result.Succeeded);
        Assert.Equal(RadarProcessingPartitionMigrationState.ValidationFailed, result.State);
        Assert.False(result.Validation.IsValid);
        Assert.Equal(RadarProcessingMigrationValidationError.StaleTopologyVersion, result.Validation.Error);
        Assert.Equal(current.Version, result.PreviousTopologyVersion);
        Assert.Equal(current.Version, result.CurrentTopologyVersion);
        Assert.Same(current, manager.Current);
        Assert.Equal(0, manager.Current.GetShardIdForPartition(0));
    }

    [Fact]
    public void WrongOldOwnerIsRejectedWithoutPublishingTopology()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var current = manager.Current;
        var decision = CreateAcceptedDecision(
            current.Version,
            partitionId: 0,
            sourceShardId: 1,
            targetShardId: 2);
        var coordinator = new RadarProcessingMigrationCoordinator(manager);

        var result = coordinator.Apply(decision);

        Assert.False(result.Succeeded);
        Assert.Equal(RadarProcessingPartitionMigrationState.ValidationFailed, result.State);
        Assert.Equal(
            RadarProcessingMigrationValidationError.SourceShardOwnershipMismatch,
            result.Validation.Error);
        Assert.Same(current, manager.Current);
        Assert.Equal(current.Version, manager.Current.Version);
        Assert.Equal(0, manager.Current.GetShardIdForPartition(0));
    }

    [Fact]
    public void FailedValidationDoesNotPublishNewTopology()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var current = manager.Current;
        var decision = CreateAcceptedDecision(
            current.Version,
            partitionId: 0,
            sourceShardId: 0,
            targetShardId: 9);
        var coordinator = new RadarProcessingMigrationCoordinator(manager);

        var result = coordinator.Apply(decision);

        Assert.False(result.Succeeded);
        Assert.Equal(RadarProcessingPartitionMigrationState.ValidationFailed, result.State);
        Assert.Equal(RadarProcessingMigrationValidationError.TargetShardIdOutOfRange, result.Validation.Error);
        Assert.Same(current, manager.Current);
        Assert.Equal(current.Version, result.CurrentTopologyVersion);
        Assert.Equal(current.Version, result.PreviousTopologyVersion);
    }

    [Fact]
    public void NonAcceptedDecisionIsRejectedBeforeValidation()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var current = manager.Current;
        var decision = RadarProcessingRebalanceDecision.NoAction(
            decisionId: 5,
            evaluationSequence: 0,
            current.Version,
            pressureWindowSampleCount: 2,
            [RadarProcessingRebalanceSkippedReason.NoHotShard]);
        var coordinator = new RadarProcessingMigrationCoordinator(manager);

        var result = coordinator.Apply(decision);

        Assert.False(result.Succeeded);
        Assert.Equal(RadarProcessingPartitionMigrationState.RejectedDecision, result.State);
        Assert.Equal(RadarProcessingMigrationValidationError.DecisionNotAcceptedMove, result.Validation.Error);
        Assert.Null(result.Migration);
        Assert.Same(current, manager.Current);
    }

    [Fact]
    public void ValidationResultCanBeCheckedWithoutPublishing()
    {
        var manager = CreateManager(sourceCount: 12, partitionCount: 6, shardCount: 3);
        var current = manager.Current;
        var decision = CreateAcceptedDecision(
            current.Version,
            partitionId: 1,
            sourceShardId: 0,
            targetShardId: 2);
        var coordinator = new RadarProcessingMigrationCoordinator(manager);

        var validation = coordinator.Validate(decision);

        Assert.True(validation.IsValid);
        Assert.Equal(RadarProcessingMigrationValidationError.None, validation.Error);
        Assert.NotNull(validation.Migration);
        Assert.Equal(current.Version, validation.CurrentTopologyVersion);
        Assert.Same(current, manager.Current);
        Assert.Equal(RadarProcessingTopologyVersion.Initial, manager.Current.Version);
        Assert.Equal(0, manager.Current.GetShardIdForPartition(1));
    }

    [Fact]
    public void MigrationContractsRejectInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPartitionMigration(
                decisionId: -1,
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
                RadarProcessingTopologyVersion.Initial,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingPartitionMigration(
                decisionId: 1,
                RadarProcessingRebalanceMoveKind.None,
                RadarProcessingTopologyVersion.Initial,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));
        Assert.Throws<ArgumentException>(() =>
            new RadarProcessingPartitionMigration(
                decisionId: 1,
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
                RadarProcessingTopologyVersion.Initial,
                partitionId: 0,
                sourceShardId: 1,
                targetShardId: 1));
    }

    private static RadarProcessingRebalanceDecision CreateAcceptedDecision(
        RadarProcessingTopologyVersion topologyVersion,
        int partitionId,
        int sourceShardId,
        int targetShardId) =>
        RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 1,
            evaluationSequence: 0,
            topologyVersion,
            pressureWindowSampleCount: 2,
            new RadarProcessingRebalanceCandidate(
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
                partitionId,
                sourceShardId,
                targetShardId,
                new RadarProcessingProjectedPressure(
                    new RadarProcessingPressureScore(8.0),
                    RadarProcessingPressureScore.Zero,
                    new RadarProcessingPressureScore(4.0),
                    new RadarProcessingPressureScore(4.0)),
                expectedRelief: 4.0));

    private static RadarProcessingTopologyManager CreateManager(
        int sourceCount,
        int partitionCount,
        int shardCount) =>
        new(
            CreateUniverse(sourceCount),
            new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount));

    private static RadarSourceUniverse CreateUniverse(int sourceCount) =>
        new(
            SourceUniverseVersion.Initial,
            radarOrdinalCount: 1,
            elevationSlotCount: 1,
            azimuthBucketCount: sourceCount,
            rangeBandCount: 1);
}

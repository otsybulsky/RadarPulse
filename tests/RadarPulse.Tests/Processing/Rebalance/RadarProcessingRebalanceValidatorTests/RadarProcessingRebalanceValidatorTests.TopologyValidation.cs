using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceValidatorTests
{
    [Fact]
    public void ValidTopologySequencePasses()
    {
        var manager = CreateManager(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var previous = manager.Current;
        var move = new RadarProcessingPartitionMigration(
            decisionId: 1,
            RadarProcessingRebalanceMoveKind.DirectHotRelief,
            previous.Version,
            partitionId: 0,
            sourceShardId: 0,
            targetShardId: 1);

        manager.MovePartition(move.ToTopologyMoveRequest());

        var sequence = RadarProcessingRebalanceValidator.ValidateTopologySequence(previous, manager.Current);
        var acceptedMove = RadarProcessingRebalanceValidator.ValidateAcceptedMove(previous, manager.Current, move);

        Assert.True(sequence.IsValid);
        Assert.True(acceptedMove.IsValid);
    }

    [Fact]
    public void NonMonotonicTopologyVersionIsRejected()
    {
        var manager = CreateManager(sourceCount: 4, partitionCount: 4, shardCount: 2);
        var previous = manager.Current;
        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                previous.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));

        var result = RadarProcessingRebalanceValidator.ValidateTopologySequence(manager.Current, previous);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.NonMonotonicTopologyVersion);
    }

    [Fact]
    public void MixedRouteAndTelemetryTopologyVersionsAreRejected()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var options = CreateOptions(partitionCount: 4, shardCount: 2);
        var core = new RadarProcessingCore(universe, options);
        var manager = new RadarProcessingTopologyManager(universe, options);
        var batch = CreateEightBitBatch(universe.Version, [0, 1]);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                manager.Current.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));
        var route = new RadarProcessingBatchRouter(manager.Current).Route(batch);

        var result = RadarProcessingRebalanceValidator.ValidateRouteTelemetry(
            route,
            telemetry,
            manager.Current);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.RouteTelemetryTopologyVersionMismatch);
    }

    [Fact]
    public void PartitionOwnerMismatchIsRejected()
    {
        var universe = CreateUniverse(sourceCount: 4);
        var options = CreateOptions(partitionCount: 4, shardCount: 2);
        var core = new RadarProcessingCore(universe, options);
        var manager = new RadarProcessingTopologyManager(universe, options);
        var batch = CreateEightBitBatch(universe.Version, [0, 1]);
        var route = new RadarProcessingBatchRouter(manager.Current).Route(batch);
        var telemetry = Assert.IsType<RadarProcessingTelemetry>(core.Process(batch).Telemetry);

        manager.MovePartition(
            new RadarProcessingTopologyMoveRequest(
                manager.Current.Version,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1));

        var result = RadarProcessingRebalanceValidator.ValidateRouteTelemetry(
            route,
            telemetry,
            manager.Current);

        AssertInvalid(result, RadarProcessingRebalanceValidationError.RoutePartitionOwnerMismatch);
    }
}

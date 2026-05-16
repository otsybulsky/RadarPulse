using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingTopologyManager
{
    public RadarProcessingTopologyManager(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions options)
    {
        Current = new RadarProcessingTopology(sourceUniverse, options);
    }

    public RadarProcessingTopology Current { get; private set; }

    public RadarProcessingTopologyMoveResult MovePartition(
        RadarProcessingTopologyMoveRequest request)
    {
        var validationError = ValidateMoveRequest(request);
        if (validationError != RadarProcessingTopologyMoveError.None)
        {
            return RadarProcessingTopologyMoveResult.Rejected(validationError, Current, request);
        }

        var previous = Current;
        Current = previous.MovePartitionOwner(
            request.PartitionId,
            request.TargetShardId,
            previous.Version.Next());

        return RadarProcessingTopologyMoveResult.Accepted(previous, Current, request);
    }

    private RadarProcessingTopologyMoveError ValidateMoveRequest(
        RadarProcessingTopologyMoveRequest request)
    {
        if (request.ExpectedTopologyVersion != Current.Version)
        {
            return RadarProcessingTopologyMoveError.StaleTopologyVersion;
        }

        if ((uint)request.PartitionId >= (uint)Current.PartitionCount)
        {
            return RadarProcessingTopologyMoveError.PartitionIdOutOfRange;
        }

        if ((uint)request.SourceShardId >= (uint)Current.ShardCount)
        {
            return RadarProcessingTopologyMoveError.SourceShardIdOutOfRange;
        }

        if ((uint)request.TargetShardId >= (uint)Current.ShardCount)
        {
            return RadarProcessingTopologyMoveError.TargetShardIdOutOfRange;
        }

        if (request.SourceShardId == request.TargetShardId)
        {
            return RadarProcessingTopologyMoveError.NoOpMove;
        }

        var currentOwner = Current.GetShardIdForPartition(request.PartitionId);
        return currentOwner == request.SourceShardId
            ? RadarProcessingTopologyMoveError.None
            : RadarProcessingTopologyMoveError.SourceShardOwnershipMismatch;
    }
}

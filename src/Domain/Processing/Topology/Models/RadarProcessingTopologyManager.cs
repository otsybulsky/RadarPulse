using RadarPulse.Domain.Streaming;

namespace RadarPulse.Domain.Processing;

/// <summary>
/// Owns the mutable current processing topology snapshot.
/// </summary>
/// <remarks>
/// Callers publish rebalance moves through this manager so stale topology
/// versions and mismatched source ownership are rejected before a new topology
/// version becomes current.
/// </remarks>
public sealed class RadarProcessingTopologyManager
{
    /// <summary>
    /// Creates a manager with an initial topology for the source universe.
    /// </summary>
    public RadarProcessingTopologyManager(
        RadarSourceUniverse sourceUniverse,
        RadarProcessingCoreOptions options)
    {
        Current = new RadarProcessingTopology(sourceUniverse, options);
    }

    /// <summary>
    /// Current topology snapshot used for routing and validation.
    /// </summary>
    public RadarProcessingTopology Current { get; private set; }

    /// <summary>
    /// Attempts to move a partition to a target shard and publish a new topology version.
    /// </summary>
    /// <returns>
    /// Accepted result when the expected version and owner match; otherwise a rejected result
    /// with the current topology unchanged.
    /// </returns>
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

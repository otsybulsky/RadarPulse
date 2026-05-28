namespace RadarPulse.Domain.Processing;

public sealed partial class RadarProcessingRebalanceSession
{
    private (RadarProcessingMigrationResult? Migration, RadarProcessingStateHandoffValidationResult? Handoff)
        ApplyAcceptedMove(RadarProcessingRebalanceDecision decision)
    {
        var candidate = decision.Candidate ??
                        throw new InvalidOperationException("Accepted rebalance decisions must include a candidate.");
        var current = core.Topology;
        var beforePartition = current.GetPartition(candidate.PartitionId);
        var beforeSnapshot = core.CapturePartitionState(beforePartition);
        var projectedAfterSnapshot = core.CapturePartitionState(
            new RadarProcessingPartitionAssignment(
                beforePartition.PartitionId,
                candidate.TargetShardId,
                beforePartition.SourceIdStart,
                beforePartition.SourceIdEndExclusive));
        var prePublicationHandoff = RadarProcessingStateHandoffValidator.Validate(
            beforeSnapshot,
            projectedAfterSnapshot);

        if (!prePublicationHandoff.IsValid)
        {
            return (null, prePublicationHandoff);
        }

        var migrationResult = migrationCoordinator.Apply(decision);
        if (!migrationResult.Succeeded)
        {
            return (migrationResult, prePublicationHandoff);
        }

        var afterSnapshot = core.CapturePartitionState(core.Topology.GetPartition(candidate.PartitionId));
        var handoff = RadarProcessingStateHandoffValidator.Validate(
            beforeSnapshot,
            afterSnapshot);

        if (handoff.IsValid)
        {
            var policyRecord = policyState.RecordAcceptedMove(candidate.ToPolicyInput());
            if (!policyRecord.IsAllowed)
            {
                throw new InvalidOperationException("Published rebalance move failed policy recording.");
            }
        }

        return (migrationResult, handoff);
    }

    private long NextDecisionId() =>
        nextDecisionId++;
}

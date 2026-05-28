using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingRebalanceValidatorTests
{
    private static RadarProcessingRebalanceDecision CreateAcceptedDecision(
        RadarProcessingTopologyVersion topologyVersion) =>
        RadarProcessingRebalanceDecision.AcceptedMove(
            decisionId: 10,
            evaluationSequence: 1,
            topologyVersion,
            pressureWindowSampleCount: 1,
            new RadarProcessingRebalanceCandidate(
                RadarProcessingRebalanceMoveKind.DirectHotRelief,
                partitionId: 0,
                sourceShardId: 0,
                targetShardId: 1,
                new RadarProcessingProjectedPressure(
                    new RadarProcessingPressureScore(5.0),
                    RadarProcessingPressureScore.Zero,
                    new RadarProcessingPressureScore(4.0),
                    new RadarProcessingPressureScore(1.0)),
                expectedRelief: 1.0));

    private static RadarProcessingStateHandoffValidationResult CreateInvalidStateHandoff()
    {
        var before = new RadarProcessingPartitionStateSnapshot(
            partitionId: 0,
            shardId: 0,
            sourceIdStart: 0,
            sourceIdEndExclusive: 1,
            activeSourceCount: 1,
            processedEventCount: 1,
            processedPayloadValueCount: 1,
            rawValueChecksum: 1,
            new RadarProcessingPartitionStateChecksum(
                ProcessingChecksum: 1,
                LastMessageTimestampChecksum: 2,
                HandlerSnapshotChecksum: 0));
        var after = new RadarProcessingPartitionStateSnapshot(
            partitionId: 0,
            shardId: 1,
            sourceIdStart: 0,
            sourceIdEndExclusive: 1,
            activeSourceCount: 0,
            processedEventCount: 1,
            processedPayloadValueCount: 1,
            rawValueChecksum: 1,
            before.Checksum);

        return RadarProcessingStateHandoffValidator.Validate(before, after);
    }
}

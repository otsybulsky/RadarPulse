using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkload
{
    private readonly record struct InitialHotPartitionClassification(
        int PartitionId,
        int ShardId,
        RadarProcessingHotPartitionClassification Classification);
}

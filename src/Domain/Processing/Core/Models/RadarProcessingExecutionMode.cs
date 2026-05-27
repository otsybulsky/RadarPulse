namespace RadarPulse.Domain.Processing;

public enum RadarProcessingExecutionMode : byte
{
    Sequential = 1,
    PartitionedBarrier = 2,
    AsyncShardTransport = 3
}

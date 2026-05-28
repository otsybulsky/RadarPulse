using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private static void ValidateCommon(
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        int activeBatchCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activeBatchCapacity);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }
    }
}

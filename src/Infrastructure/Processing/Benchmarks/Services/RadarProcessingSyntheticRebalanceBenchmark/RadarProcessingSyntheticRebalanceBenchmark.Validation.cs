using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceBenchmark
{
    private static void EnsureKnownMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        if (mode is not RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.OrderedRebalanceSession)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static void EnsureKnownExecutionMode(RadarProcessingExecutionMode executionMode)
    {
        if (executionMode is not RadarProcessingExecutionMode.PartitionedBarrier and
            not RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentOutOfRangeException(nameof(executionMode));
        }
    }

    private static RadarProcessingBenchmarkAllocationSnapshot CaptureAllocationSnapshot(
        RadarProcessingExecutionMode executionMode) =>
        executionMode == RadarProcessingExecutionMode.AsyncShardTransport
            ? RadarProcessingBenchmarkAllocationSnapshot.Capture()
            : RadarProcessingBenchmarkAllocationSnapshot.CaptureCurrentThread();

    private static ulong AppendByte(ulong checksum, byte value) =>
        unchecked((checksum ^ value) * ChecksumPrime);

    private static ulong AppendInt32(ulong checksum, int value) =>
        AppendUInt32(checksum, unchecked((uint)value));

    private static ulong AppendUInt32(ulong checksum, uint value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        return AppendByte(checksum, (byte)(value >> 24));
    }

    private static ulong AppendInt64(ulong checksum, long value) =>
        AppendUInt64(checksum, unchecked((ulong)value));

    private static ulong AppendUInt64(ulong checksum, ulong value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        checksum = AppendByte(checksum, (byte)(value >> 24));
        checksum = AppendByte(checksum, (byte)(value >> 32));
        checksum = AppendByte(checksum, (byte)(value >> 40));
        checksum = AppendByte(checksum, (byte)(value >> 48));
        return AppendByte(checksum, (byte)(value >> 56));
    }

}

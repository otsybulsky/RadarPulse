using System.Buffers;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactory
{
    /// <summary>
    /// Seeds retained payload pools with batch-sized arrays and returns allocation evidence.
    /// </summary>
    public RadarProcessingRetainedPayloadPrewarmResult Prewarm(
        int eventCount,
        int payloadBytes,
        int retainedBatchCount = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedBatchCount);

        var before = RadarProcessingBenchmarkAllocationSnapshot.CaptureCurrentThread();
        var started = TimeProvider.System.GetTimestamp();
        if (retainedBatchCount > 0)
        {
            PrewarmEventPool(eventCount, retainedBatchCount);
            PrewarmPayloadPool(payloadBytes, retainedBatchCount);
        }

        var elapsed = TimeProvider.System.GetElapsedTime(started);
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.CaptureCurrentThread().DeltaSince(before);
        return new RadarProcessingRetainedPayloadPrewarmResult(
            eventCount,
            payloadBytes,
            retainedBatchCount,
            elapsed,
            allocatedBytes,
            eventPool is RadarProcessingRetainedEventArrayPool retainedEventPool
                ? retainedEventPool.RetainedBytes
                : 0,
            payloadPool is RadarProcessingRetainedPayloadByteArrayPool retainedPayloadPool
                ? retainedPayloadPool.RetainedBytes
                : 0);
    }

    private void PrewarmEventPool(
        int eventCount,
        int retainedBatchCount)
    {
        if (eventCount == 0)
        {
            return;
        }

        if (eventPool is RadarProcessingRetainedEventArrayPool retainedEventPool)
        {
            retainedEventPool.Prewarm(eventCount, retainedBatchCount);
            return;
        }

        PrewarmFallbackPool(eventPool, eventCount, retainedBatchCount);
    }

    private void PrewarmPayloadPool(
        int payloadBytes,
        int retainedBatchCount)
    {
        if (payloadBytes == 0)
        {
            return;
        }

        if (payloadPool is RadarProcessingRetainedPayloadByteArrayPool retainedPayloadPool)
        {
            retainedPayloadPool.Prewarm(payloadBytes, retainedBatchCount);
            return;
        }

        PrewarmFallbackPool(payloadPool, payloadBytes, retainedBatchCount);
    }

    private static void PrewarmFallbackPool<T>(
        ArrayPool<T> pool,
        int minimumLength,
        int retainedBatchCount)
    {
        var arrays = new T[retainedBatchCount][];
        try
        {
            for (var i = 0; i < arrays.Length; i++)
            {
                arrays[i] = pool.Rent(minimumLength);
            }
        }
        finally
        {
            foreach (var array in arrays)
            {
                if (array is not null)
                {
                    pool.Return(array);
                }
            }
        }
    }
}

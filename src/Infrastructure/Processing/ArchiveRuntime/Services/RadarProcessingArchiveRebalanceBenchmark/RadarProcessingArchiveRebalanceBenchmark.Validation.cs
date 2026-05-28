using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingArchiveRebalanceBenchmark
{
    private static void EnsureKnownMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        if (mode is not RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession)
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

    private static void EnsureKnownProviderMode(RadarProcessingArchiveProviderMode providerMode)
    {
        if (providerMode is not RadarProcessingArchiveProviderMode.BlockingBorrowed and
            not RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new ArgumentOutOfRangeException(nameof(providerMode));
        }
    }

    private static void EnsureKnownProviderOverlapMode(RadarProcessingQueuedProviderOverlapMode providerOverlapMode)
    {
        if (providerOverlapMode is not RadarProcessingQueuedProviderOverlapMode.None and
            not RadarProcessingQueuedProviderOverlapMode.ProducerConsumer)
        {
            throw new ArgumentOutOfRangeException(nameof(providerOverlapMode));
        }
    }

    private static void ValidateQueueTimeout(TimeSpan? queueTimeout)
    {
        if (queueTimeout.HasValue &&
            queueTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueTimeout),
                queueTimeout,
                "Queue timeout must be positive when specified.");
        }
    }

    private static void ValidateQueueRetainedPayloadBytes(long? queueRetainedPayloadBytes)
    {
        if (queueRetainedPayloadBytes.HasValue &&
            queueRetainedPayloadBytes.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queueRetainedPayloadBytes),
                queueRetainedPayloadBytes,
                "Queue retained payload byte capacity must be positive when specified.");
        }
    }

    private static void ValidateOverlapConsumerDelay(TimeSpan overlapConsumerDelay)
    {
        if (overlapConsumerDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapConsumerDelay),
                overlapConsumerDelay,
                "Overlap consumer delay cannot be negative.");
        }
    }

    private static void ValidateQueuedProviderControls(
        RadarProcessingArchiveProviderMode providerMode,
        RadarProcessingQueuedProviderOverlapMode providerOverlapMode,
        RadarProcessingRetainedPayloadStrategy retentionStrategy,
        long? queueRetainedPayloadBytes,
        TimeSpan overlapConsumerDelay)
    {
        if (providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.None &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("Provider overlap mode requires queued-owned archive provider mode.");
        }

        if (retentionStrategy != RadarProcessingRetainedPayloadStrategy.SnapshotCopy &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("Retained payload strategies require queued-owned archive provider mode.");
        }

        if (queueRetainedPayloadBytes.HasValue &&
            providerMode != RadarProcessingArchiveProviderMode.QueuedOwned)
        {
            throw new InvalidOperationException("Queue retained payload byte capacity requires queued-owned archive provider mode.");
        }

        if (overlapConsumerDelay > TimeSpan.Zero &&
            (providerMode != RadarProcessingArchiveProviderMode.QueuedOwned ||
             providerOverlapMode != RadarProcessingQueuedProviderOverlapMode.ProducerConsumer))
        {
            throw new InvalidOperationException(
                "Overlap consumer delay requires queued-owned producer-consumer archive provider overlap.");
        }

        if (providerMode == RadarProcessingArchiveProviderMode.QueuedOwned &&
            retentionStrategy == RadarProcessingRetainedPayloadStrategy.BuilderTransfer)
        {
            throw new NotSupportedException("Builder-transfer retained payload strategy is not implemented for archive benchmarks.");
        }
    }

    private static void ValidateWorkerTelemetry(
        RadarProcessingWorkerTelemetrySummary? workerTelemetry,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder,
        RadarProcessingRebalanceHardeningOptions hardeningOptions)
    {
        if (workerTelemetry is null)
        {
            return;
        }

        var retentionValidation = RadarProcessingAsyncValidator.ValidateWorkerTelemetryRetention(
            workerTelemetry,
            workerTelemetryRecorder!.Options,
            hardeningOptions.ValidationProfile);
        if (!retentionValidation.IsValid)
        {
            throw new InvalidDataException(retentionValidation.Message);
        }
    }

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

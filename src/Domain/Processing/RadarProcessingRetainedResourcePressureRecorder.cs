namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingRetainedResourcePressureRecorder
{
    private readonly object sync = new();
    private long pendingBatchCount;
    private long pendingPayloadBytes;
    private long activeBatchCount;
    private long activePayloadBytes;
    private long pendingBatchCountHighWatermark;
    private long pendingPayloadBytesHighWatermark;
    private long activeBatchCountHighWatermark;
    private long activePayloadBytesHighWatermark;
    private long combinedBatchCountHighWatermark;
    private long combinedPayloadBytesHighWatermark;

    public RadarProcessingRetainedResourcePressureSnapshot CreateSnapshot()
    {
        lock (sync)
        {
            return new RadarProcessingRetainedResourcePressureSnapshot(
                pendingBatchCount,
                pendingPayloadBytes,
                activeBatchCount,
                activePayloadBytes);
        }
    }

    public RadarProcessingRetainedResourcePressureSummary CreateSummary()
    {
        lock (sync)
        {
            return RadarProcessingRetainedResourcePressureSummary.FromState(
                new RadarProcessingRetainedResourcePressureSnapshot(
                    pendingBatchCount,
                    pendingPayloadBytes,
                    activeBatchCount,
                    activePayloadBytes),
                pendingBatchCountHighWatermark,
                pendingPayloadBytesHighWatermark,
                activeBatchCountHighWatermark,
                activePayloadBytesHighWatermark,
                combinedBatchCountHighWatermark,
                combinedPayloadBytesHighWatermark);
        }
    }

    public RadarProcessingRetainedResourcePressureSummary AddPending(
        long payloadBytes,
        long batchCount = 1)
    {
        EnsureValidOperationValues(payloadBytes, batchCount);

        lock (sync)
        {
            pendingBatchCount = checked(pendingBatchCount + batchCount);
            pendingPayloadBytes = checked(pendingPayloadBytes + payloadBytes);
            UpdateHighWatermarksUnsafe();
            return CreateSummaryUnsafe();
        }
    }

    public RadarProcessingRetainedResourcePressureSummary RemovePending(
        long payloadBytes,
        long batchCount = 1)
    {
        EnsureValidOperationValues(payloadBytes, batchCount);

        lock (sync)
        {
            EnsureAvailableUnsafe(
                pendingBatchCount,
                pendingPayloadBytes,
                batchCount,
                payloadBytes,
                "Pending retained-resource pressure cannot go below zero.");

            pendingBatchCount -= batchCount;
            pendingPayloadBytes -= payloadBytes;
            UpdateHighWatermarksUnsafe();
            return CreateSummaryUnsafe();
        }
    }

    public RadarProcessingRetainedResourcePressureSummary MovePendingToActive(
        long payloadBytes,
        long batchCount = 1)
    {
        EnsureValidOperationValues(payloadBytes, batchCount);

        lock (sync)
        {
            EnsureAvailableUnsafe(
                pendingBatchCount,
                pendingPayloadBytes,
                batchCount,
                payloadBytes,
                "Pending retained-resource pressure cannot transfer more than it holds.");

            pendingBatchCount -= batchCount;
            pendingPayloadBytes -= payloadBytes;
            activeBatchCount = checked(activeBatchCount + batchCount);
            activePayloadBytes = checked(activePayloadBytes + payloadBytes);
            UpdateHighWatermarksUnsafe();
            return CreateSummaryUnsafe();
        }
    }

    public RadarProcessingRetainedResourcePressureSummary RemoveActive(
        long payloadBytes,
        long batchCount = 1)
    {
        EnsureValidOperationValues(payloadBytes, batchCount);

        lock (sync)
        {
            EnsureAvailableUnsafe(
                activeBatchCount,
                activePayloadBytes,
                batchCount,
                payloadBytes,
                "Active retained-resource pressure cannot go below zero.");

            activeBatchCount -= batchCount;
            activePayloadBytes -= payloadBytes;
            UpdateHighWatermarksUnsafe();
            return CreateSummaryUnsafe();
        }
    }

    private RadarProcessingRetainedResourcePressureSummary CreateSummaryUnsafe() =>
        RadarProcessingRetainedResourcePressureSummary.FromState(
            new RadarProcessingRetainedResourcePressureSnapshot(
                pendingBatchCount,
                pendingPayloadBytes,
                activeBatchCount,
                activePayloadBytes),
            pendingBatchCountHighWatermark,
            pendingPayloadBytesHighWatermark,
            activeBatchCountHighWatermark,
            activePayloadBytesHighWatermark,
            combinedBatchCountHighWatermark,
            combinedPayloadBytesHighWatermark);

    private void UpdateHighWatermarksUnsafe()
    {
        pendingBatchCountHighWatermark = Math.Max(pendingBatchCountHighWatermark, pendingBatchCount);
        pendingPayloadBytesHighWatermark = Math.Max(pendingPayloadBytesHighWatermark, pendingPayloadBytes);
        activeBatchCountHighWatermark = Math.Max(activeBatchCountHighWatermark, activeBatchCount);
        activePayloadBytesHighWatermark = Math.Max(activePayloadBytesHighWatermark, activePayloadBytes);
        combinedBatchCountHighWatermark = Math.Max(
            combinedBatchCountHighWatermark,
            checked(pendingBatchCount + activeBatchCount));
        combinedPayloadBytesHighWatermark = Math.Max(
            combinedPayloadBytesHighWatermark,
            checked(pendingPayloadBytes + activePayloadBytes));
    }

    private static void EnsureValidOperationValues(
        long payloadBytes,
        long batchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchCount);
    }

    private static void EnsureAvailableUnsafe(
        long availableBatchCount,
        long availablePayloadBytes,
        long batchCount,
        long payloadBytes,
        string message)
    {
        if (availableBatchCount < batchCount ||
            availablePayloadBytes < payloadBytes)
        {
            throw new InvalidOperationException(message);
        }
    }
}

using RadarPulse.Domain.Processing;

namespace RadarPulse.Tests.Processing;

public sealed class RadarProcessingRetainedResourcePressureContractTests
{
    [Fact]
    public void SnapshotDefaultsToEmptyAndRejectsNegativeValues()
    {
        var snapshot = RadarProcessingRetainedResourcePressureSnapshot.Empty;

        Assert.Equal(0, snapshot.PendingBatchCount);
        Assert.Equal(0, snapshot.PendingPayloadBytes);
        Assert.Equal(0, snapshot.ActiveBatchCount);
        Assert.Equal(0, snapshot.ActivePayloadBytes);
        Assert.Equal(0, snapshot.CombinedBatchCount);
        Assert.Equal(0, snapshot.CombinedPayloadBytes);

        var populated = new RadarProcessingRetainedResourcePressureSnapshot(
            pendingBatchCount: 2,
            pendingPayloadBytes: 128,
            activeBatchCount: 1,
            activePayloadBytes: 64);

        Assert.Equal(3, populated.CombinedBatchCount);
        Assert.Equal(192, populated.CombinedPayloadBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSnapshot(pendingBatchCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSnapshot(pendingPayloadBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSnapshot(activeBatchCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSnapshot(activePayloadBytes: -1));
    }

    [Fact]
    public void SummaryCarriesCurrentAndHighWaterValues()
    {
        var summary = new RadarProcessingRetainedResourcePressureSummary(
            currentPendingRetainedBatchCount: 1,
            currentPendingRetainedPayloadBytes: 64,
            pendingRetainedBatchCountHighWatermark: 3,
            pendingRetainedPayloadBytesHighWatermark: 192,
            currentActiveRetainedBatchCount: 2,
            currentActiveRetainedPayloadBytes: 256,
            activeRetainedBatchCountHighWatermark: 2,
            activeRetainedPayloadBytesHighWatermark: 256,
            combinedRetainedBatchCountHighWatermark: 4,
            combinedRetainedPayloadBytesHighWatermark: 384);

        Assert.Equal(1, summary.CurrentPendingRetainedBatchCount);
        Assert.Equal(64, summary.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(3, summary.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(192, summary.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(2, summary.CurrentActiveRetainedBatchCount);
        Assert.Equal(256, summary.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(2, summary.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(256, summary.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(3, summary.CurrentCombinedRetainedBatchCount);
        Assert.Equal(320, summary.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(4, summary.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(384, summary.CombinedRetainedPayloadBytesHighWatermark);
        Assert.True(summary.HasRetainedPressure);

        var current = summary.CurrentSnapshot;
        Assert.Equal(1, current.PendingBatchCount);
        Assert.Equal(64, current.PendingPayloadBytes);
        Assert.Equal(2, current.ActiveBatchCount);
        Assert.Equal(256, current.ActivePayloadBytes);

        Assert.False(RadarProcessingRetainedResourcePressureSummary.Empty.HasRetainedPressure);
    }

    [Fact]
    public void SummaryRejectsInvalidShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(currentPendingRetainedBatchCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(currentPendingRetainedPayloadBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(currentActiveRetainedBatchCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(currentActiveRetainedPayloadBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(
                currentPendingRetainedBatchCount: 2,
                pendingRetainedBatchCountHighWatermark: 1,
                combinedRetainedBatchCountHighWatermark: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(
                currentActiveRetainedPayloadBytes: 128,
                activeRetainedPayloadBytesHighWatermark: 127,
                combinedRetainedPayloadBytesHighWatermark: 128));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(
                pendingRetainedBatchCountHighWatermark: 2,
                combinedRetainedBatchCountHighWatermark: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RadarProcessingRetainedResourcePressureSummary(
                activeRetainedPayloadBytesHighWatermark: 256,
                combinedRetainedPayloadBytesHighWatermark: 255));
    }

    [Fact]
    public void RecorderTracksPendingActiveAndCombinedHighWatermarks()
    {
        var recorder = new RadarProcessingRetainedResourcePressureRecorder();

        var first = recorder.AddPending(payloadBytes: 100);

        Assert.Equal(1, first.CurrentPendingRetainedBatchCount);
        Assert.Equal(100, first.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(1, first.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(100, first.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, first.CurrentCombinedRetainedBatchCount);
        Assert.Equal(100, first.CombinedRetainedPayloadBytesHighWatermark);

        recorder.AddPending(payloadBytes: 50);
        var active = recorder.MovePendingToActive(payloadBytes: 100);

        Assert.Equal(1, active.CurrentPendingRetainedBatchCount);
        Assert.Equal(50, active.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(1, active.CurrentActiveRetainedBatchCount);
        Assert.Equal(100, active.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(2, active.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(150, active.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1, active.ActiveRetainedBatchCountHighWatermark);
        Assert.Equal(100, active.ActiveRetainedPayloadBytesHighWatermark);
        Assert.Equal(2, active.CurrentCombinedRetainedBatchCount);
        Assert.Equal(150, active.CombinedRetainedPayloadBytesHighWatermark);

        var overlapped = recorder.AddPending(payloadBytes: 25);

        Assert.Equal(2, overlapped.CurrentPendingRetainedBatchCount);
        Assert.Equal(75, overlapped.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(1, overlapped.CurrentActiveRetainedBatchCount);
        Assert.Equal(100, overlapped.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(3, overlapped.CurrentCombinedRetainedBatchCount);
        Assert.Equal(175, overlapped.CurrentCombinedRetainedPayloadBytes);
        Assert.Equal(3, overlapped.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(175, overlapped.CombinedRetainedPayloadBytesHighWatermark);

        recorder.RemoveActive(payloadBytes: 100);
        recorder.RemovePending(payloadBytes: 50);
        var empty = recorder.RemovePending(payloadBytes: 25);

        Assert.Equal(0, empty.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, empty.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(0, empty.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, empty.CurrentActiveRetainedPayloadBytes);
        Assert.Equal(3, empty.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(175, empty.CombinedRetainedPayloadBytesHighWatermark);
    }

    [Fact]
    public void RecorderRejectsInvalidTransitions()
    {
        var recorder = new RadarProcessingRetainedResourcePressureRecorder();

        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.AddPending(payloadBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => recorder.AddPending(payloadBytes: 1, batchCount: 0));
        Assert.Throws<InvalidOperationException>(() => recorder.MovePendingToActive(payloadBytes: 1));
        Assert.Throws<InvalidOperationException>(() => recorder.RemoveActive(payloadBytes: 1));

        recorder.AddPending(payloadBytes: 10);

        Assert.Throws<InvalidOperationException>(() => recorder.MovePendingToActive(payloadBytes: 11));
        Assert.Throws<InvalidOperationException>(() => recorder.MovePendingToActive(payloadBytes: 10, batchCount: 2));

        recorder.MovePendingToActive(payloadBytes: 10);

        Assert.Throws<InvalidOperationException>(() => recorder.RemoveActive(payloadBytes: 11));
        Assert.Throws<InvalidOperationException>(() => recorder.RemoveActive(payloadBytes: 10, batchCount: 2));
    }

    [Fact]
    public void RecorderSerializesConcurrentUpdates()
    {
        var recorder = new RadarProcessingRetainedResourcePressureRecorder();

        Parallel.For(0, 1_000, _ => recorder.AddPending(payloadBytes: 2));

        var populated = recorder.CreateSummary();

        Assert.Equal(1_000, populated.CurrentPendingRetainedBatchCount);
        Assert.Equal(2_000, populated.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(1_000, populated.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(2_000, populated.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1_000, populated.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(2_000, populated.CombinedRetainedPayloadBytesHighWatermark);

        Parallel.For(0, 1_000, _ => recorder.RemovePending(payloadBytes: 2));

        var empty = recorder.CreateSummary();

        Assert.Equal(0, empty.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, empty.CurrentPendingRetainedPayloadBytes);
        Assert.Equal(1_000, empty.PendingRetainedBatchCountHighWatermark);
        Assert.Equal(2_000, empty.PendingRetainedPayloadBytesHighWatermark);
        Assert.Equal(1_000, empty.CombinedRetainedBatchCountHighWatermark);
        Assert.Equal(2_000, empty.CombinedRetainedPayloadBytesHighWatermark);
    }
}

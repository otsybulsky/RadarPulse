using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingRetainedPayloadFactory
{
    private static RadarProcessingRetainedPayloadRetentionResult RetainSnapshotCopy(
        RadarEventBatch batch,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return RadarProcessingRetainedPayloadRetentionResult.Canceled(
                RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
                "Retained payload retention was canceled before snapshot copy.");
        }

        var before = RadarProcessingBenchmarkAllocationSnapshot.CaptureCurrentThread();
        var started = TimeProvider.System.GetTimestamp();
        var owned = batch.ToOwnedSnapshot();
        var elapsed = TimeProvider.System.GetElapsedTime(started);
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.CaptureCurrentThread().DeltaSince(before);

        return RadarProcessingRetainedPayloadRetentionResult.Succeeded(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy,
            owned,
            RadarProcessingRetainedBatchResource.NotRequired(RadarProcessingRetainedPayloadStrategy.SnapshotCopy),
            elapsed,
            allocatedBytes);
    }
}

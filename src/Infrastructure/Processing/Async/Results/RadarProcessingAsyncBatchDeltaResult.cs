using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingAsyncBatchDeltaResult : IDisposable
{
    public RadarProcessingAsyncBatchDeltaResult(
        RadarProcessingBatchDelta delta,
        RadarProcessingWorkerTelemetrySummary workerTelemetry)
    {
        Delta = delta ?? throw new ArgumentNullException(nameof(delta));
        WorkerTelemetry = workerTelemetry ?? throw new ArgumentNullException(nameof(workerTelemetry));
    }

    public RadarProcessingBatchDelta Delta { get; private set; }

    public RadarProcessingWorkerTelemetrySummary WorkerTelemetry { get; }

    public void Dispose()
    {
        Delta.Dispose();
        Delta = null!;
    }
}

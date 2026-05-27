using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Disposable async batch delta plus worker telemetry evidence.
/// </summary>
/// <remarks>
/// Ownership of <see cref="Delta"/> is transferred to this result. Callers must
/// dispose the result when the delta is not committed, or after a commit path has
/// finished using it.
/// </remarks>
public sealed class RadarProcessingAsyncBatchDeltaResult : IDisposable
{
    /// <summary>
    /// Creates a delta result from a computed batch delta and telemetry summary.
    /// </summary>
    public RadarProcessingAsyncBatchDeltaResult(
        RadarProcessingBatchDelta delta,
        RadarProcessingWorkerTelemetrySummary workerTelemetry)
    {
        Delta = delta ?? throw new ArgumentNullException(nameof(delta));
        WorkerTelemetry = workerTelemetry ?? throw new ArgumentNullException(nameof(workerTelemetry));
    }

    /// <summary>
    /// Computed batch delta awaiting ordered commit.
    /// </summary>
    public RadarProcessingBatchDelta Delta { get; private set; }

    /// <summary>
    /// Worker telemetry recorded while computing the delta.
    /// </summary>
    public RadarProcessingWorkerTelemetrySummary WorkerTelemetry { get; }

    /// <summary>
    /// Releases the owned delta.
    /// </summary>
    public void Dispose()
    {
        Delta.Dispose();
        Delta = null!;
    }
}

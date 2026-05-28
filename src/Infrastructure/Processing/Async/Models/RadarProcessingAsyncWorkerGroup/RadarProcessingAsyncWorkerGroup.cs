using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;


/// <summary>
/// Lifecycle-managed group of async workers used by shard transport dispatch.
/// </summary>
/// <remarks>
/// The group owns worker mailboxes, validates dispatch lifecycle state, enforces
/// single in-flight dispatch by default, maps enqueue/timeout/cancellation
/// outcomes to result contracts, and exposes drain evidence for telemetry.
/// </remarks>
public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private readonly object lifecycleSync = new();
    private readonly RadarProcessingAsyncWorker[] workers;
    private readonly RadarProcessingWorkerGroupLifecycle lifecycle;
    private readonly CancellationTokenSource workerCancellation = new();
    private int inFlight;
    private int disposeRequested;
    private int cancellationDisposed;

    /// <summary>
    /// Creates a worker group with the supplied async execution options.
    /// </summary>
    public RadarProcessingAsyncWorkerGroup(
        RadarProcessingAsyncWorkerGroupOptions? options = null)
    {
        Options = options ?? RadarProcessingAsyncWorkerGroupOptions.Default;
        lifecycle = new RadarProcessingWorkerGroupLifecycle(Options.Execution);
        workers = CreateWorkers(Options);
    }

    /// <summary>
    /// Effective worker count, queue capacity, and timeout settings.
    /// </summary>
    public RadarProcessingAsyncWorkerGroupOptions Options { get; }

    /// <summary>
    /// Current lifecycle status for dispatch and shutdown decisions.
    /// </summary>
    public RadarProcessingWorkerGroupStatus Status
    {
        get
        {
            lock (lifecycleSync)
            {
                return lifecycle.Status;
            }
        }
    }

    /// <summary>
    /// Number of accepted work items waiting in worker mailboxes.
    /// </summary>
    public int PendingWorkItemCount => workers.Sum(static worker => worker.PendingCount);

    /// <summary>
    /// Number of work items currently executing across workers.
    /// </summary>
    public int RunningWorkItemCount => workers.Sum(static worker => worker.RunningCount);

    /// <summary>
    /// Number of accepted work items that are either pending or running.
    /// </summary>
    public int OutstandingWorkItemCount => PendingWorkItemCount + RunningWorkItemCount;

    /// <summary>
}

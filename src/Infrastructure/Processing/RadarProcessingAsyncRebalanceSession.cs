using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingAsyncRebalanceSession : IAsyncDisposable
{
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingAsyncCoreSession asyncCoreSession;
    private readonly bool ownsAsyncCoreSession;
    private int disposed;

    public RadarProcessingAsyncRebalanceSession(
        RadarProcessingCore core,
        RadarProcessingPressureOptions? pressureOptions = null,
        RadarProcessingPressureWindow? pressureWindow = null,
        RadarProcessingRebalancePolicyState? policyState = null,
        RadarProcessingHotPartitionClassifier? hotPartitionClassifier = null,
        RadarProcessingDirectHotReliefPlanner? directHotReliefPlanner = null,
        RadarProcessingColdEvacuationPlanner? coldEvacuationPlanner = null,
        RadarProcessingQuarantineLifecycleTracker? quarantineLifecycleTracker = null,
        RadarProcessingRebalanceTelemetryRecorder? telemetryRecorder = null,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null,
        RadarProcessingPressureSkewOptions? pressureSkewOptions = null,
        RadarProcessingWorkerTelemetryRecorder? workerTelemetryRecorder = null)
        : this(
            new RadarProcessingRebalanceSession(
                core,
                pressureOptions,
                pressureWindow,
                policyState,
                hotPartitionClassifier,
                directHotReliefPlanner,
                coldEvacuationPlanner,
                quarantineLifecycleTracker,
                telemetryRecorder,
                hardeningOptions,
                pressureSkewOptions),
            new RadarProcessingAsyncCoreSession(core, workerTelemetryRecorder),
            ownsAsyncCoreSession: true)
    {
    }

    public RadarProcessingAsyncRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession)
        : this(
            rebalanceSession,
            new RadarProcessingAsyncCoreSession(rebalanceSession.Core),
            ownsAsyncCoreSession: true)
    {
    }

    public RadarProcessingAsyncRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingAsyncCoreSession asyncCoreSession,
        bool ownsAsyncCoreSession = false)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        ArgumentNullException.ThrowIfNull(asyncCoreSession);

        if (!ReferenceEquals(rebalanceSession.Core, asyncCoreSession.Core))
        {
            throw new ArgumentException(
                "Async rebalance session requires the rebalance session and async core session to share one core.",
                nameof(asyncCoreSession));
        }

        if (rebalanceSession.Core.Options.ExecutionMode != RadarProcessingExecutionMode.AsyncShardTransport)
        {
            throw new ArgumentException(
                "Async rebalance session requires async shard transport core options.",
                nameof(rebalanceSession));
        }

        this.rebalanceSession = rebalanceSession;
        this.asyncCoreSession = asyncCoreSession;
        this.ownsAsyncCoreSession = ownsAsyncCoreSession;
    }

    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    public RadarProcessingAsyncCoreSession AsyncCoreSession => asyncCoreSession;

    public RadarProcessingCore Core => rebalanceSession.Core;

    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    public async ValueTask<RadarProcessingRebalanceSessionResult> ProcessAsync(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(batch);

        var processingResult = await asyncCoreSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
        return rebalanceSession.ProcessCompletedResult(processingResult, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (ownsAsyncCoreSession)
        {
            await asyncCoreSession.DisposeAsync().ConfigureAwait(false);
        }
    }
}

using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Async shard-transport adapter for rebalance processing.
/// </summary>
/// <remarks>
/// The adapter shares one processing core between async processing and the
/// rebalance session, then validates the rebalance result against the current
/// topology before returning it.
/// </remarks>
public sealed class RadarProcessingAsyncRebalanceSession : IAsyncDisposable
{
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingAsyncCoreSession asyncCoreSession;
    private readonly bool ownsAsyncCoreSession;
    private int disposed;

    /// <summary>
    /// Creates an async rebalance session with owned rebalance and async core dependencies.
    /// </summary>
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

    /// <summary>
    /// Creates an async rebalance session over an existing rebalance session.
    /// </summary>
    public RadarProcessingAsyncRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession)
        : this(
            rebalanceSession,
            new RadarProcessingAsyncCoreSession(rebalanceSession.Core),
            ownsAsyncCoreSession: true)
    {
    }

    /// <summary>
    /// Creates an async rebalance session over explicit rebalance and async core dependencies.
    /// </summary>
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

    /// <summary>
    /// Rebalance session that commits topology-aware processing results.
    /// </summary>
    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    /// <summary>
    /// Async core session used to process shard work.
    /// </summary>
    public RadarProcessingAsyncCoreSession AsyncCoreSession => asyncCoreSession;

    /// <summary>
    /// Processing core shared by the rebalance and async sessions.
    /// </summary>
    public RadarProcessingCore Core => rebalanceSession.Core;

    /// <summary>
    /// Current topology after any committed rebalance migration.
    /// </summary>
    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    /// <summary>
    /// Processes a batch asynchronously, then applies rebalance policy and validation.
    /// </summary>
    public async ValueTask<RadarProcessingRebalanceSessionResult> ProcessAsync(
        RadarEventBatch batch,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(batch);

        var processingResult = await asyncCoreSession.ProcessAsync(batch, cancellationToken).ConfigureAwait(false);
        var result = rebalanceSession.ProcessCompletedResult(processingResult, cancellationToken);
        var validation = RadarProcessingAsyncValidator.ValidateRebalanceResult(
            result,
            rebalanceSession.CurrentTopology,
            RadarProcessingValidationProfile.Essential);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return result;
    }

    /// <summary>
    /// Disposes the owned async core session when this adapter created it.
    /// </summary>
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

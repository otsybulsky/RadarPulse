using System.Diagnostics;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Drains an owned provider queue through a rebalance session.
/// </summary>
/// <remarks>
/// The session couples queued provider ownership with rebalance processing so
/// topology changes, migration validation, and queue terminal states are all
/// represented in ordered queued-session evidence.
/// </remarks>
public sealed partial class RadarProcessingQueuedRebalanceSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
    private readonly bool ownsQueue;
    private readonly bool ownsAsyncRebalanceSession;
    private readonly Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private TimeSpan totalDrainTime;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a rebalance session that owns its queue and any required async rebalance session.
    /// </summary>
    public RadarProcessingQueuedRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingProviderQueueOptions? queueOptions = null,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
        : this(
            rebalanceSession,
            new RadarProcessingOwnedBatchQueue(queueOptions),
            CreateAsyncRebalanceSessionIfNeeded(rebalanceSession),
            ownsQueue: true,
            ownsAsyncRebalanceSession: RequiresAsyncRebalanceSession(rebalanceSession),
            consumerResourceLeaseFactory: consumerResourceLeaseFactory)
    {
    }

    /// <summary>
    /// Creates a rebalance session over supplied queue and optional async dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires an async rebalance session wrapping the
    /// same rebalance session. Synchronous mode rejects async dependencies.
    /// </remarks>
    public RadarProcessingQueuedRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingOwnedBatchQueue queue,
        RadarProcessingAsyncRebalanceSession? asyncRebalanceSession = null,
        bool ownsQueue = false,
        bool ownsAsyncRebalanceSession = false,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        ArgumentNullException.ThrowIfNull(queue);

        if (rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncRebalanceSession);
            if (!ReferenceEquals(rebalanceSession, asyncRebalanceSession.RebalanceSession))
            {
                throw new ArgumentException(
                    "Queued async rebalance requires the async rebalance session to wrap the supplied rebalance session.",
                    nameof(asyncRebalanceSession));
            }
        }
        else if (asyncRebalanceSession is not null)
        {
            throw new ArgumentException(
                "Queued synchronous rebalance must not carry an async rebalance session.",
                nameof(asyncRebalanceSession));
        }

        this.rebalanceSession = rebalanceSession;
        this.queue = queue;
        this.asyncRebalanceSession = asyncRebalanceSession;
        this.ownsQueue = ownsQueue;
        this.ownsAsyncRebalanceSession = ownsAsyncRebalanceSession;
        this.consumerResourceLeaseFactory = consumerResourceLeaseFactory;
    }

    /// <summary>
    /// Rebalance session that owns topology policy, migration, and processing state.
    /// </summary>
    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    /// <summary>
    /// Processing core used by the rebalance session.
    /// </summary>
    public RadarProcessingCore Core => rebalanceSession.Core;

    /// <summary>
    /// Current topology after any committed rebalance migrations.
    /// </summary>
    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    /// <summary>
    /// Owned provider queue drained by this session.
    /// </summary>
    public RadarProcessingOwnedBatchQueue Queue => queue;

    /// <summary>
    /// Enqueues an owned radar batch and records the enqueue result in session evidence.
    /// </summary>
    public async ValueTask<RadarProcessingQueuedBatchEnqueueResult> EnqueueAsync(
        RadarEventBatch batch,
        TimeSpan ownedSnapshotTime = default,
        long ownedSnapshotAllocatedBytes = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var result = await queue.EnqueueAsync(
            batch,
            ownedSnapshotTime,
            ownedSnapshotAllocatedBytes,
            cancellationToken).ConfigureAwait(false);
        RecordEnqueueResult(result);
        return result;
    }

    /// <summary>
    /// Closes the provider queue to new batches while allowing accepted batches to drain.
    /// </summary>
    public void CompleteAdding() => queue.Close();

    /// <summary>
    /// Faults the session and queue so later accepted batches are skipped after fault.
    /// </summary>
    public void Fault(string message = "")
    {
        ArgumentNullException.ThrowIfNull(message);
        MarkFaulted(message);
    }
}

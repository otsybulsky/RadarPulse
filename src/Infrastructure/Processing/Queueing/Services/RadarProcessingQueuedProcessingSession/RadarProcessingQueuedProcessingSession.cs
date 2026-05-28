using System.Buffers;
using System.Diagnostics;
using RadarPulse.Application.Processing;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Drains an owned provider queue into a processing core.
/// </summary>
/// <remarks>
/// The session records every enqueue and processing result, maps queue terminal
/// states to queued session status, and supports ordered concurrent processing
/// paths that compute work out of order while committing results by provider
/// sequence.
/// </remarks>
public sealed partial class RadarProcessingQueuedProcessingSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly RadarProcessingAsyncCoreSession? asyncCoreSession;
    private readonly bool ownsQueue;
    private readonly bool ownsAsyncCoreSession;
    private readonly Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private TimeSpan totalDrainTime;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a processing session that owns its queue and any required async core session.
    /// </summary>
    public RadarProcessingQueuedProcessingSession(
        RadarProcessingCore core,
        RadarProcessingProviderQueueOptions? queueOptions = null,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
        : this(
            core,
            new RadarProcessingOwnedBatchQueue(queueOptions),
            CreateAsyncCoreSessionIfNeeded(core),
            ownsQueue: true,
            ownsAsyncCoreSession: core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport,
            consumerResourceLeaseFactory: consumerResourceLeaseFactory)
    {
    }

    /// <summary>
    /// Creates a processing session over supplied queue and optional async core dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires an async core session that wraps the same
    /// core. Synchronous processing rejects an async session to keep ownership
    /// and execution mode explicit.
    /// </remarks>
    public RadarProcessingQueuedProcessingSession(
        RadarProcessingCore core,
        RadarProcessingOwnedBatchQueue queue,
        RadarProcessingAsyncCoreSession? asyncCoreSession = null,
        bool ownsQueue = false,
        bool ownsAsyncCoreSession = false,
        Func<RadarProcessingQueuedBatchSequence, IDisposable?>? consumerResourceLeaseFactory = null)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(queue);

        if (core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncCoreSession);
            if (!ReferenceEquals(core, asyncCoreSession.Core))
            {
                throw new ArgumentException(
                    "Queued async processing requires the async core session to share the supplied core.",
                    nameof(asyncCoreSession));
            }
        }
        else if (asyncCoreSession is not null)
        {
            throw new ArgumentException(
                "Queued synchronous processing must not carry an async core session.",
                nameof(asyncCoreSession));
        }

        this.core = core;
        this.queue = queue;
        this.asyncCoreSession = asyncCoreSession;
        this.ownsQueue = ownsQueue;
        this.ownsAsyncCoreSession = ownsAsyncCoreSession;
        this.consumerResourceLeaseFactory = consumerResourceLeaseFactory;
    }

    /// <summary>
    /// Processing core that owns source state and handler configuration.
    /// </summary>
    public RadarProcessingCore Core => core;

    /// <summary>
    /// Owned provider queue drained by this session.
    /// </summary>
    public RadarProcessingOwnedBatchQueue Queue => queue;
}

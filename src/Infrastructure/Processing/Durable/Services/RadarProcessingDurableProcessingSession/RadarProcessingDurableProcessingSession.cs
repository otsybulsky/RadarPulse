using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Processes durable envelope claims through a processing core and ordered commit gate.
/// </summary>
/// <remarks>
/// Durable processing separates claim/compute from ordered commit so recovered
/// completed envelopes can be replayed in provider sequence before retained
/// resources are marked released.
/// </remarks>
public sealed partial class RadarProcessingDurableProcessingSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingCore core;
    private readonly RadarProcessingDurableEnvelopeQueue queue;
    private readonly RadarProcessingAsyncCoreSession? asyncCoreSession;
    private readonly bool ownsAsyncCoreSession;
    private readonly SortedDictionary<long, DurableProcessingCompletion> pendingCompletions = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private long nextCommitSequence;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a durable processing session over a queue and any required async core session.
    /// </summary>
    public RadarProcessingDurableProcessingSession(
        RadarProcessingCore core,
        RadarProcessingDurableEnvelopeQueue? queue = null)
        : this(
            core,
            queue ?? new RadarProcessingDurableEnvelopeQueue(),
            CreateAsyncCoreSessionIfNeeded(core),
            ownsAsyncCoreSession: core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
    {
    }

    /// <summary>
    /// Creates a durable processing session over explicit queue and async dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires the async core session to wrap the same
    /// core; synchronous processing rejects async dependencies.
    /// </remarks>
    public RadarProcessingDurableProcessingSession(
        RadarProcessingCore core,
        RadarProcessingDurableEnvelopeQueue queue,
        RadarProcessingAsyncCoreSession? asyncCoreSession = null,
        bool ownsAsyncCoreSession = false)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(queue);

        if (core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncCoreSession);
            if (!ReferenceEquals(core, asyncCoreSession.Core))
            {
                throw new ArgumentException(
                    "Durable async processing requires the async core session to share the supplied core.",
                    nameof(asyncCoreSession));
            }
        }
        else if (asyncCoreSession is not null)
        {
            throw new ArgumentException(
                "Durable synchronous processing must not carry an async core session.",
                nameof(asyncCoreSession));
        }

        this.core = core;
        this.queue = queue;
        this.asyncCoreSession = asyncCoreSession;
        this.ownsAsyncCoreSession = ownsAsyncCoreSession;
    }

    /// <summary>
    /// Processing core used to compute and commit durable batch work.
    /// </summary>
    public RadarProcessingCore Core => core;

    /// <summary>
    /// Durable envelope queue owned by the session caller.
    /// </summary>
    public RadarProcessingDurableEnvelopeQueue Queue => queue;
}

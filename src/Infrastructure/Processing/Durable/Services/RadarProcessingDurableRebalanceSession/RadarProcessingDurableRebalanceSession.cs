using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Processes durable envelope claims through a rebalance session and ordered commit gate.
/// </summary>
/// <remarks>
/// Durable rebalance keeps provider-sequence ordering across recoverable work
/// while allowing rebalance processing and topology changes to be committed only
/// when earlier completed envelopes have published.
/// </remarks>
public sealed partial class RadarProcessingDurableRebalanceSession : IDisposable, IAsyncDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingRebalanceSession rebalanceSession;
    private readonly RadarProcessingDurableEnvelopeQueue queue;
    private readonly RadarProcessingAsyncRebalanceSession? asyncRebalanceSession;
    private readonly bool ownsAsyncRebalanceSession;
    private readonly SortedDictionary<long, DurableRebalanceCompletion> pendingCompletions = [];
    private readonly List<RadarProcessingQueuedBatchProcessingResult> processingResults = [];
    private long nextCommitSequence;
    private bool faulted;
    private bool canceled;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a durable rebalance session over a queue and any required async rebalance session.
    /// </summary>
    public RadarProcessingDurableRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingDurableEnvelopeQueue? queue = null)
        : this(
            rebalanceSession,
            queue ?? new RadarProcessingDurableEnvelopeQueue(),
            CreateAsyncRebalanceSessionIfNeeded(rebalanceSession),
            ownsAsyncRebalanceSession: rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
    {
    }

    /// <summary>
    /// Creates a durable rebalance session over explicit queue and async dependencies.
    /// </summary>
    /// <remarks>
    /// Async shard transport requires the async rebalance session to wrap the
    /// same rebalance session; synchronous processing rejects async dependencies.
    /// </remarks>
    public RadarProcessingDurableRebalanceSession(
        RadarProcessingRebalanceSession rebalanceSession,
        RadarProcessingDurableEnvelopeQueue queue,
        RadarProcessingAsyncRebalanceSession? asyncRebalanceSession = null,
        bool ownsAsyncRebalanceSession = false)
    {
        ArgumentNullException.ThrowIfNull(rebalanceSession);
        ArgumentNullException.ThrowIfNull(queue);

        if (rebalanceSession.Core.Options.ExecutionMode == RadarProcessingExecutionMode.AsyncShardTransport)
        {
            ArgumentNullException.ThrowIfNull(asyncRebalanceSession);
            if (!ReferenceEquals(rebalanceSession, asyncRebalanceSession.RebalanceSession))
            {
                throw new ArgumentException(
                    "Durable async rebalance requires the async rebalance session to wrap the supplied rebalance session.",
                    nameof(asyncRebalanceSession));
            }
        }
        else if (asyncRebalanceSession is not null)
        {
            throw new ArgumentException(
                "Durable synchronous rebalance must not carry an async rebalance session.",
                nameof(asyncRebalanceSession));
        }

        this.rebalanceSession = rebalanceSession;
        this.queue = queue;
        this.asyncRebalanceSession = asyncRebalanceSession;
        this.ownsAsyncRebalanceSession = ownsAsyncRebalanceSession;
    }

    /// <summary>
    /// Rebalance session used to process and commit durable work.
    /// </summary>
    public RadarProcessingRebalanceSession RebalanceSession => rebalanceSession;

    /// <summary>
    /// Processing core owned by the rebalance session.
    /// </summary>
    public RadarProcessingCore Core => rebalanceSession.Core;

    /// <summary>
    /// Current topology after committed durable rebalance work.
    /// </summary>
    public RadarProcessingTopology CurrentTopology => rebalanceSession.CurrentTopology;

    /// <summary>
    /// Durable envelope queue owned by the session caller.
    /// </summary>
    public RadarProcessingDurableEnvelopeQueue Queue => queue;
}

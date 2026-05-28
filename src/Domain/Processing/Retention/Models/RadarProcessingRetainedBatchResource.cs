namespace RadarPulse.Domain.Processing;

/// <summary>
/// Tracks ownership and release for a retained batch payload resource.
/// </summary>
/// <remarks>
/// The resource moves provider to queue to consumer and then into a terminal
/// released or release-failed state. State changes are synchronized so queue and
/// consumer cleanup paths cannot publish conflicting release results.
/// </remarks>
public sealed class RadarProcessingRetainedBatchResource
{
    private static readonly Func<RadarProcessingRetainedPayloadReleaseResult> SnapshotCopyReleaseNotRequired =
        static () => RadarProcessingRetainedPayloadReleaseResult.NotRequired(
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy);

    private static readonly Func<RadarProcessingRetainedPayloadReleaseResult> PooledCopyReleaseNotRequired =
        static () => RadarProcessingRetainedPayloadReleaseResult.NotRequired(
            RadarProcessingRetainedPayloadStrategy.PooledCopy);

    private static readonly Func<RadarProcessingRetainedPayloadReleaseResult> BuilderTransferReleaseNotRequired =
        static () => RadarProcessingRetainedPayloadReleaseResult.NotRequired(
            RadarProcessingRetainedPayloadStrategy.BuilderTransfer);

    private readonly object sync = new();
    private readonly Func<RadarProcessingRetainedPayloadReleaseResult>? release;
    private RadarProcessingRetainedBatchResourceState state;
    private RadarProcessingRetainedPayloadReleaseResult? lastReleaseResult;

    /// <summary>
    /// Creates a retained resource with an optional release callback.
    /// </summary>
    public RadarProcessingRetainedBatchResource(
        RadarProcessingRetainedPayloadStrategy strategy,
        long payloadBytes,
        Func<RadarProcessingRetainedPayloadReleaseResult>? release = null)
    {
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);

        Strategy = strategy;
        PayloadBytes = payloadBytes;
        this.release = release ?? CreateNotRequiredRelease(strategy);
        state = RadarProcessingRetainedBatchResourceState.ProviderOwned;
    }

    /// <summary>
    /// Retention strategy that produced the resource.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    /// <summary>
    /// Number of retained payload bytes represented by the resource.
    /// </summary>
    public long PayloadBytes { get; }

    /// <summary>
    /// Current ownership or terminal release state.
    /// </summary>
    public RadarProcessingRetainedBatchResourceState State
    {
        get
        {
            lock (sync)
            {
                return state;
            }
        }
    }

    /// <summary>
    /// Last release result recorded for the resource.
    /// </summary>
    public RadarProcessingRetainedPayloadReleaseResult? LastReleaseResult
    {
        get
        {
            lock (sync)
            {
                return lastReleaseResult;
            }
        }
    }

    /// <summary>
    /// Indicates whether the resource can no longer change ownership.
    /// </summary>
    public bool IsTerminal
    {
        get
        {
            lock (sync)
            {
                return IsTerminalState(state);
            }
        }
    }

    /// <summary>
    /// Transfers resource ownership from provider to queue.
    /// </summary>
    public void TransferToQueue()
    {
        lock (sync)
        {
            if (state != RadarProcessingRetainedBatchResourceState.ProviderOwned)
            {
                throw new InvalidOperationException("Only provider-owned retained resources can transfer to the queue.");
            }

            state = RadarProcessingRetainedBatchResourceState.QueueOwned;
        }
    }

    /// <summary>
    /// Transfers resource ownership from queue to consumer.
    /// </summary>
    public void TransferToConsumer()
    {
        lock (sync)
        {
            if (state != RadarProcessingRetainedBatchResourceState.QueueOwned)
            {
                throw new InvalidOperationException("Only queue-owned retained resources can transfer to the consumer.");
            }

            state = RadarProcessingRetainedBatchResourceState.ConsumerOwned;
        }
    }

    /// <summary>
    /// Releases the retained resource or returns the existing terminal result.
    /// </summary>
    public RadarProcessingRetainedPayloadReleaseResult Release()
    {
        lock (sync)
        {
            if (state == RadarProcessingRetainedBatchResourceState.Released)
            {
                return RadarProcessingRetainedPayloadReleaseResult.AlreadyReleased(Strategy);
            }

            if (state == RadarProcessingRetainedBatchResourceState.ReleaseFailed)
            {
                return RadarProcessingRetainedPayloadReleaseResult.Failed(
                    Strategy,
                    lastReleaseResult?.Message ?? "Retained resource release already failed.");
            }

            try
            {
                var result = ReleaseCore() ??
                             throw new InvalidOperationException("Retained resource release callback returned null.");
                if (result.Strategy != Strategy)
                {
                    result = RadarProcessingRetainedPayloadReleaseResult.Failed(
                        Strategy,
                        "Retained resource release result strategy did not match the resource strategy.");
                }

                lastReleaseResult = result;
                state = result.IsFailure
                    ? RadarProcessingRetainedBatchResourceState.ReleaseFailed
                    : RadarProcessingRetainedBatchResourceState.Released;
                return result;
            }
            catch (Exception exception)
            {
                var result = RadarProcessingRetainedPayloadReleaseResult.Failed(Strategy, exception.Message);
                lastReleaseResult = result;
                state = RadarProcessingRetainedBatchResourceState.ReleaseFailed;
                return result;
            }
        }
    }

    /// <summary>
    /// Creates a retained resource for strategies that do not require explicit release.
    /// </summary>
    public static RadarProcessingRetainedBatchResource NotRequired(
        RadarProcessingRetainedPayloadStrategy strategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy) =>
        new(strategy, payloadBytes: 0);

    private RadarProcessingRetainedPayloadReleaseResult ReleaseCore() =>
        release!();

    private static Func<RadarProcessingRetainedPayloadReleaseResult> CreateNotRequiredRelease(
        RadarProcessingRetainedPayloadStrategy strategy) =>
        strategy switch
        {
            RadarProcessingRetainedPayloadStrategy.SnapshotCopy => SnapshotCopyReleaseNotRequired,
            RadarProcessingRetainedPayloadStrategy.PooledCopy => PooledCopyReleaseNotRequired,
            RadarProcessingRetainedPayloadStrategy.BuilderTransfer => BuilderTransferReleaseNotRequired,
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };

    private static bool IsTerminalState(
        RadarProcessingRetainedBatchResourceState state) =>
        state is RadarProcessingRetainedBatchResourceState.Released or
            RadarProcessingRetainedBatchResourceState.ReleaseFailed;
}

namespace RadarPulse.Domain.Processing;

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
    private readonly IRadarProcessingRetainedPayloadReleaseOwner? releaseOwner;
    private RadarProcessingRetainedBatchResourceState state;
    private RadarProcessingRetainedPayloadReleaseResult? lastReleaseResult;

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

    internal RadarProcessingRetainedBatchResource(
        RadarProcessingRetainedPayloadStrategy strategy,
        long payloadBytes,
        IRadarProcessingRetainedPayloadReleaseOwner releaseOwner)
    {
        RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(strategy);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);

        Strategy = strategy;
        PayloadBytes = payloadBytes;
        this.releaseOwner = releaseOwner ?? throw new ArgumentNullException(nameof(releaseOwner));
        state = RadarProcessingRetainedBatchResourceState.ProviderOwned;
    }

    public RadarProcessingRetainedPayloadStrategy Strategy { get; }

    public long PayloadBytes { get; }

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

    public static RadarProcessingRetainedBatchResource NotRequired(
        RadarProcessingRetainedPayloadStrategy strategy = RadarProcessingRetainedPayloadStrategy.SnapshotCopy) =>
        new(strategy, payloadBytes: 0);

    private RadarProcessingRetainedPayloadReleaseResult ReleaseCore() =>
        releaseOwner?.Release() ?? release!();

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

using System.Diagnostics;
using System.Threading.Channels;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Bounded provider queue that accepts owned radar batches for processing sessions.
/// </summary>
/// <remarks>
/// The queue assigns monotonically increasing provider sequences, enforces the
/// retained payload byte budget, records enqueue/dequeue telemetry, and exposes
/// close, fault, cancellation, and disposal as explicit dequeue outcomes.
/// </remarks>
public sealed partial class RadarProcessingOwnedBatchQueue : IDisposable
{
    private readonly object sync = new();
    private readonly Channel<RadarProcessingQueuedBatch> channel;
    private readonly RadarProcessingProviderQueueTelemetryRecorder telemetryRecorder;
    private readonly RadarProcessingOwnedBatchQueueRetainedByteBudget retainedByteBudget;
    private readonly RadarProcessingOwnedBatchQueueCounters counters = new();
    private TaskCompletionSource<object?> retainedByteBudgetChanged = CreateRetainedByteBudgetChangedSource();
    private long nextSequence;
    private int pendingCount;
    private long pendingPayloadBytes;
    private bool closed;
    private bool faulted;
    private bool disposed;
    private string faultMessage = string.Empty;

    /// <summary>
    /// Creates a bounded owned-batch queue with the selected provider queue options.
    /// </summary>
    public RadarProcessingOwnedBatchQueue(
        RadarProcessingProviderQueueOptions? options = null)
    {
        Options = options ?? RadarProcessingProviderQueueOptions.Default;
        telemetryRecorder = new RadarProcessingProviderQueueTelemetryRecorder(Options);
        retainedByteBudget = new RadarProcessingOwnedBatchQueueRetainedByteBudget(Options.MaxRetainedPayloadBytes);
        channel = Channel.CreateBounded<RadarProcessingQueuedBatch>(
            new BoundedChannelOptions(Options.Capacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    /// <summary>
    /// Effective capacity, full-mode, timeout, shutdown, and retained-byte settings.
    /// </summary>
    public RadarProcessingProviderQueueOptions Options { get; }

    /// <summary>
    /// Number of accepted queued batches not yet dequeued or canceled.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (sync)
            {
                return pendingCount;
            }
        }
    }

    /// <summary>
    /// Total payload bytes currently retained by accepted queued batches.
    /// </summary>
    public long PendingPayloadBytes
    {
        get
        {
            lock (sync)
            {
                return pendingPayloadBytes;
            }
        }
    }

    /// <summary>
    /// Alias for pending retained payload bytes used by pressure telemetry contracts.
    /// </summary>
    public long PendingRetainedPayloadBytes => PendingPayloadBytes;

    /// <summary>
    /// Indicates whether the queue is closed to new accepted batches.
    /// </summary>
    public bool IsClosed
    {
        get
        {
            lock (sync)
            {
                return closed;
            }
        }
    }

    /// <summary>
    /// Indicates whether the queue has been faulted and will reject further work.
    /// </summary>
    public bool IsFaulted
    {
        get
        {
            lock (sync)
            {
                return faulted;
            }
        }
    }

    /// <summary>
    /// Indicates whether the queue has been disposed and drained.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (sync)
            {
                return disposed;
            }
        }
    }
}

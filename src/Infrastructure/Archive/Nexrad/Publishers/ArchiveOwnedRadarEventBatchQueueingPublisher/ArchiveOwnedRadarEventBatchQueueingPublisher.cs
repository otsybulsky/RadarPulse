using System.Diagnostics;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Archive batch publisher that retains batch memory and enqueues owned processing batches.
/// </summary>
/// <remarks>
/// The publisher converts possibly leased archive batches into retained processing payload resources, records enqueue
/// and retention telemetry, and exposes resource leases so queue consumers can release retained memory deterministically.
/// </remarks>
public sealed partial class ArchiveOwnedRadarEventBatchQueueingPublisher : IArchiveRadarEventBatchPublisher, IDisposable
{
    private readonly object sync = new();
    private readonly RadarProcessingOwnedBatchQueue queue;
    private readonly bool ownsQueue;
    private readonly RadarProcessingRetainedPayloadFactory retainedPayloadFactory;
    private readonly RadarProcessingRetainedPayloadOptions retainedPayloadOptions;
    private readonly List<RadarProcessingQueuedBatchEnqueueResult> enqueueResults = [];
    private readonly Dictionary<long, RetainedResourceEntry> retainedResources = [];
    private readonly RadarProcessingRetainedResourcePressureRecorder retainedResourcePressureRecorder = new();
    private long retentionAttemptCount;
    private long retainedBatchCount;
    private long retentionUnsupportedStrategyCount;
    private long retentionFailedCopyCount;
    private long retentionCanceledCount;
    private long retentionInvalidInputCount;
    private long retainedEventCount;
    private long retainedPayloadBytes;
    private long retainedPayloadValueCount;
    private long retainedAllocatedBytes;
    private long retainedPoolRentCount;
    private long retainedPoolReturnCount;
    private long retainedPoolMissCount;
    private long retainedEventPoolRentCount;
    private long retainedEventPoolReturnCount;
    private long retainedEventPoolMissCount;
    private long retainedPayloadPoolRentCount;
    private long retainedPayloadPoolReturnCount;
    private long retainedPayloadPoolMissCount;
    private TimeSpan totalRetentionTime;
    private long releaseAttemptCount;
    private long releasedBatchCount;
    private long alreadyReleasedBatchCount;
    private long releaseFailedCount;
    private long releaseNotRequiredCount;
    private TimeSpan totalReleaseTime;
    private bool disposed;

    /// <summary>
    /// Creates a queueing publisher with an owned processing queue.
    /// </summary>
    public ArchiveOwnedRadarEventBatchQueueingPublisher(
        RadarProcessingProviderQueueOptions? queueOptions = null,
        RadarProcessingRetainedPayloadOptions? retainedPayloadOptions = null,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null)
        : this(
            new RadarProcessingOwnedBatchQueue(queueOptions),
            ownsQueue: true,
            retainedPayloadOptions,
            retainedPayloadFactory)
    {
    }

    /// <summary>
    /// Creates a queueing publisher over an explicit processing queue.
    /// </summary>
    public ArchiveOwnedRadarEventBatchQueueingPublisher(
        RadarProcessingOwnedBatchQueue queue,
        bool ownsQueue = false,
        RadarProcessingRetainedPayloadOptions? retainedPayloadOptions = null,
        RadarProcessingRetainedPayloadFactory? retainedPayloadFactory = null)
    {
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this.ownsQueue = ownsQueue;
        this.retainedPayloadOptions = retainedPayloadOptions ?? RadarProcessingRetainedPayloadOptions.Default;
        this.retainedPayloadFactory = retainedPayloadFactory ?? new RadarProcessingRetainedPayloadFactory();
    }

    /// <summary>
    /// Gets the processing queue that receives retained archive batches.
    /// </summary>
    public RadarProcessingOwnedBatchQueue Queue => queue;

    /// <summary>
    /// Gets a snapshot of enqueue results recorded by publish calls.
    /// </summary>
    public IReadOnlyList<RadarProcessingQueuedBatchEnqueueResult> EnqueueResults
    {
        get
        {
            lock (sync)
            {
                return Array.AsReadOnly(enqueueResults.ToArray());
            }
        }
    }
}

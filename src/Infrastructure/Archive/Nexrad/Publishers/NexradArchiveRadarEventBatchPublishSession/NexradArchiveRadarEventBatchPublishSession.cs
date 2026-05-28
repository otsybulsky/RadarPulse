using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Reusable session for projecting Archive II files into radar event batches.
/// </summary>
/// <remarks>
/// Parallel mode decompresses records concurrently and drains decompressed payloads in record order before projection,
/// preserving deterministic batch and dictionary results while reusing worker buffers across files.
/// </remarks>
public sealed partial class NexradArchiveRadarEventBatchPublishSession : IDisposable
{
    private const int OutputBufferSize = 81920;
    private const int DefaultInitialEventCapacity = 256;
    private const int DefaultInitialPayloadCapacity = 4096;
    private const int EstimatedPayloadBytesPerCompressedByte = 10;
    private const int EstimatedPayloadBytesPerStreamEvent = 1536;
    private const int EstimatedEventsPerCompressedRecord = 640;
    private const int MaxInitialPayloadCapacity = 128 * 1024 * 1024;
    private const int MaxInitialEventCapacity = 1_000_000;

    private readonly IArchiveBZip2Decompressor decompressor;
    private readonly ArchiveRadarEventBatchPublishOptions options;
    private readonly ArchiveRadarEventBatchWorker[] workers;
    private readonly ConcurrentStack<ArchiveRadarEventBatchWorker> availableWorkers = new();
    private readonly Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>> inFlight = new();
    private readonly byte[] controlWordBuffer = new byte[4];
    private ArchiveTwoRadarEventBatchProjector? projector;
    private ArchiveTwoMessageStreamScanner? scanner;
    private bool disposed;

    /// <summary>
    /// Creates a reusable radar event batch publish session.
    /// </summary>
    public NexradArchiveRadarEventBatchPublishSession(
        IArchiveBZip2Decompressor decompressor,
        ArchiveRadarEventBatchPublishOptions options)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        workers = new ArchiveRadarEventBatchWorker[options.DegreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveRadarEventBatchWorker(decompressor.CreateSession());
        }
    }

    /// <summary>
    /// Gets the number of worker sessions used for compressed-record processing.
    /// </summary>
    public int DegreeOfParallelism => options.DegreeOfParallelism;

    /// <summary>
    /// Publishes one Archive II file to an internal counting batch publisher.
    /// </summary>
    public ArchiveRadarEventBatchPublishResult PublishFile(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return PublishFile(
            filePath,
            new ArchiveRadarEventBatchCountingPublisher(),
            cancellationToken);
    }

    /// <summary>
    /// Publishes one Archive II file to the supplied radar event batch publisher.
    /// </summary>
    public ArchiveRadarEventBatchPublishResult PublishFile(
        string filePath,
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(publisher);

        var fileInfo = GetExistingFileInfo(filePath);
        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, publisher, cancellationToken)
            : PublishFileParallel(fileInfo, publisher, cancellationToken);
    }
}

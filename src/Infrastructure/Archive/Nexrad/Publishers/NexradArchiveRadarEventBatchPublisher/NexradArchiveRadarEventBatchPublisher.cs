using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Publishes Archive II data as compact radar event batches.
/// </summary>
/// <remarks>
/// The publisher preserves compressed-record order for stream projection and emits batches that carry dictionary,
/// source-universe, and stream schema versions.
/// </remarks>
public sealed partial class NexradArchiveRadarEventBatchPublisher
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

    /// <summary>
    /// Creates a batch publisher with the default archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveRadarEventBatchPublisher()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a batch publisher with an explicit archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveRadarEventBatchPublisher(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Publishes one Archive II file to an internal counting batch publisher.
    /// </summary>
    public ArchiveRadarEventBatchPublishResult PublishFile(
        string filePath,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);

        return PublishFile(
            filePath,
            new ArchiveRadarEventBatchCountingPublisher(),
            options,
            cancellationToken);
    }

    /// <summary>
    /// Publishes one Archive II file to the supplied radar event batch publisher.
    /// </summary>
    public ArchiveRadarEventBatchPublishResult PublishFile(
        string filePath,
        IArchiveRadarEventBatchPublisher publisher,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);

        var fileInfo = GetExistingFileInfo(filePath);
        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, publisher, options, cancellationToken)
            : PublishFileParallel(fileInfo, publisher, options, cancellationToken);
    }
}

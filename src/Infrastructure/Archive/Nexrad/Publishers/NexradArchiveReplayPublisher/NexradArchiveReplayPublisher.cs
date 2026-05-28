using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Publishes Archive II data as ordered gate-moment replay events.
/// </summary>
/// <remarks>
/// Sequential publishing streams events directly. Parallel publishing buffers per-record projection results and drains
/// them in compressed-record order so published totals and chronology checksums remain deterministic.
/// </remarks>
public sealed partial class NexradArchiveReplayPublisher
{
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;

    /// <summary>
    /// Creates a replay publisher with the default archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveReplayPublisher()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    /// <summary>
    /// Creates a replay publisher with an explicit archive BZip2 decompressor.
    /// </summary>
    public NexradArchiveReplayPublisher(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    /// <summary>
    /// Publishes one Archive II file to an internal counting publisher and returns deterministic totals.
    /// </summary>
    public ArchiveReplayPublishResult PublishFile(
        string filePath,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var fileInfo = GetExistingFileInfo(filePath);

        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, new ArchiveReplayCountingPublisher(), options, cancellationToken)
            : PublishFileParallelCounting(fileInfo, options, cancellationToken);
    }

    /// <summary>
    /// Publishes one Archive II file to the supplied replay event publisher.
    /// </summary>
    public ArchiveReplayPublishResult PublishFile(
        string filePath,
        IArchiveReplayEventPublisher publisher,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var fileInfo = GetExistingFileInfo(filePath);

        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, publisher, options, cancellationToken)
            : PublishFileParallelBuffered(fileInfo, publisher, options, cancellationToken);
    }

}

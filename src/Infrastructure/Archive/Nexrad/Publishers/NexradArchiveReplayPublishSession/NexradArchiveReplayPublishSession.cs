using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Reusable session for publishing Archive II files or cache selections as replay events.
/// </summary>
/// <remarks>
/// The session owns reusable worker buffers and projector state across files. Parallel mode performs a metadata pass
/// to preserve sweep/radial continuity before ordered event accumulation.
/// </remarks>
public sealed partial class NexradArchiveReplayPublishSession : IDisposable
{
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;
    private readonly ArchiveReplaySessionWorker[] workers;
    private readonly ArchiveReplayEventAccumulator totalAccumulator = new();
    private ArchiveReplayRecordMetadata[] metadataByRecord = [];
    private ArchiveTwoGateMomentProjectorState[] startingStatesByRecord = [];
    private ArchiveReplayEventAccumulator[] accumulatorsByRecord = [];
    private int[] compressedRecordCountsByRecord = [];
    private long[] compressedBytesByRecord = [];
    private long[] decompressedBytesByRecord = [];
    private bool disposed;

    /// <summary>
    /// Creates a reusable replay publish session.
    /// </summary>
    public NexradArchiveReplayPublishSession(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");
        }

        DegreeOfParallelism = degreeOfParallelism;
        workers = new ArchiveReplaySessionWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveReplaySessionWorker(decompressor.CreateSession());
        }
    }

    /// <summary>
    /// Gets the number of worker sessions used for compressed-record processing.
    /// </summary>
    public int DegreeOfParallelism { get; }

    /// <summary>
    /// Publishes one Archive II file and returns deterministic replay totals.
    /// </summary>
    public ArchiveReplayPublishResult PublishFile(
        string filePath,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        return DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, cancellationToken)
            : PublishFileParallel(fileInfo, cancellationToken);
    }

    /// <summary>
    /// Publishes matching Archive II files from a cache directory and returns aggregate replay totals.
    /// </summary>
    public ArchiveReplayCachePublishResult PublishCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Max files must be greater than zero.");
        }

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var files = new List<ArchiveReplayPublishResult>();
        var examinedFiles = 0;
        var skippedFiles = 0;
        var chronologyChecksum = 0UL;

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (examinedFiles >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, normalizedRadarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            examinedFiles++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                skippedFiles++;
                continue;
            }

            var result = PublishFile(fileInfo.FullName, cancellationToken);
            files.Add(result);
            chronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Combine(
                chronologyChecksum,
                result.ChronologyChecksum,
                result.PublishedEvents);
        }

        return new ArchiveReplayCachePublishResult(
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            decompressor.Name,
            DegreeOfParallelism,
            examinedFiles,
            skippedFiles,
            files,
            chronologyChecksum);
    }

}

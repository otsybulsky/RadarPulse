using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Downloads historical archive manifest files into the local NEXRAD cache layout.
/// </summary>
/// <remarks>
/// The downloader verifies disk space before transfer, skips matching cached files, writes metadata sidecars, and
/// uses temporary part files so incomplete downloads are not mistaken for valid cache entries.
/// </remarks>
public sealed class HistoricalArchiveDownloader(
    IHistoricalArchiveClient client,
    NexradCachePathMapper pathMapper,
    IDiskSpaceProbe? diskSpaceProbe = null,
    HistoricalArchiveCacheMetadataStore? metadataStore = null)
{
    private readonly IDiskSpaceProbe _diskSpaceProbe = diskSpaceProbe ?? new DriveInfoDiskSpaceProbe();
    private readonly HistoricalArchiveCacheMetadataStore _metadataStore = metadataStore ?? new HistoricalArchiveCacheMetadataStore();

    /// <summary>
    /// Computes required download bytes and available disk space for a manifest selection.
    /// </summary>
    public HistoricalArchiveDownloadPreflight CheckPreflight(
        HistoricalArchiveManifest manifest,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var requiredDownloadBytes = CalculateRequiredDownloadBytes(manifest, outputDirectory, cancellationToken);
        var availableBytes = _diskSpaceProbe.GetAvailableBytes(outputDirectory);

        return new HistoricalArchiveDownloadPreflight(requiredDownloadBytes, availableBytes);
    }

    /// <summary>
    /// Downloads missing manifest files with bounded concurrency and cache metadata validation.
    /// </summary>
    public async Task<HistoricalArchiveDownloadResult> DownloadAsync(
        HistoricalArchiveManifest manifest,
        string outputDirectory,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (maxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Concurrency must be greater than zero.");
        }

        var preflight = CheckPreflight(manifest, outputDirectory, cancellationToken);
        if (preflight.AvailableBytes < preflight.RequiredDownloadBytes)
        {
            throw new InvalidOperationException(
                $"Insufficient disk space for archive download. Required {preflight.RequiredDownloadBytes} bytes, available {preflight.AvailableBytes} bytes.");
        }

        var downloadedFiles = 0;
        var skippedFiles = 0;
        long downloadedBytes = 0;
        long skippedBytes = 0;

        await Parallel.ForEachAsync(
            manifest.Files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken
            },
            async (file, ct) =>
            {
                var localPath = pathMapper.MapToLocalPath(outputDirectory, file);
                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (ShouldSkipExistingFile(localPath, file))
                {
                    if (!_metadataStore.HasMetadata(localPath))
                    {
                        await _metadataStore.WriteAsync(localPath, file, ct);
                    }

                    Interlocked.Increment(ref skippedFiles);
                    Interlocked.Add(ref skippedBytes, file.SizeBytes);
                    return;
                }

                var tempPath = $"{localPath}.part";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                await using (var destination = new FileStream(
                                 tempPath,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 useAsync: true))
                {
                    await client.DownloadFileAsync(file, destination, ct);
                }

                var downloadedLength = new FileInfo(tempPath).Length;
                if (downloadedLength != file.SizeBytes)
                {
                    File.Delete(tempPath);
                    throw new InvalidOperationException(
                        $"Downloaded file size mismatch for {file.ArchivePath}. Expected {file.SizeBytes} bytes, got {downloadedLength}.");
                }

                File.Move(tempPath, localPath, overwrite: true);
                await _metadataStore.WriteAsync(localPath, file, ct);

                Interlocked.Increment(ref downloadedFiles);
                Interlocked.Add(ref downloadedBytes, file.SizeBytes);
            });

        return new HistoricalArchiveDownloadResult(
            downloadedFiles,
            skippedFiles,
            downloadedBytes,
            skippedBytes);
    }

    private long CalculateRequiredDownloadBytes(
        HistoricalArchiveManifest manifest,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        long requiredBytes = 0;
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = pathMapper.MapToLocalPath(outputDirectory, file);
            if (ShouldSkipExistingFile(localPath, file))
            {
                continue;
            }

            checked
            {
                requiredBytes += file.SizeBytes;
            }
        }

        return requiredBytes;
    }

    private bool ShouldSkipExistingFile(string localPath, HistoricalArchiveFile file) =>
        _metadataStore.Matches(localPath, file);
}

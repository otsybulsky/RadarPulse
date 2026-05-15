using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveRadarEventBatchStreamBenchmark
{
    private readonly IArchiveBZip2Decompressor decompressor;

    public NexradArchiveRadarEventBatchStreamBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public NexradArchiveRadarEventBatchStreamBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    public ArchiveRadarEventBatchStreamBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveRadarEventBatchStreamBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
            .Measure(filePath, iterations, warmupIterations, degreeOfParallelism, cancellationToken);

    public ArchiveRadarEventBatchStreamBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (iterations <= 0)
        {
            throw new ArgumentException("Iterations must be greater than zero.", nameof(iterations));
        }

        if (warmupIterations < 0)
        {
            throw new ArgumentException("Warmup iterations cannot be negative.", nameof(warmupIterations));
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentException("Degree of parallelism must be greater than zero.", nameof(degreeOfParallelism));
        }

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var options = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        var publisher = new NexradArchiveRadarEventBatchPublisher(decompressor);
        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            publisher.PublishFile(fileInfo.FullName, options, cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ArchiveRadarEventBatchPublishResult? expectedIteration = null;
        RadarStreamDictionarySnapshotMetrics expectedDictionaryMetrics = default;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationResult = publisher.PublishFile(fileInfo.FullName, options, cancellationToken);
            var dictionaryMetrics = RadarStreamDictionarySnapshotMetrics.Compute(iterationResult.DictionarySnapshot);
            if (expectedIteration is null)
            {
                expectedIteration = iterationResult;
                expectedDictionaryMetrics = dictionaryMetrics;
            }
            else if (!HasSameTotals(expectedIteration, expectedDictionaryMetrics, iterationResult, dictionaryMetrics))
            {
                throw new InvalidDataException("Radar event batch stream benchmark produced inconsistent iteration totals.");
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measurement = expectedIteration ?? throw new InvalidOperationException("Radar event batch stream benchmark did not run any iterations.");

        return new ArchiveRadarEventBatchStreamBenchmarkResult(
            measurement.FilePath,
            measurement.Decompressor,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            measurement.StreamSchemaVersion,
            measurement.DictionaryVersion,
            measurement.SourceUniverseVersion,
            measurement.FileSizeBytes,
            measurement.CompressedRecordCount,
            measurement.CompressedBytes,
            measurement.DecompressedBytes,
            measurement.BatchCount,
            measurement.EventCount,
            measurement.PayloadBytes,
            measurement.PayloadValueCount,
            measurement.RawValueChecksum,
            expectedDictionaryMetrics.RadarCount,
            expectedDictionaryMetrics.MomentCount,
            expectedDictionaryMetrics.MappingChecksum,
            stopwatch.Elapsed,
            allocatedBytes);
    }

    public ArchiveRadarEventBatchStreamCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        new NexradArchiveRadarEventBatchStreamBenchmark(ArchiveBZip2Decompressors.Create(decompressorName))
            .MeasureCache(
                cachePath,
                date,
                radarId,
                maxFiles,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                cancellationToken);

    public ArchiveRadarEventBatchStreamCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentException("Max files must be greater than zero.", nameof(maxFiles));
        }

        if (iterations <= 0)
        {
            throw new ArgumentException("Iterations must be greater than zero.", nameof(iterations));
        }

        if (warmupIterations < 0)
        {
            throw new ArgumentException("Warmup iterations cannot be negative.", nameof(warmupIterations));
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentException("Degree of parallelism must be greater than zero.", nameof(degreeOfParallelism));
        }

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        var options = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        var publisher = new NexradArchiveRadarEventBatchPublisher(decompressor);
        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PublishCacheIteration(
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                publisher,
                options,
                cancellationToken);
        }

        var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        CacheIterationTotals? expectedIteration = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationResult = PublishCacheIteration(
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                publisher,
                options,
                cancellationToken);
            if (expectedIteration is null)
            {
                expectedIteration = iterationResult;
            }
            else if (!expectedIteration.Value.HasSameTotals(iterationResult))
            {
                throw new InvalidDataException("Radar event batch stream cache benchmark produced inconsistent iteration totals.");
            }
        }

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
        var measurement = expectedIteration ?? throw new InvalidOperationException("Radar event batch stream cache benchmark did not run any iterations.");

        return new ArchiveRadarEventBatchStreamCacheBenchmarkResult(
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            decompressor.Name,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            measurement.StreamSchemaVersion,
            measurement.SourceUniverseVersion,
            measurement.ExaminedFiles,
            measurement.SkippedFiles,
            measurement.PublishedFiles,
            measurement.FileSizeBytes,
            measurement.CompressedRecordCount,
            measurement.CompressedBytes,
            measurement.DecompressedBytes,
            measurement.BatchCount,
            measurement.EventCount,
            measurement.PayloadBytes,
            measurement.PayloadValueCount,
            measurement.RawValueChecksum,
            stopwatch.Elapsed,
            allocatedBytes);
    }

    private static bool HasSameTotals(
        ArchiveRadarEventBatchPublishResult expected,
        RadarStreamDictionarySnapshotMetrics expectedDictionaryMetrics,
        ArchiveRadarEventBatchPublishResult actual,
        RadarStreamDictionarySnapshotMetrics actualDictionaryMetrics) =>
        expected.FileSizeBytes == actual.FileSizeBytes &&
        expected.CompressedRecordCount == actual.CompressedRecordCount &&
        expected.CompressedBytes == actual.CompressedBytes &&
        expected.DecompressedBytes == actual.DecompressedBytes &&
        expected.StreamSchemaVersion == actual.StreamSchemaVersion &&
        expected.DictionaryVersion == actual.DictionaryVersion &&
        expected.SourceUniverseVersion == actual.SourceUniverseVersion &&
        expected.BatchCount == actual.BatchCount &&
        expected.EventCount == actual.EventCount &&
        expected.PayloadBytes == actual.PayloadBytes &&
        expected.PayloadValueCount == actual.PayloadValueCount &&
        expected.RawValueChecksum == actual.RawValueChecksum &&
        expectedDictionaryMetrics == actualDictionaryMetrics;

    private static CacheIterationTotals PublishCacheIteration(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        NexradArchiveRadarEventBatchPublisher publisher,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        var totals = new CacheIterationTotals(
            StreamSchemaVersion.Current,
            SourceUniverseVersion.Initial);

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFiles >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, radarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFiles++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFiles++;
                continue;
            }

            var result = publisher.PublishFile(fileInfo.FullName, options, cancellationToken);
            totals.Add(result);
        }

        return totals;
    }

    private static bool MatchesRadar(FileInfo fileInfo, string? radarId)
    {
        if (radarId is null)
        {
            return true;
        }

        return fileInfo.Name.StartsWith(radarId, StringComparison.OrdinalIgnoreCase) ||
            fileInfo.DirectoryName?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, radarId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static bool MatchesDate(FileInfo fileInfo, DateOnly? date)
    {
        if (date is null)
        {
            return true;
        }

        return TryReadDateFromFileName(fileInfo.Name, out var fileNameDate) && fileNameDate == date ||
            PathContainsDate(fileInfo.FullName, date.Value);
    }

    private static bool TryReadDateFromFileName(string fileName, out DateOnly date)
    {
        date = default;
        if (fileName.Length < 12)
        {
            return false;
        }

        var dateText = fileName.AsSpan(4, 8);
        if (!int.TryParse(dateText[..4], out var year) ||
            !int.TryParse(dateText.Slice(4, 2), out var month) ||
            !int.TryParse(dateText.Slice(6, 2), out var day))
        {
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool PathContainsDate(string path, DateOnly date)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i <= segments.Length - 3; i++)
        {
            if (string.Equals(segments[i], date.Year.ToString("0000"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 1], date.Month.ToString("00"), StringComparison.Ordinal) &&
                string.Equals(segments[i + 2], date.Day.ToString("00"), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private struct CacheIterationTotals
    {
        public CacheIterationTotals(
            StreamSchemaVersion streamSchemaVersion,
            SourceUniverseVersion sourceUniverseVersion)
            : this()
        {
            StreamSchemaVersion = streamSchemaVersion;
            SourceUniverseVersion = sourceUniverseVersion;
        }

        public StreamSchemaVersion StreamSchemaVersion;
        public SourceUniverseVersion SourceUniverseVersion;
        public int ExaminedFiles;
        public int SkippedFiles;
        public int PublishedFiles;
        public long FileSizeBytes;
        public int CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(ArchiveRadarEventBatchPublishResult result)
        {
            if (PublishedFiles == 0)
            {
                StreamSchemaVersion = result.StreamSchemaVersion;
                SourceUniverseVersion = result.SourceUniverseVersion;
            }
            else if (StreamSchemaVersion != result.StreamSchemaVersion ||
                SourceUniverseVersion != result.SourceUniverseVersion)
            {
                throw new InvalidDataException("Radar event batch stream cache benchmark produced mixed stream schema or source-universe versions.");
            }

            PublishedFiles++;
            FileSizeBytes += result.FileSizeBytes;
            CompressedRecordCount += result.CompressedRecordCount;
            CompressedBytes += result.CompressedBytes;
            DecompressedBytes += result.DecompressedBytes;
            BatchCount += result.BatchCount;
            EventCount += result.EventCount;
            PayloadBytes += result.PayloadBytes;
            PayloadValueCount += result.PayloadValueCount;
            RawValueChecksum += result.RawValueChecksum;
        }

        public readonly bool HasSameTotals(CacheIterationTotals other) =>
            StreamSchemaVersion == other.StreamSchemaVersion &&
            SourceUniverseVersion == other.SourceUniverseVersion &&
            ExaminedFiles == other.ExaminedFiles &&
            SkippedFiles == other.SkippedFiles &&
            PublishedFiles == other.PublishedFiles &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum;
    }
}

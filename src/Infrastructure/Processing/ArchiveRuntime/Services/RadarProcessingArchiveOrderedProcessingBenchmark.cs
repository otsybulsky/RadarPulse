using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingArchiveOrderedProcessingBenchmark
{
    private const int MaxAutoSizedCacheRadarOrdinalCount = 256;

    private readonly IArchiveBZip2Decompressor decompressor;

    public RadarProcessingArchiveOrderedProcessingBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public RadarProcessingArchiveOrderedProcessingBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    public RadarProcessingArchiveOrderedProcessingBenchmarkResult MeasureFile(
        string filePath,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        int activeBatchCapacity,
        CancellationToken cancellationToken = default,
        RadarProcessingBenchmarkHandlerSet handlerSet = RadarProcessingBenchmarkHandlerSet.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
        ValidateCommon(
            iterations,
            warmupIterations,
            partitionCount,
            shardCount,
            degreeOfParallelism,
            activeBatchCapacity);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism));

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunFileIteration(
                archiveSession,
                fileInfo,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        OrderedProcessingIterationTelemetry? expectedIteration = null;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTelemetry = RunFileIteration(
                archiveSession,
                fileInfo,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
            if (expectedIteration.HasValue &&
                !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Archive ordered processing benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var measurement = expectedIteration ??
                          throw new InvalidOperationException("Archive ordered processing benchmark did not run.");
        return CreateResult(
            measurement,
            fileInfo.FullName,
            cachePath: null,
            date: null,
            radarId: null,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceUniverse.SourceCount,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            handlerSet,
            stopwatch.Elapsed,
            ExcludeStartupPrewarmAllocation(allocatedBytes, measurement.RetainedPayloadPrewarm, iterations));
    }

    public RadarProcessingArchiveOrderedProcessingBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        int activeBatchCapacity,
        CancellationToken cancellationToken = default,
        RadarProcessingBenchmarkHandlerSet handlerSet = RadarProcessingBenchmarkHandlerSet.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFiles);
        RadarProcessingBenchmarkHandlers.EnsureKnown(handlerSet);
        ValidateCommon(
            iterations,
            warmupIterations,
            partitionCount,
            shardCount,
            degreeOfParallelism,
            activeBatchCapacity);

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var sourceUniverse = CreateCacheSourceUniverse(
            directoryInfo,
            date,
            normalizedRadarId,
            maxFiles,
            cancellationToken);
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism));

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunCacheIteration(
                archiveSession,
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        OrderedProcessingIterationTelemetry? expectedIteration = null;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTelemetry = RunCacheIteration(
                archiveSession,
                directoryInfo,
                date,
                normalizedRadarId,
                maxFiles,
                sourceUniverse,
                partitionCount,
                shardCount,
                activeBatchCapacity,
                handlerSet,
                cancellationToken);
            if (expectedIteration.HasValue &&
                !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Archive ordered processing cache benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var measurement = expectedIteration ??
                          throw new InvalidOperationException("Archive ordered processing cache benchmark did not run.");
        return CreateResult(
            measurement,
            filePath: null,
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceUniverse.SourceCount,
            partitionCount,
            shardCount,
            activeBatchCapacity,
            handlerSet,
            stopwatch.Elapsed,
            ExcludeStartupPrewarmAllocation(allocatedBytes, measurement.RetainedPayloadPrewarm, iterations));
    }

    private static long ExcludeStartupPrewarmAllocation(
        long allocatedBytes,
        RadarProcessingRetainedPayloadPrewarmResult prewarm,
        int iterations)
    {
        if (!prewarm.Applied)
        {
            return allocatedBytes;
        }

        var prewarmAllocatedBytes = checked(prewarm.AllocatedBytes * iterations);
        return allocatedBytes > prewarmAllocatedBytes
            ? allocatedBytes - prewarmAllocatedBytes
            : 0;
    }

    private OrderedProcessingIterationTelemetry RunFileIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        FileInfo fileInfo,
        RadarSourceUniverse sourceUniverse,
        int partitionCount,
        int shardCount,
        int activeBatchCapacity,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        CancellationToken cancellationToken)
    {
        var handlers = RadarProcessingBenchmarkHandlers.Create(handlerSet);
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            sourceUniverse,
            partitionCount,
            shardCount,
            handlers: handlers);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var result = RunOrderedProcessing(
                (publisher, token) => archiveSession.PublishFile(fileInfo.FullName, publisher, token),
                core,
                runner,
                activeBatchCapacity,
                cancellationToken);

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        var publishResult = result.Producer.PublishResult ??
                            throw new InvalidOperationException("Archive ordered processing producer did not publish a result.");
        var totals = CacheIterationTotals.Empty;
        totals.ExaminedFiles = 1;
        totals.Add(publishResult);
        return OrderedProcessingIterationTelemetry.FromResult(totals, result);
    }

    private OrderedProcessingIterationTelemetry RunCacheIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        RadarSourceUniverse sourceUniverse,
        int partitionCount,
        int shardCount,
        int activeBatchCapacity,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        CancellationToken cancellationToken)
    {
        var selection = SelectCacheArchiveFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        var publishedTotals = selection.Totals;
        var handlers = RadarProcessingBenchmarkHandlers.Create(handlerSet);
        var runner = new RadarProcessingArchiveQueuedOverlapRunner();
        var core = RadarProcessingRuntimeArchiveBaseline.CreateCore(
            sourceUniverse,
            partitionCount,
            shardCount,
            handlers: handlers);
        var result = RunOrderedProcessing(
                (publisher, token) =>
                {
                    var totals = selection.Totals;
                    ArchiveRadarEventBatchPublishResult? lastPublishResult = null;
                    foreach (var fileInfo in selection.BaseDataFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        var publishResult = archiveSession.PublishFile(fileInfo.FullName, publisher, token);
                        totals.Add(publishResult);
                        lastPublishResult = publishResult;
                    }

                    publishedTotals = totals;
                    return lastPublishResult is null
                        ? CreateEmptyCacheAggregatePublishResult(directoryInfo.FullName, sourceUniverse, archiveSession.DegreeOfParallelism)
                        : CreateCacheAggregatePublishResult(directoryInfo.FullName, totals, lastPublishResult);
                },
                core,
                runner,
                activeBatchCapacity,
                cancellationToken);

        if (!result.IsCompleted)
        {
            throw new InvalidOperationException(result.Message);
        }

        return OrderedProcessingIterationTelemetry.FromResult(publishedTotals, result);
    }

    private static RadarProcessingArchiveQueuedOverlapResult RunOrderedProcessing(
        Func<IArchiveRadarEventBatchPublisher, CancellationToken, ArchiveRadarEventBatchPublishResult> produce,
        RadarProcessingCore core,
        RadarProcessingArchiveQueuedOverlapRunner runner,
        int activeBatchCapacity,
        CancellationToken cancellationToken)
    {
        var orderedOptions = new RadarProcessingOrderedConcurrencyOptions(activeBatchCapacity);
        if (core.Options.Handlers.Count == 0)
        {
            return runner.RunProcessingAsync(
                    produce,
                    core,
                    orderedOptions,
                    cancellationToken: cancellationToken)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        return runner.RunMvpProcessingAsync(
                produce,
                core,
                orderedOptions,
                cancellationToken: cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult()
            .OverlapResult;
    }

    private static RadarProcessingArchiveOrderedProcessingBenchmarkResult CreateResult(
        OrderedProcessingIterationTelemetry measurement,
        string? filePath,
        string? cachePath,
        DateOnly? date,
        string? radarId,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        int sourceCount,
        int partitionCount,
        int shardCount,
        int activeBatchCapacity,
        RadarProcessingBenchmarkHandlerSet handlerSet,
        TimeSpan elapsed,
        long allocatedBytes) =>
        new(
            filePath,
            cachePath,
            date,
            radarId,
            measurement.Decompressor,
            handlerSet,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceCount,
            partitionCount,
            shardCount,
            activeBatchCapacity,
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
            measurement.Status,
            measurement.ConsumerStatus,
            measurement.SucceededBatchCount,
            measurement.FailedProcessingBatchCount,
            measurement.FailedValidationBatchCount,
            measurement.CanceledBatchCount,
            measurement.SkippedAfterFaultBatchCount,
            measurement.FinalProcessedBatchCount,
            measurement.FinalProcessedStreamEventCount,
            measurement.FinalProcessedPayloadValueCount,
            measurement.FinalRawValueChecksum,
            measurement.FinalProcessingChecksum,
            measurement.ProcessingSucceeded,
            elapsed,
            allocatedBytes,
            measurement.QueueTelemetry,
            measurement.OverlapTelemetry,
            measurement.RetainedPayloadPrewarm,
            measurement.WorkerTelemetry);

    private static RadarSourceUniverse CreateCacheSourceUniverse(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        if (radarId is not null)
        {
            return ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        }

        var radarOrdinalCount = CountSelectedCacheRadarOrdinals(
            directoryInfo,
            date,
            radarId,
            maxFiles,
            cancellationToken);
        return CreateArchiveSourceUniverse(radarOrdinalCount);
    }

    private static int CountSelectedCacheRadarOrdinals(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var selection = SelectCacheArchiveFiles(directoryInfo, date, radarId, maxFiles, cancellationToken);
        if (selection.BaseDataFiles.Count == 0)
        {
            return ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse.RadarOrdinalCount;
        }

        var radarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInfo in selection.BaseDataFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
            radarIds.Add(header.RadarId);
            if (radarIds.Count > MaxAutoSizedCacheRadarOrdinalCount)
            {
                throw new InvalidOperationException(
                    $"Cache benchmark auto-sized source universe supports at most {MaxAutoSizedCacheRadarOrdinalCount} distinct radar ids. " +
                    "Pass a radar id filter or reduce max files.");
            }
        }

        return Math.Max(1, radarIds.Count);
    }

    private static RadarSourceUniverse CreateArchiveSourceUniverse(int radarOrdinalCount)
    {
        var defaultUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        if (radarOrdinalCount == defaultUniverse.RadarOrdinalCount)
        {
            return defaultUniverse;
        }

        return new RadarSourceUniverse(
            defaultUniverse.Version,
            radarOrdinalCount,
            defaultUniverse.ElevationSlotCount,
            defaultUniverse.AzimuthBucketCount,
            defaultUniverse.RangeBandCount);
    }

    private static CacheArchiveFileSelection SelectCacheArchiveFiles(
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        CancellationToken cancellationToken)
    {
        var totals = CacheIterationTotals.Empty;
        var baseDataFiles = new List<FileInfo>();
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

            baseDataFiles.Add(fileInfo);
        }

        return new CacheArchiveFileSelection(totals, baseDataFiles);
    }

    private static ArchiveRadarEventBatchPublishResult CreateCacheAggregatePublishResult(
        string cachePath,
        CacheIterationTotals totals,
        ArchiveRadarEventBatchPublishResult lastPublishResult) =>
        new(
            cachePath,
            lastPublishResult.Decompressor,
            lastPublishResult.DegreeOfParallelism,
            totals.FileSizeBytes,
            checked((int)totals.CompressedRecordCount),
            totals.CompressedBytes,
            totals.DecompressedBytes,
            lastPublishResult.StreamSchemaVersion,
            lastPublishResult.DictionaryVersion,
            lastPublishResult.SourceUniverseVersion,
            totals.BatchCount,
            totals.EventCount,
            totals.PayloadBytes,
            totals.PayloadValueCount,
            totals.RawValueChecksum,
            lastPublishResult.DictionarySnapshot);

    private ArchiveRadarEventBatchPublishResult CreateEmptyCacheAggregatePublishResult(
        string cachePath,
        RadarSourceUniverse sourceUniverse,
        int degreeOfParallelism)
    {
        var normalizer = new RadarStreamIdentityNormalizer(sourceUniverse);
        return new ArchiveRadarEventBatchPublishResult(
            cachePath,
            decompressor.Name,
            degreeOfParallelism,
            FileSizeBytes: 0,
            CompressedRecordCount: 0,
            CompressedBytes: 0,
            DecompressedBytes: 0,
            StreamSchemaVersion.Current,
            DictionaryVersion.Initial,
            sourceUniverse.Version,
            BatchCount: 0,
            EventCount: 0,
            PayloadBytes: 0,
            PayloadValueCount: 0,
            RawValueChecksum: 0,
            normalizer.CreateDictionarySnapshot(DictionaryVersion.Initial));
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

    private static void ValidateCommon(
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        int activeBatchCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activeBatchCapacity);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }
    }

    private readonly struct OrderedProcessingIterationTelemetry
    {
        private OrderedProcessingIterationTelemetry(
            CacheIterationTotals totals,
            RadarProcessingArchiveQueuedOverlapResult result,
            RadarProcessingMetrics finalMetrics,
            RadarProcessingWorkerTelemetrySummary? workerTelemetry,
            long succeededBatchCount,
            long failedProcessingBatchCount,
            long failedValidationBatchCount,
            long canceledBatchCount,
            long skippedAfterFaultBatchCount,
            bool processingSucceeded)
        {
            Decompressor = result.Producer.PublishResult?.Decompressor ?? string.Empty;
            ExaminedFiles = totals.ExaminedFiles;
            SkippedFiles = totals.SkippedFiles;
            PublishedFiles = totals.PublishedFiles;
            FileSizeBytes = totals.FileSizeBytes;
            CompressedRecordCount = totals.CompressedRecordCount;
            CompressedBytes = totals.CompressedBytes;
            DecompressedBytes = totals.DecompressedBytes;
            BatchCount = totals.BatchCount;
            EventCount = totals.EventCount;
            PayloadBytes = totals.PayloadBytes;
            PayloadValueCount = totals.PayloadValueCount;
            RawValueChecksum = totals.RawValueChecksum;
            Status = result.Status;
            ConsumerStatus = result.Consumer.Status;
            SucceededBatchCount = succeededBatchCount;
            FailedProcessingBatchCount = failedProcessingBatchCount;
            FailedValidationBatchCount = failedValidationBatchCount;
            CanceledBatchCount = canceledBatchCount;
            SkippedAfterFaultBatchCount = skippedAfterFaultBatchCount;
            FinalProcessedBatchCount = finalMetrics.ProcessedBatchCount;
            FinalProcessedStreamEventCount = finalMetrics.ProcessedStreamEventCount;
            FinalProcessedPayloadValueCount = finalMetrics.ProcessedPayloadValueCount;
            FinalRawValueChecksum = finalMetrics.RawValueChecksum;
            FinalProcessingChecksum = finalMetrics.ProcessingChecksum;
            ProcessingSucceeded = processingSucceeded;
            QueueTelemetry = result.QueueTelemetry;
            OverlapTelemetry = result.OverlapTelemetry;
            RetainedPayloadPrewarm = result.RetainedPayloadPrewarm;
            WorkerTelemetry = workerTelemetry;
        }

        public string Decompressor { get; }
        public long ExaminedFiles { get; }
        public long SkippedFiles { get; }
        public long PublishedFiles { get; }
        public long FileSizeBytes { get; }
        public long CompressedRecordCount { get; }
        public long CompressedBytes { get; }
        public long DecompressedBytes { get; }
        public long BatchCount { get; }
        public long EventCount { get; }
        public long PayloadBytes { get; }
        public long PayloadValueCount { get; }
        public long RawValueChecksum { get; }
        public RadarProcessingArchiveQueuedOverlapStatus Status { get; }
        public RadarProcessingQueuedSessionStatus ConsumerStatus { get; }
        public long SucceededBatchCount { get; }
        public long FailedProcessingBatchCount { get; }
        public long FailedValidationBatchCount { get; }
        public long CanceledBatchCount { get; }
        public long SkippedAfterFaultBatchCount { get; }
        public long FinalProcessedBatchCount { get; }
        public long FinalProcessedStreamEventCount { get; }
        public long FinalProcessedPayloadValueCount { get; }
        public long FinalRawValueChecksum { get; }
        public ulong FinalProcessingChecksum { get; }
        public bool ProcessingSucceeded { get; }
        public RadarProcessingProviderQueueTelemetrySummary QueueTelemetry { get; }
        public RadarProcessingArchiveOverlapTelemetrySummary OverlapTelemetry { get; }
        public RadarProcessingRetainedPayloadPrewarmResult RetainedPayloadPrewarm { get; }
        public RadarProcessingWorkerTelemetrySummary? WorkerTelemetry { get; }

        public static OrderedProcessingIterationTelemetry FromResult(
            CacheIterationTotals totals,
            RadarProcessingArchiveQueuedOverlapResult result)
        {
            var processingResults = result.Consumer.SessionResult.ProcessingResults;
            var succeeded = 0L;
            var failedProcessing = 0L;
            var failedValidation = 0L;
            var canceled = 0L;
            var skippedAfterFault = 0L;
            RadarProcessingResult? finalProcessing = null;

            foreach (var processingResult in processingResults)
            {
                switch (processingResult.Status)
                {
                    case RadarProcessingQueuedBatchProcessingStatus.Succeeded:
                        succeeded++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.FailedProcessing:
                    case RadarProcessingQueuedBatchProcessingStatus.FailedMigration:
                        failedProcessing++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.FailedValidation:
                        failedValidation++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.Canceled:
                        canceled++;
                        break;
                    case RadarProcessingQueuedBatchProcessingStatus.SkippedAfterFault:
                        skippedAfterFault++;
                        break;
                    default:
                        RadarProcessingQueuedBatchProcessingResult.EnsureKnownStatus(processingResult.Status);
                        throw new ArgumentOutOfRangeException(nameof(processingResults));
                }

                if (processingResult.ProcessingResult is not null)
                {
                    finalProcessing = processingResult.ProcessingResult;
                }
            }

            var finalMetrics = finalProcessing?.Metrics ?? RadarProcessingMetrics.Empty;
            var workerTelemetry = finalProcessing?.WorkerTelemetry;
            var processingSucceeded =
                result.IsCompleted &&
                failedProcessing == 0 &&
                failedValidation == 0 &&
                canceled == 0 &&
                skippedAfterFault == 0 &&
                result.QueueTelemetry.FailedBatchCount == 0 &&
                result.QueueTelemetry.CanceledBatchCount == 0 &&
                result.QueueTelemetry.SkippedAfterFaultCount == 0 &&
                result.ProviderResult.RetentionTelemetry.ReleaseFailedCount == 0 &&
                finalMetrics.ProcessedBatchCount == totals.BatchCount &&
                finalMetrics.ProcessedStreamEventCount == totals.EventCount &&
                finalMetrics.ProcessedPayloadValueCount == totals.PayloadValueCount &&
                finalMetrics.RawValueChecksum == totals.RawValueChecksum;

            return new OrderedProcessingIterationTelemetry(
                totals,
                result,
                finalMetrics,
                workerTelemetry,
                succeeded,
                failedProcessing,
                failedValidation,
                canceled,
                skippedAfterFault,
                processingSucceeded);
        }

        public bool HasSameStableTotals(OrderedProcessingIterationTelemetry other) =>
            Decompressor == other.Decompressor &&
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
            RawValueChecksum == other.RawValueChecksum &&
            Status == other.Status &&
            ConsumerStatus == other.ConsumerStatus &&
            SucceededBatchCount == other.SucceededBatchCount &&
            FailedProcessingBatchCount == other.FailedProcessingBatchCount &&
            FailedValidationBatchCount == other.FailedValidationBatchCount &&
            CanceledBatchCount == other.CanceledBatchCount &&
            SkippedAfterFaultBatchCount == other.SkippedAfterFaultBatchCount &&
            FinalProcessedBatchCount == other.FinalProcessedBatchCount &&
            FinalProcessedStreamEventCount == other.FinalProcessedStreamEventCount &&
            FinalProcessedPayloadValueCount == other.FinalProcessedPayloadValueCount &&
            FinalRawValueChecksum == other.FinalRawValueChecksum &&
            FinalProcessingChecksum == other.FinalProcessingChecksum &&
            ProcessingSucceeded == other.ProcessingSucceeded;
    }

    private struct CacheIterationTotals
    {
        public static CacheIterationTotals Empty => new();

        public long ExaminedFiles;
        public long SkippedFiles;
        public long PublishedFiles;
        public long FileSizeBytes;
        public long CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(ArchiveRadarEventBatchPublishResult result)
        {
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
    }

    private sealed record CacheArchiveFileSelection(
        CacheIterationTotals Totals,
        IReadOnlyList<FileInfo> BaseDataFiles);
}

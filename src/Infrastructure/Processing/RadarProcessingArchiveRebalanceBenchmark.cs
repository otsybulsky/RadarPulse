using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingArchiveRebalanceBenchmark
{
    private const ulong ChecksumInitial = 14_695_981_039_346_656_037UL;
    private const ulong ChecksumPrime = 1_099_511_628_211UL;

    private readonly IArchiveBZip2Decompressor decompressor;

    public RadarProcessingArchiveRebalanceBenchmark()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public RadarProcessingArchiveRebalanceBenchmark(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    public RadarProcessingArchiveRebalanceBenchmarkResult MeasureFile(
        string filePath,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        EnsureKnownMode(mode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        var effectiveHardeningOptions = hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default;

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        if (partitionCount > sourceUniverse.SourceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be less than or equal to source count.");
        }

        var publishOptions = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            publishOptions);

        for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunIteration(
                archiveSession,
                fileInfo.FullName,
                sourceUniverse,
                mode,
                partitionCount,
                shardCount,
                effectiveHardeningOptions,
                cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ArchiveIterationTelemetry? expectedIteration = null;
        var aggregate = ArchiveIterationTelemetry.Empty;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var iterationTelemetry = RunIteration(
                archiveSession,
                fileInfo.FullName,
                sourceUniverse,
                mode,
                partitionCount,
                shardCount,
                effectiveHardeningOptions,
                cancellationToken);
            if (expectedIteration.HasValue && !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Archive rebalance benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
            aggregate = aggregate.Add(iterationTelemetry);
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var allocationSummary = RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(
            allocatedBytes,
            aggregate.ProcessingCallbackAllocatedBytes);
        var measuredIteration = expectedIteration ??
                                throw new InvalidOperationException("Archive rebalance benchmark did not run.");

        return new RadarProcessingArchiveRebalanceBenchmarkResult(
            fileInfo.FullName,
            decompressor.Name,
            mode,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceUniverse.SourceCount,
            partitionCount,
            shardCount,
            measuredIteration.FileSizeBytes,
            measuredIteration.CompressedRecordCount,
            measuredIteration.CompressedBytes,
            measuredIteration.DecompressedBytes,
            measuredIteration.BatchCount,
            measuredIteration.EventCount,
            measuredIteration.PayloadBytes,
            measuredIteration.PayloadValueCount,
            measuredIteration.RawValueChecksum,
            measuredIteration.TopologyVersionCount,
            aggregate.RebalanceEvaluationCount,
            aggregate.AcceptedMoveCount,
            aggregate.SkippedDecisionCount,
            aggregate.DirectHotReliefCount,
            aggregate.ColdEvacuationCount,
            aggregate.FailedMigrationCount,
            aggregate.ValidationSucceeded,
            aggregate.ValidationChecksum,
            CreateReadOnlyList(aggregate.SkippedReasons),
            CreateReadOnlyList(aggregate.AcceptedMovePressures),
            aggregate.RetentionStats,
            stopwatch.Elapsed,
            aggregate.ProcessingElapsed,
            allocatedBytes,
            effectiveHardeningOptions.ValidationProfile,
            effectiveHardeningOptions.TelemetryRetention.RetentionMode,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedDecisions,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedLifecycleTransitions,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedAcceptedMoves,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedValidationFailures,
            allocationSummary);
    }

    public RadarProcessingArchiveRebalanceCacheBenchmarkResult MeasureCache(
        string cachePath,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int iterations,
        int warmupIterations,
        int partitionCount,
        int shardCount,
        int degreeOfParallelism,
        CancellationToken cancellationToken = default,
        RadarProcessingRebalanceHardeningOptions? hardeningOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFiles);
        EnsureKnownMode(mode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partitionCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degreeOfParallelism);

        if (partitionCount < shardCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be greater than or equal to shard count.");
        }

        var effectiveHardeningOptions = hardeningOptions ?? RadarProcessingRebalanceHardeningOptions.Default;

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var sourceUniverse = ArchiveRadarEventBatchPublishOptions.DefaultSingleRadar.SourceUniverse;
        if (partitionCount > sourceUniverse.SourceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount),
                partitionCount,
                "Partition count must be less than or equal to source count.");
        }

        var publishOptions = new ArchiveRadarEventBatchPublishOptions(sourceUniverse, degreeOfParallelism);
        using var archiveSession = new NexradArchiveRadarEventBatchPublishSession(
            decompressor,
            publishOptions);

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
                mode,
                partitionCount,
                shardCount,
                effectiveHardeningOptions,
                cancellationToken);
        }

        var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ArchiveIterationTelemetry? expectedIteration = null;
        var aggregate = ArchiveIterationTelemetry.Empty;
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
                mode,
                partitionCount,
                shardCount,
                effectiveHardeningOptions,
                cancellationToken);
            if (expectedIteration.HasValue && !expectedIteration.Value.HasSameStableTotals(iterationTelemetry))
            {
                throw new InvalidDataException("Archive cache rebalance benchmark produced inconsistent iteration totals.");
            }

            expectedIteration ??= iterationTelemetry;
            aggregate = aggregate.Add(iterationTelemetry);
        }

        stopwatch.Stop();
        var allocatedBytes = RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore);
        var allocationSummary = RadarProcessingRebalanceAllocationSummary.ForArchiveReplay(
            allocatedBytes,
            aggregate.ProcessingCallbackAllocatedBytes);
        var measuredIteration = expectedIteration ??
                                throw new InvalidOperationException("Archive cache rebalance benchmark did not run.");

        return new RadarProcessingArchiveRebalanceCacheBenchmarkResult(
            directoryInfo.FullName,
            date,
            normalizedRadarId,
            decompressor.Name,
            mode,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            sourceUniverse.SourceCount,
            partitionCount,
            shardCount,
            measuredIteration.ExaminedFileCount,
            measuredIteration.SkippedFileCount,
            measuredIteration.PublishedFileCount,
            measuredIteration.FileSizeBytes,
            measuredIteration.CompressedRecordCount,
            measuredIteration.CompressedBytes,
            measuredIteration.DecompressedBytes,
            measuredIteration.BatchCount,
            measuredIteration.EventCount,
            measuredIteration.PayloadBytes,
            measuredIteration.PayloadValueCount,
            measuredIteration.RawValueChecksum,
            measuredIteration.TopologyVersionCount,
            aggregate.RebalanceEvaluationCount,
            aggregate.AcceptedMoveCount,
            aggregate.SkippedDecisionCount,
            aggregate.DirectHotReliefCount,
            aggregate.ColdEvacuationCount,
            aggregate.FailedMigrationCount,
            aggregate.ValidationSucceeded,
            aggregate.ValidationChecksum,
            CreateReadOnlyList(aggregate.SkippedReasons),
            CreateReadOnlyList(aggregate.AcceptedMovePressures),
            aggregate.RetentionStats,
            stopwatch.Elapsed,
            aggregate.ProcessingElapsed,
            allocatedBytes,
            effectiveHardeningOptions.ValidationProfile,
            effectiveHardeningOptions.TelemetryRetention.RetentionMode,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedDecisions,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedLifecycleTransitions,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedAcceptedMoves,
            effectiveHardeningOptions.TelemetryRetention.MaxRetainedValidationFailures,
            allocationSummary);
    }

    private static ArchiveIterationTelemetry RunIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        string filePath,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        CancellationToken cancellationToken)
    {
        var processor = new ArchiveRebalanceBatchProcessor(
            sourceUniverse,
            mode,
            partitionCount,
            shardCount,
            hardeningOptions);
        var publishResult = archiveSession.PublishFile(filePath, processor, cancellationToken);
        return processor.BuildTelemetry(publishResult);
    }

    private static ArchiveIterationTelemetry RunCacheIteration(
        NexradArchiveRadarEventBatchPublishSession archiveSession,
        DirectoryInfo directoryInfo,
        DateOnly? date,
        string? radarId,
        int maxFiles,
        RadarSourceUniverse sourceUniverse,
        RadarProcessingSyntheticRebalanceBenchmarkMode mode,
        int partitionCount,
        int shardCount,
        RadarProcessingRebalanceHardeningOptions hardeningOptions,
        CancellationToken cancellationToken)
    {
        var processor = new ArchiveRebalanceBatchProcessor(
            sourceUniverse,
            mode,
            partitionCount,
            shardCount,
            hardeningOptions);
        var totals = CacheIterationTotals.Empty;

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (totals.ExaminedFileCount >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, radarId) ||
                !MatchesDate(fileInfo, date))
            {
                continue;
            }

            totals.ExaminedFileCount++;
            if (!ArchiveTwoFileReader.IsArchiveTwoBaseData(fileInfo))
            {
                totals.SkippedFileCount++;
                continue;
            }

            var publishResult = archiveSession.PublishFile(fileInfo.FullName, processor, cancellationToken);
            totals.Add(publishResult);
        }

        return processor.BuildTelemetry(totals);
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

    private static void EnsureKnownMode(RadarProcessingSyntheticRebalanceBenchmarkMode mode)
    {
        if (mode is not RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly and
            not RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static ulong AppendByte(ulong checksum, byte value) =>
        unchecked((checksum ^ value) * ChecksumPrime);

    private static ulong AppendInt32(ulong checksum, int value) =>
        AppendUInt32(checksum, unchecked((uint)value));

    private static ulong AppendUInt32(ulong checksum, uint value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        return AppendByte(checksum, (byte)(value >> 24));
    }

    private static ulong AppendInt64(ulong checksum, long value) =>
        AppendUInt64(checksum, unchecked((ulong)value));

    private static ulong AppendUInt64(ulong checksum, ulong value)
    {
        checksum = AppendByte(checksum, (byte)value);
        checksum = AppendByte(checksum, (byte)(value >> 8));
        checksum = AppendByte(checksum, (byte)(value >> 16));
        checksum = AppendByte(checksum, (byte)(value >> 24));
        checksum = AppendByte(checksum, (byte)(value >> 32));
        checksum = AppendByte(checksum, (byte)(value >> 40));
        checksum = AppendByte(checksum, (byte)(value >> 48));
        return AppendByte(checksum, (byte)(value >> 56));
    }

    private static IReadOnlyList<T> CreateReadOnlyList<T>(List<T>? values) =>
        values is { Count: > 0 }
            ? Array.AsReadOnly(values.ToArray())
            : Array.Empty<T>();

    private sealed class ArchiveRebalanceBatchProcessor : IArchiveRadarEventBatchPublisher
    {
        private readonly RadarProcessingSyntheticRebalanceBenchmarkMode mode;
        private readonly RadarProcessingCore? core;
        private readonly RadarProcessingPressureWindow? pressureWindow;
        private readonly RadarProcessingRebalanceSession? rebalanceSession;
        private readonly System.Diagnostics.Stopwatch processingStopwatch = new();
        private ArchiveIterationTelemetry telemetry = ArchiveIterationTelemetry.Empty;
        private long processingCallbackAllocatedBytes;

        public ArchiveRebalanceBatchProcessor(
            RadarSourceUniverse sourceUniverse,
            RadarProcessingSyntheticRebalanceBenchmarkMode mode,
            int partitionCount,
            int shardCount,
            RadarProcessingRebalanceHardeningOptions hardeningOptions)
        {
            ArgumentNullException.ThrowIfNull(hardeningOptions);

            this.mode = mode;
            var coreOptions = new RadarProcessingCoreOptions(
                RadarProcessingExecutionMode.PartitionedBarrier,
                partitionCount,
                shardCount);

            switch (mode)
            {
                case RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance:
                    core = new RadarProcessingCore(sourceUniverse, coreOptions);
                    break;
                case RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly:
                    core = new RadarProcessingCore(sourceUniverse, coreOptions);
                    pressureWindow = new RadarProcessingPressureWindow(
                        new RadarProcessingPressureWindowOptions(
                            sampleCapacity: 8,
                            minimumSampleCount: 1));
                    break;
                case RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession:
                    var rebalanceCore = new RadarProcessingCore(sourceUniverse, coreOptions);
                    rebalanceSession = new RadarProcessingRebalanceSession(
                        rebalanceCore,
                        pressureWindow: new RadarProcessingPressureWindow(
                            new RadarProcessingPressureWindowOptions(
                                sampleCapacity: 8,
                                minimumSampleCount: 1)),
                        policyState: new RadarProcessingRebalancePolicyState(
                            partitionCount,
                            shardCount,
                            new RadarProcessingRebalanceOptions(
                                budgetWindowEvaluationCount: 8,
                                globalMoveBudgetPerWindow: 1,
                                sourceShardMoveBudgetPerWindow: 1,
                                targetShardReceiveBudgetPerWindow: 1,
                                minimumPartitionResidencyEvaluations: 0,
                                partitionMoveCooldownEvaluations: 4,
                                sourceShardMoveCooldownEvaluations: 1,
                                targetShardReceiveCooldownEvaluations: 1)),
                        hardeningOptions: hardeningOptions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            var allocationBefore = RadarProcessingBenchmarkAllocationSnapshot.Capture();
            processingStopwatch.Start();
            try
            {
                telemetry = mode switch
                {
                    RadarProcessingSyntheticRebalanceBenchmarkMode.StaticNoRebalance =>
                        telemetry.Add(ProcessStatic(batch, cancellationToken)),
                    RadarProcessingSyntheticRebalanceBenchmarkMode.PressureSamplingOnly =>
                        telemetry.Add(ProcessPressureSampling(batch, cancellationToken)),
                    RadarProcessingSyntheticRebalanceBenchmarkMode.RebalanceSession =>
                        telemetry.Add(ProcessRebalanceSession(batch, cancellationToken)),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }
            finally
            {
                processingStopwatch.Stop();
                processingCallbackAllocatedBytes = checked(
                    processingCallbackAllocatedBytes +
                    RadarProcessingBenchmarkAllocationSnapshot.Capture().DeltaSince(allocationBefore));
            }
        }

        public ArchiveIterationTelemetry BuildTelemetry(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult publishResult) =>
            telemetry.WithPublishResult(
                publishResult,
                processingStopwatch.Elapsed,
                processingCallbackAllocatedBytes)
                .WithRetentionStats(CreateRetentionStats());

        public ArchiveIterationTelemetry BuildTelemetry(
            CacheIterationTotals totals) =>
            telemetry.WithPublishTotals(
                totals,
                processingStopwatch.Elapsed,
                processingCallbackAllocatedBytes)
                .WithRetentionStats(CreateRetentionStats());

        private ArchiveIterationTelemetry ProcessStatic(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var candidateCore = core ?? throw new InvalidOperationException("Static processing core was not initialized.");
            var result = candidateCore.Process(batch, cancellationToken);
            EnsureValidProcessingResult(result);
            return ArchiveIterationTelemetry.FromMetrics(
                candidateCore.CreateMetrics(),
                topologyVersionCount: 1);
        }

        private ArchiveIterationTelemetry ProcessPressureSampling(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var candidateCore = core ?? throw new InvalidOperationException("Pressure sampling core was not initialized.");
            var candidatePressureWindow = pressureWindow ??
                                          throw new InvalidOperationException("Pressure window was not initialized.");
            var result = candidateCore.Process(batch, cancellationToken);
            EnsureValidProcessingResult(result);
            var telemetryResult = result.Telemetry ??
                                  throw new InvalidDataException("Archive pressure sampling requires telemetry.");
            candidatePressureWindow.AddSample(RadarProcessingPressureSample.FromTelemetry(telemetryResult));
            return ArchiveIterationTelemetry.FromMetrics(
                candidateCore.CreateMetrics(),
                topologyVersionCount: 1,
                rebalanceEvaluationCount: 1);
        }

        private ArchiveIterationTelemetry ProcessRebalanceSession(
            RadarEventBatch batch,
            CancellationToken cancellationToken)
        {
            var session = rebalanceSession ??
                          throw new InvalidOperationException("Rebalance session was not initialized.");
            var initialTopologyVersion = session.CurrentTopology.Version;
            var result = session.Process(batch, cancellationToken);
            var metrics = session.Core.CreateMetrics();
            return ArchiveIterationTelemetry.FromRebalanceSessionResult(result)
                .WithMetrics(
                    metrics,
                    session.CurrentTopology.Version.Value - initialTopologyVersion.Value + 1);
        }

        private static void EnsureValidProcessingResult(RadarProcessingResult result)
        {
            if (!result.IsValid)
            {
                throw new InvalidDataException(result.Validation.Message);
            }

            if (result.Telemetry is null)
            {
                throw new InvalidDataException("Archive rebalance benchmark requires partitioned telemetry.");
            }
        }

        private RadarProcessingRebalanceRetentionStats CreateRetentionStats() =>
            rebalanceSession?.TelemetryRecorder.CreateSummary().RetentionStats ??
            new RadarProcessingRebalanceRetentionStats();
    }

    private readonly record struct ArchiveIterationTelemetry(
        long ExaminedFileCount,
        long SkippedFileCount,
        long PublishedFileCount,
        long FileSizeBytes,
        long CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        long BatchCount,
        long EventCount,
        long PayloadBytes,
        long PayloadValueCount,
        long RawValueChecksum,
        long TopologyVersionCount,
        long RebalanceEvaluationCount,
        long AcceptedMoveCount,
        long SkippedDecisionCount,
        long DirectHotReliefCount,
        long ColdEvacuationCount,
        long FailedMigrationCount,
        bool ValidationSucceeded,
        ulong ValidationChecksum,
        List<RadarProcessingRebalanceSkippedReason>? SkippedReasons,
        List<RadarProcessingSyntheticRebalanceMovePressure>? AcceptedMovePressures,
        RadarProcessingRebalanceRetentionStats RetentionStats,
        TimeSpan ProcessingElapsed,
        long ProcessingCallbackAllocatedBytes)
    {
        public static ArchiveIterationTelemetry Empty =>
            new(
                ExaminedFileCount: 0,
                SkippedFileCount: 0,
                PublishedFileCount: 0,
                FileSizeBytes: 0,
                CompressedRecordCount: 0,
                CompressedBytes: 0,
                DecompressedBytes: 0,
                BatchCount: 0,
                EventCount: 0,
                PayloadBytes: 0,
                PayloadValueCount: 0,
                RawValueChecksum: 0,
                TopologyVersionCount: 1,
                RebalanceEvaluationCount: 0,
                AcceptedMoveCount: 0,
                SkippedDecisionCount: 0,
                DirectHotReliefCount: 0,
                ColdEvacuationCount: 0,
                FailedMigrationCount: 0,
                ValidationSucceeded: true,
                ValidationChecksum: ChecksumInitial,
                SkippedReasons: null,
                AcceptedMovePressures: null,
                RetentionStats: new RadarProcessingRebalanceRetentionStats(),
                ProcessingElapsed: TimeSpan.Zero,
                ProcessingCallbackAllocatedBytes: 0);

        public static ArchiveIterationTelemetry FromMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount = 0) =>
            Empty.WithMetrics(metrics, topologyVersionCount) with
            {
                RebalanceEvaluationCount = rebalanceEvaluationCount,
                ValidationChecksum = ComputeChecksum(
                    metrics,
                    topologyVersionCount,
                    rebalanceEvaluationCount,
                    acceptedMoveCount: 0,
                    skippedDecisionCount: 0,
                    directHotReliefCount: 0,
                    coldEvacuationCount: 0,
                    failedMigrationCount: 0,
                    validationSucceeded: true)
            };

        public static ArchiveIterationTelemetry FromRebalanceSessionResult(
            RadarProcessingRebalanceSessionResult result)
        {
            List<RadarProcessingRebalanceSkippedReason>? skippedReasons = null;
            List<RadarProcessingSyntheticRebalanceMovePressure>? movePressures = null;
            var skippedDecisionCount = 0L;
            var acceptedMoveCount = 0L;
            var directHotReliefCount = 0L;
            var coldEvacuationCount = 0L;
            var failedMigrationCount = 0L;

            AddDecision(result.DirectHotReliefDecision, ref skippedReasons, ref skippedDecisionCount);
            AddDecision(result.ColdEvacuationDecision, ref skippedReasons, ref skippedDecisionCount);

            if (result.PublishedMigration)
            {
                acceptedMoveCount = 1;
                var decision = result.RebalanceDecision ??
                               throw new InvalidDataException("Published moves require a rebalance decision.");
                movePressures = new List<RadarProcessingSyntheticRebalanceMovePressure>(capacity: 1);
                movePressures.Add(CreateMovePressure(decision));
                if (decision.MoveKind == RadarProcessingRebalanceMoveKind.DirectHotRelief)
                {
                    directHotReliefCount = 1;
                }
                else if (decision.MoveKind == RadarProcessingRebalanceMoveKind.ColdEvacuation)
                {
                    coldEvacuationCount = 1;
                }
            }

            if (result.MigrationResult is not null && !result.MigrationResult.Succeeded)
            {
                failedMigrationCount = 1;
            }

            return Empty with
            {
                RebalanceEvaluationCount = result.EvaluatedRebalance ? 1 : 0,
                AcceptedMoveCount = acceptedMoveCount,
                SkippedDecisionCount = skippedDecisionCount,
                DirectHotReliefCount = directHotReliefCount,
                ColdEvacuationCount = coldEvacuationCount,
                FailedMigrationCount = failedMigrationCount,
                ValidationSucceeded = result.Validation.IsValid,
                SkippedReasons = skippedReasons,
                AcceptedMovePressures = movePressures
            };
        }

        public ArchiveIterationTelemetry Add(ArchiveIterationTelemetry other)
        {
            var skippedReasons = SkippedReasons;
            if (other.SkippedReasons is { Count: > 0 } otherSkippedReasons)
            {
                foreach (var reason in otherSkippedReasons)
                {
                    AddSkippedReason(ref skippedReasons, reason);
                }
            }

            var movePressures = AcceptedMovePressures;
            if (other.AcceptedMovePressures is { Count: > 0 } otherMovePressures)
            {
                movePressures ??= new List<RadarProcessingSyntheticRebalanceMovePressure>(
                    otherMovePressures.Count);
                movePressures.AddRange(otherMovePressures);
            }

            return this with
            {
                ExaminedFileCount = checked(ExaminedFileCount + other.ExaminedFileCount),
                SkippedFileCount = checked(SkippedFileCount + other.SkippedFileCount),
                PublishedFileCount = checked(PublishedFileCount + other.PublishedFileCount),
                FileSizeBytes = FileSizeBytes == 0 ? other.FileSizeBytes : FileSizeBytes,
                CompressedRecordCount = checked(CompressedRecordCount + other.CompressedRecordCount),
                CompressedBytes = checked(CompressedBytes + other.CompressedBytes),
                DecompressedBytes = checked(DecompressedBytes + other.DecompressedBytes),
                BatchCount = checked(BatchCount + other.BatchCount),
                EventCount = checked(EventCount + other.EventCount),
                PayloadBytes = checked(PayloadBytes + other.PayloadBytes),
                PayloadValueCount = checked(PayloadValueCount + other.PayloadValueCount),
                RawValueChecksum = checked(RawValueChecksum + other.RawValueChecksum),
                TopologyVersionCount = Math.Max(TopologyVersionCount, other.TopologyVersionCount),
                RebalanceEvaluationCount = checked(RebalanceEvaluationCount + other.RebalanceEvaluationCount),
                AcceptedMoveCount = checked(AcceptedMoveCount + other.AcceptedMoveCount),
                SkippedDecisionCount = checked(SkippedDecisionCount + other.SkippedDecisionCount),
                DirectHotReliefCount = checked(DirectHotReliefCount + other.DirectHotReliefCount),
                ColdEvacuationCount = checked(ColdEvacuationCount + other.ColdEvacuationCount),
                FailedMigrationCount = checked(FailedMigrationCount + other.FailedMigrationCount),
                ValidationSucceeded = ValidationSucceeded && other.ValidationSucceeded,
                ValidationChecksum = AppendUInt64(ValidationChecksum, other.ValidationChecksum),
                SkippedReasons = skippedReasons,
                AcceptedMovePressures = movePressures,
                RetentionStats = AddRetentionStats(RetentionStats, other.RetentionStats),
                ProcessingElapsed = ProcessingElapsed + other.ProcessingElapsed,
                ProcessingCallbackAllocatedBytes = checked(
                    ProcessingCallbackAllocatedBytes + other.ProcessingCallbackAllocatedBytes)
            };
        }

        public ArchiveIterationTelemetry WithPublishResult(
            RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = 1,
                SkippedFileCount = 0,
                PublishedFileCount = result.BatchCount > 0 ? 1 : 0,
                FileSizeBytes = result.FileSizeBytes,
                CompressedRecordCount = result.CompressedRecordCount,
                CompressedBytes = result.CompressedBytes,
                DecompressedBytes = result.DecompressedBytes,
                BatchCount = result.BatchCount,
                EventCount = result.EventCount,
                PayloadBytes = result.PayloadBytes,
                PayloadValueCount = result.PayloadValueCount,
                RawValueChecksum = result.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithRetentionStats(
            RadarProcessingRebalanceRetentionStats retentionStats)
        {
            ArgumentNullException.ThrowIfNull(retentionStats);

            return this with
            {
                RetentionStats = retentionStats
            };
        }

        public ArchiveIterationTelemetry WithPublishTotals(
            CacheIterationTotals totals,
            TimeSpan processingElapsed,
            long processingCallbackAllocatedBytes) =>
            this with
            {
                ExaminedFileCount = totals.ExaminedFileCount,
                SkippedFileCount = totals.SkippedFileCount,
                PublishedFileCount = totals.PublishedFileCount,
                FileSizeBytes = totals.FileSizeBytes,
                CompressedRecordCount = totals.CompressedRecordCount,
                CompressedBytes = totals.CompressedBytes,
                DecompressedBytes = totals.DecompressedBytes,
                BatchCount = totals.BatchCount,
                EventCount = totals.EventCount,
                PayloadBytes = totals.PayloadBytes,
                PayloadValueCount = totals.PayloadValueCount,
                RawValueChecksum = totals.RawValueChecksum,
                ProcessingElapsed = processingElapsed,
                ProcessingCallbackAllocatedBytes = processingCallbackAllocatedBytes
            };

        public ArchiveIterationTelemetry WithMetrics(
            RadarProcessingMetrics metrics,
            long topologyVersionCount)
        {
            var validationChecksum = ComputeChecksum(
                metrics,
                topologyVersionCount,
                RebalanceEvaluationCount,
                AcceptedMoveCount,
                SkippedDecisionCount,
                DirectHotReliefCount,
                ColdEvacuationCount,
                FailedMigrationCount,
                ValidationSucceeded);

            return this with
            {
                TopologyVersionCount = topologyVersionCount,
                ValidationChecksum = validationChecksum
            };
        }

        public bool HasSameStableTotals(ArchiveIterationTelemetry other) =>
            ExaminedFileCount == other.ExaminedFileCount &&
            SkippedFileCount == other.SkippedFileCount &&
            PublishedFileCount == other.PublishedFileCount &&
            FileSizeBytes == other.FileSizeBytes &&
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            BatchCount == other.BatchCount &&
            EventCount == other.EventCount &&
            PayloadBytes == other.PayloadBytes &&
            PayloadValueCount == other.PayloadValueCount &&
            RawValueChecksum == other.RawValueChecksum &&
            TopologyVersionCount == other.TopologyVersionCount &&
            RebalanceEvaluationCount == other.RebalanceEvaluationCount &&
            AcceptedMoveCount == other.AcceptedMoveCount &&
            SkippedDecisionCount == other.SkippedDecisionCount &&
            DirectHotReliefCount == other.DirectHotReliefCount &&
            ColdEvacuationCount == other.ColdEvacuationCount &&
            FailedMigrationCount == other.FailedMigrationCount &&
            ValidationSucceeded == other.ValidationSucceeded &&
            ValidationChecksum == other.ValidationChecksum &&
            HasSameRetentionStats(RetentionStats, other.RetentionStats);

        private static RadarProcessingRebalanceRetentionStats AddRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            new(
                Math.Max(current.RetainedDecisionCount, other.RetainedDecisionCount),
                checked(current.DroppedDecisionCount + other.DroppedDecisionCount),
                Math.Max(
                    current.RetainedLifecycleTransitionCount,
                    other.RetainedLifecycleTransitionCount),
                checked(current.DroppedLifecycleTransitionCount + other.DroppedLifecycleTransitionCount),
                Math.Max(current.RetainedAcceptedMoveCount, other.RetainedAcceptedMoveCount),
                checked(current.DroppedAcceptedMoveCount + other.DroppedAcceptedMoveCount),
                Math.Max(current.RetainedValidationFailureCount, other.RetainedValidationFailureCount),
                checked(current.DroppedValidationFailureCount + other.DroppedValidationFailureCount));

        private static bool HasSameRetentionStats(
            RadarProcessingRebalanceRetentionStats current,
            RadarProcessingRebalanceRetentionStats other) =>
            current.RetainedDecisionCount == other.RetainedDecisionCount &&
            current.DroppedDecisionCount == other.DroppedDecisionCount &&
            current.RetainedLifecycleTransitionCount == other.RetainedLifecycleTransitionCount &&
            current.DroppedLifecycleTransitionCount == other.DroppedLifecycleTransitionCount &&
            current.RetainedAcceptedMoveCount == other.RetainedAcceptedMoveCount &&
            current.DroppedAcceptedMoveCount == other.DroppedAcceptedMoveCount &&
            current.RetainedValidationFailureCount == other.RetainedValidationFailureCount &&
            current.DroppedValidationFailureCount == other.DroppedValidationFailureCount;

        private static void AddDecision(
            RadarProcessingRebalanceDecision? decision,
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            ref long skippedDecisionCount)
        {
            if (decision is null || decision.HasAcceptedMove)
            {
                return;
            }

            skippedDecisionCount = checked(skippedDecisionCount + 1);
            foreach (var reason in decision.SkippedReasons)
            {
                AddSkippedReason(ref skippedReasons, reason);
            }
        }

        private static void AddSkippedReason(
            ref List<RadarProcessingRebalanceSkippedReason>? skippedReasons,
            RadarProcessingRebalanceSkippedReason reason)
        {
            skippedReasons ??= new List<RadarProcessingRebalanceSkippedReason>();
            if (!skippedReasons.Contains(reason))
            {
                skippedReasons.Add(reason);
            }
        }

        private static RadarProcessingSyntheticRebalanceMovePressure CreateMovePressure(
            RadarProcessingRebalanceDecision decision) =>
            new(
                decision.MoveKind,
                decision.ProjectedPressure.SourceShardBefore.Value,
                decision.ProjectedPressure.TargetShardBefore.Value,
                decision.ProjectedPressure.SourceShardAfter.Value,
                decision.ProjectedPressure.TargetShardAfter.Value,
                decision.ExpectedRelief);

        private static ulong ComputeChecksum(
            RadarProcessingMetrics metrics,
            long topologyVersionCount,
            long rebalanceEvaluationCount,
            long acceptedMoveCount,
            long skippedDecisionCount,
            long directHotReliefCount,
            long coldEvacuationCount,
            long failedMigrationCount,
            bool validationSucceeded)
        {
            var checksum = ChecksumInitial;
            checksum = AppendInt64(checksum, metrics.ProcessedBatchCount);
            checksum = AppendInt64(checksum, metrics.ProcessedStreamEventCount);
            checksum = AppendInt64(checksum, metrics.ProcessedPayloadValueCount);
            checksum = AppendInt64(checksum, metrics.ActiveSourceCount);
            checksum = AppendInt64(checksum, metrics.RawValueChecksum);
            checksum = AppendUInt64(checksum, metrics.ProcessingChecksum);
            checksum = AppendInt64(checksum, topologyVersionCount);
            checksum = AppendInt64(checksum, rebalanceEvaluationCount);
            checksum = AppendInt64(checksum, acceptedMoveCount);
            checksum = AppendInt64(checksum, skippedDecisionCount);
            checksum = AppendInt64(checksum, directHotReliefCount);
            checksum = AppendInt64(checksum, coldEvacuationCount);
            checksum = AppendInt64(checksum, failedMigrationCount);
            return AppendInt32(checksum, validationSucceeded ? 1 : 0);
        }
    }

    private struct CacheIterationTotals
    {
        public static CacheIterationTotals Empty => new();

        public long ExaminedFileCount;
        public long SkippedFileCount;
        public long PublishedFileCount;
        public long FileSizeBytes;
        public long CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long BatchCount;
        public long EventCount;
        public long PayloadBytes;
        public long PayloadValueCount;
        public long RawValueChecksum;

        public void Add(RadarPulse.Domain.Archive.ArchiveRadarEventBatchPublishResult result)
        {
            PublishedFileCount = checked(PublishedFileCount + 1);
            FileSizeBytes = checked(FileSizeBytes + result.FileSizeBytes);
            CompressedRecordCount = checked(CompressedRecordCount + result.CompressedRecordCount);
            CompressedBytes = checked(CompressedBytes + result.CompressedBytes);
            DecompressedBytes = checked(DecompressedBytes + result.DecompressedBytes);
            BatchCount = checked(BatchCount + result.BatchCount);
            EventCount = checked(EventCount + result.EventCount);
            PayloadBytes = checked(PayloadBytes + result.PayloadBytes);
            PayloadValueCount = checked(PayloadValueCount + result.PayloadValueCount);
            RawValueChecksum = checked(RawValueChecksum + result.RawValueChecksum);
        }
    }
}

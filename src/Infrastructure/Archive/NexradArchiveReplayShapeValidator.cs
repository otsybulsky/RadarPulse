using System.Buffers;
using System.Buffers.Binary;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveReplayShapeValidator
{
    private const int ArchiveTwoVolumeHeaderLength = 24;
    private const int BZip2SignatureLength = 3;
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;

    public NexradArchiveReplayShapeValidator()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public NexradArchiveReplayShapeValidator(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    public ArchiveTwoReplayShapeValidationResult ValidateFile(
        string filePath,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");
        }

        ArchiveTwoReplayShapeValidationFileResult[] files = IsArchiveTwoBaseData(fileInfo)
            ? [ValidateArchiveTwoFile(fileInfo, degreeOfParallelism, cancellationToken)]
            : [];

        return new ArchiveTwoReplayShapeValidationResult(
            decompressor.Name,
            degreeOfParallelism,
            ExaminedFileCount: 1,
            SkippedFileCount: files.Length == 0 ? 1 : 0,
            files);
    }

    public ArchiveTwoReplayShapeValidationResult ValidateCache(
        string cachePath,
        string? radarId,
        int maxFiles,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        if (maxFiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Max files must be greater than zero.");
        }

        if (degreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");
        }

        var directoryInfo = new DirectoryInfo(cachePath);
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"NEXRAD cache directory was not found: {cachePath}");
        }

        var normalizedRadarId = string.IsNullOrWhiteSpace(radarId)
            ? null
            : HistoricalArchiveRequest.NormalizeRadarId(radarId);
        var files = new List<ArchiveTwoReplayShapeValidationFileResult>();
        var examinedFiles = 0;
        var skippedFiles = 0;

        foreach (var fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).OrderBy(file => file.FullName))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (files.Count >= maxFiles)
            {
                break;
            }

            if (!MatchesRadar(fileInfo, normalizedRadarId))
            {
                continue;
            }

            examinedFiles++;
            if (!IsArchiveTwoBaseData(fileInfo))
            {
                skippedFiles++;
                continue;
            }

            files.Add(ValidateArchiveTwoFile(fileInfo, degreeOfParallelism, cancellationToken));
        }

        return new ArchiveTwoReplayShapeValidationResult(
            decompressor.Name,
            degreeOfParallelism,
            examinedFiles,
            skippedFiles,
            files);
    }

    private ArchiveTwoReplayShapeValidationFileResult ValidateArchiveTwoFile(
        FileInfo fileInfo,
        int degreeOfParallelism,
        CancellationToken cancellationToken)
    {
        try
        {
            var sequential = AnalyzeArchiveTwoFile(fileInfo, cancellationToken);
            var parallel = new NexradArchiveReplayShapeBenchmark().Measure(
                fileInfo.FullName,
                iterations: 1,
                warmupIterations: 0,
                degreeOfParallelism,
                decompressor.Name,
                cancellationToken);
            var parallelMetrics = ToValidationMetrics(parallel);
            var diagnostic = CompareMetrics(sequential.Metrics, parallelMetrics);
            return new ArchiveTwoReplayShapeValidationFileResult(
                fileInfo.FullName,
                sequential.Metrics,
                parallelMetrics,
                sequential.RecordUnevenness,
                sequential.SweepUnevenness,
                diagnostic);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var emptyMetrics = EmptyMetrics();
            return new ArchiveTwoReplayShapeValidationFileResult(
                fileInfo.FullName,
                emptyMetrics,
                emptyMetrics,
                ArchiveTwoReplayShapeUnevennessSummary.Empty("record"),
                ArchiveTwoReplayShapeUnevennessSummary.Empty("sweep"),
                ex.Message);
        }
    }

    private ArchiveTwoReplayShapeAnalysis AnalyzeArchiveTwoFile(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ReadArchiveTwoVolumeHeader(fileInfo);
        var accumulator = new ArchiveTwoReplayShapeFlowAccumulator();
        var projector = new ArchiveTwoGateMomentEventProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            accumulator.AcceptEvent);
        var scanner = new ArchiveTwoMessageStreamScanner(projector);
        var decompressionSession = decompressor.CreateSession();
        var outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        byte[]? compressedPayloadBuffer = null;
        var controlWordBuffer = new byte[4];

        try
        {
            using var stream = File.OpenRead(fileInfo.FullName);
            stream.Position = ArchiveTwoVolumeHeaderLength;

            while (stream.Position < stream.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var controlWordOffset = stream.Position;
                var compressedSizeBytes = ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
                compressedPayloadBuffer = EnsureBufferCapacity(compressedPayloadBuffer, compressedSizeBytes);
                ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
                ValidateBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes), controlWordOffset);

                accumulator.AcceptCompressedRecord(compressedSizeBytes);
                scanner.Reset(accumulator.CompressedRecordCount);
                accumulator.AcceptDecompressedBytes(decompressionSession.Decompress(
                    compressedPayloadBuffer,
                    compressedSizeBytes,
                    outputBuffer,
                    scanner.Append));
                scanner.Complete();
            }

            return accumulator.Build();
        }
        finally
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }

    private static ArchiveTwoReplayShapeValidationMetrics ToValidationMetrics(
        ArchiveTwoReplayShapeBenchmarkResult result) =>
        new(
            result.CompressedRecordsPerIteration,
            result.CompressedBytesPerIteration,
            result.DecompressedBytesPerIteration,
            result.EventsPerIteration,
            result.ValidEventsPerIteration,
            result.BelowThresholdEventsPerIteration,
            result.RangeFoldedEventsPerIteration,
            result.ClutterFilterNotAppliedEventsPerIteration,
            result.PointClutterFilterAppliedEventsPerIteration,
            result.DualPolarizationFilteredEventsPerIteration,
            result.ReservedEventsPerIteration,
            result.UnsupportedEventsPerIteration,
            result.RawValueChecksumPerIteration,
            result.CalibratedValueScaledChecksumPerIteration,
            result.ChronologyChecksumPerIteration);

    private static ArchiveTwoReplayShapeValidationMetrics EmptyMetrics() =>
        new(
            CompressedRecordCount: 0,
            CompressedBytes: 0,
            DecompressedBytes: 0,
            Events: 0,
            ValidEvents: 0,
            BelowThresholdEvents: 0,
            RangeFoldedEvents: 0,
            ClutterFilterNotAppliedEvents: 0,
            PointClutterFilterAppliedEvents: 0,
            DualPolarizationFilteredEvents: 0,
            ReservedEvents: 0,
            UnsupportedEvents: 0,
            RawValueChecksum: 0,
            CalibratedValueScaledChecksum: 0,
            ChronologyChecksum: 0);

    private static string? CompareMetrics(
        ArchiveTwoReplayShapeValidationMetrics sequential,
        ArchiveTwoReplayShapeValidationMetrics parallel)
    {
        if (sequential.CompressedRecordCount != parallel.CompressedRecordCount)
        {
            return $"Compressed record count mismatch: sequential={sequential.CompressedRecordCount}, parallel={parallel.CompressedRecordCount}.";
        }

        if (sequential.CompressedBytes != parallel.CompressedBytes)
        {
            return $"Compressed byte count mismatch: sequential={sequential.CompressedBytes}, parallel={parallel.CompressedBytes}.";
        }

        if (sequential.DecompressedBytes != parallel.DecompressedBytes)
        {
            return $"Decompressed byte count mismatch: sequential={sequential.DecompressedBytes}, parallel={parallel.DecompressedBytes}.";
        }

        if (sequential.Events != parallel.Events)
        {
            return $"Event count mismatch: sequential={sequential.Events}, parallel={parallel.Events}.";
        }

        if (sequential.ValidEvents != parallel.ValidEvents)
        {
            return $"Valid event count mismatch: sequential={sequential.ValidEvents}, parallel={parallel.ValidEvents}.";
        }

        if (sequential.BelowThresholdEvents != parallel.BelowThresholdEvents ||
            sequential.RangeFoldedEvents != parallel.RangeFoldedEvents ||
            sequential.ClutterFilterNotAppliedEvents != parallel.ClutterFilterNotAppliedEvents ||
            sequential.PointClutterFilterAppliedEvents != parallel.PointClutterFilterAppliedEvents ||
            sequential.DualPolarizationFilteredEvents != parallel.DualPolarizationFilteredEvents ||
            sequential.ReservedEvents != parallel.ReservedEvents ||
            sequential.UnsupportedEvents != parallel.UnsupportedEvents)
        {
            return "Status-count mismatch between sequential and parallel replay-shape projection.";
        }

        if (sequential.RawValueChecksum != parallel.RawValueChecksum)
        {
            return $"Raw checksum mismatch: sequential={sequential.RawValueChecksum}, parallel={parallel.RawValueChecksum}.";
        }

        if (sequential.CalibratedValueScaledChecksum != parallel.CalibratedValueScaledChecksum)
        {
            return $"Calibrated checksum mismatch: sequential={sequential.CalibratedValueScaledChecksum}, parallel={parallel.CalibratedValueScaledChecksum}.";
        }

        if (sequential.ChronologyChecksum != parallel.ChronologyChecksum)
        {
            return $"Chronology checksum mismatch: sequential={sequential.ChronologyChecksum}, parallel={parallel.ChronologyChecksum}.";
        }

        return null;
    }

    private static ArchiveTwoVolumeHeader ReadArchiveTwoVolumeHeader(FileInfo fileInfo)
    {
        if (fileInfo.Length < ArchiveTwoVolumeHeaderLength)
        {
            throw new InvalidDataException("File is shorter than the 24-byte Archive Two volume header.");
        }

        Span<byte> header = stackalloc byte[ArchiveTwoVolumeHeaderLength];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, header);
        if (header[0] != (byte)'A' ||
            header[1] != (byte)'R' ||
            header[2] != (byte)'2' ||
            header[3] != (byte)'V')
        {
            throw new InvalidDataException("File does not start with an Archive Two volume header.");
        }

        var archiveFilename = System.Text.Encoding.ASCII.GetString(header[..12]);
        var version = System.Text.Encoding.ASCII.GetString(header.Slice(6, 2));
        var extensionText = System.Text.Encoding.ASCII.GetString(header.Slice(9, 3));
        if (!int.TryParse(extensionText, out var extensionNumber))
        {
            throw new FormatException($"Archive Two volume extension is not numeric: {extensionText}");
        }

        var nexradModifiedJulianDate = BinaryPrimitives.ReadInt32BigEndian(header.Slice(12, 4));
        var millisecondsPastMidnight = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var radarId = System.Text.Encoding.ASCII.GetString(header.Slice(20, 4)).TrimEnd('\0', ' ');
        var volumeDate = DateOnly.FromDayNumber(
            new DateOnly(1970, 1, 1).DayNumber + nexradModifiedJulianDate - 1);
        var volumeTime = TimeSpan.FromMilliseconds(millisecondsPastMidnight);

        return new ArchiveTwoVolumeHeader(
            archiveFilename,
            version,
            extensionNumber,
            volumeDate,
            volumeTime,
            new DateTimeOffset(volumeDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).Add(volumeTime),
            radarId);
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

    private static bool IsArchiveTwoBaseData(FileInfo fileInfo)
    {
        if (fileInfo.Length < 4)
        {
            return false;
        }

        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, signature);
        return signature[0] == (byte)'A' &&
            signature[1] == (byte)'R' &&
            signature[2] == (byte)'2' &&
            signature[3] == (byte)'V';
    }

    private static int ReadCompressedRecordSize(
        Stream stream,
        byte[] controlWordBuffer,
        long controlWordOffset)
    {
        var remainingBytes = stream.Length - stream.Position;
        if (remainingBytes < 4)
        {
            throw new InvalidDataException($"Trailing {remainingBytes} byte(s) after compressed records cannot contain a control word.");
        }

        ReadExactly(stream, controlWordBuffer);
        var controlWord = BinaryPrimitives.ReadInt32BigEndian(controlWordBuffer);
        if (controlWord == int.MinValue)
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} has an unsupported control word value.");
        }

        var compressedSizeBytes = Math.Abs(controlWord);
        if (compressedSizeBytes == 0)
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} has zero compressed bytes.");
        }

        if (compressedSizeBytes > stream.Length - stream.Position)
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} declares {compressedSizeBytes} bytes, but only {stream.Length - stream.Position} remain.");
        }

        return compressedSizeBytes;
    }

    private static byte[] EnsureBufferCapacity(byte[]? buffer, int requiredLength)
    {
        if (buffer is not null && buffer.Length >= requiredLength)
        {
            return buffer;
        }

        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return ArrayPool<byte>.Shared.Rent(requiredLength);
    }

    private static void ValidateBZip2Signature(ReadOnlySpan<byte> buffer, long controlWordOffset)
    {
        if (buffer.Length < BZip2SignatureLength ||
            buffer[0] != (byte)'B' ||
            buffer[1] != (byte)'Z' ||
            buffer[2] != (byte)'h')
        {
            throw new InvalidDataException($"Compressed record at offset {controlWordOffset} does not start with a BZip2 signature.");
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = stream.Read(buffer[totalBytesRead..]);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected end of NEXRAD archive file.");
            }

            totalBytesRead += bytesRead;
        }
    }

    private sealed record ArchiveTwoReplayShapeAnalysis(
        ArchiveTwoReplayShapeValidationMetrics Metrics,
        ArchiveTwoReplayShapeUnevennessSummary RecordUnevenness,
        ArchiveTwoReplayShapeUnevennessSummary SweepUnevenness);

    private sealed class ArchiveTwoReplayShapeFlowAccumulator
    {
        private readonly Dictionary<int, BucketAccumulator> recordBuckets = new();
        private readonly Dictionary<int, BucketAccumulator> sweepBuckets = new();
        private long events;
        private long validEvents;
        private long belowThresholdEvents;
        private long rangeFoldedEvents;
        private long clutterFilterNotAppliedEvents;
        private long pointClutterFilterAppliedEvents;
        private long dualPolarizationFilteredEvents;
        private long reservedEvents;
        private long unsupportedEvents;
        private long rawValueChecksum;
        private long calibratedValueScaledChecksum;
        private ulong chronologyChecksum;

        public int CompressedRecordCount { get; private set; }

        public long CompressedBytes { get; private set; }

        public long DecompressedBytes { get; private set; }

        public void AcceptCompressedRecord(int compressedSizeBytes)
        {
            CompressedRecordCount++;
            CompressedBytes += compressedSizeBytes;
        }

        public void AcceptDecompressedBytes(long decompressedBytes) =>
            DecompressedBytes += decompressedBytes;

        public void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            events++;
            rawValueChecksum += gateMomentEvent.RawValue;
            chronologyChecksum = ArchiveTwoGateMomentChronologyChecksum.Append(chronologyChecksum, gateMomentEvent);
            AcceptBucket(recordBuckets, gateMomentEvent.SourceOrder.CompressedRecordSequenceNumber, gateMomentEvent);
            AcceptBucket(sweepBuckets, gateMomentEvent.SweepSequenceNumber, gateMomentEvent);

            switch (gateMomentEvent.Status)
            {
                case ArchiveTwoGateMomentStatus.Valid:
                    validEvents++;
                    checked
                    {
                        calibratedValueScaledChecksum += (long)Math.Round(
                            gateMomentEvent.CalibratedValue!.Value * 1_000d,
                            MidpointRounding.AwayFromZero);
                    }

                    break;
                case ArchiveTwoGateMomentStatus.BelowThreshold:
                    belowThresholdEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.RangeFolded:
                    rangeFoldedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.ClutterFilterNotApplied:
                    clutterFilterNotAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.PointClutterFilterApplied:
                    pointClutterFilterAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.DualPolarizationFiltered:
                    dualPolarizationFilteredEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Reserved:
                    reservedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Unsupported:
                    unsupportedEvents++;
                    break;
            }
        }

        public ArchiveTwoReplayShapeAnalysis Build()
        {
            var metrics = new ArchiveTwoReplayShapeValidationMetrics(
                CompressedRecordCount,
                CompressedBytes,
                DecompressedBytes,
                events,
                validEvents,
                belowThresholdEvents,
                rangeFoldedEvents,
                clutterFilterNotAppliedEvents,
                pointClutterFilterAppliedEvents,
                dualPolarizationFilteredEvents,
                reservedEvents,
                unsupportedEvents,
                rawValueChecksum,
                calibratedValueScaledChecksum,
                chronologyChecksum);
            return new ArchiveTwoReplayShapeAnalysis(
                metrics,
                BuildUnevenness("record", recordBuckets.Values),
                BuildUnevenness("sweep", sweepBuckets.Values));
        }

        private static void AcceptBucket(
            Dictionary<int, BucketAccumulator> buckets,
            int bucketNumber,
            ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            if (!buckets.TryGetValue(bucketNumber, out var bucket))
            {
                bucket = new BucketAccumulator(bucketNumber);
                buckets.Add(bucketNumber, bucket);
            }

            bucket.Accept(gateMomentEvent);
        }

        private static ArchiveTwoReplayShapeUnevennessSummary BuildUnevenness(
            string bucketKind,
            IEnumerable<BucketAccumulator> bucketAccumulators)
        {
            var buckets = bucketAccumulators
                .Select(bucket => bucket.Build())
                .Where(bucket => bucket.Events > 0)
                .OrderBy(bucket => bucket.BucketNumber)
                .ToArray();
            if (buckets.Length == 0)
            {
                return ArchiveTwoReplayShapeUnevennessSummary.Empty(bucketKind);
            }

            return new ArchiveTwoReplayShapeUnevennessSummary(
                bucketKind,
                buckets.Length,
                buckets.MinBy(bucket => (bucket.ValidEventShare, bucket.BucketNumber))!,
                buckets.MaxBy(bucket => (bucket.ValidEventShare, -bucket.BucketNumber))!,
                buckets.MinBy(bucket => (bucket.ValidEvents, bucket.BucketNumber))!,
                buckets.MaxBy(bucket => (bucket.ValidEvents, -bucket.BucketNumber))!);
        }
    }

    private sealed class BucketAccumulator
    {
        private long events;
        private long validEvents;

        public BucketAccumulator(int bucketNumber)
        {
            BucketNumber = bucketNumber;
        }

        private int BucketNumber { get; }

        public void Accept(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            events++;
            if (gateMomentEvent.Status == ArchiveTwoGateMomentStatus.Valid)
            {
                validEvents++;
            }
        }

        public ArchiveTwoReplayShapeUnevennessBucket Build() =>
            new(BucketNumber, events, validEvents);
    }
}

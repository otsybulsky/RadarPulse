using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveReplayPublishSession : IDisposable
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

    public int DegreeOfParallelism { get; }

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

    private ArchiveReplayPublishResult PublishFileSequential(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var worker = workers[0];
        var controlWordBuffer = new byte[4];
        totalAccumulator.Reset();
        worker.ResetProjection(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            totalAccumulator.AcceptEvent,
            default);

        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        using var stream = File.OpenRead(fileInfo.FullName);
        stream.Position = ArchiveTwoFileReader.VolumeHeaderLength;

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controlWordOffset = stream.Position;
            var compressedSizeBytes = ArchiveTwoFileReader.ReadCompressedRecordSize(
                stream,
                controlWordBuffer,
                controlWordOffset);
            var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(compressedSizeBytes);
            ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
            ArchiveTwoFileReader.ValidateBZip2Signature(
                compressedPayloadBuffer.AsSpan(0, compressedSizeBytes),
                controlWordOffset);

            compressedRecordCount++;
            compressedBytes += compressedSizeBytes;
            decompressedBytes += worker.ProjectRecordContinuing(
                compressedPayloadBuffer,
                compressedSizeBytes,
                compressedRecordCount);
        }

        return totalAccumulator.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            DegreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
    }

    private ArchiveReplayPublishResult PublishFileParallel(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        EnsureRecordCapacity(records.Count);

        using var fileHandle = File.OpenHandle(
            fileInfo.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.RandomAccess);
        var availableWorkers = new ConcurrentStack<ArchiveReplaySessionWorker>(workers);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = DegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        RunParallelRecordPass(
            records,
            fileHandle,
            availableWorkers,
            parallelOptions,
            (worker, record, compressedPayloadBuffer) =>
            {
                metadataByRecord[record.Index] = worker.ReadRecordMetadata(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    record.Index + 1);
            });

        BuildStartingProjectorStates(metadataByRecord, startingStatesByRecord, records.Count);

        RunParallelRecordPass(
            records,
            fileHandle,
            availableWorkers,
            parallelOptions,
            (worker, record, compressedPayloadBuffer) =>
            {
                var accumulator = accumulatorsByRecord[record.Index];
                accumulator.Reset();
                worker.ResetProjection(
                    volumeHeader.RadarId,
                    volumeHeader.VolumeTimestamp,
                    accumulator.AcceptEvent,
                    startingStatesByRecord[record.Index]);
                var decompressedBytes = worker.ProjectRecordContinuing(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    record.Index + 1);
                compressedRecordCountsByRecord[record.Index] = 1;
                compressedBytesByRecord[record.Index] = record.CompressedSizeBytes;
                decompressedBytesByRecord[record.Index] = decompressedBytes;
            });

        return BuildOrderedResult(fileInfo, records.Count);
    }

    private void EnsureRecordCapacity(int recordCount)
    {
        if (metadataByRecord.Length < recordCount)
        {
            Array.Resize(ref metadataByRecord, recordCount);
            Array.Resize(ref startingStatesByRecord, recordCount);
            Array.Resize(ref accumulatorsByRecord, recordCount);
            Array.Resize(ref compressedRecordCountsByRecord, recordCount);
            Array.Resize(ref compressedBytesByRecord, recordCount);
            Array.Resize(ref decompressedBytesByRecord, recordCount);
        }

        for (var i = 0; i < recordCount; i++)
        {
            accumulatorsByRecord[i] ??= new ArchiveReplayEventAccumulator();
            compressedRecordCountsByRecord[i] = 0;
            compressedBytesByRecord[i] = 0;
            decompressedBytesByRecord[i] = 0;
        }
    }

    private static void RunParallelRecordPass(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveReplaySessionWorker> availableWorkers,
        ParallelOptions options,
        Action<ArchiveReplaySessionWorker, ArchiveTwoCompressedRecordDescriptor, byte[]> processRecord)
    {
        try
        {
            Parallel.For(
                0,
                records.Count,
                options,
                recordIndex =>
                {
                    options.CancellationToken.ThrowIfCancellationRequested();
                    var record = records[recordIndex];
                    if (!availableWorkers.TryPop(out var worker))
                    {
                        throw new InvalidOperationException("No archive replay worker was available.");
                    }

                    try
                    {
                        var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(record.CompressedSizeBytes);
                        ArchiveTwoFileReader.ReadExactly(
                            fileHandle,
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.PayloadOffset);
                        ArchiveTwoFileReader.ValidateBZip2Signature(
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.ControlWordOffset);

                        processRecord(worker, record, compressedPayloadBuffer);
                    }
                    finally
                    {
                        availableWorkers.Push(worker);
                    }
                });
        }
        catch (AggregateException ex)
        {
            ThrowSingleInnerExceptionWhenUseful(ex);
            throw;
        }
    }

    private ArchiveReplayPublishResult BuildOrderedResult(FileInfo fileInfo, int recordCount)
    {
        totalAccumulator.Reset();
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        for (var i = 0; i < recordCount; i++)
        {
            compressedRecordCount += compressedRecordCountsByRecord[i];
            compressedBytes += compressedBytesByRecord[i];
            decompressedBytes += decompressedBytesByRecord[i];
            totalAccumulator.AddOrdered(accumulatorsByRecord[i]);
        }

        return totalAccumulator.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            DegreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
    }

    private static void BuildStartingProjectorStates(
        IReadOnlyList<ArchiveReplayRecordMetadata> metadataByRecord,
        Span<ArchiveTwoGateMomentProjectorState> states,
        int recordCount)
    {
        var state = default(ArchiveTwoGateMomentProjectorState);
        for (var i = 0; i < recordCount; i++)
        {
            states[i] = state;
            foreach (var radial in metadataByRecord[i].Radials)
            {
                state = ArchiveTwoGateMomentEventProjector.AdvanceState(
                    state,
                    radial.RadialStatus,
                    radial.ElevationNumber,
                    out _);
            }
        }
    }

    private static void ThrowSingleInnerExceptionWhenUseful(AggregateException exception)
    {
        var flattened = exception.Flatten();
        if (flattened.InnerExceptions.Count != 1)
        {
            return;
        }

        var innerException = flattened.InnerExceptions[0];
        if (innerException is InvalidDataException or IOException)
        {
            ExceptionDispatchInfo.Capture(innerException).Throw();
        }
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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var worker in workers)
        {
            worker.Dispose();
        }
    }

    private sealed record ArchiveReplayRecordMetadata(
        IReadOnlyList<ArchiveReplayRadialMetadata> Radials);

    private readonly record struct ArchiveReplayRadialMetadata(
        int RadialStatus,
        int ElevationNumber);

    private sealed class ArchiveReplayRecordMetadataCollector : IArchiveTwoMessageConsumer
    {
        private const int MessageHeaderLength = 16;
        private const int Type31DataHeaderMinimumLength = 72;
        private readonly List<ArchiveReplayRadialMetadata> radials = new();

        public void Reset() => radials.Clear();

        public ArchiveReplayRecordMetadata Build() => new(radials.ToArray());

        public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
        {
            if (message.Length < MessageHeaderLength || message[3] != 31)
            {
                return;
            }

            var payload = message[MessageHeaderLength..];
            if (payload.Length < Type31DataHeaderMinimumLength)
            {
                return;
            }

            radials.Add(new ArchiveReplayRadialMetadata(
                payload[21],
                payload[22]));
        }
    }

    private sealed class ArchiveReplaySessionWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private readonly ArchiveReplayRecordMetadataCollector metadataCollector = new();
        private readonly ArchiveTwoMessageStreamScanner metadataScanner;
        private readonly ArchiveTwoGateMomentEventProjector projector;
        private readonly ArchiveTwoMessageStreamScanner projectorScanner;
        private byte[]? compressedPayloadBuffer;
        private bool disposed;

        public ArchiveReplaySessionWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            this.decompressionSession = decompressionSession;
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            metadataScanner = new ArchiveTwoMessageStreamScanner(metadataCollector);
            projector = new ArchiveTwoGateMomentEventProjector("INIT", DateTimeOffset.UnixEpoch, AcceptEvent);
            projectorScanner = new ArchiveTwoMessageStreamScanner(projector);
        }

        private Action<ArchiveTwoGateMomentEvent> acceptEvent = _ => { };

        public ArchiveReplayRecordMetadata ReadRecordMetadata(
            byte[] compressedPayloadBuffer,
            int compressedSizeBytes,
            int sourceRecordSequenceNumber)
        {
            metadataCollector.Reset();
            metadataScanner.Reset(sourceRecordSequenceNumber);
            decompressionSession.Decompress(
                compressedPayloadBuffer,
                compressedSizeBytes,
                outputBuffer,
                metadataScanner.Append);
            metadataScanner.Complete();
            return metadataCollector.Build();
        }

        public void ResetProjection(
            string radarId,
            DateTimeOffset volumeTimestamp,
            Action<ArchiveTwoGateMomentEvent> acceptEvent,
            ArchiveTwoGateMomentProjectorState projectorState)
        {
            this.acceptEvent = acceptEvent ?? throw new ArgumentNullException(nameof(acceptEvent));
            projector.Reset(radarId, volumeTimestamp, AcceptEvent, projectorState);
        }

        public long ProjectRecordContinuing(
            byte[] compressedPayloadBuffer,
            int compressedSizeBytes,
            int sourceRecordSequenceNumber)
        {
            projectorScanner.Reset(sourceRecordSequenceNumber);
            var decompressedBytes = decompressionSession.Decompress(
                compressedPayloadBuffer,
                compressedSizeBytes,
                outputBuffer,
                projectorScanner.Append);
            projectorScanner.Complete();
            return decompressedBytes;
        }

        public byte[] EnsureCompressedPayloadBuffer(int requiredLength)
        {
            if (compressedPayloadBuffer is not null && compressedPayloadBuffer.Length >= requiredLength)
            {
                return compressedPayloadBuffer;
            }

            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            compressedPayloadBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);
            return compressedPayloadBuffer;
        }

        private void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent) => acceptEvent(gateMomentEvent);

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}

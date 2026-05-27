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
public sealed class NexradArchiveRadarEventBatchPublisher
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

    private ArchiveRadarEventBatchPublishResult PublishFileSequential(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = new ArchiveTwoRadarEventBatchProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            options.SourceUniverse,
            EstimateInitialEventCapacity(
                compressedRecordCount: null,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
        var scanner = new ArchiveTwoMessageStreamScanner(projector);
        var decompressionSession = decompressor.CreateSession();
        var outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        byte[]? compressedPayloadBuffer = null;
        var controlWordBuffer = new byte[4];
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        try
        {
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
                compressedPayloadBuffer = ArchiveTwoFileReader.EnsurePooledBufferCapacity(
                    compressedPayloadBuffer,
                    compressedSizeBytes);
                ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
                ArchiveTwoFileReader.ValidateBZip2Signature(
                    compressedPayloadBuffer.AsSpan(0, compressedSizeBytes),
                    controlWordOffset);

                compressedRecordCount++;
                compressedBytes += compressedSizeBytes;
                scanner.Reset(compressedRecordCount);
                decompressedBytes += decompressionSession.Decompress(
                    compressedPayloadBuffer,
                    compressedSizeBytes,
                    outputBuffer,
                    scanner.Append);
                scanner.Complete();
            }

            var batch = projector.BuildBatch();
            if (batch.EventCount > 0)
            {
                countingPublisher.Publish(batch, cancellationToken);
            }

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes,
                projector.DictionarySnapshot);
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

    private ArchiveRadarEventBatchPublishResult PublishFileParallel(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        ArchiveRadarEventBatchPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = new ArchiveTwoRadarEventBatchProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            options.SourceUniverse,
            EstimateInitialEventCapacity(
                records.Count,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
        var scanner = new ArchiveTwoMessageStreamScanner(projector);
        var workers = CreateWorkers(options.DegreeOfParallelism);
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        var inFlight = new Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>>();
        var availableWorkers = new ConcurrentStack<ArchiveRadarEventBatchWorker>(workers);

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var nextRecordToSchedule = 0;

            while (nextRecordToSchedule < records.Count &&
                inFlight.Count < options.DegreeOfParallelism)
            {
                ScheduleDecompressedRecord(
                    records,
                    fileHandle,
                    availableWorkers,
                    inFlight,
                    nextRecordToSchedule,
                    cancellationToken);
                nextRecordToSchedule++;
            }

            for (var recordIndex = 0; recordIndex < records.Count; recordIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var recordResult = inFlight[recordIndex].GetAwaiter().GetResult();
                inFlight.Remove(recordIndex);

                try
                {
                    compressedRecordCount += recordResult.CompressedRecordCount;
                    compressedBytes += recordResult.CompressedBytes;
                    decompressedBytes += recordResult.DecompressedBytes;
                    scanner.Reset(recordIndex + 1);
                    scanner.Append(recordResult.DecompressedPayload);
                    scanner.Complete();
                }
                finally
                {
                    recordResult.Dispose();
                    availableWorkers.Push(recordResult.Worker);
                }

                if (nextRecordToSchedule < records.Count)
                {
                    ScheduleDecompressedRecord(
                        records,
                        fileHandle,
                        availableWorkers,
                        inFlight,
                        nextRecordToSchedule,
                        cancellationToken);
                    nextRecordToSchedule++;
                }
            }

            var batch = projector.BuildBatch();
            if (batch.EventCount > 0)
            {
                countingPublisher.Publish(batch, cancellationToken);
            }

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes,
                projector.DictionarySnapshot);
        }
        finally
        {
            WaitForInFlightTasks(inFlight.Values, availableWorkers);
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }

    private IReadOnlyList<ArchiveRadarEventBatchWorker> CreateWorkers(int degreeOfParallelism)
    {
        var workers = new ArchiveRadarEventBatchWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveRadarEventBatchWorker(decompressor.CreateSession());
        }

        return workers;
    }

    private static int EstimateInitialPayloadCapacity(FileInfo fileInfo)
    {
        var estimated = fileInfo.Length * EstimatedPayloadBytesPerCompressedByte;
        if (estimated <= 0)
        {
            return DefaultInitialPayloadCapacity;
        }

        estimated = Math.Max(estimated, DefaultInitialPayloadCapacity);
        return checked((int)Math.Min(estimated, MaxInitialPayloadCapacity));
    }

    private static int EstimateInitialEventCapacity(
        int? compressedRecordCount,
        int rangeBandCount,
        int initialPayloadCapacity)
    {
        long estimated = Math.Max(
            DefaultInitialEventCapacity,
            initialPayloadCapacity / EstimatedPayloadBytesPerStreamEvent);
        if (compressedRecordCount is { } recordCount)
        {
            estimated = Math.Max(
                estimated,
                (long)recordCount * Math.Max(rangeBandCount, 1) * EstimatedEventsPerCompressedRecord);
        }

        return checked((int)Math.Min(estimated, MaxInitialEventCapacity));
    }

    private static void ScheduleDecompressedRecord(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveRadarEventBatchWorker> availableWorkers,
        Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>> inFlight,
        int recordIndex,
        CancellationToken cancellationToken)
    {
        var record = records[recordIndex];
        inFlight.Add(
            recordIndex,
            Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!availableWorkers.TryPop(out var worker))
                    {
                        throw new InvalidOperationException("No archive radar event batch worker was available.");
                    }

                    try
                    {
                        return worker.DecompressRecord(record, fileHandle);
                    }
                    catch
                    {
                        availableWorkers.Push(worker);
                        throw;
                    }
                },
                cancellationToken));
    }

    private static void WaitForInFlightTasks(
        IEnumerable<Task<ArchiveRadarEventBatchDecompressedRecord>> tasks,
        ConcurrentStack<ArchiveRadarEventBatchWorker> availableWorkers)
    {
        foreach (var task in tasks.ToArray())
        {
            try
            {
                var record = task.GetAwaiter().GetResult();
                record.Dispose();
                availableWorkers.Push(record.Worker);
            }
            catch
            {
                // The ordered drain path observes the original task exception.
            }
        }
    }

    private static FileInfo GetExistingFileInfo(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        return fileInfo;
    }

    private sealed class ArchiveRadarEventBatchWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private byte[]? compressedPayloadBuffer;
        private byte[] decompressedPayloadBuffer;
        private int decompressedPayloadLength;
        private bool disposed;

        public ArchiveRadarEventBatchWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            this.decompressionSession = decompressionSession ?? throw new ArgumentNullException(nameof(decompressionSession));
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            decompressedPayloadBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        }

        public ArchiveRadarEventBatchDecompressedRecord DecompressRecord(
            ArchiveTwoCompressedRecordDescriptor record,
            SafeFileHandle fileHandle)
        {
            var compressedPayloadBuffer = EnsureCompressedPayloadBuffer(record.CompressedSizeBytes);
            ArchiveTwoFileReader.ReadExactly(
                fileHandle,
                compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                record.PayloadOffset);
            ArchiveTwoFileReader.ValidateBZip2Signature(
                compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                record.ControlWordOffset);

            decompressedPayloadLength = 0;
            try
            {
                var decompressedBytes = decompressionSession.Decompress(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    outputBuffer,
                    AppendDecompressedChunk);
                if (decompressedBytes != decompressedPayloadLength)
                {
                    throw new InvalidDataException("Decompressed byte count does not match the buffered payload length.");
                }

                return new ArchiveRadarEventBatchDecompressedRecord(
                    this,
                    compressedRecordCount: 1,
                    record.CompressedSizeBytes,
                    decompressedBytes,
                    decompressedPayloadBuffer.AsMemory(0, decompressedPayloadLength));
            }
            catch
            {
                decompressedPayloadLength = 0;
                throw;
            }
        }

        public void CompleteDecompressedRecord() => decompressedPayloadLength = 0;

        private byte[] EnsureCompressedPayloadBuffer(int requiredLength)
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

        private void AppendDecompressedChunk(ReadOnlySpan<byte> chunk)
        {
            EnsureDecompressedPayloadCapacity(checked(decompressedPayloadLength + chunk.Length));
            chunk.CopyTo(decompressedPayloadBuffer.AsSpan(decompressedPayloadLength));
            decompressedPayloadLength += chunk.Length;
        }

        private void EnsureDecompressedPayloadCapacity(int requiredLength)
        {
            if (decompressedPayloadBuffer.Length >= requiredLength)
            {
                return;
            }

            var newLength = decompressedPayloadBuffer.Length;
            while (newLength < requiredLength)
            {
                newLength = checked(newLength * 2);
            }

            var newBuffer = ArrayPool<byte>.Shared.Rent(newLength);
            decompressedPayloadBuffer.AsSpan(0, decompressedPayloadLength).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(decompressedPayloadBuffer);
            decompressedPayloadBuffer = newBuffer;
        }

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

            ArrayPool<byte>.Shared.Return(decompressedPayloadBuffer);
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }

    private sealed class ArchiveRadarEventBatchDecompressedRecord : IDisposable
    {
        private ReadOnlyMemory<byte> decompressedPayload;
        private bool disposed;

        public ArchiveRadarEventBatchDecompressedRecord(
            ArchiveRadarEventBatchWorker worker,
            int compressedRecordCount,
            long compressedBytes,
            long decompressedBytes,
            ReadOnlyMemory<byte> decompressedPayload)
        {
            Worker = worker ?? throw new ArgumentNullException(nameof(worker));
            CompressedRecordCount = compressedRecordCount;
            CompressedBytes = compressedBytes;
            DecompressedBytes = decompressedBytes;
            this.decompressedPayload = decompressedPayload;
        }

        public ArchiveRadarEventBatchWorker Worker { get; }

        public int CompressedRecordCount { get; }

        public long CompressedBytes { get; }

        public long DecompressedBytes { get; }

        public ReadOnlySpan<byte> DecompressedPayload =>
            disposed
                ? throw new ObjectDisposedException(nameof(ArchiveRadarEventBatchDecompressedRecord))
                : decompressedPayload.Span;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            decompressedPayload = default;
            Worker.CompleteDecompressedRecord();
        }
    }
}

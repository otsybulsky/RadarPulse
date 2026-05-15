using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveRadarEventBatchPublishSession : IDisposable
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
    private readonly ArchiveRadarEventBatchPublishOptions options;
    private readonly ArchiveRadarEventBatchWorker[] workers;
    private readonly ConcurrentStack<ArchiveRadarEventBatchWorker> availableWorkers = new();
    private readonly Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>> inFlight = new();
    private readonly byte[] controlWordBuffer = new byte[4];
    private ArchiveTwoRadarEventBatchProjector? projector;
    private ArchiveTwoMessageStreamScanner? scanner;
    private bool disposed;

    public NexradArchiveRadarEventBatchPublishSession(
        IArchiveBZip2Decompressor decompressor,
        ArchiveRadarEventBatchPublishOptions options)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        workers = new ArchiveRadarEventBatchWorker[options.DegreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveRadarEventBatchWorker(decompressor.CreateSession());
        }
    }

    public int DegreeOfParallelism => options.DegreeOfParallelism;

    public ArchiveRadarEventBatchPublishResult PublishFile(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return PublishFile(
            filePath,
            new ArchiveRadarEventBatchCountingPublisher(),
            cancellationToken);
    }

    public ArchiveRadarEventBatchPublishResult PublishFile(
        string filePath,
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(publisher);

        var fileInfo = GetExistingFileInfo(filePath);
        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, publisher, cancellationToken)
            : PublishFileParallel(fileInfo, publisher, cancellationToken);
    }

    private ArchiveRadarEventBatchPublishResult PublishFileSequential(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = PrepareProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            EstimateInitialEventCapacity(
                compressedRecordCount: null,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
        var scanner = this.scanner ?? throw new InvalidOperationException("Archive stream scanner was not initialized.");
        var worker = workers[0];
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
            scanner.Reset(compressedRecordCount);
            decompressedBytes += worker.DecompressTo(
                compressedPayloadBuffer,
                compressedSizeBytes,
                scanner.Append);
            scanner.Complete();
        }

        projector.PublishLeasedBatch(countingPublisher, cancellationToken);

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

    private ArchiveRadarEventBatchPublishResult PublishFileParallel(
        FileInfo fileInfo,
        IArchiveRadarEventBatchPublisher publisher,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var countingPublisher = publisher as ArchiveRadarEventBatchCountingPublisher ??
            new ArchiveRadarEventBatchCountingPublisher(publisher);
        var initialPayloadCapacity = EstimateInitialPayloadCapacity(fileInfo);
        var projector = PrepareProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            EstimateInitialEventCapacity(
                records.Count,
                options.SourceUniverse.RangeBandCount,
                initialPayloadCapacity),
            initialPayloadCapacity);
        var scanner = this.scanner ?? throw new InvalidOperationException("Archive stream scanner was not initialized.");
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;

        inFlight.Clear();
        ResetAvailableWorkers();
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
                        nextRecordToSchedule,
                        cancellationToken);
                    nextRecordToSchedule++;
                }
            }

            projector.PublishLeasedBatch(countingPublisher, cancellationToken);

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
            WaitForInFlightTasks();
            inFlight.Clear();
        }
    }

    private ArchiveTwoRadarEventBatchProjector PrepareProjector(
        string radarId,
        DateTimeOffset volumeTimestamp,
        int initialEventCapacity,
        int initialPayloadCapacity)
    {
        if (projector is null)
        {
            projector = new ArchiveTwoRadarEventBatchProjector(
                radarId,
                volumeTimestamp,
                options.SourceUniverse,
                initialEventCapacity,
                initialPayloadCapacity);
            scanner = new ArchiveTwoMessageStreamScanner(projector);
            return projector;
        }

        projector.ResetVolume(
            radarId,
            volumeTimestamp,
            initialEventCapacity,
            initialPayloadCapacity);
        return projector;
    }

    private void ResetAvailableWorkers()
    {
        availableWorkers.Clear();
        for (var i = 0; i < workers.Length; i++)
        {
            availableWorkers.Push(workers[i]);
        }
    }

    private void ScheduleDecompressedRecord(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
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

    private void WaitForInFlightTasks()
    {
        foreach (var task in inFlight.Values.ToArray())
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

    private static FileInfo GetExistingFileInfo(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("NEXRAD archive file was not found.", filePath);
        }

        return fileInfo;
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

        public long DecompressTo(
            byte[] compressedPayloadBuffer,
            int compressedSizeBytes,
            ArchiveBZip2DecompressedChunkHandler append)
        {
            return decompressionSession.Decompress(
                compressedPayloadBuffer,
                compressedSizeBytes,
                outputBuffer,
                append);
        }

        public void CompleteDecompressedRecord() => decompressedPayloadLength = 0;

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

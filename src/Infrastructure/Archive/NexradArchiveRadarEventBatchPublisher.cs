using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveRadarEventBatchPublisher
{
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;

    public NexradArchiveRadarEventBatchPublisher()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public NexradArchiveRadarEventBatchPublisher(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

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
        var projector = new ArchiveTwoRadarEventBatchProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            options.SourceUniverse);
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
        var projector = new ArchiveTwoRadarEventBatchProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            options.SourceUniverse);
        var scanner = new ArchiveTwoMessageStreamScanner(projector);
        var workers = CreateWorkers(options.DegreeOfParallelism);
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        var inFlight = new Dictionary<int, Task<ArchiveRadarEventBatchDecompressedRecord>>();

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var availableWorkers = new ConcurrentStack<ArchiveRadarEventBatchWorker>(workers);
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
                using var recordResult = inFlight[recordIndex].GetAwaiter().GetResult();
                inFlight.Remove(recordIndex);

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

                compressedRecordCount += recordResult.CompressedRecordCount;
                compressedBytes += recordResult.CompressedBytes;
                decompressedBytes += recordResult.DecompressedBytes;
                scanner.Reset(recordIndex + 1);
                scanner.Append(recordResult.DecompressedPayload);
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
            WaitForInFlightTasks(inFlight.Values);
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
                    finally
                    {
                        availableWorkers.Push(worker);
                    }
                },
                cancellationToken));
    }

    private static void WaitForInFlightTasks(IEnumerable<Task<ArchiveRadarEventBatchDecompressedRecord>> tasks)
    {
        foreach (var task in tasks.ToArray())
        {
            try
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result.Dispose();
                    continue;
                }

                task.GetAwaiter().GetResult().Dispose();
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
        private bool disposed;

        public ArchiveRadarEventBatchWorker(IArchiveBZip2DecompressionSession decompressionSession)
        {
            this.decompressionSession = decompressionSession ?? throw new ArgumentNullException(nameof(decompressionSession));
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
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

            var decompressedRecord = new ArchiveRadarEventBatchDecompressedRecord(
                compressedRecordCount: 1,
                record.CompressedSizeBytes);
            try
            {
                var decompressedBytes = decompressionSession.Decompress(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    outputBuffer,
                    decompressedRecord.Append);
                decompressedRecord.SetDecompressedBytes(decompressedBytes);
                return decompressedRecord;
            }
            catch
            {
                decompressedRecord.Dispose();
                throw;
            }
        }

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

    private sealed class ArchiveRadarEventBatchDecompressedRecord : IDisposable
    {
        private byte[]? buffer;

        public ArchiveRadarEventBatchDecompressedRecord(
            int compressedRecordCount,
            long compressedBytes)
        {
            CompressedRecordCount = compressedRecordCount;
            CompressedBytes = compressedBytes;
            buffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
        }

        public int CompressedRecordCount { get; }

        public long CompressedBytes { get; }

        public long DecompressedBytes { get; private set; }

        public int DecompressedPayloadLength { get; private set; }

        public ReadOnlySpan<byte> DecompressedPayload =>
            buffer is null
                ? throw new ObjectDisposedException(nameof(ArchiveRadarEventBatchDecompressedRecord))
                : buffer.AsSpan(0, DecompressedPayloadLength);

        public void Append(ReadOnlySpan<byte> chunk)
        {
            EnsureCapacity(checked(DecompressedPayloadLength + chunk.Length));
            chunk.CopyTo(buffer!.AsSpan(DecompressedPayloadLength));
            DecompressedPayloadLength += chunk.Length;
        }

        public void SetDecompressedBytes(long decompressedBytes)
        {
            if (decompressedBytes != DecompressedPayloadLength)
            {
                throw new InvalidDataException("Decompressed byte count does not match the buffered payload length.");
            }

            DecompressedBytes = decompressedBytes;
        }

        private void EnsureCapacity(int requiredLength)
        {
            if (buffer!.Length >= requiredLength)
            {
                return;
            }

            var newLength = buffer.Length;
            while (newLength < requiredLength)
            {
                newLength = checked(newLength * 2);
            }

            var newBuffer = ArrayPool<byte>.Shared.Rent(newLength);
            buffer.AsSpan(0, DecompressedPayloadLength).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = newBuffer;
        }

        public void Dispose()
        {
            if (buffer is null)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(buffer);
            buffer = null;
        }
    }
}

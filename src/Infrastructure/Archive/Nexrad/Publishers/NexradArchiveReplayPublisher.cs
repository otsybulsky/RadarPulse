using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveReplayPublisher
{
    private const int OutputBufferSize = 81920;

    private readonly IArchiveBZip2Decompressor decompressor;

    public NexradArchiveReplayPublisher()
        : this(ArchiveBZip2Decompressors.Create(ArchiveBZip2Decompressors.DefaultName))
    {
    }

    public NexradArchiveReplayPublisher(IArchiveBZip2Decompressor decompressor)
    {
        this.decompressor = decompressor ?? throw new ArgumentNullException(nameof(decompressor));
    }

    public ArchiveReplayPublishResult PublishFile(
        string filePath,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var fileInfo = GetExistingFileInfo(filePath);

        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, new ArchiveReplayCountingPublisher(), options, cancellationToken)
            : PublishFileParallelCounting(fileInfo, options, cancellationToken);
    }

    public ArchiveReplayPublishResult PublishFile(
        string filePath,
        IArchiveReplayEventPublisher publisher,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var fileInfo = GetExistingFileInfo(filePath);

        return options.DegreeOfParallelism == 1
            ? PublishFileSequential(fileInfo, publisher, options, cancellationToken)
            : PublishFileParallelBuffered(fileInfo, publisher, options, cancellationToken);
    }

    private ArchiveReplayPublishResult PublishFileSequential(
        FileInfo fileInfo,
        IArchiveReplayEventPublisher publisher,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var countingPublisher = publisher as ArchiveReplayCountingPublisher ?? new ArchiveReplayCountingPublisher(publisher);
        var projector = new ArchiveTwoGateMomentEventProjector(
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp,
            gateMomentEvent => countingPublisher.Publish(gateMomentEvent, cancellationToken));
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

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes);
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

    private ArchiveReplayPublishResult PublishFileParallelCounting(
        FileInfo fileInfo,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var metadataByRecord = new ArchiveReplayRecordMetadata[records.Count];
        var measurementsByRecord = new ArchiveReplayRecordMeasurement[records.Count];
        var workers = CreateWorkers(options.DegreeOfParallelism, volumeHeader.RadarId, volumeHeader.VolumeTimestamp);

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var availableWorkers = new ConcurrentStack<ArchiveReplayWorker>(workers);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.DegreeOfParallelism,
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

            var startingStatesByRecord = BuildStartingProjectorStates(metadataByRecord);

            RunParallelRecordPass(
                records,
                fileHandle,
                availableWorkers,
                parallelOptions,
                (worker, record, compressedPayloadBuffer) =>
                {
                    var accumulator = new ArchiveReplayEventAccumulator();
                    worker.ResetProjection(accumulator.AcceptEvent, startingStatesByRecord[record.Index]);
                    var decompressedBytes = worker.ProjectRecordContinuing(
                        compressedPayloadBuffer,
                        record.CompressedSizeBytes,
                        record.Index + 1);
                    measurementsByRecord[record.Index] = new ArchiveReplayRecordMeasurement(
                        CompressedRecordCount: 1,
                        record.CompressedSizeBytes,
                        decompressedBytes,
                        accumulator);
                });

            return BuildOrderedResult(
                fileInfo,
                options.DegreeOfParallelism,
                measurementsByRecord);
        }
        finally
        {
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }

    private ArchiveReplayPublishResult PublishFileParallelBuffered(
        FileInfo fileInfo,
        IArchiveReplayEventPublisher publisher,
        ArchiveReplayPublishOptions options,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
        var records = ArchiveTwoFileReader.ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var metadataByRecord = new ArchiveReplayRecordMetadata[records.Count];
        var workers = CreateWorkers(options.DegreeOfParallelism, volumeHeader.RadarId, volumeHeader.VolumeTimestamp);
        var countingPublisher = publisher as ArchiveReplayCountingPublisher ?? new ArchiveReplayCountingPublisher(publisher);
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        var inFlight = new Dictionary<int, Task<ArchiveReplayBufferedRecord>>();

        try
        {
            using var fileHandle = File.OpenHandle(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.RandomAccess);
            var availableWorkers = new ConcurrentStack<ArchiveReplayWorker>(workers);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.DegreeOfParallelism,
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

            var startingStatesByRecord = BuildStartingProjectorStates(metadataByRecord);
            var nextRecordToSchedule = 0;
            while (nextRecordToSchedule < records.Count &&
                inFlight.Count < options.DegreeOfParallelism)
            {
                ScheduleBufferedRecord(
                    records,
                    startingStatesByRecord,
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

                if (nextRecordToSchedule < records.Count)
                {
                    ScheduleBufferedRecord(
                        records,
                        startingStatesByRecord,
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
                foreach (var gateMomentEvent in recordResult.Events)
                {
                    countingPublisher.Publish(gateMomentEvent, cancellationToken);
                }
            }

            return countingPublisher.BuildResult(
                fileInfo.FullName,
                decompressor.Name,
                options.DegreeOfParallelism,
                fileInfo.Length,
                compressedRecordCount,
                compressedBytes,
                decompressedBytes);
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

    private static void ValidateOptions(ArchiveReplayPublishOptions options)
    {
        if (options.DegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ArchiveReplayPublishOptions.DegreeOfParallelism),
                "Degree of parallelism must be greater than zero.");
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

    private IReadOnlyList<ArchiveReplayWorker> CreateWorkers(
        int degreeOfParallelism,
        string radarId,
        DateTimeOffset volumeTimestamp)
    {
        var workers = new ArchiveReplayWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveReplayWorker(
                decompressor.CreateSession(),
                radarId,
                volumeTimestamp);
        }

        return workers;
    }

    private static void RunParallelRecordPass(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveReplayWorker> availableWorkers,
        ParallelOptions options,
        Action<ArchiveReplayWorker, ArchiveTwoCompressedRecordDescriptor, byte[]> processRecord)
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

    private static void ScheduleBufferedRecord(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        IReadOnlyList<ArchiveTwoGateMomentProjectorState> startingStatesByRecord,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveReplayWorker> availableWorkers,
        Dictionary<int, Task<ArchiveReplayBufferedRecord>> inFlight,
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

                        var events = new List<ArchiveTwoGateMomentEvent>();
                        worker.ResetProjection(events.Add, startingStatesByRecord[record.Index]);
                        var decompressedBytes = worker.ProjectRecordContinuing(
                            compressedPayloadBuffer,
                            record.CompressedSizeBytes,
                            record.Index + 1);
                        return new ArchiveReplayBufferedRecord(
                            CompressedRecordCount: 1,
                            record.CompressedSizeBytes,
                            decompressedBytes,
                            events);
                    }
                    finally
                    {
                        availableWorkers.Push(worker);
                    }
                },
                cancellationToken));
    }

    private ArchiveReplayPublishResult BuildOrderedResult(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveReplayRecordMeasurement> measurementsByRecord)
    {
        var accumulator = new ArchiveReplayEventAccumulator();
        var compressedRecordCount = 0;
        long compressedBytes = 0;
        long decompressedBytes = 0;
        foreach (var measurement in measurementsByRecord)
        {
            compressedRecordCount += measurement.CompressedRecordCount;
            compressedBytes += measurement.CompressedBytes;
            decompressedBytes += measurement.DecompressedBytes;
            accumulator.AddOrdered(measurement.Accumulator);
        }

        return accumulator.BuildResult(
            fileInfo.FullName,
            decompressor.Name,
            degreeOfParallelism,
            fileInfo.Length,
            compressedRecordCount,
            compressedBytes,
            decompressedBytes);
    }

    private static ArchiveTwoGateMomentProjectorState[] BuildStartingProjectorStates(
        IReadOnlyList<ArchiveReplayRecordMetadata> metadataByRecord)
    {
        var states = new ArchiveTwoGateMomentProjectorState[metadataByRecord.Count];
        var state = default(ArchiveTwoGateMomentProjectorState);
        for (var i = 0; i < metadataByRecord.Count; i++)
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

        return states;
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

    private static void WaitForInFlightTasks(IEnumerable<Task<ArchiveReplayBufferedRecord>> tasks)
    {
        try
        {
            Task.WaitAll(tasks.ToArray());
        }
        catch
        {
            // The original task exception is observed by the ordered drain path.
        }
    }

    private sealed record ArchiveReplayRecordMetadata(
        IReadOnlyList<ArchiveReplayRadialMetadata> Radials);

    private readonly record struct ArchiveReplayRadialMetadata(
        int RadialStatus,
        int ElevationNumber);

    private sealed record ArchiveReplayRecordMeasurement(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        ArchiveReplayEventAccumulator Accumulator);

    private sealed record ArchiveReplayBufferedRecord(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        IReadOnlyList<ArchiveTwoGateMomentEvent> Events);

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

    private sealed class ArchiveReplayWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private readonly ArchiveReplayRecordMetadataCollector metadataCollector = new();
        private readonly ArchiveTwoMessageStreamScanner metadataScanner;
        private readonly ArchiveTwoGateMomentEventProjector projector;
        private readonly ArchiveTwoMessageStreamScanner projectorScanner;
        private byte[]? compressedPayloadBuffer;

        public ArchiveReplayWorker(
            IArchiveBZip2DecompressionSession decompressionSession,
            string radarId,
            DateTimeOffset volumeTimestamp)
        {
            this.decompressionSession = decompressionSession;
            outputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            metadataScanner = new ArchiveTwoMessageStreamScanner(metadataCollector);
            projector = new ArchiveTwoGateMomentEventProjector(radarId, volumeTimestamp, AcceptEvent);
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
            Action<ArchiveTwoGateMomentEvent> acceptEvent,
            ArchiveTwoGateMomentProjectorState projectorState)
        {
            this.acceptEvent = acceptEvent;
            projector.Reset(projectorState);
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
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}

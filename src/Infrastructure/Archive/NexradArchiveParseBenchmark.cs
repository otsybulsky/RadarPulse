using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveParseBenchmark
{
    private const int ArchiveTwoVolumeHeaderLength = 24;
    private const int BZip2SignatureLength = 3;
    private const int OutputBufferSize = 81920;

    public ArchiveTwoParseBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            degreeOfParallelism,
            decompressorName,
            decodeMomentValues: false,
            cancellationToken);

    public ArchiveTwoParseBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
        bool decodeMomentValues,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var decompressor = ArchiveBZip2Decompressors.Create(decompressorName);
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

        ValidateArchiveTwoSignature(fileInfo);
        var workers = CreateWorkers(decompressor, degreeOfParallelism, decodeMomentValues);
        try
        {
            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
            }

            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ArchiveTwoParseIterationMeasurement? expectedIteration = null;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationResult = MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
                expectedIteration ??= iterationResult;

                if (expectedIteration.Value != iterationResult)
                {
                    throw new InvalidDataException("Archive parse benchmark produced inconsistent iteration totals.");
                }
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
            var measurement = expectedIteration ?? default;

            return new ArchiveTwoParseBenchmarkResult(
                filePath,
                decompressor.Name,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                decodeMomentValues,
                fileInfo.Length,
                measurement.CompressedRecordCount,
                measurement.CompressedBytes,
                measurement.DecompressedBytes,
                measurement.MessageCount,
                measurement.Type31RadialCount,
                measurement.EstimatedGateMomentEvents,
                measurement.DecodedGateMomentValues,
                measurement.DecodedGateMomentValueChecksum,
                stopwatch.Elapsed,
                allocatedBytes);
        }
        finally
        {
            foreach (var worker in workers)
            {
                worker.Dispose();
            }
        }
    }

    private static void ValidateArchiveTwoSignature(FileInfo fileInfo)
    {
        if (fileInfo.Length < ArchiveTwoVolumeHeaderLength)
        {
            throw new InvalidDataException("File is shorter than the 24-byte Archive Two volume header.");
        }

        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(fileInfo.FullName);
        ReadExactly(stream, signature);
        if (signature[0] != (byte)'A' ||
            signature[1] != (byte)'R' ||
            signature[2] != (byte)'2' ||
            signature[3] != (byte)'V')
        {
            throw new InvalidDataException("File does not start with an Archive Two volume header.");
        }
    }

    private static ArchiveTwoParseIterationMeasurement MeasureIteration(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoParseBenchmarkWorker> workers,
        CancellationToken cancellationToken) =>
        degreeOfParallelism == 1
            ? MeasureIterationSequential(fileInfo, workers[0], cancellationToken)
            : MeasureIterationParallel(fileInfo, degreeOfParallelism, workers, cancellationToken);

    private static ArchiveTwoParseIterationMeasurement MeasureIterationSequential(
        FileInfo fileInfo,
        ArchiveTwoParseBenchmarkWorker worker,
        CancellationToken cancellationToken)
    {
        var controlWordBuffer = new byte[4];
        var measurement = new ArchiveTwoParseIterationMeasurement();

        using var stream = File.OpenRead(fileInfo.FullName);
        stream.Position = ArchiveTwoVolumeHeaderLength;

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controlWordOffset = stream.Position;
            var compressedSizeBytes = ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
            var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(compressedSizeBytes);
            ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
            ValidateBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes), controlWordOffset);

            measurement += worker.MeasureRecord(compressedPayloadBuffer, compressedSizeBytes);
        }

        return measurement;
    }

    private static ArchiveTwoParseIterationMeasurement MeasureIterationParallel(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoParseBenchmarkWorker> workers,
        CancellationToken cancellationToken)
    {
        var records = ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var measurementsByRecord = new ArchiveTwoParseIterationMeasurement[records.Count];
        var availableWorkers = new ConcurrentStack<ArchiveTwoParseBenchmarkWorker>(workers);

        using var fileHandle = File.OpenHandle(
            fileInfo.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.RandomAccess);

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degreeOfParallelism,
            CancellationToken = cancellationToken
        };

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
                        throw new InvalidOperationException("No archive parse benchmark worker was available.");
                    }

                    try
                    {
                        var compressedPayloadBuffer = worker.EnsureCompressedPayloadBuffer(record.CompressedSizeBytes);
                        ReadExactly(
                            fileHandle,
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.PayloadOffset);
                        ValidateBZip2Signature(
                            compressedPayloadBuffer.AsSpan(0, record.CompressedSizeBytes),
                            record.ControlWordOffset);

                        measurementsByRecord[record.Index] = worker.MeasureRecord(
                            compressedPayloadBuffer,
                            record.CompressedSizeBytes);
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

        var total = new ArchiveTwoParseIterationMeasurement();
        for (var i = 0; i < measurementsByRecord.Length; i++)
        {
            total += measurementsByRecord[i];
        }

        return total;
    }

    private static IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> ReadCompressedRecordDescriptors(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var records = new List<ArchiveTwoCompressedRecordDescriptor>();
        var controlWordBuffer = new byte[4];
        Span<byte> signature = stackalloc byte[BZip2SignatureLength];

        using var stream = File.OpenRead(fileInfo.FullName);
        stream.Position = ArchiveTwoVolumeHeaderLength;

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controlWordOffset = stream.Position;
            var compressedSizeBytes = ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
            if (compressedSizeBytes < BZip2SignatureLength)
            {
                throw new InvalidDataException($"Compressed record at offset {controlWordOffset} is too short to contain a BZip2 signature.");
            }

            var payloadOffset = stream.Position;
            ReadExactly(stream, signature);
            ValidateBZip2Signature(signature, controlWordOffset);
            stream.Position += compressedSizeBytes - BZip2SignatureLength;

            records.Add(new ArchiveTwoCompressedRecordDescriptor(
                records.Count,
                controlWordOffset,
                payloadOffset,
                compressedSizeBytes));
        }

        return records;
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

    private static IReadOnlyList<ArchiveTwoParseBenchmarkWorker> CreateWorkers(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism,
        bool decodeMomentValues)
    {
        var workers = new ArchiveTwoParseBenchmarkWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveTwoParseBenchmarkWorker(decompressor.CreateSession(), decodeMomentValues);
        }

        return workers;
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

    private static void ReadExactly(SafeFileHandle fileHandle, Span<byte> buffer, long fileOffset)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = RandomAccess.Read(fileHandle, buffer[totalBytesRead..], fileOffset + totalBytesRead);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected end of NEXRAD archive file.");
            }

            totalBytesRead += bytesRead;
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

    private readonly record struct ArchiveTwoCompressedRecordDescriptor(
        int Index,
        long ControlWordOffset,
        long PayloadOffset,
        int CompressedSizeBytes);

    private readonly record struct ArchiveTwoParseIterationMeasurement(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        int MessageCount,
        int Type31RadialCount,
        long EstimatedGateMomentEvents,
        long DecodedGateMomentValues,
        ulong DecodedGateMomentValueChecksum)
    {
        public static ArchiveTwoParseIterationMeasurement operator +(
            ArchiveTwoParseIterationMeasurement left,
            ArchiveTwoParseIterationMeasurement right)
        {
            unchecked
            {
                return new ArchiveTwoParseIterationMeasurement(
                    left.CompressedRecordCount + right.CompressedRecordCount,
                    left.CompressedBytes + right.CompressedBytes,
                    left.DecompressedBytes + right.DecompressedBytes,
                    left.MessageCount + right.MessageCount,
                    left.Type31RadialCount + right.Type31RadialCount,
                    left.EstimatedGateMomentEvents + right.EstimatedGateMomentEvents,
                    left.DecodedGateMomentValues + right.DecodedGateMomentValues,
                    left.DecodedGateMomentValueChecksum + right.DecodedGateMomentValueChecksum);
            }
        }
    }

    private sealed class ArchiveTwoParseBenchmarkWorker : IDisposable
    {
        private readonly ArchiveTwoMessageSummaryBuilder messageSummaryBuilder;
        private readonly ArchiveTwoMessageStreamScanner messageScanner;
        private byte[]? compressedPayloadBuffer;

        public ArchiveTwoParseBenchmarkWorker(
            IArchiveBZip2DecompressionSession decompressionSession,
            bool decodeMomentValues)
        {
            DecompressionSession = decompressionSession;
            OutputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            messageSummaryBuilder = new ArchiveTwoMessageSummaryBuilder(
                decodeMomentValues,
                collectSweepSummaries: false);
            messageScanner = new ArchiveTwoMessageStreamScanner(messageSummaryBuilder);
        }

        public IArchiveBZip2DecompressionSession DecompressionSession { get; }

        public byte[] OutputBuffer { get; }

        public ArchiveTwoParseIterationMeasurement MeasureRecord(byte[] compressedPayloadBuffer, int compressedSizeBytes)
        {
            messageSummaryBuilder.Reset();
            messageScanner.Reset();
            var decompressedBytes = DecompressionSession.Decompress(
                compressedPayloadBuffer,
                compressedSizeBytes,
                OutputBuffer,
                messageScanner.Append);
            messageScanner.Complete();

            return new ArchiveTwoParseIterationMeasurement(
                CompressedRecordCount: 1,
                CompressedBytes: compressedSizeBytes,
                DecompressedBytes: decompressedBytes,
                MessageCount: messageSummaryBuilder.MessageCount,
                Type31RadialCount: messageSummaryBuilder.Type31RadialCount,
                EstimatedGateMomentEvents: messageSummaryBuilder.EstimatedGateMomentEventCount,
                DecodedGateMomentValues: messageSummaryBuilder.DecodedGateMomentValueCount,
                DecodedGateMomentValueChecksum: messageSummaryBuilder.DecodedGateMomentValueChecksum);
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

        public void Dispose()
        {
            if (compressedPayloadBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(compressedPayloadBuffer);
            }

            ArrayPool<byte>.Shared.Return(OutputBuffer);
        }
    }
}

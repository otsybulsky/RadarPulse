using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class NexradArchiveReplayShapeBenchmark
{
    private const int ArchiveTwoVolumeHeaderLength = 24;
    private const int BZip2SignatureLength = 3;
    private const int OutputBufferSize = 81920;
    private const ulong ChronologyChecksumPrime = 1_099_511_628_211UL;
    private const ulong ChronologyChecksumOffset = 14_695_981_039_346_656_037UL;

    public ArchiveTwoReplayShapeBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        string decompressorName,
        CancellationToken cancellationToken) =>
        Measure(
            filePath,
            iterations,
            warmupIterations,
            degreeOfParallelism: 1,
            decompressorName,
            cancellationToken);

    public ArchiveTwoReplayShapeBenchmarkResult Measure(
        string filePath,
        int iterations,
        int warmupIterations,
        int degreeOfParallelism,
        string decompressorName,
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

        var volumeHeader = ReadArchiveTwoVolumeHeader(fileInfo);
        var workers = CreateWorkers(
            decompressor,
            degreeOfParallelism,
            volumeHeader.RadarId,
            volumeHeader.VolumeTimestamp);
        try
        {
            for (var warmupIteration = 0; warmupIteration < warmupIterations; warmupIteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
            }

            var allocatedBytesBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ArchiveTwoReplayShapeIterationMeasurement? expectedIteration = null;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var iterationResult = MeasureIteration(fileInfo, degreeOfParallelism, workers, cancellationToken);
                if (expectedIteration is null)
                {
                    expectedIteration = iterationResult;
                }
                else if (!expectedIteration.HasSameTotals(iterationResult))
                {
                    throw new InvalidDataException("Replay-shape benchmark produced inconsistent iteration totals.");
                }
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBytesBefore;
            var measurement = expectedIteration ?? throw new InvalidOperationException("Replay-shape benchmark did not run any iterations.");

            return new ArchiveTwoReplayShapeBenchmarkResult(
                filePath,
                decompressor.Name,
                iterations,
                warmupIterations,
                degreeOfParallelism,
                fileInfo.Length,
                measurement.CompressedRecordCount,
                measurement.CompressedBytes,
                measurement.DecompressedBytes,
                measurement.Events,
                measurement.ValidEvents,
                measurement.BelowThresholdEvents,
                measurement.RangeFoldedEvents,
                measurement.ClutterFilterNotAppliedEvents,
                measurement.PointClutterFilterAppliedEvents,
                measurement.DualPolarizationFilteredEvents,
                measurement.ReservedEvents,
                measurement.UnsupportedEvents,
                measurement.RawValueChecksum,
                measurement.CalibratedValueScaledChecksum,
                measurement.ChronologyChecksum,
                measurement.MinimumCalibratedValue,
                measurement.MaximumCalibratedValue,
                measurement.MinimumRangeKilometers,
                measurement.MaximumRangeKilometers,
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

    private static IReadOnlyList<ArchiveTwoReplayShapeBenchmarkWorker> CreateWorkers(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism,
        string radarId,
        DateTimeOffset volumeTimestamp)
    {
        var workers = new ArchiveTwoReplayShapeBenchmarkWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveTwoReplayShapeBenchmarkWorker(
                decompressor.CreateSession(),
                radarId,
                volumeTimestamp);
        }

        return workers;
    }

    private static ArchiveTwoReplayShapeIterationMeasurement MeasureIteration(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoReplayShapeBenchmarkWorker> workers,
        CancellationToken cancellationToken) =>
        degreeOfParallelism == 1
            ? MeasureIterationSequential(fileInfo, workers[0], cancellationToken)
            : MeasureIterationParallel(fileInfo, degreeOfParallelism, workers, cancellationToken);

    private static ArchiveTwoReplayShapeIterationMeasurement MeasureIterationSequential(
        FileInfo fileInfo,
        ArchiveTwoReplayShapeBenchmarkWorker worker,
        CancellationToken cancellationToken)
    {
        var measurement = new ArchiveTwoReplayShapeIterationMeasurement();
        worker.ResetProjection(measurement.AcceptEvent, default);

        var controlWordBuffer = new byte[4];
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

            measurement.CompressedRecordCount++;
            measurement.CompressedBytes += compressedSizeBytes;
            measurement.DecompressedBytes += worker.ProjectRecordContinuing(
                compressedPayloadBuffer,
                compressedSizeBytes,
                measurement.CompressedRecordCount);
        }

        return measurement;
    }

    private static ArchiveTwoReplayShapeIterationMeasurement MeasureIterationParallel(
        FileInfo fileInfo,
        int degreeOfParallelism,
        IReadOnlyList<ArchiveTwoReplayShapeBenchmarkWorker> workers,
        CancellationToken cancellationToken)
    {
        var records = ReadCompressedRecordDescriptors(fileInfo, cancellationToken);
        var metadataByRecord = new ArchiveTwoReplayShapeRecordMetadata[records.Count];
        var measurementsByRecord = new ArchiveTwoReplayShapeIterationMeasurement[records.Count];
        var availableWorkers = new ConcurrentStack<ArchiveTwoReplayShapeBenchmarkWorker>(workers);

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

        RunParallelRecordPass(
            records,
            fileHandle,
            availableWorkers,
            options,
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
            options,
            (worker, record, compressedPayloadBuffer) =>
            {
                var measurement = new ArchiveTwoReplayShapeIterationMeasurement
                {
                    CompressedRecordCount = 1,
                    CompressedBytes = record.CompressedSizeBytes
                };
                worker.ResetProjection(measurement.AcceptEvent, startingStatesByRecord[record.Index]);
                measurement.DecompressedBytes = worker.ProjectRecordContinuing(
                    compressedPayloadBuffer,
                    record.CompressedSizeBytes,
                    record.Index + 1);
                measurementsByRecord[record.Index] = measurement;
            });

        var total = new ArchiveTwoReplayShapeIterationMeasurement();
        for (var i = 0; i < measurementsByRecord.Length; i++)
        {
            total.AddOrdered(measurementsByRecord[i]);
        }

        return total;
    }

    private static void RunParallelRecordPass(
        IReadOnlyList<ArchiveTwoCompressedRecordDescriptor> records,
        SafeFileHandle fileHandle,
        ConcurrentStack<ArchiveTwoReplayShapeBenchmarkWorker> availableWorkers,
        ParallelOptions options,
        Action<ArchiveTwoReplayShapeBenchmarkWorker, ArchiveTwoCompressedRecordDescriptor, byte[]> processRecord)
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
                        throw new InvalidOperationException("No replay-shape benchmark worker was available.");
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

    private static ArchiveTwoGateMomentProjectorState[] BuildStartingProjectorStates(
        IReadOnlyList<ArchiveTwoReplayShapeRecordMetadata> metadataByRecord)
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

        var archiveFilename = Encoding.ASCII.GetString(header[..12]);
        var version = Encoding.ASCII.GetString(header.Slice(6, 2));
        var extensionText = Encoding.ASCII.GetString(header.Slice(9, 3));
        if (!int.TryParse(extensionText, out var extensionNumber))
        {
            throw new FormatException($"Archive Two volume extension is not numeric: {extensionText}");
        }

        var nexradModifiedJulianDate = BinaryPrimitives.ReadInt32BigEndian(header.Slice(12, 4));
        var millisecondsPastMidnight = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        var radarId = Encoding.ASCII.GetString(header.Slice(20, 4)).TrimEnd('\0', ' ');
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

    private static ulong CombineChronologyChecksums(
        ulong left,
        ulong right,
        long rightEventCount)
    {
        unchecked
        {
            return left * PowUnchecked(ChronologyChecksumPrime, rightEventCount) + right;
        }
    }

    private static ulong PowUnchecked(ulong value, long exponent)
    {
        var result = 1UL;
        var factor = value;
        var remaining = exponent;
        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                unchecked
                {
                    result *= factor;
                }
            }

            remaining >>= 1;
            unchecked
            {
                factor *= factor;
            }
        }

        return result;
    }

    private static ulong HashEventChronology(ArchiveTwoGateMomentEvent gateMomentEvent)
    {
        var hash = ChronologyChecksumOffset;
        hash = Mix(hash, gateMomentEvent.SourceOrder.CompressedRecordSequenceNumber);
        hash = Mix(hash, gateMomentEvent.SourceOrder.MessageSequenceNumberInRecord);
        hash = Mix(hash, gateMomentEvent.SourceOrder.Type31RadialSequenceNumber);
        hash = Mix(hash, gateMomentEvent.SweepSequenceNumber);
        hash = Mix(hash, gateMomentEvent.ElevationNumber);
        hash = Mix(hash, gateMomentEvent.RadialSequenceNumber);
        hash = Mix(hash, gateMomentEvent.GateIndex);
        hash = Mix(hash, BitConverter.SingleToInt32Bits(gateMomentEvent.RangeKilometers));
        hash = Mix(hash, gateMomentEvent.MomentName);
        hash = Mix(hash, gateMomentEvent.RawValue);
        hash = Mix(hash, (int)gateMomentEvent.Status);
        if (gateMomentEvent.CalibratedValue is { } calibratedValue)
        {
            hash = Mix(hash, (long)Math.Round(calibratedValue * 1_000d, MidpointRounding.AwayFromZero));
        }

        return hash;
    }

    private static ulong Mix(ulong hash, int value) => Mix(hash, (long)value);

    private static ulong Mix(ulong hash, long value)
    {
        unchecked
        {
            return (hash ^ (ulong)value) * ChronologyChecksumPrime;
        }
    }

    private static ulong Mix(ulong hash, string value)
    {
        foreach (var character in value)
        {
            hash = Mix(hash, character);
        }

        return hash;
    }

    private readonly record struct ArchiveTwoCompressedRecordDescriptor(
        int Index,
        long ControlWordOffset,
        long PayloadOffset,
        int CompressedSizeBytes);

    private sealed record ArchiveTwoReplayShapeRecordMetadata(
        IReadOnlyList<ArchiveTwoReplayShapeRadialMetadata> Radials);

    private readonly record struct ArchiveTwoReplayShapeRadialMetadata(
        int RadialStatus,
        int ElevationNumber);

    private sealed class ArchiveTwoReplayShapeRecordMetadataCollector : IArchiveTwoMessageConsumer
    {
        private const int MessageHeaderLength = 16;
        private const int Type31DataHeaderMinimumLength = 72;
        private readonly List<ArchiveTwoReplayShapeRadialMetadata> radials = new();

        public void Reset() => radials.Clear();

        public ArchiveTwoReplayShapeRecordMetadata Build() =>
            new(radials.ToArray());

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

            radials.Add(new ArchiveTwoReplayShapeRadialMetadata(
                payload[21],
                payload[22]));
        }
    }

    private sealed class ArchiveTwoReplayShapeIterationMeasurement
    {
        public int CompressedRecordCount;
        public long CompressedBytes;
        public long DecompressedBytes;
        public long Events;
        public long ValidEvents;
        public long BelowThresholdEvents;
        public long RangeFoldedEvents;
        public long ClutterFilterNotAppliedEvents;
        public long PointClutterFilterAppliedEvents;
        public long DualPolarizationFilteredEvents;
        public long ReservedEvents;
        public long UnsupportedEvents;
        public long RawValueChecksum;
        public long CalibratedValueScaledChecksum;
        public ulong ChronologyChecksum;
        public double MinimumCalibratedValue;
        public double MaximumCalibratedValue;
        public double MinimumRangeKilometers;
        public double MaximumRangeKilometers;

        public bool HasSameTotals(ArchiveTwoReplayShapeIterationMeasurement other) =>
            CompressedRecordCount == other.CompressedRecordCount &&
            CompressedBytes == other.CompressedBytes &&
            DecompressedBytes == other.DecompressedBytes &&
            Events == other.Events &&
            ValidEvents == other.ValidEvents &&
            BelowThresholdEvents == other.BelowThresholdEvents &&
            RangeFoldedEvents == other.RangeFoldedEvents &&
            ClutterFilterNotAppliedEvents == other.ClutterFilterNotAppliedEvents &&
            PointClutterFilterAppliedEvents == other.PointClutterFilterAppliedEvents &&
            DualPolarizationFilteredEvents == other.DualPolarizationFilteredEvents &&
            ReservedEvents == other.ReservedEvents &&
            UnsupportedEvents == other.UnsupportedEvents &&
            RawValueChecksum == other.RawValueChecksum &&
            CalibratedValueScaledChecksum == other.CalibratedValueScaledChecksum &&
            ChronologyChecksum == other.ChronologyChecksum &&
            MinimumCalibratedValue.Equals(other.MinimumCalibratedValue) &&
            MaximumCalibratedValue.Equals(other.MaximumCalibratedValue) &&
            MinimumRangeKilometers.Equals(other.MinimumRangeKilometers) &&
            MaximumRangeKilometers.Equals(other.MaximumRangeKilometers);

        public void AddOrdered(ArchiveTwoReplayShapeIterationMeasurement other)
        {
            if (other.Events == 0)
            {
                CompressedRecordCount += other.CompressedRecordCount;
                CompressedBytes += other.CompressedBytes;
                DecompressedBytes += other.DecompressedBytes;
                return;
            }

            var hadEvents = Events > 0;
            var hadValidEvents = ValidEvents > 0;

            CompressedRecordCount += other.CompressedRecordCount;
            CompressedBytes += other.CompressedBytes;
            DecompressedBytes += other.DecompressedBytes;
            RawValueChecksum += other.RawValueChecksum;
            CalibratedValueScaledChecksum += other.CalibratedValueScaledChecksum;
            ChronologyChecksum = CombineChronologyChecksums(
                ChronologyChecksum,
                other.ChronologyChecksum,
                other.Events);

            if (!hadEvents)
            {
                MinimumRangeKilometers = other.MinimumRangeKilometers;
                MaximumRangeKilometers = other.MaximumRangeKilometers;
            }
            else
            {
                MinimumRangeKilometers = Math.Min(MinimumRangeKilometers, other.MinimumRangeKilometers);
                MaximumRangeKilometers = Math.Max(MaximumRangeKilometers, other.MaximumRangeKilometers);
            }

            if (other.ValidEvents > 0)
            {
                if (!hadValidEvents)
                {
                    MinimumCalibratedValue = other.MinimumCalibratedValue;
                    MaximumCalibratedValue = other.MaximumCalibratedValue;
                }
                else
                {
                    MinimumCalibratedValue = Math.Min(MinimumCalibratedValue, other.MinimumCalibratedValue);
                    MaximumCalibratedValue = Math.Max(MaximumCalibratedValue, other.MaximumCalibratedValue);
                }
            }

            Events += other.Events;
            ValidEvents += other.ValidEvents;
            BelowThresholdEvents += other.BelowThresholdEvents;
            RangeFoldedEvents += other.RangeFoldedEvents;
            ClutterFilterNotAppliedEvents += other.ClutterFilterNotAppliedEvents;
            PointClutterFilterAppliedEvents += other.PointClutterFilterAppliedEvents;
            DualPolarizationFilteredEvents += other.DualPolarizationFilteredEvents;
            ReservedEvents += other.ReservedEvents;
            UnsupportedEvents += other.UnsupportedEvents;
        }

        public void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            Events++;
            RawValueChecksum += gateMomentEvent.RawValue;
            AcceptRange(gateMomentEvent.RangeKilometers);
            AcceptChronology(gateMomentEvent);

            switch (gateMomentEvent.Status)
            {
                case ArchiveTwoGateMomentStatus.Valid:
                    ValidEvents++;
                    AcceptCalibratedValue(gateMomentEvent.CalibratedValue!.Value);
                    break;
                case ArchiveTwoGateMomentStatus.BelowThreshold:
                    BelowThresholdEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.RangeFolded:
                    RangeFoldedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.ClutterFilterNotApplied:
                    ClutterFilterNotAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.PointClutterFilterApplied:
                    PointClutterFilterAppliedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.DualPolarizationFiltered:
                    DualPolarizationFilteredEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Reserved:
                    ReservedEvents++;
                    break;
                case ArchiveTwoGateMomentStatus.Unsupported:
                    UnsupportedEvents++;
                    break;
            }
        }

        private void AcceptChronology(ArchiveTwoGateMomentEvent gateMomentEvent)
        {
            unchecked
            {
                ChronologyChecksum = ChronologyChecksum * ChronologyChecksumPrime + HashEventChronology(gateMomentEvent);
            }
        }

        private void AcceptCalibratedValue(double value)
        {
            if (ValidEvents == 1)
            {
                MinimumCalibratedValue = value;
                MaximumCalibratedValue = value;
            }
            else
            {
                MinimumCalibratedValue = Math.Min(MinimumCalibratedValue, value);
                MaximumCalibratedValue = Math.Max(MaximumCalibratedValue, value);
            }

            checked
            {
                CalibratedValueScaledChecksum += (long)Math.Round(value * 1_000d, MidpointRounding.AwayFromZero);
            }
        }

        private void AcceptRange(float rangeKilometers)
        {
            if (Events == 1)
            {
                MinimumRangeKilometers = rangeKilometers;
                MaximumRangeKilometers = rangeKilometers;
                return;
            }

            MinimumRangeKilometers = Math.Min(MinimumRangeKilometers, rangeKilometers);
            MaximumRangeKilometers = Math.Max(MaximumRangeKilometers, rangeKilometers);
        }
    }

    private sealed class ArchiveTwoReplayShapeBenchmarkWorker : IDisposable
    {
        private readonly IArchiveBZip2DecompressionSession decompressionSession;
        private readonly byte[] outputBuffer;
        private readonly ArchiveTwoReplayShapeRecordMetadataCollector metadataCollector = new();
        private readonly ArchiveTwoMessageStreamScanner metadataScanner;
        private readonly ArchiveTwoGateMomentEventProjector projector;
        private readonly ArchiveTwoMessageStreamScanner projectorScanner;
        private byte[]? compressedPayloadBuffer;

        public ArchiveTwoReplayShapeBenchmarkWorker(
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

        public ArchiveTwoReplayShapeRecordMetadata ReadRecordMetadata(
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

        private void AcceptEvent(ArchiveTwoGateMomentEvent gateMomentEvent) =>
            acceptEvent(gateMomentEvent);

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

using System.Buffers.Binary;
using System.Text;
using RadarPulse.Application.Archive;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Archive;


public sealed partial class NexradArchiveRadarEventBatchPublisherTests
{
    private static byte[] BuildArchiveTwoHeader(
        string radarId = "KTLX",
        DateOnly? date = null,
        int millisecondsPastMidnight = 164_018)
    {
        var effectiveDate = date ?? new DateOnly(2026, 5, 4);
        var header = new byte[24];
        Encoding.ASCII.GetBytes("AR2V0006.266").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32BigEndian(
            header.AsSpan(12, 4),
            effectiveDate.DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), millisecondsPastMidnight);
        Encoding.ASCII.GetBytes(radarId).CopyTo(header, 20);
        return header;
    }

    private static byte[] BuildCompressedRecord(int controlWord, byte[] compressedPayload) =>
        BuildCompressedRecordControlWord(controlWord).Concat(compressedPayload).ToArray();

    private static byte[] BuildCompressedRecordControlWord(int controlWord)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, controlWord);
        return buffer;
    }

    private static byte[] BuildFakeBZip2Payload(byte key) => [(byte)'B', (byte)'Z', (byte)'h', key];

    private static byte[] BuildMessage(
        byte messageType,
        byte[] payload,
        DateOnly? date = null,
        uint? millisecondsPastMidnight = null)
    {
        var effectiveDate = date ?? new DateOnly(2026, 5, 4);
        var effectiveMillisecondsPastMidnight = millisecondsPastMidnight ?? 164_018;
        var messageBytes = 16 + payload.Length;
        if (messageBytes % 2 != 0)
        {
            throw new ArgumentException("Synthetic message length must be even.", nameof(payload));
        }

        var message = new byte[messageBytes];
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0, 2), (ushort)(messageBytes / 2));
        message[2] = 8;
        message[3] = messageType;
        BinaryPrimitives.WriteUInt16BigEndian(
            message.AsSpan(6, 2),
            checked((ushort)(effectiveDate.DayNumber - new DateOnly(1970, 1, 1).DayNumber + 1)));
        BinaryPrimitives.WriteUInt32BigEndian(message.AsSpan(8, 4), effectiveMillisecondsPastMidnight);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(12, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(14, 2), 1);
        payload.CopyTo(message.AsSpan(16));
        return message;
    }

    private static byte[] BuildEightBitType31Payload(
        string momentName,
        byte[] values,
        float scale,
        float offset)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 8,
            values.Length,
            scale,
            offset);
        values.CopyTo(payload.AsSpan(100));
        return payload;
    }

    private static byte[] BuildSixteenBitType31Payload(
        string momentName,
        ushort[] values,
        float scale,
        float offset)
    {
        var payload = BuildType31Payload(
            momentName,
            checked((ushort)values.Length),
            wordSizeBits: 16,
            values.Length * sizeof(ushort),
            scale,
            offset);
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(100 + i * sizeof(ushort), sizeof(ushort)), values[i]);
        }

        return payload;
    }

    private static byte[] BuildType31Payload(
        string momentName,
        ushort gates,
        byte wordSizeBits,
        int momentDataByteCount,
        float scale,
        float offset)
    {
        const int momentOffset = 72;
        var payload = new byte[Math.Max(momentOffset + 28 + momentDataByteCount, 160)];
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(18, 2), (ushort)payload.Length);
        payload[22] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(30, 2), 1);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(32, 4), momentOffset);

        payload[momentOffset] = (byte)'D';
        for (var i = 0; i < momentName.Length && i < 3; i++)
        {
            payload[momentOffset + 1 + i] = (byte)momentName[i];
        }

        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(momentOffset + 8, 2), gates);
        payload[momentOffset + 19] = wordSizeBits;
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 20, 4), scale);
        WriteSingleBigEndian(payload.AsSpan(momentOffset + 24, 4), offset);
        return payload;
    }

    private static void WriteSingleBigEndian(Span<byte> destination, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(destination, BitConverter.SingleToInt32Bits(value));

    private static string WriteTempFile(string fileName, byte[] contents)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RadarPulse.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static string WriteTempFileInDirectory(string directory, string fileName, byte[] contents)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static void AssertDirectBorrowedDefaultContour(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, result.ProviderMode);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(0, result.QueueCapacity);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.RetentionStrategy);
        Assert.Null(result.QueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, result.OverlapConsumerDelay);
        Assert.False(result.HasWorkerTelemetry);
        Assert.Null(result.WorkerTelemetry);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.False(result.HasQueueTelemetry);
        Assert.False(result.HasRetentionTelemetry);
        Assert.False(result.HasOverlapTelemetry);
        Assert.Equal(0, result.OwnedSnapshotAllocatedBytes);
        Assert.Equal(
            result.ProcessingCallbackAllocatedBytes,
            result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes);
        Assert.Equal(0, result.QueueTelemetry.EnqueueAttemptCount);
        Assert.Equal(0, result.RetentionTelemetry.RetentionAttemptCount);
        Assert.Equal(0, result.OverlapTelemetry.MeasuredAllocatedBytes);
        Assert.Equal(RadarProcessingRetainedResourcePressureSummary.Empty, result.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.HasRetainedPayloadPrewarm);
        Assert.Equal(0, result.RetainedPayloadPrewarmAllocatedBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertDirectBorrowedDefaultContour(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveProviderMode.BlockingBorrowed, result.ProviderMode);
        Assert.Equal(RadarProcessingExecutionMode.PartitionedBarrier, result.ExecutionMode);
        Assert.Equal(0, result.QueueCapacity);
        Assert.Equal(RadarProcessingQueuedProviderOverlapMode.None, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.SnapshotCopy, result.RetentionStrategy);
        Assert.Null(result.QueueRetainedPayloadBytes);
        Assert.Equal(TimeSpan.Zero, result.OverlapConsumerDelay);
        Assert.False(result.HasWorkerTelemetry);
        Assert.Null(result.WorkerTelemetry);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.False(result.HasQueueTelemetry);
        Assert.False(result.HasRetentionTelemetry);
        Assert.False(result.HasOverlapTelemetry);
        Assert.Equal(0, result.OwnedSnapshotAllocatedBytes);
        Assert.Equal(
            result.ProcessingCallbackAllocatedBytes,
            result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes);
        Assert.Equal(0, result.QueueTelemetry.EnqueueAttemptCount);
        Assert.Equal(0, result.RetentionTelemetry.RetentionAttemptCount);
        Assert.Equal(0, result.OverlapTelemetry.MeasuredAllocatedBytes);
        Assert.Equal(RadarProcessingRetainedResourcePressureSummary.Empty, result.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.HasRetainedPayloadPrewarm);
        Assert.Equal(0, result.RetainedPayloadPrewarmAllocatedBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertDefaultRetainedPayloadPrewarm(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            result.RetainedPayloadPrewarm.EventCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            result.RetainedPayloadPrewarm.PayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.RetainedPayloadPrewarmAllocatedBytes > 0);
        Assert.Equal(
            result.RetainedPayloadPrewarm.RetainedBytes,
            result.RetainedPayloadPrewarmRetainedBytes);
    }

    private static void AssertDefaultRetainedPayloadPrewarm(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        Assert.True(result.HasRetainedPayloadPrewarm);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmEventCount,
            result.RetainedPayloadPrewarm.EventCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmPayloadBytes,
            result.RetainedPayloadPrewarm.PayloadBytes);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadPrewarmBatchCount,
            result.RetainedPayloadPrewarm.RetainedBatchCount);
        Assert.True(result.RetainedPayloadPrewarmAllocatedBytes > 0);
        Assert.Equal(
            result.RetainedPayloadPrewarm.RetainedBytes,
            result.RetainedPayloadPrewarmRetainedBytes);
    }

    private static void AssertDirectQueuedOwnedRolloutContour(
        RadarProcessingArchiveRebalanceBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode, result.ProviderMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode, result.ExecutionMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, result.QueueCapacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy, result.RetentionStrategy);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes, result.QueueRetainedPayloadBytes);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay, result.OverlapConsumerDelay);
        Assert.True(result.HasWorkerTelemetry);
        Assert.NotNull(result.WorkerTelemetry);
        var workerTelemetry = result.WorkerTelemetry!;
        Assert.True(
            RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                result.ProviderMode,
                result.ProviderOverlapMode,
                result.RetentionStrategy,
                result.ExecutionMode,
                new RadarProcessingAsyncExecutionOptions(
                    workerTelemetry.WorkerCount,
                    workerTelemetry.QueueCapacity),
                result.QueueCapacity,
                result.QueueRetainedPayloadBytes,
                result.OverlapConsumerDelay));
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, workerTelemetry.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            workerTelemetry.QueueCapacity);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.True(result.HasQueueTelemetry);
        Assert.True(result.HasRetentionTelemetry);
        Assert.True(result.HasOverlapTelemetry);
        Assert.Equal(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, result.OwnedSnapshotAllocatedBytes);
        Assert.True(result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes >= 0);
        Assert.True(result.QueueTelemetry.EnqueueAttemptCount > 0);
        Assert.True(result.RetentionTelemetry.RetentionAttemptCount > 0);
        Assert.Equal(0, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.OverlapTelemetry.ReleaseFailedCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionTelemetry.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        AssertRetainedPoolTelemetryReleased(result.RetentionTelemetry);
        Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertDirectQueuedOwnedRolloutContour(
        RadarProcessingArchiveRebalanceCacheBenchmarkResult result)
    {
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderMode, result.ProviderMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ExecutionMode, result.ExecutionMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderQueueCapacity, result.QueueCapacity);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.ProviderOverlapMode, result.ProviderOverlapMode);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetentionStrategy, result.RetentionStrategy);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.RetainedPayloadBytes, result.QueueRetainedPayloadBytes);
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.OverlapConsumerDelay, result.OverlapConsumerDelay);
        Assert.True(result.HasWorkerTelemetry);
        Assert.NotNull(result.WorkerTelemetry);
        var workerTelemetry = result.WorkerTelemetry!;
        Assert.True(
            RadarProcessingArchiveRebalanceRolloutDefaults.Matches(
                result.ProviderMode,
                result.ProviderOverlapMode,
                result.RetentionStrategy,
                result.ExecutionMode,
                new RadarProcessingAsyncExecutionOptions(
                    workerTelemetry.WorkerCount,
                    workerTelemetry.QueueCapacity),
                result.QueueCapacity,
                result.QueueRetainedPayloadBytes,
                result.OverlapConsumerDelay));
        Assert.Equal(RadarProcessingArchiveRebalanceRolloutDefaults.WorkerCount, workerTelemetry.WorkerCount);
        Assert.Equal(
            RadarProcessingArchiveRebalanceRolloutDefaults.WorkerQueueCapacity,
            workerTelemetry.QueueCapacity);
        Assert.True(result.ProcessingSucceeded);
        Assert.Equal(0, result.ProcessingValidationFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedBatchCount);
        Assert.Equal(0, result.WorkerFailedWorkItemCount);
        Assert.True(result.HasQueueTelemetry);
        Assert.True(result.HasRetentionTelemetry);
        Assert.True(result.HasOverlapTelemetry);
        Assert.Equal(result.QueueTelemetry.OwnedSnapshotAllocatedBytes, result.OwnedSnapshotAllocatedBytes);
        Assert.True(result.ProcessingCallbackNonOwnedSnapshotAllocatedBytes >= 0);
        Assert.True(result.QueueTelemetry.EnqueueAttemptCount > 0);
        Assert.True(result.RetentionTelemetry.RetentionAttemptCount > 0);
        Assert.Equal(0, result.RetentionTelemetry.ReleaseFailedCount);
        Assert.Equal(0, result.OverlapTelemetry.ReleaseFailedCount);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.RetentionTelemetry.Strategy);
        Assert.Equal(RadarProcessingRetainedPayloadStrategy.PooledCopy, result.OverlapTelemetry.RetentionStrategy);
        AssertRetainedPoolTelemetryReleased(result.RetentionTelemetry);
        Assert.Equal(result.QueueTelemetry.RetainedResourcePressure, result.OverlapTelemetry.RetainedResourcePressure);
        Assert.Equal(0, result.CurrentPendingRetainedBatchCount);
        Assert.Equal(0, result.CurrentActiveRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedBatchCount);
        Assert.Equal(0, result.CurrentCombinedRetainedPayloadBytes);
        Assert.False(result.AllocationSummary.IncludesCliFormatting);
    }

    private static void AssertRetainedPoolTelemetryReleased(
        RadarProcessingRetainedPayloadTelemetrySummary telemetry)
    {
        Assert.Equal(
            telemetry.PoolRentCount,
            telemetry.EventPoolRentCount + telemetry.PayloadPoolRentCount);
        Assert.Equal(
            telemetry.PoolReturnCount,
            telemetry.EventPoolReturnCount + telemetry.PayloadPoolReturnCount);
        Assert.Equal(
            telemetry.PoolMissCount,
            telemetry.EventPoolMissCount + telemetry.PayloadPoolMissCount);
        Assert.True(telemetry.EventPoolRentCount > 0);
        Assert.True(telemetry.PayloadPoolRentCount > 0);
        Assert.Equal(telemetry.EventPoolRentCount, telemetry.EventPoolReturnCount);
        Assert.Equal(telemetry.PayloadPoolRentCount, telemetry.PayloadPoolReturnCount);
    }

    private sealed class CapturingRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private sealed class LeasedCapturingRadarEventBatchPublisher : IArchiveRadarEventBatchPublisher
    {
        private readonly List<RadarEventBatch> batches = new();

        public IReadOnlyList<RadarEventBatch> Batches => batches;

        public void Publish(RadarEventBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(RadarEventBatchLifetime.Leased, batch.Lifetime);
            batches.Add(batch.ToOwnedSnapshot());
        }
    }

    private static void AssertArchiveRadarEventBatchPublishTotalsEqual(
        ArchiveRadarEventBatchPublishResult expected,
        ArchiveRadarEventBatchPublishResult actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Decompressor, actual.Decompressor);
        Assert.Equal(expected.DegreeOfParallelism, actual.DegreeOfParallelism);
        Assert.Equal(expected.FileSizeBytes, actual.FileSizeBytes);
        Assert.Equal(expected.CompressedRecordCount, actual.CompressedRecordCount);
        Assert.Equal(expected.CompressedBytes, actual.CompressedBytes);
        Assert.Equal(expected.DecompressedBytes, actual.DecompressedBytes);
        Assert.Equal(expected.StreamSchemaVersion, actual.StreamSchemaVersion);
        Assert.Equal(expected.DictionaryVersion, actual.DictionaryVersion);
        Assert.Equal(expected.SourceUniverseVersion, actual.SourceUniverseVersion);
        Assert.Equal(expected.BatchCount, actual.BatchCount);
        Assert.Equal(expected.EventCount, actual.EventCount);
        Assert.Equal(expected.PayloadBytes, actual.PayloadBytes);
        Assert.Equal(expected.PayloadValueCount, actual.PayloadValueCount);
        Assert.Equal(expected.RawValueChecksum, actual.RawValueChecksum);
    }

    private sealed class FakeArchiveBZip2Decompressor : IArchiveBZip2Decompressor
    {
        private readonly IReadOnlyDictionary<byte, byte[]> decompressedRecords;
        private readonly IReadOnlyDictionary<byte, int> delayMillisecondsByRecord;

        public FakeArchiveBZip2Decompressor(
            IReadOnlyDictionary<byte, byte[]> decompressedRecords,
            IReadOnlyDictionary<byte, int>? delayMillisecondsByRecord = null)
        {
            this.decompressedRecords = decompressedRecords;
            this.delayMillisecondsByRecord = delayMillisecondsByRecord ?? new Dictionary<byte, int>();
        }

        public string Name => "fake";

        public IArchiveBZip2DecompressionSession CreateSession() =>
            new Session(decompressedRecords, delayMillisecondsByRecord);

        public long Decompress(
            byte[] compressedPayload,
            int compressedSizeBytes,
            byte[] outputBuffer,
            ArchiveBZip2DecompressedChunkHandler? chunkHandler) =>
            CreateSession().Decompress(compressedPayload, compressedSizeBytes, outputBuffer, chunkHandler);

        public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
            CreateSession().CountDecompressedBytes(compressedPayload, compressedSizeBytes, outputBuffer);

        private sealed class Session : IArchiveBZip2DecompressionSession
        {
            private readonly IReadOnlyDictionary<byte, byte[]> decompressedRecords;
            private readonly IReadOnlyDictionary<byte, int> delayMillisecondsByRecord;

            public Session(
                IReadOnlyDictionary<byte, byte[]> decompressedRecords,
                IReadOnlyDictionary<byte, int> delayMillisecondsByRecord)
            {
                this.decompressedRecords = decompressedRecords;
                this.delayMillisecondsByRecord = delayMillisecondsByRecord;
            }

            public long Decompress(
                byte[] compressedPayload,
                int compressedSizeBytes,
                byte[] outputBuffer,
                ArchiveBZip2DecompressedChunkHandler? chunkHandler)
            {
                var record = ReadRecord(compressedPayload, compressedSizeBytes);
                if (chunkHandler is null)
                {
                    return record.Length;
                }

                var firstChunkLength = Math.Min(5, record.Length);
                chunkHandler(record.AsSpan(0, firstChunkLength));
                if (firstChunkLength < record.Length)
                {
                    chunkHandler(record.AsSpan(firstChunkLength));
                }

                return record.Length;
            }

            public long CountDecompressedBytes(byte[] compressedPayload, int compressedSizeBytes, byte[] outputBuffer) =>
                ReadRecord(compressedPayload, compressedSizeBytes).Length;

            private byte[] ReadRecord(byte[] compressedPayload, int compressedSizeBytes)
            {
                if (compressedSizeBytes < 4 ||
                    compressedPayload[0] != (byte)'B' ||
                    compressedPayload[1] != (byte)'Z' ||
                    compressedPayload[2] != (byte)'h')
                {
                    throw new InvalidDataException("Fake compressed payload does not start with BZh.");
                }

                var recordKey = compressedPayload[3];
                if (delayMillisecondsByRecord.TryGetValue(recordKey, out var delayMilliseconds))
                {
                    Thread.Sleep(delayMilliseconds);
                }

                return decompressedRecords[recordKey];
            }
        }
    }
}

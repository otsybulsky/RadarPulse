using System.Buffers;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveReplayShapeValidator
{
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
                sequential.RadialUnevenness,
                sequential.TimeBucketUnevenness,
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
                ArchiveTwoReplayShapeUnevennessSummary.Empty("radial"),
                ArchiveTwoReplayShapeUnevennessSummary.Empty("minute"),
                ex.Message);
        }
    }

    private ArchiveTwoReplayShapeAnalysis AnalyzeArchiveTwoFile(
        FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        var volumeHeader = ArchiveTwoFileReader.ReadVolumeHeader(fileInfo);
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
            stream.Position = ArchiveTwoFileReader.VolumeHeaderLength;

            while (stream.Position < stream.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var controlWordOffset = stream.Position;
                var compressedSizeBytes = ArchiveTwoFileReader.ReadCompressedRecordSize(stream, controlWordBuffer, controlWordOffset);
                compressedPayloadBuffer = ArchiveTwoFileReader.EnsurePooledBufferCapacity(compressedPayloadBuffer, compressedSizeBytes);
                ArchiveTwoFileReader.ReadExactly(stream, compressedPayloadBuffer.AsSpan(0, compressedSizeBytes));
                ArchiveTwoFileReader.ValidateBZip2Signature(compressedPayloadBuffer.AsSpan(0, compressedSizeBytes), controlWordOffset);

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
}

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveParseBenchmark
{
    private sealed class ArchiveTwoParseBenchmarkWorker : IDisposable
    {
        private readonly ArchiveTwoMessageSummaryBuilder messageSummaryBuilder;
        private readonly ArchiveTwoMessageStreamScanner messageScanner;
        private byte[]? compressedPayloadBuffer;

        public ArchiveTwoParseBenchmarkWorker(
            IArchiveBZip2DecompressionSession decompressionSession,
            bool decodeMomentValues,
            bool decodeCalibratedMomentValues)
        {
            DecompressionSession = decompressionSession;
            OutputBuffer = ArrayPool<byte>.Shared.Rent(OutputBufferSize);
            messageSummaryBuilder = new ArchiveTwoMessageSummaryBuilder(
                decodeMomentValues,
                collectSweepSummaries: false,
                decodeCalibratedMomentValues);
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
                DecodedGateMomentValueChecksum: messageSummaryBuilder.DecodedGateMomentValueChecksum,
                CalibratedGateMomentValues: messageSummaryBuilder.CalibratedGateMomentValueCount,
                BelowThresholdGateMomentValues: messageSummaryBuilder.BelowThresholdGateMomentValueCount,
                RangeFoldedGateMomentValues: messageSummaryBuilder.RangeFoldedGateMomentValueCount,
                ClutterFilterNotAppliedGateMomentValues: messageSummaryBuilder.ClutterFilterNotAppliedGateMomentValueCount,
                PointClutterFilterAppliedGateMomentValues: messageSummaryBuilder.PointClutterFilterAppliedGateMomentValueCount,
                DualPolarizationFilteredGateMomentValues: messageSummaryBuilder.DualPolarizationFilteredGateMomentValueCount,
                ReservedGateMomentValues: messageSummaryBuilder.ReservedGateMomentValueCount,
                UnsupportedCalibratedGateMomentValues: messageSummaryBuilder.UnsupportedCalibratedGateMomentValueCount,
                CalibratedGateMomentValueScaledChecksum: messageSummaryBuilder.CalibratedGateMomentValueScaledChecksum,
                MinimumCalibratedGateMomentValue: messageSummaryBuilder.MinimumCalibratedGateMomentValue,
                MaximumCalibratedGateMomentValue: messageSummaryBuilder.MaximumCalibratedGateMomentValue);
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

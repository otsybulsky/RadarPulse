using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveParseBenchmark
{
    private readonly record struct ArchiveTwoParseIterationMeasurement(
        int CompressedRecordCount,
        long CompressedBytes,
        long DecompressedBytes,
        int MessageCount,
        int Type31RadialCount,
        long EstimatedGateMomentEvents,
        long DecodedGateMomentValues,
        ulong DecodedGateMomentValueChecksum,
        long CalibratedGateMomentValues,
        long BelowThresholdGateMomentValues,
        long RangeFoldedGateMomentValues,
        long ClutterFilterNotAppliedGateMomentValues,
        long PointClutterFilterAppliedGateMomentValues,
        long DualPolarizationFilteredGateMomentValues,
        long ReservedGateMomentValues,
        long UnsupportedCalibratedGateMomentValues,
        long CalibratedGateMomentValueScaledChecksum,
        double MinimumCalibratedGateMomentValue,
        double MaximumCalibratedGateMomentValue)
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
                    left.DecodedGateMomentValueChecksum + right.DecodedGateMomentValueChecksum,
                    left.CalibratedGateMomentValues + right.CalibratedGateMomentValues,
                    left.BelowThresholdGateMomentValues + right.BelowThresholdGateMomentValues,
                    left.RangeFoldedGateMomentValues + right.RangeFoldedGateMomentValues,
                    left.ClutterFilterNotAppliedGateMomentValues + right.ClutterFilterNotAppliedGateMomentValues,
                    left.PointClutterFilterAppliedGateMomentValues + right.PointClutterFilterAppliedGateMomentValues,
                    left.DualPolarizationFilteredGateMomentValues + right.DualPolarizationFilteredGateMomentValues,
                    left.ReservedGateMomentValues + right.ReservedGateMomentValues,
                    left.UnsupportedCalibratedGateMomentValues + right.UnsupportedCalibratedGateMomentValues,
                    left.CalibratedGateMomentValueScaledChecksum + right.CalibratedGateMomentValueScaledChecksum,
                    CombineMinimumCalibratedMomentValue(left, right),
                    CombineMaximumCalibratedMomentValue(left, right));
            }
        }

        private static double CombineMinimumCalibratedMomentValue(
            ArchiveTwoParseIterationMeasurement left,
            ArchiveTwoParseIterationMeasurement right)
        {
            if (left.CalibratedGateMomentValues == 0)
            {
                return right.MinimumCalibratedGateMomentValue;
            }

            if (right.CalibratedGateMomentValues == 0)
            {
                return left.MinimumCalibratedGateMomentValue;
            }

            return Math.Min(left.MinimumCalibratedGateMomentValue, right.MinimumCalibratedGateMomentValue);
        }

        private static double CombineMaximumCalibratedMomentValue(
            ArchiveTwoParseIterationMeasurement left,
            ArchiveTwoParseIterationMeasurement right)
        {
            if (left.CalibratedGateMomentValues == 0)
            {
                return right.MaximumCalibratedGateMomentValue;
            }

            if (right.CalibratedGateMomentValues == 0)
            {
                return left.MaximumCalibratedGateMomentValue;
            }

            return Math.Max(left.MaximumCalibratedGateMomentValue, right.MaximumCalibratedGateMomentValue);
        }
    }
}

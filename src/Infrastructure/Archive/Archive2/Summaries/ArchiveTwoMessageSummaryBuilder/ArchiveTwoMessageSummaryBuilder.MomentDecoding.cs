using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoMessageSummaryBuilder
{
    private void DecodeMomentValues(
        string momentName,
        ReadOnlySpan<byte> block,
        Type31MomentMetadata metadata)
    {
        switch (metadata.WordSizeBits)
        {
            case 8:
                DecodeEightBitMomentValues(momentName, block[GenericMomentDataOffset..], metadata);
                break;
            case 16:
                DecodeSixteenBitMomentValues(momentName, block[GenericMomentDataOffset..], metadata);
                break;
        }
    }

    private void DecodeEightBitMomentValues(
        string momentName,
        ReadOnlySpan<byte> data,
        Type31MomentMetadata metadata)
    {
        if (data.Length < metadata.GateCount)
        {
            return;
        }

        for (var i = 0; i < metadata.GateCount; i++)
        {
            var rawValue = data[i];
            unchecked
            {
                decodedGateMomentValueChecksum += rawValue;
            }

            AcceptCalibratedMomentValue(momentName, rawValue, metadata);
        }

        decodedGateMomentValues += metadata.GateCount;
    }

    private void DecodeSixteenBitMomentValues(
        string momentName,
        ReadOnlySpan<byte> data,
        Type31MomentMetadata metadata)
    {
        var requiredBytes = checked(metadata.GateCount * sizeof(ushort));
        if (data.Length < requiredBytes)
        {
            return;
        }

        for (var i = 0; i < requiredBytes; i += sizeof(ushort))
        {
            var rawValue = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i, sizeof(ushort)));
            unchecked
            {
                decodedGateMomentValueChecksum += rawValue;
            }

            AcceptCalibratedMomentValue(momentName, rawValue, metadata);
        }

        decodedGateMomentValues += metadata.GateCount;
    }

    private void AcceptCalibratedMomentValue(
        string momentName,
        int rawValue,
        Type31MomentMetadata metadata)
    {
        if (!decodeCalibratedMomentValues)
        {
            return;
        }

        if (IsClutterFilterPowerRemovedMoment(momentName))
        {
            switch (rawValue)
            {
                case 0:
                    clutterFilterNotAppliedGateMomentValues++;
                    return;
                case 1:
                    pointClutterFilterAppliedGateMomentValues++;
                    return;
                case 2:
                    dualPolarizationFilteredGateMomentValues++;
                    return;
            }

            if (rawValue < 8)
            {
                reservedGateMomentValues++;
                return;
            }
        }
        else
        {
            switch (rawValue)
            {
                case 0:
                    belowThresholdGateMomentValues++;
                    return;
                case 1:
                    rangeFoldedGateMomentValues++;
                    return;
            }
        }

        if (metadata.Scale == 0 || !float.IsFinite(metadata.Scale))
        {
            unsupportedCalibratedGateMomentValues++;
            return;
        }

        var calibratedValue = (rawValue - metadata.Offset) / metadata.Scale;
        if (!double.IsFinite(calibratedValue))
        {
            unsupportedCalibratedGateMomentValues++;
            return;
        }

        if (calibratedGateMomentValues == 0)
        {
            minimumCalibratedGateMomentValue = calibratedValue;
            maximumCalibratedGateMomentValue = calibratedValue;
        }
        else
        {
            minimumCalibratedGateMomentValue = Math.Min(minimumCalibratedGateMomentValue, calibratedValue);
            maximumCalibratedGateMomentValue = Math.Max(maximumCalibratedGateMomentValue, calibratedValue);
        }

        calibratedGateMomentValues++;
        checked
        {
            calibratedGateMomentValueScaledChecksum += (long)Math.Round(calibratedValue * 1_000d, MidpointRounding.AwayFromZero);
        }
    }

    private static bool IsClutterFilterPowerRemovedMoment(string momentName) =>
        string.Equals(momentName, "CFP", StringComparison.Ordinal);

}

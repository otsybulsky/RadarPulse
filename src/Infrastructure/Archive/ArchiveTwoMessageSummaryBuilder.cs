using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveTwoMessageSummaryBuilder : IArchiveTwoMessageConsumer
{
    private const int MessageHeaderLength = 16;
    private const int Type31DataHeaderMinimumLength = 72;
    private const int Type31DataBlockPointerOffset = 32;
    private const int Type31DataBlockPointerLength = 4;
    private const int Type31MaximumDataBlockPointers = 10;
    private const int GenericMomentDescriptorLength = 28;
    private const int GenericMomentDataOffset = 28;
    private const int GenericMomentGateCountOffset = 8;
    private const int GenericMomentFirstGateRangeOffset = 10;
    private const int GenericMomentGateSpacingOffset = 12;
    private const int GenericMomentWordSizeOffset = 19;
    private const int GenericMomentScaleOffset = 20;
    private const int GenericMomentOffsetOffset = 24;

    private readonly bool decodeMomentValues;
    private readonly bool collectSweepSummaries;
    private readonly bool decodeCalibratedMomentValues;
    private readonly Dictionary<int, int> messageTypeCounts = new();
    private readonly Dictionary<string, MomentAccumulator> moments = new(StringComparer.Ordinal);
    private readonly List<SweepAccumulator> sweeps = new();
    private int messageCount;
    private int type31RadialCount;
    private long estimatedGateMomentEvents;
    private long decodedGateMomentValues;
    private ulong decodedGateMomentValueChecksum;
    private long calibratedGateMomentValues;
    private long belowThresholdGateMomentValues;
    private long rangeFoldedGateMomentValues;
    private long clutterFilterNotAppliedGateMomentValues;
    private long pointClutterFilterAppliedGateMomentValues;
    private long dualPolarizationFilteredGateMomentValues;
    private long reservedGateMomentValues;
    private long unsupportedCalibratedGateMomentValues;
    private long calibratedGateMomentValueScaledChecksum;
    private double minimumCalibratedGateMomentValue;
    private double maximumCalibratedGateMomentValue;
    private int volumeConstantBlockCount;
    private int elevationConstantBlockCount;
    private int radialConstantBlockCount;
    private SweepAccumulator? currentSweep;

    public ArchiveTwoMessageSummaryBuilder(
        bool decodeMomentValues = false,
        bool collectSweepSummaries = true,
        bool decodeCalibratedMomentValues = false)
    {
        this.decodeMomentValues = decodeMomentValues || decodeCalibratedMomentValues;
        this.collectSweepSummaries = collectSweepSummaries;
        this.decodeCalibratedMomentValues = decodeCalibratedMomentValues;
    }

    public int MessageCount => messageCount;

    public int Type31RadialCount => type31RadialCount;

    public long EstimatedGateMomentEventCount => estimatedGateMomentEvents;

    public long DecodedGateMomentValueCount => decodedGateMomentValues;

    public ulong DecodedGateMomentValueChecksum => decodedGateMomentValueChecksum;

    public long CalibratedGateMomentValueCount => calibratedGateMomentValues;

    public long BelowThresholdGateMomentValueCount => belowThresholdGateMomentValues;

    public long RangeFoldedGateMomentValueCount => rangeFoldedGateMomentValues;

    public long ClutterFilterNotAppliedGateMomentValueCount => clutterFilterNotAppliedGateMomentValues;

    public long PointClutterFilterAppliedGateMomentValueCount => pointClutterFilterAppliedGateMomentValues;

    public long DualPolarizationFilteredGateMomentValueCount => dualPolarizationFilteredGateMomentValues;

    public long ReservedGateMomentValueCount => reservedGateMomentValues;

    public long UnsupportedCalibratedGateMomentValueCount => unsupportedCalibratedGateMomentValues;

    public long CalibratedGateMomentValueScaledChecksum => calibratedGateMomentValueScaledChecksum;

    public double MinimumCalibratedGateMomentValue => minimumCalibratedGateMomentValue;

    public double MaximumCalibratedGateMomentValue => maximumCalibratedGateMomentValue;

    public void Reset()
    {
        messageTypeCounts.Clear();
        moments.Clear();
        messageCount = 0;
        type31RadialCount = 0;
        estimatedGateMomentEvents = 0;
        decodedGateMomentValues = 0;
        decodedGateMomentValueChecksum = 0;
        calibratedGateMomentValues = 0;
        belowThresholdGateMomentValues = 0;
        rangeFoldedGateMomentValues = 0;
        clutterFilterNotAppliedGateMomentValues = 0;
        pointClutterFilterAppliedGateMomentValues = 0;
        dualPolarizationFilteredGateMomentValues = 0;
        reservedGateMomentValues = 0;
        unsupportedCalibratedGateMomentValues = 0;
        calibratedGateMomentValueScaledChecksum = 0;
        minimumCalibratedGateMomentValue = 0;
        maximumCalibratedGateMomentValue = 0;
        volumeConstantBlockCount = 0;
        elevationConstantBlockCount = 0;
        radialConstantBlockCount = 0;
        sweeps.Clear();
        currentSweep = null;
    }

    public void Add(ArchiveTwoMessageSummary summary)
    {
        messageCount += summary.MessageCount;
        foreach (var messageType in summary.MessageTypes)
        {
            messageTypeCounts.TryGetValue(messageType.MessageType, out var count);
            messageTypeCounts[messageType.MessageType] = count + messageType.Count;
        }

        type31RadialCount += summary.Type31.RadialCount;
        estimatedGateMomentEvents += summary.Type31.EstimatedGateMomentEventCount;
        volumeConstantBlockCount += summary.Type31.ConstantBlocks.VolumeCount;
        elevationConstantBlockCount += summary.Type31.ConstantBlocks.ElevationCount;
        radialConstantBlockCount += summary.Type31.ConstantBlocks.RadialCount;
        foreach (var moment in summary.Type31.Moments)
        {
            moments.TryGetValue(moment.Name, out var accumulator);
            accumulator ??= new MomentAccumulator();
            accumulator.Add(moment);
            moments[moment.Name] = accumulator;
        }

        if (collectSweepSummaries)
        {
            foreach (var sweep in summary.Type31.Sweeps)
            {
                sweeps.Add(SweepAccumulator.FromSummary(sweeps.Count + 1, sweep));
            }
        }
    }

    public void AcceptMessage(ReadOnlySpan<byte> message) =>
        AcceptMessage(message, default);

    public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
    {
        if (message.Length < MessageHeaderLength)
        {
            throw new InvalidDataException("RDA/RPG message is shorter than the 16-byte message header.");
        }

        var messageType = message[3];
        messageCount++;
        messageTypeCounts.TryGetValue(messageType, out var count);
        messageTypeCounts[messageType] = count + 1;

        if (messageType == 31)
        {
            ParseType31(message[MessageHeaderLength..], source);
        }
    }

    public ArchiveTwoMessageSummary Build() =>
        new(
            messageCount,
            messageTypeCounts
                .OrderBy(pair => pair.Key)
                .Select(pair => new ArchiveTwoMessageTypeCount(pair.Key, pair.Value))
                .ToArray(),
            new ArchiveTwoType31Summary(
                type31RadialCount,
                estimatedGateMomentEvents,
                new ArchiveTwoType31ConstantBlockSummary(
                    volumeConstantBlockCount,
                    elevationConstantBlockCount,
                    radialConstantBlockCount),
                moments
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new ArchiveTwoMomentSummary(
                        pair.Key,
                        pair.Value.RadialCount,
                        pair.Value.GateCount,
                        pair.Value.MinimumGateCount,
                        pair.Value.MaximumGateCount,
                        pair.Value.MinimumWordSizeBits,
                        pair.Value.MaximumWordSizeBits,
                        pair.Value.MinimumFirstGateRangeKilometers,
                        pair.Value.MaximumFirstGateRangeKilometers,
                        pair.Value.MinimumGateSpacingKilometers,
                        pair.Value.MaximumGateSpacingKilometers,
                        pair.Value.MinimumScale,
                        pair.Value.MaximumScale,
                        pair.Value.MinimumOffset,
                        pair.Value.MaximumOffset))
                    .ToArray(),
                collectSweepSummaries
                    ? sweeps.Select(sweep => sweep.ToSummary()).ToArray()
                    : Array.Empty<ArchiveTwoSweepSummary>()));

    private void ParseType31(ReadOnlySpan<byte> payload, ArchiveTwoMessageSource source)
    {
        if (payload.Length < Type31DataHeaderMinimumLength)
        {
            return;
        }

        var radialLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(18, 2));
        var parseLength = Math.Min(payload.Length, radialLength > 0 ? radialLength : payload.Length);
        var parsePayload = payload[..parseLength];
        var radial = ReadType31RadialMetadata(parsePayload);
        var sourceOrder = new ArchiveTwoRadialSourceOrder(
            source.CompressedRecordSequenceNumber,
            source.MessageSequenceNumberInRecord,
            type31RadialCount + 1);
        var sweep = AcceptRadial(radial, sourceOrder);
        var blockCount = BinaryPrimitives.ReadUInt16BigEndian(parsePayload.Slice(30, 2));
        var pointerCount = Math.Min(
            Math.Min((int)blockCount, Type31MaximumDataBlockPointers),
            (parsePayload.Length - Type31DataBlockPointerOffset) / Type31DataBlockPointerLength);

        for (var i = 0; i < pointerCount; i++)
        {
            var pointer = BinaryPrimitives.ReadInt32BigEndian(
                parsePayload.Slice(Type31DataBlockPointerOffset + i * Type31DataBlockPointerLength, Type31DataBlockPointerLength));
            if (pointer <= 0 || pointer >= parsePayload.Length)
            {
                continue;
            }

            var block = parsePayload[pointer..];
            if (block.Length < 4)
            {
                continue;
            }

            var blockName = ReadDataBlockName(block);
            if (block[0] == (byte)'R')
            {
                AcceptConstantBlock(blockName, sweep);
                continue;
            }

            if (block[0] == (byte)'D')
            {
                AcceptMomentBlock(block, blockName, sweep);
            }
        }

        type31RadialCount++;
    }

    private static Type31RadialMetadata ReadType31RadialMetadata(ReadOnlySpan<byte> payload) =>
        new(
            payload[21],
            payload[22],
            payload[23],
            ReadSingleBigEndian(payload.Slice(24, 4)));

    private SweepAccumulator? AcceptRadial(
        Type31RadialMetadata radial,
        ArchiveTwoRadialSourceOrder sourceOrder)
    {
        if (!collectSweepSummaries)
        {
            return null;
        }

        if (currentSweep is null ||
            ShouldStartNewSweep(radial, currentSweep))
        {
            currentSweep = new SweepAccumulator(
                sweeps.Count + 1,
                radial.ElevationNumber,
                radial.RadialStatus,
                sourceOrder);
            sweeps.Add(currentSweep);
        }

        currentSweep.AcceptRadial(radial, sourceOrder);
        return currentSweep;
    }

    private static bool ShouldStartNewSweep(Type31RadialMetadata radial, SweepAccumulator current) =>
        current.RadialCount > 0 &&
        (IsStartRadialStatus(radial.RadialStatus) ||
            radial.ElevationNumber != current.ElevationNumber);

    private static bool IsStartRadialStatus(int radialStatus) =>
        radialStatus is 0 or 3 or 5 or 80 or 83 or 85;

    private void AcceptConstantBlock(string blockName, SweepAccumulator? sweep)
    {
        switch (blockName)
        {
            case "VOL":
                volumeConstantBlockCount++;
                sweep?.AcceptVolumeConstantBlock();
                break;
            case "ELV":
                elevationConstantBlockCount++;
                sweep?.AcceptElevationConstantBlock();
                break;
            case "RAD":
                radialConstantBlockCount++;
                sweep?.AcceptRadialConstantBlock();
                break;
        }
    }

    private void AcceptMomentBlock(
        ReadOnlySpan<byte> block,
        string name,
        SweepAccumulator? sweep)
    {
        if (name.Length == 0 ||
            block.Length < GenericMomentDescriptorLength)
        {
            return;
        }

        var metadata = ReadMomentMetadata(block);
        moments.TryGetValue(name, out var accumulator);
        accumulator ??= new MomentAccumulator();
        accumulator.Add(metadata);
        moments[name] = accumulator;
        sweep?.AcceptMoment(name);
        estimatedGateMomentEvents += metadata.GateCount;
        if (decodeMomentValues)
        {
            DecodeMomentValues(name, block, metadata);
        }
    }

    private static string ReadDataBlockName(ReadOnlySpan<byte> block) =>
        Encoding.ASCII.GetString(block.Slice(1, 3)).TrimEnd('\0', ' ');

    private static Type31MomentMetadata ReadMomentMetadata(ReadOnlySpan<byte> block) =>
        new(
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentGateCountOffset, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentFirstGateRangeOffset, 2)) / 1_000f,
            BinaryPrimitives.ReadUInt16BigEndian(block.Slice(GenericMomentGateSpacingOffset, 2)) / 1_000f,
            block[GenericMomentWordSizeOffset],
            ReadSingleBigEndian(block.Slice(GenericMomentScaleOffset, 4)),
            ReadSingleBigEndian(block.Slice(GenericMomentOffsetOffset, 4)));

    private static float ReadSingleBigEndian(ReadOnlySpan<byte> buffer) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(buffer));

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

    private sealed class MomentAccumulator
    {
        private bool hasMetadata;

        public int RadialCount { get; private set; }

        public long GateCount { get; private set; }

        public int MinimumGateCount { get; private set; }

        public int MaximumGateCount { get; private set; }

        public int MinimumWordSizeBits { get; private set; }

        public int MaximumWordSizeBits { get; private set; }

        public float MinimumFirstGateRangeKilometers { get; private set; }

        public float MaximumFirstGateRangeKilometers { get; private set; }

        public float MinimumGateSpacingKilometers { get; private set; }

        public float MaximumGateSpacingKilometers { get; private set; }

        public float MinimumScale { get; private set; }

        public float MaximumScale { get; private set; }

        public float MinimumOffset { get; private set; }

        public float MaximumOffset { get; private set; }

        public void Add(Type31MomentMetadata metadata)
        {
            RadialCount++;
            GateCount += metadata.GateCount;
            AcceptMetadata(
                metadata.GateCount,
                metadata.WordSizeBits,
                metadata.FirstGateRangeKilometers,
                metadata.GateSpacingKilometers,
                metadata.Scale,
                metadata.Offset);
        }

        public void Add(ArchiveTwoMomentSummary summary)
        {
            RadialCount += summary.RadialCount;
            GateCount += summary.GateCount;
            AcceptMetadata(
                summary.MinimumGateCount,
                summary.MinimumWordSizeBits,
                summary.MinimumFirstGateRangeKilometers,
                summary.MinimumGateSpacingKilometers,
                summary.MinimumScale,
                summary.MinimumOffset);

            AcceptMetadata(
                summary.MaximumGateCount,
                summary.MaximumWordSizeBits,
                summary.MaximumFirstGateRangeKilometers,
                summary.MaximumGateSpacingKilometers,
                summary.MaximumScale,
                summary.MaximumOffset);
        }

        private void AcceptMetadata(
            int gateCount,
            int wordSizeBits,
            float firstGateRangeKilometers,
            float gateSpacingKilometers,
            float scale,
            float offset)
        {
            if (!hasMetadata)
            {
                MinimumGateCount = gateCount;
                MaximumGateCount = gateCount;
                MinimumWordSizeBits = wordSizeBits;
                MaximumWordSizeBits = wordSizeBits;
                MinimumFirstGateRangeKilometers = firstGateRangeKilometers;
                MaximumFirstGateRangeKilometers = firstGateRangeKilometers;
                MinimumGateSpacingKilometers = gateSpacingKilometers;
                MaximumGateSpacingKilometers = gateSpacingKilometers;
                MinimumScale = scale;
                MaximumScale = scale;
                MinimumOffset = offset;
                MaximumOffset = offset;
                hasMetadata = true;
                return;
            }

            MinimumGateCount = Math.Min(MinimumGateCount, gateCount);
            MaximumGateCount = Math.Max(MaximumGateCount, gateCount);
            MinimumWordSizeBits = Math.Min(MinimumWordSizeBits, wordSizeBits);
            MaximumWordSizeBits = Math.Max(MaximumWordSizeBits, wordSizeBits);
            MinimumFirstGateRangeKilometers = Math.Min(MinimumFirstGateRangeKilometers, firstGateRangeKilometers);
            MaximumFirstGateRangeKilometers = Math.Max(MaximumFirstGateRangeKilometers, firstGateRangeKilometers);
            MinimumGateSpacingKilometers = Math.Min(MinimumGateSpacingKilometers, gateSpacingKilometers);
            MaximumGateSpacingKilometers = Math.Max(MaximumGateSpacingKilometers, gateSpacingKilometers);
            MinimumScale = Math.Min(MinimumScale, scale);
            MaximumScale = Math.Max(MaximumScale, scale);
            MinimumOffset = Math.Min(MinimumOffset, offset);
            MaximumOffset = Math.Max(MaximumOffset, offset);
        }
    }

    private readonly record struct Type31MomentMetadata(
        int GateCount,
        float FirstGateRangeKilometers,
        float GateSpacingKilometers,
        int WordSizeBits,
        float Scale,
        float Offset);

    private readonly record struct Type31RadialMetadata(
        int RadialStatus,
        int ElevationNumber,
        int CutSectorNumber,
        float ElevationAngleDegrees);

    private sealed class SweepAccumulator
    {
        private readonly SortedSet<string> moments = new(StringComparer.Ordinal);
        private double elevationAngleTotal;

        public SweepAccumulator(
            int sequenceNumber,
            int elevationNumber,
            int startRadialStatus,
            ArchiveTwoRadialSourceOrder firstRadial)
        {
            SequenceNumber = sequenceNumber;
            ElevationNumber = elevationNumber;
            StartRadialStatus = startRadialStatus;
            EndRadialStatus = startRadialStatus;
            FirstRadial = firstRadial;
            LastRadial = firstRadial;
        }

        public int SequenceNumber { get; }

        public int ElevationNumber { get; }

        public int MinimumCutSectorNumber { get; private set; }

        public int MaximumCutSectorNumber { get; private set; }

        public int RadialCount { get; private set; }

        public int StartRadialStatus { get; }

        public int EndRadialStatus { get; private set; }

        public float MinimumElevationAngleDegrees { get; private set; }

        public float MaximumElevationAngleDegrees { get; private set; }

        public int VolumeConstantBlockCount { get; private set; }

        public int ElevationConstantBlockCount { get; private set; }

        public int RadialConstantBlockCount { get; private set; }

        public ArchiveTwoRadialSourceOrder FirstRadial { get; }

        public ArchiveTwoRadialSourceOrder LastRadial { get; private set; }

        public static SweepAccumulator FromSummary(int sequenceNumber, ArchiveTwoSweepSummary summary)
        {
            var accumulator = new SweepAccumulator(
                sequenceNumber,
                summary.ElevationNumber,
                summary.StartRadialStatus,
                summary.FirstRadial)
            {
                RadialCount = summary.RadialCount,
                MinimumCutSectorNumber = summary.MinimumCutSectorNumber,
                MaximumCutSectorNumber = summary.MaximumCutSectorNumber,
                EndRadialStatus = summary.EndRadialStatus,
                MinimumElevationAngleDegrees = summary.MinimumElevationAngleDegrees,
                MaximumElevationAngleDegrees = summary.MaximumElevationAngleDegrees,
                elevationAngleTotal = summary.AverageElevationAngleDegrees * summary.RadialCount,
                VolumeConstantBlockCount = summary.VolumeConstantBlockCount,
                ElevationConstantBlockCount = summary.ElevationConstantBlockCount,
                RadialConstantBlockCount = summary.RadialConstantBlockCount,
                LastRadial = summary.LastRadial
            };

            foreach (var moment in summary.Moments)
            {
                accumulator.moments.Add(moment);
            }

            return accumulator;
        }

        public void AcceptRadial(Type31RadialMetadata radial, ArchiveTwoRadialSourceOrder sourceOrder)
        {
            if (RadialCount == 0)
            {
                MinimumCutSectorNumber = radial.CutSectorNumber;
                MaximumCutSectorNumber = radial.CutSectorNumber;
                MinimumElevationAngleDegrees = radial.ElevationAngleDegrees;
                MaximumElevationAngleDegrees = radial.ElevationAngleDegrees;
            }
            else
            {
                MinimumCutSectorNumber = Math.Min(MinimumCutSectorNumber, radial.CutSectorNumber);
                MaximumCutSectorNumber = Math.Max(MaximumCutSectorNumber, radial.CutSectorNumber);
                MinimumElevationAngleDegrees = Math.Min(MinimumElevationAngleDegrees, radial.ElevationAngleDegrees);
                MaximumElevationAngleDegrees = Math.Max(MaximumElevationAngleDegrees, radial.ElevationAngleDegrees);
            }

            RadialCount++;
            EndRadialStatus = radial.RadialStatus;
            LastRadial = sourceOrder;
            elevationAngleTotal += radial.ElevationAngleDegrees;
        }

        public void AcceptVolumeConstantBlock() => VolumeConstantBlockCount++;

        public void AcceptElevationConstantBlock() => ElevationConstantBlockCount++;

        public void AcceptRadialConstantBlock() => RadialConstantBlockCount++;

        public void AcceptMoment(string name) => moments.Add(name);

        public ArchiveTwoSweepSummary ToSummary() =>
            new(
                SequenceNumber,
                ElevationNumber,
                MinimumCutSectorNumber,
                MaximumCutSectorNumber,
                RadialCount,
                StartRadialStatus,
                EndRadialStatus,
                MinimumElevationAngleDegrees,
                MaximumElevationAngleDegrees,
                RadialCount == 0 ? 0 : (float)(elevationAngleTotal / RadialCount),
                VolumeConstantBlockCount,
                ElevationConstantBlockCount,
                RadialConstantBlockCount,
                moments.ToArray(),
                FirstRadial,
                LastRadial);
    }
}

using System.Buffers.Binary;
using System.Text;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Builds aggregate Archive II message summaries from decompressed RDA/RPG messages.
/// </summary>
/// <remarks>
/// The builder can cheaply count message and type 31 radial structure, or additionally decode raw and calibrated
/// moment values when benchmark and inspection workflows need deterministic value totals.
/// </remarks>
public sealed partial class ArchiveTwoMessageSummaryBuilder : IArchiveTwoMessageConsumer
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

    /// <summary>
    /// Creates a message summary builder with optional moment-value and sweep-summary collection.
    /// </summary>
    public ArchiveTwoMessageSummaryBuilder(
        bool decodeMomentValues = false,
        bool collectSweepSummaries = true,
        bool decodeCalibratedMomentValues = false)
    {
        this.decodeMomentValues = decodeMomentValues || decodeCalibratedMomentValues;
        this.collectSweepSummaries = collectSweepSummaries;
        this.decodeCalibratedMomentValues = decodeCalibratedMomentValues;
    }

    /// <summary>
    /// Gets the number of RDA/RPG messages accepted by the builder.
    /// </summary>
    public int MessageCount => messageCount;

    /// <summary>
    /// Gets the number of type 31 radials accepted by the builder.
    /// </summary>
    public int Type31RadialCount => type31RadialCount;

    /// <summary>
    /// Gets the estimated gate-moment event count represented by accepted type 31 moment blocks.
    /// </summary>
    public long EstimatedGateMomentEventCount => estimatedGateMomentEvents;

    /// <summary>
    /// Gets the number of decoded raw gate-moment values.
    /// </summary>
    public long DecodedGateMomentValueCount => decodedGateMomentValues;

    /// <summary>
    /// Gets the deterministic checksum over decoded raw gate-moment values.
    /// </summary>
    public ulong DecodedGateMomentValueChecksum => decodedGateMomentValueChecksum;

    /// <summary>
    /// Gets the number of decoded calibrated gate-moment values.
    /// </summary>
    public long CalibratedGateMomentValueCount => calibratedGateMomentValues;

    /// <summary>
    /// Gets the number of below-threshold decoded gate values.
    /// </summary>
    public long BelowThresholdGateMomentValueCount => belowThresholdGateMomentValues;

    /// <summary>
    /// Gets the number of range-folded decoded gate values.
    /// </summary>
    public long RangeFoldedGateMomentValueCount => rangeFoldedGateMomentValues;

    /// <summary>
    /// Gets the number of clutter-filter-not-applied decoded gate values.
    /// </summary>
    public long ClutterFilterNotAppliedGateMomentValueCount => clutterFilterNotAppliedGateMomentValues;

    /// <summary>
    /// Gets the number of point-clutter-filter-applied decoded gate values.
    /// </summary>
    public long PointClutterFilterAppliedGateMomentValueCount => pointClutterFilterAppliedGateMomentValues;

    /// <summary>
    /// Gets the number of dual-polarization-filtered decoded gate values.
    /// </summary>
    public long DualPolarizationFilteredGateMomentValueCount => dualPolarizationFilteredGateMomentValues;

    /// <summary>
    /// Gets the number of reserved decoded gate values.
    /// </summary>
    public long ReservedGateMomentValueCount => reservedGateMomentValues;

    /// <summary>
    /// Gets the number of unsupported calibrated gate values.
    /// </summary>
    public long UnsupportedCalibratedGateMomentValueCount => unsupportedCalibratedGateMomentValues;

    /// <summary>
    /// Gets a deterministic checksum over calibrated values scaled to thousandths.
    /// </summary>
    public long CalibratedGateMomentValueScaledChecksum => calibratedGateMomentValueScaledChecksum;

    /// <summary>
    /// Gets the minimum calibrated gate-moment value observed.
    /// </summary>
    public double MinimumCalibratedGateMomentValue => minimumCalibratedGateMomentValue;

    /// <summary>
    /// Gets the maximum calibrated gate-moment value observed.
    /// </summary>
    public double MaximumCalibratedGateMomentValue => maximumCalibratedGateMomentValue;

    /// <summary>
    /// Clears accumulated summary state for reuse.
    /// </summary>
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

    /// <summary>
    /// Adds a previously built summary into the current accumulator.
    /// </summary>
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

    /// <summary>
    /// Accepts one complete RDA/RPG message without source-order metadata.
    /// </summary>
    public void AcceptMessage(ReadOnlySpan<byte> message) =>
        AcceptMessage(message, default);

    /// <inheritdoc />
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

    /// <summary>
    /// Builds an immutable summary from the accumulated message and type 31 state.
    /// </summary>
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

}

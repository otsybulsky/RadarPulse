using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

/// <summary>
/// Projects Archive II type 31 generic moment blocks into gate-moment replay events.
/// </summary>
/// <remarks>
/// The projector keeps sweep and radial sequence state across messages so sequential and parallel record replay can
/// reconstruct stable chronology.
/// </remarks>
public sealed partial class ArchiveTwoGateMomentEventProjector : IArchiveTwoMessageConsumer
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

    private Action<ArchiveTwoGateMomentEvent> acceptEvent = _ => { };
    private int radialSequenceNumber;
    private int currentSweepSequenceNumber;
    private int currentSweepElevationNumber;
    private int currentSweepRadialCount;

    /// <summary>
    /// Creates a projector for one Archive II volume.
    /// </summary>
    public ArchiveTwoGateMomentEventProjector(
        string radarId,
        DateTimeOffset volumeTimestamp,
        Action<ArchiveTwoGateMomentEvent> acceptEvent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);
        Reset(radarId, volumeTimestamp, acceptEvent, default);
    }

    /// <summary>
    /// Gets the radar id assigned to projected events.
    /// </summary>
    public string RadarId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the volume timestamp assigned to projected events.
    /// </summary>
    public DateTimeOffset VolumeTimestamp { get; private set; }

    /// <summary>
    /// Resets only continuing projection sequence state.
    /// </summary>
    public void Reset() => Reset(default);

    internal void Reset(ArchiveTwoGateMomentProjectorState state)
    {
        radialSequenceNumber = state.RadialSequenceNumber;
        currentSweepSequenceNumber = state.CurrentSweepSequenceNumber;
        currentSweepElevationNumber = state.CurrentSweepElevationNumber;
        currentSweepRadialCount = state.CurrentSweepRadialCount;
    }

    internal void Reset(
        string radarId,
        DateTimeOffset volumeTimestamp,
        Action<ArchiveTwoGateMomentEvent> acceptEvent,
        ArchiveTwoGateMomentProjectorState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(radarId);
        RadarId = radarId;
        VolumeTimestamp = volumeTimestamp;
        this.acceptEvent = acceptEvent ?? throw new ArgumentNullException(nameof(acceptEvent));
        Reset(state);
    }

    /// <inheritdoc />
    public void AcceptMessage(ReadOnlySpan<byte> message, ArchiveTwoMessageSource source)
    {
        if (message.Length < MessageHeaderLength || message[3] != 31)
        {
            return;
        }

        ParseType31(message[MessageHeaderLength..], source);
    }
}

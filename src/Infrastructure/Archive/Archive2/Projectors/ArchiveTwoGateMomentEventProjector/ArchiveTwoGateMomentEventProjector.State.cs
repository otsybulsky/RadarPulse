namespace RadarPulse.Infrastructure.Archive;

public sealed partial class ArchiveTwoGateMomentEventProjector
{
    private int AcceptRadial(Type31RadialMetadata radial)
    {
        var nextState = AdvanceState(
            new ArchiveTwoGateMomentProjectorState(
                radialSequenceNumber,
                currentSweepSequenceNumber,
                currentSweepElevationNumber,
                currentSweepRadialCount),
            radial.RadialStatus,
            radial.ElevationNumber,
            out var sweepSequenceNumber);
        Reset(nextState);
        return sweepSequenceNumber;
    }

    internal static ArchiveTwoGateMomentProjectorState AdvanceState(
        ArchiveTwoGateMomentProjectorState state,
        int radialStatus,
        int elevationNumber,
        out int sweepSequenceNumber)
    {
        var radialSequenceNumber = state.RadialSequenceNumber + 1;
        var currentSweepSequenceNumber = state.CurrentSweepSequenceNumber;
        var currentSweepElevationNumber = state.CurrentSweepElevationNumber;
        var currentSweepRadialCount = state.CurrentSweepRadialCount;

        if (currentSweepSequenceNumber == 0 ||
            (currentSweepRadialCount > 0 &&
                (IsStartRadialStatus(radialStatus) ||
                    elevationNumber != currentSweepElevationNumber)))
        {
            currentSweepSequenceNumber++;
            currentSweepElevationNumber = elevationNumber;
            currentSweepRadialCount = 0;
        }

        currentSweepRadialCount++;
        sweepSequenceNumber = currentSweepSequenceNumber;
        return new ArchiveTwoGateMomentProjectorState(
            radialSequenceNumber,
            currentSweepSequenceNumber,
            currentSweepElevationNumber,
            currentSweepRadialCount);
    }

    private static bool IsStartRadialStatus(int radialStatus) =>
        radialStatus is 0 or 3 or 5 or 80 or 83 or 85;
}

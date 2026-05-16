namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingTopologyMoveResult
{
    private RadarProcessingTopologyMoveResult(
        bool succeeded,
        RadarProcessingTopologyMoveError error,
        RadarProcessingTopology previousTopology,
        RadarProcessingTopology currentTopology,
        RadarProcessingTopologyMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(previousTopology);
        ArgumentNullException.ThrowIfNull(currentTopology);

        if (succeeded && error != RadarProcessingTopologyMoveError.None)
        {
            throw new ArgumentException(
                "Successful topology move results must not carry an error.",
                nameof(error));
        }

        if (!succeeded && error == RadarProcessingTopologyMoveError.None)
        {
            throw new ArgumentException(
                "Rejected topology move results must carry an error.",
                nameof(error));
        }

        Succeeded = succeeded;
        Error = error;
        PreviousTopology = previousTopology;
        CurrentTopology = currentTopology;
        Request = request;
    }

    public bool Succeeded { get; }

    public RadarProcessingTopologyMoveError Error { get; }

    public RadarProcessingTopology PreviousTopology { get; }

    public RadarProcessingTopology CurrentTopology { get; }

    public RadarProcessingTopologyMoveRequest Request { get; }

    public static RadarProcessingTopologyMoveResult Accepted(
        RadarProcessingTopology previousTopology,
        RadarProcessingTopology currentTopology,
        RadarProcessingTopologyMoveRequest request) =>
        new(
            succeeded: true,
            RadarProcessingTopologyMoveError.None,
            previousTopology,
            currentTopology,
            request);

    public static RadarProcessingTopologyMoveResult Rejected(
        RadarProcessingTopologyMoveError error,
        RadarProcessingTopology currentTopology,
        RadarProcessingTopologyMoveRequest request) =>
        new(
            succeeded: false,
            error,
            currentTopology,
            currentTopology,
            request);
}

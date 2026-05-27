namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of attempting to publish a partition owner move.
/// </summary>
/// <remarks>
/// Rejected results keep previous and current topology references equal. Accepted
/// results expose the previous snapshot and the newly published snapshot so
/// validation can assert monotonic versioning and single-partition movement.
/// </remarks>
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

    /// <summary>
    /// Indicates whether the move was published.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Rejection reason, or <see cref="RadarProcessingTopologyMoveError.None"/> on success.
    /// </summary>
    public RadarProcessingTopologyMoveError Error { get; }

    /// <summary>
    /// Topology before the attempted move.
    /// </summary>
    public RadarProcessingTopology PreviousTopology { get; }

    /// <summary>
    /// Current topology after the attempt.
    /// </summary>
    public RadarProcessingTopology CurrentTopology { get; }

    /// <summary>
    /// Move request that produced the result.
    /// </summary>
    public RadarProcessingTopologyMoveRequest Request { get; }

    /// <summary>
    /// Creates an accepted move result.
    /// </summary>
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

    /// <summary>
    /// Creates a rejected move result with the current topology unchanged.
    /// </summary>
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

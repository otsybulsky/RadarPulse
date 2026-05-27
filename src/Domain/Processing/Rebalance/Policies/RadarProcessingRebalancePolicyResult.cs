namespace RadarPulse.Domain.Processing;

/// <summary>
/// Decision from evaluating a candidate move against rebalance policy.
/// </summary>
public sealed class RadarProcessingRebalancePolicyResult
{
    private readonly IReadOnlyList<RadarProcessingRebalancePolicyRejection> rejections;

    private RadarProcessingRebalancePolicyResult(
        RadarProcessingRebalanceMovePolicyInput input,
        RadarProcessingRebalancePolicyRejection[] rejections)
    {
        ArgumentNullException.ThrowIfNull(rejections);

        Input = input;
        this.rejections = rejections.Length == 0
            ? Array.Empty<RadarProcessingRebalancePolicyRejection>()
            : Array.AsReadOnly((RadarProcessingRebalancePolicyRejection[])rejections.Clone());
    }

    /// <summary>
    /// Candidate input that was evaluated.
    /// </summary>
    public RadarProcessingRebalanceMovePolicyInput Input { get; }

    /// <summary>
    /// Policy rejections that blocked the move.
    /// </summary>
    public IReadOnlyList<RadarProcessingRebalancePolicyRejection> Rejections => rejections;

    /// <summary>
    /// Indicates whether no policy rejection was recorded.
    /// </summary>
    public bool IsAllowed => rejections.Count == 0;

    /// <summary>
    /// Creates an allowed policy result.
    /// </summary>
    public static RadarProcessingRebalancePolicyResult Allowed(
        RadarProcessingRebalanceMovePolicyInput input) =>
        new(input, Array.Empty<RadarProcessingRebalancePolicyRejection>());

    /// <summary>
    /// Creates a rejected policy result with one or more explicit reasons.
    /// </summary>
    public static RadarProcessingRebalancePolicyResult Rejected(
        RadarProcessingRebalanceMovePolicyInput input,
        IReadOnlyCollection<RadarProcessingRebalancePolicyRejection> rejections)
    {
        ArgumentNullException.ThrowIfNull(rejections);

        if (rejections.Count == 0)
        {
            throw new ArgumentException("Rejected policy results must include at least one rejection.", nameof(rejections));
        }

        return new RadarProcessingRebalancePolicyResult(input, rejections.ToArray());
    }
}

namespace RadarPulse.Domain.Processing;

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

    public RadarProcessingRebalanceMovePolicyInput Input { get; }

    public IReadOnlyList<RadarProcessingRebalancePolicyRejection> Rejections => rejections;

    public bool IsAllowed => rejections.Count == 0;

    public static RadarProcessingRebalancePolicyResult Allowed(
        RadarProcessingRebalanceMovePolicyInput input) =>
        new(input, Array.Empty<RadarProcessingRebalancePolicyRejection>());

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

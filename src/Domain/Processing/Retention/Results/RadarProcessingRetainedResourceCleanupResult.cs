namespace RadarPulse.Domain.Processing;

/// <summary>
/// Aggregate result from releasing a set of retained resources.
/// </summary>
public sealed record RadarProcessingRetainedResourceCleanupResult
{
    /// <summary>
    /// Empty cleanup result.
    /// </summary>
    public static RadarProcessingRetainedResourceCleanupResult Empty { get; } = new();

    /// <summary>
    /// Creates a cleanup result from release results.
    /// </summary>
    public RadarProcessingRetainedResourceCleanupResult(
        IReadOnlyCollection<RadarProcessingRetainedPayloadReleaseResult>? releaseResults = null)
    {
        ReleaseResults = CopyRequired(
            releaseResults ?? Array.Empty<RadarProcessingRetainedPayloadReleaseResult>(),
            nameof(releaseResults));

        foreach (var result in ReleaseResults)
        {
            switch (result.Status)
            {
                case RadarProcessingRetainedPayloadReleaseStatus.Released:
                    ReleasedCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.AlreadyReleased:
                    AlreadyReleasedCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.Failed:
                    FailedCount++;
                    break;

                case RadarProcessingRetainedPayloadReleaseStatus.NotRequired:
                    NotRequiredCount++;
                    break;

                default:
                    RadarProcessingRetainedPayloadReleaseResult.EnsureKnownStatus(result.Status);
                    throw new ArgumentOutOfRangeException(nameof(releaseResults));
            }
        }
    }

    /// <summary>
    /// Individual release results included in the cleanup.
    /// </summary>
    public IReadOnlyList<RadarProcessingRetainedPayloadReleaseResult> ReleaseResults { get; }

    /// <summary>
    /// Number of release attempts.
    /// </summary>
    public long ReleaseAttemptCount => ReleaseResults.Count;

    /// <summary>
    /// Number of resources released.
    /// </summary>
    public long ReleasedCount { get; }

    /// <summary>
    /// Number of resources already released before cleanup.
    /// </summary>
    public long AlreadyReleasedCount { get; }

    /// <summary>
    /// Number of release failures.
    /// </summary>
    public long FailedCount { get; }

    /// <summary>
    /// Number of resources that required no release action.
    /// </summary>
    public long NotRequiredCount { get; }

    /// <summary>
    /// Indicates whether cleanup completed without release failures.
    /// </summary>
    public bool IsSuccessful => FailedCount == 0;

    /// <summary>
    /// Releases all resources and returns an aggregate cleanup result.
    /// </summary>
    public static RadarProcessingRetainedResourceCleanupResult ReleaseAll(
        IEnumerable<RadarProcessingRetainedBatchResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var results = new List<RadarProcessingRetainedPayloadReleaseResult>();
        foreach (var resource in resources)
        {
            ArgumentNullException.ThrowIfNull(resource);
            results.Add(resource.Release());
        }

        return new RadarProcessingRetainedResourceCleanupResult(results);
    }

    private static IReadOnlyList<T> CopyRequired<T>(
        IReadOnlyCollection<T> values,
        string paramName)
        where T : class
    {
        if (values.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(values.Count);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }

            result.Add(value);
        }

        return Array.AsReadOnly(result.ToArray());
    }
}

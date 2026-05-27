namespace RadarPulse.Domain.Processing;

public sealed record RadarProcessingRetainedResourceCleanupResult
{
    public static RadarProcessingRetainedResourceCleanupResult Empty { get; } = new();

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

    public IReadOnlyList<RadarProcessingRetainedPayloadReleaseResult> ReleaseResults { get; }

    public long ReleaseAttemptCount => ReleaseResults.Count;

    public long ReleasedCount { get; }

    public long AlreadyReleasedCount { get; }

    public long FailedCount { get; }

    public long NotRequiredCount { get; }

    public bool IsSuccessful => FailedCount == 0;

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

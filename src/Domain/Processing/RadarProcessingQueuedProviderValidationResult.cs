namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingQueuedProviderValidationResult
{
    private RadarProcessingQueuedProviderValidationResult(
        bool isValid,
        RadarProcessingQueuedProviderValidationError error,
        string message,
        RadarProcessingQueuedProviderValidationProfile profile,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null)
    {
        EnsureKnownError(error);
        RadarProcessingQueuedProviderValidator.EnsureKnownProfile(profile);
        ArgumentNullException.ThrowIfNull(message);

        if (isValid && error != RadarProcessingQueuedProviderValidationError.None)
        {
            throw new ArgumentException("Valid queued provider validation results must not carry an error.", nameof(error));
        }

        if (!isValid && error == RadarProcessingQueuedProviderValidationError.None)
        {
            throw new ArgumentException("Invalid queued provider validation results must carry an error.", nameof(error));
        }

        if (isValid && !string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Valid queued provider validation results must not carry diagnostics.", nameof(message));
        }

        if (!isValid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        IsValid = isValid;
        Error = error;
        Message = message;
        Profile = profile;
        ExpectedChecksum = expectedChecksum;
        ActualChecksum = actualChecksum;
        ExpectedCount = expectedCount;
        ActualCount = actualCount;
    }

    public bool IsValid { get; }

    public RadarProcessingQueuedProviderValidationError Error { get; }

    public string Message { get; }

    public RadarProcessingQueuedProviderValidationProfile Profile { get; }

    public ulong? ExpectedChecksum { get; }

    public ulong? ActualChecksum { get; }

    public long? ExpectedCount { get; }

    public long? ActualCount { get; }

    public static RadarProcessingQueuedProviderValidationResult Valid(
        RadarProcessingQueuedProviderValidationProfile profile) =>
        new(
            isValid: true,
            RadarProcessingQueuedProviderValidationError.None,
            string.Empty,
            profile);

    public static RadarProcessingQueuedProviderValidationResult Invalid(
        RadarProcessingQueuedProviderValidationError error,
        string message,
        RadarProcessingQueuedProviderValidationProfile profile,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null) =>
        new(
            isValid: false,
            error,
            message,
            profile,
            expectedChecksum,
            actualChecksum,
            expectedCount,
            actualCount);

    internal static void EnsureKnownError(
        RadarProcessingQueuedProviderValidationError error)
    {
        if (error is not RadarProcessingQueuedProviderValidationError.None and
            not RadarProcessingQueuedProviderValidationError.NonOwnedQueuedBatch and
            not RadarProcessingQueuedProviderValidationError.ProviderSequenceRegression and
            not RadarProcessingQueuedProviderValidationError.ProcessingSequenceRegression and
            not RadarProcessingQueuedProviderValidationError.MissingCompletionForAcceptedBatch and
            not RadarProcessingQueuedProviderValidationError.TopologyVersionRegression and
            not RadarProcessingQueuedProviderValidationError.TelemetryCounterMismatch and
            not RadarProcessingQueuedProviderValidationError.QueueFaultStateMismatch and
            not RadarProcessingQueuedProviderValidationError.WorkerFailureCountMismatch and
            not RadarProcessingQueuedProviderValidationError.DeterministicChecksumMismatch and
            not RadarProcessingQueuedProviderValidationError.AcceptedMoveCountMismatch and
            not RadarProcessingQueuedProviderValidationError.SkippedDecisionCountMismatch and
            not RadarProcessingQueuedProviderValidationError.FailureCountMismatch and
            not RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}

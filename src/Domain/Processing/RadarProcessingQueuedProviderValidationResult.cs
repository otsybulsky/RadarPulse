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
        long? actualCount = null,
        RadarProcessingQueuedProviderValidationSurface? semanticSurface = null,
        RadarProcessingQueuedProviderOverlapMode? overlapMode = null,
        RadarProcessingRetainedPayloadStrategy? retentionStrategy = null)
    {
        EnsureKnownError(error);
        RadarProcessingQueuedProviderValidator.EnsureKnownProfile(profile);
        ArgumentNullException.ThrowIfNull(message);
        if (semanticSurface.HasValue)
        {
            RadarProcessingQueuedProviderValidationContext.EnsureKnownSurface(semanticSurface.Value);
        }

        if (overlapMode.HasValue)
        {
            RadarProcessingQueuedProviderValidationContext.EnsureKnownOverlapMode(overlapMode.Value);
        }

        if (retentionStrategy.HasValue)
        {
            RadarProcessingRetainedPayloadOptions.EnsureKnownStrategy(retentionStrategy.Value);
        }

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
        SemanticSurface = semanticSurface;
        OverlapMode = overlapMode;
        RetentionStrategy = retentionStrategy;
    }

    public bool IsValid { get; }

    public RadarProcessingQueuedProviderValidationError Error { get; }

    public string Message { get; }

    public RadarProcessingQueuedProviderValidationProfile Profile { get; }

    public ulong? ExpectedChecksum { get; }

    public ulong? ActualChecksum { get; }

    public long? ExpectedCount { get; }

    public long? ActualCount { get; }

    public RadarProcessingQueuedProviderValidationSurface? SemanticSurface { get; }

    public RadarProcessingQueuedProviderOverlapMode? OverlapMode { get; }

    public RadarProcessingRetainedPayloadStrategy? RetentionStrategy { get; }

    public static RadarProcessingQueuedProviderValidationResult Valid(
        RadarProcessingQueuedProviderValidationProfile profile,
        RadarProcessingQueuedProviderValidationContext? context = null) =>
        new(
            isValid: true,
            RadarProcessingQueuedProviderValidationError.None,
            string.Empty,
            profile,
            semanticSurface: context?.SemanticSurface,
            overlapMode: context?.OverlapMode,
            retentionStrategy: context?.RetentionStrategy);

    public static RadarProcessingQueuedProviderValidationResult Invalid(
        RadarProcessingQueuedProviderValidationError error,
        string message,
        RadarProcessingQueuedProviderValidationProfile profile,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null,
        RadarProcessingQueuedProviderValidationContext? context = null) =>
        new(
            isValid: false,
            error,
            message,
            profile,
            expectedChecksum,
            actualChecksum,
            expectedCount,
            actualCount,
            context?.SemanticSurface,
            context?.OverlapMode,
            context?.RetentionStrategy);

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
            not RadarProcessingQueuedProviderValidationError.FinalTopologyVersionMismatch and
            not RadarProcessingQueuedProviderValidationError.ProviderSequenceGap and
            not RadarProcessingQueuedProviderValidationError.ProcessingSequenceGap and
            not RadarProcessingQueuedProviderValidationError.PayloadValueCountMismatch and
            not RadarProcessingQueuedProviderValidationError.FailedMigrationCountMismatch and
            not RadarProcessingQueuedProviderValidationError.ReferenceSemanticSurfaceMismatch and
            not RadarProcessingQueuedProviderValidationError.RetentionTelemetryIncomplete and
            not RadarProcessingQueuedProviderValidationError.RetentionTelemetryMismatch and
            not RadarProcessingQueuedProviderValidationError.RetainedResourceCleanupIncomplete and
            not RadarProcessingQueuedProviderValidationError.OverlapTelemetryIncomplete)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}

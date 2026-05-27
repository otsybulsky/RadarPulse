namespace RadarPulse.Domain.Processing;

/// <summary>
/// Semantic validation result for a queued-provider session.
/// </summary>
/// <remarks>
/// Valid results carry no diagnostics. Invalid results must carry a concrete
/// error and message, with optional expected/actual values for deterministic
/// checksum or count mismatches.
/// </remarks>
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

    /// <summary>
    /// Indicates whether queued-provider evidence passed validation.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Validation error classification.
    /// </summary>
    public RadarProcessingQueuedProviderValidationError Error { get; }

    /// <summary>
    /// Diagnostic message for invalid validation results.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Validation depth used to produce the result.
    /// </summary>
    public RadarProcessingQueuedProviderValidationProfile Profile { get; }

    /// <summary>
    /// Expected checksum for checksum validations.
    /// </summary>
    public ulong? ExpectedChecksum { get; }

    /// <summary>
    /// Actual checksum for checksum validations.
    /// </summary>
    public ulong? ActualChecksum { get; }

    /// <summary>
    /// Expected count for count validations.
    /// </summary>
    public long? ExpectedCount { get; }

    /// <summary>
    /// Actual count for count validations.
    /// </summary>
    public long? ActualCount { get; }

    /// <summary>
    /// Semantic surface used by validation when available.
    /// </summary>
    public RadarProcessingQueuedProviderValidationSurface? SemanticSurface { get; }

    /// <summary>
    /// Overlap mode used by validation when available.
    /// </summary>
    public RadarProcessingQueuedProviderOverlapMode? OverlapMode { get; }

    /// <summary>
    /// Retention strategy used by validation when available.
    /// </summary>
    public RadarProcessingRetainedPayloadStrategy? RetentionStrategy { get; }

    /// <summary>
    /// Creates a valid validation result.
    /// </summary>
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

    /// <summary>
    /// Creates an invalid validation result with optional expected/actual evidence.
    /// </summary>
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

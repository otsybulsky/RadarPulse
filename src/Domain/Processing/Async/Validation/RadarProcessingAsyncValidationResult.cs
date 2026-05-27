namespace RadarPulse.Domain.Processing;

/// <summary>
/// Reports async processing validation status and optional deterministic comparison checksums.
/// </summary>
public sealed class RadarProcessingAsyncValidationResult
{
    private RadarProcessingAsyncValidationResult(
        bool isValid,
        RadarProcessingAsyncValidationError error,
        string message,
        RadarProcessingValidationProfile validationProfile,
        ulong? synchronousChecksum,
        ulong? asyncChecksum)
    {
        RadarProcessingRebalanceHardeningOptions.EnsureKnownValidationProfile(validationProfile);
        EnsureKnownError(error);

        if (isValid && error != RadarProcessingAsyncValidationError.None)
        {
            throw new ArgumentException("Valid async validation results cannot carry an error.", nameof(error));
        }

        if (!isValid && error == RadarProcessingAsyncValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Invalid async validation results require an error.");
        }

        if (!isValid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        IsValid = isValid;
        Error = error;
        Message = isValid ? string.Empty : message;
        ValidationProfile = validationProfile;
        SynchronousChecksum = synchronousChecksum;
        AsyncChecksum = asyncChecksum;
    }

    /// <summary>
    /// Gets whether async validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the async validation error, or <see cref="RadarProcessingAsyncValidationError.None"/>.
    /// </summary>
    public RadarProcessingAsyncValidationError Error { get; }

    /// <summary>
    /// Gets the diagnostic message for invalid results.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the validation profile used to produce the result.
    /// </summary>
    public RadarProcessingValidationProfile ValidationProfile { get; }

    /// <summary>
    /// Gets the synchronous reference checksum when benchmark comparison was run.
    /// </summary>
    public ulong? SynchronousChecksum { get; }

    /// <summary>
    /// Gets the async processing checksum when benchmark comparison was run.
    /// </summary>
    public ulong? AsyncChecksum { get; }

    /// <summary>
    /// Gets whether both deterministic comparison checksums are available.
    /// </summary>
    public bool HasComparisonChecksums =>
        SynchronousChecksum.HasValue &&
        AsyncChecksum.HasValue;

    /// <summary>
    /// Creates a successful async validation result.
    /// </summary>
    public static RadarProcessingAsyncValidationResult Valid(
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic,
        ulong? synchronousChecksum = null,
        ulong? asyncChecksum = null) =>
        new(
            true,
            RadarProcessingAsyncValidationError.None,
            string.Empty,
            validationProfile,
            synchronousChecksum,
            asyncChecksum);

    /// <summary>
    /// Creates a failed async validation result with an error and diagnostic message.
    /// </summary>
    public static RadarProcessingAsyncValidationResult Invalid(
        RadarProcessingAsyncValidationError error,
        string message,
        RadarProcessingValidationProfile validationProfile = RadarProcessingValidationProfile.Diagnostic,
        ulong? synchronousChecksum = null,
        ulong? asyncChecksum = null) =>
        new(
            false,
            error,
            message,
            validationProfile,
            synchronousChecksum,
            asyncChecksum);

    internal static void EnsureKnownError(
        RadarProcessingAsyncValidationError error)
    {
        if (error is not RadarProcessingAsyncValidationError.None and
            not RadarProcessingAsyncValidationError.NonAsyncExecutionMode and
            not RadarProcessingAsyncValidationError.MissingWorkerTelemetry and
            not RadarProcessingAsyncValidationError.MissingProcessingTelemetry and
            not RadarProcessingAsyncValidationError.FailedBatchCompletion and
            not RadarProcessingAsyncValidationError.IncompleteBatchCompletion and
            not RadarProcessingAsyncValidationError.WorkerFailureNotPropagated and
            not RadarProcessingAsyncValidationError.TopologyVersionMismatch and
            not RadarProcessingAsyncValidationError.UnexpectedMigrationAfterFailedProcessing and
            not RadarProcessingAsyncValidationError.MissingWorkItem and
            not RadarProcessingAsyncValidationError.DuplicateWorkAssignment and
            not RadarProcessingAsyncValidationError.WorkItemScopeMismatch and
            not RadarProcessingAsyncValidationError.WorkItemShardOwnershipMismatch and
            not RadarProcessingAsyncValidationError.WorkItemWorkerAssignmentMismatch and
            not RadarProcessingAsyncValidationError.CompletionScopeMismatch and
            not RadarProcessingAsyncValidationError.CompletionStatusMismatch and
            not RadarProcessingAsyncValidationError.AggregationMetricMismatch and
            not RadarProcessingAsyncValidationError.TelemetryMetricMismatch and
            not RadarProcessingAsyncValidationError.DeterministicChecksumMismatch and
            not RadarProcessingAsyncValidationError.RetentionLimitExceeded)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }
}

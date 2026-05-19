namespace RadarPulse.Domain.Processing;

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

    public bool IsValid { get; }

    public RadarProcessingAsyncValidationError Error { get; }

    public string Message { get; }

    public RadarProcessingValidationProfile ValidationProfile { get; }

    public ulong? SynchronousChecksum { get; }

    public ulong? AsyncChecksum { get; }

    public bool HasComparisonChecksums =>
        SynchronousChecksum.HasValue &&
        AsyncChecksum.HasValue;

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

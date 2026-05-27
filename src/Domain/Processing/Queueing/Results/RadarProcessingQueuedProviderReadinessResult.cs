namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result for one queued-provider rollout readiness gate.
/// </summary>
/// <remarks>
/// The result enforces a strict relationship between status and error: passed
/// gates carry no error, while failed or inconclusive gates carry a concrete
/// reason and message. Optional expected/actual values provide evidence without
/// requiring callers to parse text diagnostics.
/// </remarks>
public sealed class RadarProcessingQueuedProviderReadinessResult
{
    private RadarProcessingQueuedProviderReadinessResult(
        RadarProcessingQueuedProviderReadinessGate gate,
        RadarProcessingQueuedProviderReadinessStatus status,
        RadarProcessingQueuedProviderReadinessError error,
        string message,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null,
        long? expectedBytes = null,
        long? actualBytes = null,
        double? expectedRatio = null,
        double? actualRatio = null)
    {
        EnsureKnownGate(gate);
        EnsureKnownStatus(status);
        EnsureKnownError(error);
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfNegative(expectedCount, nameof(expectedCount));
        ThrowIfNegative(actualCount, nameof(actualCount));
        ThrowIfNegative(expectedBytes, nameof(expectedBytes));
        ThrowIfNegative(actualBytes, nameof(actualBytes));
        ThrowIfNegative(expectedRatio, nameof(expectedRatio));
        ThrowIfNegative(actualRatio, nameof(actualRatio));

        if (status == RadarProcessingQueuedProviderReadinessStatus.Passed &&
            error != RadarProcessingQueuedProviderReadinessError.None)
        {
            throw new ArgumentException("Passed readiness results must not carry an error.", nameof(error));
        }

        if (status != RadarProcessingQueuedProviderReadinessStatus.Passed &&
            error == RadarProcessingQueuedProviderReadinessError.None)
        {
            throw new ArgumentException("Non-passed readiness results must carry an explicit error.", nameof(error));
        }

        if (status == RadarProcessingQueuedProviderReadinessStatus.NotEvaluated &&
            error != RadarProcessingQueuedProviderReadinessError.NotEvaluated)
        {
            throw new ArgumentException("Not-evaluated readiness results must use the NotEvaluated error.", nameof(error));
        }

        if (status != RadarProcessingQueuedProviderReadinessStatus.Passed)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        Gate = gate;
        Status = status;
        Error = error;
        Message = message;
        ExpectedChecksum = expectedChecksum;
        ActualChecksum = actualChecksum;
        ExpectedCount = expectedCount;
        ActualCount = actualCount;
        ExpectedBytes = expectedBytes;
        ActualBytes = actualBytes;
        ExpectedRatio = expectedRatio;
        ActualRatio = actualRatio;
    }

    /// <summary>
    /// Readiness gate evaluated by this result.
    /// </summary>
    public RadarProcessingQueuedProviderReadinessGate Gate { get; }

    /// <summary>
    /// Gate status.
    /// </summary>
    public RadarProcessingQueuedProviderReadinessStatus Status { get; }

    /// <summary>
    /// Gate error classification.
    /// </summary>
    public RadarProcessingQueuedProviderReadinessError Error { get; }

    /// <summary>
    /// Human-readable gate diagnostic.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Expected checksum when checksum evidence is available.
    /// </summary>
    public ulong? ExpectedChecksum { get; }

    /// <summary>
    /// Actual checksum when checksum evidence is available.
    /// </summary>
    public ulong? ActualChecksum { get; }

    /// <summary>
    /// Expected count evidence.
    /// </summary>
    public long? ExpectedCount { get; }

    /// <summary>
    /// Actual count evidence.
    /// </summary>
    public long? ActualCount { get; }

    /// <summary>
    /// Expected byte evidence.
    /// </summary>
    public long? ExpectedBytes { get; }

    /// <summary>
    /// Actual byte evidence.
    /// </summary>
    public long? ActualBytes { get; }

    /// <summary>
    /// Expected ratio evidence.
    /// </summary>
    public double? ExpectedRatio { get; }

    /// <summary>
    /// Actual ratio evidence.
    /// </summary>
    public double? ActualRatio { get; }

    /// <summary>
    /// Indicates a passing gate.
    /// </summary>
    public bool IsPassed => Status == RadarProcessingQueuedProviderReadinessStatus.Passed;

    /// <summary>
    /// Indicates a failing gate.
    /// </summary>
    public bool IsFailed => Status == RadarProcessingQueuedProviderReadinessStatus.Failed;

    /// <summary>
    /// Indicates an evaluated gate with insufficient evidence.
    /// </summary>
    public bool IsInconclusive => Status == RadarProcessingQueuedProviderReadinessStatus.Inconclusive;

    /// <summary>
    /// Indicates that the gate was evaluated.
    /// </summary>
    public bool IsEvaluated => Status != RadarProcessingQueuedProviderReadinessStatus.NotEvaluated;

    /// <summary>
    /// Creates a passing readiness result.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult Passed(
        RadarProcessingQueuedProviderReadinessGate gate,
        string message = "") =>
        new(
            gate,
            RadarProcessingQueuedProviderReadinessStatus.Passed,
            RadarProcessingQueuedProviderReadinessError.None,
            message);

    /// <summary>
    /// Creates a failed readiness result with optional expected/actual evidence.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult Failed(
        RadarProcessingQueuedProviderReadinessGate gate,
        RadarProcessingQueuedProviderReadinessError error,
        string message,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null,
        long? expectedBytes = null,
        long? actualBytes = null,
        double? expectedRatio = null,
        double? actualRatio = null) =>
        new(
            gate,
            RadarProcessingQueuedProviderReadinessStatus.Failed,
            error,
            message,
            expectedChecksum,
            actualChecksum,
            expectedCount,
            actualCount,
            expectedBytes,
            actualBytes,
            expectedRatio,
            actualRatio);

    /// <summary>
    /// Creates an inconclusive readiness result with optional expected/actual evidence.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult Inconclusive(
        RadarProcessingQueuedProviderReadinessGate gate,
        RadarProcessingQueuedProviderReadinessError error,
        string message,
        ulong? expectedChecksum = null,
        ulong? actualChecksum = null,
        long? expectedCount = null,
        long? actualCount = null,
        long? expectedBytes = null,
        long? actualBytes = null,
        double? expectedRatio = null,
        double? actualRatio = null) =>
        new(
            gate,
            RadarProcessingQueuedProviderReadinessStatus.Inconclusive,
            error,
            message,
            expectedChecksum,
            actualChecksum,
            expectedCount,
            actualCount,
            expectedBytes,
            actualBytes,
            expectedRatio,
            actualRatio);

    /// <summary>
    /// Creates a result for a gate that was not evaluated.
    /// </summary>
    public static RadarProcessingQueuedProviderReadinessResult NotEvaluated(
        RadarProcessingQueuedProviderReadinessGate gate,
        string message) =>
        new(
            gate,
            RadarProcessingQueuedProviderReadinessStatus.NotEvaluated,
            RadarProcessingQueuedProviderReadinessError.NotEvaluated,
            message);

    internal static void EnsureKnownGate(
        RadarProcessingQueuedProviderReadinessGate gate)
    {
        if (gate is not RadarProcessingQueuedProviderReadinessGate.CorrectnessParity and
            not RadarProcessingQueuedProviderReadinessGate.TopologyAndRebalanceParity and
            not RadarProcessingQueuedProviderReadinessGate.RetainedResourceReleaseHealth and
            not RadarProcessingQueuedProviderReadinessGate.RetainedResourcePressure and
            not RadarProcessingQueuedProviderReadinessGate.AllocationMovement and
            not RadarProcessingQueuedProviderReadinessGate.PerformanceDelta and
            not RadarProcessingQueuedProviderReadinessGate.RunVariance and
            not RadarProcessingQueuedProviderReadinessGate.EffectiveConfiguration and
            not RadarProcessingQueuedProviderReadinessGate.NaturalEvidence)
        {
            throw new ArgumentOutOfRangeException(nameof(gate));
        }
    }

    internal static void EnsureKnownStatus(
        RadarProcessingQueuedProviderReadinessStatus status)
    {
        if (status is not RadarProcessingQueuedProviderReadinessStatus.NotEvaluated and
            not RadarProcessingQueuedProviderReadinessStatus.Passed and
            not RadarProcessingQueuedProviderReadinessStatus.Failed and
            not RadarProcessingQueuedProviderReadinessStatus.Inconclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    internal static void EnsureKnownError(
        RadarProcessingQueuedProviderReadinessError error)
    {
        if (error is not RadarProcessingQueuedProviderReadinessError.None and
            not RadarProcessingQueuedProviderReadinessError.NotEvaluated and
            not RadarProcessingQueuedProviderReadinessError.MissingBorrowedReference and
            not RadarProcessingQueuedProviderReadinessError.QueuedProviderValidationFailed and
            not RadarProcessingQueuedProviderReadinessError.ChecksumMismatch and
            not RadarProcessingQueuedProviderReadinessError.TopologyOrRebalanceMismatch and
            not RadarProcessingQueuedProviderReadinessError.RetainedResourceReleaseFailed and
            not RadarProcessingQueuedProviderReadinessError.RetainedResourceCleanupIncomplete and
            not RadarProcessingQueuedProviderReadinessError.RetainedResourceRetentionFailed and
            not RadarProcessingQueuedProviderReadinessError.MissingRetainedResourcePressureTelemetry and
            not RadarProcessingQueuedProviderReadinessError.MissingActiveRetainedTelemetry and
            not RadarProcessingQueuedProviderReadinessError.CombinedRetainedPayloadBudgetExceeded and
            not RadarProcessingQueuedProviderReadinessError.ControlledProofExcluded and
            not RadarProcessingQueuedProviderReadinessError.CandidateContourMismatch and
            not RadarProcessingQueuedProviderReadinessError.PerformanceRegression and
            not RadarProcessingQueuedProviderReadinessError.RunVarianceTooHigh and
            not RadarProcessingQueuedProviderReadinessError.AllocationRegression)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }
    }

    private static void ThrowIfNegative(
        long? value,
        string paramName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }

    private static void ThrowIfNegative(
        double? value,
        string paramName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}

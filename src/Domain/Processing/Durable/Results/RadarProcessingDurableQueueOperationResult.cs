namespace RadarPulse.Domain.Processing;

/// <summary>
/// Result of one durable queue operation.
/// </summary>
/// <remarks>
/// Claimed operation results must carry a claimed envelope. Other operation
/// results may carry the updated snapshot but cannot carry claimed payload
/// ownership. This keeps durable state transitions and retained payload ownership
/// explicit.
/// </remarks>
public sealed class RadarProcessingDurableQueueOperationResult
{
    /// <summary>
    /// Creates a durable operation result with validated snapshot/claim consistency.
    /// </summary>
    public RadarProcessingDurableQueueOperationResult(
        RadarProcessingDurableQueueOperationStatus status,
        RadarProcessingDurableEnvelopeSnapshot? snapshot = null,
        RadarProcessingDurableClaimedEnvelope? claimedEnvelope = null,
        string message = "")
    {
        EnsureKnownStatus(status);
        ArgumentNullException.ThrowIfNull(message);

        if (status == RadarProcessingDurableQueueOperationStatus.Claimed)
        {
            ArgumentNullException.ThrowIfNull(claimedEnvelope);
        }
        else if (claimedEnvelope is not null)
        {
            throw new ArgumentException("Only claimed operation results may carry a claimed envelope.", nameof(claimedEnvelope));
        }

        if (claimedEnvelope is not null &&
            snapshot is not null &&
            !ReferenceEquals(claimedEnvelope.Snapshot, snapshot))
        {
            throw new ArgumentException(
                "Claimed operation result snapshot must match the claimed envelope snapshot.",
                nameof(snapshot));
        }

        Status = status;
        Snapshot = snapshot ?? claimedEnvelope?.Snapshot;
        ClaimedEnvelope = claimedEnvelope;
        Message = message;
    }

    /// <summary>
    /// Durable queue operation outcome.
    /// </summary>
    public RadarProcessingDurableQueueOperationStatus Status { get; }

    /// <summary>
    /// Updated durable envelope snapshot when available.
    /// </summary>
    public RadarProcessingDurableEnvelopeSnapshot? Snapshot { get; }

    /// <summary>
    /// Claimed envelope and retained payload for claim operations.
    /// </summary>
    public RadarProcessingDurableClaimedEnvelope? ClaimedEnvelope { get; }

    /// <summary>
    /// Optional diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Indicates whether the result carries envelope state.
    /// </summary>
    public bool HasEnvelope => Snapshot is not null;

    /// <summary>
    /// Indicates whether an accept operation succeeded or found an existing duplicate.
    /// </summary>
    public bool IsAccepted =>
        Status is RadarProcessingDurableQueueOperationStatus.Accepted or
            RadarProcessingDurableQueueOperationStatus.Duplicate;

    /// <summary>
    /// Indicates an invalid state transition.
    /// </summary>
    public bool IsInvalidState => Status == RadarProcessingDurableQueueOperationStatus.InvalidState;

    /// <summary>
    /// Indicates a missing target envelope.
    /// </summary>
    public bool IsNotFound => Status == RadarProcessingDurableQueueOperationStatus.NotFound;

    /// <summary>
    /// Creates an accepted operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Accepted(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Accepted, snapshot);

    /// <summary>
    /// Creates a duplicate accept operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Duplicate(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Duplicate, snapshot);

    /// <summary>
    /// Creates a claimed operation result with retained payload ownership.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Claimed(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope) =>
        new(RadarProcessingDurableQueueOperationStatus.Claimed, claimedEnvelope: claimedEnvelope);

    /// <summary>
    /// Creates an empty claim result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Empty() =>
        new(RadarProcessingDurableQueueOperationStatus.Empty);

    /// <summary>
    /// Creates a completed operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Completed(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Completed, snapshot);

    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Failed(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Failed, snapshot);

    /// <summary>
    /// Creates an abandoned operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Abandoned(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Abandoned, snapshot);

    /// <summary>
    /// Creates a retried operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Retried(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Retried, snapshot);

    /// <summary>
    /// Creates a poisoned operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Poisoned(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Poisoned, snapshot);

    /// <summary>
    /// Creates a committed operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Committed(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Committed, snapshot);

    /// <summary>
    /// Creates a released operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Released(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Released, snapshot);

    /// <summary>
    /// Creates a canceled operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult Canceled(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Canceled, snapshot);

    /// <summary>
    /// Creates a not-found operation result.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult NotFound(
        string message = "") =>
        new(RadarProcessingDurableQueueOperationStatus.NotFound, message: message);

    /// <summary>
    /// Creates an invalid-state operation result with current snapshot evidence.
    /// </summary>
    public static RadarProcessingDurableQueueOperationResult InvalidState(
        RadarProcessingDurableEnvelopeSnapshot snapshot,
        string message = "") =>
        new(RadarProcessingDurableQueueOperationStatus.InvalidState, snapshot, message: message);

    internal static void EnsureKnownStatus(
        RadarProcessingDurableQueueOperationStatus status)
    {
        if (status is not RadarProcessingDurableQueueOperationStatus.Accepted and
            not RadarProcessingDurableQueueOperationStatus.Duplicate and
            not RadarProcessingDurableQueueOperationStatus.Claimed and
            not RadarProcessingDurableQueueOperationStatus.Empty and
            not RadarProcessingDurableQueueOperationStatus.Completed and
            not RadarProcessingDurableQueueOperationStatus.Failed and
            not RadarProcessingDurableQueueOperationStatus.Abandoned and
            not RadarProcessingDurableQueueOperationStatus.Retried and
            not RadarProcessingDurableQueueOperationStatus.Poisoned and
            not RadarProcessingDurableQueueOperationStatus.Committed and
            not RadarProcessingDurableQueueOperationStatus.Released and
            not RadarProcessingDurableQueueOperationStatus.Canceled and
            not RadarProcessingDurableQueueOperationStatus.NotFound and
            not RadarProcessingDurableQueueOperationStatus.InvalidState)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}

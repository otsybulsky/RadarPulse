namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDurableQueueOperationResult
{
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

    public RadarProcessingDurableQueueOperationStatus Status { get; }

    public RadarProcessingDurableEnvelopeSnapshot? Snapshot { get; }

    public RadarProcessingDurableClaimedEnvelope? ClaimedEnvelope { get; }

    public string Message { get; }

    public bool HasEnvelope => Snapshot is not null;

    public bool IsAccepted =>
        Status is RadarProcessingDurableQueueOperationStatus.Accepted or
            RadarProcessingDurableQueueOperationStatus.Duplicate;

    public bool IsInvalidState => Status == RadarProcessingDurableQueueOperationStatus.InvalidState;

    public bool IsNotFound => Status == RadarProcessingDurableQueueOperationStatus.NotFound;

    public static RadarProcessingDurableQueueOperationResult Accepted(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Accepted, snapshot);

    public static RadarProcessingDurableQueueOperationResult Duplicate(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Duplicate, snapshot);

    public static RadarProcessingDurableQueueOperationResult Claimed(
        RadarProcessingDurableClaimedEnvelope claimedEnvelope) =>
        new(RadarProcessingDurableQueueOperationStatus.Claimed, claimedEnvelope: claimedEnvelope);

    public static RadarProcessingDurableQueueOperationResult Empty() =>
        new(RadarProcessingDurableQueueOperationStatus.Empty);

    public static RadarProcessingDurableQueueOperationResult Completed(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Completed, snapshot);

    public static RadarProcessingDurableQueueOperationResult Failed(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Failed, snapshot);

    public static RadarProcessingDurableQueueOperationResult Abandoned(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Abandoned, snapshot);

    public static RadarProcessingDurableQueueOperationResult Retried(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Retried, snapshot);

    public static RadarProcessingDurableQueueOperationResult Poisoned(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Poisoned, snapshot);

    public static RadarProcessingDurableQueueOperationResult Committed(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Committed, snapshot);

    public static RadarProcessingDurableQueueOperationResult Released(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Released, snapshot);

    public static RadarProcessingDurableQueueOperationResult Canceled(
        RadarProcessingDurableEnvelopeSnapshot snapshot) =>
        new(RadarProcessingDurableQueueOperationStatus.Canceled, snapshot);

    public static RadarProcessingDurableQueueOperationResult NotFound(
        string message = "") =>
        new(RadarProcessingDurableQueueOperationStatus.NotFound, message: message);

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

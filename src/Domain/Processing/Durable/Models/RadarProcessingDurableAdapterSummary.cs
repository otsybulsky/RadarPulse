namespace RadarPulse.Domain.Processing;

/// <summary>
/// Read-only summary of a durable adapter and its queue state.
/// </summary>
/// <remarks>
/// The summary is adapter-neutral: it describes schema, storage identity,
/// compatibility, optional storage diagnostics, and queue evidence without
/// exposing file, broker, or database implementation details.
/// </remarks>
public sealed class RadarProcessingDurableAdapterSummary
{
    /// <summary>
    /// Creates a durable adapter summary with validated compatibility status.
    /// </summary>
    public RadarProcessingDurableAdapterSummary(
        string adapterKind,
        int schemaVersion,
        string storageIdentity,
        RadarProcessingDurableAdapterCompatibilityStatus compatibilityStatus =
            RadarProcessingDurableAdapterCompatibilityStatus.Compatible,
        string storageMessage = "",
        RadarProcessingDurableQueueSummary? queueSummary = null)
    {
        if (string.IsNullOrWhiteSpace(adapterKind))
        {
            throw new ArgumentException("Adapter kind must not be empty.", nameof(adapterKind));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        ArgumentNullException.ThrowIfNull(storageIdentity);
        EnsureKnownCompatibilityStatus(compatibilityStatus);
        ArgumentNullException.ThrowIfNull(storageMessage);

        AdapterKind = adapterKind;
        SchemaVersion = schemaVersion;
        StorageIdentity = storageIdentity;
        CompatibilityStatus = compatibilityStatus;
        StorageMessage = storageMessage;
        QueueSummary = queueSummary ?? RadarProcessingDurableQueueSummary.Empty;
    }

    /// <summary>
    /// Adapter kind name, such as the accepted local file-backed adapter.
    /// </summary>
    public string AdapterKind { get; }

    /// <summary>
    /// Durable storage schema version.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Adapter-specific storage identity.
    /// </summary>
    public string StorageIdentity { get; }

    /// <summary>
    /// Compatibility posture for the current storage contents.
    /// </summary>
    public RadarProcessingDurableAdapterCompatibilityStatus CompatibilityStatus { get; }

    /// <summary>
    /// Optional storage diagnostic message.
    /// </summary>
    public string StorageMessage { get; }

    /// <summary>
    /// Durable queue summary for the adapter.
    /// </summary>
    public RadarProcessingDurableQueueSummary QueueSummary { get; }

    /// <summary>
    /// Indicates whether current storage can be used by the runtime.
    /// </summary>
    public bool IsCompatible =>
        CompatibilityStatus is RadarProcessingDurableAdapterCompatibilityStatus.Compatible or
            RadarProcessingDurableAdapterCompatibilityStatus.Empty;

    /// <summary>
    /// Indicates whether a storage diagnostic message is present.
    /// </summary>
    public bool HasStorageMessage => !string.IsNullOrWhiteSpace(StorageMessage);

    /// <summary>
    /// Throws when the supplied compatibility status is unknown.
    /// </summary>
    public static void EnsureKnownCompatibilityStatus(
        RadarProcessingDurableAdapterCompatibilityStatus status)
    {
        if (status is not RadarProcessingDurableAdapterCompatibilityStatus.Compatible and
            not RadarProcessingDurableAdapterCompatibilityStatus.Empty and
            not RadarProcessingDurableAdapterCompatibilityStatus.Incompatible and
            not RadarProcessingDurableAdapterCompatibilityStatus.Failed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}

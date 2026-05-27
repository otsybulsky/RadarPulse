namespace RadarPulse.Domain.Processing;

public sealed class RadarProcessingDurableAdapterSummary
{
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

    public string AdapterKind { get; }

    public int SchemaVersion { get; }

    public string StorageIdentity { get; }

    public RadarProcessingDurableAdapterCompatibilityStatus CompatibilityStatus { get; }

    public string StorageMessage { get; }

    public RadarProcessingDurableQueueSummary QueueSummary { get; }

    public bool IsCompatible =>
        CompatibilityStatus is RadarProcessingDurableAdapterCompatibilityStatus.Compatible or
            RadarProcessingDurableAdapterCompatibilityStatus.Empty;

    public bool HasStorageMessage => !string.IsNullOrWhiteSpace(StorageMessage);

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

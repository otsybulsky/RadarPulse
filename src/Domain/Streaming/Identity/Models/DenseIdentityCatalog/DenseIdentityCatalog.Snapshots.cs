using System.Threading;

namespace RadarPulse.Domain.Streaming;

public sealed partial class DenseIdentityCatalog
{
    /// <summary>
    /// Creates a snapshot at the current version.
    /// </summary>
    public DenseIdentityCatalogSnapshot CreateSnapshot() => CreateSnapshot(CurrentVersion);

    /// <summary>
    /// Creates a snapshot containing entries visible at the requested version.
    /// </summary>
    public DenseIdentityCatalogSnapshot CreateSnapshot(DictionaryVersion snapshotVersion)
    {
        var snapshotCount = GetVisibleCount(snapshotVersion);
        return new DenseIdentityCatalogSnapshot(
            Name,
            snapshotVersion,
            CopyEntries(startId: 0, count: snapshotCount));
    }

    /// <summary>
    /// Creates a delta from a version to the current version.
    /// </summary>
    public DenseIdentityCatalogDelta CreateDelta(DictionaryVersion fromVersion) =>
        CreateDelta(fromVersion, CurrentVersion);

    /// <summary>
    /// Creates a delta for entries visible between two versions.
    /// </summary>
    public DenseIdentityCatalogDelta CreateDelta(
        DictionaryVersion fromVersion,
        DictionaryVersion toVersion)
    {
        if (toVersion.Value < fromVersion.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(toVersion));
        }

        var fromCount = GetVisibleCount(fromVersion);
        var toCount = GetVisibleCount(toVersion);
        return new DenseIdentityCatalogDelta(
            Name,
            fromVersion,
            toVersion,
            CopyEntries(fromCount, toCount - fromCount));
    }

    private DenseIdentityCatalogEntry[] CopyEntries(int startId, int count)
    {
        if (count == 0)
        {
            return [];
        }

        var reverse = Volatile.Read(ref idToText);
        var entries = new DenseIdentityCatalogEntry[count];
        for (var i = 0; i < count; i++)
        {
            var id = startId + i;
            var text = Volatile.Read(ref reverse[id]);
            if (text is null)
            {
                throw new InvalidOperationException($"Catalog '{Name}' has no published text for id {id}.");
            }

            entries[i] = new DenseIdentityCatalogEntry(id, text);
        }

        return entries;
    }

    private int GetVisibleCount(DictionaryVersion requestedVersion)
    {
        var currentVersionValue = Volatile.Read(ref version);
        if (requestedVersion.Value < DictionaryVersion.Initial.Value ||
            requestedVersion.Value > currentVersionValue)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedVersion));
        }

        var visibleCount = requestedVersion.Value - DictionaryVersion.Initial.Value;
        if (visibleCount > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedVersion));
        }

        return (int)visibleCount;
    }

    private static long CountToVersionValue(int count) =>
        checked(DictionaryVersion.Initial.Value + count);
}

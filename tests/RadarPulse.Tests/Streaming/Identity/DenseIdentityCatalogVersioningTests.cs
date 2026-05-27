using System.Text;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class DenseIdentityCatalogVersioningTests
{
    [Fact]
    public void SnapshotVersionExposesOnlyEntriesVisibleAtThatVersion()
    {
        var catalog = new DenseIdentityCatalog("radar");
        var initialVersion = catalog.CurrentVersion;
        var initialSnapshot = catalog.CreateSnapshot(initialVersion);

        var ktlx = catalog.GetOrAdd("KTLX");
        var ktlxVersion = catalog.CurrentVersion;
        var ktlxSnapshot = catalog.CreateSnapshot(ktlxVersion);

        catalog.GetOrAdd("KOUN");
        var currentSnapshot = catalog.CreateSnapshot();

        Assert.Equal(DictionaryVersion.Initial, initialVersion);
        Assert.Equal(0, initialSnapshot.Count);
        Assert.Equal(0, ktlx);
        Assert.Equal(new DictionaryVersion(2), ktlxVersion);
        Assert.Equal(1, ktlxSnapshot.Count);
        Assert.True(ktlxSnapshot.TryGetId("KTLX", out var ktlxId));
        Assert.Equal(0, ktlxId);
        Assert.False(ktlxSnapshot.TryGetId("KOUN", out _));
        Assert.False(ktlxSnapshot.TryGetText(1, out _));

        Assert.Equal(new DictionaryVersion(3), currentSnapshot.Version);
        Assert.Equal(2, currentSnapshot.Count);
        Assert.True(currentSnapshot.TryGetId("KOUN", out var kounId));
        Assert.Equal(1, kounId);
    }

    [Fact]
    public void DeltaReconstructsMappingsForLaterVersion()
    {
        var catalog = new DenseIdentityCatalog("radar");
        catalog.GetOrAdd("KTLX");
        var baseSnapshot = catalog.CreateSnapshot();

        catalog.GetOrAdd("KOUN");
        catalog.GetOrAdd("KFDR");
        var delta = catalog.CreateDelta(baseSnapshot.Version);
        var reconstructed = baseSnapshot.Apply(delta);
        var current = catalog.CreateSnapshot();

        Assert.Equal(baseSnapshot.Version, delta.FromVersion);
        Assert.Equal(current.Version, delta.ToVersion);
        Assert.Equal(2, delta.Count);
        Assert.Equal([1, 2], delta.Entries.ToArray().Select(entry => entry.Id).ToArray());

        Assert.Equal(current.Version, reconstructed.Version);
        Assert.Equal(current.Count, reconstructed.Count);
        for (var id = 0; id < current.Count; id++)
        {
            Assert.True(current.TryGetText(id, out var expected));
            Assert.True(reconstructed.TryGetText(id, out var actual));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void SnapshotLookupSupportsPublishedForwardAndReverseMappings()
    {
        var catalog = new DenseIdentityCatalog("moment");
        catalog.GetOrAdd("REF");
        catalog.GetOrAdd("VEL");

        var snapshot = catalog.CreateSnapshot();
        var utf8 = Encoding.UTF8.GetBytes("VEL");

        Assert.True(snapshot.TryGetId("VEL", out var stringId));
        Assert.True(snapshot.TryGetId("VEL".AsSpan(), out var spanId));
        Assert.True(snapshot.TryGetId(utf8, out var utf8Id));
        Assert.True(snapshot.TryGetText(utf8Id, out var text));

        Assert.Equal(1, stringId);
        Assert.Equal(stringId, spanId);
        Assert.Equal(stringId, utf8Id);
        Assert.Equal("VEL", text);
        Assert.False(snapshot.TryGetId("SW", out _));
        Assert.False(snapshot.TryGetText(2, out _));
    }

    [Fact]
    public void DuplicateRegistrationDoesNotAdvanceVersion()
    {
        var catalog = new DenseIdentityCatalog("moment");

        var first = catalog.GetOrAdd("REF");
        var version = catalog.CurrentVersion;
        var duplicate = catalog.GetOrAdd("REF");

        Assert.Equal(first, duplicate);
        Assert.Equal(version, catalog.CurrentVersion);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void ExistingSnapshotIsStableAfterLaterAppends()
    {
        var catalog = new DenseIdentityCatalog("moment");
        catalog.GetOrAdd("REF");
        var snapshot = catalog.CreateSnapshot();

        catalog.GetOrAdd("VEL");
        catalog.GetOrAdd("SW");

        Assert.Equal(new DictionaryVersion(2), snapshot.Version);
        Assert.Equal(1, snapshot.Count);
        Assert.True(snapshot.TryGetId("REF", out var id));
        Assert.Equal(0, id);
        Assert.False(snapshot.TryGetId("VEL", out _));
        Assert.False(snapshot.TryGetId("SW", out _));
    }

    [Fact]
    public void EmptyDeltaKeepsSnapshotUnchanged()
    {
        var catalog = new DenseIdentityCatalog("radar");
        catalog.GetOrAdd("KTLX");
        var snapshot = catalog.CreateSnapshot();

        var delta = catalog.CreateDelta(snapshot.Version, snapshot.Version);
        var applied = snapshot.Apply(delta);

        Assert.Equal(0, delta.Count);
        Assert.Same(snapshot, applied);
    }

    [Fact]
    public void RequestedVersionMustAlreadyBeVisible()
    {
        var catalog = new DenseIdentityCatalog("radar");
        catalog.GetOrAdd("KTLX");

        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.CreateSnapshot(new DictionaryVersion(99)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            catalog.CreateDelta(new DictionaryVersion(99), catalog.CurrentVersion));
    }
}

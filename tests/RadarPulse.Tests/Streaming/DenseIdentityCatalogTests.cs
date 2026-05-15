using System.Text;
using RadarPulse.Domain.Streaming;

namespace RadarPulse.Tests.Streaming;

public sealed class DenseIdentityCatalogTests
{
    [Fact]
    public void SameCanonicalTextMapsToSameIdAcrossLookupViews()
    {
        var catalog = new DenseIdentityCatalog("moment");
        var id = catalog.GetOrAdd("REF");
        var utf8 = Encoding.UTF8.GetBytes("REF");

        Assert.True(catalog.TryGetId("REF", out var stringId));
        Assert.True(catalog.TryGetId("REF".AsSpan(), out var spanId));
        Assert.True(catalog.TryGetId(utf8, out var utf8Id));

        Assert.Equal(0, id);
        Assert.Equal(id, stringId);
        Assert.Equal(id, spanId);
        Assert.Equal(id, utf8Id);
        Assert.Equal(1, catalog.Count);
    }

    [Fact]
    public void IdsAreDenseAppendOnlyAndReverseLookupIsDense()
    {
        var catalog = new DenseIdentityCatalog("radar");

        var ktlx = catalog.GetOrAdd("KTLX");
        var koun = catalog.GetOrAdd("KOUN");
        var kfdr = catalog.GetOrAdd("KFDR");

        Assert.Equal([0, 1, 2], [ktlx, koun, kfdr]);
        Assert.Equal(3, catalog.Count);

        Assert.True(catalog.TryGetText(0, out var first));
        Assert.True(catalog.TryGetText(1, out var second));
        Assert.True(catalog.TryGetText(2, out var third));
        Assert.Equal("KTLX", first);
        Assert.Equal("KOUN", second);
        Assert.Equal("KFDR", third);

        Assert.Equal(koun, catalog.GetOrAdd("KOUN"));
        Assert.Equal(3, catalog.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ref")]
    [InlineData("REF ")]
    [InlineData(" REF")]
    [InlineData("R-EF")]
    [InlineData("R.EF")]
    [InlineData("\u0420\u0415\u0424")]
    public void InvalidTextDoesNotReceiveId(string text)
    {
        var catalog = new DenseIdentityCatalog("moment");

        Assert.False(catalog.TryGetId(text, out _));
        Assert.False(catalog.TryGetId(text.AsSpan(), out _));
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd(text));
        Assert.Equal(0, catalog.Count);
    }

    [Fact]
    public void InvalidUtf8ViewDoesNotReceiveId()
    {
        var catalog = new DenseIdentityCatalog("moment");
        byte[] lowercase = [(byte)'r', (byte)'e', (byte)'f'];
        byte[] padded = [(byte)'R', (byte)'E', (byte)'F', (byte)' '];
        byte[] nonAscii = [(byte)'R', 0xD0, 0xAE];

        Assert.False(catalog.TryGetId(lowercase, out _));
        Assert.False(catalog.TryGetId(padded, out _));
        Assert.False(catalog.TryGetId(nonAscii, out _));
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd(lowercase));
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd(padded));
        Assert.Throws<ArgumentException>(() => catalog.GetOrAdd(nonAscii));
        Assert.Equal(0, catalog.Count);
    }

    [Fact]
    public void ConcurrentRegistrationOfSameIdentityAppendsOnce()
    {
        var catalog = new DenseIdentityCatalog("radar");
        var ids = new int[1_024];

        Parallel.For(0, ids.Length, i => ids[i] = catalog.GetOrAdd("KTLX"));

        Assert.All(ids, id => Assert.Equal(0, id));
        Assert.Equal(1, catalog.Count);
        Assert.True(catalog.TryGetText(0, out var text));
        Assert.Equal("KTLX", text);
    }

    [Fact]
    public void ConcurrentRegistrationKeepsAssignedIdsDense()
    {
        var catalog = new DenseIdentityCatalog("radar", initialCapacity: 4);
        var keys = Enumerable.Range(0, 128)
            .Select(i => $"R{i:D4}")
            .ToArray();
        var ids = new int[keys.Length];

        Parallel.For(0, keys.Length, i => ids[i] = catalog.GetOrAdd(keys[i]));

        Assert.Equal(keys.Length, catalog.Count);
        Assert.Equal(keys.Length, ids.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, keys.Length), ids.Order());

        var keySet = keys.ToHashSet(StringComparer.Ordinal);
        for (var id = 0; id < keys.Length; id++)
        {
            Assert.True(catalog.TryGetText(id, out var text));
            Assert.True(keySet.Contains(text!), $"Unexpected reverse identity '{text}' for id {id}.");
        }
    }

    [Fact]
    public async Task SuccessfulLookupNeverExposesPartialReverseEntry()
    {
        var catalog = new DenseIdentityCatalog("moment", initialCapacity: 4);
        var keys = Enumerable.Range(0, 256)
            .Select(i => $"M{i:D4}")
            .ToArray();
        var failed = 0;
        var done = false;

        var writer = Task.Run(() =>
        {
            foreach (var key in keys)
            {
                catalog.GetOrAdd(key);
            }

            Volatile.Write(ref done, true);
        });

        var readers = Enumerable.Range(0, Environment.ProcessorCount)
            .Select(_ => Task.Run(() =>
            {
                while (!Volatile.Read(ref done))
                {
                    foreach (var key in keys)
                    {
                        if (!catalog.TryGetId(key, out var id))
                        {
                            continue;
                        }

                        if (!catalog.TryGetText(id, out var text) ||
                            !StringComparer.Ordinal.Equals(key, text))
                        {
                            Interlocked.Increment(ref failed);
                        }
                    }
                }
            }))
            .ToArray();

        await Task.WhenAll([writer, .. readers]);

        Assert.Equal(0, failed);
        Assert.Equal(keys.Length, catalog.Count);
    }
}

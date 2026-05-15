using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace RadarPulse.Domain.Streaming;

public sealed class DenseIdentityCatalog
{
    private const int DefaultInitialCapacity = 256;
    private const int Utf8BucketLoadFactor = 4;
    private const int MinUtf8BucketCount = 64;

    private readonly ConcurrentDictionary<string, int> textToId;
    private readonly ConcurrentDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> textSpanLookup;
    private readonly Utf8Bucket?[] utf8Buckets;
    private readonly object registrationGate = new();
    private string?[] idToText;
    private int count;

    public DenseIdentityCatalog(
        string name,
        int initialCapacity = DefaultInitialCapacity,
        int maximumTextLength = 32)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (initialCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        if (maximumTextLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
        }

        Name = name;
        MaximumTextLength = maximumTextLength;
        textToId = new ConcurrentDictionary<string, int>(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: initialCapacity,
            comparer: StringComparer.Ordinal);
        textSpanLookup = textToId.GetAlternateLookup<ReadOnlySpan<char>>();
        idToText = new string?[initialCapacity];

        var bucketCount = RoundUpPowerOfTwo(Math.Max(initialCapacity * Utf8BucketLoadFactor, MinUtf8BucketCount));
        utf8Buckets = new Utf8Bucket?[bucketCount];
    }

    public string Name { get; }

    public int Count => Volatile.Read(ref count);

    public int MaximumTextLength { get; }

    public bool TryGetId(string text, out int id)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsCanonical(text.AsSpan()))
        {
            id = default;
            return false;
        }

        return textToId.TryGetValue(text, out id);
    }

    public bool TryGetId(ReadOnlySpan<char> text, out int id)
    {
        if (!IsCanonical(text))
        {
            id = default;
            return false;
        }

        return textSpanLookup.TryGetValue(text, out id);
    }

    public bool TryGetId(ReadOnlySpan<byte> utf8Text, out int id)
    {
        if (!IsCanonicalUtf8(utf8Text))
        {
            id = default;
            return false;
        }

        var hash = GetUtf8Hash(utf8Text);
        var bucket = Volatile.Read(ref utf8Buckets[hash & (uint)(utf8Buckets.Length - 1)]);
        if (bucket is not null && bucket.TryGetId(utf8Text, hash, out id))
        {
            return true;
        }

        id = default;
        return false;
    }

    public bool TryGetText(int id, out string? text)
    {
        var reverse = Volatile.Read(ref idToText);
        if ((uint)id < (uint)reverse.Length)
        {
            text = Volatile.Read(ref reverse[id]);
            return text is not null;
        }

        text = null;
        return false;
    }

    public int GetOrAdd(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return GetOrAdd(text.AsSpan());
    }

    public int GetOrAdd(ReadOnlySpan<char> text)
    {
        EnsureCanonical(text);
        if (textSpanLookup.TryGetValue(text, out var id))
        {
            return id;
        }

        return Register(text);
    }

    public int GetOrAdd(ReadOnlySpan<byte> utf8Text)
    {
        EnsureCanonicalUtf8(utf8Text);
        if (TryGetId(utf8Text, out var id))
        {
            return id;
        }

        return Register(utf8Text);
    }

    private int Register(ReadOnlySpan<char> text)
    {
        var canonicalText = text.ToString();
        var utf8Text = Encoding.UTF8.GetBytes(canonicalText);
        var hash = GetUtf8Hash(utf8Text);

        lock (registrationGate)
        {
            if (textToId.TryGetValue(canonicalText, out var existingId))
            {
                return existingId;
            }

            return Append(canonicalText, utf8Text, hash);
        }
    }

    private int Register(ReadOnlySpan<byte> utf8Text)
    {
        var canonicalText = Encoding.UTF8.GetString(utf8Text);
        var storedUtf8Text = utf8Text.ToArray();
        var hash = GetUtf8Hash(utf8Text);

        lock (registrationGate)
        {
            if (textToId.TryGetValue(canonicalText, out var existingId))
            {
                return existingId;
            }

            return Append(canonicalText, storedUtf8Text, hash);
        }
    }

    private int Append(string canonicalText, byte[] utf8Text, uint hash)
    {
        var id = count;
        EnsureReverseCapacity(id);

        var reverse = idToText;
        Volatile.Write(ref reverse[id], canonicalText);
        AddUtf8Entry(utf8Text, id, hash);
        textToId[canonicalText] = id;
        Volatile.Write(ref count, id + 1);

        return id;
    }

    private void AddUtf8Entry(byte[] utf8Text, int id, uint hash)
    {
        var bucketIndex = (int)(hash & (uint)(utf8Buckets.Length - 1));
        var bucket = Volatile.Read(ref utf8Buckets[bucketIndex]);
        if (bucket is null)
        {
            bucket = new Utf8Bucket();
            Volatile.Write(ref utf8Buckets[bucketIndex], bucket);
        }

        bucket.Add(utf8Text, id, hash);
    }

    private void EnsureReverseCapacity(int id)
    {
        var reverse = idToText;
        if (id < reverse.Length)
        {
            return;
        }

        var expanded = new string?[Math.Max(id + 1, reverse.Length * 2)];
        Array.Copy(reverse, expanded, reverse.Length);
        Volatile.Write(ref idToText, expanded);
    }

    private void EnsureCanonical(ReadOnlySpan<char> text)
    {
        if (IsCanonical(text))
        {
            return;
        }

        throw new ArgumentException(
            $"Identity text for catalog '{Name}' must be 1..{MaximumTextLength} characters and contain only A-Z, 0-9, or underscore.");
    }

    private void EnsureCanonicalUtf8(ReadOnlySpan<byte> text)
    {
        if (IsCanonicalUtf8(text))
        {
            return;
        }

        throw new ArgumentException(
            $"UTF-8 identity text for catalog '{Name}' must be 1..{MaximumTextLength} bytes and contain only A-Z, 0-9, or underscore.");
    }

    private bool IsCanonical(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text.Length > MaximumTextLength)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (!IsCanonicalAscii(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsCanonicalUtf8(ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty || text.Length > MaximumTextLength)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (!IsCanonicalAscii(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCanonicalAscii(int value) =>
        value is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private static uint GetUtf8Hash(ReadOnlySpan<byte> value)
    {
        unchecked
        {
            uint hash = 0;
            var remaining = value;

            while (remaining.Length >= sizeof(ulong))
            {
                hash = BitOperations.Crc32C(hash, MemoryMarshal.Read<ulong>(remaining));
                remaining = remaining[sizeof(ulong)..];
            }

            if (remaining.Length >= sizeof(uint))
            {
                hash = BitOperations.Crc32C(hash, MemoryMarshal.Read<uint>(remaining));
                remaining = remaining[sizeof(uint)..];
            }

            for (var i = 0; i < remaining.Length; i++)
            {
                hash = BitOperations.Crc32C(hash, remaining[i]);
            }

            return hash;
        }
    }

    private static int RoundUpPowerOfTwo(int value)
    {
        if (value <= 2)
        {
            return 2;
        }

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    private sealed class Utf8Bucket
    {
        private Entry[] entries = [];

        public bool TryGetId(ReadOnlySpan<byte> text, uint hash, out int id)
        {
            var snapshot = Volatile.Read(ref entries);
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i].Hash == hash &&
                    snapshot[i].Text.Length == text.Length &&
                    snapshot[i].Text.AsSpan().SequenceEqual(text))
                {
                    id = snapshot[i].Id;
                    return true;
                }
            }

            id = default;
            return false;
        }

        public void Add(byte[] text, int id, uint hash)
        {
            var snapshot = entries;
            var expanded = new Entry[snapshot.Length + 1];
            Array.Copy(snapshot, expanded, snapshot.Length);
            expanded[^1] = new Entry(text, id, hash);
            Volatile.Write(ref entries, expanded);
        }

        private readonly record struct Entry(byte[] Text, int Id, uint Hash);
    }
}

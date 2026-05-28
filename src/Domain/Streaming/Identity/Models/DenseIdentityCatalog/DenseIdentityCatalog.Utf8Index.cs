using System.Threading;

namespace RadarPulse.Domain.Streaming;

public sealed partial class DenseIdentityCatalog
{
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

    private sealed class Utf8Bucket
    {
        private Entry[] entries = [];

        /// <summary>
        /// Finds an existing id in this hash bucket by comparing UTF-8 bytes after the hash match.
        /// </summary>
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

        /// <summary>
        /// Adds a new immutable bucket entry and publishes the expanded snapshot atomically.
        /// </summary>
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

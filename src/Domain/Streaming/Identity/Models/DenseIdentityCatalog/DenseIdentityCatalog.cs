using System.Collections.Concurrent;
using System.Threading;

namespace RadarPulse.Domain.Streaming;

/// <summary>
/// Thread-safe dense catalog that assigns stable ids to canonical identity text.
/// </summary>
/// <remarks>
/// Ids are assigned densely in registration order. The catalog exposes snapshots
/// and deltas by dictionary version so stream batches can be validated against
/// the exact text visibility that existed when an identity was normalized.
/// </remarks>
public sealed partial class DenseIdentityCatalog
{
    private const int DefaultInitialCapacity = 256;
    private const int Utf8BucketLoadFactor = 4;
    private const int MinUtf8BucketCount = 64;

    private readonly DenseIdentityCanonicalizationPolicy canonicalizationPolicy;
    private readonly ConcurrentDictionary<string, int> textToId;
    private readonly ConcurrentDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> textSpanLookup;
    private readonly Utf8Bucket?[] utf8Buckets;
    private readonly object registrationGate = new();
    private string?[] idToText;
    private int count;
    private long version = DictionaryVersion.Initial.Value;

    /// <summary>
    /// Creates a compact identifier catalog with a maximum text length.
    /// </summary>
    public DenseIdentityCatalog(
        string name,
        int initialCapacity = DefaultInitialCapacity,
        int maximumTextLength = 32)
        : this(name, DenseIdentityCanonicalizationPolicy.CompactIdentifier(maximumTextLength), initialCapacity)
    {
    }

    /// <summary>
    /// Creates a dense identity catalog with an explicit canonicalization policy.
    /// </summary>
    public DenseIdentityCatalog(
        string name,
        DenseIdentityCanonicalizationPolicy canonicalizationPolicy,
        int initialCapacity = DefaultInitialCapacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(canonicalizationPolicy);

        if (initialCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        Name = name;
        this.canonicalizationPolicy = canonicalizationPolicy;
        textToId = new ConcurrentDictionary<string, int>(
            concurrencyLevel: Environment.ProcessorCount,
            capacity: initialCapacity,
            comparer: StringComparer.Ordinal);
        textSpanLookup = textToId.GetAlternateLookup<ReadOnlySpan<char>>();
        idToText = new string?[initialCapacity];

        var bucketCount = RoundUpPowerOfTwo(Math.Max(initialCapacity * Utf8BucketLoadFactor, MinUtf8BucketCount));
        utf8Buckets = new Utf8Bucket?[bucketCount];
    }

    /// <summary>
    /// Catalog name used in snapshots and diagnostics.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of registered entries currently visible.
    /// </summary>
    public int Count => Volatile.Read(ref count);

    /// <summary>
    /// Current dictionary version for the visible entry count.
    /// </summary>
    public DictionaryVersion CurrentVersion => new(Volatile.Read(ref version));

    /// <summary>
    /// Canonicalization policy enforced by the catalog.
    /// </summary>
    public DenseIdentityCanonicalizationPolicy CanonicalizationPolicy => canonicalizationPolicy;

    /// <summary>
    /// Minimum canonical text length.
    /// </summary>
    public int MinimumTextLength => canonicalizationPolicy.MinimumLength;

    /// <summary>
    /// Maximum canonical text length.
    /// </summary>
    public int MaximumTextLength => canonicalizationPolicy.MaximumLength;
}

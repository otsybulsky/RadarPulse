using System.Text;
using System.Threading;

namespace RadarPulse.Domain.Streaming;

public sealed partial class DenseIdentityCatalog
{
    /// <summary>
    /// Gets an existing id or registers canonical string text.
    /// </summary>
    public int GetOrAdd(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return GetOrAdd(text.AsSpan());
    }

    /// <summary>
    /// Gets an existing id or registers canonical UTF-16 text.
    /// </summary>
    public int GetOrAdd(ReadOnlySpan<char> text)
    {
        EnsureCanonical(text);
        if (textSpanLookup.TryGetValue(text, out var id))
        {
            return id;
        }

        return Register(text);
    }

    /// <summary>
    /// Gets an existing id or registers canonical UTF-8 bytes.
    /// </summary>
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
        Volatile.Write(ref version, CountToVersionValue(id + 1));

        return id;
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
}

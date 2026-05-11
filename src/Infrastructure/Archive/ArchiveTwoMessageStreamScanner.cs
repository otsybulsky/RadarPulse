using System.Buffers.Binary;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed class ArchiveTwoMessageStreamScanner
{
    private const int MessageHeaderLength = 16;
    private const int RetainedSearchBytes = 128;
    private const int MaximumMessageBytes = 2 * 1024 * 1024;

    private readonly IArchiveTwoMessageConsumer messageConsumer;
    private byte[] pendingBuffer = new byte[RetainedSearchBytes];
    private int pendingLength;
    private int sourceRecordSequenceNumber;
    private int sourceMessageSequenceNumber;

    public ArchiveTwoMessageStreamScanner(IArchiveTwoMessageConsumer messageConsumer)
    {
        this.messageConsumer = messageConsumer ?? throw new ArgumentNullException(nameof(messageConsumer));
    }

    public void Reset(int sourceRecordSequenceNumber = 0)
    {
        pendingLength = 0;
        this.sourceRecordSequenceNumber = sourceRecordSequenceNumber;
        sourceMessageSequenceNumber = 0;
    }

    public void Append(ReadOnlySpan<byte> chunk)
    {
        EnsurePendingCapacity(pendingLength + chunk.Length);
        chunk.CopyTo(pendingBuffer.AsSpan(pendingLength));
        pendingLength += chunk.Length;
        ProcessPending(final: false);
    }

    public void Complete()
    {
        ProcessPending(final: true);
        pendingLength = 0;
    }

    private void ProcessPending(bool final)
    {
        while (pendingLength >= MessageHeaderLength)
        {
            var headerOffset = FindHeader(pendingBuffer.AsSpan(0, pendingLength));
            if (headerOffset < 0)
            {
                if (final)
                {
                    if (!IsIgnorablePadding(pendingBuffer.AsSpan(0, pendingLength)))
                    {
                        throw new InvalidDataException("Trailing decompressed bytes do not contain a valid RDA/RPG message header.");
                    }

                    pendingLength = 0;
                    return;
                }

                RetainSearchTail();
                return;
            }

            if (headerOffset > 0)
            {
                if (!IsIgnorablePadding(pendingBuffer.AsSpan(0, headerOffset)))
                {
                    throw new InvalidDataException("Unexpected non-padding bytes before RDA/RPG message header.");
                }

                Consume(headerOffset);
            }

            if (!TryGetMessageLength(pendingBuffer.AsSpan(0, pendingLength), out var messageBytes))
            {
                return;
            }

            if (messageBytes > MaximumMessageBytes)
            {
                throw new InvalidDataException($"RDA/RPG message declares {messageBytes} bytes, exceeding the scanner limit.");
            }

            if (pendingLength < messageBytes)
            {
                return;
            }

            sourceMessageSequenceNumber++;
            messageConsumer.AcceptMessage(
                pendingBuffer.AsSpan(0, messageBytes),
                new ArchiveTwoMessageSource(sourceRecordSequenceNumber, sourceMessageSequenceNumber));
            Consume(messageBytes);
        }
    }

    private static int FindHeader(ReadOnlySpan<byte> buffer)
    {
        for (var offset = 0; offset <= buffer.Length - MessageHeaderLength; offset++)
        {
            if (TryGetMessageLength(buffer[offset..], out _))
            {
                return offset;
            }
        }

        return -1;
    }

    private static bool TryGetMessageLength(ReadOnlySpan<byte> buffer, out int messageBytes)
    {
        messageBytes = 0;
        if (buffer.Length < MessageHeaderLength)
        {
            return false;
        }

        var sizeHalfwords = BinaryPrimitives.ReadUInt16BigEndian(buffer[..2]);
        var channel = buffer[2];
        var messageType = buffer[3];
        if (messageType is < 1 or > 33)
        {
            return false;
        }

        if (channel is not (0 or 1 or 2 or 8))
        {
            return false;
        }

        var messageDate = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(6, 2));
        if (messageDate is < 1 or > 50_000)
        {
            return false;
        }

        var messageTime = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8, 4));
        if (messageTime > 86_400_000)
        {
            return false;
        }

        if (sizeHalfwords == ushort.MaxValue)
        {
            messageBytes = checked((int)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(12, 4)));
        }
        else
        {
            var segmentCount = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(12, 2));
            var segmentNumber = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(14, 2));
            if (segmentCount is < 1 or > 4_096 ||
                segmentNumber < 1 ||
                segmentNumber > segmentCount)
            {
                return false;
            }

            messageBytes = sizeHalfwords * 2;
        }

        return messageBytes >= MessageHeaderLength && messageBytes <= MaximumMessageBytes;
    }

    private static bool IsIgnorablePadding(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private void RetainSearchTail()
    {
        if (pendingLength <= RetainedSearchBytes)
        {
            return;
        }

        pendingBuffer.AsSpan(pendingLength - RetainedSearchBytes, RetainedSearchBytes)
            .CopyTo(pendingBuffer);
        pendingLength = RetainedSearchBytes;
    }

    private void Consume(int byteCount)
    {
        if (byteCount == pendingLength)
        {
            pendingLength = 0;
            return;
        }

        pendingBuffer.AsSpan(byteCount, pendingLength - byteCount).CopyTo(pendingBuffer);
        pendingLength -= byteCount;
    }

    private void EnsurePendingCapacity(int requiredLength)
    {
        if (pendingBuffer.Length >= requiredLength)
        {
            return;
        }

        var newLength = pendingBuffer.Length;
        while (newLength < requiredLength)
        {
            newLength *= 2;
        }

        Array.Resize(ref pendingBuffer, newLength);
    }
}

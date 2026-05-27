using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingHandlerOutputDescriptor
{
    private readonly IReadOnlyList<RadarProcessingHandlerOutputField> fields;

    public RadarProcessingHandlerOutputDescriptor(
        int handlerIndex,
        string name,
        int int64SlotCount,
        int doubleSlotCount,
        IReadOnlyList<RadarProcessingHandlerOutputField>? fields = null,
        RadarSourceProcessingHandlerExecutionClassification executionClassification =
            RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(handlerIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(int64SlotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(doubleSlotCount);
        EnsureKnownExecutionClassification(executionClassification);

        HandlerIndex = handlerIndex;
        Name = name;
        Int64SlotCount = int64SlotCount;
        DoubleSlotCount = doubleSlotCount;
        ExecutionClassification = executionClassification;
        this.fields = CopyFields(handlerIndex, name, fields);
    }

    public int HandlerIndex { get; }

    public string Name { get; }

    public int Int64SlotCount { get; }

    public int DoubleSlotCount { get; }

    public RadarSourceProcessingHandlerExecutionClassification ExecutionClassification { get; }

    public IReadOnlyList<RadarProcessingHandlerOutputField> Fields => fields;

    internal static void EnsureKnownExecutionClassification(
        RadarSourceProcessingHandlerExecutionClassification executionClassification)
    {
        if (executionClassification is not RadarSourceProcessingHandlerExecutionClassification.Mergeable and
            not RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly and
            not RadarSourceProcessingHandlerExecutionClassification.Unsupported)
        {
            throw new ArgumentOutOfRangeException(nameof(executionClassification));
        }
    }

    private static IReadOnlyList<RadarProcessingHandlerOutputField> CopyFields(
        int handlerIndex,
        string handlerName,
        IReadOnlyList<RadarProcessingHandlerOutputField>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return Array.Empty<RadarProcessingHandlerOutputField>();
        }

        var result = new RadarProcessingHandlerOutputField[fields.Count];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i] ?? throw new ArgumentNullException(nameof(fields));
            if (field.HandlerIndex != handlerIndex)
            {
                throw new ArgumentException("Handler output field index must match descriptor index.", nameof(fields));
            }

            if (!string.Equals(field.HandlerName, handlerName, StringComparison.Ordinal))
            {
                throw new ArgumentException("Handler output field name must match descriptor name.", nameof(fields));
            }

            if (!names.Add(field.Name))
            {
                throw new ArgumentException(
                    "Handler output field names must be unique within a handler descriptor.",
                    nameof(fields));
            }

            result[i] = field;
        }

        return Array.AsReadOnly(result);
    }
}

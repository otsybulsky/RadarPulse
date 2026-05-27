using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

public sealed class RadarProcessingHandlerOutputContract
{
    private const string HandlerFreeMessage =
        "Handler-free processing can use ordered concurrent delta compute.";
    private const string StatefulSnapshotMessage =
        "Stateful handler output uses committed snapshots and requires sequential fallback until a handler delta/merge contract exists.";
    private const string MergeableMessage =
        "Mergeable handler output is eligible for ordered handler delta/merge once the runtime path is wired.";
    private const string UnsupportedMessage =
        "Unsupported handler output cannot participate in the MVP runtime surface.";

    private readonly IReadOnlyList<RadarProcessingHandlerOutputDescriptor> handlers;

    private RadarProcessingHandlerOutputContract(
        RadarProcessingHandlerStatePosture statePosture,
        IReadOnlyList<RadarProcessingHandlerOutputDescriptor> handlers,
        string message,
        string? firstBlockingReason = null)
    {
        EnsureKnownPosture(statePosture);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        StatePosture = statePosture;
        this.handlers = handlers;
        Message = message;
        FirstBlockingReason = firstBlockingReason;
    }

    public RadarProcessingHandlerStatePosture StatePosture { get; }

    public IReadOnlyList<RadarProcessingHandlerOutputDescriptor> Handlers => handlers;

    public string Message { get; }

    public string? FirstBlockingReason { get; }

    public bool HasHandlers => handlers.Count != 0;

    public bool IsBlocked => FirstBlockingReason is not null;

    public bool IsUnsupported =>
        StatePosture == RadarProcessingHandlerStatePosture.UnsupportedHandlerSet;

    public bool RequiresSequentialFallback =>
        StatePosture == RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback;

    public bool AllowsOrderedConcurrentDelta =>
        StatePosture == RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent;

    public bool AllowsOrderedConcurrentHandlerDeltaMerge =>
        StatePosture == RadarProcessingHandlerStatePosture.MergeableHandlerDeltaMergeEligible;

    public static RadarProcessingHandlerOutputContract FromOptions(
        RadarProcessingCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return FromHandlers(options.Handlers);
    }

    public static RadarProcessingHandlerOutputContract FromHandlers(
        IReadOnlyList<IRadarSourceProcessingHandler>? handlers)
    {
        if (handlers is null || handlers.Count == 0)
        {
            return new RadarProcessingHandlerOutputContract(
                RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent,
                Array.Empty<RadarProcessingHandlerOutputDescriptor>(),
                HandlerFreeMessage);
        }

        var descriptors = CreateHandlerDescriptors(handlers);
        var unsupported = descriptors.FirstOrDefault(static descriptor =>
            descriptor.ExecutionClassification == RadarSourceProcessingHandlerExecutionClassification.Unsupported);
        if (unsupported is not null)
        {
            return new RadarProcessingHandlerOutputContract(
                RadarProcessingHandlerStatePosture.UnsupportedHandlerSet,
                descriptors,
                UnsupportedMessage,
                $"Unsupported handler '{unsupported.Name}' blocks MVP processing.");
        }

        var snapshotOnly = descriptors.FirstOrDefault(static descriptor =>
            descriptor.ExecutionClassification == RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly);
        if (snapshotOnly is not null)
        {
            return new RadarProcessingHandlerOutputContract(
                RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback,
                descriptors,
                $"{StatefulSnapshotMessage} First snapshot-only handler: '{snapshotOnly.Name}'.");
        }

        return new RadarProcessingHandlerOutputContract(
            RadarProcessingHandlerStatePosture.MergeableHandlerDeltaMergeEligible,
            descriptors,
            MergeableMessage);
    }

    internal static void EnsureKnownPosture(
        RadarProcessingHandlerStatePosture statePosture)
    {
        if (statePosture is not RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent and
            not RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback and
            not RadarProcessingHandlerStatePosture.MergeableHandlerDeltaMergeEligible and
            not RadarProcessingHandlerStatePosture.UnsupportedHandlerSet)
        {
            throw new ArgumentOutOfRangeException(nameof(statePosture));
        }
    }

    private static IReadOnlyList<RadarProcessingHandlerOutputDescriptor> CreateHandlerDescriptors(
        IReadOnlyList<IRadarSourceProcessingHandler> handlers)
    {
        var result = new RadarProcessingHandlerOutputDescriptor[handlers.Count];
        var handlerNames = new HashSet<string>(StringComparer.Ordinal);
        var outputFieldNames = new HashSet<string>(StringComparer.Ordinal);

        for (var handlerIndex = 0; handlerIndex < handlers.Count; handlerIndex++)
        {
            var handler = handlers[handlerIndex] ?? throw new ArgumentNullException(nameof(handlers));
            var descriptor = handler.Descriptor ?? throw new ArgumentNullException(nameof(handlers));
            var executionClassification = GetExecutionClassification(handler);
            if (!handlerNames.Add(descriptor.Name))
            {
                throw new ArgumentException(
                    "Handler output names must be unique for frontend-facing discovery.",
                    nameof(handlers));
            }

            var fields = new RadarProcessingHandlerOutputField[descriptor.SnapshotFields.Count];
            for (var fieldIndex = 0; fieldIndex < descriptor.SnapshotFields.Count; fieldIndex++)
            {
                var field = descriptor.SnapshotFields[fieldIndex];
                if (!outputFieldNames.Add(field.Name))
                {
                    throw new ArgumentException(
                        "Handler output field names must be unique across all handlers.",
                        nameof(handlers));
                }

                fields[fieldIndex] = new RadarProcessingHandlerOutputField(
                    handlerIndex,
                    descriptor.Name,
                    field.Name,
                    field.Type,
                    field.SlotIndex);
            }

            result[handlerIndex] = new RadarProcessingHandlerOutputDescriptor(
                handlerIndex,
                descriptor.Name,
                descriptor.Int64SlotCount,
                descriptor.DoubleSlotCount,
                fields,
                executionClassification);
        }

        return Array.AsReadOnly(result);
    }

    private static RadarSourceProcessingHandlerExecutionClassification GetExecutionClassification(
        IRadarSourceProcessingHandler handler)
    {
        if (handler is not IRadarSourceProcessingHandlerExecutionMetadata metadata)
        {
            return RadarSourceProcessingHandlerExecutionClassification.SnapshotOnly;
        }

        var executionClassification = metadata.ExecutionClassification;
        RadarProcessingHandlerOutputDescriptor.EnsureKnownExecutionClassification(executionClassification);
        return executionClassification;
    }
}

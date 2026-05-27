using RadarPulse.Domain.Processing;

namespace RadarPulse.Application.Processing;

/// <summary>
/// BFF-facing contract describing handler output posture and exported fields.
/// </summary>
/// <remarks>
/// The contract converts backend handler descriptors into a stable product/UI
/// shape and records whether handler output is handler-free, snapshot fallback,
/// mergeable delta/merge eligible, or blocked by unsupported handlers.
/// </remarks>
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

    /// <summary>
    /// Handler state posture for the configured handler set.
    /// </summary>
    public RadarProcessingHandlerStatePosture StatePosture { get; }

    /// <summary>
    /// Handler descriptors exposed to BFF/product consumers.
    /// </summary>
    public IReadOnlyList<RadarProcessingHandlerOutputDescriptor> Handlers => handlers;

    /// <summary>
    /// Human-readable posture message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// First handler output blocker when the contract is blocked.
    /// </summary>
    public string? FirstBlockingReason { get; }

    /// <summary>
    /// Indicates whether any handlers are configured.
    /// </summary>
    public bool HasHandlers => handlers.Count != 0;

    /// <summary>
    /// Indicates whether handler output is blocked.
    /// </summary>
    public bool IsBlocked => FirstBlockingReason is not null;

    /// <summary>
    /// Indicates whether an unsupported handler set blocks output.
    /// </summary>
    public bool IsUnsupported =>
        StatePosture == RadarProcessingHandlerStatePosture.UnsupportedHandlerSet;

    /// <summary>
    /// Indicates whether output requires sequential snapshot fallback.
    /// </summary>
    public bool RequiresSequentialFallback =>
        StatePosture == RadarProcessingHandlerStatePosture.StatefulSnapshotSequentialFallback;

    /// <summary>
    /// Indicates whether handler-free ordered concurrency is allowed.
    /// </summary>
    public bool AllowsOrderedConcurrentDelta =>
        StatePosture == RadarProcessingHandlerStatePosture.HandlerFreeOrderedConcurrent;

    /// <summary>
    /// Indicates whether ordered handler delta/merge is allowed.
    /// </summary>
    public bool AllowsOrderedConcurrentHandlerDeltaMerge =>
        StatePosture == RadarProcessingHandlerStatePosture.MergeableHandlerDeltaMergeEligible;

    /// <summary>
    /// Creates handler output contract from processing core options.
    /// </summary>
    public static RadarProcessingHandlerOutputContract FromOptions(
        RadarProcessingCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return FromHandlers(options.Handlers);
    }

    /// <summary>
    /// Creates handler output contract from a handler set.
    /// </summary>
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

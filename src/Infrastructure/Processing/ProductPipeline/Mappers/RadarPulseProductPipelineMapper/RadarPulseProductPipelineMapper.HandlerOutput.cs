using RadarPulse.Application.Processing;
using RadarPulse.Application.Product;

namespace RadarPulse.Infrastructure.Processing;

public static partial class RadarPulseProductPipelineMapper
{
    /// <summary>
    /// Maps handler output contract evidence into product vocabulary.
    /// </summary>
    public static RadarPulseProductHandlerContract ToProductHandlerContract(
        RadarProcessingHandlerOutputContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var handlers = contract.Handlers
            .Select(ToProductHandlerDescriptor)
            .ToArray();

        return new RadarPulseProductHandlerContract(
            contract.StatePosture.ToString(),
            contract.Message,
            contract.FirstBlockingReason,
            contract.IsBlocked,
            Array.AsReadOnly(handlers));
    }

    /// <summary>
    /// Maps one handler output value into product vocabulary.
    /// </summary>
    public static RadarPulseProductHandlerOutput ToProductHandlerOutput(
        RadarProcessingHandlerOutputValueReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new RadarPulseProductHandlerOutput(
            value.HandlerIndex,
            value.HandlerName,
            value.Name,
            value.Type.ToString(),
            value.Int64Value,
            value.DoubleValue);
    }

    private static RadarPulseProductHandlerDescriptor ToProductHandlerDescriptor(
        RadarProcessingHandlerOutputDescriptor descriptor)
    {
        var fields = descriptor.Fields
            .Select(static field => new RadarPulseProductHandlerField(
                field.HandlerIndex,
                field.HandlerName,
                field.Name,
                field.Type.ToString(),
                field.SlotIndex))
            .ToArray();

        return new RadarPulseProductHandlerDescriptor(
            descriptor.HandlerIndex,
            descriptor.Name,
            descriptor.Int64SlotCount,
            descriptor.DoubleSlotCount,
            descriptor.ExecutionClassification.ToString(),
            Array.AsReadOnly(fields));
    }
}

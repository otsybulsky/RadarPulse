using RadarPulse.Application.Product;
using RadarPulse.Infrastructure.Product;

namespace RadarPulse.Tests.Product;

internal static class RadarPulseProductPipelineApiContractTestFactory
{
    public static RadarPulseProductPipelineApiContract Create(
        RadarPulseProductPipelineService service) =>
        new(
            service,
            service,
            service,
            service);
}

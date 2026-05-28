using RadarPulse.Domain.Processing;
using RadarPulse.Infrastructure.Processing;

namespace RadarPulse.Tests.Processing;

public sealed partial class RadarProcessingSyntheticRebalanceWorkloadTests
{
    [Fact]
    public void WorkloadFactoryRejectsUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RadarProcessingSyntheticRebalanceWorkload.Create(
                (RadarProcessingSyntheticRebalanceWorkloadKind)255));
    }
}

namespace RadarPulse.Tests;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("RADARPULSE_RUN_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set RADARPULSE_RUN_INTEGRATION_TESTS=true to run live AWS integration tests.";
        }
    }
}

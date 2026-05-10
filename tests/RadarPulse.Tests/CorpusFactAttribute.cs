namespace RadarPulse.Tests;

public sealed class CorpusFactAttribute : FactAttribute
{
    public CorpusFactAttribute()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("RADARPULSE_RUN_CORPUS_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set RADARPULSE_RUN_CORPUS_TESTS=true to run local NEXRAD corpus tests.";
        }
    }
}

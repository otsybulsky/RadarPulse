public sealed partial record ProcessingBenchmarkArchiveRebalanceOptions
{
    public static ProcessingBenchmarkArchiveRebalanceOptions Parse(string[] args)
    {
        var state = new ParseState();
        state.Read(args);
        state.ApplyRolloutDefaults();
        state.Validate();
        return state.ToOptions();
    }
}

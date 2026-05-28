internal static class ProcessingCliApplication
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "benchmark" => ProcessingBenchmarkCliApplication.Run(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }
}

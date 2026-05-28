internal static partial class ArchiveCliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "list" => await ListArchiveAsync(args[1..]),
            "download" => await DownloadArchiveAsync(args[1..]),
            "inspect" => await ArchiveInspectionCliApplication.RunAsync(args[1..]),
            "replay" => ReplayArchive(args[1..]),
            "stream" => StreamArchive(args[1..]),
            "benchmark" => ArchiveBenchmarkCliApplication.Run(args[1..]),
            "validate" => ArchiveValidationCliApplication.Run(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }
}

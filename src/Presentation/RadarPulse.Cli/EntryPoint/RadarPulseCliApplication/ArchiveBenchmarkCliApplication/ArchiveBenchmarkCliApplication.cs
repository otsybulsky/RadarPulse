using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;
using static CliFormat;

internal static partial class ArchiveBenchmarkCliApplication
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            return RadarPulseCliUsage.Print();
        }

        return args[0] switch
        {
            "decompress" => BenchmarkArchiveDecompression(args[1..]),
            "parse" => BenchmarkArchiveParse(args[1..]),
            "replay-shape" => BenchmarkArchiveReplayShape(args[1..]),
            "replay-publish" => BenchmarkArchiveReplayPublish(args[1..]),
            "stream" => BenchmarkArchiveStream(args[1..]),
            _ => RadarPulseCliUsage.Print()
        };
    }
}

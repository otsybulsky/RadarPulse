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
    static int BenchmarkArchiveReplayPublish(string[] args)
    {
        var options = ArchiveBenchmarkReplayPublishOptions.Parse(args);
        if (options.FilePath is null)
        {
            var cacheResult = new NexradArchiveReplayPublishBenchmark().MeasureCache(
                options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
                options.Date,
                options.RadarId,
                options.MaxFiles,
                options.Iterations,
                options.WarmupIterations,
                options.Parallelism,
                options.Decompressor,
                CancellationToken.None);
            PrintArchiveReplayPublishCacheBenchmarkResult(cacheResult);
            return 0;
        }

        var result = new NexradArchiveReplayPublishBenchmark().Measure(
            options.FilePath,
            options.Iterations,
            options.WarmupIterations,
            options.Parallelism,
            options.Decompressor,
            CancellationToken.None);
        PrintArchiveReplayPublishBenchmarkResult(result);
        return 0;
    }
}

using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

internal static class RadarPulseCliUsage
{
    public static int Print()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  radarpulse archive list --date yyyy-MM-dd --radar KTLX [--max-files n] [--max-bytes n] [--manifest path]");
        Console.WriteLine("  radarpulse archive list --date yyyy-MM-dd --all-radars [--max-files n] [--max-bytes n] [--manifest path]");
        Console.WriteLine("  radarpulse archive download --date yyyy-MM-dd --radar KTLX --output data/nexrad [--concurrency n]");
        Console.WriteLine("  radarpulse archive download --date yyyy-MM-dd --all-radars --output data/nexrad [--concurrency n]");
        Console.WriteLine("  radarpulse archive download --manifest data/manifests/2026-05-04.json --output data/nexrad [--radar KTLX] [--max-files n] [--max-bytes n] [--concurrency n]");
        Console.WriteLine("  radarpulse archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06");
        Console.WriteLine("  radarpulse archive inspect --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]");
        Console.WriteLine("  radarpulse archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive replay --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive stream --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--decode-moments] [--decode-calibrated-moments]");
        Console.WriteLine("  radarpulse archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive benchmark replay-publish --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive benchmark replay-publish --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n] [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse archive benchmark stream (--file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 | --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]) [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        Console.WriteLine("  radarpulse processing benchmark synthetic [--mode sequential|partitioned|async] [--sources n] [--batches n] [--events-per-batch n] [--payload-values n] [--partitions n] [--shards n] [--workers n] [--queue-capacity n] [--handlers none|counter-checksum|counter-checksum-heavy] [--iterations n] [--warmup-iterations n]");
        Console.WriteLine("  radarpulse processing benchmark rebalance-synthetic [--workload balanced|hot-shard|intrinsic-hot|oscillating|cooldown-storm|quarantine-ttl-retry|quarantine-cooling-clear|quarantine-pressure-change-retry|quarantine-retry-reentry|quarantine-successful-relief-clear|long-no-hot-shard|long-cooldown-rejection|long-unsafe-target-rejection|long-mixed-skipped-reasons|counters-only-retention|all] [--mode static|sampling|rebalance|ordered-rebalance|all] [--active-batches n] [--execution sync|async] [--workers n] [--queue-capacity n] [--validation-profile off|essential|diagnostic|benchmark] [--quarantine-ttl-evaluations n] [--quarantine-sustained-cooling-samples n] [--quarantine-material-pressure-change n] [--iterations n] [--warmup-iterations n]");
        Console.WriteLine("  radarpulse processing benchmark rebalance-archive (--file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 | --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]) [--mode static|sampling|rebalance|all] [--provider blocking-borrowed|queued-owned] [--provider-overlap none|producer-consumer] [--retention-strategy snapshot-copy|pooled-copy|builder-transfer] [--execution sync|async] [--workers n] [--queue-capacity n] [--queue-timeout-ms n] [--queue-retained-bytes n] [--queue-telemetry none|summary|recent] [--overlap-telemetry none|summary|recent] [--overlap-consumer-delay-ms n] [--partitions n] [--shards n] [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--validation-profile off|essential|diagnostic|benchmark] [--quarantine-ttl-evaluations n] [--quarantine-sustained-cooling-samples n] [--quarantine-material-pressure-change n] [--retention-mode counters|recent|diagnostic] [--max-retained-decisions n] [--max-retained-transitions n] [--max-retained-accepted-moves n] [--max-retained-validation-failures n] [--skew-profile none|hot-shard|rotating-hot-shard|hot-partition|target-starvation|budget-storm] [--skew-factor n] [--skew-period n]");
        Console.WriteLine("  radarpulse processing benchmark ordered-archive-processing (--file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 | --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]) [--active-batches n] [--partitions n] [--shards n] [--handlers none|counter-checksum|counter-checksum-heavy] [--iterations n] [--warmup-iterations n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--queue-telemetry none|summary|recent] [--overlap-telemetry none|summary|recent]");
        Console.WriteLine("  radarpulse product pipeline demo [--run-id id] [--sources n] [--batches n] [--events-per-batch n] [--partitions n] [--shards n] [--handlers none|counter-checksum|counter-checksum-heavy|snapshot-counting|unsupported] [--workers n] [--worker-queue-capacity n] [--provider-queue-capacity n] [--active-batches n]");
        Console.WriteLine("  radarpulse product pipeline run-archive --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--run-id id] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress] [--handlers none|counter-checksum|counter-checksum-heavy|snapshot-counting|unsupported]");
        Console.WriteLine("    rebalance-archive omitted-provider default: queued-owned + pooled-copy + producer-consumer, async workers 4, queue capacity 8, retained-byte budget 536870912, retained-payload prewarm on.");
        Console.WriteLine("    rebalance-archive fallback/oracle: use --provider blocking-borrowed for the borrowed path and same-run comparison.");
        Console.WriteLine("    rebalance-archive direct MeasureFile()/MeasureCache() defaults use the same queued-owned rollout contour.");
        Console.WriteLine("    ordered-archive-processing uses the runtime/archive MVP path; handler-free rows use RunProcessingAsync and handler rows use RunMvpProcessingAsync.");
        Console.WriteLine("    --overlap-consumer-delay-ms is controlled mechanics proof, not natural rollout evidence.");
        Console.WriteLine("  radarpulse archive validate decompress (--file path | --cache data/nexrad [--radar KTLX] [--max-files n])");
        Console.WriteLine("  radarpulse archive validate replay-shape (--file path | --cache data/nexrad [--radar KTLX] [--max-files n]) [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]");
        return 2;
    }

}

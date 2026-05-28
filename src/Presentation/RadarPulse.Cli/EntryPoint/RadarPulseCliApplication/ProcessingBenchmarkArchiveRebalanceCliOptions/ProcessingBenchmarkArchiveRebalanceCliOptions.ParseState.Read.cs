using System.Globalization;
using RadarPulse.Application.Archive;
using RadarPulse.Application.Product;
using RadarPulse.Domain.Archive;
using RadarPulse.Domain.Processing;
using RadarPulse.Domain.Streaming;
using RadarPulse.Infrastructure.Archive;
using RadarPulse.Infrastructure.Processing;
using RadarPulse.Infrastructure.Product;

public sealed partial record ProcessingBenchmarkArchiveRebalanceOptions
{
    private sealed partial class ParseState
    {
        public void Read(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--file":
                        filePath = RequireValue(args, ref i, "--file");
                        break;
                    case "--cache":
                        cachePath = RequireValue(args, ref i, "--cache");
                        break;
                    case "--date":
                        date = DateOnly.Parse(RequireValue(args, ref i, "--date"));
                        break;
                    case "--radar":
                        radarId = HistoricalArchiveRequest.NormalizeRadarId(RequireValue(args, ref i, "--radar"));
                        break;
                    case "--max-files":
                        maxFiles = int.Parse(RequireValue(args, ref i, "--max-files"));
                        maxFilesWasProvided = true;
                        break;
                    case "--mode":
                        modes = ParseMode(RequireValue(args, ref i, "--mode"));
                        break;
                    case "--provider":
                        providerMode = ParseProviderMode(RequireValue(args, ref i, "--provider"));
                        providerModeWasProvided = true;
                        break;
                    case "--provider-overlap":
                        providerOverlapMode = ParseProviderOverlapMode(RequireValue(args, ref i, "--provider-overlap"));
                        providerOverlapModeWasProvided = true;
                        break;
                    case "--retention-strategy":
                        retentionStrategy = ParseRetentionStrategy(RequireValue(args, ref i, "--retention-strategy"));
                        retentionStrategyWasProvided = true;
                        break;
                    case "--execution":
                        executionMode = ParseExecutionMode(RequireValue(args, ref i, "--execution"));
                        executionModeWasProvided = true;
                        break;
                    case "--workers":
                        workerCount = int.Parse(RequireValue(args, ref i, "--workers"));
                        workerCountWasProvided = true;
                        break;
                    case "--queue-capacity":
                        queueCapacity = int.Parse(RequireValue(args, ref i, "--queue-capacity"));
                        queueCapacityWasProvided = true;
                        break;
                    case "--queue-timeout-ms":
                        queueTimeout = TimeSpan.FromMilliseconds(
                            double.Parse(RequireValue(args, ref i, "--queue-timeout-ms"), CultureInfo.InvariantCulture));
                        break;
                    case "--queue-retained-bytes":
                        queueRetainedPayloadBytes = long.Parse(RequireValue(args, ref i, "--queue-retained-bytes"));
                        queueRetainedPayloadBytesWasProvided = true;
                        break;
                    case "--queue-telemetry":
                        queueTelemetryOutput = ParseQueueTelemetryOutput(RequireValue(args, ref i, "--queue-telemetry"));
                        queueTelemetryWasProvided = true;
                        break;
                    case "--overlap-telemetry":
                        overlapTelemetryOutput = ParseOverlapTelemetryOutput(
                            RequireValue(args, ref i, "--overlap-telemetry"));
                        overlapTelemetryWasProvided = true;
                        break;
                    case "--overlap-consumer-delay-ms":
                        overlapConsumerDelay = TimeSpan.FromMilliseconds(
                            double.Parse(
                                RequireValue(args, ref i, "--overlap-consumer-delay-ms"),
                                CultureInfo.InvariantCulture));
                        overlapConsumerDelayWasProvided = true;
                        break;
                    case "--partitions":
                        partitionCount = int.Parse(RequireValue(args, ref i, "--partitions"));
                        break;
                    case "--shards":
                        shardCount = int.Parse(RequireValue(args, ref i, "--shards"));
                        break;
                    case "--iterations":
                        iterations = int.Parse(RequireValue(args, ref i, "--iterations"));
                        break;
                    case "--warmup-iterations":
                        warmupIterations = int.Parse(RequireValue(args, ref i, "--warmup-iterations"));
                        break;
                    case "--parallelism":
                        parallelism = int.Parse(RequireValue(args, ref i, "--parallelism"));
                        break;
                    case "--decompressor":
                        decompressor = RequireValue(args, ref i, "--decompressor");
                        break;
                    case "--validation-profile":
                        validationProfile = ParseValidationProfile(RequireValue(args, ref i, "--validation-profile"));
                        break;
                    case "--quarantine-ttl":
                    case "--quarantine-ttl-evaluations":
                        quarantineTtlEvaluations = int.Parse(RequireValue(args, ref i, args[i]));
                        break;
                    case "--quarantine-sustained-cooling-samples":
                    case "--quarantine-sustained-cooling-sample-count":
                        sustainedCoolingSampleCount = int.Parse(RequireValue(args, ref i, args[i]));
                        break;
                    case "--quarantine-material-pressure-change":
                    case "--quarantine-material-pressure-change-threshold":
                        materialPressureChangeThreshold = double.Parse(
                            RequireValue(args, ref i, args[i]),
                            CultureInfo.InvariantCulture);
                        break;
                    case "--retention-mode":
                        retentionMode = ParseRetentionMode(RequireValue(args, ref i, "--retention-mode"));
                        break;
                    case "--max-retained-decisions":
                        maxRetainedDecisions = int.Parse(RequireValue(args, ref i, "--max-retained-decisions"));
                        break;
                    case "--max-retained-transitions":
                    case "--max-retained-lifecycle-transitions":
                        maxRetainedTransitions = int.Parse(RequireValue(args, ref i, args[i]));
                        break;
                    case "--max-retained-accepted-moves":
                        maxRetainedAcceptedMoves = int.Parse(RequireValue(args, ref i, "--max-retained-accepted-moves"));
                        break;
                    case "--max-retained-validation-failures":
                        maxRetainedValidationFailures = int.Parse(
                            RequireValue(args, ref i, "--max-retained-validation-failures"));
                        break;
                    case "--skew-profile":
                    case "--pressure-skew-profile":
                        skewProfile = ParsePressureSkewProfile(RequireValue(args, ref i, args[i]));
                        break;
                    case "--skew-factor":
                    case "--pressure-skew-factor":
                        skewFactor = double.Parse(
                            RequireValue(args, ref i, args[i]),
                            CultureInfo.InvariantCulture);
                        break;
                    case "--skew-period":
                    case "--pressure-skew-period":
                        skewPeriod = int.Parse(RequireValue(args, ref i, args[i]));
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {args[i]}");
                }
            }
        }
    }
}

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

internal static partial class ProcessingBenchmarkCliReporter
{
    public static void PrintProcessingRebalanceMovePressures(
        IReadOnlyList<RadarProcessingSyntheticRebalanceMovePressure> acceptedMovePressures)
    {
        const int displayedMovePressureLimit = 8;

        if (acceptedMovePressures.Count == 0)
        {
            Console.WriteLine("Accepted move pressures: (none)");
            return;
        }

        Console.WriteLine("Accepted move pressures:");
        var displayedCount = Math.Min(acceptedMovePressures.Count, displayedMovePressureLimit);
        for (var i = 0; i < displayedCount; i++)
        {
            var pressure = acceptedMovePressures[i];
            Console.WriteLine(
                $"  {FormatNumber(i + 1)}. {FormatProcessingRebalanceMoveKind(pressure.MoveKind)} " +
                $"source {FormatDecimal(pressure.SourceShardBefore)}->{FormatDecimal(pressure.SourceShardAfter)}, " +
                $"target {FormatDecimal(pressure.TargetShardBefore)}->{FormatDecimal(pressure.TargetShardAfter)}, " +
                $"relief {FormatDecimal(pressure.ExpectedRelief)}");
        }

        var omittedCount = acceptedMovePressures.Count - displayedCount;
        if (omittedCount > 0)
        {
            Console.WriteLine($"  ... {FormatNumber(omittedCount)} more accepted move pressure samples omitted");
        }
    }

    public static void PrintProcessingRebalanceRetentionStats(
        RadarProcessingRebalanceRetentionStats stats)
    {
        Console.WriteLine($"Retained decisions: {FormatNumber(stats.RetainedDecisionCount)}");
        Console.WriteLine($"Dropped decision details: {FormatNumber(stats.DroppedDecisionCount)}");
        Console.WriteLine($"Retained lifecycle transitions: {FormatNumber(stats.RetainedLifecycleTransitionCount)}");
        Console.WriteLine($"Dropped lifecycle transition details: {FormatNumber(stats.DroppedLifecycleTransitionCount)}");
        Console.WriteLine($"Retained accepted moves: {FormatNumber(stats.RetainedAcceptedMoveCount)}");
        Console.WriteLine($"Dropped accepted move details: {FormatNumber(stats.DroppedAcceptedMoveCount)}");
        Console.WriteLine($"Retained validation failures: {FormatNumber(stats.RetainedValidationFailureCount)}");
        Console.WriteLine($"Dropped validation failure details: {FormatNumber(stats.DroppedValidationFailureCount)}");
    }

    public static void PrintProcessingQuarantineLifecycle(
        int quarantineTtlEvaluations,
        int sustainedCoolingSampleCount,
        double materialPressureChangeThreshold)
    {
        Console.WriteLine($"Quarantine TTL evaluations: {FormatNumber(quarantineTtlEvaluations)}");
        Console.WriteLine($"Quarantine sustained cooling samples: {FormatNumber(sustainedCoolingSampleCount)}");
        Console.WriteLine($"Quarantine material pressure change: {FormatDecimal(materialPressureChangeThreshold)}");
    }

    public static void PrintProcessingPressureSkew(
        RadarProcessingPressureSkewOptions options)
    {
        Console.WriteLine($"Synthetic pressure overlay: {FormatBoolean(options.IsEnabled)}");
        Console.WriteLine($"Pressure skew profile: {FormatProcessingPressureSkewProfile(options.Profile)}");
        Console.WriteLine($"Pressure skew factor: {FormatDecimal(options.Factor)}");
        Console.WriteLine($"Pressure skew period: {FormatNumber(options.Period)}");
    }

}

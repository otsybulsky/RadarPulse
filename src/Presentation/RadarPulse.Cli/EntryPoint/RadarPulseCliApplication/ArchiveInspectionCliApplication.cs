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

internal static class ArchiveInspectionCliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var options = ArchiveInspectOptions.Parse(args);
        if (options.FilePath is not null)
        {
            var inspection = await new NexradArchiveFileInspector().InspectAsync(options.FilePath, CancellationToken.None);
            PrintArchiveFileInspection(inspection);
            return 0;
        }

        var cacheInspection = await new NexradArchiveCacheInspector().InspectAsync(
            options.CachePath ?? throw new InvalidOperationException("--cache is required when --file is not provided."),
            options.Date,
            options.RadarId,
            options.MaxFiles,
            CancellationToken.None);
        PrintArchiveCacheInspection(cacheInspection);
        return 0;
    }

    static void PrintArchiveFileInspection(NexradArchiveFileInspection inspection)
    {
        Console.WriteLine($"File: {inspection.FilePath}");
        Console.WriteLine($"Size bytes: {FormatNumber(inspection.SizeBytes)}");
        Console.WriteLine($"Kind: {FormatNexradArchiveFileKind(inspection.FileKind)}");
        if (inspection.ArchiveTwoVolumeHeader is { } header)
        {
            Console.WriteLine($"Archive filename: {header.ArchiveFilename}");
            Console.WriteLine($"Version: {header.Version}");
            Console.WriteLine($"Extension number: {header.ExtensionNumber}");
            Console.WriteLine($"Radar: {header.RadarId}");
            Console.WriteLine($"Volume time: {header.VolumeTimestamp:yyyy-MM-ddTHH:mm:ss.fffZ}");
        }

        if (inspection.CompressedRecords.Count > 0)
        {
            var compressedBytes = inspection.CompressedRecords.Sum(record => (long)record.CompressedSizeBytes);
            var recordsWithBZip2Signature = inspection.CompressedRecords.Count(record => record.StartsWithBZip2Signature);
            var decompressedRecordCount = inspection.CompressedRecords.Count(record => record.DecompressedSizeBytes is not null);
            var decompressedBytes = inspection.CompressedRecords.Sum(record => record.DecompressedSizeBytes ?? 0L);
            var recordsWithDecompressionDiagnostics = inspection.CompressedRecords.Count(record => record.DecompressionDiagnostic is not null);
            var firstRecord = inspection.CompressedRecords[0];
            Console.WriteLine($"Compressed records: {FormatNumber(inspection.CompressedRecords.Count)}");
            Console.WriteLine($"Compressed bytes: {FormatNumber(compressedBytes)}");
            Console.WriteLine($"Records with BZip2 signature: {FormatNumber(recordsWithBZip2Signature)}");
            Console.WriteLine($"Decompressed records: {FormatNumber(decompressedRecordCount)}");
            Console.WriteLine($"Decompressed bytes: {FormatNumber(decompressedBytes)}");
            Console.WriteLine($"Records with decompression diagnostics: {FormatNumber(recordsWithDecompressionDiagnostics)}");
            Console.WriteLine($"First record compressed bytes: {FormatNumber(firstRecord.CompressedSizeBytes)}");
            if (firstRecord.DecompressedSizeBytes is not null)
            {
                Console.WriteLine($"First record decompressed bytes: {FormatNumber(firstRecord.DecompressedSizeBytes.Value)}");
            }
        }

        if (inspection.MessageSummary is { MessageCount: > 0 } messages)
        {
            Console.WriteLine($"Messages: {FormatNumber(messages.MessageCount)}");
            Console.WriteLine("Message types: " + string.Join(", ", messages.MessageTypes.Select(type => $"{type.MessageType}={FormatNumber(type.Count)}")));
            Console.WriteLine($"Type 31 radials: {FormatNumber(messages.Type31.RadialCount)}");
            Console.WriteLine($"Estimated gate-moment events: {FormatNumber(messages.Type31.EstimatedGateMomentEventCount)}");
            if (messages.Type31.ConstantBlocks is { VolumeCount: > 0 } or { ElevationCount: > 0 } or { RadialCount: > 0 })
            {
                Console.WriteLine(
                    "Type 31 constant blocks: " +
                    $"VOL={FormatNumber(messages.Type31.ConstantBlocks.VolumeCount)}, " +
                    $"ELV={FormatNumber(messages.Type31.ConstantBlocks.ElevationCount)}, " +
                    $"RAD={FormatNumber(messages.Type31.ConstantBlocks.RadialCount)}");
            }

            if (messages.Type31.Moments.Count > 0)
            {
                Console.WriteLine("Moment calibration formula: value=(raw-offset)/scale");
                Console.WriteLine("Moments:");
                foreach (var moment in messages.Type31.Moments)
                {
                    Console.WriteLine($"  {FormatMomentSummary(moment)}");
                }
            }

            if (messages.Type31.Sweeps.Count > 0)
            {
                Console.WriteLine($"Sweeps: {FormatNumber(messages.Type31.Sweeps.Count)}");
                foreach (var sweep in messages.Type31.Sweeps)
                {
                    Console.WriteLine(
                        $"Sweep {FormatNumber(sweep.SequenceNumber)}: " +
                        $"elevation={FormatNumber(sweep.ElevationNumber)}, " +
                        $"cutSector={FormatCutSectorRange(sweep.MinimumCutSectorNumber, sweep.MaximumCutSectorNumber)}, " +
                        $"radials={FormatNumber(sweep.RadialCount)}, " +
                        $"angle={FormatDegrees(sweep.MinimumElevationAngleDegrees)}-{FormatDegrees(sweep.MaximumElevationAngleDegrees)} deg " +
                        $"avg={FormatDegrees(sweep.AverageElevationAngleDegrees)} deg, " +
                        $"status={FormatRadialStatus(sweep.StartRadialStatus)}->{FormatRadialStatus(sweep.EndRadialStatus)}, " +
                        $"source={FormatSourceOrder(sweep.FirstRadial)}->{FormatSourceOrder(sweep.LastRadial)}, " +
                        $"moments={FormatMomentNames(sweep.Moments)}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(inspection.Diagnostic))
        {
            Console.WriteLine($"Diagnostic: {inspection.Diagnostic}");
        }
    }

    static void PrintArchiveCacheInspection(NexradArchiveCacheInspection inspection)
    {
        Console.WriteLine($"Cache: {inspection.CachePath}");
        if (inspection.Date is { } date)
        {
            Console.WriteLine($"Date: {date:yyyy-MM-dd}");
        }

        if (inspection.RadarId is not null)
        {
            Console.WriteLine($"Radar: {inspection.RadarId}");
        }

        Console.WriteLine($"Examined files: {FormatNumber(inspection.ExaminedFileCount)}");
        Console.WriteLine($"Archive Two base-data files: {FormatNumber(inspection.ArchiveTwoBaseDataFileCount)}");
        Console.WriteLine($"MDM/compressed-stream files: {FormatNumber(inspection.MdmOrCompressedStreamFileCount)}");
        Console.WriteLine($"Unknown files: {FormatNumber(inspection.UnknownFileCount)}");
        Console.WriteLine($"Files with diagnostics: {FormatNumber(inspection.DiagnosticFileCount)}");
        Console.WriteLine($"Size bytes: {FormatNumber(inspection.TotalSizeBytes)}");
        Console.WriteLine($"Compressed records: {FormatNumber(inspection.TotalCompressedRecordCount)}");
        Console.WriteLine($"Compressed bytes: {FormatNumber(inspection.TotalCompressedBytes)}");
        Console.WriteLine($"Records with BZip2 signature: {FormatNumber(inspection.TotalRecordsWithBZip2Signature)}");
        Console.WriteLine($"Decompressed records: {FormatNumber(inspection.TotalDecompressedRecordCount)}");
        Console.WriteLine($"Decompressed bytes: {FormatNumber(inspection.TotalDecompressedBytes)}");
        Console.WriteLine($"Messages: {FormatNumber(inspection.TotalMessages)}");
        Console.WriteLine($"Type 31 radials: {FormatNumber(inspection.TotalType31Radials)}");
        Console.WriteLine($"Estimated gate-moment events: {FormatNumber(inspection.TotalEstimatedGateMomentEvents)}");

        if (inspection.Files.Count == 0)
        {
            Console.WriteLine("Diagnostic: no files matched the cache inspection filters.");
            return;
        }

        Console.WriteLine("Files:");
        foreach (var file in inspection.Files)
        {
            var compressedRecordCount = file.CompressedRecords.Count;
            var decompressedBytes = file.CompressedRecords.Sum(record => record.DecompressedSizeBytes ?? 0L);
            var messageCount = file.MessageSummary?.MessageCount ?? 0;
            var type31RadialCount = file.MessageSummary?.Type31.RadialCount ?? 0;
            var diagnostic = HasInspectionDiagnostic(file) ? ", diagnostic=yes" : string.Empty;
            Console.WriteLine(
                $"  {Path.GetFileName(file.FilePath)}: " +
                $"{FormatNexradArchiveFileKind(file.FileKind)}, " +
                $"records={FormatNumber(compressedRecordCount)}, " +
                $"decompressed={FormatNumber(decompressedBytes)}, " +
                $"messages={FormatNumber(messageCount)}, " +
                $"type31 radials={FormatNumber(type31RadialCount)}" +
                diagnostic);
        }
    }

    static bool HasInspectionDiagnostic(NexradArchiveFileInspection inspection) =>
        !string.IsNullOrWhiteSpace(inspection.Diagnostic) ||
        inspection.CompressedRecords.Any(record => !string.IsNullOrWhiteSpace(record.DecompressionDiagnostic));

    static string FormatNexradArchiveFileKind(NexradArchiveFileKind fileClass) =>
        fileClass switch
        {
            NexradArchiveFileKind.ArchiveTwoBaseData => "Archive Two base data",
            NexradArchiveFileKind.MdmOrCompressedStream => "MDM or compressed stream",
            _ => "Unknown"
        };

    static string FormatDegrees(float value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    static string FormatSourceOrder(ArchiveTwoRadialSourceOrder sourceOrder) =>
        $"{FormatNumber(sourceOrder.CompressedRecordSequenceNumber)}/" +
        $"{FormatNumber(sourceOrder.MessageSequenceNumberInRecord)}/" +
        $"{FormatNumber(sourceOrder.Type31RadialSequenceNumber)}";

    static string FormatMomentNames(IReadOnlyList<string> moments) =>
        moments.Count == 0
            ? "none"
            : string.Join(",", moments);

    static string FormatMomentSummary(ArchiveTwoMomentSummary moment) =>
        $"{moment.Name}: {FormatNumber(moment.GateCount)} gates/{FormatNumber(moment.RadialCount)} radials, " +
        $"gates/radial={FormatIntRange(moment.MinimumGateCount, moment.MaximumGateCount)}, " +
        $"wordSize={FormatIntRange(moment.MinimumWordSizeBits, moment.MaximumWordSizeBits)} bits, " +
        $"firstGate={FormatFloatRange(moment.MinimumFirstGateRangeKilometers, moment.MaximumFirstGateRangeKilometers)} km, " +
        $"gateSpacing={FormatFloatRange(moment.MinimumGateSpacingKilometers, moment.MaximumGateSpacingKilometers)} km, " +
        $"scale={FormatFloatRange(moment.MinimumScale, moment.MaximumScale)}, " +
        $"offset={FormatFloatRange(moment.MinimumOffset, moment.MaximumOffset)}";

    static string FormatIntRange(int minimum, int maximum) =>
        minimum == maximum
            ? FormatNumber(minimum)
            : $"{FormatNumber(minimum)}-{FormatNumber(maximum)}";

    static string FormatFloatRange(float minimum, float maximum) =>
        MathF.Abs(minimum - maximum) < 0.0005f
            ? FormatCompactFloat(minimum)
            : $"{FormatCompactFloat(minimum)}-{FormatCompactFloat(maximum)}";

    static string FormatCompactFloat(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    static string FormatCompactDouble(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    static string FormatCutSectorRange(int minimum, int maximum) =>
        minimum == maximum
            ? FormatNumber(minimum)
            : $"{FormatNumber(minimum)}-{FormatNumber(maximum)}";

    static string FormatRadialStatus(int status) =>
        status switch
        {
            0 or 80 => $"start elevation ({status})",
            1 or 81 => $"intermediate ({status})",
            2 or 82 => $"end elevation ({status})",
            3 or 83 => $"start volume ({status})",
            4 or 84 => $"end volume ({status})",
            5 or 85 => $"start last elevation ({status})",
            _ => status.ToString(CultureInfo.InvariantCulture)
        };
}

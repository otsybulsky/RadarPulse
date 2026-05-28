using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using RadarPulse.Domain.Archive;

namespace RadarPulse.Infrastructure.Archive;

public sealed partial class NexradArchiveParseBenchmark
{
    private static IReadOnlyList<ArchiveTwoParseBenchmarkWorker> CreateWorkers(
        IArchiveBZip2Decompressor decompressor,
        int degreeOfParallelism,
        bool decodeMomentValues,
        bool decodeCalibratedMomentValues)
    {
        var workers = new ArchiveTwoParseBenchmarkWorker[degreeOfParallelism];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new ArchiveTwoParseBenchmarkWorker(
                decompressor.CreateSession(),
                decodeMomentValues,
                decodeCalibratedMomentValues);
        }

        return workers;
    }

    private static void ThrowSingleInnerExceptionWhenUseful(AggregateException exception)
    {
        var flattened = exception.Flatten();
        if (flattened.InnerExceptions.Count != 1)
        {
            return;
        }

        var innerException = flattened.InnerExceptions[0];
        if (innerException is InvalidDataException or IOException)
        {
            ExceptionDispatchInfo.Capture(innerException).Throw();
        }
    }
}

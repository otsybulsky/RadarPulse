using System.Diagnostics;
using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Single async shard worker backed by a bounded mailbox.
/// </summary>
/// <remarks>
/// The worker is an infrastructure detail of the worker group. It measures
/// queue wait and execution time, maps cancellation/failure into work
/// completions, and reports completions through shared batch state.
/// </remarks>
internal sealed class RadarProcessingAsyncWorker : IDisposable
{
    private readonly RadarProcessingWorkerMailbox<RadarProcessingAsyncWorkerRequest> mailbox;
    private Task? runTask;
    private int started;
    private int runningCount;

    /// <summary>
    /// Creates a worker with its id and bounded mailbox options.
    /// </summary>
    public RadarProcessingAsyncWorker(
        RadarProcessingWorkerId id,
        RadarProcessingWorkerMailboxOptions mailboxOptions)
    {
        ArgumentNullException.ThrowIfNull(mailboxOptions);

        Id = id;
        mailbox = new RadarProcessingWorkerMailbox<RadarProcessingAsyncWorkerRequest>(mailboxOptions);
    }

    /// <summary>
    /// Worker id used by dispatch plans.
    /// </summary>
    public RadarProcessingWorkerId Id { get; }

    /// <summary>
    /// Number of requests accepted by the mailbox but not yet executing.
    /// </summary>
    public int PendingCount => mailbox.PendingCount;

    /// <summary>
    /// Number of requests currently executing on this worker.
    /// </summary>
    public int RunningCount => Volatile.Read(ref runningCount);

    /// <summary>
    /// Completion task for the worker loop.
    /// </summary>
    public Task Completion => Volatile.Read(ref runTask) ?? Task.CompletedTask;

    /// <summary>
    /// Starts the worker loop once.
    /// </summary>
    public void Start(
        CancellationToken cancellationToken,
        Action<Exception> faultHandler)
    {
        ArgumentNullException.ThrowIfNull(faultHandler);

        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            return;
        }

        runTask = Task.Run(
            () => RunAsync(cancellationToken, faultHandler),
            CancellationToken.None);
    }

    /// <summary>
    /// Attempts to enqueue a request into the worker mailbox.
    /// </summary>
    public RadarProcessingWorkerMailboxEnqueueResult TryEnqueue(
        RadarProcessingAsyncWorkerRequest request) =>
        mailbox.TryEnqueue(request);

    /// <summary>
    /// Closes the worker mailbox to new requests.
    /// </summary>
    public void Close() => mailbox.Close();

    /// <summary>
    /// Disposes the worker mailbox and drops buffered requests.
    /// </summary>
    public void Dispose() => mailbox.Dispose();

    private async Task RunAsync(
        CancellationToken cancellationToken,
        Action<Exception> faultHandler)
    {
        try
        {
            while (true)
            {
                var dequeue = await mailbox.DequeueAsync(cancellationToken).ConfigureAwait(false);
                if (!dequeue.HasItem)
                {
                    return;
                }

                await ExecuteAsync(dequeue.Item!, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            faultHandler(exception);
        }
    }

    private async ValueTask ExecuteAsync(
        RadarProcessingAsyncWorkerRequest request,
        CancellationToken workerCancellationToken)
    {
        var queueWaitTime = Stopwatch.GetElapsedTime(request.EnqueuedTimestamp);
        if (request.CancellationToken.IsCancellationRequested)
        {
            request.BatchState.RecordCompletion(
                RadarProcessingAsyncWorkCompletion.Canceled(
                    request.WorkItem,
                    queueWaitTime,
                    cancellationKind: request.BatchState.GetCancellationKind(executionStarted: false)));
            return;
        }

        Interlocked.Increment(ref runningCount);
        var executionStarted = Stopwatch.GetTimestamp();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            workerCancellationToken,
            request.CancellationToken);
        var executionCancellationToken = linkedCancellation.Token;
        RadarProcessingAsyncWorkCompletion completion;

        try
        {
            var executorCompletion = await request.Executor(
                request.WorkItem,
                executionCancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(executorCompletion);

            completion = WithMeasuredTiming(
                request.WorkItem,
                executorCompletion,
                queueWaitTime,
                Stopwatch.GetElapsedTime(executionStarted));
        }
        catch (OperationCanceledException) when (executionCancellationToken.IsCancellationRequested)
        {
            completion = RadarProcessingAsyncWorkCompletion.Canceled(
                request.WorkItem,
                queueWaitTime,
                Stopwatch.GetElapsedTime(executionStarted),
                request.BatchState.GetCancellationKind(executionStarted: true));
        }
        catch
        {
            completion = RadarProcessingAsyncWorkCompletion.Failed(
                request.WorkItem,
                queueWaitTime,
                Stopwatch.GetElapsedTime(executionStarted),
                RadarProcessingAsyncFailureKind.WorkerException);
        }
        finally
        {
            Interlocked.Decrement(ref runningCount);
        }

        request.BatchState.RecordCompletion(completion);
    }

    private static RadarProcessingAsyncWorkCompletion WithMeasuredTiming(
        RadarProcessingAsyncWorkItem workItem,
        RadarProcessingAsyncWorkCompletion executorCompletion,
        TimeSpan queueWaitTime,
        TimeSpan executionTime) =>
        new(
            workItem.BatchSequence,
            workItem.WorkItemId,
            workItem.TopologyVersion,
            workItem.WorkerId,
            executorCompletion.Status,
            queueWaitTime,
            executionTime,
            executorCompletion.ProcessedStreamEventCount,
            executorCompletion.ProcessedPayloadValueCount,
            executorCompletion.FailureKind,
            executorCompletion.CancellationKind);
}

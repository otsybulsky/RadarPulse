using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed partial class RadarProcessingAsyncWorkerGroup : IDisposable, IAsyncDisposable
{
    private void ValidateWorkItems(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems)
    {
        if (workItems.Count != scope.ExpectedWorkItemCount)
        {
            throw new ArgumentException("Work item count must match the batch scope.", nameof(workItems));
        }

        var seenWorkItemIds = new bool[scope.ExpectedWorkItemCount];
        for (var i = 0; i < workItems.Count; i++)
        {
            var workItem = workItems[i];
            ArgumentNullException.ThrowIfNull(workItem, nameof(workItems));

            if (workItem.BatchSequence != scope.BatchSequence)
            {
                throw new ArgumentException("Work item batch sequence must match the batch scope.", nameof(workItems));
            }

            if (workItem.TopologyVersion != scope.TopologyVersion)
            {
                throw new ArgumentException("Work item topology version must match the batch scope.", nameof(workItems));
            }

            if ((uint)workItem.WorkItemId >= (uint)scope.ExpectedWorkItemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(workItems));
            }

            if (seenWorkItemIds[workItem.WorkItemId])
            {
                throw new ArgumentException("Work item ids must be unique within the batch scope.", nameof(workItems));
            }

            if ((uint)workItem.WorkerId.Value >= (uint)Options.WorkerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(workItems), "Work item worker id is outside the worker group.");
            }

            seenWorkItemIds[workItem.WorkItemId] = true;
        }
    }

    private bool CanFitCurrentMailboxCapacity(
        IReadOnlyList<RadarProcessingAsyncWorkItem> workItems)
    {
        var perWorker = new int[Options.WorkerCount];
        foreach (var workItem in workItems)
        {
            perWorker[workItem.WorkerId.Value]++;
            if (perWorker[workItem.WorkerId.Value] > Options.QueueCapacity)
            {
                return false;
            }
        }

        return true;
    }
}

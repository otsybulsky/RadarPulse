using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

public sealed class RadarProcessingAsyncDispatchPlan
{
    private readonly IReadOnlyList<RadarProcessingAsyncWorkItem> workItems;

    public RadarProcessingAsyncDispatchPlan(
        RadarProcessingAsyncBatchScope scope,
        RadarProcessingBatchRoute route,
        IReadOnlyCollection<RadarProcessingAsyncWorkItem> workItems)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(workItems);

        if (scope.TopologyVersion != route.TopologyVersion)
        {
            throw new ArgumentException("Dispatch scope topology version must match the route.", nameof(scope));
        }

        Scope = scope;
        Route = route;
        this.workItems = CopyWorkItems(scope, workItems);
    }

    public RadarProcessingAsyncBatchScope Scope { get; }

    public RadarProcessingBatchRoute Route { get; }

    public long BatchSequence => Scope.BatchSequence;

    public RadarProcessingTopologyVersion TopologyVersion => Scope.TopologyVersion;

    public int ExpectedWorkItemCount => Scope.ExpectedWorkItemCount;

    public int PartitionCount => Route.PartitionCount;

    public int ShardCount => Route.ShardCount;

    public int RoutedEventCount => Route.EventCount;

    public IReadOnlyList<RadarProcessingAsyncWorkItem> WorkItems => workItems;

    private static IReadOnlyList<RadarProcessingAsyncWorkItem> CopyWorkItems(
        RadarProcessingAsyncBatchScope scope,
        IReadOnlyCollection<RadarProcessingAsyncWorkItem> workItems)
    {
        if (workItems.Count != scope.ExpectedWorkItemCount)
        {
            throw new ArgumentException("Dispatch work item count must match the batch scope.", nameof(workItems));
        }

        var result = workItems.ToArray();
        var seen = new bool[scope.ExpectedWorkItemCount];
        for (var i = 0; i < result.Length; i++)
        {
            var workItem = result[i];
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

            if (seen[workItem.WorkItemId])
            {
                throw new ArgumentException("Work item ids must be unique within the dispatch plan.", nameof(workItems));
            }

            seen[workItem.WorkItemId] = true;
        }

        return Array.AsReadOnly(result);
    }
}

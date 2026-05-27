using RadarPulse.Domain.Processing;

namespace RadarPulse.Infrastructure.Processing;

/// <summary>
/// Immutable dispatch plan for routing one batch across async shard workers.
/// </summary>
/// <remarks>
/// The plan binds the captured topology route, batch scope, and work item list.
/// Constructor validation ensures work ids are unique and share the same batch
/// sequence and topology version.
/// </remarks>
public sealed class RadarProcessingAsyncDispatchPlan
{
    private readonly IReadOnlyList<RadarProcessingAsyncWorkItem> workItems;

    /// <summary>
    /// Creates a dispatch plan from a scope, route, and complete work item set.
    /// </summary>
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

    /// <summary>
    /// Batch completion scope used by workers.
    /// </summary>
    public RadarProcessingAsyncBatchScope Scope { get; }

    /// <summary>
    /// Route captured from the current topology.
    /// </summary>
    public RadarProcessingBatchRoute Route { get; }

    /// <summary>
    /// Provider batch sequence assigned to this dispatch.
    /// </summary>
    public long BatchSequence => Scope.BatchSequence;

    /// <summary>
    /// Topology version used to build the route and work items.
    /// </summary>
    public RadarProcessingTopologyVersion TopologyVersion => Scope.TopologyVersion;

    /// <summary>
    /// Number of work completions required to finish the batch.
    /// </summary>
    public int ExpectedWorkItemCount => Scope.ExpectedWorkItemCount;

    /// <summary>
    /// Partition count in the captured route.
    /// </summary>
    public int PartitionCount => Route.PartitionCount;

    /// <summary>
    /// Shard count in the captured route.
    /// </summary>
    public int ShardCount => Route.ShardCount;

    /// <summary>
    /// Number of stream events routed by the plan.
    /// </summary>
    public int RoutedEventCount => Route.EventCount;

    /// <summary>
    /// Ordered work items assigned to worker mailboxes.
    /// </summary>
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

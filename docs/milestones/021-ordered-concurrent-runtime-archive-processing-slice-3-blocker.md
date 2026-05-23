# Milestone 021: Slice 3 Blocker

Status: blocked pending architecture decision.

Slice 3 attempted to move from ordered result coordination into actual
multi-batch processing-session concurrency.

The blocker is:

```text
current RadarProcessingCore and RadarProcessingRebalanceSession mutation
semantics cannot safely support overlapping active batches with active batch
capacity greater than 1 without a snapshot, merge, or ordered commit layer
```

## Evidence

`RadarProcessingCore` is a cumulative mutable processing component:

```text
state store:
  RadarSourceProcessingStateStore arrays are mutated as events are processed

batch count:
  processedBatchCount is incremented by each completed batch

result metrics:
  RadarProcessingResult is created from current cumulative metrics
```

`RadarProcessingAsyncCoreSession.ProcessAsync` still mutates the shared core
while a batch is in flight:

```text
workers call RadarProcessingCore.ProcessAsyncShardWorkItem
ProcessAsyncShardWorkItem applies events into the shared core state store
CompleteAsyncBatch increments processedBatchCount and creates the cumulative
  RadarProcessingResult
```

This is safe for the current one-active-batch session because a batch completes
before the next queued batch starts processing. It is not enough for
multi-batch overlap:

```text
if batch 1 and batch 2 mutate the same core at the same time, the
RadarProcessingResult for either batch can include partial state from the
other batch

even if source ids are disjoint, CreateMetrics reads cumulative state while
other active work may still be mutating it, so per-batch result metrics can
become nondeterministic

ordered result buffering can prevent out-of-order publication, but it cannot
undo shared state mutations that already happened
```

`RadarProcessingRebalanceSession` has a stronger topology/commit blocker:

```text
ProcessCompletedResult mutates pressure window, policy state, quarantine
  lifecycle, telemetry, decision id, and potentially topology through
  migration

processing results are validated against current topology version

later-batch rebalance decisions cannot safely be computed or committed before
earlier ordered publication and topology commit are resolved
```

The ordered result coordinator from slice 2 is still useful, but it solves
only deterministic publication. It does not provide state isolation, merge, or
ordered commit.

## Why This Stops Slice 3

Exposing `RadarProcessingQueuedProcessingSession` or
`RadarProcessingQueuedRebalanceSession` with active batch capacity greater
than 1 over the current shared core/session would risk:

```text
nondeterministic cumulative metrics
source-local timestamp ordering violations
hidden mutation from results blocked by terminal failure
topology/rebalance commit becoming visible before ordered publication permits
checksum or processing completeness drift versus the sequential baseline
retained cleanup appearing healthy while processing state is already corrupted
```

The milestone architecture explicitly requires stopping rather than hiding
this risk behind buffering.

## Architecture Choices Needed

Choose one path before implementation continues:

```text
snapshot/merge/commit:
  split processing into isolated per-batch computation followed by ordered
  state merge and ordered rebalance commit

per-batch isolated cores:
  process active batches on isolated cloned state and commit deltas in input
  order; requires clone/delta/merge contracts for source state, handlers,
  metrics, pressure, and topology

non-mutating pre-processing only:
  limit multi-batch concurrency to decode/retention/pre-validation work and
  keep core/rebalance mutation sequential; this would not satisfy the full
  milestone 021 processing-concurrency goal without narrowing scope

source-disjoint limited concurrency:
  allow overlapping processing only when active batches have provably
  disjoint source ids and no shared metric/read race; still needs result
  metric isolation and does not solve rebalance commit ordering
```

Until one of these is selected, slice 3 remains blocked.

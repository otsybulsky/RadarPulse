# Milestone 022: Ordered Rebalance/Topology Commit Architecture Decision

Status: accepted for implementation.

Milestone 021 proved that ordered result buffering alone is not enough when
shared processing state is mutable. It accepted non-mutating per-batch
processing delta compute plus provider-sequence ordered commit.

Milestone 022 extends that decision to rebalance/topology state.

## Decision

Runtime/archive ordered rebalance should use this pipeline:

```text
concurrent compute stage:
  compute handler-free processing deltas for multiple active batches
  do not mutate RadarProcessingCore state
  do not mutate rebalance pressure, policy, quarantine, telemetry, decision,
    or topology state
  retain the batch and worker telemetry needed to retry commit safely

ordered commit stage:
  commit exactly one provider sequence at a time
  validate the active delta topology version against current topology
  recompute the active batch delta when an earlier ordered rebalance commit
    changed topology
  commit the processing delta into RadarProcessingCore
  pass the committed processing result into RadarProcessingRebalanceSession
  let RadarProcessingRebalanceSession own pressure, policy, quarantine,
    telemetry, decision, migration, validation, and topology mutation
  record the rebalance result in provider sequence
```

This means milestone 022 does not make rebalance planning itself concurrent.
It overlaps the expensive batch processing delta work, then serializes the
stateful rebalance/topology commit boundary.

## Why This Direction

This is preferred over the alternatives:

```text
concurrent rebalance planning and later merge:
  too risky without snapshot/merge contracts for pressure windows, policy
  budgets, quarantine lifecycle, telemetry retention, decision ids, accepted
  moves, and topology migration

failing every stale topology delta:
  safe but too fragile; any accepted move by an earlier batch would turn
  later active work into a fault even though the original batch can be
  reprocessed against the new topology

full rebalance session cloning:
  too broad; it requires cloning and merging pressure, policy, quarantine,
  telemetry, topology manager state, processing core state, and handler state

silently using stale route telemetry:
  unsafe; pressure samples and topology validation would no longer describe
  the topology that actually committed
```

The chosen approach preserves deterministic shared-state mutation while still
allowing active-batch overlap on the processing work that can be represented
as non-mutating deltas.

## Required Invariants

Implementation must preserve:

```text
provider sequence is the only publication order
processing commit happens before rebalance commit for the same sequence
rebalance commit happens before the next sequence can publish
topology changes from earlier sequences are visible to later commits
stale topology deltas are recomputed, not silently committed
accepted move counters and recent accepted move evidence remain visible
queued-owned failures fail closed
no automatic borrowed fallback is introduced
retained resources release after success, failure, cancellation, drain, and
  cleanup
startup retained payload prewarm remains separately visible
```

## Performance Constraints

Implementation should preserve the existing performance posture:

```text
reuse RadarProcessingBatchDelta and async delta compute
avoid per-event heap allocation in hot paths
avoid LINQ in hot processing loops
keep active batch capacity separate from provider and worker queue capacity
keep recompute-on-stale-topology as a correctness path, not an always-on
  duplicate compute path
collect processing-bottleneck evidence before broader default promotion
```

## Unsupported State

The ordered active rebalance path inherits milestone 021's handler boundary:

```text
custom handler cores remain unsupported because handler state has no
non-mutating delta/merge contract
```

Durable and cross-process runtime boundaries remain future work:

```text
durable queues
brokers
cross-process providers/workers
true live network ingestion
production operator/deployment/rollback surfaces
product-facing radar workflows
```

## Implementation Direction

The implementation should add the minimum explicit path:

```text
RadarProcessingQueuedRebalanceSession.DrainOrderedConcurrentAsync
RadarProcessingArchiveQueuedOverlapRunner ordered rebalance entry point or
  ordered option
focused tests for ordered result order, accepted move parity, stale topology
  recompute, failure skip, cancellation cleanup, and retained pressure
processing-bottleneck benchmark evidence after correctness gates pass
```

Existing sequential rebalance APIs must remain unchanged unless a caller opts
into ordered active rebalance.

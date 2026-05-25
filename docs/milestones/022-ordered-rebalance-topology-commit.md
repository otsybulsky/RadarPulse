# Milestone 022: Ordered Rebalance/Topology Commit Architecture

Status: active concept.

RadarPulse milestone 022 starts from the closed milestone 021 ordered
runtime/archive processing foundation.

Milestone 021 closed with this answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
processing-core path is ready to keep multiple accepted batches active,
compute them concurrently, and publish externally visible processing results
in deterministic provider sequence order over the accepted milestone 020
baseline
```

Milestone 022 should not reopen the milestone 020 provider/execution default
decision or the milestone 021 processing-delta decision. It extends the same
ordered active-batch model to the rebalance/topology boundary.

The important shift is:

```text
from:
  ordered active batches can compute processing deltas concurrently, but
  rebalance pressure, policy, quarantine, telemetry, decision, and topology
  mutation still run only through the older sequential rebalance drain

to:
  the runtime/archive rebalance path can keep multiple accepted batches active
  for processing-delta compute while committing processing, pressure,
  rebalance decisions, and topology mutation strictly in provider sequence
```

This document records the milestone 022 concept, architecture boundary,
ordered rebalance/topology commit model, safety invariants, performance
evidence target, and expected gate posture. It is intentionally not the
detailed task plan.

## Milestone Goal

Milestone 022 should implement ordered rebalance/topology commit over the
accepted runtime/archive baseline and collect processing-bottleneck evidence
before any broader default-promotion decision.

The inherited runtime/archive contour remains:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
execution: async shard transport
worker count: 4
worker queue capacity: 8
ordered active batch capacity: 4
```

The milestone 022 concurrency target is:

```text
runtime/archive ordered rebalance surface:
  accepts provider batches in input order
  bounds active batches separately from provider queue and worker queue
  computes handler-free processing deltas concurrently
  commits processing deltas strictly by provider sequence
  evaluates rebalance pressure, policy, quarantine, telemetry, decisions, and
    topology mutation strictly after ordered processing commit
  preserves deterministic result order, accepted-move evidence, topology
    version safety, failure cleanup, and retained-pressure cleanup
```

## Architecture Boundary

Safe in milestone 022:

```text
add an explicit ordered-concurrent rebalance drain path for queued rebalance
  sessions
add a runtime/archive RunRebalanceAsync overload or option that can opt into
  ordered active-batch rebalance
reuse RadarProcessingOrderedConcurrencyOptions for active rebalance capacity
reuse non-mutating RadarProcessingBatchDelta compute for active batch work
commit processing plus rebalance/topology mutation in provider sequence
validate active batch topology version at ordered commit
recompute stale active processing deltas before commit when an earlier
  ordered rebalance migration changed topology
preserve existing sequential DrainAsync and RunRebalanceAsync behavior
add focused success, stale-topology, failure, cancellation, cleanup, and
  runtime/archive integration tests
add processing-bottleneck synthetic or representative matrix evidence
```

Not safe in milestone 022 unless explicitly reprioritized:

```text
running rebalance policy or topology mutation concurrently
publishing accepted moves from later batches before earlier batches commit
discarding accepted-move evidence from overlapping batches
using a processing delta computed against an old topology after topology has
  changed
adding automatic silent borrowed fallback after queued-owned failure
hiding startup retained payload prewarm inside steady measured allocation
claiming durable queue, broker, cross-process, production runtime, or
  product-facing readiness
```

Out of scope for this milestone:

```text
durable queues or brokers
cross-process providers/workers
production deployment, rollback, and operator surfaces
true live network ingestion
product-facing radar workflows
automatic silent borrowed fallback
full handler-state delta/merge support unless a narrow blocker forces the
  decision
```

## Ordered Rebalance Commit Model

Milestone 022 keeps the milestone 021 two-stage shape and extends the ordered
commit stage:

```text
concurrent compute stage:
  validate immutable batch shape
  route the batch against the current topology snapshot
  compute handler-free per-batch processing deltas without mutating shared
    RadarProcessingCore state
  preserve async shard worker telemetry where async execution is used
  retain the original batch so stale topology can be recomputed if required

ordered commit stage:
  publish only the next provider sequence
  validate that the computed delta topology version still matches the current
    topology
  if topology changed because an earlier ordered commit migrated a partition,
    discard the stale delta and recompute the processing delta against the
    current topology before commit
  commit the processing delta into RadarProcessingCore
  evaluate pressure, policy, quarantine, telemetry, rebalance decision, and
    topology migration from the committed processing result
  record the rebalance session result in provider sequence
```

The recompute-on-stale-topology rule is important. It avoids both unsafe
publication of stale route telemetry and unnecessary milestone-level blocking
when a previous ordered batch legitimately changed topology.

## Deterministic Ordering Contract

The ordered rebalance path must preserve these externally visible contracts:

```text
processing results publish by provider sequence
rebalance results publish by provider sequence
accepted move counters and recent accepted move evidence are not lost
final topology version is the topology after the last ordered committed
  successful rebalance result
later active successes do not publish past an earlier failure boundary
validation and migration failures remain visible failures
no automatic borrowed fallback is used
```

## Topology Safety

Topology safety is the main new milestone risk.

The safe posture is:

```text
active compute may route against a topology snapshot
ordered commit must never use stale topology silently
an earlier accepted move may invalidate later active deltas
stale later deltas are recomputed before ordered commit
topology migration remains owned by RadarProcessingRebalanceSession
topology version validation remains part of session result validation
```

If stale-delta recompute proves insufficient for a concrete rebalance state
surface, the milestone should stop and record a blocker rather than weaken
the topology-version invariant.

## Handler State

Milestone 021 explicitly rejected custom handler cores for ordered processing
deltas. Milestone 022 inherits that boundary.

Expected posture:

```text
handler-free cores are supported
handler cores remain rejected by ordered active rebalance until handler state
  has an explicit delta/merge contract
the existing sequential rebalance path remains available for handler-owned
  behavior
```

## Failure, Cancellation, Release, And Cleanup

The ordered rebalance path must remain fail-closed.

Required invariants:

```text
queued-owned failure does not trigger automatic borrowed fallback
processing validation failure remains visible
rebalance validation failure remains visible
migration failure remains visible and faults the ordered drain
later active successes are skipped after an earlier failure boundary
cancellation stops publish/commit and returns terminal retained pressure to
  zero
retained release failures remain readiness blockers
startup retained payload prewarm remains visible as lifecycle cost
```

## Performance Evidence Target

Milestone 021 captured clean full-cache ordered-processing evidence, but that
cache shape was archive-producer dominated. Milestone 022 should add a
processing-bottleneck matrix.

Useful evidence:

```text
ordered active rebalance active=4 versus active=1 on a deterministic
  processing-bottleneck synthetic or representative archive-shaped workload
accepted move parity and final topology parity
processing checksum parity where applicable
worker failed batches/items 0/0
release failures 0
terminal combined retained pressure 0
stale topology recompute count visible in focused tests or reporting if it
  becomes operationally meaningful
repeated variance evidence if the path is considered for broader default
  promotion
```

## Expected Gate Question

The expected milestone 022 gate question is:

```text
Is the scoped in-process runtime/archive rebalance path ready to keep multiple
accepted batches active for processing-delta compute while committing
processing, rebalance decisions, and topology mutation deterministically in
provider sequence?
```

Expected decision posture if the milestone succeeds:

```text
accepted with scoped warnings for ordered rebalance/topology commit over the
scoped in-process runtime/archive queued-overlap path
```

Expected warnings to carry:

```text
handler-state delta/merge is still not implemented
durable queues and cross-process runtime are still not implemented
true live network ingestion is still not implemented
production operator/deployment/rollback surfaces are still not implemented
broader default promotion still depends on workload breadth and variance
evidence
```

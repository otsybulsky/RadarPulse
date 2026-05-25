# Milestone 022: Ordered Rebalance/Topology Commit Implementation Plan

Status: active plan.

This plan implements the milestone 022 architecture defined in
`022-ordered-rebalance-topology-commit.md` and the accepted implementation
direction in
`022-ordered-rebalance-topology-commit-architecture-decision.md`.

The plan is scoped to the in-process runtime/archive queued-overlap path. It
extends milestone 021 ordered active-batch processing into ordered
rebalance/topology commit without reopening the accepted queued-owned provider
baseline, async shard transport baseline, or non-mutating processing delta
decision.

## Goal

Implement ordered active-batch rebalance over the accepted default baseline.

The milestone must prove these concrete outcomes:

```text
there is an explicit ordered rebalance drain path
active rebalance batch capacity uses RadarProcessingOrderedConcurrencyOptions
active rebalance capacity remains independent from provider queue capacity
  and worker queue capacity
multiple accepted rebalance batches can compute processing deltas concurrently
processing, pressure, policy, quarantine, telemetry, decision, and topology
  mutation commit strictly by provider sequence
topology changes from earlier accepted moves are visible before later commits
stale active deltas are recomputed before commit rather than silently used
accepted move evidence and final topology match the sequential reference
failure, cancellation, release, and retained pressure cleanup remain clean
startup retained payload prewarm cost remains visible and separate
no automatic borrowed fallback is introduced
```

Scope note:

```text
milestone 022 does not make rebalance planning itself concurrent. It overlaps
handler-free processing delta compute, then commits processing plus
rebalance/topology state in provider sequence.
```

## Slice 1: Ordered Rebalance Commit Contract

Status: pending.

Implementation:

```text
add an internal RadarProcessingRebalanceSession ordered-delta commit helper
  that accepts a RadarProcessingBatchDelta plus optional worker telemetry
commit the processing delta through RadarProcessingCore.CommitProcessingDelta
then run existing ProcessCompletedResult against the committed
  RadarProcessingResult
preserve RadarProcessingRebalanceSession as the sole owner of pressure,
  policy, quarantine, telemetry, decision, migration, validation, and
  topology mutation
```

Tests:

```text
committing a processing delta through rebalance produces the same accepted
  move and final topology as direct Process
invalid ordered processing delta fails visibly before rebalance mutation
handler cores remain rejected by the existing delta contract
```

Exit criteria:

```text
ordered rebalance commit has a narrow domain-level helper before queued
session concurrency is added
```

## Slice 2: Queued Rebalance Ordered Concurrent Drain

Status: pending.

Implementation:

```text
add RadarProcessingQueuedRebalanceSession.DrainOrderedConcurrentAsync
reuse RadarProcessingOrderedConcurrencyOptions for active batch capacity
preserve existing DrainAsync sequential behavior
compute active handler-free processing deltas concurrently
use async delta compute through RadarProcessingAsyncCoreSession when the
  rebalance core uses AsyncShardTransport
commit completions only in provider sequence
mark later unpublished active successes as skipped after an earlier failure
retain consumer resource leases until completion is committed, skipped, or
  disposed
```

Tests:

```text
ordered rebalance results record by provider sequence
accepted move counts and final topology match a sequential reference
async shard transport preserves worker telemetry through ordered rebalance
validation failure fails closed and skips later active work
cancellation before dequeue returns canceled and leaves retained pressure zero
```

Exit criteria:

```text
queued rebalance can overlap processing-delta compute while preserving
ordered rebalance/topology mutation
```

## Slice 3: Stale Topology Recompute

Status: pending.

Implementation:

```text
detect when an active processing delta was computed against a topology version
  older than the current rebalance session topology
discard the stale delta before commit
recompute the batch delta against the current topology
commit the recomputed delta and then run rebalance commit
preserve worker telemetry for non-stale async active deltas; if stale async
  recompute happens during ordered commit, recompute through async delta
  compute before commit
```

Tests:

```text
an accepted move from an earlier ordered batch updates the topology seen by a
  later active batch before commit
later active batch commits successfully after stale topology recompute
the later processing result telemetry topology version matches the current
  topology at commit
final topology and accepted move evidence match sequential reference
```

Exit criteria:

```text
topology-version safety is explicit and stale active work is corrected rather
than silently committed or unnecessarily faulted
```

## Slice 4: Runtime/Archive Integration

Status: pending.

Implementation:

```text
add an explicit ordered rebalance runtime/archive entry point or option on
  RadarProcessingArchiveQueuedOverlapRunner
compose it with the accepted queued-overlap provider default
scale startup retained payload prewarm to active capacity like ordered
  processing
preserve the old RunRebalanceAsync sequential consumer unless ordered
  rebalance is explicitly selected
```

Tests:

```text
deterministic live-adapter-shaped ordered rebalance completes through the
  default runtime/archive baseline
ordered rebalance uses startup retained payload prewarm sized to active
  capacity
validation failure cleans retained pressure without borrowed fallback
final queue telemetry reports completed, failed, canceled, and skipped counts
  correctly
```

Exit criteria:

```text
runtime/archive callers have an explicit ordered rebalance surface with
provider, execution, active capacity, prewarm, telemetry, and cleanup
contracts visible
```

## Slice 5: Processing-Bottleneck Evidence

Status: pending.

Implementation:

```text
add or extend a benchmark path that exercises ordered active rebalance on a
  deterministic processing-bottleneck workload
compare active=4 against active=1 on the same path
report accepted moves, final topology version, worker telemetry, release
health, retained pressure, and allocation/elapsed ratios
capture focused matrix output in a milestone 022 performance document
```

Tests:

```text
CLI options parse the ordered rebalance benchmark settings
benchmark result exposes ordered active capacity and rebalance/topology
  correctness counters
focused benchmark tests verify output shape and provenance
```

Exit criteria:

```text
the milestone has evidence where processing/rebalance is the meaningful
bottleneck before any broader promotion discussion
```

## Slice 6: Gate Documentation

Status: pending.

Implementation:

```text
write milestone 022 gate evidence after implementation and verification
record focused test commands and results
record full Release test result and any known caveats
record performance matrix result
update handoff and project-progress after gate capture
stop before decision trace for review and discussion
```

Exit criteria:

```text
decision trace is not written until the milestone result and warnings are
reviewed
```

## Verification Targets

Expected verification before decision-trace discussion:

```text
Release build succeeds with 0 warnings and 0 errors
focused ordered rebalance test suite passes
runtime/archive ordered rebalance integration tests pass
focused CLI/benchmark tests pass
full Release test project is run, with any known allocation-sensitive caveat
  explicitly recorded
processing-bottleneck matrix is captured or a concrete blocker is recorded
```

## Stop Conditions

Stop before implementation only for a blocker that changes the architecture
decision. Examples:

```text
stale topology recompute cannot preserve accepted move or validation parity
rebalance state requires a real snapshot/merge contract before safe ordered
  commit
handler-state delta/merge becomes unavoidable for the scoped path
retained resource cleanup cannot be made deterministic with active rebalance
```

Do not stop for routine file creation, test failures with a clear fix, or
mechanical plan/document updates.

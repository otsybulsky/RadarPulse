# Milestone 022: Ordered Rebalance/Topology Commit Gate

Status: gate captured; decision trace intentionally not written.

This document records milestone 022 gate evidence for review before the
decision trace discussion.

## Gate Question

```text
Is the scoped in-process runtime/archive rebalance path ready to keep multiple
accepted batches active for processing-delta compute while committing
processing, rebalance decisions, and topology mutation deterministically in
provider sequence?
```

## Implemented Scope

Milestone 022 implemented:

```text
RadarProcessingRebalanceSession.CommitProcessingDelta:
  commits a non-mutating processing delta into RadarProcessingCore
  then runs the existing rebalance completion pipeline
  keeps RadarProcessingRebalanceSession as owner of pressure, policy,
    quarantine, telemetry, decision, migration, validation, and topology
    mutation

RadarProcessingQueuedRebalanceSession.DrainOrderedConcurrentAsync:
  opt-in ordered active-batch drain for queued rebalance sessions
  reuses RadarProcessingOrderedConcurrencyOptions
  computes handler-free processing deltas concurrently
  commits processing plus rebalance/topology mutation in provider sequence
  preserves async shard transport worker telemetry
  skips later active successes after an earlier failure boundary

stale topology recompute:
  detects active deltas computed against an older topology version
  discards stale deltas and recomputes them against current topology before
    ordered commit
  avoids silently committing stale route telemetry after an earlier migration

runtime/archive integration:
  added RadarProcessingArchiveQueuedOverlapRunner.RunOrderedRebalanceAsync
  composes ordered rebalance with queued-overlap provider defaults
  scales startup retained payload prewarm to active batch capacity
  preserves old RunRebalanceAsync sequential consumer behavior

processing-bottleneck evidence:
  extended rebalance-synthetic with ordered-rebalance mode
  added --active-batches / --ordered-active-batches
  captured active=4 versus active=1 Release CLI matrix on a prebuilt
    synthetic processing/rebalance workload

worker mailbox hardening:
  fixed pending-count reservation race exposed by concurrent async delta
    dispatch
```

Milestone 022 did not implement:

```text
handler-state delta/merge
durable queues or brokers
cross-process providers/workers
true live network ingestion
production deployment/operator/rollback surfaces
product-facing radar workflows
automatic silent borrowed fallback
decision trace or closeout
```

## Gate Results

### Ordered Rebalance Commit Contract

Result:

```text
passed
```

Evidence:

```text
ordered delta commit preserves accepted move and final topology parity against
direct Process

ordered delta commit validation failure remains a visible processing failure
and does not evaluate rebalance
```

### Queued Ordered Rebalance Drain

Result:

```text
passed
```

Evidence:

```text
ordered concurrent rebalance records results by provider sequence
accepted move and no-action counts match sequential references
async shard transport worker telemetry remains visible
validation failure fails closed and skips later active work
cancellation before dequeue returns canceled with terminal retained pressure
zero
```

### Stale Topology Recompute

Result:

```text
passed
```

Evidence:

```text
when an earlier ordered batch publishes an accepted move, a later active
delta computed against the old topology is recomputed before commit

the later processing result telemetry topology version matches the updated
topology

final topology and accepted move evidence match the sequential reference
```

### Runtime/Archive Integration

Result:

```text
passed
```

Evidence:

```text
RunOrderedRebalanceAsync completes through the default runtime/archive
baseline

startup retained payload prewarm is sized to ordered active batch capacity

validation failure cleans retained pressure without borrowed fallback

terminal combined retained pressure returns to zero
```

### Processing-Bottleneck Evidence

Result:

```text
passed with performance warning
```

Evidence:

```text
docs/milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md

active=4 elapsed ratio versus active=1:
  0.891x

active=4 allocation ratio versus active=1:
  1.137x

validation checksum:
  matched

accepted moves:
  matched at 2_000 vs 2_000

failed migrations:
  0

worker failed batches/items:
  0/0
```

Interpretation:

```text
active-batch overlap improves elapsed time on the captured
processing/rebalance-shaped workload, but topology churn causes stale delta
recompute, increasing worker dispatches and allocation
```

### Full-Cache Regression Matrix

Result:

```text
passed
```

Evidence:

```text
docs/milestones/022-ordered-rebalance-topology-commit-full-cache-performance-matrix.md

rebalance-archive default elapsed ratios versus explicit BlockingBorrowed:
  0.883x static
  0.891x sampling
  0.871x rebalance-session

rebalance-archive default allocation ratios versus explicit BlockingBorrowed:
  1.002x static
  1.001x sampling
  1.002x rebalance-session

ordered-archive-processing active=4 elapsed ratio versus active=1:
  0.999x

ordered-archive-processing active=4 steady allocation ratio versus active=1:
  1.007x

validation and processing completeness:
  succeeded

worker failed batches/items:
  0/0

retained payload pool misses:
  0

release failures:
  0

terminal combined retained pressure:
  0
```

Interpretation:

```text
no full-cache performance regression was observed after milestone 022 ordered
rebalance/topology commit work

the full-cache workload remains archive-producer dominated, so the synthetic
processing-bottleneck matrix remains the stronger evidence for ordered
rebalance compute overlap
```

## Verification

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Focused milestone 022 Release gate suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "(FullyQualifiedName~RadarProcessingRebalanceSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests)&FullyQualifiedName!~AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  76 passed, 0 failed, 0 skipped
```

Known allocation-sensitive synthetic test isolated rerun:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  1 passed, 0 failed, 0 skipped
```

Full Release test project:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build

result:
  821 passed, 1 failed, 3 skipped
```

Known full-suite failure:

```text
RadarProcessingSyntheticRebalanceBenchmarkTests.
  AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

full-suite failure:
  Expected bounded benchmark aggregation allocation, got 382811064 bytes.
```

This is the same allocation-sensitive synthetic benchmark caveat carried from
earlier milestones. It passed in isolated rerun and is outside the ordered
rebalance/topology correctness surface.

## Gate Interpretation Input

Recommended decision-trace input for discussion:

```text
accepted with scoped warnings for ordered rebalance/topology commit over the
scoped in-process runtime/archive queued-overlap path
```

Reasons:

```text
ordered active rebalance now overlaps handler-free processing-delta compute
while preserving provider-sequence processing commit, rebalance commit,
topology mutation, accepted move evidence, stale topology safety, async
worker telemetry, fail-closed behavior, visible startup prewarm, release
health, and terminal retained pressure cleanup
```

Warnings to carry:

```text
handler-state delta/merge is not implemented
topology churn can increase stale-delta recompute, worker dispatches, and
allocation under active-batch overlap
durable queues, brokers, and cross-process workers are not implemented
true live network ingestion is not implemented
production operator/deployment/rollback surfaces are not implemented
broader default promotion still needs workload breadth and variance evidence
full-suite allocation sensitivity remains for one synthetic benchmark test
```

## Decision Trace

Do not write the decision trace yet. Stop here for review and discussion.

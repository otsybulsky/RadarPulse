# Milestone 018: Runtime Pressure And Failure Gate

Status: complete.

This document records the slice 7 runtime pressure, backpressure,
cancellation, failure, drain, and cleanup gate. The gate uses synthetic leased
batches through the runtime queue, queued session, archive overlap, and
archive queued provider surfaces so that pressure and failure states can be
injected deterministically.

The gate does not prove true live network ingestion, durable broker behavior,
cross-process worker behavior, or broad failure-mode exhaustiveness.

## Runner

Temporary runner:

```text
data\temp\m018-runtime-pressure-gate-runner
```

Committed production code changes:

```text
none
```

Raw output:

```text
data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.jsonl
data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.md
```

Runtime posture:

```text
queued-owned: explicit runtime gate candidate
runtime default: undecided
input shape: synthetic leased batches through runtime queue/session/overlap
  surfaces
production code changes: none
```

## Summary

```text
rows: 11
pass: 11
fail: 0
operator-visible rows: 11
fail-closed rows: 10
terminal pressure clean rows: 11
release-failure visible rows: 1
backpressure rows: 3
cancellation rows: 4
failure rows: 6
max queue depth high-watermark: 3
max combined retained bytes high-watermark: 6
```

The normal drain row is the only row not counted as fail-closed, because it is
a successful drain/cleanup control rather than a failure outcome.

## Gate Rows

| Id | Group | Scenario | Status | Runtime | Key result |
|---|---|---|---:|---|---|
| D1 | queue-pressure | return-full queue capacity rejection | pass | ProducerFailed | 2 attempts, 1 accepted, 1 full rejection, terminal cleanup clean |
| D2 | retained-byte-pressure | retained-byte budget rejection | pass | ProducerFailed | retained-byte full rejection visible, high-watermark 2 <= budget 3, terminal cleanup clean |
| D3 | queue-pressure | wait-on-full queue timeout | pass | ProducerFailed | timeout rejection visible, enqueue wait attributed, terminal cleanup clean |
| E1 | cancellation | enqueue cancellation before start and while waiting | pass | Canceled | 3 attempts, 1 accepted, 2 canceled enqueue attempts, terminal cleanup clean |
| E2 | cancellation | cancel-queued shutdown cancels accepted pending work | pass | Canceled | 2 accepted pending batches recorded as canceled before dequeue |
| E3 | cancellation | archive overlap cancellation after accepted enqueue | pass | Canceled | accepted pending resource released on overlap cancellation |
| E4 | cancellation | active consumer cancellation releases active resource | pass | Canceled | active retained resource reached active high-watermark 2 and released cleanly |
| G1 | drain-cleanup | normal drain completes queued pending work | pass | Completed | 3 accepted, 3 completed, queue high-watermark 3, release attempts 3/3 |
| F1 | failure | processing validation failure faults without fallback | pass | ConsumerFaulted | failed batch count 1, completed count 0, release clean |
| F2 | failure | retained release failure remains visible and readiness-blocking | pass | Completed | release failure count 1, readiness error RetainedResourceReleaseFailed |
| F3 | failure | producer failure releases pending retained resources | pass | ProducerFailed | accepted pending resource released during producer-failure cleanup |

## Terminal Cleanup

All rows returned current retained pressure to zero:

```text
current pending retained batches: 0
current pending retained bytes: 0
current active retained batches: 0
current active retained bytes: 0
current combined retained batches: 0
current combined retained bytes: 0
```

Release outcomes:

```text
D1 release attempts/released/failed: 2/2/0
D2 release attempts/released/failed: 2/2/0
D3 release attempts/released/failed: 2/2/0
E3 release attempts/released/failed: 1/1/0
E4 release attempts/released/failed: 1/1/0
G1 release attempts/released/failed: 3/3/0
F1 release attempts/released/failed: 1/1/0
F2 release attempts/released/failed: 1/0/1
F3 release attempts/released/failed: 1/1/0
```

F2 intentionally injects a release failure. It still returns active and
combined pressure to zero, and the release failure remains visible through
retention telemetry and the readiness evaluator.

## Interpretation

Backpressure behavior:

```text
queue full:
  visible full rejection, no silent drop

retained-byte pressure:
  visible full rejection when pending retained bytes would exceed budget

wait-on-full timeout:
  visible timeout rejection with enqueue wait attribution
```

Cancellation behavior:

```text
before enqueue:
  cancellation is visible and no partial work is accepted

while waiting for capacity:
  cancellation is visible and the existing accepted item remains owned until
  explicit cleanup

after accepted enqueue:
  overlap cancellation releases pending retained resources

while active:
  active consumer cancellation releases active retained resources and records
  the batch as canceled

cancel-queued shutdown:
  accepted pending work is recorded as canceled before dequeue
```

Failure behavior:

```text
processing validation failure:
  faults the consumer, records failed batch count, and does not use borrowed
  fallback

retained release failure:
  remains operator-visible and readiness-blocking even when processing itself
  completed

producer failure:
  releases pending retained resources and leaves terminal retained pressure at
  zero
```

No row revealed a cleanup, release attribution, pressure, cancellation,
backpressure, or fail-closed blocker for the scoped in-process queued-owned
runtime surfaces.

## Verification

Focused regression suite:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests"
```

Result:

```text
passed: 56
failed: 0
skipped: 0
```

## Coverage Limits

This gate does not close:

```text
true live ingestion coverage
durable queue or broker coverage
cross-process provider/worker coverage
repeated variance evidence
all possible worker failure shapes
runtime default migration
operator-facing production reporting beyond existing lower-level contracts
```

The release-failure row is an injected failure proving visibility and cleanup
shape. It is not a successful readiness row; it confirms that such a failure
would remain a readiness blocker instead of being hidden.

## Outcome

Slice 7 is complete.

The scoped in-process queued-owned runtime surfaces pass deterministic
pressure, cancellation, failure, drain, release, and cleanup gate evidence.
Proceed to slice 8 gate interpretation and follow-up fix triage. Runtime/live
default posture remains undecided until the decision trace.

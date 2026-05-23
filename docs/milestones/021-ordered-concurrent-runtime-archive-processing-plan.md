# Milestone 021: Ordered Concurrent Runtime/Archive Processing Implementation Plan

Status: active after architecture decision.

This plan implements the milestone 021 architecture defined in
`021-ordered-concurrent-runtime-archive-processing.md`.

The plan is scoped to the in-process runtime/archive queued-overlap path. It
starts from the accepted milestone 020 runtime/archive baseline and adds
ordered concurrent active-batch processing only where deterministic ordering,
topology safety, fail-closed behavior, and retained-pressure cleanup can be
proved. It does not implement durable queues, brokers, cross-process workers,
production operator surfaces, or product-facing workflows.

## Goal

Implement ordered concurrent runtime/archive processing over the accepted
default baseline.

The milestone must prove these concrete outcomes:

```text
there is an explicit ordered-concurrency contract with active batch capacity
active batch capacity is independent from provider queue capacity and async
  worker queue capacity
multiple accepted batches can be active at once when the surface is safe
processing completion may occur out of order
published processing results remain in deterministic provider sequence order
processing completeness, counters, and checksums match the sequential
  baseline for deterministic input
queued-owned failures remain fail-closed with no silent borrowed fallback
retained pressure returns to zero across success, failure, cancellation, and
  cleanup paths
startup retained payload prewarm cost remains visible and separate
provider and execution provenance remain separately assertable
```

Scope note:

```text
milestone 021 does not silently make every caller-owned processing core or
rebalance session concurrent. Ordered concurrency is introduced through an
explicit runtime/archive path and must stop if mutable core, topology, or
rebalance commit semantics cannot be made deterministic with the current
architecture.
```

The slice 3 shared-mutation blocker is resolved by the architecture decision
recorded in
`021-ordered-concurrent-runtime-archive-processing-architecture-decision.md`.
The implementation direction is a per-batch non-mutating processing delta
pipeline with ordered commit, not full `RadarProcessingCore` cloning.

## Slice 1: Ordered Concurrency Contract

Status: complete.

Implementation:

```text
add a small ordered-concurrency options contract for active batch capacity
add validation for capacity, drain, and cancellation behavior
add baseline helpers from RadarProcessingRuntimeArchiveBaseline if needed so
  omitted runtime/archive construction can expose the ordered capacity without
  rewriting provider or worker queue defaults
keep provider queue capacity 8 and worker queue capacity 8 unchanged
```

Tests:

```text
default ordered-concurrency capacity is valid and independent from provider
  queue capacity and worker queue capacity
invalid active batch capacity is rejected
baseline provider and execution defaults still match milestone 020
```

Exit criteria:

```text
ordered concurrency is explicit and bounded before any runtime/archive drain
logic changes
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests"

result:
  11 passed, 0 failed, 0 skipped
```

## Slice 2: Ordered Result Coordinator

Status: complete.

Implementation:

```text
add an internal ordered result coordinator for queued batch processing results
allow completion records to arrive out of provider sequence order
publish contiguous completed results in provider sequence order
surface fault/cancel state without publishing later successful results ahead
  of an earlier failed sequence
```

Tests:

```text
out-of-order successful completions publish in sequence order
later completion waits behind an incomplete earlier sequence
earlier failure prevents hidden success publication after the failure boundary
canceled/skipped records preserve deterministic ordering
```

Exit criteria:

```text
deterministic result publication is proven independently from processing core
or rebalance mutation
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests"

result:
  5 passed, 0 failed, 0 skipped
```

## Slice 3: Processing Session Ordered Concurrency

Status: active after architecture decision.

Implementation:

```text
add an opt-in ordered-concurrent drain path for RadarProcessingQueuedProcessingSession
bound active dequeued batches by ordered-concurrency options
use the ordered result coordinator for published processing results
preserve consumer resource leases until each active batch is completed or
  canceled
preserve existing sequential DrainAsync behavior
```

Safety checkpoint:

```text
before enabling active capacity greater than 1, prove that the processing
core path is safe for overlapping batches. If the current mutable
RadarProcessingCore state store cannot support deterministic overlapping
batch mutation, stop and record the blocker instead of exposing unsafe
concurrency.
```

Tests:

```text
opt-in ordered-concurrent drain preserves provider sequence result order
active capacity limits concurrent active work
sequential DrainAsync behavior remains unchanged
validation failure fails closed and skips/cancels later unpublished work
cancellation releases retained resources and returns terminal pressure to zero
```

Exit criteria:

```text
processing-session ordered concurrency is either implemented with focused
proof, or a concrete core-state blocker is recorded before any unsafe default
is exposed
```

Resolved blocker:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing-slice-3-blocker.md
docs/milestones/021-ordered-concurrent-runtime-archive-processing-architecture-decision.md
```

Summary:

```text
the current shared RadarProcessingCore mutates cumulative state while a batch
is processing, and RadarProcessingRebalanceSession mutates pressure, policy,
telemetry, decision, and topology state during completion. Ordered result
buffering can preserve publication order, but it cannot isolate or undo
shared state mutations from overlapping active batches.
```

Resolution:

```text
implement non-mutating per-batch processing deltas and commit shared
RadarProcessingCore state strictly by provider sequence. The initial
implementation may reject custom handler cores until a handler-delta contract
exists.
```

Progress:

```text
processing delta core foundation is implemented for handler-free cores:
  RadarProcessingBatchDelta uses pooled dense source-indexed arrays
  RadarProcessingCore.ComputeProcessingDelta computes without mutating shared
    state
  RadarProcessingCore.CommitProcessingDelta validates ordered source-local
    timestamps, mutates shared state, and creates cumulative results
  handler cores are explicitly rejected until a handler-delta contract exists

processing session ordered concurrent drain is implemented for handler-free
  processing cores:
  RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync bounds
    active batch compute by RadarProcessingOrderedConcurrencyOptions
  active batch compute creates deltas without shared core mutation
  ordered commit publishes processing results by provider sequence
  later active successes are skipped after an earlier failure boundary
  existing sequential DrainAsync behavior remains separate

async ordered delta support:
  RadarProcessingAsyncWorkerGroup keeps the existing one-in-flight guard for
    mutating ProcessAsync dispatch by default
  non-mutating delta compute can opt into concurrent worker-group dispatch
  RadarProcessingAsyncCoreSession.ComputeDeltaAsync uses worker telemetry for
    async shard transport delta compute without mutating shared core state
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingBatchDeltaTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingBatchDeltaTests"

result:
  16 passed, 0 failed, 0 skipped

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests"

result:
  21 passed, 0 failed, 0 skipped
```

## Slice 4: Runtime/Archive Integration

Status: planned.

Implementation:

```text
wire the safe ordered-concurrent drain path into the in-process
runtime/archive queued-overlap runner or an explicit owned-construction
runtime/archive entry point
preserve the milestone 020 provider and execution defaults when options are
  omitted
keep caller-supplied sessions explicit
keep startup prewarm visible in result output
```

Safety checkpoint:

```text
if rebalance/topology commit semantics require serial publication or a
snapshot/commit layer that does not exist yet, keep rebalance ordered
concurrency disabled and stop for design review rather than letting later
batch topology become externally visible early.
```

Tests:

```text
runtime/archive ordered-concurrent path uses queued-owned pooled-copy startup
  prewarm and async shard transport worker sizing
multiple deterministic archive-shaped batches are accepted and published in
  provider sequence order
processing completeness passes
no silent borrowed fallback occurs
terminal retained pressure returns to zero
```

Exit criteria:

```text
the scoped runtime/archive surface either consumes the ordered-concurrent
path safely, or the milestone stops with a concrete topology/rebalance
blocker
```

## Slice 5: Failure, Cancellation, And Cleanup Hardening

Status: planned.

Implementation:

```text
exercise success, validation failure, processing failure, cancellation, and
drain paths with concurrent active work where supported
ensure retained leases release for active and queued batches
ensure queued-owned failure remains fail-closed
ensure terminal queue/provider retained pressure returns to zero
```

Tests:

```text
validation failure is visible and does not fall back to BlockingBorrowed
cancellation cancels active and queued work deterministically
retained release failures remain visible readiness blockers
terminal combined retained pressure is zero after cleanup
worker failed/canceled/rejected counters remain reviewable
```

Exit criteria:

```text
ordered concurrency does not weaken milestone 020 lifecycle invariants
```

## Slice 6: Gate Capture And Documentation Checkpoint

Status: planned.

Implementation:

```text
record focused milestone 021 gate evidence in a gate document
update this plan's slice statuses as work completes
update handoff with implemented behavior, verification, blockers, and
  remaining warnings
stop before writing decision trace
```

Gate requirements:

```text
ordered-concurrency contract tests pass
ordered result coordinator tests pass
processing/runtime integration tests pass for every implemented surface
startup prewarm is visible and separate
provider and execution defaults are separately assertable
no silent borrowed fallback is introduced
terminal retained pressure is clean
Release build succeeds
focused Release performance or parity matrix is captured against milestone
  020 accepted baseline if ordered concurrency reaches runtime integration
```

Exit criteria:

```text
the milestone is ready for human review before decision trace
```

## Stop Conditions

Stop before decision trace in all cases.

Stop earlier only if one of these blockers is proven:

```text
shared RadarProcessingCore mutation cannot safely support overlapping batch
  processing without a snapshot/merge/commit design
rebalance topology publication cannot be kept deterministic without a larger
  ordered commit layer
retained resource cleanup cannot be made deterministic for concurrent active
  work
focused gates reveal correctness, ordering, fail-closed, or pressure cleanup
  regressions that need architecture review
```

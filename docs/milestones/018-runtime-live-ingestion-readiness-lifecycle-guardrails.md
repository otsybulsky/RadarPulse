# Milestone 018: Lifecycle Guardrails

Status: complete.

This document records the slice 5 backpressure, failure, cancellation, and
cleanup guardrail work for milestone 018.

## Scope

Slice 5 resolved the known `ShutdownMode.CancelQueued` contract gap and
verified the existing lifecycle guardrails needed before runtime-shaped gate
capture.

The implementation remains in-process and queue/session scoped. It does not
change runtime defaults, add durable queues, add cross-process transport, add
automatic borrowed fallback, or add product live-ingestion behavior.

## Implemented Guardrail

`RadarProcessingOwnedBatchQueue` now exposes explicit queued-work cancellation:

```text
operation:
  CancelQueued()

behavior:
  closes provider intake
  wakes enqueue and retained-byte-budget waiters
  removes accepted pending batches that have not reached the consumer
  returns the canceled queued batches with their provider sequence ids
  leaves later enqueue attempts closed
  leaves already active consumer work outside the queue cancellation surface
```

`CompleteAdding()` and default `Close()` behavior still drain accepted work.
The default queue shutdown mode remains `Drain`.

## Session Behavior

`RadarProcessingQueuedProcessingSession` and
`RadarProcessingQueuedRebalanceSession` now apply the configured shutdown mode
when cancellation is observed:

```text
ShutdownMode.Drain:
  close provider intake and preserve existing drain-shaped close behavior

ShutdownMode.CancelQueued:
  close provider intake
  remove accepted pending batches before dequeue
  record canceled processing results for the canceled provider sequence ids
  return a canceled session result
```

Active work that observes cancellation remains recorded as a canceled batch.
Pending work canceled before dequeue is now visible as canceled processing
results instead of silently remaining as queue pressure.

## Overlap Runner Behavior

`RadarProcessingArchiveQueuedOverlapRunner` now applies the same cancellation
shutdown policy on producer or consumer cancellation.

For archive-owned queued overlap:

```text
producer cancellation:
  applies Drain or CancelQueued according to queue options

consumer cancellation:
  applies Drain or CancelQueued according to queue options

terminal cleanup:
  releases pending retained resources
  reports terminal pending, active, and combined retained pressure as zero
```

Queued-owned failure remains fail-closed. A failed or canceled queued-owned row
is not replaced by a borrowed/reference success row.

## Telemetry Contract

`RadarProcessingProviderQueueTelemetrySummary` now allows canceled batches to
represent accepted work canceled before dequeue.

The retained invariant is:

```text
completed + failed + skipped-after-fault <= dequeued
```

Canceled batches may be active-work cancellations or accepted-before-dequeue
cancellations. This lets `CancelQueued` report canceled accepted work without
pretending that it was processed or dequeued.

## Guardrails Verified

Existing tests already covered:

```text
queue full ReturnFull behavior
queue full Wait behavior
enqueue timeout behavior
retained-byte budget rejection and timeout behavior
cancellation before enqueue
cancellation while waiting for enqueue
close rejects later enqueue and drains accepted work
fault rejects later enqueue and remains visible after drain
dispose rejects later enqueue/dequeue and clears pending queue pressure
retained resource pending-to-active-to-release pressure transitions
release failure visibility
validation failure release cleanup
validation failure fail-closed behavior without borrowed fallback
provider enqueue result separation from processing completion
```

Slice 5 added focused tests for:

```text
queue-level CancelQueued clearing accepted pending batches
processing session CancelQueued cancellation with accepted pending batches
rebalance session CancelQueued cancellation with accepted pending batches
telemetry shape for canceled accepted work before dequeue
archive queued overlap CancelQueued cancellation releasing pending retained
  resources and returning terminal retained pressure to zero
```

## Touched Code

```text
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs
tests/RadarPulse.Tests/Processing/RadarProcessingOwnedBatchQueueTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
```

## Verification

Focused guardrail tests:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Result:

```text
Passed: 54
Failed: 0
Skipped: 0
```

Additional full-project check:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Result:

```text
full project attempted twice
same single failure both times:
  RadarProcessingSyntheticRebalanceBenchmarkTests.
    AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
  reason: allocation threshold exceeded in full-suite run

isolated rerun of the failing test:
  passed: 1
  failed: 0
```

Interpretation:

```text
the full-suite failure is an allocation-threshold sensitivity outside the
slice 5 queue/session/overlap guardrail surface; the focused guardrail suite
and isolated failing-test rerun passed
```

## Residual Limits

The following items remain for runtime gate capture rather than slice 5 code:

```text
startup-prewarmed runtime candidate rows still need slice 6/7 harness evidence
natural first-use queued-owned rows still need separate control evidence
borrowed/reference rows still need separate gate evidence
true live ingestion remains a coverage gap
durable queues and cross-process cancellation remain out of scope
release-failure readiness remains tested by deterministic unit fixtures, not
  by a full runtime replay gate yet
```

## Outcome

Slice 5 is complete. The milestone can proceed to slice 6 runtime steady
intake gate capture with the `CancelQueued` lifecycle contract implemented and
tested as an explicit shutdown mode.

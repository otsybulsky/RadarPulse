# Milestone 021: Ordered Concurrent Runtime/Archive Processing Gate

Status: gate captured; stop before decision trace.

This document records milestone 021 gate evidence for review. It does not make
the decision-trace call.

## Gate Question

```text
Is the scoped in-process runtime/archive path ready to process multiple
accepted batches concurrently while preserving deterministic ordered
publication, baseline provenance, fail-closed behavior, and retained-pressure
cleanup?
```

## Implemented Scope

Milestone 021 implemented:

```text
ordered concurrency options with active batch capacity independent from
  provider queue capacity and worker queue capacity

ordered result coordinator for out-of-order completion and deterministic
  provider-sequence publication

processing delta foundation:
  RadarProcessingBatchDelta
  non-mutating handler-free batch delta compute
  ordered RadarProcessingCore delta commit
  pooled dense source-indexed delta arrays

ordered concurrent processing session:
  RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync
  active batch compute bounded by RadarProcessingOrderedConcurrencyOptions
  ordered commit and ordered result recording
  fail-closed skip after earlier failure

async ordered delta support:
  concurrent worker-group dispatch only for non-mutating delta compute
  normal mutating ProcessAsync keeps one-in-flight guard
  AsyncShardTransport worker telemetry remains visible

runtime/archive processing integration:
  RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync
  omitted provider options keep milestone 020 queued-owned pooled-copy
    startup prewarm default
  ordered active capacity defaults through RadarProcessingRuntimeArchiveBaseline
```

Milestone 021 did not implement:

```text
ordered concurrent rebalance/topology commit
handler-state delta/merge contract
durable queues or brokers
cross-process providers/workers
production deployment/operator/rollback surfaces
product-facing radar workflows
automatic silent borrowed fallback
```

## Gate Results

### Contract And Delta Foundation

Result:

```text
passed
```

Evidence:

```text
RadarProcessingOrderedConcurrencyOptions exposes active batch capacity
separately from provider queue capacity and worker queue capacity

RadarProcessingBatchDelta computes handler-free per-batch deltas without
mutating shared core state

RadarProcessingCore.CommitProcessingDelta validates source-local timestamp
ordering against current committed state before mutation

handler cores are explicitly rejected until a handler-delta contract exists
```

### Ordered Processing Session

Result:

```text
passed
```

Evidence:

```text
ordered concurrent processing results publish by provider sequence
active successes after an earlier failure are skipped rather than silently
  published
commit-order source timestamp validation prevents later deltas from mutating
  committed state after an ordered failure
cancellation before dequeue returns canceled with zero retained pressure
existing sequential DrainAsync behavior remains separately tested
```

### Runtime/Archive Processing Integration

Result:

```text
passed for processing-core runtime/archive path
```

Evidence:

```text
RunProcessingAsync composes ordered concurrent processing drain with the
queued-overlap provider path

omitted provider options apply startup retained payload prewarm

AsyncShardTransport processing cores preserve worker telemetry through
non-mutating async delta compute

deterministic live-adapter-shaped ordered processing succeeds with terminal
combined retained pressure zero

validation failure remains visible, later work is skipped after fault, no
borrowed fallback is used, and terminal retained pressure returns to zero
```

### Verification

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Focused milestone 021 gate suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingBatchDeltaTests|FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests"

result:
  46 passed, 0 failed, 0 skipped
```

Full Release test project:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build

result:
  805 passed, 1 failed, 3 skipped
```

Known full-suite failure:

```text
RadarProcessingSyntheticRebalanceBenchmarkTests.
  AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

full-suite failure:
  Expected bounded benchmark aggregation allocation, got 454073528 bytes.
```

Isolated rerun of full-suite failure:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  1 passed, 0 failed, 0 skipped
```

## Gate Interpretation Input

Recommended decision-trace input:

```text
accepted with scoped warnings for processing-core runtime/archive ordered
concurrency
```

Reason:

```text
the scoped processing-core runtime/archive path now has bounded active batch
compute, non-mutating per-batch deltas, ordered shared-state commit,
deterministic provider-sequence publication, visible async worker telemetry,
startup prewarm preservation, fail-closed behavior, and clean retained
pressure across focused success, failure, and cancellation evidence
```

Warnings to carry:

```text
ordered concurrent rebalance/topology commit is not implemented
handler-state delta/merge is not implemented
true live network ingestion is not implemented
durable queues, brokers, and cross-process workers are not implemented
production operator/deployment/rollback surfaces are not implemented
full-suite allocation sensitivity remains for one synthetic benchmark test
performance matrix for representative archive workloads is still needed
before broad default promotion beyond the scoped processing-core path
```

## Post-Gate Performance Matrix Update

After the gate was captured, direct full-cache CLI evidence was added for the
`RunProcessingAsync` ordered-processing path:

```text
docs/milestones/021-ordered-concurrent-runtime-archive-processing-ordered-full-cache-performance-matrix.md
```

Result:

```text
ordered active=4 completed the full cache with processing completeness,
checksum parity against active=1, clean retained pressure, zero worker
failures, zero release failures, and zero retained pool misses
```

Carry-forward warning now narrows to:

```text
the full-cache shape is archive-producer dominated, so additional workload
matrices where processing is the bottleneck are still useful before broad
default promotion beyond the scoped processing-core path
```

Closeout:

```text
do not write decision trace until this gate is reviewed
```

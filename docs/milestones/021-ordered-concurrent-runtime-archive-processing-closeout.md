# Milestone 021: Closeout

## Status

Milestone 021 is complete.

RadarPulse now has an explicit ordered concurrent runtime/archive processing
path over the accepted milestone 020 default baseline. The scoped
`RunProcessingAsync` processing-core surface can keep multiple accepted
batches active, compute them concurrently, and publish externally visible
processing results in deterministic provider sequence order.

The important milestone result is:

```text
020 accepted RadarProcessingRuntimeArchiveBaseline as the named construction
    profile for composing queued-owned provider defaults with async shard
    transport execution defaults.
021 adds ordered active-batch processing over that baseline for the scoped
    processing-core runtime/archive path.
021 uses non-mutating per-batch delta compute plus provider-sequence ordered
    commit instead of overlapping shared RadarProcessingCore mutation.
021 preserves fail-closed queued-owned behavior, visible startup prewarm,
    worker telemetry, checksum/completeness parity, release health, and
    terminal retained pressure cleanup.
```

Final readiness posture:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
processing-core path is ready to keep multiple accepted batches active,
compute them concurrently, and publish externally visible processing results
in deterministic provider sequence order over the accepted milestone 020
baseline
```

The accepted warnings and limits are:

```text
rebalance/topology ordered commit:
  not implemented; pressure, policy, quarantine, telemetry, decision, and
  topology mutation still require a larger ordered commit design

handler-state delta/merge:
  not implemented; custom handler cores are rejected by the ordered delta path
  until handler state has an explicit delta/merge contract

performance breadth:
  direct full-cache ordered-processing evidence is clean, but the measured
  cache shape is archive-producer dominated; processing-bottleneck and
  repeated variance matrices remain useful before broad default promotion

startup prewarm:
  accepted as visible lifecycle cost; active=4 prewarms four retained batches
  and does not hide that cost inside steady measured allocation

live/durable/production runtime:
  true live network ingestion, durable queues, brokers, cross-process
  providers/workers, production operator/deployment/rollback surfaces, and
  product-facing workflows remain future work

full-suite allocation sensitivity:
  one synthetic benchmark allocation-threshold test remains sensitive in the
  full suite but passes in isolated rerun
```

## Final Outcome

Implemented:

- `RadarProcessingOrderedConcurrencyOptions` as the explicit active batch
  capacity contract.
- Active batch capacity default `4`, independent from provider queue capacity
  `8` and worker queue capacity `8`.
- `RadarProcessingRuntimeArchiveBaseline` ordered-concurrency helpers and
  matchable ordered active batch capacity.
- `RadarProcessingOrderedResultCoordinator` for out-of-order completion and
  provider-sequence publication.
- `RadarProcessingBatchDelta` as the handler-free non-mutating per-batch
  delta foundation.
- `RadarProcessingCore.ComputeProcessingDelta` for non-mutating batch delta
  compute.
- `RadarProcessingCore.CommitProcessingDelta` for provider-sequence ordered
  shared-state mutation and cumulative result creation.
- Source-local timestamp validation during ordered commit.
- Explicit handler-core rejection until a handler-delta contract exists.
- `RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync` as the
  opt-in ordered concurrent drain path.
- Later active success skip after an earlier failure boundary.
- Async ordered delta compute through `RadarProcessingAsyncCoreSession`.
- Worker-group concurrent dispatch only for non-mutating delta compute while
  normal mutating `ProcessAsync` keeps the one-in-flight guard.
- `RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync` as the
  explicit ordered runtime/archive processing entry point.
- `processing benchmark ordered-archive-processing` as the direct full-cache
  CLI benchmark for `RunProcessingAsync`.
- Ordered-path startup retained payload prewarm scaling to active capacity.
- Ordered-path retained payload factory sizing to avoid active-batch
  large-array pool misses.
- Gate evidence, full-cache default-vs-borrowed matrix, direct
  ordered-processing full-cache matrix, decision trace, handoff, and
  project-progress updates.

Not implemented here:

- Ordered concurrent rebalance/topology commit.
- Handler-state delta/merge contract.
- Automatic concurrency for caller-owned processing cores.
- True live network ingestion.
- Durable queues or brokers.
- Cross-process providers or workers.
- Production operator/deployment/rollback surfaces.
- Product-facing radar workflows.
- Automatic silent borrowed fallback.
- Changing `RadarProcessingCoreOptions.Default`.
- Reopening the milestone 020 provider/execution baseline decision.

Still rejected:

```text
automatic silent borrowed fallback
```

## Final Ordered Runtime/Archive Baseline

Accepted ordered surface:

```text
RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync
RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync
RadarProcessingCore.ComputeProcessingDelta
RadarProcessingCore.CommitProcessingDelta
processing benchmark ordered-archive-processing
```

Accepted provider and execution contour:

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

Accepted ordered commit contour:

```text
concurrent compute:
  validate immutable batch shape
  compute per-batch source deltas without mutating shared RadarProcessingCore
    state
  use dense source-indexed pooled arrays
  preserve async worker telemetry for AsyncShardTransport

ordered commit:
  commit deltas strictly by provider sequence
  validate source-local timestamp ordering against current committed state
  mutate shared RadarProcessingCore cumulative state only in ordered commit
  create cumulative RadarProcessingResult after ordered mutation
  publish externally visible results in provider sequence order
```

Ownership boundary:

```text
RunProcessingAsync is the accepted explicit ordered processing path

caller-owned processing cores and sessions remain explicit

rebalance/topology mutation and handler-state mutation remain outside the
accepted ordered active-batch surface until they have their own ordered commit
or delta/merge contracts
```

## Gate Summary

Contract and delta foundation:

```text
passed

ordered active batch capacity is explicit and separate from provider queue
capacity and worker queue capacity

handler-free per-batch delta compute does not mutate shared
RadarProcessingCore state

ordered commit validates source-local timestamp ordering before mutation

handler cores are rejected until handler-delta support exists
```

Ordered processing session:

```text
passed

ordered concurrent processing results publish by provider sequence

completion may occur out of provider order

active successes after an earlier failure are skipped rather than silently
published

cancellation before dequeue returns canceled with zero retained pressure

existing sequential DrainAsync behavior remains separately tested
```

Runtime/archive processing integration:

```text
passed for processing-core runtime/archive path

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

## Full-Cache Performance Matrices

Post-gate rebalance-archive matrix:

```text
processing benchmark rebalance-archive --cache data\nexrad
--max-files 1000000 --mode all
```

Cache shape:

```text
examined files: 1_554
skipped files: 726
published base-data files: 828
stream events: 27_254_760
payload values: 32_306_203_200
```

End-to-end default ratios versus explicit BlockingBorrowed oracle:

```text
static:
  elapsed: 0.965x
  allocation: 1.003x

sampling:
  elapsed: 0.878x
  allocation: 1.001x

rebalance-session:
  elapsed: 0.884x
  allocation: 1.000x
```

Correctness and lifecycle:

```text
validation succeeded
processing completeness succeeded
checksum parity matched in all modes
accepted moves matched at 4 vs 4 for rebalance-session
worker failed batches/items 0/0
release failures 0
terminal combined retained pressure 0
```

Direct ordered-processing matrix:

```text
processing benchmark ordered-archive-processing --cache data\nexrad
--max-files 1000000
```

Active=4 ordered default versus active=1 same-path baseline:

```text
elapsed ratio: 0.994x
steady allocation ratio: 1.006x
final processing checksum matched:
  2_294_439_733_285_583_699
processing completeness passed
worker failed batches/items 0/0
retained payload pool misses 0
event array pool misses 0
byte array pool misses 0
release failures 0
terminal combined retained pressure 0
```

Active=4 lifecycle:

```text
startup prewarm batch count: 4
startup prewarm allocated bytes: 285_213_112
startup prewarm retained bytes: 285_212_672
active retained batches high watermark: 4
active retained payload bytes high watermark: 213_402_240
steady measured allocation excludes startup prewarm
```

Interpretation:

```text
direct RunProcessingAsync full-cache evidence is clean

the measured full-cache workload is archive-producer dominated, so
active-batch concurrency has little end-to-end elapsed leverage on this
corpus

active=4 completes deterministically with clean retained pressure and zero
retained pool misses
```

## Preserved Invariants

```text
the milestone 020 provider/execution baseline decision remains closed
startup prewarm cost remains visible and separate from steady measured
  allocation
provider queue capacity, active batch capacity, and worker queue capacity
  remain separate contracts
concurrent compute does not mutate shared RadarProcessingCore state
shared processing state mutates only through ordered commit
externally visible processing results publish in provider sequence order
later active successes do not publish past an earlier failure boundary
provider enqueue success remains distinct from processing completion
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
release failures remain readiness blockers
retained pressure returns to zero after success, cancellation, failure, drain,
  and cleanup paths
worker telemetry remains visible for async shard transport
RadarProcessingCoreOptions.Default remains conservative
```

## Verification

Final verification used for closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused milestone 021 Release gate suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingBatchDeltaTests|FullyQualifiedName~RadarProcessingOrderedResultCoordinatorTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingAsyncWorkerGroupTests"
  result: 46 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 805 passed, 1 failed, 3 skipped
  failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    Expected bounded benchmark aggregation allocation, got 454073528 bytes.

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped

post-CLI benchmark focused Debug suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
  result: 38 passed, 0 failed, 0 skipped

post-CLI benchmark focused Release suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionOrderedConcurrentTests"
  result: 38 passed, 0 failed, 0 skipped
```

The full-suite failure matches the known allocation-sensitive synthetic
benchmark caveat carried from earlier milestones. It passed in isolated rerun
and is outside the ordered runtime/archive processing surface.

This closeout slice is documentation-only. No additional test run was needed
after closeout text updates.

## Decision Trace

The decision trace is written in
`021-ordered-concurrent-runtime-archive-processing-decision-trace.md`.

Final closeout answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
processing-core path is ready to keep multiple accepted batches active,
compute them concurrently, and publish externally visible processing results
in deterministic provider sequence order over the accepted milestone 020
baseline
```

Recommended next milestone input:

```text
implement ordered rebalance/topology commit over the ordered processing
foundation, and collect processing-bottleneck performance evidence before
broader default promotion. Preserve the non-mutating delta plus ordered commit
model, visible startup prewarm, fail-closed queued-owned behavior, no silent
borrowed fallback, worker telemetry, release health, and terminal retained
pressure cleanup.
```

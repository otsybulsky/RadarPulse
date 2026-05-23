# Milestone 019: Closeout

## Status

Milestone 019 is complete.

RadarPulse now accepts startup-prewarmed queued-owned as the omitted default
for the scoped in-process runtime/archive queued-overlap provider path and as
the default baseline for remaining runtime/archive integration work.

The important milestone result is:

```text
017 accepted the prewarmed queued-owned direct benchmark default-equivalent
    contour.
018 accepted startup-prewarmed queued-owned as runtime-safe when selected
    explicitly for scoped in-process runtime/archive surfaces.
019 promotes that contour into the scoped queued-overlap omitted default and
    makes it the baseline rail for remaining runtime/archive work.
```

Final readiness posture:

```text
accepted with scoped warnings, startup-prewarmed queued-owned is accepted as
the omitted default for the scoped in-process runtime/archive queued-overlap
provider path and as the default baseline for remaining runtime/archive work
```

The accepted warnings and limits are:

```text
startup prewarm:
  accepted as visible default lifecycle cost; not hidden inside steady
  measured allocation

processing execution:
  queued-overlap defaults do not rewrite processing core execution mode or
  async worker sizing; future processing-core default work should adopt the
  accepted baseline explicitly

future runtime/archive surfaces:
  live network ingestion, durable queues, cross-process providers/workers,
  ordered concurrent rebalance, builder-transfer, production operator
  surfaces, and product-facing workflows are not implemented by this
  milestone, but they inherit the accepted default baseline unless a concrete
  surface incompatibility is proven

fallback:
  automatic silent borrowed fallback remains rejected
```

## Final Outcome

Implemented:

- `RadarProcessingArchiveQueuedOverlapOptions.Default` now represents the
  runtime rollout contour: provider queue capacity `8`, retained-byte budget
  `536870912`, pooled-copy retained payload strategy, and rollout retained
  payload prewarm options.
- `RadarProcessingRetainedPayloadPrewarmOptions` records prewarm sizing and
  validates all-or-none prewarm configuration.
- `RadarProcessingArchiveQueuedOverlapRunner` applies startup retained
  payload prewarm before steady overlap allocation capture when options
  request it.
- The prewarmed retained payload factory is passed into
  `ArchiveOwnedRadarEventBatchQueueingPublisher`.
- `RadarProcessingArchiveQueuedOverlapResult` surfaces
  `RadarProcessingRetainedPayloadPrewarmResult` separately from steady
  overlap telemetry.
- Explicit constructed options remain diagnostic/no-prewarm unless the caller
  explicitly requests prewarm.
- Focused runtime default tests cover omitted default prewarm and explicit
  no-prewarm diagnostics.
- Gate evidence and decision trace were recorded in the standard milestone
  format.

Not implemented here, but inheriting the accepted baseline:

- Processing-core execution mode or async worker sizing defaulting.
- True live network ingestion implementation.
- Durable queues or brokers.
- Cross-process provider or worker transport.
- Ordered concurrent rebalance.
- Builder-transfer retained payload execution.
- Production operator/deployment/rollback surfaces.
- Product-facing radar workflows.

Still rejected:

```text
automatic silent borrowed fallback
```

## Final Runtime Default Baseline

Accepted default surface:

```text
RadarProcessingArchiveQueuedOverlapRunner omitted options
RadarProcessingArchiveQueuedOverlapOptions.Default
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingOwnedBatchQueue
RadarProcessingRetainedPayloadFactory
```

Accepted default contour:

```text
provider path: queued-owned by construction of queued-overlap runner
provider overlap: producer-consumer queued-overlap runner
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
```

Execution ownership:

```text
processing execution mode remains supplied by the processing core or rebalance
session
queued-overlap defaults do not rewrite execution mode or async worker sizing
future processing-core defaulting should adopt the accepted baseline
explicitly rather than reopen the provider default decision
```

## Gate Summary

Focused runtime default gate:

```text
test:
  RadarProcessingArchiveQueuedOverlapRunnerTests.
    OmittedOptionsApplyRuntimeDefaultStartupPrewarm

result:
  completed: true
  retained payload prewarm applied: true
  prewarm event count: 65_536
  prewarm payload bytes: 67_108_864
  prewarm retained batch count: 1
  prewarm allocated bytes: greater than 0
  prewarm retained bytes: greater than 0
  steady measured allocation: less than startup prewarm allocated bytes
  retention strategy: pooled-copy
  release attempts/releases/failures: 1/1/0
  terminal combined retained batch count: 0
  terminal combined retained payload bytes: 0
```

Explicit no-prewarm diagnostic guard:

```text
test:
  RadarProcessingArchiveQueuedOverlapRunnerTests.
    ExplicitOptionsDoNotApplyStartupPrewarmUnlessRequested

result:
  explicit options complete
  retained payload prewarm applied: false
  retention strategy: snapshot-copy
```

## Preserved Invariants

```text
prewarm cost remains visible and separate from steady measured allocation
provider enqueue success remains distinct from processing completion
queued-owned failures fail closed
no automatic borrowed fallback follows queued-owned failure
explicit BlockingBorrowed remains fallback/oracle where supported
release failures remain readiness blockers
retained pressure must return to zero after success, cancellation, failure,
  drain, and cleanup paths
explicit diagnostic/no-prewarm options remain available for tests and named
  exceptions
future runtime/archive work starts from the accepted default baseline unless
  a concrete surface incompatibility is proven
```

## Verification

Final verification used for closeout:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused Debug runtime/prewarm suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
  result: 41 passed, 0 failed, 0 skipped

focused Release runtime/prewarm suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"
  result: 41 passed, 0 failed, 0 skipped

full test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  result: 776 passed, 1 failed, 3 skipped
  failure:
    RadarProcessingSyntheticRebalanceBenchmarkTests.
      AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
    expected bounded allocation, got 469_019_824 bytes

isolated rerun of full-suite failure:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed
```

The full-suite failure matches the known allocation-sensitive synthetic
benchmark caveat from milestone 018 and passed in isolated rerun. It is
outside the runtime queued-overlap default promotion surface.

This closeout slice is documentation-only. No additional test run was needed
after closeout text updates.

## Decision Trace

The decision trace is written in
`019-prewarmed-queued-owned-runtime-default-promotion-decision-trace.md`.

Final closeout answer:

```text
accepted with scoped warnings, startup-prewarmed queued-owned is accepted as
the omitted default for the scoped in-process runtime/archive queued-overlap
provider path and as the default baseline for remaining runtime/archive work
```

Recommended next milestone input:

```text
use the accepted prewarmed queued-owned runtime/archive default baseline for
remaining integration work. Add processing-core execution defaulting only in
the surface that owns core construction, add live adapter or production
pipeline evidence as integration evidence, and keep prewarm, pressure,
cancellation, failure, release, cleanup, and explicit borrowed fallback/oracle
visible without reopening the provider default decision.
```

# Milestone 019: Runtime Default Promotion Gate

Status: complete.

This document records the milestone 019 gate evidence for promoting the
startup-prewarmed queued-owned contour into the scoped runtime
queued-overlap omitted-default path.

## Gate Scope

The gate validates the implementation gap identified for milestone 019:

```text
RadarProcessingArchiveQueuedOverlapRunner with omitted options now uses the
runtime rollout contour:
  provider queue capacity: 8
  retained-byte budget: 536870912
  retained payload strategy: pooled-copy
  startup retained payload prewarm: enabled and visible
```

This gate does not implement true live network ingestion, durable queues,
brokers, cross-process workers, ordered concurrent rebalance, or
product-facing readiness. Those future surfaces should inherit the accepted
default baseline unless their boundary proves a concrete incompatibility.

## Implemented Contract

Runtime default options:

```text
RadarProcessingArchiveQueuedOverlapOptions.Default:
  queue capacity: 8
  queue retained-byte budget: 536870912
  retained strategy: pooled-copy
  retained options retained-byte budget: 536870912
  prewarm options: rollout default
  retained payload factory: created by runner at startup
```

Execution-scope note:

```text
the promoted default is the queued-overlap provider/retention/prewarm default.
Processing execution mode remains owned by the supplied processing core or
rebalance session. This gate does not claim that omitted queued-overlap
options can rewrite an existing rebalance session into async shard transport.
```

Explicit constructed options remain explicit diagnostics:

```text
new RadarProcessingArchiveQueuedOverlapOptions():
  queue options: RadarProcessingProviderQueueOptions.Default
  retained payload options: RadarProcessingRetainedPayloadOptions.Default
  prewarm options: None
```

Startup prewarm lifecycle:

```text
prewarm runs before steady overlap allocation capture
prewarmed factory is passed to ArchiveOwnedRadarEventBatchQueueingPublisher
prewarm result is surfaced on RadarProcessingArchiveQueuedOverlapResult
steady overlap telemetry remains separate from prewarm allocation
```

## Focused Runtime Gate

Focused test:

```text
RadarPulse.Tests.Processing.
  RadarProcessingArchiveQueuedOverlapRunnerTests.
    OmittedOptionsApplyRuntimeDefaultStartupPrewarm
```

Gate shape:

```text
input:
  synthetic archive-shaped leased batch published through
  IArchiveRadarEventBatchPublisher

runtime path:
  RadarProcessingArchiveQueuedOverlapRunner.RunAsync(..., options: null)

consumer path:
  dequeues accepted batch
  acquires retained consumer resource lease
  returns successful processing result
```

Assertions:

```text
result completed: true
retained payload prewarm applied: true
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
prewarm allocated bytes: greater than 0
prewarm retained bytes: greater than 0
steady measured allocation: less than startup prewarm allocated bytes
retention strategy: pooled-copy
release attempts: 1
released batches: 1
release failures: 0
current combined retained batch count: 0
current combined retained payload bytes: 0
```

Explicit no-prewarm guard:

```text
RadarPulse.Tests.Processing.
  RadarProcessingArchiveQueuedOverlapRunnerTests.
    ExplicitOptionsDoNotApplyStartupPrewarmUnlessRequested

assertions:
  explicit options complete
  retained payload prewarm applied: false
  retention strategy: snapshot-copy
```

## Verification

Focused Debug verification:

```text
command:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"

result:
  passed: 41
  failed: 0
  skipped: 0
```

Release build:

```text
command:
  dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded
  warnings: 0
  errors: 0
```

Focused Release verification:

```text
command:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release --no-restore
    --filter "FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests"

result:
  passed: 41
  failed: 0
  skipped: 0
```

Full test project:

```text
command:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore

result:
  passed: 776
  failed: 1
  skipped: 3

failure:
  RadarProcessingSyntheticRebalanceBenchmarkTests.
    AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
  expected bounded benchmark aggregation allocation, got 469_019_824 bytes
```

Isolated rerun of the full-suite failure:

```text
command:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  passed: 1
  failed: 0
```

Interpretation:

```text
the full-suite failure matches the known milestone 018 allocation-sensitive
synthetic benchmark caveat and passed in isolated rerun. It is outside the
runtime queued-overlap default promotion surface.
```

## Decision-Trace Input

Evidence available for review before decision trace:

```text
omitted runtime queued-overlap options now use the startup-prewarmed
queued-owned rollout contour
startup prewarm is visible on the runtime result
startup prewarm allocation is separate from steady overlap allocation
explicit diagnostic/no-prewarm options remain available
focused runtime guardrails pass in Debug and Release
Release build is clean
full-suite residual risk is the known allocation-sensitive synthetic benchmark
```

Open review question before decision trace:

```text
Should milestone 019 accept the promoted default for the scoped in-process
runtime/archive queued-overlap surface and carry it as the default baseline
for remaining runtime/archive work, while treating true live ingestion and
durable/cross-process surfaces as future implementation boundaries?
```

Scoped warning to carry into review:

```text
runtime queued-overlap defaults now cover queue capacity, retained-byte budget,
pooled-copy retention, and startup prewarm. Processing execution mode and async
worker sizing are still supplied by the processing core/rebalance session.
```

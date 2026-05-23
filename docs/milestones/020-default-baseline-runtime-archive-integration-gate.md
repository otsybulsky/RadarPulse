# Milestone 020: Default-Baseline Runtime/Archive Integration Gate

Status: complete.

This document records the milestone 020 gate evidence before decision trace.
It is not the decision trace.

## Gate Question

```text
Are the remaining scoped in-process runtime/archive integration surfaces ready
to consume the accepted prewarmed queued-owned plus async execution default
baseline without reopening the provider default decision?
```

## Implemented Surface

Milestone 020 implemented:

```text
RadarProcessingRuntimeArchiveBaseline:
  named runtime/archive baseline profile
  default async execution options
  default processing core options for caller-supplied topology shape
  default processing core construction
  default rebalance session construction
  provider/default and execution/default match helpers

focused runtime/archive integration tests:
  baseline profile contract
  owned rebalance-session construction
  omitted queued-overlap provider default composition
  caller-supplied rebalance session execution preservation
  deterministic live-adapter-shaped steady intake
  deterministic live-adapter-shaped validation failure cleanup

reporting/provenance audit:
  no production result-contract change required before gate capture
```

Milestone 020 did not implement:

```text
true live network ingestion
durable queues or brokers
cross-process providers/workers
ordered concurrent rebalance
production deployment/operator/rollback surfaces
product-facing radar workflows
automatic silent borrowed fallback
```

## Gate Results

### Baseline Contract

Result:

```text
passed
```

Evidence:

```text
RadarProcessingRuntimeArchiveBaseline creates async shard transport core
options with worker count 4 and worker queue capacity 8

RadarProcessingRuntimeArchiveBaseline.QueuedOverlapOptions delegates to the
milestone 019 queued-overlap omitted provider default

baseline matching rejects non-rollout execution shapes

RadarProcessingCoreOptions.Default remains the existing conservative
sequential default
```

### Owned Construction Integration

Result:

```text
passed
```

Evidence:

```text
baseline-created rebalance sessions use async shard transport with worker
count 4 and worker queue capacity 8

baseline-created rebalance sessions compose with omitted queued-overlap
provider defaults

omitted provider defaults apply pooled-copy retention and startup retained
payload prewarm

caller-supplied rebalance sessions keep their explicit execution mode and are
not silently rewritten into async shard transport
```

### Live-Adapter-Shaped Evidence

Result:

```text
passed
```

Steady intake evidence:

```text
deterministic in-memory archive-shaped batches complete through the integrated
baseline path

provider accepted publish count matches processed batch count
processing completion succeeds for all batches
worker failed/canceled/timed-out/rejected counters remain zero
retained release failures remain zero
terminal combined retained pressure returns to zero
startup retained payload prewarm remains visible
```

Failure cleanup evidence:

```text
deterministic validation failure faults the consumer path
producer remains completed after accepted input
failed validation is visible as failed processing, not hidden fallback
accepted remainder is skipped after fault
retained release failures remain zero
terminal combined retained pressure returns to zero
retention strategy remains pooled-copy
```

### Reporting And Provenance

Result:

```text
passed
```

Evidence:

```text
docs/milestones/020-default-baseline-runtime-archive-integration-provenance-audit.md
```

Conclusion:

```text
existing result, telemetry, prewarm, worker, and CLI provenance contracts are
sufficient for milestone 020 gate review; no production result-contract
change is required before decision trace
```

## Verification

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Focused milestone 020 gate suite:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour"

result:
  24 passed, 0 failed, 0 skipped
```

Full test project:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore

result:
  787 passed, 1 failed, 3 skipped

failure:
  RadarProcessingSyntheticRebalanceBenchmarkTests.
    AcceptedMovePressureAggregationDoesNotCopyPreviousIterations
  expected bounded benchmark aggregation allocation, got 489_482_744 bytes
```

Isolated rerun of full-suite failure:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  1 passed, 0 failed, 0 skipped
```

The full-suite failure matches the known allocation-sensitive synthetic
benchmark caveat carried from milestones 018 and 019. It passed in isolated
rerun and is outside the runtime/archive default-baseline integration surface.

## Gate Interpretation Input

Recommended decision-trace input:

```text
accepted with scoped warnings
```

Reason:

```text
the scoped in-process runtime/archive integration boundary now has a named
baseline profile, owned construction evidence, live-adapter-shaped steady and
failure evidence, visible provider/execution provenance, visible startup
prewarm cost, clean retained pressure, release health, and no silent borrowed
fallback
```

Warnings to carry:

```text
startup retained payload prewarm remains a visible lifecycle cost
true live network ingestion is not implemented
durable queues, brokers, cross-process workers, and ordered concurrent
rebalance are not implemented
production operator/deployment/rollback surfaces are not implemented
full-suite allocation sensitivity remains for one synthetic benchmark test
```

Stop point:

```text
stop before decision trace for review
```

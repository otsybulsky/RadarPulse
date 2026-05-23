# Milestone 020: Reporting And Provenance Audit

Status: complete.

This document records the milestone 020 slice 4 reporting and provenance pass.

The audit question was:

```text
Does default-baseline runtime/archive integration need new production result
fields before gate capture, or are the current contracts sufficient to review
provider defaulting, execution defaulting, startup prewarm cost, and lifecycle
health?
```

## Finding

No production result-contract change is required before milestone 020 gate
capture.

The existing contracts are sufficient for the current scoped in-process
runtime/archive integration boundary:

```text
provider defaulting:
  RadarProcessingArchiveQueuedOverlapOptions.Default
  RadarProcessingArchiveQueuedOverlapOptions.IsRuntimeDefaultContour
  RadarProcessingRuntimeArchiveBaseline.MatchesQueuedOverlapOptions()

execution defaulting:
  RadarProcessingRuntimeArchiveBaseline.CreateCoreOptions()
  RadarProcessingRuntimeArchiveBaseline.CreateRebalanceSession()
  RadarProcessingRuntimeArchiveBaseline.MatchesCoreOptions()
  RadarProcessingResult.ExecutionMode
  RadarProcessingResult.PartitionCount
  RadarProcessingResult.ShardCount
  RadarProcessingWorkerTelemetrySummary.WorkerCount
  RadarProcessingWorkerTelemetrySummary.QueueCapacity

startup prewarm cost:
  RadarProcessingArchiveQueuedOverlapResult.RetainedPayloadPrewarm
  RadarProcessingArchiveQueuedOverlapResult.HasRetainedPayloadPrewarm

steady allocation and provider lifecycle:
  RadarProcessingArchiveQueuedOverlapResult.OverlapTelemetry
  RadarProcessingArchiveOverlapTelemetrySummary.MeasuredAllocatedBytes
  RadarProcessingArchiveOverlapTelemetrySummary.RetentionStrategy
  RadarProcessingArchiveOverlapTelemetrySummary.RetentionTelemetry
  RadarProcessingProviderQueueTelemetrySummary
  RadarProcessingRetainedResourcePressureSummary

processing completeness and failure health:
  RadarProcessingQueuedSessionResult.ProcessingResults
  RadarProcessingProviderQueueTelemetrySummary.CompletedBatchCount
  RadarProcessingProviderQueueTelemetrySummary.FailedBatchCount
  RadarProcessingProviderQueueTelemetrySummary.CanceledBatchCount
  RadarProcessingProviderQueueTelemetrySummary.SkippedAfterFaultCount
  RadarProcessingWorkerTelemetryCounters
```

## Runtime/Archive Integration Posture

Milestone 020 added the named baseline profile:

```text
RadarProcessingRuntimeArchiveBaseline
```

The profile keeps provider and execution halves separately reviewable:

```text
provider half:
  QueuedOverlapOptions delegates to
  RadarProcessingArchiveQueuedOverlapOptions.Default

execution half:
  CreateCoreOptions() and CreateRebalanceSession() create async shard
  transport cores with worker count 4 and worker queue capacity 8
```

The runner still preserves caller ownership:

```text
RadarProcessingArchiveQueuedOverlapRunner.RunRebalanceAsync() does not rewrite
an already supplied RadarProcessingRebalanceSession
```

That boundary is covered by focused tests.

## CLI And Benchmark Provenance

The archive rebalance benchmark CLI already reports direct benchmark option
provenance:

```text
provider mode source
provider overlap source
retention strategy source
worker queue capacity source
execution mode source
worker count source
provider default rollout contour
retained payload prewarm sizing and cost
```

This remains direct benchmark reporting, not a production runtime operator
contract. It is sufficient as a regression guard for milestone 020 because it
proves the existing provenance vocabulary still distinguishes rollout
defaults from explicit and current-default inputs.

## No New Fields Added

No new production fields were added in this slice because the current evidence
can already answer the milestone 020 review questions:

```text
is provider defaulting visible?
  yes

is execution defaulting visible?
  yes

is startup prewarm separate from steady measured allocation?
  yes

are retained pressure, release, cancellation/failure, and processing
completeness visible?
  yes

does the scoped integration hide queued-owned failure behind borrowed fallback?
  no
```

Future production operator/deployment surfaces may still need their own
reporting contract. That remains outside milestone 020.

## Verification

Focused provenance and integration verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRuntimeArchiveBaselineTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandUsesRolloutDefaultsWhenProviderOmitted|FullyQualifiedName~ArchiveRebalanceBenchmarkCommandLabelsDefaultCandidateContour"

result:
  13 passed, 0 failed, 0 skipped
```

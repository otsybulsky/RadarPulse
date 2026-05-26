# Milestone 025: Handler Delta/Merge Contract For Fast Custom Analytics Gate

Status: captured before decision trace; milestone implementation slices are
complete.

This document records pre-decision gate evidence for milestone 025. It does
not record the decision trace and does not close the milestone.

## Scope Under Gate

The gate covers the scoped in-process MVP handler delta/merge path:

```text
mergeable handler classification
per-batch handler delta identity, validation, serialization, and versioning
deterministic provider-sequence handler delta merge
MVP runtime selection for mergeable, snapshot-only, and unsupported handlers
BFF/read-model provenance and diagnostics
handler-heavy deterministic performance gate
```

The gate does not claim:

```text
production persistent durable adapter readiness
production HTTP BFF host readiness
frontend readiness
true live network ingestion readiness
cross-machine performance certification
exactly-once production delivery
safe concurrency for arbitrary stateful handlers that do not opt into the
  mergeable contract
```

## Implemented Slices

```text
slice 1 handler classification contract: complete
slice 2 per-batch handler delta contract: complete
slice 3 deterministic ordered merge coordinator: complete
slice 4 MVP runtime integration and fallback policy: complete
slice 5 BFF compatibility and diagnostics: complete
slice 6 handler-heavy performance gate: complete
slice 7 pre-decision trace review point: reached
```

## Accepted Runtime Shape Under Test

```text
handler-free:
  keeps existing ordered concurrent processing-delta runtime posture

snapshot-only handlers:
  keep explicit sequential fallback and committed snapshot export

mergeable handlers:
  may use ordered concurrent handler delta compute
  produce immutable per-batch handler deltas
  merge only through provider-sequence ordered commit
  expose merged output through milestone 024 read models

unsupported handlers:
  fail closed through handler output diagnostics and readiness blocking
```

## Focused Verification

Slice 1:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerOutputContractTests"

result:
  11 passed, 0 failed, 0 skipped
```

Slice 2:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaContractTests"

result:
  6 passed, 0 failed, 0 skipped
```

Slice 3:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests"

result:
  6 passed, 0 failed, 0 skipped
```

Slice 4:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingMvpRuntimePlanTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests"

result:
  7 passed, 0 failed, 0 skipped
```

Slice 5:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingBffReadModelStoreTests|FullyQualifiedName~RadarProcessingRunReadModelTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests"

result:
  11 passed, 0 failed, 0 skipped
```

Focused milestone 025 Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests"

result:
  26 passed, 0 failed, 0 skipped
```

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Full Test Project

Full Release test project:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build

result:
  890 passed, 1 failed, 3 skipped
```

Observed failure:

```text
RadarProcessingSyntheticRebalanceBenchmarkTests.
  AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

message:
  Expected bounded benchmark aggregation allocation, got 894196968 bytes.
```

Known-caveat isolated rerun:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"

result:
  1 passed, 0 failed, 0 skipped
```

Interpretation:

```text
the full-suite failure matches the allocation-sensitive synthetic benchmark
caveat previously recorded in earlier milestone evidence; the isolated rerun
passed and the failure is outside the handler delta/merge focused gate
```

## Gate Evidence Summary

The focused gate proves:

```text
handler classification is explicit and fail-closed
existing stateful handlers remain snapshot-only by default
mergeable handlers must opt into mergeable classification and merger contract
handler deltas are value-like, versioned, serializable, and replayable
duplicate delta application does not double-count merged state
out-of-order worker completion does not become handler output order
provider-sequence merge produces parity with sequential fallback
MVP runtime uses ordered handler delta/merge only for mergeable handlers
snapshot-only handlers keep sequential fallback
unsupported handler sets surface readiness diagnostics
BFF read models preserve existing query shape and expose provenance
handler-heavy deterministic gate captures parity, retained cleanup, elapsed,
  and allocation evidence
```

## Proposed Decision Trace Warnings

The decision trace should discuss these scoped warnings:

```text
the accepted fast path applies only to explicitly mergeable handlers
mergeable handlers must provide deterministic handler-owned merge semantics
delta serialization is an in-process/versioned contract gate, not a
  production persistent adapter proof
the performance gate is deterministic in-process evidence, not cross-machine
  or production throughput certification
persistent durable adapter readiness remains future reliability work
true live network ingestion remains future work
production HTTP BFF host and frontend remain future work
production deployment, rollback, autoscaling, alerts, and runbooks remain
  future work
exactly-once production delivery is not claimed
the known allocation-sensitive synthetic benchmark caveat remains outside
  the focused handler delta/merge gate
```

## Stop Point

Milestone 025 is ready for decision trace discussion.

Do not write
`025-handler-delta-merge-contract-for-fast-custom-analytics-decision-trace.md`
until the decision trace posture and warnings are reviewed.

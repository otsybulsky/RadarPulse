# Milestone 025: Handler Delta/Merge Contract For Fast Custom Analytics Implementation Plan

Status: planned.

This plan implements the milestone 025 architecture defined in
`025-handler-delta-merge-contract-for-fast-custom-analytics.md`.

The plan is scoped to the handler delta/merge contract needed for fast custom
analytics over deterministic archive-shaped workloads. It does not implement
a frontend application, production HTTP BFF host, persistent durable adapter,
true live network ingestion, production operations, or exactly-once
production delivery.

## Goal

Implement a safe ordered concurrent path for explicitly mergeable stateful
custom handlers.

The milestone must prove these concrete outcomes:

```text
handler execution classification is explicit
mergeable handlers can produce immutable per-batch deltas
handler deltas have stable identity, validation, serialization, and version
  boundaries
completed handler deltas may arrive out of order but merge only by provider
  sequence
merged handler output matches the existing sequential fallback output
snapshot-only handlers keep explicit sequential fallback
unsupported handler sets fail closed with diagnostics
retry, replay, duplicate delta application, and merge idempotency are tested
merged output remains compatible with milestone 024 BFF read models
handler-heavy performance gates exercise handler work at large volume
```

Scope note:

```text
milestone 025 does not make arbitrary stateful handlers concurrent. Only
handlers that opt into the mergeable contract may enter ordered concurrent
handler delta compute.
```

## Slice 1: Handler Classification Contract

Status: complete.

Implementation:

```text
add handler execution classification values:
  mergeable
  snapshot-only
  unsupported
add metadata that connects the classification to existing handler output
  descriptors
make existing stateful handlers default to snapshot-only unless they
  explicitly opt into mergeable semantics
add runtime posture validation for handler sets:
  all mergeable -> ordered delta/merge eligible
  any snapshot-only -> sequential fallback unless fail-closed mode is chosen
  any unsupported -> fail closed with diagnostic
preserve handler-free ordered concurrent eligibility
```

Tests:

```text
handler-free posture remains ordered concurrent eligible
existing stateful handlers default to snapshot-only
all-mergeable handler set is delta/merge eligible
mixed mergeable and snapshot-only handler set selects sequential fallback
unsupported handler set fails closed with a first blocking reason
handler output descriptors expose classification metadata
```

Exit criteria:

```text
runtime selection cannot accidentally route an unclassified stateful handler
through ordered concurrent delta compute
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerOutputContractTests"

result:
  11 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Commit:

```text
Implement handler delta classification
```

## Slice 2: Per-Batch Handler Delta Contract

Status: complete.

Implementation:

```text
add value-like handler delta identity and payload contracts
include handler identity, handler contract version, provider sequence,
  optional durable batch id, event count, source count, payload value count,
  input checksum, and deterministic delta id
add validation for missing identity, duplicate fields, negative counters,
  invalid provider sequence, and ambiguous source output
add in-memory serialization/versioning roundtrip contract
add version mismatch and deserialization diagnostics
keep delta granularity per accepted RadarEventBatch, not per event
```

Tests:

```text
valid delta carries deterministic identity and batch metadata
invalid identity, counters, or version are rejected
serialization roundtrip preserves idempotency key and payload values
unknown required version fails closed with diagnostic
retrying delta compute for the same handler and batch produces equivalent
  identity and payload
```

Exit criteria:

```text
handler deltas are replayable, versioned, and independent of process-local
object identity before merge behavior is implemented
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaContractTests"

result:
  6 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Commit:

```text
Add handler delta contract
```

## Slice 3: Deterministic Ordered Merge Coordinator

Status: complete.

Implementation:

```text
add an ordered handler delta coordinator
accept completed handler deltas in any completion order
merge only the next provider sequence
hold later deltas behind missing, failed, invalid, or poison earlier deltas
detect duplicate delta id application and make it idempotent
surface merge state, blocked sequence, applied sequence, warnings, and first
  blocking reason
keep merge state separate from batch-local delta compute state
```

Tests:

```text
out-of-order completed deltas merge in provider sequence
later completed delta waits behind missing earlier sequence
duplicate delta application does not double-count output
invalid earlier delta blocks later merge and reports first blocking reason
merged output matches sequential fallback for the same input batches
merge state can be summarized without exposing mutable internals
```

Exit criteria:

```text
worker completion order cannot become externally visible handler output
order, and duplicate replay cannot double-count merged handler state
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests"

result:
  6 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Commit:

```text
Add ordered handler delta merge coordinator
```

## Slice 4: MVP Runtime Integration And Fallback Policy

Status: complete.

Implementation:

```text
wire handler classification into RadarProcessingMvpRuntimePlan
select ordered delta/merge only when all configured stateful handlers are
  mergeable
preserve explicit sequential fallback for snapshot-only handler sets
add fail-closed behavior for unsupported handler sets
compose mergeable handler delta compute with the accepted ordered concurrent
  runtime/archive path without changing handler-free behavior
preserve release health, retained pressure, processing completeness, provider
  sequence, checksums, and first blocking reason diagnostics
```

Tests:

```text
all-mergeable MVP plan uses ordered delta/merge provenance
snapshot-only MVP plan keeps sequential fallback provenance
unsupported handler set fails closed with diagnostic
handler-free MVP plan keeps existing ordered concurrent provenance
merged runtime output matches sequential fallback output
validation failure or cancellation preserves visible diagnostics
```

Exit criteria:

```text
the MVP runtime can use the fast path only for explicitly safe handler sets
and preserves the milestone 024 fallback posture otherwise
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingMvpRuntimePlanTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests"

result:
  7 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

Commit:

```text
Wire handler delta merge into MVP runtime
```

## Slice 5: BFF Compatibility And Diagnostics

Status: planned.

Implementation:

```text
project merged handler output through existing milestone 024 read models
add handler posture/provenance metadata without breaking existing BFF query
  semantics
add diagnostics for sequential fallback, ordered delta/merge, unsupported
  handler mix, delta validation failure, version mismatch, duplicate replay,
  merge block, and merge failure
ensure readiness status and first blocking reason remain visible
avoid exposing queue, worker, or mutable merge internals as BFF contracts
```

Tests:

```text
BFF run detail exposes merged handler output through existing read models
handler catalog exposes mergeable and snapshot-only posture metadata
diagnostics identify ordered delta/merge versus sequential fallback
blocked or unsupported handler delta work appears as readiness diagnostics
empty, failed, blocked, and successful runs keep stable responses
existing milestone 024 BFF tests remain compatible
```

Exit criteria:

```text
a future frontend can consume merged handler output without learning the
runtime delta/merge internals or changing established query semantics
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingBffReadModelStoreTests|FullyQualifiedName~RadarProcessingRunReadModelTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Commit:

```text
Expose handler delta diagnostics through BFF models
```

## Slice 6: Handler-Heavy Performance Gate

Status: planned.

Implementation:

```text
add a deterministic handler-heavy workload that makes handler compute and
  merge cost visible
compare ordered delta/merge against sequential fallback for correctness and
  elapsed time
capture allocation shape and retained pressure
record provider sequence, checksums, output parity, release health, worker
  health, and first blocking reason
include enough volume to avoid archive producer dominance hiding handler cost
```

Tests and verification:

```text
focused handler delta/merge correctness suites pass
Release build succeeds with 0 warnings and 0 errors
focused milestone 025 Release gate passes
handler-heavy large-volume gate captures output parity and performance
full Release test project is run if affordable before decision trace
```

Exit criteria:

```text
RadarPulse has scoped evidence that mergeable stateful custom analytics can
use ordered delta/merge safely and materially faster than sequential fallback
for handler-heavy deterministic archive-shaped workloads
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerDeltaClassificationTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingMvpHandlerDeltaRuntimeTests|FullyQualifiedName~RadarProcessingHandlerDeltaBffCompatibilityTests|FullyQualifiedName~RadarProcessingHandlerDeltaPerformanceGateTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
```

Commit:

```text
Add handler delta performance gate
```

## Slice 7: Pre-Decision Trace Review Point

Status: planned.

Implementation:

```text
capture gate evidence in a milestone 025 gate document
summarize implementation results, accepted warnings, and any residual risks
update handoff and project progress with current status
stop before writing the decision trace
```

Exit criteria:

```text
the milestone is ready for a decision trace discussion, but the decision is
not yet recorded
```

Commit:

```text
Capture milestone 025 gate evidence
```

## Stop Point

The planned stop point is before
`025-handler-delta-merge-contract-for-fast-custom-analytics-decision-trace.md`
is written.

At that point, the expected handoff should include:

```text
implemented slices
focused and Release gate results
handler-heavy performance evidence
accepted warnings proposed for decision trace
open questions requiring decision before closeout
```

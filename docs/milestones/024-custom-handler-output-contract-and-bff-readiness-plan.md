# Milestone 024: Custom Handler Output Contract And BFF Readiness Implementation Plan

Status: in progress.

This plan implements the milestone 024 architecture defined in
`024-custom-handler-output-contract-and-bff-readiness.md`.

The plan is scoped to MVP-facing custom handler output and
backend-for-frontend readiness. It does not implement a persistent durable
adapter, true live network ingestion, production deployment/runbooks, or the
frontend application.

## Goal

Implement the first stable product-facing output surface over the accepted
runtime/archive foundations.

The milestone must prove these concrete outcomes:

```text
custom handler outputs are exported through stable contracts
handler descriptor metadata is discoverable by a future frontend
stateful handler execution has an explicit safe posture
processing run, batch, source, handler output, diagnostic, and readiness read
  models exist
BFF-facing consumers can retrieve latest run, batch list, batch detail,
  source summaries, handler outputs, and runtime diagnostics
queued-owned fail-closed behavior, processing completeness, provider
  sequence, retained pressure, release health, and first blocking reason
  remain visible
persistent adapter readiness remains deferred and does not block MVP output
```

Scope note:

```text
milestone 024 is allowed to choose a conservative sequential fallback for
stateful custom handlers. Ordered concurrent handler delta/merge is only in
scope if the milestone explicitly implements and gates a safe merge contract.
```

## Slice 1: Handler Output Contract Audit

Status: complete.

Implementation:

```text
audit existing handler descriptor, handler state, and snapshot types
define the handler output fields that are stable enough for MVP use
choose the accepted handler-state posture:
  snapshot/export with sequential fallback, restricted mergeable handlers, or
  a narrower explicit subset
record the decision before adding BFF contracts
```

Tests:

```text
existing handler descriptors expose stable names and field metadata
invalid or duplicate handler output fields are rejected
stateful handlers are not routed through unsafe ordered concurrent delta
compute without an accepted merge contract
```

Exit criteria:

```text
the milestone has a clear handler output and handler-state safety boundary
before BFF read models depend on it
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingHandlerOutputContractTests"

result:
  5 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 2: Processing Output Read Models

Status: complete.

Implementation:

```text
add processing run summary read model
add batch summary and batch detail read models
add source summary read model
add handler catalog and handler output read models
map processing completeness, checksums, timestamp bounds, provider sequence,
  status, and warnings into stable DTOs
keep internal runtime objects out of the BFF contract
```

Tests:

```text
read models preserve provider sequence order
handler catalog fields match descriptor metadata
source output values match committed handler snapshots
batch details expose status, event counts, payload bytes, checksums, and
  diagnostic warnings
DTO construction rejects invalid or ambiguous shapes
```

Exit criteria:

```text
processing results can be inspected without reaching into processing core,
queue, or durable session internals
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingRunReadModelTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 3: BFF Application Surface

Status: complete.

Implementation:

```text
add a BFF-oriented query service or lightweight presentation surface
support latest run, run detail, batch list, batch detail, source summary,
  handler output, and diagnostics queries
include readiness summary fields from the accepted runtime/durable posture
keep the surface deterministic and archive-shaped for MVP evidence
avoid taking a dependency on a persistent adapter
```

Tests:

```text
latest run returns the most recent completed MVP processing result
batch list and batch detail preserve provider sequence and status
source summary returns deterministic handler output values
diagnostics include processing completeness, release health, retained
  pressure, and first blocking reason
empty, failed, blocked, and successful runs have stable responses
```

Exit criteria:

```text
a future frontend has a stable backend contract for the first MVP processing
results and diagnostics
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingBffReadModelStoreTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 4: Handler Execution Posture Gate

Status: complete.

Implementation:

```text
wire the accepted handler-state posture into the MVP runtime path
if sequential fallback is chosen, make the fallback explicit in result
  provenance and tests
if restricted mergeable handlers are chosen, implement delta/merge and gate
  only those handlers through ordered concurrent compute
ensure unsupported handler shapes fail closed with a clear diagnostic
```

Tests:

```text
stateful custom handler output is deterministic across repeated runs
unsupported handler concurrency returns an explicit unsupported diagnostic
sequential fallback does not claim ordered concurrent handler readiness
handler output remains stable after validation failure or cancellation
```

Exit criteria:

```text
custom handler output is useful for MVP without weakening ordered commit or
state mutation safety
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingMvpRuntimePlanTests"

result:
  3 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 5: Archive-Shaped MVP Gate

Status: planned.

Implementation:

```text
run a deterministic archive-shaped workload through the MVP output surface
capture processing output, handler output, BFF diagnostics, and readiness
record retained pressure, release health, processing completeness, provider
  sequence, and first blocking reason
```

Tests and verification:

```text
focused handler-output and BFF suite passes
Release build succeeds with 0 warnings and 0 errors
focused Release gate suite passes
full test project result is captured, with known residual caveats called out
  separately if they remain unrelated to milestone 024 correctness
```

Exit criteria:

```text
RadarPulse can produce and serve MVP processing results for a future frontend
over deterministic archive-shaped input
```

## Slice 6: Decision Trace And Closeout

Status: planned.

Documentation:

```text
write gate evidence
write decision trace
write closeout
update project-progress
update handoff
record whether persistent durable adapter remains next, or whether the next
  MVP slice should build more product-facing output
```

Closeout question:

```text
is RadarPulse ready to expose MVP processing results through stable custom
handler output and BFF read models for a future frontend?
```

Expected closeout answer:

```text
accepted with scoped warnings for deterministic archive-shaped MVP workloads
and the accepted handler-state posture; persistent adapter, true live
ingestion, production operations, and frontend implementation remain future
work
```

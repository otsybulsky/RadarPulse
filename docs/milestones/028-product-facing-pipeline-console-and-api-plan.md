# Milestone 028: Product-Facing Pipeline Console And API Implementation Plan

Status: planned.

This plan implements the milestone 028 architecture defined in
`028-product-facing-pipeline-console-and-api.md`.

The plan is scoped to a deterministic archive-shaped product-facing
pipeline console/API surface. It composes the milestone 027 production
pipeline, milestone 024 BFF read models, milestone 025 handler output
posture, and milestone 027 rollback/fallback vocabulary. It does not
introduce a frontend SPA, true live network ingestion, external
broker/cloud queue/database persistence, deployment automation, or
exactly-once production delivery claims.

## Goal

Turn RadarPulse's accepted backend pipeline into a usable product-facing
surface with stable DTOs, product run/read/control workflows, console output,
API-facing contract shape, documentation, and focused Release gates.

The milestone must prove these concrete outcomes:

```text
product DTOs map accepted production pipeline output into stable user-facing
  run, summary, diagnostics, batch, source, handler, configuration, and
  capacity shapes
a product pipeline service can run deterministic synthetic/demo batches and
  archive-file derived RadarEventBatch input through the accepted pipeline
the product read surface can query latest run, run detail, batches, sources,
  handler output, diagnostics, and capacity evidence
the product control surface exposes stop-accepting, drain-accepted,
  cancel-open/release, and unsafe fallback rejection using milestone 027
  rollback/fallback semantics
the existing console presentation layer exposes product-oriented pipeline
  commands distinct from benchmark commands
an API-facing contract or thin host can reuse the same product service
  without reimplementing processing
documentation and focused Release gates prove the end-to-end product
  workflow before decision trace
```

Scope note:

```text
milestone 028 validates product-facing completion over deterministic
archive-shaped workloads. It does not claim frontend SPA completion, true live
network ingestion, external durable adapter certification, production
deployment readiness, cross-machine throughput, or exactly-once delivery.
```

## Slice 1: Product DTO And Mapping Contract

Status: complete.

Implementation:

```text
add product-level DTOs for run summary, run detail, operator summary,
  configuration provenance, capacity evidence, diagnostics, batches, sources,
  handler outputs, and warnings
add mapping from RadarProcessingProductionPipelineRunResult,
  RadarProcessingRunReadModel, RadarProcessingProductionPipelineOperatorSummary,
  and RadarProcessingProductionPipelineCapacityEvidence into product DTOs
preserve product-friendly field names while keeping first blocker, fallback
  recommendation, handler posture, retained pressure, processing
  completeness, and configuration provenance visible
keep DTOs immutable and independent from mutable internal collections
```

Tests:

```text
completed production pipeline result maps to product run detail
blocked production pipeline result maps first blocking reason and fallback
configuration provenance maps profile/default/override source values
handler output and diagnostics map into stable product shapes
capacity evidence maps completion counters and readiness posture
```

Exit criteria:

```text
product-facing DTOs are stable before service, console, or API code depends
on them
```

Commit:

```text
Add product pipeline DTO mapping
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineDtoTests"

result:
  3 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore --no-build
  --filter "FullyQualifiedName~RadarPulseProductPipelineDtoTests"

result:
  3 passed, 0 failed, 0 skipped
```

## Slice 2: Product Pipeline Run Service

Status: complete.

Implementation:

```text
add product pipeline run requests for deterministic synthetic/demo input and
  archive-file input
add a product pipeline service that composes the accepted production pipeline
  runner and stores product run records in memory
for synthetic/demo input, build deterministic RadarEventBatch data suitable
  for focused gates and examples
for archive-file input, use the existing NEXRAD archive RadarEventBatch
  publisher with an owned-snapshot capturing publisher
capture product run detail, read model store, capacity evidence, run state,
  warnings, and source input identity
fail closed for invalid input, incompatible source universe, unsupported
  handler posture, or invalid production pipeline configuration
```

Tests:

```text
synthetic product run completes and stores product run detail
archive-file request validates input shape without bypassing archive
  publisher contracts
invalid run request fails before publishing product history
mergeable handler mode is visible through product run detail
snapshot-only fallback warning remains visible
blocked handler posture remains visible without read model publication
```

Exit criteria:

```text
the product surface has one service-level run path over the accepted
production pipeline
```

Commit:

```text
Add product pipeline run service
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineServiceTests"

result:
  6 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineServiceTests"

result:
  6 passed, 0 failed, 0 skipped
```

## Slice 3: Product Read Query Surface

Status: complete.

Implementation:

```text
add latest-run, list-runs, run-detail, batch-list, batch-detail,
  source-list, source-detail, handler-output, diagnostics, and capacity
  evidence query methods over stored product runs
return not-found responses without throwing for normal query misses
keep query results detached from internal production read-model objects
include product-level input summary and scoped warnings in run detail
```

Tests:

```text
latest run follows publication order
run list returns stable summaries
batch and source detail queries return expected product records
handler output query returns field value and not-found for missing fields
diagnostics and capacity evidence stay available after run completion
query misses return not-found product responses
```

Exit criteria:

```text
product users can inspect completed and blocked runs without lower-level BFF
store knowledge
```

Commit:

```text
Add product pipeline read queries
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineQueryTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineQueryTests"

result:
  4 passed, 0 failed, 0 skipped
```

## Slice 4: Product Operator Control Surface

Status: complete.

Implementation:

```text
add product control request/result DTOs for stop-accepting, drain-accepted,
  cancel-open/release, and reject-unsafe-fallback
compose RadarProcessingProductionPipelineControlCoordinator where a durable
  store path is supplied
map control results to product operator summary and fallback recommendation
preserve explicit blocked, failed, claimed, poison, canceled, incompatible,
  and unsafe fallback posture
do not silently recover claimed work or switch providers/adapters
```

Tests:

```text
stop-accepting control reports preserved durable state
drain-accepted control reports drained processing count
cancel-open/release control reports canceled and released counts
unsafe fallback control is rejected with product-facing reason
control request without required durable store path fails closed
```

Exit criteria:

```text
product controls expose milestone 027 rollback/fallback posture without
hidden behavior
```

Commit:

```text
Add product pipeline control surface
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineControlTests"

result:
  5 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineControlTests"

result:
  5 passed, 0 failed, 0 skipped
```

## Slice 5: Console Product Workflow

Status: complete.

Implementation:

```text
add product-oriented console commands under the existing CLI presentation
  layer, separate from archive/processing benchmark commands
support a deterministic product pipeline demo run for focused gates
support archive-file product run when a NEXRAD archive file is provided
print run id, input kind, readiness, run state, first blocker, fallback
  recommendation, accepted/processed/committed counts, handler posture,
  capacity evidence, warnings, and read-model availability
add product status/query command output for stored or just-completed runs
preserve existing archive and benchmark command output
```

Tests:

```text
CLI usage lists product pipeline commands
product demo run prints completed run summary and capacity evidence
product demo run with mergeable handler prints mergeable handler posture
invalid product command returns product-facing validation error
existing benchmark command tests keep passing
```

Exit criteria:

```text
the repository has a user-facing product workflow that can be run from the
existing console application
```

Commit:

```text
Add product pipeline console workflow
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineCliTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineCliTests"

result:
  4 passed, 0 failed, 0 skipped
```

## Slice 6: API-Facing Contract Or Thin Host

Status: complete.

Implementation:

```text
add API-facing request/response contract helpers over the product pipeline
  service
if a thin HTTP host is added, keep it limited to route mapping and service
  invocation:
    run product pipeline demo/archive request
    list runs
    latest run
    run detail
    batches
    sources
    diagnostics
    controls
do not put processing, durable recovery, or BFF read-model logic in the host
```

Tests:

```text
API contract maps run command to product service result
API contract maps list/latest/detail queries
API contract maps not-found query result
API contract maps control result and unsafe fallback rejection
solution build includes any added host project
```

Exit criteria:

```text
API consumers have a stable product contract over the same service used by
the console surface
```

Commit:

```text
Add product pipeline API contract
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

result:
  4 passed, 0 failed, 0 skipped
```

## Slice 7: Documentation, Gate Evidence, And Handoff

Status: complete.

Implementation:

```text
add product-facing usage documentation or milestone gate notes for console
  and API workflows
capture focused Release gate over product DTOs, service, queries, controls,
  console workflow, and API contract
run Release build
record gate evidence in
  028-product-facing-pipeline-console-and-api-gate.md
update handoff with completed slices and latest verification
stop before decision trace for review
```

Tests:

```text
focused milestone 028 Release suite passes
Release build succeeds with zero warnings
documentation examples align with implemented commands/contracts
```

Exit criteria:

```text
all implementation evidence needed for pre-decision review is captured, and
no decision trace has been written yet
```

Commit:

```text
Capture product-facing pipeline gate
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineDtoTests|FullyQualifiedName~RadarPulseProductPipelineServiceTests|FullyQualifiedName~RadarPulseProductPipelineQueryTests|FullyQualifiedName~RadarPulseProductPipelineControlTests|FullyQualifiedName~RadarPulseProductPipelineCliTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

result:
  26 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Gate Command Shape

Expected focused Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPipelineDtoTests|FullyQualifiedName~RadarPulseProductPipelineServiceTests|FullyQualifiedName~RadarPulseProductPipelineQueryTests|FullyQualifiedName~RadarPulseProductPipelineControlTests|FullyQualifiedName~RadarPulseProductPipelineCliTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full Release test project can be run if implementation touches shared runtime
behavior beyond the new product orchestration and presentation surface.

## Stop Point

Stop before writing decision trace.

The milestone should pause for review when:

```text
implementation slices are complete
focused Release gate is captured
Release build is clean
gate evidence is written
handoff is updated
```

The next step after review will be a separate decision trace and closeout
request.

## Completion State

The milestone is stopped before decision trace as requested.

The milestone currently has:

```text
028-product-facing-pipeline-console-and-api.md
028-product-facing-pipeline-console-and-api-plan.md
028-product-facing-pipeline-console-and-api-gate.md
028-product-facing-pipeline-console-and-api-decision-trace.md
implementation slices complete
handoff updated
decision trace written
closeout not written
```

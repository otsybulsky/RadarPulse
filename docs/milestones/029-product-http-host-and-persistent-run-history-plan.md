# Milestone 029: Product HTTP Host And Persistent Run History Implementation Plan

Status: planned.

This plan implements the milestone 029 architecture defined in
`029-product-http-host-and-persistent-run-history.md`.

The plan is scoped to a thin local HTTP delivery surface and deterministic
local file-backed product run history over the accepted milestone 028 product
pipeline service. It composes the milestone 028 product DTOs, product service,
query/control surface, and API-facing contract. It does not introduce a
frontend SPA, true live network ingestion, external broker/cloud
queue/database persistence, deployment automation, or exactly-once production
delivery claims.

## Goal

Expose RadarPulse's accepted product pipeline contract through a local HTTP
host and persist product run history across process restart.

The milestone must prove these concrete outcomes:

```text
product run history has an injectable store contract instead of service-local
  in-memory dictionaries only
existing in-memory product history behavior remains available behind that
  contract
a deterministic local file-backed product history store persists product run
  details and reloads them after service/store recreation
history readiness exposes storage kind, storage path, schema version, loaded
  record count, invalid record count, and first blocking reason
RadarPulseProductPipelineService can use persistent history without changing
  accepted run/read/control semantics
an HTTP host maps run, read, diagnostics, capacity, handler output, readiness,
  and control routes to the same product service/API contract
HTTP responses preserve product API status codes and product response bodies
documentation and focused Release gates prove hosted and restart-history
  behavior before decision trace
```

Scope note:

```text
milestone 029 validates a local product HTTP host and local product run
history persistence over deterministic archive-shaped workloads. It does not
claim frontend SPA completion, true live network ingestion, external durable
adapter certification, production deployment readiness, public internet
exposure, auth/TLS/CORS hardening, cross-machine throughput, or exactly-once
delivery.
```

## Slice 1: Product Run History Store Contract

Status: planned.

Implementation:

```text
add product history store contract types in the product application layer:
  IRadarPulseProductRunHistoryStore
  RadarPulseProductRunHistoryReadiness
  RadarPulseProductRunHistoryStorageKind
add in-memory implementation that preserves the existing milestone 028
  service-local behavior
adapt RadarPulseProductPipelineService to receive an optional history store
  while keeping the current default constructor behavior
move ListRuns, TryGetRun, TryGetLatestRun, ListBatches, ListSources,
  TryGetDiagnostics, TryGetCapacityEvidence, and Store behavior through the
  history store contract
expose product history readiness through the service and API contract
```

Tests:

```text
default product service still stores and queries runs in memory
injected in-memory history store preserves latest-run ordering
history readiness reports in-memory healthy posture
API contract exposes product history readiness
existing milestone 028 service/query/API tests remain green
```

Exit criteria:

```text
product run history is abstracted without adding persistence yet and without
changing accepted product run/read/control behavior
```

Commit:

```text
Add product run history store contract
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductPipelineServiceTests|FullyQualifiedName~RadarPulseProductPipelineQueryTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

## Slice 2: File-Backed Product Run History Store

Status: planned.

Implementation:

```text
add RadarPulseProductFileRunHistoryStore in infrastructure
persist a versioned JSON history file containing product run details in
  deterministic publication order
load persisted history during store construction
preserve latest-run ordering after reload
write history through a temporary file and replace operation for deterministic
  local gates
fail closed for unsupported schema version, corrupt JSON, duplicate run ids
  with conflicting records, invalid run ids, and unsafe file/directory paths
expose load summary and first blocking reason through history readiness
```

Tests:

```text
file-backed store saves and reloads product run detail
latest-run ordering survives store recreation
duplicate same-record replay is idempotent
duplicate conflicting run identity fails closed
corrupt JSON and unsupported schema report blocked readiness
invalid or unsafe storage path reports blocked readiness
```

Exit criteria:

```text
deterministic local file-backed product run history can survive process/store
recreation and report blocked load posture explicitly
```

Commit:

```text
Add file product run history store
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductFileRunHistoryStoreTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

## Slice 3: Persistent History Service Integration

Status: planned.

Implementation:

```text
wire RadarPulseProductPipelineService to the file-backed history store through
  constructor injection and factory helpers
ensure RunSyntheticAsync and RunArchiveFileAsync persist product run details
  before reporting durable product history success
ensure persisted runs can be listed and queried after service recreation
ensure persistence failures surface as product-facing invalid operation
  failures instead of silently claiming stored history
extend RadarPulseProductPipelineApiContract with history readiness query
  mapping
```

Tests:

```text
product service with file history persists completed demo run
new service over the same history file can list latest and detail
diagnostics, handler output, and capacity evidence remain queryable after
  service recreation
persistence failure fails closed and does not claim healthy history
API history readiness maps blocked and healthy store posture
```

Exit criteria:

```text
the accepted product service supports persistent product history without
changing run/read/control semantics or lower-level runtime behavior
```

Commit:

```text
Persist product pipeline run history
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductPersistentHistoryServiceTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

## Slice 4: Product HTTP Host Project And Route Mapping

Status: planned.

Implementation:

```text
add a thin HTTP presentation project to the solution
add host options for product history path and local demo defaults
add application/service factory that creates RadarPulseProductPipelineService
  with a local file-backed history store when a history path is configured
add route mapping for:
  POST /product/pipeline/runs/demo
  POST /product/pipeline/runs/archive
  GET /product/pipeline/runs
  GET /product/pipeline/runs/latest
  GET /product/pipeline/runs/{runId}
  GET /product/pipeline/runs/{runId}/batches
  GET /product/pipeline/runs/{runId}/batches/{providerSequence}
  GET /product/pipeline/runs/{runId}/sources
  GET /product/pipeline/runs/{runId}/sources/{sourceId}
  GET /product/pipeline/runs/{runId}/handlers/{sourceId}/{fieldName}
  GET /product/pipeline/runs/{runId}/diagnostics
  GET /product/pipeline/runs/{runId}/capacity
  GET /product/pipeline/host/readiness
keep route handlers thin and route all product work through the existing
  product API contract/service
```

Tests:

```text
route mapper exposes expected product endpoints
demo route maps to product service and returns Created product response
list/latest/detail routes map success and not-found responses
batch/source/handler/diagnostics/capacity routes preserve product status
host readiness route reports history posture
solution build includes the HTTP host project
```

Exit criteria:

```text
the repository contains a runnable local product HTTP host that reuses the
accepted product service instead of duplicating processing behavior
```

Commit:

```text
Add product pipeline HTTP host
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

## Slice 5: HTTP Control And Failure Posture

Status: planned.

Implementation:

```text
add HTTP route mapping for:
  POST /product/pipeline/controls/stop-accepting
  POST /product/pipeline/controls/drain-accepted
  POST /product/pipeline/controls/cancel-open-release
  POST /product/pipeline/controls/reject-unsafe-fallback
ensure HTTP routes preserve product control summaries and unsafe fallback
  rejection
ensure validation, not-found, blocked history, corrupt history, and bad
  request paths map to stable product API responses
ensure hosted startup/readiness does not hide invalid persistent history
```

Tests:

```text
control routes return product control summaries
unsafe fallback route returns rejected product posture
bad request route returns 400 product response
not-found route returns 404 product response
blocked history readiness stays visible through HTTP readiness response
```

Exit criteria:

```text
HTTP clients can run, inspect, and control the product pipeline while seeing
explicit failure and fallback posture
```

Commit:

```text
Add product HTTP control posture
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductHttpHostTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

## Slice 6: Documentation, Gate Evidence, And Handoff

Status: planned.

Implementation:

```text
add hosted product usage notes or milestone gate notes for local HTTP host
  routes and persistent history configuration
capture focused Release gate over product history, service persistence, HTTP
  host, HTTP controls, and existing product API contract tests
run Release build
record gate evidence in
  029-product-http-host-and-persistent-run-history-gate.md
update handoff with completed slices and latest verification
stop before decision trace for review
```

Tests:

```text
focused milestone 029 Release suite passes
Release build succeeds with zero warnings
documentation examples align with implemented routes/contracts
```

Exit criteria:

```text
all implementation evidence needed for pre-decision review is captured, and
no decision trace has been written yet
```

Commit:

```text
Capture product HTTP host gate
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductFileRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductPersistentHistoryServiceTests|FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

## Gate Command Shape

Expected focused Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductFileRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductPersistentHistoryServiceTests|FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full Release test project can be run if implementation touches shared runtime
behavior beyond the product history store, product service injection, and HTTP
presentation surface.

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

The milestone currently has:

```text
029-product-http-host-and-persistent-run-history.md
029-product-http-host-and-persistent-run-history-plan.md
implementation slices planned
decision trace not written
closeout not written
```

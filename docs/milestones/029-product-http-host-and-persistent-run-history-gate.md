# Milestone 029: Product HTTP Host And Persistent Run History Gate

## Gate Status

Focused Release gate passed.

Milestone 029 is complete through implementation slices and gate capture. This
document records pre-decision evidence only. Decision trace and closeout have
not been written.

## Gate Question

```text
Is RadarPulse ready to expose the accepted product pipeline contract through
a thin local HTTP host and preserve product run history through deterministic
local file-backed persistence for archive-shaped workflows?
```

Pre-decision gate answer:

```text
yes with scoped warnings for product HTTP host and persistent run history over
deterministic archive-shaped workloads
```

The scoped warnings remain:

```text
the HTTP host is a local hosted delivery adapter, not production deployment
  automation
product run history persistence is deterministic local file-backed storage,
  not external broker, cloud queue, or database durability
the hosted workflows remain deterministic demo/archive-shaped product
  workflows, not true live network ingestion
auth, authorization, TLS termination, CORS hardening, public internet
  exposure, autoscaling, alert routing, operator runbooks, cross-machine
  throughput certification, and exactly-once delivery are not claimed
frontend SPA or rich radar visualization is not implemented
accepted milestone 020-028 runtime, durable, handler, BFF, production
  pipeline, and product contract decisions are not reopened
```

## Implemented Evidence

Product run history contract:

```text
IRadarPulseProductRunHistoryStore
RadarPulseProductRunHistoryReadiness
RadarPulseProductRunHistoryStorageKind
RadarPulseProductInMemoryRunHistoryStore
RadarPulseProductPipelineService history-store injection
RadarPulseProductPipelineApiContract history readiness response
```

File-backed history:

```text
RadarPulseProductFileRunHistoryStore
versioned JSON product history file
deterministic reload after store/service recreation
latest-run ordering preserved after reload
idempotent same-record duplicate replay
conflicting duplicate run identity rejected
blocked readiness for corrupt JSON, unsupported schema, and directory path
```

Persistent service integration:

```text
RadarPulseProductPipelineService.CreateWithFileHistory
completed product run details persisted before successful history claim
run list, latest, detail, diagnostics, handler output, and capacity evidence
  queryable after service recreation
blocked history store fails product run before claiming stored history
API history readiness maps healthy and blocked persistent stores
```

HTTP host:

```text
src/Presentation.Http/RadarPulse.Http.csproj
local ASP.NET Core host entry point
RadarPulseProductHttpOptions
RadarPulseProductHttpServiceCollectionExtensions
RadarPulseProductHttpEndpoints
solution build includes RadarPulse.Http
```

HTTP routes:

```text
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
POST /product/pipeline/controls/stop-accepting
POST /product/pipeline/controls/drain-accepted
POST /product/pipeline/controls/cancel-open-release
POST /product/pipeline/controls/reject-unsafe-fallback
```

HTTP posture:

```text
route handlers call RadarPulseProductPipelineApiContract/product service
directly
HTTP route layer does not implement processing, durable recovery, or BFF
  read-model logic
product API response status codes and response bodies are preserved
not-found, bad-request, blocked history, and unsafe fallback posture remain
product-visible
control route action is derived from the route and not trusted from request
  body action
```

## Verification

Focused milestone 029 Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductFileRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductPersistentHistoryServiceTests|FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

result:
  27 passed, 0 failed, 0 skipped
```

Release build:

```text
dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Stop Point

Stop before writing decision trace.

The next step is review of this implementation and gate evidence. Decision
trace and closeout should be written only after that review.

# Milestone 029 Decision Trace

Date: 2026-05-26

Decision: accept product HTTP host and persistent run history for
deterministic archive-shaped workloads with named scoped warnings.

This decision accepts milestone 029's product run history store contract,
in-memory compatibility adapter, deterministic local file-backed product
history store, persistent history service integration, thin local HTTP host,
HTTP run/read/diagnostics/capacity/handler/readiness routes, HTTP control and
failure posture, focused Release gate, Release build, gate evidence, and
handoff update on top of the milestone 028 product-facing pipeline
console/API surface.

The accepted scope is a local hosted product delivery adapter and local
product history persistence boundary. RadarPulse can now expose the accepted
product pipeline contract through HTTP routes, persist product run details to
deterministic local file-backed history, reload run history after service
recreation, query previous runs, diagnostics, handler output, and capacity
evidence after restart, and preserve product-visible failure and control
posture through HTTP responses.

The decision deliberately does not claim frontend SPA implementation, true
live network ingestion, external broker/cloud queue/database adapter
readiness, production deployment automation, public internet exposure,
authentication, authorization, TLS termination, CORS hardening, autoscaling,
alert routing, operator runbooks, cross-machine throughput certification, or
exactly-once production delivery. The HTTP host accepted here is a thin local
hosted delivery surface over the product service/API contract, not a
production deployment claim.

## Decision Matrix

```text
product HTTP host and persistent run history:
  accepted with scoped warnings

product run history store contract:
  accepted; product history is now behind IRadarPulseProductRunHistoryStore
  instead of service-local dictionaries only

in-memory history adapter:
  accepted; milestone 028-compatible default behavior is preserved through
  RadarPulseProductInMemoryRunHistoryStore

history readiness contract:
  accepted; storage kind, storage identity, schema version, loaded count,
  rejected count, first blocking reason, readiness, and warnings are visible

file-backed product history store:
  accepted with warning; deterministic local JSON persistence reloads product
  run details after store/service recreation but is not database or broker
  durability

history schema and load posture:
  accepted; unsupported schema, corrupt JSON, conflicting duplicate run ids,
  invalid records, and unsafe file/directory paths fail closed through
  blocked readiness

persistent service integration:
  accepted; RadarPulseProductPipelineService can use injected persistent
  history and CreateWithFileHistory without changing accepted product run,
  read, and control semantics

restart query behavior:
  accepted; list/latest/detail, diagnostics, handler output, and capacity
  evidence remain queryable after service recreation over the same history
  file

HTTP host project:
  accepted; RadarPulse.Http is a thin local ASP.NET Core host project in the
  solution

HTTP route mapping:
  accepted; run, list, latest, detail, batch, source, handler output,
  diagnostics, capacity, readiness, and control routes map to the product
  API contract/service

HTTP response posture:
  accepted; product API status codes and product response bodies are
  preserved through the HTTP route layer

HTTP control posture:
  accepted; stop-accepting, drain-accepted, cancel-open/release, and
  reject-unsafe-fallback remain product-visible, and route-derived control
  actions are not silently taken from request body action values

not-found and bad-request posture:
  accepted; normal misses and invalid requests return product-shaped HTTP
  responses rather than lower-level exceptions

blocked history posture:
  accepted; invalid persistent history remains visible through readiness and
  fails run creation before claiming healthy history

frontend SPA:
  not implemented; future product operator UI milestone required

true live network ingestion:
  not implemented; deterministic demo/archive-shaped workflows remain the
  accepted input contour

external broker/cloud queue/database adapter:
  not implemented and not planned for this project

deployment and operations:
  not implemented; deployment automation, public hosting, auth/TLS/CORS
  hardening, autoscaling, alert routing, runbooks, and production operator
  procedures remain future work

exactly-once production delivery:
  not claimed; future storage/downstream idempotency gates would be required

focused Release gate:
  accepted; 27 focused Release tests passed with no failures or skips

Release build:
  accepted; Release build succeeded with zero warnings and zero errors
```

## Decision Explanations

### Accept Product Run History Store Contract

Decision: accept `IRadarPulseProductRunHistoryStore` as the product history
boundary for milestone 029.

Why chosen: milestone 028 stored product run history inside
`RadarPulseProductPipelineService`. A hosted surface needs product history
that can be swapped from in-memory to persistent storage without changing the
accepted product run, read, control, or API contract semantics.

Alternatives: keep history service-local, add persistence directly inside the
service, or defer history abstraction until a frontend milestone.

Rejected because: service-local history cannot survive restart; direct
persistence inside the service would mix orchestration and storage concerns;
deferring the abstraction would make the HTTP host depend on volatile state.

Trade-offs/debt: the store contract is intentionally narrow: append/store and
query by the accepted product run shapes. Pagination, retention policy,
multi-writer coordination, and migrations can be added later if needed.

Review explanation: "Product history is now a product storage boundary, not
an implementation detail of the service."

### Preserve In-Memory History As Default

Decision: accept `RadarPulseProductInMemoryRunHistoryStore` as the default
history adapter.

Why chosen: milestone 028 behavior should remain compatible for tests, CLI
workflows, and direct service consumers that do not configure persistent
history.

Alternatives: make file history mandatory for all product service instances,
or keep two separate service types.

Rejected because: mandatory file history would add storage configuration to
existing deterministic product workflows; separate service types would split
the accepted product semantics.

Trade-offs/debt: in-memory history is still volatile. Hosted product usage
should configure file-backed history when restart visibility matters.

Review explanation: "Persistence is available without breaking existing
in-memory product workflows."

### Accept Deterministic Local File-Backed Product History

Decision: accept `RadarPulseProductFileRunHistoryStore` as the concrete
persistent product history adapter for milestone 029.

Why chosen: the project has explicitly scoped persistence to deterministic
local file-based adapters. A versioned JSON file gives a transparent,
testable, local persistence boundary for product run records without pulling
in external broker, cloud queue, or database certification.

Alternatives: use a database, reuse the durable envelope store directly for
product history, write one file per run, or keep only in-memory history.

Rejected because: database/broker persistence is outside the project plan;
the durable envelope store has different semantics from product run history;
one-file-per-run would add ordering and discovery concerns unnecessary for
the milestone gate; in-memory-only history cannot support restart queries.

Trade-offs/debt: the JSON history file is a local deterministic store, not a
production multi-process database. Retention, compaction, schema migration,
concurrent writer locking, and backup/restore policy remain future work if
the product needs them.

Review explanation: "Product runs now survive restart through local history
without pretending this is external durable infrastructure."

### Accept Fail-Closed History Readiness

Decision: accept explicit blocked readiness for invalid persistent history.

Why chosen: product history corruption, unsupported schema, invalid path, or
conflicting records must not be silently treated as healthy history. Hosted
clients need a direct readiness answer and first blocking reason.

Alternatives: ignore bad records, start with empty history after load
failure, or throw only during host startup.

Rejected because: silently dropping history would hide data loss; starting
empty would create false confidence; startup-only exceptions would make
normal readiness inspection harder and less product-shaped.

Trade-offs/debt: the current posture blocks the store at the first invalid
history condition. More granular partial quarantine can be considered later
if product retention requirements justify it.

Review explanation: "Bad history is visible as blocked history, not erased."

### Accept Persistent History Service Integration

Decision: accept product service integration through constructor injection
and `CreateWithFileHistory`.

Why chosen: the accepted product service should remain the orchestration
surface for console, API contract, and HTTP host. Injecting the history store
lets the same service preserve milestone 028 semantics while gaining restart
history capability.

Alternatives: make the HTTP host manage persistence around the service, or
create a separate persistent product service.

Rejected because: host-managed persistence would duplicate product storage
semantics outside the service; a separate persistent service would split the
product contract and risk divergent behavior.

Trade-offs/debt: persistence is synchronous and local for the milestone gate.
Background flushing, retention, and operational storage monitoring remain
future hardening concerns.

Review explanation: "The product service stays the single product
orchestrator; persistence is injected behind it."

### Accept Thin Local HTTP Host

Decision: accept `RadarPulse.Http` as the milestone 029 hosted delivery
adapter.

Why chosen: milestone 028 already accepted a transport-stable product API
contract. A thin ASP.NET Core host makes that contract reachable over HTTP
while keeping processing, durable recovery, and BFF read-model logic out of
route handlers.

Alternatives: extend the CLI only, build a frontend first, or put HTTP
routes directly over lower-level processing and BFF objects.

Rejected because: CLI-only workflows are not a hosted product surface; a
frontend needs an HTTP contract first; lower-level route handlers would
bypass the accepted product service/API boundary.

Trade-offs/debt: the host is local and intentionally thin. Deployment
configuration, service lifetime hardening, auth, TLS, CORS, OpenAPI, and
public hosting remain future work.

Review explanation: "HTTP is now a delivery adapter over the product
contract, not a second pipeline."

### Accept HTTP Route And Response Contract

Decision: accept the milestone 029 route groups for run, read, diagnostics,
capacity, handler output, readiness, and controls.

Why chosen: these routes map directly to accepted product workflows from
milestone 028 and expose the minimum hosted product surface needed for
clients and future UI work.

Alternatives: expose only run/detail routes, use benchmark-style route names,
or defer controls/readiness to a later host hardening milestone.

Rejected because: run-only hosting is not inspectable; benchmark vocabulary
would leak non-product concerns; controls and readiness are necessary for
honest blocked/failure posture.

Trade-offs/debt: route names are product-oriented but minimal. OpenAPI
metadata, pagination, filtering, auth-scoped route policy, and UI-specific
projection routes can be added later.

Review explanation: "The HTTP surface exposes the product workflows users
need without widening product semantics."

### Accept HTTP Control And Failure Posture

Decision: accept product-visible control, bad-request, not-found, blocked
history, and unsafe fallback posture through HTTP.

Why chosen: hosted clients must see the same fail-closed semantics accepted
by the product service and production pipeline controls. The route should not
hide invalid input, missing runs, blocked history, or unsafe fallback.

Alternatives: return generic HTTP errors, throw exceptions for normal misses,
or trust the request body action for control route behavior.

Rejected because: generic errors lose product diagnostics; exception-shaped
normal misses are poor product API behavior; trusting body action could make
route semantics ambiguous or unsafe.

Trade-offs/debt: the current control routes are deterministic local adapters.
Long-running control workflows and deployed operator procedures remain
future operational work.

Review explanation: "HTTP clients get product failure answers, not hidden
fallbacks or generic server errors."

### Keep Frontend, Live, External Adapters, Deployment, Security Hardening, And Exactly-Once Out Of Scope

Decision: keep frontend SPA, true live network ingestion, external
broker/cloud queue/database adapters, production deployment automation,
auth/TLS/CORS hardening, public hosting, autoscaling, alert routing,
runbooks, and exactly-once delivery outside milestone 029.

Why chosen: milestone 029 adds hosted local delivery and local persistent
product history. The excluded areas carry separate product, security,
operational, and reliability acceptance criteria.

Alternatives: combine HTTP host completion with frontend, live ingestion,
deployment, public security posture, and external durable storage readiness.

Rejected because: combining those would blur the local product host
acceptance boundary and create claims not supported by the focused gate.

Trade-offs/debt: RadarPulse now has a strong boundary for a next product UI
milestone, but production operations and live ingestion still need their own
gates.

Review explanation: "This milestone makes product HTTP and persistent
history real; production deployment and live radar remain separate."

## Included Surface

Included:

```text
IRadarPulseProductRunHistoryStore
RadarPulseProductRunHistoryReadiness
RadarPulseProductRunHistoryStorageKind
RadarPulseProductInMemoryRunHistoryStore
RadarPulseProductFileRunHistoryStore
RadarPulseProductPipelineService history-store injection
RadarPulseProductPipelineService.CreateWithFileHistory
RadarPulseProductPipelineApiContract history readiness query
RadarPulseProductPipelineApiContract batch/source/handler-output query
  methods needed by HTTP route mapping
RadarPulse.Http project
RadarPulseProductHttpOptions
RadarPulseProductHttpServiceCollectionExtensions
RadarPulseProductHttpEndpoints
HTTP run routes for demo and archive-shaped product runs
HTTP read routes for run list, latest, detail, batches, sources, handler
  output, diagnostics, and capacity evidence
HTTP readiness route for product host/history posture
HTTP control routes for stop-accepting, drain-accepted,
  cancel-open/release, and reject-unsafe-fallback
focused Release gate and Release build evidence
gate documentation and handoff updates
```

Excluded:

```text
frontend SPA or rich radar visualization UI
true live network ingestion
external broker/cloud queue/database adapter certification
database-backed product history
production deployment automation
public internet exposure
authentication and authorization
TLS termination
CORS hardening
autoscaling
alert routing
operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claim
changing milestone 020-028 accepted backend or product decisions
```

## Evidence

Primary source documents:

```text
docs/milestones/029-product-http-host-and-persistent-run-history.md
docs/milestones/029-product-http-host-and-persistent-run-history-plan.md
docs/milestones/029-product-http-host-and-persistent-run-history-gate.md
```

Input evidence from earlier milestones:

```text
milestone 024:
  custom handler output contracts, processing run read models, BFF query
  shapes, and diagnostics accepted for deterministic archive-shaped MVP
  workloads

milestone 026:
  deterministic local file-based durable adapter accepted as the persistent
  restart/recovery baseline for durable envelopes

milestone 027:
  production pipeline profile, archive-shaped production pipeline runner,
  BFF read-model publication, operator diagnostics, rollback/fallback
  controls, file durable recovery posture, and capacity evidence accepted

milestone 028:
  product DTOs, product pipeline service, product read/control workflows,
  product CLI commands, and transport-stable API-facing response contract
  accepted
```

Implementation evidence:

```text
RadarPulseProductRunHistoryModels:
  product history storage kind, readiness, and store contract

RadarPulseProductInMemoryRunHistoryStore:
  milestone 028-compatible in-memory history behind the new contract

RadarPulseProductFileRunHistoryStore:
  versioned deterministic local JSON product history persistence with
  reload, idempotent duplicate replay, conflicting duplicate rejection, and
  blocked readiness for invalid storage

RadarPulseProductPipelineService:
  injected product history store and CreateWithFileHistory factory

RadarPulseProductPipelineApiContract:
  history readiness plus batch, source, and handler-output query mapping for
  hosted route coverage

RadarPulse.Http:
  thin ASP.NET Core host project with service registration, host options,
  endpoint mapping, and Program entry point
```

Verification:

```text
slice 1 focused product history contract/service/API suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductPipelineServiceTests|FullyQualifiedName~RadarPulseProductPipelineQueryTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 17 passed, 0 failed, 0 skipped

slice 2 focused file history store suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductFileRunHistoryStoreTests"
  result: 7 passed, 0 failed, 0 skipped

slice 3 focused persistent service/API suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPersistentHistoryServiceTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 8 passed, 0 failed, 0 skipped

slice 4 focused HTTP host suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
  result: 4 passed, 0 failed, 0 skipped

slice 5 focused HTTP control/host suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductHttpHostTests"
  result: 9 passed, 0 failed, 0 skipped

focused milestone 029 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductFileRunHistoryStoreTests|FullyQualifiedName~RadarPulseProductPersistentHistoryServiceTests|FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 27 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

## Final Decision

Decision:

```text
accepted with scoped warnings for product HTTP host and persistent run
history over deterministic archive-shaped workloads
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to expose the accepted product
pipeline contract through a thin local HTTP host and preserve product run
history through deterministic local file-backed persistence for
archive-shaped workflows
```

Named warnings:

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

Recommended next milestone input:

```text
product operator UI over the HTTP host.

Use the accepted product DTOs, persistent run history, product HTTP routes,
readiness route, diagnostics, handler output, capacity evidence, and control
routes to build a product operator UI for running, inspecting, diagnosing,
and controlling deterministic archive-shaped RadarPulse workflows. Do not
expand the next milestone into true live network ingestion, external
broker/cloud queue/database adapter certification, deployment automation,
public production hosting, auth/TLS/CORS hardening, or exactly-once delivery
unless explicitly reprioritized.
```

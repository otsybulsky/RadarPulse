# Milestone 028 Decision Trace

Date: 2026-05-26

Decision: accept product-facing pipeline console/API completion for
deterministic archive-shaped workloads with named scoped warnings.

This decision accepts milestone 028's product DTO and mapping contract,
product pipeline run service, product read query surface, product operator
control surface, console product workflow, API-facing contract wrapper,
focused Release gate, Release build, gate evidence, and handoff update on top
of the milestone 027 production pipeline integration surface.

The accepted scope is a product-facing console/API contract over the accepted
production-shaped backend pipeline. RadarPulse can now run deterministic
synthetic/demo and archive-file shaped product workflows, expose stable
product DTOs, publish and query product run results, surface readiness,
diagnostics, handler output, capacity evidence, first blockers and fallback
recommendations, execute explicit product control actions, and present a
product-oriented CLI workflow distinct from benchmark commands.

The decision deliberately does not claim frontend SPA implementation,
production HTTP deployment, true live network ingestion, external
broker/cloud queue/database adapter readiness, deployment platform
automation, cross-machine throughput certification, or exactly-once
production delivery. The API-facing work accepted here is a transport-stable
contract wrapper over the product service, not a hosted production HTTP
service.

## Decision Matrix

```text
product-facing pipeline console/API completion:
  accepted with scoped warnings

product DTO contract:
  accepted; product run detail, run summary, configuration, operator
  summary, capacity evidence, diagnostics, batches, sources, handler
  output, handler contract, query result, API response, and control summary
  shapes are stable product-facing records

product mapping contract:
  accepted; production pipeline result, BFF read model, operator summary,
  configuration provenance, diagnostics, handler output, and capacity
  evidence map into product DTOs without exposing mutable lower-level
  internals

product pipeline run service:
  accepted; deterministic synthetic/demo input and archive-file derived
  RadarEventBatch input run through the accepted milestone 027 production
  pipeline

archive-file input:
  accepted with warning; archive-file input uses the existing NEXRAD archive
  RadarEventBatch publisher and owned-snapshot capture, but remains
  archive-shaped input, not live network ingestion

handler posture:
  accepted; handler-free, mergeable-delta, snapshot-sequential fallback, and
  unsupported-handler blocking postures remain product-visible

product read query surface:
  accepted; list runs, latest run, run detail, batch list/detail, source
  list/detail, handler output, diagnostics, and capacity evidence are
  queryable through product-level results

not-found query posture:
  accepted; normal query misses return product not-found responses instead
  of leaking lower-level store behavior

product control surface:
  accepted; stop-accepting, drain-accepted, cancel-open/release, and
  reject-unsafe-fallback use the milestone 027 rollback/fallback posture
  and map to product control summaries

console workflow:
  accepted; `radarpulse product pipeline demo` and
  `radarpulse product pipeline run-archive` provide product-oriented CLI
  workflows distinct from benchmark commands

API-facing contract:
  accepted; run, list, latest, detail, batch/source, diagnostics, capacity,
  and control calls map to HTTP-like status/body responses over the same
  product service

production HTTP host:
  not implemented; API-facing contract is accepted as a transport-stable
  service contract only

frontend SPA:
  not implemented; future product UI milestone required

true live network ingestion:
  not implemented; deterministic synthetic/demo and archive-file workloads
  remain the gate input

external broker/cloud queue/database adapter:
  not implemented and not planned for this project

deployment and operations:
  not implemented; deployment automation, autoscaling, alert routing,
  runbooks, and production operator procedures remain future work

exactly-once production delivery:
  not claimed; future storage and downstream idempotency gates are required

focused Release gate:
  accepted; 26 focused Release tests passed with no failures or skips

Release build:
  accepted; Release build succeeded with zero warnings and zero errors
```

## Decision Explanations

### Accept Product DTO And Mapping Contract

Decision: accept the product DTO and mapping contract as the product-facing
boundary for milestone 028.

Why chosen: milestone 027 exposed production pipeline and BFF read-model
objects, but product/API/console consumers need stable user-facing shapes
that do not depend on lower-level runtime, durable, or BFF internals. The
new DTOs preserve readiness, first blockers, fallback recommendations,
configuration provenance, diagnostics, handler output, and capacity evidence
in product vocabulary.

Alternatives: expose milestone 024/027 read models directly, or wait for a
frontend/HTTP host to define DTOs.

Rejected because: direct exposure would leak implementation-shaped objects
into product contracts; waiting for a host would force transport choices to
define the product model.

Trade-offs/debt: DTOs are intentionally conservative and mirror accepted
backend semantics. Rich visualization-specific DTOs can be added later
without changing this product run contract.

Review explanation: "The product contract wraps backend truth without making
backend internals the product API."

### Accept Product Pipeline Run Service

Decision: accept `RadarPulseProductPipelineService` as the product run and
history facade over the accepted production pipeline.

Why chosen: the product surface needed one application service that can run
deterministic product workloads, store product run details, and feed console
and API-facing adapters without duplicating pipeline logic.

Alternatives: keep all product runs in CLI code, add an HTTP host directly
over milestone 027 runner, or require archive files only.

Rejected because: CLI-only logic would not be reusable by API adapters;
direct HTTP hosting would add transport and deployment concerns too early;
archive-only input would make deterministic focused gates and examples
fragile.

Trade-offs/debt: run history is in-memory for this milestone. Persistent run
history and hosted lifecycle are future product delivery work.

Review explanation: "One product service owns product orchestration; CLI and
API adapters call it."

### Accept Synthetic/Demo And Archive-File Product Inputs

Decision: accept deterministic synthetic/demo input and archive-file input
as milestone 028 product run sources.

Why chosen: synthetic/demo input gives stable focused tests and a reliable
first product workflow; archive-file input connects the product workflow to
real NEXRAD archive-shaped data using the existing archive batch publisher.

Alternatives: require only real cached archive files, or pull true live feed
ingestion into the milestone.

Rejected because: real-file-only gates are less deterministic and depend on
local cache availability; true live feed ingestion has a separate failure
model and was explicitly outside milestone 027 and 028 scope.

Trade-offs/debt: archive-file product runs are valuable, but still
archive-shaped. Live ingestion needs its own milestone if selected later.

Review explanation: "Product workflows can run today without pretending they
are live radar ingestion."

### Accept Product Read Query Surface

Decision: accept product-level queries for latest run, run list, run detail,
batches, sources, handler output, diagnostics, and capacity evidence.

Why chosen: a user-facing product surface must support inspection after a
run completes or blocks. The query surface makes product history readable
without requiring callers to know `RadarProcessingBffReadModelStore` or
pipeline result internals.

Alternatives: rely on the BFF store directly, or defer read queries to a
frontend milestone.

Rejected because: the BFF store is an application read-model component, not
the final product contract; deferring queries would leave the product
surface able to run but not inspect results consistently.

Trade-offs/debt: the first query surface is in-process and service-level.
Persistent run history, pagination, auth, and HTTP transport remain future
work.

Review explanation: "A product run is inspectable through product queries,
not only through backend objects."

### Accept Product Operator Controls

Decision: accept product controls for stop-accepting, drain-accepted,
cancel-open/release, and reject-unsafe-fallback.

Why chosen: milestone 027 accepted rollback/fallback controls. Milestone 028
needed to expose those controls in product vocabulary while preserving the
same fail-closed and operator-visible posture.

Alternatives: hide controls until deployment/runbook work, or expose the
production pipeline control coordinator directly.

Rejected because: deferring controls would leave blocked product workflows
without user-facing recovery posture; direct coordinator exposure would leak
durable/recovery implementation details.

Trade-offs/debt: controls operate over the deterministic file-durable store
path where supplied. Full long-running hosted orchestration remains future
work.

Review explanation: "Product controls describe safe actions without hiding
fallback or rewriting durable state."

### Accept Console Product Workflow

Decision: accept product-oriented CLI commands as the first concrete
user-facing workflow.

Why chosen: the repository already has a .NET presentation layer and CLI
tests. Adding `product pipeline demo` and `product pipeline run-archive`
creates an immediate user-facing surface with stable output and no
deployment prerequisites.

Alternatives: build a frontend first, build an HTTP host first, or leave the
CLI benchmark-oriented.

Rejected because: frontend and HTTP hosting add additional delivery
concerns; benchmark commands are not product workflows and expose too much
benchmark vocabulary.

Trade-offs/debt: the CLI is a product command surface, not a rich operator
console or visualization UI. A frontend/operator console can now build on
the same DTO/service/API contract.

Review explanation: "The first product surface is runnable from the existing
CLI and speaks product vocabulary."

### Accept API-Facing Contract Wrapper

Decision: accept `RadarPulseProductPipelineApiContract` as the API-facing
contract layer for milestone 028.

Why chosen: future HTTP or UI work needs a stable API shape. The wrapper maps
run/query/control calls to HTTP-like status/body responses over the product
service without adding a hosted HTTP stack or duplicating processing logic.

Alternatives: add an ASP.NET host now, expose the service directly to future
hosts, or defer API contract until frontend work.

Rejected because: ASP.NET hosting would introduce routing, hosting,
deployment, auth, and lifecycle scope; exposing the service directly would
leave transport semantics unspecified; deferring the contract would make the
next milestone start from an unstable delivery boundary.

Trade-offs/debt: route definitions, auth, server lifetime, deployment
configuration, persistence, and operational hosting remain future work.

Review explanation: "API shape is accepted; production HTTP hosting is not
claimed."

### Keep Frontend, Live, External Adapters, Deployment, And Exactly-Once Out Of Scope

Decision: keep frontend SPA, true live network ingestion, external
broker/cloud queue/database adapters, production deployment automation, and
exactly-once delivery outside milestone 028.

Why chosen: milestone 028 completes a product-facing surface over the
accepted backend pipeline. Each excluded area carries independent acceptance
criteria, failure modes, and operational requirements.

Alternatives: combine product-facing completion with frontend, HTTP hosting,
live ingestion, external persistence, and deployment readiness.

Rejected because: combining those would blur the accepted product surface
and create claims not supported by the focused gate evidence.

Trade-offs/debt: the next milestone can now move into hosted delivery or
persistent product history from a stable product contract.

Review explanation: "This milestone makes the product usable; hosting,
frontends, live feeds, and deployment still need their own gates."

## Included Surface

Included:

```text
RadarPulseProductPipelineModels product DTO records
RadarPulseProductPipelineMapper
RadarPulseProductPipelineService
deterministic synthetic/demo product run input
archive-file product run input using NEXRAD RadarEventBatch publisher and
  owned-snapshot capture
product run in-memory history
product run list/latest/detail queries
product batch/source/handler-output/diagnostics/capacity queries
product control request/result contract
stop-accepting, drain-accepted, cancel-open/release, and
  reject-unsafe-fallback product controls
product CLI commands:
  radarpulse product pipeline demo
  radarpulse product pipeline run-archive
API-facing response wrapper and contract methods
focused Release gate and Release build evidence
gate documentation and handoff updates
```

Excluded:

```text
frontend SPA or rich radar visualization UI
production HTTP host or deployed API service
HTTP routing, auth, CORS, TLS, server lifecycle, or deployment configuration
persistent product run history
true live network ingestion
external broker/cloud queue/database adapter certification
production broker durability or cross-machine delivery
deployment automation
autoscaling
alert routing
operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claim
changing milestone 020-027 accepted backend decisions
```

## Evidence

Primary source documents:

```text
docs/milestones/028-product-facing-pipeline-console-and-api.md
docs/milestones/028-product-facing-pipeline-console-and-api-plan.md
docs/milestones/028-product-facing-pipeline-console-and-api-gate.md
```

Input evidence from earlier milestones:

```text
milestone 024:
  custom handler output contracts, processing run read models, BFF query
  shapes, and diagnostics accepted for deterministic archive-shaped MVP
  workloads

milestone 025:
  handler delta/merge contract accepted for explicitly mergeable handlers

milestone 026:
  deterministic local file-based durable adapter accepted as the persistent
  restart/recovery baseline

milestone 027:
  production pipeline profile, archive-shaped production pipeline runner,
  BFF read-model publication, operator diagnostics, rollback/fallback
  controls, file durable recovery posture, and capacity evidence accepted
```

Implementation evidence:

```text
RadarPulseProductPipelineModels:
  stable product DTOs for run, configuration, operator summary, capacity,
  diagnostics, batches, sources, handler output, query results, API
  responses, and controls

RadarPulseProductPipelineMapper:
  maps milestone 027 production pipeline results and milestone 024 read
  models into product contracts

RadarPulseProductPipelineService:
  composes synthetic/demo input, archive-file input, the accepted production
  pipeline runner, product history, read queries, and control mapping

RadarPulseProductPipelineApiContract:
  maps product run/query/control operations to HTTP-like response contracts
  over the same product service

src/Presentation/Program.cs:
  exposes product pipeline demo and archive-file commands in the existing
  CLI presentation layer
```

Verification:

```text
slice 1 focused product DTO suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineDtoTests"
  result: 3 passed, 0 failed, 0 skipped

slice 2 focused product service suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineServiceTests"
  result: 6 passed, 0 failed, 0 skipped

slice 3 focused product query suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineQueryTests"
  result: 4 passed, 0 failed, 0 skipped

slice 4 focused product control suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineControlTests"
  result: 5 passed, 0 failed, 0 skipped

slice 5 focused product CLI suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineCliTests"
  result: 4 passed, 0 failed, 0 skipped

slice 6 focused product API contract suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 4 passed, 0 failed, 0 skipped

focused milestone 028 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductPipelineDtoTests|FullyQualifiedName~RadarPulseProductPipelineServiceTests|FullyQualifiedName~RadarPulseProductPipelineQueryTests|FullyQualifiedName~RadarPulseProductPipelineControlTests|FullyQualifiedName~RadarPulseProductPipelineCliTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 26 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

## Final Decision

Decision:

```text
accepted with scoped warnings for product-facing pipeline console/API
completion over deterministic archive-shaped workloads
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to expose the accepted
production-shaped backend pipeline as a product-facing console/API contract
for deterministic archive-shaped radar workflows, with stable DTOs,
run/read/control workflows, operator diagnostics, handler output visibility,
capacity evidence, product CLI commands, and an API-facing response contract
```

Named warnings:

```text
milestone 028 validates deterministic synthetic/demo and archive-file shaped
  product workflows, not true live network ingestion
the API-facing contract is transport-stable service/API shape, not a
  production HTTP deployment claim
the console workflow is a product command surface, not a frontend SPA or
  rich radar visualization
product run history is in-memory for this milestone
the file durable adapter remains the local restart/recovery baseline
external broker/cloud queue/database adapters are not included and are not
  planned for this project
deployment automation, autoscaling, alert routing, runbooks, cross-machine
  throughput certification, and exactly-once delivery are not claimed
```

Recommended next milestone input:

```text
product HTTP host and persistent run history.

Use the accepted product DTOs, product pipeline service, query/control
surface, console workflow, and API-facing contract to add a thin hosted
delivery surface and durable product run history if hosted delivery is the
selected next product slice. Do not expand the next milestone into true live
network ingestion, external broker/cloud queue/database adapter
certification, deployment automation, frontend SPA completion, or
exactly-once delivery unless explicitly reprioritized.
```

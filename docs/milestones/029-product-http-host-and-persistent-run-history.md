# Milestone 029: Product HTTP Host And Persistent Run History

Status: planned.

Milestone 029 starts from the closed milestone 028 product-facing pipeline
console/API milestone.

Milestone 028 closed with this answer:

```text
accepted with scoped warnings for product-facing pipeline console/API
completion over deterministic archive-shaped workloads
```

The important next input is:

```text
product HTTP host and persistent run history
```

The important shift is:

```text
from:
  RadarPulse has stable product DTOs, a product pipeline service, read and
  control workflows, console commands, and a transport-stable API-facing
  contract, with product run history kept in memory

to:
  RadarPulse has a thin hosted HTTP product surface over the same accepted
  product service contract, plus deterministic local persistent product run
  history that survives process restart for product read workflows
```

This document records the milestone 029 concept, architecture boundary,
hosted delivery posture, persistent run-history posture, API route shape,
state and recovery behavior, packaging boundary, and expected gate question.
The detailed implementation sequence will be recorded in
`029-product-http-host-and-persistent-run-history-plan.md`.

## Milestone Goal

Milestone 029 should make the milestone 028 product surface available through
a thin HTTP host and make product run history durable across process restart.

The milestone should prove these concrete outcomes:

```text
a product HTTP host can expose the accepted milestone 028 run, read, and
  control contract without reimplementing processing or read-model logic
HTTP routes map product requests and product API responses into stable status
  codes and JSON response bodies
product run history can be persisted through a deterministic local file-backed
  product history store
persisted product run summaries, details, diagnostics, handler output,
  capacity evidence, and product warnings can be reloaded after process
  restart
history persistence is fail-closed for incompatible schema versions, corrupt
  files, invalid run records, and unsafe storage paths
the console and HTTP host can share the same product service semantics while
  keeping hosted lifecycle, storage configuration, and product diagnostics
  explicit
documentation and focused Release gates prove hosted run/read/control and
  restart-history behavior end to end
the product surface stays honest about deterministic archive-shaped scope,
  local file persistence, absent frontend SPA, absent true live network
  ingestion, absent deployment automation, and absent exactly-once delivery
  claims
```

This is a hosted product delivery and local history durability milestone over
the accepted product service. It is not a new processing architecture
milestone, not a live-ingestion milestone, not an external broker/cloud
queue/database adapter milestone, not a frontend SPA milestone, and not a
deployment platform milestone.

## Selected Product Surface

Milestone 029 selects a narrow hosted product delivery surface.

The selected surface is:

```text
hosted workflow:
  run deterministic product demo workloads through HTTP
  inspect latest and historical persisted product runs through HTTP
  inspect run detail, batches, sources, handler output, diagnostics, capacity
    evidence, warnings, readiness, first blocker, and fallback recommendation
  execute explicit product controls through HTTP

delivery adapter:
  thin ASP.NET Core/minimal HTTP host in the existing repository solution
  HTTP routes call the milestone 028 product API contract or product service
    directly without creating a second pipeline implementation

history adapter:
  deterministic local file-backed product run history store
  versioned persisted product run record format
  reload-on-start behavior for previously completed or blocked product runs
```

The hosted surface deliberately uses deterministic archive-shaped input. The
first host may support deterministic demo runs and archive-file shaped requests
where the existing product service supports them. It must not present hosted
archive-shaped operation as true live radar ingestion.

## Architecture Boundary

Safe in milestone 029:

```text
add product run history store abstractions over milestone 028 product run
  records and DTOs
add an in-memory product history store adapter if needed to preserve existing
  milestone 028 behavior behind the same contract
add a deterministic local file-backed product history store with explicit
  schema version, storage identity, load summary, and first blocking reason
add product history persistence tests for append/save, reload, duplicate run
  identity, corrupt content, unsupported schema, invalid records, and
  path/configuration validation
adapt RadarPulseProductPipelineService so history storage is injectable while
  keeping existing product run/read/control semantics
add a thin HTTP host project or presentation host surface that maps routes to
  product API contract/service calls
add HTTP route tests for demo run, list/latest/detail, diagnostics, capacity,
  handler output, not-found responses, validation errors, and controls
add host configuration for product history path, demo defaults, and explicit
  local-only hosted posture
add focused tests, gate evidence, docs, and handoff updates
```

Not safe in milestone 029 unless explicitly reprioritized:

```text
silently changing milestone 027 production pipeline defaults
silently changing milestone 028 product DTO or product workflow semantics
silently changing ordered processing, rebalance, durable envelope, or handler
  merge semantics
silently replacing local file product history with external broker, cloud
  queue, or database persistence
claiming true live network ingestion from hosted deterministic input
claiming deployment platform readiness from a local HTTP host
building a frontend SPA or rich radar visualization as part of this milestone
claiming autoscaling, alert routing, runbooks, cross-machine throughput, or
  exactly-once production delivery
hiding fallback by switching provider, durable adapter, execution mode, or
  product history adapter
```

Out of scope for this milestone:

```text
external broker/cloud queue/database adapter certification
true live network feed subscription and live backfill policy
frontend SPA or rich visualization application
production deployment automation
autoscaling, alert routing, pager policy, and operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claims
authentication, authorization, TLS termination, CORS policy hardening, and
  public internet exposure unless explicitly pulled forward
reopening milestone 020-028 backend or product contract decisions
```

## HTTP Host Posture

The HTTP host should be a delivery adapter, not a processing owner.

The host should:

```text
create or receive one configured product pipeline service instance
map HTTP request bodies and route parameters to product requests
map product API responses to stable HTTP status codes and JSON response bodies
expose health/readiness information for the product host and history store
surface validation errors, not-found results, blocked states, warnings, and
  first blocking reasons without throwing for normal product misses
keep route handlers thin enough that product behavior can still be tested at
  the service/API-contract layer
avoid storing mutable runtime state in route handlers
```

The host may be runnable from the existing console application or from a small
new presentation project, whichever best fits the current solution. If a new
project is added, it must remain thin and reference existing application
contracts rather than duplicating processing code.

## Expected HTTP Route Shape

The route names may be refined during implementation, but the contract groups
should remain stable:

```text
run commands:
  POST /product/pipeline/runs/demo
  POST /product/pipeline/runs/archive

run queries:
  GET /product/pipeline/runs
  GET /product/pipeline/runs/latest
  GET /product/pipeline/runs/{runId}
  GET /product/pipeline/runs/{runId}/batches
  GET /product/pipeline/runs/{runId}/batches/{batchId}
  GET /product/pipeline/runs/{runId}/sources
  GET /product/pipeline/runs/{runId}/sources/{sourceId}
  GET /product/pipeline/runs/{runId}/handlers/{handlerId}
  GET /product/pipeline/runs/{runId}/diagnostics
  GET /product/pipeline/runs/{runId}/capacity

control commands:
  POST /product/pipeline/controls/stop-accepting
  POST /product/pipeline/controls/drain-accepted
  POST /product/pipeline/controls/cancel-open-release
  POST /product/pipeline/controls/reject-unsafe-fallback

host state:
  GET /product/pipeline/host/readiness
```

The route layer can return the milestone 028 `RadarPulseProductApiResponse<T>`
shape directly or a thin HTTP wrapper over it. The important constraint is that
HTTP transport concerns do not change product meaning.

## Persistent Product Run History

Milestone 028 deliberately kept product run history in memory. Milestone 029
adds a local persistent product history store.

The product history store should:

```text
persist product run records after successful or blocked product run creation
preserve run summaries, run details, diagnostics, batch/source/handler output,
  capacity evidence, warnings, input summary, timestamps, and run state
reload records deterministically at service startup or store construction
preserve latest-run ordering after reload
reject duplicate run identity unless the duplicate is an idempotent same-record
  replay
expose storage kind, storage path, schema version, loaded record count,
  rejected record count, first invalid record, first blocking reason, and
  compatibility posture
fail closed for unsupported schema, corrupt content, invalid JSON, invalid
  run ids, unsafe storage paths, and partial write failures
write records atomically enough for deterministic local gates
```

The accepted persistence boundary remains local file-backed product history.
It is separate from the milestone 026 durable envelope adapter and does not
claim broker durability, database durability, cross-machine delivery, or
exactly-once end-to-end product delivery.

## Product Workflow

The hosted product workflow should answer:

```text
start a deterministic RadarPulse product pipeline run over HTTP
receive a stable product response with run id, state, readiness, first
  blocker, fallback recommendation, warnings, and capacity evidence
restart the service and still query completed or blocked product runs
list previous runs and identify the latest run
query run detail, batches, sources, handler outputs, diagnostics, and capacity
  evidence through HTTP
issue explicit product controls and see accepted, rejected, blocked, or
  recommended control posture
inspect host readiness and persistent history load posture
```

The workflow should not require HTTP clients to understand provider queue
telemetry, durable envelope internals, processing core mutation rules, or
benchmark threshold vocabulary. Those details remain available only through
diagnostic fields where they explain readiness and blockers.

## State, Recovery, And Failure Posture

Milestone 029 must keep failure states explicit.

Expected posture:

```text
history store cannot load:
  host readiness reports blocked history posture
  run creation fails closed unless an explicit in-memory mode is configured

history record is corrupt or incompatible:
  load summary reports first invalid record and first blocking reason
  incompatible records are not silently ignored as healthy history

run execution succeeds but persistence fails:
  product response reports persistence failure and run history is not claimed
  durable product history readiness becomes blocked

query misses:
  HTTP returns product not-found response instead of exception-shaped failure

control requests:
  unsafe fallback remains rejected and product-visible
```

## Documentation And Packaging

Milestone 029 should include hosted product documentation that explains:

```text
how to run the local product HTTP host
how to configure the product history path
how to create a deterministic demo run
how to query run history, diagnostics, handlers, capacity, and controls
how to interpret persistent history readiness and blocked states
what is still outside scope
```

Release packaging remains scoped to the repository's .NET solution. A local
HTTP host is not deployment automation, public production hosting, TLS/CORS
hardening, authentication, autoscaling, or an operations runbook.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to expose the accepted product pipeline contract through
a thin local HTTP host and preserve product run history through deterministic
local file-backed persistence for archive-shaped workflows?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for product HTTP host and persistent run history over
deterministic archive-shaped workloads; no frontend SPA, true live network
ingestion, external broker/database adapter, deployment automation,
cross-machine delivery, auth/TLS/CORS hardening, or exactly-once delivery is
claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
product history store contract and deterministic file-backed persistence
product service compatibility with injectable persistent history
restart/reload product run history queries
history load failure and fail-closed invalid storage posture
HTTP route mapping for run, list/latest/detail, diagnostics, handler output,
  capacity, not-found, validation error, readiness, and controls
documentation examples
focused Release product HTTP/history suite
Release build
```

Full-suite validation can be run if implementation touches shared runtime
behavior beyond the product history store, product service injection, and host
presentation surface.

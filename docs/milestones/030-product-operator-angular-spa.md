# Milestone 030: Product Operator Angular SPA

Status: planned.

Milestone 030 starts from the closed milestone 029 product HTTP host and
persistent run history milestone.

Milestone 029 closed with this answer:

```text
accepted with scoped warnings for product HTTP host and persistent run
history over deterministic archive-shaped workloads
```

The important next input is:

```text
product operator UI over the HTTP host
```

The selected implementation direction is:

```text
build a local Angular SPA operator UI in a separate folder under
src/Presentation, using the newest stable Angular release available at
milestone start, and call only the accepted RadarPulse.Http product routes
```

As of 2026-05-26, the official Angular release and compatibility pages list
Angular 21.x as the newest actively supported stable line and show Angular
22.0 scheduled for the week of 2026-06-01. Milestone 030 therefore selects
Angular 21 latest patch through `@angular/cli@latest`/`@angular/core@latest`
at scaffold time, with Node.js 22.18.0 satisfying the official Angular 21
Node.js compatibility range.

This document records the milestone 030 concept, architecture boundary,
frontend delivery posture, UI workflow, HTTP client posture, state and failure
behavior, local packaging boundary, and expected gate question. The detailed
implementation sequence will be recorded in
`030-product-operator-angular-spa-plan.md`.

## Milestone Goal

Milestone 030 should turn the accepted local product HTTP host into a usable
operator-facing browser application for deterministic archive-shaped
RadarPulse workflows.

The milestone should prove these concrete outcomes:

```text
an Angular SPA can be created under src/Presentation without destabilizing
  the existing .NET CLI and HTTP host projects
the UI can be run locally and configured with a RadarPulse.Http base URL
the UI can call the accepted milestone 029 product HTTP routes without
  reaching into application, infrastructure, processing, durable, or BFF
  internals
operators can create deterministic demo/archive-shaped product runs through
  the UI
operators can list persisted runs, inspect the latest run, and inspect run
  detail after product history reload
operators can inspect batches, sources, handler output, diagnostics, capacity
  evidence, warnings, first blockers, fallback recommendations, and readiness
  posture in product vocabulary
operators can issue explicit product controls through the accepted HTTP
  control routes and see accepted, rejected, blocked, or recommended posture
the UI handles loading, empty, not-found, validation, blocked history, and
  HTTP connection failure states explicitly
documentation and focused gates prove the Angular app builds and that its API
  client/view-model mapping matches the accepted product HTTP contract
the product surface stays honest about deterministic archive-shaped scope,
  local HTTP hosting, local file-backed product history, absent true live
  network ingestion, absent public production deployment, absent auth/TLS/CORS
  hardening, and absent exactly-once delivery claims
```

This is a product operator UI milestone over the accepted local HTTP product
delivery surface. It is not a new processing architecture milestone, not a
live-ingestion milestone, not an external broker/cloud queue/database adapter
milestone, not a public hosted production milestone, and not a deployment or
security hardening milestone.

## Selected UI Surface

Milestone 030 selects a narrow Angular SPA operator surface.

The selected surface is:

```text
local UI project:
  src/Presentation/OperatorUi

framework:
  latest stable Angular at scaffold time
  standalone Angular application structure
  TypeScript strict mode
  Angular CLI build/test tooling

HTTP dependency:
  RadarPulse.Http local host from milestone 029
  accepted product pipeline routes only

operator workflow:
  inspect host/history readiness
  run deterministic demo workloads
  run archive-shaped workloads where the HTTP host supports archive requests
  list persisted product runs
  inspect latest run and selected historical runs
  inspect diagnostics, capacity evidence, batches, sources, and handler output
  execute explicit product controls
```

The UI deliberately uses the product HTTP contract as its backend boundary.
It must not import .NET assemblies, parse local history JSON files directly,
or duplicate lower-level backend runtime decisions in frontend code.

## Architecture Boundary

Safe in milestone 030:

```text
create an Angular SPA in src/Presentation/OperatorUi
exclude the Angular project folder from the existing RadarPulse.Cli .NET SDK
  item glob if needed to avoid build/package churn
add a small typed TypeScript API client for the milestone 029 HTTP routes
add product-facing TypeScript DTOs that mirror the stable JSON response shape
  needed by the UI
add a UI state layer for selected run, latest run, readiness, diagnostics,
  control outcomes, and request errors
add routed or tabbed operator views for overview, runs, run detail,
  diagnostics, batches, sources, handlers, capacity, readiness, and controls
add local Angular environment configuration for the product HTTP base URL
add focused Angular unit tests for API URL mapping, response-state mapping,
  view-model helpers, and key components
add documentation for running RadarPulse.Http and the Angular UI together
add gate evidence for Angular build/test plus the relevant .NET HTTP/product
  gate that proves the backend contract remains intact
```

Not safe in milestone 030 unless explicitly reprioritized:

```text
silently changing milestone 029 HTTP route semantics
silently changing milestone 028/029 product DTO meaning to suit the UI
reaching around the HTTP host into CLI commands, local JSON history files, or
  lower-level application services
claiming true live network ingestion from deterministic demo/archive-shaped
  workflows
claiming public production frontend deployment from the local Angular app
adding authentication, authorization, TLS termination, or CORS hardening as
  hidden scope
claiming external broker/cloud queue/database adapter readiness
claiming autoscaling, alert routing, runbooks, cross-machine throughput, or
  exactly-once production delivery
reopening accepted milestone 020-029 backend runtime, durable, handler, BFF,
  production pipeline, product contract, HTTP host, or persistence decisions
```

Out of scope for this milestone:

```text
true live radar network feed subscription and live backfill policy
external broker/cloud queue/database adapter certification
database-backed product history
public production deployment automation
authentication, authorization, TLS termination, and CORS hardening
autoscaling, alert routing, pager policy, and operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claims
rich meteorological radar visualization beyond product read/diagnostic views
reopening milestone 020-029 backend architecture decisions
```

## UI Product Workflow

The UI should be organized around operator intent.

The main workflow should answer:

```text
is the local product HTTP host reachable
is persistent product history healthy, blocked, or unavailable
what is the latest product run
which historical product runs are available after restart
can I start a deterministic demo or archive-shaped run
is the selected run completed, blocked, failed, canceled, or waiting for
  operator action
what is the first blocker and recommended action
what warnings and scoped limitations apply to the run
which batches were processed and committed
which sources are visible and what output is available for each source
which handler outputs are available and what posture produced them
what diagnostic and capacity evidence explains readiness
which controls are available and what outcome did the last control request
  produce
```

The UI should not require operators to understand provider queue telemetry,
durable envelope internals, processing core mutation rules, or benchmark
threshold vocabulary. Those details can appear only where the product HTTP
diagnostic fields already expose them to explain readiness and blockers.

## HTTP Client Posture

The Angular app should treat the HTTP host as the source of truth.

Expected client groups:

```text
run commands:
  POST /product/pipeline/runs/demo
  POST /product/pipeline/runs/archive

run queries:
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

controls:
  POST /product/pipeline/controls/stop-accepting
  POST /product/pipeline/controls/drain-accepted
  POST /product/pipeline/controls/cancel-open-release
  POST /product/pipeline/controls/reject-unsafe-fallback

readiness:
  GET /product/pipeline/host/readiness
```

The first UI client can hand-code the TypeScript DTO subset needed by the UI.
OpenAPI generation is not required unless the HTTP host later publishes an
OpenAPI document or the contract breadth makes manual DTOs too costly.

## UI Design Posture

The UI should feel like an operator console, not a marketing site.

Expected design posture:

```text
first viewport shows operational state, latest run, readiness, and primary
  run actions
navigation is dense and predictable
status, warning, blocked, rejected, and failed states are visible without
  requiring console logs or browser devtools
controls use direct command buttons with confirmation posture where the action
  is destructive or release-oriented
tables/lists are stable and scannable for repeated run inspection
diagnostics and capacity evidence use compact key/value and table layouts
empty, loading, offline, and not-found states are product-specific and
  actionable
```

The UI should avoid oversized landing-page composition, decorative hero
sections, and in-app explanatory text about how the UI works. It should show
the actual product state and controls immediately.

## State, Recovery, And Failure Posture

Milestone 030 must keep failure states explicit.

Expected posture:

```text
HTTP host is unreachable:
  UI shows connection failure and keeps local controls disabled

history readiness is blocked:
  UI shows blocked history posture, storage kind/path if returned, and first
  blocking reason

query misses:
  UI shows product not-found state instead of treating it as a crash

run creation fails validation:
  UI shows the product response reason and does not invent a local run

control request is rejected:
  UI shows rejected control posture and the product recommendation

handler output is absent:
  UI distinguishes no handler output from request failure

route responses include warnings or first blockers:
  UI surfaces them near the relevant run/readiness/control context
```

No frontend state should claim a run exists unless the HTTP product response
or subsequent HTTP query confirms it.

## Documentation And Packaging

Milestone 030 should include UI documentation that explains:

```text
how the Angular UI is located under src/Presentation/OperatorUi
how to install npm dependencies
how to run RadarPulse.Http locally
how to configure the UI product HTTP base URL
how to start the UI dev server
how to build and test the UI
how to create a deterministic demo run
how to inspect persisted run history, diagnostics, handlers, capacity,
  readiness, and controls
what remains outside scope
```

Local packaging remains scoped to the repository. The Angular SPA is not a
public production deployment, CDN release, auth/TLS/CORS posture, or
operations runbook.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to provide a local Angular product operator UI over the
accepted product HTTP host for running, inspecting, diagnosing, and
controlling deterministic archive-shaped workflows?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for product operator Angular SPA over the local
product HTTP host; no true live network ingestion, external broker/database
adapter, public production deployment, auth/TLS/CORS hardening, cross-machine
delivery, or exactly-once delivery is claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
Angular latest stable scaffold under src/Presentation/OperatorUi
Angular dependency install lockfile committed for deterministic local builds
typed product HTTP client route mapping
operator overview and readiness UI
run creation, run list, latest run, and run detail UI
batches, sources, handler output, diagnostics, and capacity UI
control route UI and rejected/blocked posture
unreachable host, not-found, validation, and blocked history states
Angular unit tests for API/view-model/component behavior
Angular production build
focused .NET product HTTP/API gate proving the backend contract remains
  healthy
documentation examples
```

Full .NET suite validation can be run if implementation touches shared
backend runtime behavior. The expected backend edits should be limited to
presentation packaging or explicit project item exclusions, not product
runtime semantics.

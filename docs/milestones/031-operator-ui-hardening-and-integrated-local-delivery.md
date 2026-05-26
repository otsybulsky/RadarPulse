# Milestone 031: Operator UI Hardening And Integrated Local Delivery

Status: planned.

Milestone 031 starts from the closed milestone 030 product operator Angular
SPA milestone.

Milestone 030 closed with this answer:

```text
accepted with scoped warnings for product operator Angular SPA over the local
product HTTP host for deterministic archive-shaped workflows
```

The important next input is:

```text
operator UI hardening and integrated local delivery
```

The important shift is:

```text
from:
  RadarPulse has a local Angular operator SPA that can be run beside
  RadarPulse.Http and can call the accepted product HTTP routes through a
  typed client

to:
  RadarPulse has a hardened local product UI surface with browser-level smoke
  coverage, URL-restorable operator state, stricter form/control validation,
  polished failure posture, and an integrated local same-origin delivery path
  from RadarPulse.Http for the built Angular SPA
```

This document records the milestone 031 concept, architecture boundary,
browser validation posture, navigation-state posture, integrated local
delivery posture, UI validation posture, documentation posture, and expected
gate question. The detailed implementation sequence will be recorded in
`031-operator-ui-hardening-and-integrated-local-delivery-plan.md`.

## Milestone Goal

Milestone 031 should make the accepted local operator UI a stable local
product surface rather than only a dev-server SPA.

The milestone should prove these concrete outcomes:

```text
browser-level smoke coverage exercises the critical operator workflows through
  a real browser runtime
selected run and active inspection tab are restorable from URL state
operator forms validate input before issuing HTTP requests while still
  preserving product response errors from the backend
control actions expose loading, blocked, rejected, disabled, and successful
  posture without hiding product reasons
the UI remains usable and stable across desktop and narrow browser viewports
the built Angular SPA can be served by RadarPulse.Http as a local same-origin
  product UI delivery path
the Angular dev-server CORS bridge remains scoped to local development and is
  not upgraded into a production public API security claim
documentation explains both local workflows:
  dev server plus RadarPulse.Http
  single RadarPulse.Http host serving the built UI
focused gates prove Angular unit/build behavior, browser smoke behavior, and
  the .NET HTTP/static-delivery surface
the product surface stays honest about deterministic archive-shaped scope,
  local file-backed history, absent true live ingestion, absent public
  production hosting, absent auth/TLS/production CORS hardening, and absent
  exactly-once delivery claims
```

This is an operator UI hardening and local delivery milestone over the
accepted milestone 029/030 product HTTP and Angular surfaces. It is not a new
processing architecture milestone, not a live-ingestion milestone, not an
external broker/cloud queue/database adapter milestone, not a public hosted
production milestone, and not a security or deployment-automation milestone.

## Selected Product Surface

Milestone 031 keeps the same product boundary accepted in milestone 030.

The selected surface is:

```text
local UI project:
  src/Presentation/OperatorUi

HTTP delivery host:
  src/Presentation/RadarPulse.Http

development delivery:
  Angular dev server on localhost
  scoped local CORS bridge in RadarPulse.Http

integrated local delivery:
  Angular production build output served by RadarPulse.Http
  same-origin product API and operator UI under one local host
  client-side route fallback for operator UI routes

browser validation:
  real-browser smoke tests for critical operator workflows
  mocked or deterministic local HTTP responses where needed for stable UI
    smoke coverage
  focused same-origin host smoke coverage for the integrated delivery path
```

The UI still calls only the accepted product HTTP routes. It must not import
.NET assemblies, parse local history JSON directly, or duplicate lower-level
runtime decisions in frontend code.

## Architecture Boundary

Safe in milestone 031:

```text
add browser smoke tooling under src/Presentation/OperatorUi
add browser smoke tests for readiness, run creation, run list/detail,
  inspection tabs, handler lookup, diagnostics/capacity views, controls, and
  unreachable/error states
add URL state for selected run and active run-detail tab
add route fallback behavior that keeps unknown local UI routes within the
  operator app while not intercepting product HTTP API routes
add form-level validation for product HTTP base URL, archive run inputs,
  handler lookup inputs, and control action availability
add UI polish for narrow viewport behavior, stable table/panel sizing,
  loading/disabled states, and empty/error posture
add a RadarPulse.Http static UI delivery path for the built Angular SPA
add configuration that keeps static UI delivery explicitly local and optional
  for the product host
add focused .NET tests for static UI route fallback and API route separation
add focused Angular tests for URL state, validation, and hardened view-model
  behavior
update OperatorUi README, docs/handoff.md, and milestone gate evidence
```

Not safe in milestone 031 unless explicitly reprioritized:

```text
silently changing accepted milestone 029 HTTP route semantics
silently changing milestone 028/029 product DTO meaning to suit the UI
reaching around the HTTP host into CLI commands, local JSON history files, or
  lower-level application services
claiming true live network ingestion from deterministic demo/archive-shaped
  workflows
claiming public production frontend deployment from the integrated local
  same-origin host
turning the local CORS bridge into a production security posture
adding authentication, authorization, TLS termination, or production CORS
  hardening as hidden scope
claiming external broker/cloud queue/database adapter readiness
claiming deployment automation, autoscaling, alert routing, runbooks,
  cross-machine throughput, or exactly-once production delivery
reopening accepted milestone 020-030 backend runtime, durable, handler, BFF,
  production pipeline, product contract, HTTP host, persistence, or UI
  boundary decisions
```

Out of scope for this milestone:

```text
true live radar network feed subscription and live backfill policy
external broker/cloud queue/database adapter certification
database-backed product history
public production deployment automation
authentication, authorization, TLS termination, and production CORS hardening
autoscaling, alert routing, pager policy, and operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claims
rich meteorological radar visualization beyond product read/diagnostic views
reopening milestone 020-030 backend architecture decisions
```

## Browser Smoke Posture

Milestone 030 used Angular unit/component tests and manual browser validation.
Milestone 031 adds automated browser smoke evidence.

The smoke suite should cover:

```text
host readiness can be loaded and rendered
persisted run list and latest run can be rendered
a deterministic demo run action can create or surface a product run response
selected run detail can render summary, batches, sources, handlers,
  diagnostics, and capacity evidence
selected run and active tab survive a page reload through URL state
handler output lookup distinguishes absent output from request failure
control actions render accepted, rejected, blocked, and loading posture
unreachable host or failed request posture is visible and does not enable
  unsafe controls
the integrated local host serves the Angular shell without intercepting
  product API routes
```

The browser smoke suite should stay focused. It should prove operator-critical
behavior, not replace lower-level .NET product API tests or Angular unit tests.

## Navigation And URL State

Milestone 031 should make the browser location useful operator state.

Expected URL state:

```text
selected run id:
  restorable after reload
  shareable as a local deep link
  resilient when the run is no longer present or the host is unreachable

active inspection tab:
  summary
  batches
  sources
  handlers
  diagnostics
  capacity

optional detail selections:
  selected batch/source/handler lookup values may stay local unless the
  implementation can preserve them without noisy URLs
```

The UI should not claim that a run exists merely because a URL contains a run
id. A selected run from URL state must be confirmed through the product HTTP
query surface or shown as not found/unavailable.

## Integrated Local Delivery

Milestone 031 should add a single-host local workflow.

The selected integrated delivery posture is:

```text
Angular build:
  production output remains produced by the Angular CLI

RadarPulse.Http delivery:
  serves the built operator UI from a configured local static asset root
  falls back to index.html for operator UI routes
  does not intercept /product/pipeline API routes
  keeps API responses and UI assets on the same local origin

development workflow:
  Angular dev server remains supported for frontend development
  the scoped local CORS bridge remains only for the dev-server origin

production claim boundary:
  same-origin local delivery is not public production deployment
  no auth/TLS/production CORS hardening is claimed
  no deployment automation or hosting platform certification is claimed
```

The implementation may choose a conventional static asset folder or a
configuration-driven path to the Angular `dist` output. The important
architectural constraint is that RadarPulse.Http stays a thin local delivery
adapter and does not own frontend build orchestration as hidden runtime logic.

## UI Validation And Failure Posture

Milestone 031 should reduce avoidable bad requests while preserving backend
truth.

Expected validation:

```text
product HTTP base URL:
  must be a non-empty absolute HTTP or HTTPS URL for dev-server mode
  invalid local input does not replace the active client base URL

archive-shaped run request:
  required fields are validated before the HTTP call
  unsupported or missing fields show local validation errors

handler lookup:
  source id and handler field name are required before lookup
  absent product output remains distinct from validation failure

controls:
  disabled while host/readiness state is unavailable or blocked
  loading state is visible during request execution
  rejected and blocked product responses remain visible near the controls
```

Expected failure posture:

```text
network failure:
  visible connection posture and disabled unsafe controls

not found:
  product not-found state, not a generic crash

blocked history:
  history readiness and first blocking reason stay visible

rejected control:
  product rejection reason and recommendation stay visible

unknown route:
  operator UI route fallback remains in the UI shell for same-origin delivery
```

## Documentation And Packaging

Milestone 031 should update local operator documentation for two workflows:

```text
development workflow:
  run RadarPulse.Http locally
  run Angular dev server
  configure HTTP base URL
  run Angular unit/build gate
  run browser smoke gate

integrated local workflow:
  build Angular app
  run RadarPulse.Http with static UI delivery enabled or configured
  open the local product UI from the HTTP host
  create a deterministic demo/archive-shaped run
  inspect persisted history, diagnostics, handlers, capacity, readiness, and
    controls
```

The documentation must keep the scope warning visible: this is local product
delivery, not public production deployment or production security hardening.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to use the Angular operator UI as the hardened local
product surface, including browser-smoke validated workflows and integrated
same-origin local delivery through RadarPulse.Http?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for operator UI hardening and integrated local
delivery over deterministic archive-shaped workflows; no true live network
ingestion, external broker/database adapter, public production deployment,
auth/TLS/production CORS hardening, deployment automation, cross-machine
delivery, or exactly-once delivery is claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
Angular URL/navigation state tests
Angular validation and hardened state tests
browser smoke tests for critical operator workflows
Angular production build
RadarPulse.Http static UI serving and route fallback tests
focused .NET product HTTP/static-delivery Release gate
single-host local workflow documentation
handoff updates
```

Full .NET suite validation can be run if implementation touches shared
runtime behavior beyond the HTTP presentation host. The expected backend edits
should stay limited to local static UI delivery, route fallback, route/API
separation, and tests.

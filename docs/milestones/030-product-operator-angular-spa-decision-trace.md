# Milestone 030 Decision Trace

Date: 2026-05-26

Decision: accept product operator Angular SPA over the local product HTTP host
for deterministic archive-shaped workflows with named scoped warnings.

This decision accepts milestone 030's Angular 21 operator SPA, CLI project
packaging boundary, typed TypeScript product HTTP client, runtime API base URL
override, operator overview, run workflow, persisted run list, selected run
detail, inspection views, operator controls, explicit failure posture, scoped
local Operator UI CORS bridge, focused Angular gate, focused .NET HTTP/API
gate, Release build, gate evidence, and handoff update on top of the
milestone 029 product HTTP host and persistent run history surface.

The accepted scope is a local browser operator UI over the accepted product
HTTP contract. RadarPulse can now run, inspect, diagnose, and control
deterministic demo/archive-shaped product workflows through a browser UI while
keeping product semantics behind `RadarPulse.Http`. The UI does not import
.NET assemblies, parse product history files directly, or call lower-level
application, processing, durable, handler, or BFF internals.

The decision deliberately does not claim true live network ingestion, rich
meteorological radar visualization, public production frontend deployment,
authentication, authorization, TLS termination, production CORS hardening,
external broker/cloud queue/database adapter readiness, database-backed
product history, cross-machine throughput certification, or exactly-once
production delivery. The CORS work accepted here is a scoped local development
bridge for the Angular dev server origin, not a production public API security
posture.

## Decision Matrix

```text
product operator Angular SPA:
  accepted with scoped warnings

Angular version:
  accepted; milestone 030 uses Angular 21 latest stable packages available at
  scaffold time

Angular workspace location:
  accepted; the app lives under src/Presentation/OperatorUi

.NET CLI packaging boundary:
  accepted; RadarPulse.Cli.csproj excludes OperatorUi/** from SDK item
  discovery so Angular workspace files and node_modules do not become CLI
  project content

npm dependency lock:
  accepted; package-lock.json is committed for deterministic local installs

typed product HTTP client:
  accepted; RadarPulseProductApiClient maps only the accepted milestone 029
  product routes

TypeScript product DTO subset:
  accepted; the UI mirrors the product HTTP response fields it needs without
  redefining backend semantics

runtime API base URL override:
  accepted; browser-local override is stored in localStorage and can be
  changed from the UI without rebuild

operator overview:
  accepted; host/history readiness, latest run, primary run action, and run
  list are visible in the first operator surface

run creation workflow:
  accepted; deterministic demo run creation and archive-shaped run request
  surfaces call the product HTTP host

run list/latest/detail workflow:
  accepted; persisted run summaries, latest run, and selected run detail are
  visible through HTTP queries

inspection views:
  accepted; summary, batches, sources, handlers, diagnostics, and capacity
  evidence are available in the selected run view

handler output lookup:
  accepted; handler output is queried through the accepted HTTP handler
  route, with absent-output posture distinguished from request failure

operator controls:
  accepted; stop accepting, drain accepted, cancel/release, and reject unsafe
  fallback are available through the accepted HTTP control routes

failure posture:
  accepted; loading, empty, not-found, blocked history, rejected control,
  bad-request, and unreachable-host states are explicit in UI state

local Operator UI CORS bridge:
  accepted with warning; RadarPulse.Http allows the Angular dev server origin
  by default for local milestone validation, but this is not production CORS
  hardening

frontend SPA deployment:
  not implemented; the UI runs as a local Angular app/dev server or build
  artifact, not a public production deployment

rich radar visualization:
  not implemented; the UI is an operator console over product read and
  diagnostic data

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

Angular gate:
  accepted; 13 Angular tests passed and production build succeeded with no
  warnings

focused .NET HTTP/API gate:
  accepted; 14 focused Release tests passed with no failures or skips

Release build:
  accepted; .NET Release build succeeded with zero warnings and zero errors
```

## Decision Explanations

### Accept Angular 21 Operator SPA

Decision: accept Angular 21 as the milestone 030 product operator UI
framework.

Why chosen: the user selected Angular SPA explicitly, and Angular 21 was the
newest stable Angular line at milestone start. It gives a standard local SPA
toolchain, strict TypeScript, Angular CLI build/test support, and a clear
path for future browser-level validation.

Alternatives: build a .NET Razor/Blazor UI, keep the product surface CLI-only,
or defer browser UI until a later hosted deployment milestone.

Rejected because: the request explicitly selected Angular SPA; CLI-only work
would not exercise the accepted HTTP product surface as a browser client; a
deployment milestone would add unrelated security and operations claims.

Trade-offs/debt: Angular adds an npm toolchain and browser delivery concerns.
The milestone keeps that contained under `src/Presentation/OperatorUi` and
does not make the .NET host responsible for serving the SPA yet.

Review explanation: "The project now has a real browser operator surface, not
only CLI and HTTP contracts."

### Accept OperatorUi Workspace Under Presentation

Decision: accept `src/Presentation/OperatorUi` as the Angular project
location.

Why chosen: the UI is a presentation-layer artifact, but the existing
`src/Presentation` folder is also the .NET CLI project folder. A nested
`OperatorUi` folder keeps the Angular workspace separate while matching the
requested placement.

Alternatives: create `src/Presentation.Ui`, place the UI at repo root, or put
the SPA inside `src/Presentation.Http`.

Rejected because: `src/Presentation.Ui` diverges from the requested location;
repo-root UI would separate it from presentation surfaces; putting Angular
inside the HTTP project would blur API host and frontend workspace concerns
before integrated delivery is selected.

Trade-offs/debt: the CLI project needed explicit item exclusions for
`OperatorUi/**`. Future integrated delivery may move build artifacts into
the HTTP host, but source ownership remains clean.

Review explanation: "Angular lives in Presentation, but not inside the CLI
project's compile surface."

### Accept Typed Product HTTP Client Boundary

Decision: accept a hand-coded typed TypeScript client over the milestone 029
HTTP routes.

Why chosen: milestone 029 already accepted the product HTTP route and response
contract. A small typed client makes the frontend boundary explicit and keeps
route mapping and product response state testable without requiring OpenAPI
generation.

Alternatives: call `fetch` ad hoc from components, generate a client from
OpenAPI, or import shared .NET contract artifacts.

Rejected because: ad hoc calls would spread HTTP semantics across components;
the HTTP host does not yet publish OpenAPI; importing .NET artifacts would
break the accepted HTTP-only UI boundary.

Trade-offs/debt: TypeScript DTOs must be maintained with the product HTTP
contract. OpenAPI generation can be reconsidered if route breadth grows or
contract drift becomes costly.

Review explanation: "The UI depends on the product HTTP contract, not backend
implementation objects."

### Accept Runtime API Base URL Override

Decision: accept a browser-local API base URL override in the UI top bar.

Why chosen: local host ports vary, and port 5000 was already occupied by
Docker during validation. A runtime override lets operators point the UI at
the active local `RadarPulse.Http` URL without rebuilding the Angular app.

Alternatives: hard-code `http://localhost:5000`, require an Angular
environment rebuild, or proxy all API requests through the dev server.

Rejected because: hard-coding failed the local workflow; rebuilds are clumsy
for local operator work; a proxy would hide the real browser-to-host boundary
that the milestone is meant to validate.

Trade-offs/debt: the override is browser-local state, not a deployment
configuration system. Integrated host delivery can remove or narrow this if
the API and SPA share an origin later.

Review explanation: "The UI can follow the local HTTP host instead of
requiring a fixed port."

### Accept Operator Overview And Run Workflow

Decision: accept the overview, readiness, run creation, latest run, and run
list workflow as the first operator surface.

Why chosen: operators need to know whether the host/history is usable before
they start work, then create or select product runs and see the latest state
without understanding backend internals.

Alternatives: start with a route explorer, build only run creation, or focus
first on detailed diagnostics screens.

Rejected because: route explorers are not product workflows; run-only UI is
not inspectable; diagnostics-only UI cannot start or select product work.

Trade-offs/debt: the first shell is intentionally compact and single-page.
Deep links, saved filters, route-level selected run state, and richer
navigation remain good hardening candidates.

Review explanation: "The first screen answers whether the product host is
usable and what run the operator should inspect."

### Accept Run Inspection Views

Decision: accept selected run tabs for summary, batches, sources, handlers,
diagnostics, and capacity evidence.

Why chosen: milestone 029 made these product routes available over HTTP.
The UI must expose the accepted product read surface so an operator can
diagnose blocked or completed runs without leaving product vocabulary.

Alternatives: show only run summaries, defer handler/diagnostic/capacity
views, or expose raw JSON responses.

Rejected because: summaries alone do not explain readiness; deferring
diagnostics would weaken the operator value; raw JSON would be a debugging
tool, not a product UI.

Trade-offs/debt: current views are compact and tabular. Rich radar-specific
visualization and advanced comparison workflows are outside this milestone.

Review explanation: "The UI makes product run evidence readable, not just
available through HTTP."

### Accept Operator Controls And Failure Posture

Decision: accept product control buttons and explicit failure state mapping.

Why chosen: the accepted product contract includes controls and fail-closed
posture. The browser UI needs to preserve stop/drain/cancel/reject semantics,
unsafe fallback rejection, blocked history, not-found, validation, and host
connection failure as visible operator states.

Alternatives: hide controls until production operations work, expose controls
without durable store path input, or treat request failures as generic toast
errors.

Rejected because: hiding controls would leave accepted product behavior
unused; durable control requests require a store path in the accepted
contract; generic errors would lose first blocker and recommendation posture.

Trade-offs/debt: controls are still local deterministic product controls, not
production runbook workflows. Confirmation flows and long-running action
tracking can be added in UI hardening.

Review explanation: "The UI preserves product control semantics instead of
turning failures into generic browser errors."

### Accept Scoped Local Operator UI CORS Bridge

Decision: accept a named local CORS policy in `RadarPulse.Http` for the
Angular dev server origin.

Why chosen: real browser validation showed Angular on `http://localhost:4200`
could not call `RadarPulse.Http` on `http://localhost:5117`; preflight
returned 405. The local UI requires a browser-valid cross-origin path during
development.

Alternatives: tell users to disable browser security, require a dev proxy,
serve the built SPA from the HTTP host immediately, or leave the UI usable
only through tests.

Rejected because: disabling browser security is unacceptable; a proxy would
hide the real local HTTP host boundary; integrated SPA hosting is a larger
delivery milestone; tests alone missed the browser preflight issue.

Trade-offs/debt: this is explicitly a local development bridge. Production
auth, TLS, CORS hardening, public hosting, and same-origin integrated
delivery remain separate future work.

Review explanation: "The browser can call the local product host, but this
is not a production security claim."

### Keep Live, Rich Visualization, External Adapters, Deployment, Security Hardening, And Exactly-Once Out Of Scope

Decision: keep true live network ingestion, rich radar visualization,
external broker/cloud queue/database adapters, public deployment,
auth/TLS/CORS hardening, operations automation, and exactly-once delivery
outside milestone 030.

Why chosen: milestone 030 validates the local product operator UI over the
accepted HTTP host. The excluded areas have independent architecture,
security, operations, and reliability requirements.

Alternatives: combine UI completion with live ingestion, public hosting,
security posture, and external persistence readiness.

Rejected because: combining them would blur the accepted local UI boundary
and create claims not supported by the milestone gate.

Trade-offs/debt: RadarPulse now has a usable UI path, but production
deployment, security, live feed, and exactly-once claims still need their own
gates if selected.

Review explanation: "This milestone makes the local operator UI real; it
does not make RadarPulse a public production SaaS."

## Included Surface

Included:

```text
src/Presentation/OperatorUi Angular workspace
Angular 21 package set and package-lock.json
Angular CLI build/test configuration
RadarPulse.Cli.csproj OperatorUi/** item exclusions
TypeScript product DTO subset
RADARPULSE_PRODUCT_API_BASE_URL injection token
runtime API base URL localStorage override
RadarPulseProductApiClient
product API response-state mapping helpers
operator overview shell
host/history readiness UI
latest run UI
deterministic demo run form/action
archive-shaped run form/action
persisted run list
selected run summary
run inspection tabs:
  summary
  batches
  sources
  handlers
  diagnostics
  capacity
handler output lookup UI
operator controls:
  stop accepting
  drain accepted
  cancel open/release
  reject unsafe fallback
explicit loading, empty, not-found, blocked, rejected, bad-request, and
  unreachable-host UI posture
RadarPulse.Http named local Operator UI CORS policy for http://localhost:4200
project-specific OperatorUi README
focused Angular gate
focused .NET HTTP/API gate
Release build evidence
gate documentation and handoff updates
```

Excluded:

```text
true live radar network ingestion
rich meteorological radar visualization
public production frontend deployment
serving the built Angular SPA from RadarPulse.Http
authentication and authorization
TLS termination
production CORS hardening
deployment automation
autoscaling
alert routing
operator runbooks
external broker/cloud queue/database adapter certification
database-backed product history
cross-machine throughput certification
exactly-once end-to-end production delivery claim
changing milestone 020-029 accepted backend or product decisions
```

## Evidence

Primary source documents:

```text
docs/milestones/030-product-operator-angular-spa.md
docs/milestones/030-product-operator-angular-spa-plan.md
docs/milestones/030-product-operator-angular-spa-gate.md
```

Input evidence from earlier milestones:

```text
milestone 028:
  product DTOs, product pipeline service, product read/control workflows,
  product CLI commands, and transport-stable API-facing response contract
  accepted

milestone 029:
  local product HTTP host, product run/read/diagnostics/capacity/handler/
  readiness/control routes, and deterministic local file-backed product run
  history accepted
```

Implementation evidence:

```text
src/Presentation/OperatorUi:
  Angular 21 standalone SPA, package-lock.json, local README, strict
  TypeScript build/test configuration

product-api.models.ts:
  TypeScript DTO subset for product API responses, requests, run summaries,
  run detail, diagnostics, capacity, batches, sources, handler output,
  readiness, and controls

product-api.client.ts:
  typed route methods over the accepted milestone 029 HTTP routes

product-api-state.ts:
  product response-state mapping for success, not-found, bad-request,
  blocked, rejected, failure, and network-error states

app.ts/app.html/app.css:
  operator overview, run workflow, inspection views, controls, and UI state

RadarPulseProductHttpServiceCollectionExtensions:
  named local Operator UI CORS policy registration

Program.cs:
  applies the local Operator UI CORS policy before product endpoint mapping
```

Verification:

```text
slice 1 Angular scaffold gate:
  cd src\Presentation\OperatorUi
  npm test -- --watch=false
  result: 2 passed, 0 failed
  npm run build
  result: succeeded

slice 1 .NET Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

slice 2 Angular client gate:
  npm test -- --watch=false
  result: 5 passed, 0 failed
  npm run build
  result: succeeded

slice 3 operator overview/run workflow gate:
  npm test -- --watch=false
  result: 7 passed, 0 failed
  npm run build
  result: succeeded

slice 4 run inspection gate:
  npm test -- --watch=false
  result: 9 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings

slice 5 controls/failure posture gate:
  npm test -- --watch=false
  result: 11 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings

final Angular gate:
  cd src\Presentation\OperatorUi
  npm test -- --watch=false
  result: 13 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings

focused .NET product HTTP/API Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 14 passed, 0 failed, 0 skipped

.NET Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

manual local browser validation issue and fix:
  initial Angular browser calls from http://localhost:4200 to
  http://localhost:5117 failed because CORS preflight returned 405
  after the local CORS bridge fix:
    OPTIONS /product/pipeline/host/readiness with Origin
      http://localhost:4200 returned 204
    GET /product/pipeline/host/readiness with Origin
      http://localhost:4200 returned 200 and Access-Control-Allow-Origin
```

## Final Decision

Decision:

```text
accepted with scoped warnings for product operator Angular SPA over the local
product HTTP host for deterministic archive-shaped workflows
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to provide a local Angular
product operator UI over the accepted product HTTP host for running,
inspecting, diagnosing, and controlling deterministic demo/archive-shaped
product workflows
```

Named warnings:

```text
the Angular app is a local operator UI, not public production deployment
the UI consumes deterministic demo/archive-shaped HTTP workflows, not true
  live network ingestion
the default CORS policy is a local Angular dev-server bridge, not production
  public API security hardening
the UI uses local browser state only for HTTP base URL configuration
rich meteorological radar visualization is not implemented
serving the built Angular SPA from RadarPulse.Http is not implemented
auth, authorization, TLS termination, production CORS hardening, and public
  internet exposure are not claimed
external broker/cloud queue/database adapters remain outside the project plan
cross-machine throughput certification is not claimed
exactly-once end-to-end production delivery is not claimed
accepted milestone 020-029 backend runtime, durable, handler, BFF,
  production pipeline, product contract, HTTP host, and persistence decisions
  are not reopened
```

Recommended next milestone input:

```text
operator UI hardening and integrated local delivery.

Use the accepted Angular operator UI, typed product HTTP client, local
Operator UI CORS bridge, product HTTP host, persistent run history, readiness
route, diagnostics, handler output, capacity evidence, and control routes to
make the UI the stable local product surface. Focus on browser-level smoke
coverage, URL/navigation state for selected runs and tabs, form validation
and UI polish, optional same-origin serving of the built Angular SPA from
RadarPulse.Http, and documentation for a single local product host + UI
workflow. Do not expand the next milestone into true live network ingestion,
external broker/cloud queue/database adapter certification, public production
hosting, auth/TLS/CORS hardening, deployment automation, or exactly-once
delivery unless explicitly reprioritized.
```

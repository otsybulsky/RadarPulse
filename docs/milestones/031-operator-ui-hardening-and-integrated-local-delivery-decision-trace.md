# Milestone 031 Decision Trace

Date: 2026-05-26

Decision: accept operator UI hardening and integrated local delivery over
deterministic archive-shaped workflows with named scoped warnings.

This decision accepts milestone 031's URL-restorable operator state, UI input
validation hardening, control posture hardening, browser smoke harness,
integrated local same-origin delivery through `RadarPulse.Http`, static
Angular asset route fallback, product API route separation, same-origin API
base URL default, local workflow documentation, focused Angular gate, browser
smoke gates, focused .NET HTTP/API/static-delivery gate, Release build, gate
evidence, and handoff update on top of the milestone 030 Angular operator UI
and milestone 029 product HTTP host.

The accepted scope is a hardened local product operator surface. RadarPulse
can now run the Angular UI through the dev-server workflow or through a
single local `RadarPulse.Http` origin that serves both the built UI and the
accepted product API routes. The UI keeps product semantics behind
`RadarPulse.Http`; it does not import .NET assemblies, parse product history
files directly, or call lower-level application, processing, durable, handler,
or BFF internals.

The decision deliberately does not claim true live network ingestion, rich
meteorological radar visualization, public production frontend/backend
deployment, authentication, authorization, TLS termination, production CORS
hardening, external broker/cloud queue/database adapter readiness,
database-backed product history, cross-machine throughput certification, or
exactly-once production delivery. The same-origin delivery accepted here is a
local product delivery path, not production deployment automation or public
hosting readiness.

## Decision Matrix

```text
operator UI hardening and integrated local delivery:
  accepted with scoped warnings

URL-restorable selected run:
  accepted; selected run id can be restored from query state and must still
  be confirmed through product HTTP queries before the UI claims the run
  exists

URL-restorable active inspection tab:
  accepted; summary, batches, sources, handlers, diagnostics, and capacity
  tabs can be restored through query state

selected run not-found posture:
  accepted; missing or unavailable URL-selected runs render product
  not-found/unavailable posture instead of a generic empty state

product HTTP base URL validation:
  accepted; invalid local URL input does not replace the active API base URL

archive run validation:
  accepted; archive file path is validated before the UI issues an avoidable
  bad archive run request

handler lookup validation:
  accepted; source id and field name are validated before handler output
  lookup

operator control posture:
  accepted; controls preserve disabled, loading, blocked, rejected, and
  successful product posture

browser smoke harness:
  accepted; Playwright smoke tests validate critical operator workflows
  through a real browser runtime

dev-server smoke fixtures:
  accepted; deterministic product API route fixtures cover UI integration
  behavior without replacing lower-level product API tests

integrated static UI delivery:
  accepted with warning; RadarPulse.Http can serve the built Angular SPA from
  a configured local static asset root

operator UI route fallback:
  accepted; local UI routes fall back to index.html when static delivery is
  available

product API route separation:
  accepted; /product/pipeline routes are not intercepted by the UI fallback

same-origin API base URL default:
  accepted; when the UI is served by RadarPulse.Http, the default product API
  base URL is the current origin

dev-server API base URL default:
  accepted; the Angular dev-server workflow keeps the existing local
  RadarPulse.Http default instead of assuming localhost:4200 is the API

hosted same-origin smoke:
  accepted; Playwright validates built UI and product API from one local
  RadarPulse.Http origin

frontend build orchestration:
  not accepted as runtime behavior; RadarPulse.Http serves an existing built
  Angular bundle and does not build frontend assets at runtime

local Operator UI CORS bridge:
  carried forward with warning; it remains scoped to Angular dev-server
  development and is not production CORS hardening

product demo/readiness packaging:
  not implemented here; selected as the recommended next milestone input

true live network ingestion:
  not implemented; deterministic demo/archive-shaped workflows remain the
  accepted input contour

rich radar visualization:
  not implemented; the UI remains an operator console over product read and
  diagnostic data

external broker/cloud queue/database adapter:
  not implemented and not planned for this project

deployment and operations:
  not implemented; deployment automation, public hosting, auth/TLS/CORS
  hardening, autoscaling, alert routing, runbooks, and production operator
  procedures remain outside this decision

exactly-once production delivery:
  not claimed; future storage/downstream idempotency gates would be required

Angular gate:
  accepted; 20 Angular tests passed and production build succeeded with no
  warnings

browser smoke gates:
  accepted; dev-server smoke passed 4 tests and hosted same-origin smoke
  passed 1 test

focused .NET HTTP/API/static-delivery gate:
  accepted; 18 focused Release tests passed with no failures or skips

Release build:
  accepted; .NET Release build succeeded with zero warnings and zero errors
```

## Decision Explanations

### Accept URL-Restorable Operator State

Decision: accept query-state restoration for selected run id and active
inspection tab.

Why chosen: milestone 030 had a usable local UI, but selected run and active
tab were volatile browser state. Operators need reloadable and shareable
local links for run investigation, especially when diagnosing readiness,
handler output, diagnostics, or capacity evidence.

Alternatives: keep all state in component memory, add a larger route tree, or
persist selected UI state only in localStorage.

Rejected because: component memory loses operator context on reload; a larger
route tree would add complexity beyond current workflow needs; localStorage
does not give useful local deep links.

Trade-offs/debt: only the selected run and active tab are URL state. More
fine-grained selections, such as selected batch/source/handler field, can be
added later if operator workflows need them.

Review explanation: "The UI can now return to the same run and tab after a
reload without claiming the run exists until the HTTP host confirms it."

### Accept Local Form Validation Hardening

Decision: accept frontend validation for product API base URL, archive run
input, and handler output lookup input.

Why chosen: the accepted backend product API remains the source of truth, but
the UI should prevent obvious local input mistakes from causing avoidable bad
requests or confusing operator state.

Alternatives: rely entirely on backend validation, add a full form framework,
or hide archive/handler inputs until later packaging work.

Rejected because: backend-only validation creates noisy local operator flows;
a full form framework is unnecessary for the current surface; hiding inputs
would weaken accepted milestone 030 workflows.

Trade-offs/debt: validation is intentionally narrow and local. Backend product
responses are still preserved and shown when the HTTP host rejects a request.

Review explanation: "The UI rejects obvious bad local inputs without
overriding backend product truth."

### Accept Control Posture Hardening

Decision: accept stricter disabled/loading/blocked/rejected posture for
operator controls.

Why chosen: controls are product-facing commands. They should not be enabled
when host readiness is unknown, blocked, unavailable, or missing required
request context. Product rejection and blocker reasons must remain visible
near the control surface.

Alternatives: leave milestone 030 control behavior unchanged, require manual
operator discipline, or replace controls with read-only diagnostics.

Rejected because: unchanged behavior was sufficient for initial UI readiness
but not hardened product use; manual discipline is not a product control
strategy; read-only diagnostics would abandon accepted product controls.

Trade-offs/debt: these controls are still deterministic local product
controls, not production runbook workflows. Confirmation flows and
long-running action progress can be part of later demo/readiness polish if
needed.

Review explanation: "Unsafe or impossible control actions are visibly
disabled, and rejected product posture stays in the operator surface."

### Accept Browser Smoke Harness

Decision: accept Playwright browser smoke coverage for critical operator
workflows.

Why chosen: milestone 030 already found a browser-only CORS issue that unit
tests did not catch. Milestone 031 needs a repeatable browser gate for
readiness, run selection, URL state, handler lookup, controls, and
unreachable-host posture.

Alternatives: rely on Angular component tests, use manual browser validation,
or add a large end-to-end test suite against real backend workloads.

Rejected because: component tests do not prove browser navigation and runtime
behavior; manual validation is not repeatable; full backend E2E would be
slower and duplicate product API tests rather than focusing on UI integration.

Trade-offs/debt: the dev-server smoke suite uses deterministic route fixtures
for stable browser evidence. Backend correctness remains covered by .NET
product tests and the hosted smoke covers the same-origin HTTP host path.

Review explanation: "Browser workflows now have automated smoke evidence
instead of depending only on unit tests and manual checks."

### Accept Integrated Local Same-Origin Delivery

Decision: accept serving the built Angular SPA from `RadarPulse.Http` as an
integrated local same-origin delivery path.

Why chosen: milestone 030 left the UI and product host as separate local
origins. A single local host makes the product surface easier to run, demo,
and validate while reducing dependence on the dev-server CORS bridge.

Alternatives: keep dev-server delivery only, require a separate static file
server, put Angular source inside the HTTP project, or add public deployment
packaging now.

Rejected because: dev-server-only delivery is less product-like; a separate
static file server adds another local process; putting Angular source inside
the HTTP project blurs source ownership; public deployment is outside the
milestone's security and operations scope.

Trade-offs/debt: `RadarPulse.Http` serves an existing build output and does
not run frontend build orchestration at runtime. Product demo/readiness
packaging can later add scripts or commands that build the UI before starting
the host.

Review explanation: "One local HTTP origin can now serve both the operator UI
and the product API."

### Accept Product API Route Separation

Decision: accept explicit separation between UI fallback routes and
`/product/pipeline` API routes.

Why chosen: same-origin SPA fallback must not hide product API misses or
change HTTP product semantics. Unknown UI routes can fall back to
`index.html`; product API routes must remain product API routes.

Alternatives: use a broad catch-all fallback for all unknown paths, place the
UI under a subpath only, or require hash routing in Angular.

Rejected because: broad catch-all fallback can mask product API errors;
subpath-only hosting is not needed for local delivery; hash routing would
avoid fallback complexity but make product URLs less clean.

Trade-offs/debt: if a future public host introduces more route namespaces,
the fallback exclusion list may need to expand.

Review explanation: "The SPA fallback helps UI navigation but does not turn
API 404s into index.html."

### Accept Same-Origin API Base URL Default

Decision: accept origin-aware frontend API base URL defaulting.

Why chosen: when the UI is served from `RadarPulse.Http`, the default API base
URL should be the current browser origin. When the UI is served by Angular's
dev server on port 4200, the existing local HTTP host default remains useful.

Alternatives: keep hard-coded `http://localhost:5000`, require users to type
the host URL in same-origin mode, or use only relative URLs everywhere.

Rejected because: hard-coded port 5000 breaks same-origin hosted smoke and
varied local hosts; requiring manual input weakens integrated local delivery;
relative-only URLs would break the dev-server workflow without a proxy.

Trade-offs/debt: runtime override remains available through localStorage. A
future demo package can make the startup URL and port more scripted.

Review explanation: "The same UI works in dev-server mode and hosted mode
without rebuilding."

### Keep Frontend Build Orchestration Out Of Runtime Host

Decision: do not make `RadarPulse.Http` build the Angular app at runtime.

Why chosen: the HTTP host should remain a thin local delivery adapter. Serving
static assets is appropriate; building frontend assets introduces toolchain,
process, and failure concerns into runtime startup.

Alternatives: have `RadarPulse.Http` run npm build on startup, copy Angular
assets during .NET build, or commit built `dist` artifacts.

Rejected because: runtime npm build hides a heavy external dependency inside
host startup; .NET build-copy packaging is better handled as demo/readiness
packaging; committing `dist` would add generated asset churn.

Trade-offs/debt: users must build the Angular app before using integrated
local delivery. Milestone 032 can make that repeatable through scripts or a
packaging command.

Review explanation: "The HTTP host serves the UI; it does not secretly become
a frontend build system."

### Carry Forward Local CORS Bridge As Development-Only

Decision: carry forward the milestone 030 local dev-server CORS bridge as a
development-only path.

Why chosen: developers still need Angular dev-server iteration, and the
bridge keeps browser calls to `RadarPulse.Http` valid during frontend work.

Alternatives: remove CORS now that same-origin delivery exists, or broaden
CORS for arbitrary origins.

Rejected because: removing CORS would make dev-server work harder; broad CORS
would imply a public API posture that the project has not accepted.

Trade-offs/debt: production CORS hardening, auth, TLS, and public exposure
remain out of scope.

Review explanation: "Dev-server CORS remains a local development bridge, not
a production security decision."

### Keep Live, Rich Visualization, External Adapters, Deployment, Security Hardening, And Exactly-Once Out Of Scope

Decision: keep true live network ingestion, rich radar visualization,
external broker/cloud queue/database adapters, public deployment,
auth/TLS/production CORS hardening, operations automation, and exactly-once
delivery outside milestone 031.

Why chosen: milestone 031 validates local product UI hardening and delivery.
The excluded areas have independent architecture, security, operations, and
reliability requirements.

Alternatives: combine UI hardening with live ingestion, public hosting,
security posture, external persistence readiness, and operations packaging.

Rejected because: combining them would blur the accepted local delivery
boundary and create claims not supported by the milestone gate.

Trade-offs/debt: RadarPulse now has a hardened local product surface, but
production deployment, security, live feed, and exactly-once claims still
need their own gates if selected.

Review explanation: "This milestone makes the local product surface stable;
it does not make RadarPulse a public production service."

## Included Surface

Included:

```text
src/Presentation/OperatorUi Angular app hardening
selected run URL state
active detail tab URL state
selected run not-found/unavailable posture from URL state
product API base URL validation
archive run input validation
handler lookup input validation
control disabled/loading/blocked/rejected posture hardening
Playwright browser smoke harness
dev-server browser smoke route fixtures
hosted same-origin browser smoke harness
same-origin product API base URL default
dev-server product API base URL default preservation
RadarPulse.Http static Angular asset delivery options
RadarPulse.Http operator UI static file middleware
RadarPulse.Http operator UI fallback to index.html
explicit /product/pipeline API route fallback exclusion
focused .NET static-delivery tests
OperatorUi README updates for dev-server and integrated local workflows
milestone 031 gate evidence
handoff updates
```

Excluded:

```text
true live radar network ingestion
rich meteorological radar visualization
public production frontend/backend deployment
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
frontend build orchestration inside RadarPulse.Http runtime startup
committing generated Angular dist output
changing milestone 020-030 accepted backend, product, HTTP, or UI decisions
```

## Evidence

Primary source documents:

```text
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-plan.md
docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-gate.md
```

Input evidence from earlier milestones:

```text
milestone 029:
  local product HTTP host, product run/read/diagnostics/capacity/handler/
  readiness/control routes, and deterministic local file-backed product run
  history accepted

milestone 030:
  Angular 21 operator SPA, typed product HTTP client, runtime API base URL
  override, operator overview, selected run inspection tabs, handler output,
  capacity/diagnostic views, controls, failure posture, and local dev-server
  CORS bridge accepted
```

Implementation evidence:

```text
src/Presentation/OperatorUi/src/app/app.ts:
  selected run and active tab URL state, validation helpers, control posture,
  selected run not-found handling

src/Presentation/OperatorUi/src/app/app.html:
  validation messages, hardened handler/control disabled state, URL-selected
  missing-run posture

src/Presentation/OperatorUi/src/app/product/product-api.config.ts:
  origin-aware default product API base URL for dev-server and hosted modes

src/Presentation/OperatorUi/playwright.config.ts:
  dev-server browser smoke configuration

src/Presentation/OperatorUi/playwright.hosted.config.ts:
  hosted same-origin browser smoke configuration through RadarPulse.Http

src/Presentation/OperatorUi/smoke:
  operator UI browser smoke specs for dev-server and hosted workflows

src/Presentation/RadarPulse.Http/Product/RadarPulseProductHttpOptions.cs:
  local operator UI static delivery options

src/Presentation/RadarPulse.Http/Product/RadarPulseOperatorUiStaticDeliveryExtensions.cs:
  static file serving, index.html fallback, product API route exclusion, and
  static asset root validation

src/Presentation/RadarPulse.Http/Program.cs:
  static UI delivery and fallback composition around product endpoint mapping

tests/RadarPulse.Tests/Product/RadarPulseProductHttpHostTests.cs:
  static asset root validation, UI fallback, and product API route separation
  tests

src/Presentation/OperatorUi/README.md:
  dev-server workflow, integrated local host workflow, smoke commands, and
  scope boundary
```

Verification:

```text
slice 1 Angular gate:
  cd src\Presentation\OperatorUi
  npm test -- --watch=false
  result: 18 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings

slice 2 browser smoke gate:
  npm run smoke
  result: 4 passed, 0 failed
  npm test -- --watch=false
  result: 18 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings

slice 3 focused .NET HTTP host gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
  result: 9 passed, 0 failed, 0 skipped
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

slice 4 same-origin/local docs gate:
  npm test -- --watch=false
  result: 20 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings
  npm run smoke
  result: 4 passed, 0 failed
  npm run smoke:hosted
  result: 1 passed, 0 failed

final Angular gate:
  cd src\Presentation\OperatorUi
  npm test -- --watch=false
  result: 20 passed, 0 failed
  npm run build
  result: succeeded, 0 warnings

final browser smoke gates:
  npm run smoke
  result: 4 passed, 0 failed
  npm run smoke:hosted
  result: 1 passed, 0 failed

focused .NET product HTTP/API/static-delivery Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  result: 18 passed, 0 failed, 0 skipped

.NET Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

Browser smoke coverage:

```text
dev-server smoke:
  readiness rendering
  persisted run list rendering
  deep-linked selected run and diagnostics tab
  demo run creation
  URL state preservation for run id and active tab
  handler output lookup
  rejected control posture
  unreachable host posture
  disabled unsafe controls while host is unreachable

hosted same-origin smoke:
  RadarPulse.Http serves the built Angular shell
  RadarPulse.Http product API returns JSON from the same origin
  the UI defaults the product API base URL to the hosted origin
  demo run creation works through the hosted same-origin API
  selected run and capacity tab survive reload through URL state
```

## Final Decision

Decision:

```text
accepted with scoped warnings for operator UI hardening and integrated local
delivery over deterministic archive-shaped workflows
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to use the Angular operator UI
as the hardened local product surface, including browser-smoke validated
workflows and integrated same-origin local delivery through RadarPulse.Http
```

Named warnings:

```text
the integrated UI delivery path is local same-origin hosting through
  RadarPulse.Http, not public production deployment
the hosted smoke uses deterministic demo/archive-shaped product workflows,
  not true live radar network ingestion
the Angular dev-server CORS bridge remains a local development bridge and is
  not production public API security hardening
same-origin local delivery does not add authentication, authorization, TLS
  termination, production CORS hardening, deployment automation, autoscaling,
  alert routing, or operator runbooks
the static asset root expects a built Angular bundle; RadarPulse.Http does
  not perform frontend build orchestration at runtime
external broker/cloud queue/database adapters remain outside the project plan
cross-machine throughput certification is not claimed
exactly-once end-to-end production delivery is not claimed
accepted milestone 020-030 backend runtime, durable, handler, BFF,
  production pipeline, product contract, HTTP host, persistence, and UI
  decisions are not reopened
```

Recommended next milestone input:

```text
Product demo/readiness packaging.

Use the hardened Angular operator UI, same-origin RadarPulse.Http local
delivery, browser smoke gates, product readiness route, deterministic product
demo/archive-shaped workflows, persistent run history, diagnostics, handler
output, capacity evidence, and control routes to make the project repeatable
as a local product demo/readiness package. Focus on scripted local startup,
demo seed/workload profiles, sample workflow documentation, readiness
checklists, history reset/inspection workflow, and a packaged verify command
set. Do not expand the next milestone into true live network ingestion,
external broker/cloud queue/database adapter certification, public production
hosting, auth/TLS/production CORS hardening, deployment automation, or
exactly-once delivery unless explicitly reprioritized.
```

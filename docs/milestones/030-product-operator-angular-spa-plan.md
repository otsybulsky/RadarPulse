# Milestone 030: Product Operator Angular SPA Implementation Plan

Status: in progress.

This plan implements the milestone 030 architecture defined in
`030-product-operator-angular-spa.md`.

The plan is scoped to a local Angular SPA operator UI over the accepted
milestone 029 product HTTP host. It uses the newest stable Angular release at
implementation time, which is Angular 21.x as of 2026-05-26, and it composes
only the accepted product HTTP routes. It does not introduce true live network
ingestion, external broker/cloud queue/database persistence, public production
deployment, auth/TLS/CORS hardening, or exactly-once production delivery
claims.

## Goal

Create a local Angular operator UI for RadarPulse product workflows.

The milestone must prove these concrete outcomes:

```text
an Angular 21 SPA exists under src/Presentation/OperatorUi
the existing .NET CLI project is protected from Angular workspace item churn
the UI can be configured with a RadarPulse.Http base URL
the UI calls accepted product HTTP routes through a typed TypeScript API
  client
the UI can show host/history readiness, run list, latest run, selected run
  detail, diagnostics, capacity evidence, batches, sources, handler output,
  warnings, first blockers, fallback recommendations, and controls
the UI handles unreachable host, loading, empty, not-found, validation,
  blocked history, rejected controls, and request failure states explicitly
Angular tests and production build pass
the focused .NET product HTTP/API gate remains green
documentation and handoff are updated
decision trace is not written until after review
```

Scope note:

```text
milestone 030 validates a local Angular product operator UI over the local
product HTTP host for deterministic archive-shaped workflows. It does not
claim true live ingestion, public deployment, external durable adapters,
auth/TLS/CORS hardening, cross-machine delivery, or exactly-once delivery.
```

## Slice 1: Angular Workspace Scaffold And Packaging Boundary

Status: complete.

Implementation:

```text
scaffold an Angular standalone application under:
  src/Presentation/OperatorUi
use the latest stable Angular CLI/core package versions at scaffold time
enable strict TypeScript settings and routing
commit package-lock.json for deterministic local installs
keep Angular generated files within the OperatorUi folder
update src/Presentation/RadarPulse.Cli.csproj if needed so Angular files and
  future node_modules content are excluded from the .NET CLI project item
  glob
add or preserve local UI ignore rules for dist, .angular, coverage, and
  node_modules
verify Angular scaffold build/test succeeds before moving to product code
```

Tests:

```text
npm install in src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
dotnet build RadarPulse.sln -c Release --no-restore
```

Verification:

```text
npm test -- --watch=false
  result: 2 passed, 0 failed
npm run build
  result: succeeded
dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

Exit criteria:

```text
the repository contains a clean Angular 21 app in src/Presentation/OperatorUi,
and the existing .NET solution still builds without treating the UI workspace
as CLI project content
```

Commit:

```text
Scaffold product operator Angular UI
```

## Slice 2: Typed Product HTTP Client And DTO Mapping

Status: complete.

Implementation:

```text
add TypeScript DTOs for the product API response wrapper and the product
  fields needed by the UI
add an injectable API base URL configuration token/service
add RadarPulseProductApiClient methods for:
  host readiness
  list runs
  latest run
  run detail
  run batches
  batch detail
  run sources
  source detail
  handler output
  diagnostics
  capacity evidence
  demo run creation
  archive run creation
  stop accepting
  drain accepted
  cancel open/release
  reject unsafe fallback
add response-state mapping helpers that preserve product success, not-found,
  validation, blocked, rejected, warnings, and first blocker posture
avoid frontend-only product semantics that are not present in HTTP responses
```

Tests:

```text
Angular unit tests cover base URL joining and every product route method
Angular unit tests cover API response-state mapping for success, not-found,
  blocked, rejected, and connection failure posture
```

Verification:

```text
npm test -- --watch=false
  result: 5 passed, 0 failed
npm run build
  result: succeeded
```

Exit criteria:

```text
the UI has a typed client boundary over milestone 029 HTTP routes, with
tested URL and response mapping but no operator screens depending on mocked
backend internals
```

Commit:

```text
Add product HTTP client for operator UI
```

## Slice 3: Operator Shell, Readiness, Run Creation, And Run List

Status: complete.

Implementation:

```text
replace the scaffold page with an operator console shell
add a dashboard/overview first viewport with:
  host reachability
  history readiness
  latest run summary
  warnings and first blocker
  primary deterministic demo run action
add run list view over persisted product history
add selected/latest run loading behavior
add deterministic demo run form/action through POST /product/pipeline/runs/demo
add archive-shaped run form/action through POST /product/pipeline/runs/archive
  only to the extent supported by the milestone 029 route contract
add empty/loading/offline states for readiness and runs
keep UI layout work-focused, dense, and directly operational
```

Tests:

```text
Angular component tests cover readiness rendering
Angular component tests cover run list rendering and empty state
Angular component tests cover demo run action success and failure mapping
Angular production build succeeds
```

Verification:

```text
npm test -- --watch=false
  result: 7 passed, 0 failed
npm run build
  result: succeeded
```

Exit criteria:

```text
operators can open the UI, see whether RadarPulse.Http/history is usable,
start a deterministic run, and browse persisted run summaries
```

Commit:

```text
Add operator overview and run workflow
```

## Slice 4: Run Detail Inspection Views

Status: complete.

Implementation:

```text
add selected run detail view with product state, readiness, timestamps,
  warnings, first blocker, fallback recommendation, configuration summary,
  and capacity summary
add batches view with provider sequence, batch state, counts, and selected
  batch detail where available
add sources view with source list and source detail where available
add handler output view with source/field selection and absent-output posture
add diagnostics view with product diagnostic fields and support-oriented
  details
add capacity evidence view with compact metrics/tables
use stable responsive dimensions for tables, panes, and controls
```

Tests:

```text
Angular component/helper tests cover run detail view-model mapping
Angular component/helper tests cover absent handler output versus request
  failure
Angular component/helper tests cover diagnostics/capacity empty and populated
  states
Angular production build succeeds
```

Verification:

```text
npm test -- --watch=false
  result: 9 passed, 0 failed
npm run build
  result: succeeded, 0 warnings
```

Exit criteria:

```text
operators can inspect the accepted product read-model surface through the UI
without leaving product vocabulary or using lower-level backend objects
```

Commit:

```text
Add operator run inspection views
```

## Slice 5: Operator Controls And Failure Posture

Status: complete.

Implementation:

```text
add controls panel for:
  stop accepting
  drain accepted
  cancel open/release
  reject unsafe fallback
show accepted, blocked, rejected, failed, and recommended control posture
show the last control outcome near the control surface
ensure destructive/release-oriented actions have clear intent in the UI
surface blocked history readiness and connection failure in controls
normalize HTTP/network errors into product operator state without hiding the
  original status/reason
verify keyboard/focus basics for command controls and route/tab navigation
```

Tests:

```text
Angular tests cover each control route action
Angular tests cover unsafe fallback rejection rendering
Angular tests cover blocked/unreachable host disabling local controls
Angular production build succeeds
```

Verification:

```text
npm test -- --watch=false
  result: 11 passed, 0 failed
npm run build
  result: succeeded, 0 warnings
```

Exit criteria:

```text
the UI can run, inspect, diagnose, and control the product pipeline through
HTTP while preserving explicit failure and fallback posture
```

Commit:

```text
Add operator controls and failure states
```

## Slice 6: Documentation, Gate Evidence, And Handoff

Status: planned.

Implementation:

```text
add UI usage notes or gate documentation for:
  npm install
  npm test
  npm run build
  running RadarPulse.Http locally
  configuring the Angular API base URL
  creating a demo run
  inspecting persisted history, diagnostics, handler output, capacity,
    readiness, and controls
capture focused Angular gate evidence
capture focused .NET product HTTP/API Release gate evidence
capture Release build evidence for the .NET solution if touched by packaging
update docs/handoff.md with completed slices and latest verification
stop before writing decision trace
```

Tests:

```text
npm test -- --watch=false
npm run build
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
dotnet build RadarPulse.sln -c Release --no-restore
```

Exit criteria:

```text
all implementation evidence needed for pre-decision review is captured, and
no decision trace has been written yet
```

Commit:

```text
Capture product operator UI gate
```

## Gate Command Shape

Expected Angular gate:

```text
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
```

Expected focused .NET Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full .NET suite validation can be run if implementation touches shared runtime
behavior beyond presentation packaging. Full browser end-to-end tests are not
required in this milestone unless a real browser regression is found during
manual or component-level validation.

## Stop Point

Stop before writing decision trace.

The milestone should pause for review when:

```text
implementation slices are complete
Angular test/build gate is captured
focused .NET product HTTP/API Release gate is captured
.NET Release build is clean
gate evidence is written
handoff is updated
```

The next step after review will be a separate decision trace and closeout
request.

## Completion State

The milestone currently has:

```text
030-product-operator-angular-spa.md
030-product-operator-angular-spa-plan.md
implementation not started
gate evidence not captured
decision trace not written
closeout not written
```

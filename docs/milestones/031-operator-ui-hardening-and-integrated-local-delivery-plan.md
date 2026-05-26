# Milestone 031: Operator UI Hardening And Integrated Local Delivery Plan

Status: planned.

This plan implements the milestone 031 architecture defined in
`031-operator-ui-hardening-and-integrated-local-delivery.md`.

The plan is scoped to hardening the accepted milestone 030 Angular operator UI
and adding an integrated same-origin local delivery path through
`RadarPulse.Http`. It does not introduce true live network ingestion,
external broker/cloud queue/database persistence, public production
deployment, auth/TLS/production CORS hardening, deployment automation, or
exactly-once production delivery claims.

## Goal

Make the Angular operator UI the stable local product surface for RadarPulse.

The milestone must prove these concrete outcomes:

```text
selected run and active run-detail tab are restorable from URL state
operator forms validate local input before issuing avoidable bad HTTP
  requests
control actions preserve loading, disabled, blocked, rejected, and successful
  product posture
browser-level smoke tests exercise the critical operator UI flows
RadarPulse.Http can serve the built Angular SPA as a local same-origin UI
  without intercepting product API routes
documentation covers dev-server and single-host local workflows
focused Angular, browser smoke, and .NET gates pass
decision trace is not written until after review
```

Scope note:

```text
milestone 031 validates local operator UI hardening and integrated local
delivery over deterministic archive-shaped workflows. It does not claim true
live ingestion, public deployment, external durable adapters, auth/TLS/CORS
hardening, cross-machine delivery, or exactly-once delivery.
```

## Slice 1: URL State And Validation Hardening

Status: complete.

Implementation:

```text
make selected run id restorable from Angular URL state
make active run-detail tab restorable from Angular URL state
preserve not-found/unreachable posture when URL state points at a missing run
validate HTTP base URL before replacing the active product API base URL
validate archive run input before issuing the archive run request
validate handler output lookup input before issuing the handler request
ensure controls remain disabled while host state is unavailable or blocked
ensure control requests expose loading and product rejected/blocked posture
keep all product response reasons visible instead of replacing them with
  frontend-only semantics
```

Tests:

```text
Angular tests cover URL state mapping for selected run and active tab
Angular tests cover invalid HTTP base URL rejection
Angular tests cover archive and handler lookup validation
Angular tests cover disabled/loading control posture
npm run build succeeds
```

Verification:

```text
npm test -- --watch=false
  result: 18 passed, 0 failed

npm run build
  result: succeeded, 0 warnings
```

Exit criteria:

```text
operators can reload or share a local URL for a selected run/tab, and common
bad local inputs are rejected before avoidable HTTP calls without hiding
backend product errors
```

Commit:

```text
Harden operator UI state and validation
```

## Slice 2: Browser Smoke Harness

Status: complete.

Implementation:

```text
add focused browser smoke tooling under src/Presentation/OperatorUi
add smoke fixtures for accepted product API response shapes
mock product HTTP routes in the browser smoke suite where deterministic UI
  behavior is the target
cover readiness, run list, latest run, demo run action, selected run detail,
  tabs, handler output lookup, diagnostics/capacity views, controls, and
  unreachable/request-failure posture
keep the smoke suite focused on UI integration behavior rather than replacing
  product API tests
add an npm script for the browser smoke gate
```

Tests:

```text
npm run smoke
npm test -- --watch=false
npm run build
```

Verification:

```text
npm run smoke
  result: 4 passed, 0 failed

npm test -- --watch=false
  result: 18 passed, 0 failed

npm run build
  result: succeeded, 0 warnings
```

Exit criteria:

```text
critical operator browser workflows have automated smoke coverage in a real
browser runtime
```

Commit:

```text
Add operator UI browser smoke coverage
```

## Slice 3: Integrated Static UI Delivery

Status: complete.

Implementation:

```text
add RadarPulse.Http options for local operator UI static delivery
serve the built Angular SPA from a configured local static asset root
fall back to index.html for operator UI routes
do not intercept /product/pipeline API routes
keep local dev-server CORS behavior unchanged
add focused .NET tests for static UI route fallback and API route separation
document the option names and local delivery boundary in code-level tests or
  README updates
```

Tests:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
dotnet build RadarPulse.sln -c Release --no-restore
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
  result: 9 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

Exit criteria:

```text
RadarPulse.Http can act as a single local host for product API and built
operator UI assets without changing product API semantics
```

Commit:

```text
Serve operator UI from product HTTP host
```

## Slice 4: Same-Origin Smoke And Local Workflow Docs

Status: planned.

Implementation:

```text
add browser smoke coverage for the same-origin local delivery route if the
  implementation can run it deterministically in the local gate
update src/Presentation/OperatorUi/README.md with:
  dev-server workflow
  integrated local same-origin workflow
  browser smoke command
  static UI delivery configuration
  scope boundary
update milestone docs or gate draft with current evidence as slices complete
update docs/handoff.md with completed slices and latest verification
```

Tests:

```text
npm run smoke
npm test -- --watch=false
npm run build
focused .NET HTTP/static-delivery gate
```

Exit criteria:

```text
the repository documents and validates both local workflows: Angular dev
server plus RadarPulse.Http, and a single RadarPulse.Http host serving the
built operator UI
```

Commit:

```text
Document integrated operator UI workflow
```

## Slice 5: Gate Evidence And Handoff

Status: planned.

Implementation:

```text
capture final milestone 031 gate evidence in:
  docs/milestones/031-operator-ui-hardening-and-integrated-local-delivery-gate.md
update this plan with completed slice statuses and verification results
update docs/handoff.md with final pre-decision stop point
do not write the decision trace
do not write closeout
```

Tests:

```text
cd src\Presentation\OperatorUi
npm test -- --watch=false
npm run build
npm run smoke

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Exit criteria:

```text
all implementation evidence needed for pre-decision review is captured, and
no decision trace has been written
```

Commit:

```text
Capture operator UI hardening gate
```

## Gate Command Shape

Expected Angular gate:

```text
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
```

Expected focused .NET Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full .NET suite validation can be run if implementation touches shared runtime
behavior beyond the local HTTP presentation host. The expected backend edits
should be limited to static UI delivery, local route fallback, and HTTP host
tests.

## Stop Point

Stop before writing decision trace.

The milestone should pause for review when:

```text
implementation slices are complete
Angular unit/build gate is captured
browser smoke gate is captured
focused .NET product HTTP/static-delivery Release gate is captured
.NET Release build is clean
gate evidence is written
handoff is updated
```

The next step after review will be a separate decision trace and closeout
request.

## Completion State

The milestone currently has:

```text
031-operator-ui-hardening-and-integrated-local-delivery.md
031-operator-ui-hardening-and-integrated-local-delivery-plan.md
implementation not started
gate evidence not captured
decision trace not written
closeout not written
```

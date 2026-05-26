# Milestone 032: Product Demo/Readiness Packaging Plan

Status: planned.

This plan implements the milestone 032 architecture defined in
`032-product-demo-readiness-packaging.md`.

The plan is scoped to packaging the accepted local product HTTP host,
same-origin Angular operator UI, deterministic product workflows, persistent
run history, and focused gates into a repeatable local demo/readiness
experience. It does not introduce true live network ingestion, external
broker/cloud queue/database persistence, public production deployment,
auth/TLS/production CORS hardening, deployment automation, or exactly-once
production delivery claims.

## Goal

Make RadarPulse repeatable as a local product demo/readiness package.

The milestone must prove these concrete outcomes:

```text
RadarPulse.Http exposes a product demo/readiness view that composes history
  readiness, operator UI static asset posture, and local package warnings
repository-local scripts can start the same-origin local product host, inspect
  readiness, run a deterministic demo workload, inspect/reset local history,
  and run packaged verification
documentation gives an operator a concise first-use, demo, reset, and verify
  workflow
the packaged verification path keeps the accepted Angular, browser smoke,
  hosted smoke, focused .NET HTTP/API, and Release build gates individually
  visible
decision trace is not written until after review
```

Scope note:

```text
milestone 032 validates local product demo/readiness packaging over
deterministic archive-shaped workflows. It does not claim true live ingestion,
public deployment, external durable adapters, auth/TLS/CORS hardening,
cross-machine delivery, or exactly-once delivery.
```

## Slice 1: Product Demo Readiness Surface

Status: complete.

Implementation:

```text
add an HTTP-host-level product demo/readiness model for local package posture
add a product route that reports:
  history storage kind/path/load status/first blocker
  operator UI static asset root configured/resolved/unavailable posture
  local product API route posture
  scoped package warnings and non-claims
keep the existing /product/pipeline/host/readiness route unchanged
return a product API response shape consistent with existing product routes
add focused .NET tests for ready and blocked package posture
ensure /product/pipeline route mapping includes the new readiness route
```

Tests:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
  result: 12 passed, 0 failed, 0 skipped
```

Exit criteria:

```text
operators and scripts can ask RadarPulse.Http whether the local demo package
has usable history and UI static assets without changing accepted history or
product API semantics
```

Commit:

```text
Expose product demo readiness posture
```

## Slice 2: Local Demo Package Script

Status: planned.

Implementation:

```text
add a repository-local PowerShell script under scripts/
support command discovery/help without starting services
support a paths command that prints resolved demo package paths:
  repository root
  operator UI project
  operator UI dist root
  product HTTP project
  local demo history path
  local product URL
support a start command that:
  optionally builds the Angular UI
  starts RadarPulse.Http with explicit local product options
  uses deterministic local file-backed demo history
  serves the built operator UI from the configured static asset root
support a readiness command that calls the product demo/readiness route
support a demo command that calls the accepted deterministic demo run route
support a history command that lists history/readiness/latest-run posture
support a reset-history command that clears only the resolved local demo
  history file after verifying it stays inside the intended demo workspace
keep destructive reset explicit and safe by construction
```

Tests:

```text
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  help
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  paths
```

Exit criteria:

```text
the repository has one discoverable command surface for local startup,
readiness, deterministic demo run, history inspection, and safe history reset
```

Commit:

```text
Add local product demo script
```

## Slice 3: Product Demo Workflow Documentation

Status: planned.

Implementation:

```text
add product-level local demo/readiness documentation under docs/
document first-use prerequisites:
  .NET SDK
  OperatorUi npm dependencies
document same-origin local startup through the package script
document readiness inspection and the meaning of ready/blocked/warning posture
document deterministic demo run creation and run inspection in the UI
document handler output, diagnostics, capacity evidence, and controls posture
document local history inspection and safe reset
document individual fallback commands for manual diagnosis
update OperatorUi README to point at the product demo/readiness package
keep the deterministic local scope warnings visible
```

Tests:

```text
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  help
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  paths
```

Exit criteria:

```text
a new operator can follow repository documentation to start the local product
demo, check readiness, run a deterministic demo, inspect results, reset
history, and diagnose gates
```

Commit:

```text
Document product demo readiness workflow
```

## Slice 4: Packaged Verification Command

Status: planned.

Implementation:

```text
extend the package script with a verify command
run the accepted gate commands in a predictable order:
  Angular unit tests
  Angular production build
  browser smoke
  hosted same-origin browser smoke
  focused .NET product HTTP/API/readiness Release gate
  Release solution build
allow focused verification to be run without hiding individual commands
make command failures stop the packaged verify command with the failing step
preserve manual command documentation for diagnosis
```

Tests:

```text
cd src\Presentation\OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
npm run smoke:hosted

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Exit criteria:

```text
one packaged verify command exists, and the accepted focused gates still pass
as individually diagnosable commands
```

Commit:

```text
Add product demo verification command
```

## Slice 5: Gate Evidence And Handoff

Status: planned.

Implementation:

```text
capture final milestone 032 gate evidence in:
  docs/milestones/032-product-demo-readiness-packaging-gate.md
update this plan with completed slice statuses and verification results
update docs/handoff.md with final pre-decision stop point
do not write the decision trace
do not write closeout
```

Tests:

```text
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  help
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  paths
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1
  verify
```

Exit criteria:

```text
all implementation evidence needed for pre-decision review is captured, and
no decision trace has been written
```

Commit:

```text
Capture product demo readiness gate
```

## Gate Command Shape

Expected packaged command:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

Expected individual Angular gate:

```powershell
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
npm run smoke:hosted
```

Expected focused .NET Release gate:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release `
  --no-restore `
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full .NET suite validation can be run if implementation touches shared runtime
behavior beyond the local product HTTP/demo packaging surface. The expected
backend edits should stay limited to product demo readiness posture, HTTP host
tests, scripts, and documentation.

## Stop Point

Stop before writing decision trace.

The milestone should pause for review when:

```text
implementation slices are complete
product demo/readiness route is implemented and tested
local package script is implemented and smoke-checked
sample workflow documentation is written
packaged verify command is implemented
Angular unit/build gate is captured
browser smoke gate is captured
hosted same-origin browser smoke gate is captured
focused .NET product HTTP/API/readiness Release gate is captured
.NET Release build is clean
gate evidence is written
handoff is updated
```

The next step after review will be a separate decision trace and closeout
request.

## Completion State

The milestone currently has:

```text
032-product-demo-readiness-packaging.md
032-product-demo-readiness-packaging-plan.md
slice 1 product demo readiness surface complete
gate evidence not captured
decision trace not written
closeout not written
```

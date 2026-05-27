# Milestone 033: Product Demo Polish And Portfolio Readiness Plan

Status: planned.

This plan implements the milestone 033 architecture defined in
`033-product-demo-polish-and-portfolio-readiness.md`.

The plan is scoped to polishing the accepted local product demo/readiness
package into a portfolio-ready product demonstration. It does not introduce
true live network ingestion, external broker/cloud queue/database
persistence, public production deployment, auth/TLS/production CORS
hardening, deployment automation, or exactly-once production delivery claims.

## Goal

Make RadarPulse understandable, runnable, inspectable, and verifiable as a
local portfolio product demo.

The milestone must prove these concrete outcomes:

```text
the repository has a first-entry portfolio README or project summary
the accepted local demo package has a concise happy-path walkthrough
the package script and docs reduce first-run friction without hiding blockers
operator-facing wording and visual checkpoint guidance support a first-time
  reviewer
the accepted non-claims remain explicit and visible
the relevant focused gates are captured before decision trace review
decision trace is not written until after review
```

Scope note:

```text
milestone 033 validates product demo polish and portfolio readiness over the
accepted deterministic local demo/readiness package. It does not claim true
live ingestion, public deployment, external durable adapters,
auth/TLS/CORS hardening, cross-machine delivery, or exactly-once delivery.
```

## Slice 1: Portfolio Entrypoint

Status: complete.

Implementation:

```text
add a repository-level README.md as the public-facing project entrypoint
explain what RadarPulse demonstrates in portfolio language
summarize the accepted local architecture:
  OperatorUi
  RadarPulse.Http
  RadarPulse.Cli
  local file-backed product history
  deterministic demo/archive-shaped workflows
point to docs/product-demo-readiness.md for the full operator workflow
show the shortest local demo command sequence
show the packaged verify command
include an honest capability/non-claim summary
avoid duplicating every lower-level milestone detail
```

Tests:

```text
documentation-only slice; no runtime gate required
```

Verification:

```text
README.md added as repository-level portfolio entrypoint
no runtime gate required for documentation-only slice
```

Exit criteria:

```text
a first-time reviewer can open the repository README and understand what the
project is, what the local demo proves, how to start it, how to verify it,
and what is intentionally not claimed
```

Commit:

```text
Add portfolio README
```

## Slice 2: Happy-Path Demo Walkthrough And Script Help

Status: planned.

Implementation:

```text
update docs/product-demo-readiness.md with a concise happy-path walkthrough:
  install UI dependencies when needed
  inspect paths
  reset default demo history
  start the same-origin host
  inspect readiness
  run the default deterministic demo
  inspect the browser UI and history
  run packaged verification
polish scripts/radarpulse-product-demo.ps1 help output so first-use command
  order, docs pointer, default URL, and scope boundary are visible
keep destructive reset explicit and narrow
keep readiness blockers and warning-only posture visible
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
the accepted package has a single happy path for local demo startup and
inspection, and the script help leads a reviewer to that path without hiding
manual diagnostic commands
```

Commit:

```text
Polish product demo walkthrough
```

## Slice 3: Operator Wording And Visual Checkpoints

Status: planned.

Implementation:

```text
review the Angular operator UI for wording that is too internal or hard to
  scan during a portfolio demo
polish labels, headings, empty states, or warning descriptions where the
  change does not alter accepted product API semantics
add or update tests when rendered wording covered by Angular tests changes
add visual checkpoint guidance to the product demo docs:
  readiness
  latest run and persisted run list
  selected run detail
  handler output
  diagnostics
  capacity evidence
  control posture
update OperatorUi README if the workflow pointer needs clearer portfolio
  context
```

Tests:

```text
cd src\Presentation\OperatorUi
npm test -- --watch=false
npm run build

run browser smoke gates if rendered workflow behavior changes:
npm run smoke
npm run smoke:hosted
```

Exit criteria:

```text
the operator UI and docs present the accepted local product workflow in
portfolio-readable language while preserving diagnostics, blockers, warnings,
and control semantics
```

Commit:

```text
Polish operator demo presentation
```

## Slice 4: Gate Evidence And Handoff

Status: planned.

Implementation:

```text
capture milestone 033 gate evidence in:
  docs/milestones/033-product-demo-polish-and-portfolio-readiness-gate.md
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

if script or docs-only changes are the only non-UI edits, no .NET gate is
required unless product HTTP behavior changed

if Angular files changed:
  cd src\Presentation\OperatorUi
  npm test -- --watch=false
  npm run build
  npm run smoke
  npm run smoke:hosted

packaged verify can be run before review if runtime cost is acceptable:
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
Capture product demo polish gate
```

## Gate Command Shape

Expected package script smoke:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
```

Expected individual Angular gate when UI files change:

```powershell
cd src/Presentation/OperatorUi
npm test -- --watch=false
npm run build
npm run smoke
npm run smoke:hosted
```

Expected packaged verification command when full local package verification is
needed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
```

Expected focused .NET Release gate if product HTTP/API behavior changes:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release `
  --no-restore `
  --filter "FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full .NET suite validation can be run if implementation touches shared runtime
behavior beyond the local product demo polish and presentation surface. The
expected edits should stay limited to documentation, package script help, and
Angular presentation wording.

## Stop Point

Stop before writing decision trace.

The milestone should pause for review when:

```text
implementation slices are complete
portfolio README or project summary is written
happy-path demo walkthrough is documented
script help/output first-run polish is complete
operator wording and visual checkpoint polish is complete or explicitly
  judged unnecessary with evidence
package script smoke checks are captured
Angular gates are captured if UI files changed
browser smoke gates are captured if rendered workflow behavior changed
gate evidence is written
handoff is updated
```

The next step after review will be a separate decision trace and closeout
request.

## Completion State

The milestone currently has:

```text
033-product-demo-polish-and-portfolio-readiness.md
033-product-demo-polish-and-portfolio-readiness-plan.md
slice 1 portfolio entrypoint complete
slice 2 happy-path demo walkthrough and script help planned
slice 3 operator wording and visual checkpoints planned
slice 4 gate evidence and handoff planned
gate evidence not written
decision trace not written
closeout not written
```

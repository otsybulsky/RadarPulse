# Milestone 032 Decision Trace

Date: 2026-05-26

Decision: accept product demo/readiness packaging over deterministic
archive-shaped workflows with named scoped warnings.

This decision accepts milestone 032's product demo/readiness HTTP posture,
repository-local package script, scripted same-origin local startup path,
readiness inspection command, deterministic demo run command, history
inspection command, safe local demo history reset, packaged verify command,
product demo/readiness workflow documentation, focused HTTP/API/readiness
gate, Angular gate, browser smoke gates, Release build, gate evidence, and
handoff update on top of the milestone 031 hardened Angular operator UI and
same-origin `RadarPulse.Http` local delivery path.

The accepted scope is a repeatable local product demo/readiness package.
RadarPulse can now build and serve the Angular operator UI from the local
product HTTP host, expose a product-level demo readiness route, create a
deterministic product demo run, inspect persisted local history, safely reset
the default demo history, and run a single packaged verification command over
the accepted focused gates. The package keeps product semantics behind
`RadarPulse.Http`; it does not import .NET assemblies into the UI, parse
product history files from the UI, or call lower-level application,
processing, durable, handler, or BFF internals.

The decision deliberately does not claim true live network ingestion, rich
meteorological radar visualization, public production frontend/backend
deployment, authentication, authorization, TLS termination, production CORS
hardening, external broker/cloud queue/database adapter readiness,
database-backed product history, cross-machine throughput certification, or
exactly-once production delivery. The accepted package is a local
demo/readiness package, not an installer, deployment platform, security
posture, or production operations package.

## Decision Matrix

```text
product demo/readiness packaging:
  accepted with scoped warnings

product demo/readiness HTTP route:
  accepted; GET /product/pipeline/host/demo-readiness reports local package
  posture without changing existing /product/pipeline/host/readiness

product API posture in demo readiness:
  accepted; product pipeline HTTP routes are reported as the accepted API
  route surface under /product/pipeline

history posture in demo readiness:
  accepted; local package readiness composes the existing product run history
  readiness, storage identity, loaded count, warnings, and first blocker

operator UI static asset posture:
  accepted; readiness reports whether static UI delivery is enabled and
  whether the configured built Angular asset root resolves with index.html

package warnings and non-claims:
  accepted; readiness response explicitly carries deterministic local scope
  warnings and production non-claims

repository-local package script:
  accepted; scripts/radarpulse-product-demo.ps1 is the local product demo
  entrypoint

paths command:
  accepted; prints repository root, OperatorUi project, OperatorUi dist,
  RadarPulse.Http project, demo workspace, history path, and local product URL

start command:
  accepted with warning; builds the Angular UI unless skipped and starts
  RadarPulse.Http with explicit local product options

readiness command:
  accepted; calls /product/pipeline/host/demo-readiness and returns non-zero
  when local package readiness is blocked

demo command:
  accepted; calls the accepted deterministic product demo run route

history command:
  accepted; calls accepted history readiness and run-list product routes

reset-history command:
  accepted with guardrail; default destructive reset is constrained to the
  resolved .tmp/product-demo workspace

verify command:
  accepted; runs the accepted Angular, browser smoke, focused .NET, and
  Release build gates in a predictable order

packaged verify command output:
  accepted; the runner prints each underlying command before execution so
  long gates are observable

product demo workflow documentation:
  accepted; docs/product-demo-readiness.md records first-use, startup,
  readiness, demo, inspection, reset, verification, troubleshooting, and
  scope boundaries

OperatorUi README pointer:
  accepted; the UI README points operators to the product-level demo package

frontend build orchestration:
  not accepted as RadarPulse.Http runtime behavior; the package script can
  build the UI before startup, but the HTTP host still serves existing assets

committed Angular dist output:
  not accepted; generated dist output remains build output, not source

database-backed product history:
  not implemented; deterministic local file-backed history remains the
  accepted product history boundary

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

packaged verification gate:
  accepted; packaged verify passed and preserved individual gate output

Angular gate:
  accepted; 20 Angular tests passed and production build succeeded

browser smoke gates:
  accepted; dev-server smoke passed 4 tests and hosted same-origin smoke
  passed 1 test

focused .NET HTTP/API/readiness gate:
  accepted; 21 focused Release tests passed with no failures or skips

Release build:
  accepted; .NET Release build succeeded with zero warnings and zero errors
```

## Decision Explanations

### Accept Product Demo/Readiness HTTP Posture

Decision: accept a product demo/readiness route under
`/product/pipeline/host/demo-readiness`.

Why chosen: milestone 031 made the local UI and product API usable from one
local origin, but scripts and operators still needed a single product-level
answer for demo package posture. The route composes the accepted history
readiness and static UI asset posture into a local package readiness view.

Alternatives: reuse `/product/pipeline/host/readiness` unchanged, add a CLI
only readiness check, or infer package readiness from documentation.

Rejected because: existing host readiness is intentionally history-focused;
CLI-only readiness would not help HTTP/UI callers; documentation is not a
machine-checkable readiness surface.

Trade-offs/debt: demo readiness is local package readiness, not public
production health. If future public deployment is selected, it needs a
separate health/security/operations contract.

Review explanation: "The host can now answer whether the local demo package
has usable history and UI assets without changing existing product routes."

### Accept Explicit Warnings And Non-Claims In Readiness

Decision: accept package warnings and non-claims as part of the demo readiness
response.

Why chosen: the product has many accepted scoped warnings. A local package
can look polished enough that readers may infer production claims unless the
API keeps scope boundaries visible.

Alternatives: keep warnings only in documentation, make warnings failures, or
omit warnings from machine-readable readiness.

Rejected because: documentation-only warnings are easy to miss during demos;
treating accepted scope warnings as failures would make the local package
unusable despite valid deterministic readiness; omitting warnings would
weaken decision trace honesty.

Trade-offs/debt: warning text is product-facing but still simple. If a future
portfolio milestone adds public copy, wording can be polished without
changing the accepted boundary.

Review explanation: "Readiness says what is ready and what is not being
claimed."

### Accept Repository-Local Package Script

Decision: accept `scripts/radarpulse-product-demo.ps1` as the local product
demo/readiness entrypoint.

Why chosen: the project already had all product pieces, but they required
operators to remember scattered commands. A single repository-local script
keeps startup, readiness, demo, history, reset, and verification discoverable
without introducing installer or deployment machinery.

Alternatives: add more CLI subcommands, use only README commands, add a .NET
global tool, or create deployment packaging.

Rejected because: CLI subcommands would mix product runtime behavior with
repository orchestration; README-only commands are not repeatable enough; a
global tool is unnecessary for local portfolio work; deployment packaging is
outside the selected scope.

Trade-offs/debt: the script is PowerShell-first because the working
environment is Windows/PowerShell. Cross-platform shell wrappers can be added
later if portfolio distribution requires them.

Review explanation: "There is now one local command surface for product demo
work instead of a list of remembered commands."

### Accept Scripted Same-Origin Startup

Decision: accept a `start` command that optionally builds the Angular UI and
starts `RadarPulse.Http` with explicit local product options.

Why chosen: milestone 031 intentionally kept frontend build orchestration out
of `RadarPulse.Http` runtime. A script is the right level to compose build
and host startup for a local demo package.

Alternatives: make `RadarPulse.Http` run npm build, require manual build and
host commands, commit dist output, or use an external process manager.

Rejected because: runtime npm build would hide toolchain complexity in the
HTTP host; manual commands are demo friction; committed dist adds generated
churn; an external process manager is unnecessary for local readiness.

Trade-offs/debt: `start` is a foreground local process. More advanced
background process management or packaged app behavior remains future work
if selected.

Review explanation: "The script can prepare the local host, while the HTTP
host stays a thin product delivery adapter."

### Accept Safe Local History Reset

Decision: accept an explicit `reset-history` command constrained to the
resolved `.tmp/product-demo` workspace.

Why chosen: repeatable demos need a clean history option, but destructive
file deletion must be narrow and predictable.

Alternatives: clear history on every startup, allow arbitrary `HistoryPath`
reset, or leave reset as manual file deletion.

Rejected because: startup should not silently destroy demo continuity;
arbitrary-path deletion is too risky; manual deletion is error-prone and
undermines repeatability.

Trade-offs/debt: custom history paths are intentionally not reset unless they
fall inside the demo workspace. Operators who opt into custom paths can
manage them manually or through a future explicit archive/reset command.

Review explanation: "Clean demos are easy, but the reset command cannot
wander outside the local demo workspace."

### Accept Deterministic Demo Command

Decision: accept a `demo` command over the existing product demo run route.

Why chosen: the local package should let an operator create a known product
run without opening the UI first or reconstructing JSON. The command uses the
accepted deterministic product demo route and handler-set values.

Alternatives: require the UI button only, require raw HTTP calls, add new
synthetic workload semantics, or default to archive-file input.

Rejected because: UI-only is less scriptable; raw HTTP is not operator
friendly; new workload semantics would reopen product API decisions; archive
input depends on local data availability.

Trade-offs/debt: archive-shaped workflows remain available through accepted
product routes and UI inputs, but the package's default demo is synthetic and
deterministic.

Review explanation: "A demo run can be created from one local command using
accepted product API semantics."

### Accept History Inspection Command

Decision: accept a `history` command that reads accepted history readiness
and run-list routes.

Why chosen: operators need to understand whether a demo is clean, persisted,
or blocked without directly opening JSON files or guessing paths.

Alternatives: inspect the history file directly, rely only on UI state, or
add a separate persistence debugger.

Rejected because: direct JSON reads bypass product API semantics; UI-only
inspection is not script-friendly; a separate debugger would be unnecessary
for the current product package.

Trade-offs/debt: the command prints a summary, not full run detail. Full
detail remains available through product API/UI surfaces.

Review explanation: "History is inspectable through product routes, not by
hand-editing local storage."

### Accept Packaged Verify Command

Decision: accept a `verify` command that runs the accepted focused gate set.

Why chosen: the project needs one command that proves local product
demo/readiness after changes, while preserving individual gate visibility for
diagnosis.

Alternatives: rely on separate manual commands, run the full test suite every
time, or hide verification inside a CI-only script.

Rejected because: manual commands are easy to miss; full suite is larger than
the local package scope; CI-only validation does not help local portfolio
readiness.

Trade-offs/debt: `verify` is intentionally focused. Full suite validation can
still be run when shared runtime behavior is touched.

Review explanation: "One command proves the local product package, and each
underlying gate still remains visible."

### Accept Observable Script Runner

Decision: accept printing each command before the script executes it.

Why chosen: during validation the initial process runner looked like it was
hanging because it was too quiet around long-running gates. The final runner
prints step names and exact commands so operators can see progress.

Alternatives: keep output minimal, redirect logs to files, or build a richer
task runner.

Rejected because: minimal output created confusing validation posture; log
files are unnecessary for current gates; a richer runner would add avoidable
complexity.

Trade-offs/debt: command output is console-oriented. If future portfolio
packaging needs HTML/JSON reports, that should be a separate polish task.

Review explanation: "Long-running verification is visible and diagnosable."

### Accept Product Demo Workflow Documentation

Decision: accept `docs/product-demo-readiness.md` as the operator-level local
demo/readiness workflow.

Why chosen: milestone docs record architecture and evidence, but an operator
needs a concise workflow for first use, startup, readiness, demo, inspection,
reset, verification, and troubleshooting.

Alternatives: keep workflow only in milestone docs, document only the script
help, or move all docs into the UI README.

Rejected because: milestone docs are too historical for daily use; script
help is too short for workflow context; UI README should not own the full
product package because the package includes HTTP host, history, and gates.

Trade-offs/debt: this is still local technical documentation, not polished
portfolio narrative. The recommended next milestone can convert it into
portfolio-facing presentation material.

Review explanation: "The package has an operator workflow, not only a set of
commands."

### Keep Frontend Build Orchestration Out Of Runtime Host

Decision: keep frontend build orchestration out of `RadarPulse.Http` runtime
startup.

Why chosen: milestone 031 accepted `RadarPulse.Http` as a thin local delivery
adapter. Milestone 032 can script `npm run build` before startup without
turning the HTTP host into a frontend build system.

Alternatives: make the HTTP host run npm build, copy Angular assets during
.NET build, or commit generated dist output.

Rejected because: runtime npm build hides a heavy external toolchain inside
host startup; .NET build-copy behavior is not needed for local readiness;
committed dist output would add generated artifact churn.

Trade-offs/debt: users still need Node/npm for fresh UI builds. That is
accepted for the local package and documented in prerequisites.

Review explanation: "The package orchestrates local build/start; the host
still only serves built assets."

### Keep Live, Rich Visualization, External Adapters, Deployment, Security Hardening, And Exactly-Once Out Of Scope

Decision: keep true live network ingestion, rich radar visualization,
external broker/cloud queue/database adapters, public deployment,
auth/TLS/production CORS hardening, operations automation, and exactly-once
delivery outside milestone 032.

Why chosen: milestone 032 validates local product demo/readiness packaging.
The excluded areas have independent architecture, security, operations, and
reliability requirements.

Alternatives: combine final local packaging with live ingestion, public
hosting, security posture, external persistence readiness, and operations
packaging.

Rejected because: combining them would blur the accepted local package
boundary and create claims not supported by the milestone gate.

Trade-offs/debt: RadarPulse now has a repeatable local product package, but
production deployment, security, live feed, and exactly-once claims still
need their own gates if selected.

Review explanation: "This milestone makes the local product demonstrable and
verifiable; it does not make RadarPulse a public production service."

## Included Surface

Included:

```text
GET /product/pipeline/host/demo-readiness
RadarPulseProductDemoReadiness
RadarPulseProductDemoReadinessItem
history readiness composition into local package readiness
operator UI static asset root readiness
package warnings and explicit non-claims
scripts/radarpulse-product-demo.ps1
package commands:
  help
  paths
  start
  readiness
  demo
  history
  reset-history
  verify
safe default history reset inside .tmp/product-demo
command output for observable verification
docs/product-demo-readiness.md
OperatorUi README pointer to product demo/readiness package
focused product HTTP host tests for demo readiness
milestone 032 gate evidence
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
changing milestone 020-031 accepted backend, product, HTTP, or UI decisions
```

## Evidence

Primary source documents:

```text
docs/milestones/032-product-demo-readiness-packaging.md
docs/milestones/032-product-demo-readiness-packaging-plan.md
docs/milestones/032-product-demo-readiness-packaging-gate.md
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

milestone 031:
  URL-restorable operator state, input/control hardening, browser smoke
  gates, hosted same-origin smoke, and RadarPulse.Http static Angular
  same-origin delivery accepted
```

Implementation evidence:

```text
src/Presentation/RadarPulse.Http/Product/RadarPulseProductDemoReadiness.cs:
  local product demo/readiness DTOs, history readiness composition, operator
  UI static asset readiness, package warnings, and explicit non-claims

src/Presentation/RadarPulse.Http/Product/RadarPulseProductHttpEndpoints.cs:
  GET /product/pipeline/host/demo-readiness route mapping

tests/RadarPulse.Tests/Product/RadarPulseProductHttpHostTests.cs:
  route mapping, ready package posture, missing static UI posture, and
  blocked history posture tests

scripts/radarpulse-product-demo.ps1:
  package command surface for help, paths, start, readiness, demo, history,
  reset-history, and verify

docs/product-demo-readiness.md:
  first-use, startup, readiness, deterministic demo, inspection, reset,
  verification, troubleshooting, and scope boundary workflow

src/Presentation/OperatorUi/README.md:
  pointer from the UI documentation to the product demo/readiness package

docs/milestones/032-product-demo-readiness-packaging-gate.md:
  final gate evidence and pre-decision stop point
```

Verification:

```text
slice 1 focused .NET HTTP host gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarPulseProductHttpHostTests"
  result: 12 passed, 0 failed, 0 skipped

slice 2 package script smoke:
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
  result: passed
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
  result: passed
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 reset-history
  result: passed, absent default demo history reported without deletion

slice 3 package script smoke:
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
  result: passed, includes docs/product-demo-readiness.md pointer
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
  result: passed

slice 4 packaged verify:
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
  result: passed

packaged verify evidence:
  Angular unit tests:
    20 passed, 0 failed
  Angular production build:
    succeeded
  operator UI browser smoke:
    4 passed, 0 failed
  hosted same-origin browser smoke:
    1 passed, 0 failed
  focused .NET product HTTP/API/readiness Release gate:
    21 passed, 0 failed, 0 skipped
  .NET Release build:
    succeeded, 0 warnings, 0 errors

slice 5 package script smoke:
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
  result: passed
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 paths
  result: passed
  powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 verify
  result: passed
```

Package command coverage:

```text
help:
  command discovery, scope boundary, docs pointer

paths:
  repository root, OperatorUi project, OperatorUi dist, RadarPulse.Http
  project, demo workspace, history path, and local product URL

start:
  Angular build unless skipped, RadarPulse.Http startup, deterministic local
  file-backed history, and built operator UI static asset root

readiness:
  GET /product/pipeline/host/demo-readiness

demo:
  POST /product/pipeline/runs/demo with explicit deterministic request
  options

history:
  GET /product/pipeline/host/readiness and GET /product/pipeline/runs

reset-history:
  explicit removal of only the resolved local demo history file inside
  .tmp/product-demo

verify:
  accepted Angular, browser smoke, focused .NET, and Release build gates
```

## Final Decision

Decision:

```text
accepted with scoped warnings for product demo/readiness packaging over
deterministic archive-shaped workflows
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to be demonstrated and
readiness-checked as a repeatable local product package over the accepted
same-origin UI/API host and deterministic product workflows
```

Named warnings:

```text
the product demo/readiness package covers deterministic demo/archive-shaped
  local workflows only
the same-origin host is local RadarPulse.Http delivery, not public production
  deployment
the package does not add authentication, authorization, TLS termination,
  production CORS hardening, deployment automation, autoscaling, alert
  routing, or operator runbooks
history remains deterministic local file-backed product history, not
  database-backed product history
the static asset root expects a built Angular bundle; RadarPulse.Http does
  not perform frontend build orchestration at runtime
external broker/cloud queue/database adapters remain outside the project plan
cross-machine throughput certification is not claimed
exactly-once end-to-end production delivery is not claimed
accepted milestone 020-031 backend runtime, durable, handler, BFF,
  production pipeline, product contract, HTTP host, persistence, and UI
  decisions are not reopened
```

Recommended next milestone input:

```text
Product demo polish and portfolio readiness.

Use the accepted local product demo/readiness package, same-origin
RadarPulse.Http UI/API delivery, deterministic demo workflows, local product
history reset/inspection, readiness route, packaged verify command, browser
smoke gates, and product workflow documentation to prepare RadarPulse as a
portfolio-ready product demo. Focus on final demo polish, portfolio-facing
README/project summary, operator wording, happy-path demo script, visual or
screenshot checkpoints if useful, first-run friction reduction, and an honest
capability/non-claim summary. Do not expand the next milestone into true live
network ingestion, external broker/cloud queue/database adapter
certification, public production hosting, auth/TLS/production CORS hardening,
deployment automation, or exactly-once delivery unless explicitly
reprioritized.
```

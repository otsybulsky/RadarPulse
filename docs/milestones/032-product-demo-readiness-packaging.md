# Milestone 032: Product Demo/Readiness Packaging

Status: planned.

Milestone 032 starts from the closed milestone 031 operator UI hardening and
integrated local delivery milestone.

Milestone 031 closed with this answer:

```text
accepted with scoped warnings for operator UI hardening and integrated local
delivery over deterministic archive-shaped workflows
```

The important next input is:

```text
Product demo/readiness packaging
```

The important shift is:

```text
from:
  RadarPulse has a hardened local Angular operator UI and a local same-origin
  RadarPulse.Http delivery path over the accepted product API

to:
  RadarPulse has a repeatable local product demo/readiness package with
  scripted startup, deterministic demo workload profiles, history
  reset/inspection workflows, readiness checks, and a packaged verification
  command set
```

This document records the milestone 032 concept, architecture boundary,
packaging posture, demo workload posture, readiness checklist posture,
history posture, documentation posture, and expected gate question. The
detailed implementation sequence will be recorded in
`032-product-demo-readiness-packaging-plan.md`.

## Milestone Goal

Milestone 032 should make the accepted local product surface easy to start,
demo, inspect, reset, and verify from the repository.

The milestone should prove these concrete outcomes:

```text
a single documented local demo startup path builds the Angular UI and starts
  RadarPulse.Http with same-origin UI/API delivery
demo workload profiles let an operator run deterministic demo/archive-shaped
  product workflows without rediscovering command syntax
readiness checks explain whether the local product host, history store,
  operator UI, and deterministic workflow surface are usable
history reset and inspection workflows make local demos repeatable without
  manual file hunting
packaged verification commands run the accepted Angular, browser smoke, HTTP,
  and build gates from an operator-friendly entry point
sample workflow documentation explains first-use setup, startup, demo run,
  run inspection, diagnostics, handlers, capacity evidence, controls, reset,
  and verification
the package keeps all accepted scope warnings visible: deterministic
  archive-shaped local workflows, local file-backed history, no true live
  network ingestion, no public production hosting, no auth/TLS/production
  CORS hardening, no external broker/database adapter certification, and no
  exactly-once delivery claim
```

This is a local product demo/readiness packaging milestone over the accepted
milestone 027-031 production pipeline, product API, HTTP host, persistent run
history, Angular operator UI, and local same-origin delivery surfaces. It is
not a new processing architecture milestone, not a live-ingestion milestone,
not an external broker/cloud queue/database adapter milestone, not a public
deployment milestone, and not a security hardening milestone.

## Selected Product Surface

Milestone 032 keeps the same product boundary accepted in milestone 031.

The selected surface is:

```text
product HTTP host:
  src/Presentation/RadarPulse.Http

operator UI:
  src/Presentation/OperatorUi

product CLI:
  src/Presentation/RadarPulse.Cli

local product API:
  accepted /product/pipeline routes from milestone 029

demo/readiness delivery:
  RadarPulse.Http serves the built Angular UI and product API from one local
  origin

history:
  deterministic local file-backed product run history

verification:
  Angular unit/build gate
  browser smoke gate
  hosted same-origin browser smoke gate
  focused product HTTP/API Release gate
  Release solution build
```

The package may add scripts, project-local documentation, sample settings, and
thin helper commands. It must not duplicate lower-level runtime decisions or
make hidden product semantics outside the accepted product service/API
surface.

## Architecture Boundary

Safe in milestone 032:

```text
add repository-local scripts for demo startup, readiness checks, history
  reset/inspection, and packaged verification
add a stable local demo settings/profile shape for RadarPulse.Http and the
  operator UI static asset path
add deterministic demo workload profile names that map to accepted product
  demo/archive-shaped workflows
add product-facing readiness/check output that composes existing host
  readiness, history readiness, UI static asset posture, and demo workflow
  preconditions
add focused tests for any new script-discoverable configuration or product
  packaging service behavior
add documentation for first-use setup, startup, demo run, run inspection,
  diagnostics, handler output, capacity evidence, controls, reset, and verify
add milestone gate evidence and handoff updates as implementation slices
  complete
```

Not safe in milestone 032 unless explicitly reprioritized:

```text
silently changing accepted product HTTP route semantics
silently changing accepted product DTO meaning to suit scripts or docs
making RadarPulse.Http perform frontend build orchestration at runtime
committing generated Angular dist output as the product package
reaching around the product API into lower-level runtime services for demo
  semantics
replacing deterministic local file-backed history with a database
claiming true live network ingestion from deterministic demo/archive-shaped
  workflows
claiming public production deployment from local startup scripts
claiming auth, authorization, TLS termination, or production CORS hardening
claiming external broker/cloud queue/database adapter readiness
claiming deployment automation, autoscaling, alert routing, runbooks,
  cross-machine throughput, or exactly-once production delivery
reopening accepted milestone 020-031 backend runtime, durable, handler, BFF,
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
autoscaling, alert routing, pager policy, and production operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claims
rich meteorological radar visualization beyond product read/diagnostic views
reopening milestone 020-031 backend architecture decisions
```

## Packaging Posture

Milestone 032 should add a repository-local product demo package, not a public
deployment artifact.

The selected packaging posture is:

```text
startup:
  a script or documented command builds the Angular UI and starts
  RadarPulse.Http with the configured static UI asset path

configuration:
  local demo settings are explicit and inspectable
  history path and UI static asset path are visible
  default local URL is deterministic unless the caller overrides it

verification:
  a packaged command runs the accepted focused gates in a predictable order
  each gate remains individually runnable for diagnosis

repeatability:
  local history can be reset before a demo
  local history can be inspected after a demo

claim boundary:
  the package is a local demo/readiness package, not installer packaging,
  production deployment automation, or hosting platform certification
```

The implementation should prefer thin scripts and product-level helpers over
new long-lived abstractions unless the codebase needs a reusable service for
readiness or history inspection.

## Demo Workload Profiles

Milestone 032 should make deterministic product workflows easy to run without
changing their accepted semantics.

Expected profile posture:

```text
demo profile:
  uses the accepted product demo run workflow
  does not require external archive data
  produces persisted run history that the UI can inspect

archive-shaped profile:
  uses the accepted product archive-shaped run workflow when a caller supplies
  a valid local archive file path
  keeps local path validation and product API errors visible

read-only profile:
  starts the host/UI and inspects readiness/history without creating a run

reset profile:
  removes or archives local demo history through an explicit command
```

The milestone does not need to invent new radar analytics. It should package
the accepted deterministic product workflows in a way that is easy to repeat
locally.

## Readiness Checklist Posture

Milestone 032 should give a clear product-level answer before and after a
local demo.

The checklist should cover:

```text
host:
  RadarPulse.Http starts and exposes product routes

history:
  local file-backed product history is configured, loadable, and not blocked

operator UI:
  built Angular bundle exists at the configured static asset root
  same-origin UI shell is reachable without intercepting product API routes

workflow:
  deterministic demo run can be created or latest persisted run can be
  inspected

diagnostics:
  readiness, first blockers, warnings, diagnostics, handler output, capacity
  evidence, and controls remain visible through product API/UI surfaces

verification:
  packaged gate command reports Angular, browser smoke, HTTP/API, and build
  status
```

The checklist should distinguish blocked, unavailable, missing configuration,
and warning-only posture. It should not turn scoped warnings into failures
unless the local package cannot run the accepted deterministic workflow.

## History Reset And Inspection

Milestone 032 should make local demos repeatable.

Expected history posture:

```text
inspect:
  show configured history storage path, schema/version posture, load status,
  summary counts, latest run id, and first blocker if blocked

reset:
  explicitly remove or replace local demo history for a clean demo start
  avoid deleting paths outside the configured demo history boundary
  require a clear command or option for destructive reset behavior

preserve:
  normal startup does not clear history implicitly
  operators can choose between persisted demo continuity and clean reset
```

If scripts perform file deletion, the implementation must verify resolved
paths stay inside the intended local demo workspace.

## Documentation And Workflow

Milestone 032 should add product-level documentation for an operator-facing
local demo.

The documentation should cover:

```text
first-use prerequisites:
  .NET SDK
  Node/npm dependencies for OperatorUi

start:
  build UI
  start RadarPulse.Http with local demo settings
  open the same-origin UI

demo:
  inspect readiness
  create deterministic demo run
  inspect latest run and run detail
  inspect batches, sources, handlers, diagnostics, and capacity evidence
  exercise safe controls and observe rejected/blocked posture

history:
  inspect local history
  reset local demo history
  rerun demo from a clean state

verify:
  run packaged verification
  run individual gates when diagnosis is needed

scope:
  repeat the accepted local deterministic boundary and non-claims
```

The documentation should be concise enough to be usable during a demo, while
the milestone docs retain the architecture and evidence record.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to be demonstrated and readiness-checked as a repeatable
local product package over the accepted same-origin UI/API host and
deterministic product workflows?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for local product demo/readiness packaging over
deterministic archive-shaped workflows; no true live network ingestion,
external broker/database adapter, public production deployment,
auth/TLS/production CORS hardening, deployment automation, cross-machine
delivery, or exactly-once delivery is claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
demo startup script or command path
readiness check command path
history reset/inspection workflow
packaged verification command set
same-origin UI/API local workflow documentation
Angular unit/build gate
browser smoke and hosted same-origin smoke gates
focused .NET product HTTP/API/readiness packaging Release gate
Release solution build
handoff updates
```

Full .NET suite validation can be run if implementation touches shared runtime
behavior beyond the product/presentation packaging surface. The expected
edits should stay limited to local scripts, documentation, product HTTP
packaging/readiness helpers, and focused tests.

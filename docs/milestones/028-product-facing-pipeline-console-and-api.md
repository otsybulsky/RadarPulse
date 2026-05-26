# Milestone 028: Product-Facing Pipeline Console And API

Status: planned.

Milestone 028 starts from the closed milestone 027 production pipeline
integration milestone.

Milestone 027 closed with this answer:

```text
accepted with scoped warnings for production pipeline integration over
deterministic archive-shaped backend workloads
```

The important next input is:

```text
product-facing completion
```

The important shift is:

```text
from:
  RadarPulse has one accepted production-shaped backend pipeline that can run
  deterministic archive-shaped workloads, publish BFF read models, expose
  operator readiness, recovery posture, fallback posture, and capacity
  evidence

to:
  RadarPulse has a selected product-facing delivery surface that lets a user
  run, inspect, diagnose, and control that accepted pipeline without knowing
  the lower-level runtime, durable, handler, or benchmark internals
```

This document records the milestone 028 concept, architecture boundary,
product workflow, API/console shape, read model posture, control posture,
release packaging, and expected gate question. The detailed implementation
sequence will be recorded in
`028-product-facing-pipeline-console-and-api-plan.md`.

## Milestone Goal

Milestone 028 should turn the accepted production-shaped backend pipeline into
the first intended user-facing RadarPulse product surface.

The milestone should prove these concrete outcomes:

```text
a product-level pipeline workflow can start an archive-shaped RadarPulse run
  through the accepted milestone 027 production pipeline
stable product DTOs expose run summary, operator readiness, diagnostics,
  batches, sources, handler output, configuration provenance, and capacity
  evidence
a console-facing operator workflow can run and inspect the product pipeline
  without using benchmark-oriented commands
a thin API-facing contract can expose the same product run/read/control shape
  without reimplementing processing
product controls expose stop-accepting, drain-accepted, cancel-open/release,
  and unsafe-fallback rejection through milestone 027 posture
documentation and focused Release gates prove the product workflow end to end
the product surface stays honest about archive-shaped scope, local file
  durability, absent frontend SPA, absent true live network ingestion, absent
  deployment automation, and absent exactly-once delivery claims
```

This is a product delivery milestone over the accepted backend pipeline. It is
not a new processing architecture milestone, not a live-ingestion milestone,
not an external broker/cloud queue/database adapter milestone, not a frontend
SPA milestone, and not a deployment platform milestone.

## Selected Product Surface

Milestone 028 selects a narrow product-facing pipeline console/API surface.

The selected surface is:

```text
product workflow:
  run deterministic archive-shaped RadarPulse pipeline workloads
  inspect latest and historical in-process product runs
  inspect batches, source summaries, handler outputs, diagnostics, readiness,
    blockers, fallback recommendations, and capacity evidence
  execute explicit operator controls against product-visible pipeline state

delivery adapters:
  console-facing workflow in the existing .NET presentation layer
  API-facing DTO/contract layer that can be hosted by a thin HTTP adapter
    without changing processing or read-model internals
```

The first product surface deliberately uses deterministic archive-shaped input.
It can support synthetic/demo batches for focused gates and NEXRAD
archive-derived batches where existing archive projectors are available. It
must not present archive-shaped operation as true live network ingestion.

## Architecture Boundary

Safe in milestone 028:

```text
add product-level request/result/read/control DTOs over the accepted pipeline
add a product pipeline application service/facade that composes:
  RadarProcessingProductionPipelineRunner
  RadarProcessingProductionPipelineControlCoordinator
  RadarProcessingBffReadModelStore
  RadarProcessingProductionPipelineCapacityEvidence
add product run identity, run listing, latest-run, run detail, batch, source,
  handler output, diagnostics, and capacity evidence query contracts
add product operator control contracts for stop, drain, cancel/release, and
  unsafe fallback rejection
add console-facing product commands or command helpers that use the product
  service instead of benchmark-specific output
add API-facing endpoint/DTO contracts that stay transport-thin and reuse the
  same product service
add focused tests for DTO stability, product run workflow, read queries,
  controls, console/API contract shape, and documentation examples
```

Not safe in milestone 028 unless explicitly reprioritized:

```text
silently changing milestone 027 production pipeline defaults
silently changing ordered processing, rebalance, or handler merge semantics
silently replacing file-based durable adapter posture with external broker,
  cloud queue, or database persistence
claiming true live network ingestion from deterministic archive-shaped input
claiming production HTTP hosting readiness without explicit host gates
building a frontend SPA or visual radar UI as part of this milestone
claiming deployment automation, autoscaling, alert routing, runbooks, or
  exactly-once production delivery
hiding fallback by switching provider, durable adapter, or execution mode
```

Out of scope for this milestone:

```text
external broker/cloud queue/database adapter certification
true live network feed subscription and live backfill policy
frontend SPA or rich visualization application
deployment platform automation
autoscaling, alert routing, pager policy, and operator runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claims
reopening milestone 020-027 backend architecture decisions
```

## Product Workflow

The product workflow should be phrased around user intent, not benchmark
mechanics.

The main workflow should answer:

```text
start a RadarPulse pipeline run from selected deterministic input
see whether the run is ready, completed, blocked, failed, canceled, or
  waiting for explicit recovery
see the first blocking reason and recommended action
see processed/committed batch counts and source-level outputs
see handler output availability and handler execution posture
see retained pressure and release health
see configuration values and whether they came from profile defaults or
  explicit overrides
see capacity evidence for the run
query latest run, run detail, batches, sources, handler outputs, and
  diagnostics using product DTOs
request explicit controls for stop accepting, drain accepted work, cancel
  open work and release, or reject unsafe fallback
```

The workflow should not require a user to understand provider queue telemetry,
durable envelope internals, processing core mutation rules, or benchmark
threshold vocabulary. Those remain available only through diagnostics fields
where they are needed to explain readiness and blockers.

## API Contract Shape

The API-facing contract should be transport-stable and not tied to HTTP
framework types.

Expected product API contract groups:

```text
run commands:
  create/run product pipeline request
  product run accepted/completed/blocked response

run queries:
  list runs
  latest run
  run detail
  batch list
  batch detail
  source list
  source detail
  handler output
  diagnostics
  capacity evidence

control commands:
  stop accepting
  drain accepted
  cancel open and release
  reject unsafe fallback
```

The transport adapter, if added in this milestone, should be thin: it maps HTTP
or console input to product service requests and maps product responses back to
stable DTOs. It must not create a second pipeline implementation.

## Console Surface

The console-facing surface should be product-oriented rather than
benchmark-oriented.

Expected console posture:

```text
commands use product terms such as run, status, batches, sources, handlers,
  diagnostics, and controls
output includes run id, readiness, state, first blocker, fallback
  recommendation, processed/committed counts, handler posture, capacity
  evidence, and read-model availability
unsafe or unsupported requests fail closed with a direct product-facing reason
```

The console does not need to become an interactive TUI in this milestone. A
small set of deterministic commands with stable output is enough to prove the
first product delivery surface.

## Read Model And DTO Posture

Milestone 024 accepted BFF read models; milestone 027 connected them to the
production pipeline. Milestone 028 should not expose those internal objects as
the final product contract without review.

The product DTO layer should:

```text
preserve stable product field names
collapse lower-level readiness details into product-level readiness and first
  blocker fields
keep configuration provenance visible
keep warnings and scoped limitations visible
expose handler descriptors and handler values in a frontend/API-friendly
  shape
preserve enough diagnostic detail for support and operator workflows
avoid leaking mutable internal collections or lower-level implementation
objects
```

## Control And Recovery Posture

Product controls should reuse milestone 027 rollback/fallback semantics.

Accepted product controls:

```text
stop accepting:
  make accepted durable state visible without accepting new work

drain accepted:
  complete accepted work in provider sequence where possible

cancel open and release:
  cancel open work and surface release cleanup posture

reject unsafe fallback:
  reject hidden borrowed-provider or alternate-adapter fallback
```

Controls may return recommendations rather than mutating real long-running
background infrastructure where the milestone uses deterministic in-process
runs. The user-facing contract must still make the action, result state,
first blocker, and recommendation explicit.

## Documentation And Packaging

Milestone 028 should include product-facing documentation that explains:

```text
what the product surface does
how to run the first deterministic product workflow
how to inspect run status, outputs, diagnostics, and handler results
how to interpret blocked states and fallback recommendations
what is still outside scope
```

Release packaging should be scoped to the repository's current .NET solution.
No installer, deployment automation, or hosted production environment is
claimed unless explicitly added later.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to expose the accepted production-shaped backend pipeline
as a usable product-facing console/API surface for deterministic
archive-shaped radar workflows, with stable DTOs, run/read/control workflows,
operator diagnostics, handler output visibility, and documentation?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for product-facing pipeline console/API completion
over deterministic archive-shaped workloads; no frontend SPA, true live
network ingestion, external broker/database adapter, deployment automation,
cross-machine delivery, or exactly-once production delivery is claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
product DTO stability and mapping from accepted pipeline/read models
product run workflow over deterministic archive-shaped input
query surface for latest run, run detail, batches, sources, handler output,
  diagnostics, and capacity evidence
operator control/fallback contract
console-facing product command output
API-facing contract or thin endpoint mapping if the HTTP adapter is included
documentation examples
focused Release product suite
Release build
```

Full-suite validation can be run if implementation touches shared runtime
behavior beyond the product orchestration and presentation surface.

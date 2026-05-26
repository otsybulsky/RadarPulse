# Milestone 027: Production Pipeline Integration

Status: planned.

Milestone 027 starts from the closed milestone 026 persistent durable adapter
readiness milestone.

Milestone 026 closed with this answer:

```text
accepted with scoped warnings for persistent durable adapter readiness over
deterministic archive-shaped MVP workloads, stopping milestone 026 at the
deterministic local file-based adapter
```

The important next input is:

```text
production pipeline integration
```

The important shift is:

```text
from:
  accepted runtime/archive building blocks are proven in focused milestone
  surfaces: queued-owned provider defaults, async execution, ordered
  processing and rebalance commit, durable envelopes, file-based persistence,
  handler output/BFF read models, and handler delta/merge semantics

to:
  one deployable operational backend pipeline composes those accepted pieces
  under explicit production-shaped configuration, diagnostics,
  restart/recovery validation, rollback/fallback posture, and representative
  capacity evidence
```

This document records the milestone 027 concept, architecture boundary,
pipeline model, configuration posture, operational diagnostics, restart and
rollback posture, capacity evidence, and expected gate question. The detailed
implementation sequence will be recorded in
`027-production-pipeline-integration-plan.md`.

## Milestone Goal

Milestone 027 should connect the accepted runtime provider posture into an
end-to-end operational backend pipeline.

The milestone should prove these concrete outcomes:

```text
a named production pipeline profile composes the accepted
  RadarProcessingRuntimeArchiveBaseline defaults
pipeline configuration resolves defaults and overrides with operator-visible
  provenance
archive-shaped ingestion can run through durable processing, handler output,
  and BFF read models as one pipeline surface
the deterministic file-based durable adapter is usable as the local
  persistence baseline for restart/recovery gates
startup, steady processing, completion, blocking, recovery, and terminal
  cleanup states are visible through one operator summary
rollback and fallback posture is explicit for stopped, failed, poison,
  claimed, canceled, and incompatible durable states
representative workload gates capture correctness, readiness, retained
  pressure cleanup, handler output compatibility, and capacity evidence
the pipeline can be validated without claiming broker durability,
  cross-machine delivery, production HTTP hosting, frontend readiness, or
  exactly-once production delivery
```

This is an integration milestone. It is not a Kafka, RabbitMQ, cloud queue,
database adapter, frontend, production HTTP BFF host, deployment platform, or
exactly-once delivery milestone.

## Architecture Boundary

Safe in milestone 027:

```text
add a named production pipeline profile that composes accepted backend
  defaults without changing their lower-level decisions
add application-level pipeline options for provider, execution, ordered
  active batches, durable adapter, handler mode, diagnostics, and workload
  limits
add configuration resolution with default/override provenance and validation
add a production-shaped archive/runtime pipeline runner over deterministic
  archive-shaped input
compose the pipeline with the milestone 026 file-based durable adapter
compose processing results through milestone 024 read models and milestone
  025 handler delta/merge where eligible
add a unified operator summary for pipeline state, durable state,
  processing completeness, handler posture, retained pressure, and first
  blocking reason
add restart/recovery gates that recreate pipeline services around persisted
  durable state
add rollback/fallback plans and machine-readable fallback recommendations
add representative Release gates and local capacity evidence
```

Not safe in milestone 027 unless explicitly reprioritized:

```text
silently changing RadarProcessingCoreOptions.Default
silently changing the accepted queued-owned provider/execution defaults
silently changing ordered commit semantics for processing, rebalance, or
  handler merge
silently replacing the file-based durable adapter with Kafka, RabbitMQ,
  cloud queue, or database-backed persistence
claiming production broker retention or cross-machine delivery from local
  file persistence
claiming exactly-once production delivery without downstream idempotency,
  production storage, and adapter-specific evidence
building a frontend application
building a production HTTP API host unless it is explicitly selected as a
  separate surface
hiding a fallback to borrowed provider mode
retrying claimed envelopes after restart without explicit policy posture
```

Out of scope for this milestone:

```text
Kafka, RabbitMQ, cloud queue, or database-backed production adapter
production broker operations or retention certification
production HTTP BFF host
frontend application
true live network ingestion from an external radar feed
deployment platform automation, autoscaling, alert routing, and pager policy
cross-machine throughput certification
exactly-once end-to-end production delivery claims
full product-facing completion
```

## Pipeline Shape

Milestone 027 should introduce a narrow application-level production pipeline
surface. The lower-level processing and durable components remain reusable,
but the pipeline gives operators one place to start, inspect, validate, stop,
recover, and reason about a run.

The production pipeline should compose:

```text
ingestion:
  deterministic archive-shaped input selected from cache/file/runtime
  provider surfaces already accepted by earlier milestones

runtime construction:
  RadarProcessingRuntimeArchiveBaseline-owned construction for queued-owned
  provider defaults plus async shard transport defaults

ordered work:
  accepted provider-sequence ordered processing commit
  accepted ordered rebalance/topology commit where rebalance is enabled
  accepted handler delta/merge path for explicitly mergeable handlers
  explicit sequential fallback for snapshot-only stateful handlers

durability:
  milestone 026 deterministic file-based durable adapter as the local
  restart/recovery baseline
  durable envelope state remains the source of recovery truth

output:
  milestone 024 processing run read models and BFF application read surface
  handler catalog, batch detail, source output, handler output, and
  diagnostics remain queryable after pipeline completion

operator view:
  one pipeline summary that includes readiness, progress, first blocker,
  durable state, handler posture, retained pressure, fallback posture, and
  capacity evidence
```

The pipeline should not become a second implementation of processing. It
should orchestrate the accepted pieces and expose the missing production-
shaped contract around them.

## Configuration Posture

Milestone 027 should make production-shaped configuration explicit and
auditable.

The accepted defaults inherited by the pipeline are:

```text
provider mode:
  queued-owned
provider overlap:
  producer-consumer
retention strategy:
  pooled-copy
provider queue capacity:
  8
retained-byte budget:
  536870912
startup retained payload prewarm:
  enabled and visible
execution:
  async shard transport
worker count:
  4
worker queue capacity:
  8
ordered active batch capacity:
  4
durable adapter:
  deterministic local file-based persistence for this milestone
```

Configuration resolution should report:

```text
resolved value
source:
  default
  profile
  explicit override
  test harness
validation status
warnings
first invalid option
first unsafe fallback request
```

The profile may expose narrower capacity controls for tests and local gates,
but it should not normalize unsafe values into safe-looking defaults. Invalid
or unsupported combinations should fail closed with an operator-readable
reason.

## Ingestion Boundary

Milestone 027 should prove a live-shaped archive/runtime ingestion path
without claiming true live network ingestion.

Accepted ingestion scope:

```text
deterministic archive cache or file input
runtime-shaped batch acceptance into durable envelopes
bounded workload selection for focused tests
representative local cache rows for capacity evidence when available
processing completeness and checksum evidence
restart/recovery using persisted durable state
```

Not accepted as done by this milestone:

```text
network feed subscription
external radar feed retry/backfill policy
multi-process deployment topology
cross-machine delivery
operator-managed live incident response
production SLA or SLO commitment
```

This keeps the milestone focused on the operational backend contract instead
of mixing it with external live-feed reliability.

## Durability And Recovery

The milestone 026 file-based adapter becomes the local persistence baseline
for milestone 027 gates.

Expected recovery posture:

```text
pending work remains claimable after restart
claimed work remains blocking until explicit recovery policy acts
completed uncommitted work remains ordered commit input
failed, abandoned, poison, canceled, and incompatible states remain visible
released work remains terminal
operator summary identifies first blocking durable state and reason
pipeline restart does not erase durable state or silently start fresh
```

The pipeline can offer recovery recommendations, but it must not execute
unsafe recovery automatically. Recovery actions should remain explicit where
they can change externally visible progress.

## Rollback And Fallback Posture

Milestone 027 should define rollback and fallback as operator-visible
posture, not hidden behavior.

Rollback/fallback should answer:

```text
can the pipeline stop accepting new work
can it drain already accepted work
can it cancel open work and release retained resources
can it preserve durable state for later recovery
can it report the first blocking batch and state
can it recommend explicit recovery actions for claimed, failed, abandoned,
  poison, canceled, and incompatible persisted states
can it reject unsafe fallback requests such as silent borrowed-provider
  fallback or fresh-start over incompatible durable state
```

Fallback to a different provider, durable adapter, or execution mode must be
explicitly configured and reported. The accepted default path remains
queued-owned plus async execution plus ordered commit plus file-based durable
persistence for this milestone.

## Diagnostics And Readiness

The pipeline should expose a unified readiness and operations summary.

The summary should report at least:

```text
pipeline profile name
configuration validity and first invalid option
resolved runtime defaults and provenance
ingestion source identity and workload limits
run state
accepted, processed, committed, failed, poison, canceled, and released counts
processing completeness
provider sequence progress
durable adapter kind, schema, storage identity, compatibility, and blocker
handler execution posture:
  handler-free
  mergeable delta/merge
  snapshot-only sequential fallback
  unsupported blocking
retained pressure summary
release health
first blocking batch id
first blocking provider sequence
first blocking state
first blocking reason
fallback recommendation
capacity evidence status
warnings
```

Readiness should fail closed when configuration is invalid, durable state is
incompatible, retained pressure remains non-terminal, processing completeness
fails, handler output conflicts, or a blocking durable state requires an
operator action.

## Capacity Evidence

Milestone 027 should capture representative local capacity evidence for the
integrated pipeline.

Evidence should include:

```text
elapsed time
allocated bytes
accepted batch count
processed batch count
committed batch count
handler output posture
durable adapter state counts
terminal retained pressure
processing completeness
first blocking reason if not successful
configuration contour
```

The capacity gate is local and representative. It should not be described as
cross-machine throughput certification or broker durability certification.

## Gate Question

The milestone gate should answer:

```text
Is RadarPulse ready to run the accepted backend runtime as one
production-shaped operational pipeline over deterministic archive-shaped
workloads, with deployable defaults, durable restart/recovery validation,
operator diagnostics, rollback/fallback posture, and representative capacity
evidence?
```

The expected acceptable answer is likely:

```text
yes with scoped warnings for deterministic archive-shaped production
pipeline integration; no external broker adapter, true live network
ingestion, production HTTP host, frontend, cross-machine delivery,
autoscaling/runbook certification, or exactly-once production delivery is
claimed
```

## Expected Evidence

The milestone should capture focused evidence for:

```text
configuration resolution and provenance
invalid or unsafe configuration fail-closed behavior
pipeline run over deterministic archive-shaped input
durable adapter-backed restart/recovery inside the pipeline
claimed/failed/poison/canceled state diagnostics and fallback
recommendations
handler delta/merge and snapshot-only fallback compatibility through the
pipeline output surface
BFF read-model availability after pipeline completion
retained pressure cleanup and release health
representative local capacity row
Release build and focused Release pipeline suite
```

Full-suite validation can be run if implementation touches shared runtime
behavior beyond the new pipeline orchestration surface. Known historical
allocation-sensitive benchmark caveats should remain separately attributed if
they appear outside the focused pipeline gate.

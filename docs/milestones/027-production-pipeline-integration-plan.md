# Milestone 027: Production Pipeline Integration Implementation Plan

Status: planned.

This plan implements the milestone 027 architecture defined in
`027-production-pipeline-integration.md`.

The plan is scoped to a deterministic archive-shaped operational backend
pipeline. It composes the accepted runtime/archive provider baseline,
ordered processing and rebalance commit contracts, durable file-based
adapter, handler output/BFF read models, and handler delta/merge semantics.
It does not introduce Kafka, RabbitMQ, cloud queue, database-backed
persistence, a production HTTP host, a frontend application, or exactly-once
production delivery claims.

## Goal

Connect RadarPulse backend runtime pieces into one production-shaped pipeline
surface with explicit configuration, diagnostics, restart/recovery posture,
rollback/fallback posture, and representative capacity evidence.

The milestone must prove these concrete outcomes:

```text
production pipeline configuration resolves the accepted defaults with
  explicit provenance
invalid or unsafe configuration fails closed
the pipeline runner can process deterministic archive-shaped input through
  the accepted runtime/archive baseline
durable file-based persistence is used as the local restart/recovery baseline
operator summaries expose pipeline state, durable state, readiness, first
  blocking reason, retained pressure, handler posture, and fallback posture
handler output and BFF read models remain compatible with pipeline output
rollback/fallback recommendations are explicit and do not hide provider or
  durable adapter changes
focused Release gates and representative local capacity evidence are
  captured before decision trace
```

Scope note:

```text
milestone 027 validates production-shaped integration over deterministic
archive-shaped workloads. True live network ingestion, external broker or
database adapters, production HTTP hosting, frontend workflows, deployment
automation, cross-machine throughput certification, and exactly-once
production delivery remain future work unless explicitly selected later.
```

## Slice 1: Production Pipeline Profile And Configuration Contract

Status: planned.

Implementation:

```text
add a production pipeline profile shape with a stable profile name
add pipeline options for provider/execution/ordered capacity/durable
  adapter/handler posture/workload limits
add resolved configuration and option provenance records
resolve accepted RadarProcessingRuntimeArchiveBaseline defaults into the
  pipeline profile
validate invalid capacities, unsupported adapter kinds, unsafe fallback
  requests, and unsupported handler posture combinations
report first invalid option and warnings without normalizing unsafe values
```

Tests:

```text
default profile resolves queued-owned, producer-consumer, pooled-copy,
  async execution, worker count 4, worker queue capacity 8, provider queue
  capacity 8, retained-byte budget 536870912, ordered active batch capacity
  4, and file durable adapter
explicit overrides are preserved with override provenance
invalid capacity values fail closed with first invalid option
unsupported durable adapter kind fails closed instead of falling back
silent borrowed-provider fallback request is rejected
```

Exit criteria:

```text
the pipeline has a named, validated configuration contract before any runner
or operator summary depends on it
```

Commit:

```text
Add production pipeline configuration profile
```

## Slice 2: Pipeline Operator Summary And Readiness Contract

Status: planned.

Implementation:

```text
add pipeline run state and fallback recommendation vocabulary
add a pipeline operator summary that composes resolved configuration,
  durable readiness, processing progress, handler posture, retained pressure,
  release health, first blocking state, and warnings
derive readiness from configuration validity, durable adapter compatibility,
  queue blockers, processing completeness, retained pressure, handler
  conflicts, and release health
map blocked durable states to explicit fallback recommendations
keep summary construction independent from concrete archive ingestion
```

Tests:

```text
ready summary reports accepted defaults and no first blocking reason
invalid configuration blocks readiness with first invalid option
failed durable envelope blocks readiness and recommends explicit recovery
claimed durable envelope after restart recommends explicit claim recovery
poison durable envelope recommends operator quarantine/dead-letter action
non-terminal retained pressure blocks readiness and reports cleanup
```

Exit criteria:

```text
operators can inspect why the pipeline is ready, blocked, recoverable, or
unsafe before the runner is introduced
```

Commit:

```text
Add production pipeline operator summary
```

## Slice 3: Archive-Shaped Pipeline Runner

Status: planned.

Implementation:

```text
add a production pipeline runner that accepts deterministic RadarEventBatch
  input
construct processing through the accepted runtime/archive baseline unless an
  explicit validated override is supplied
use the accepted queued-owned ordered runtime surfaces for handler-free work
use the MVP processing path for handler-aware work
store final read models in the milestone 024 BFF read-model store
return a pipeline result containing run id, resolved configuration, operator
  summary, processing result/read model, and gate counters
preserve cancellation and fail-closed behavior
```

Tests:

```text
pipeline runner processes deterministic batches and publishes a BFF run
pipeline output includes batch list, source output, diagnostics, and handler
  catalog-compatible output
handler-free run uses ordered concurrent processing defaults
mergeable handler run uses handler delta/merge path where eligible
snapshot-only handler run uses explicit sequential fallback posture
canceled run reports canceled state without claiming readiness
```

Exit criteria:

```text
RadarPulse has one application-level backend pipeline surface that composes
the accepted runtime and read-model pieces
```

Commit:

```text
Add archive shaped production pipeline runner
```

## Slice 4: Durable Restart And Recovery Pipeline Gate

Status: planned.

Implementation:

```text
compose the pipeline runner with a file-backed durable envelope queue
support pipeline service recreation around an existing file durable store
prove pending, claimed, completed, failed, poison, canceled, and released
  durable states remain visible through the pipeline summary
prove completed durable work can still reach ordered commit after pipeline
  recreation where milestone 026 recovery permits it
prevent incompatible durable state from being treated as a fresh run
```

Tests:

```text
pipeline restart preserves accepted durable state and provider sequence
claimed envelope blocks restarted pipeline until explicit recovery action
completed envelope can be committed after pipeline recreation
failed or poison envelope blocks readiness with first blocking reason
canceled envelope can be released through cleanup posture
incompatible persisted state fails closed
```

Exit criteria:

```text
the pipeline treats durable state as restart/recovery truth and does not hide
adapter-backed blockers
```

Commit:

```text
Validate production pipeline durable restart recovery
```

## Slice 5: Rollback, Fallback, And Handler Output Compatibility

Status: planned.

Implementation:

```text
add explicit stop, drain, cancel-open, and release-cleanup posture to the
  pipeline result or coordinator
add fallback recommendations for stopped, draining, failed, poison, claimed,
  canceled, incompatible, unsupported handler, and unsafe fallback states
prove handler output and BFF read models remain stable after fallback or
  recovery-visible runs
reject fallback to borrowed provider or alternate durable adapter unless it
  is explicitly configured and validated
```

Tests:

```text
stop accepting new work keeps already accepted durable state visible
drain completes accepted work in provider sequence
cancel-open marks open work canceled and release cleanup is visible
unsupported handler posture blocks with handler-specific reason
unsafe fallback request is rejected and surfaced in diagnostics
BFF diagnostics expose fallback recommendation and first blocking reason
```

Exit criteria:

```text
rollback and fallback are observable operator postures, not hidden behavior
```

Commit:

```text
Add production pipeline rollback fallback diagnostics
```

## Slice 6: Representative Capacity And Gate Evidence

Status: planned.

Implementation:

```text
add a focused pipeline gate or benchmark helper that captures elapsed time,
  allocated bytes, accepted/processed/committed counts, handler posture,
  durable adapter counts, terminal retained pressure, processing
  completeness, configuration contour, and first blocking reason
capture focused Release gate over pipeline configuration, summary, runner,
  restart/recovery, rollback/fallback, and BFF compatibility suites
run Release build
record gate evidence in
  027-production-pipeline-integration-gate.md
update handoff with completed implementation slices and latest verification
```

Tests:

```text
capacity evidence row captures contour and completion counters
successful gate has terminal retained pressure and processing completeness
blocked gate preserves first blocking reason
focused Release pipeline suite passes
Release build succeeds with zero warnings
```

Exit criteria:

```text
all implementation evidence needed for pre-decision review is captured, and
no decision trace has been written yet
```

Commit:

```text
Capture production pipeline integration gate
```

## Slice 7: Pre-Decision Trace Review Point

Status: planned.

Implementation:

```text
review completed slices, gate evidence, warnings, and scope boundary
stop before writing the decision trace
prepare open questions or blocker list if the evidence is not sufficient
```

Exit criteria:

```text
implementation and gate evidence are complete, handoff is current, and the
next action is explicit review before decision trace
```

Commit:

```text
not applicable; stop before decision trace unless review requests changes
```

## Gate Command Shape

Expected focused Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingProductionPipelineConfigurationTests|FullyQualifiedName~RadarProcessingProductionPipelineSummaryTests|FullyQualifiedName~RadarProcessingProductionPipelineRunnerTests|FullyQualifiedName~RadarProcessingProductionPipelineRecoveryTests|FullyQualifiedName~RadarProcessingProductionPipelineFallbackTests|FullyQualifiedName~RadarProcessingProductionPipelineGateTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full Release test project can be run if implementation touches shared
runtime behavior beyond the new pipeline orchestration surface. Historical
allocation-sensitive benchmark caveats should remain attributed separately if
they appear outside the focused pipeline gate.

## Stop Rule

Stop before writing the decision trace.

The milestone should stop with:

```text
027-production-pipeline-integration.md
027-production-pipeline-integration-plan.md
027-production-pipeline-integration-gate.md
implementation slices complete
handoff updated
decision trace not written
closeout not written
```

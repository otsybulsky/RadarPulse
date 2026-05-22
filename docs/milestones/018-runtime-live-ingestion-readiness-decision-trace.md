# Milestone 018 Decision Trace

Date: 2026-05-22

Decision: keep queued-owned runtime/live readiness as **explicit opt-in only**
for the scoped in-process runtime/archive replay surfaces, with startup
retained payload prewarm as the explicit candidate lifecycle and with named
coverage and rollout warnings.

This decision accepts queued-owned as runtime-safe when it is selected
explicitly for the scoped in-process archive/runtime replay surfaces covered
by milestone 018. The accepted explicit contour uses queued-owned
producer-consumer overlap, pooled-copy retained payload ownership,
async shard transport, existing queue/pressure/cancellation/failure cleanup
guardrails, and explicit startup retained payload prewarm.

This decision does not accept queued-owned as the omitted runtime/live
ingestion default. Natural first-use remains allocation-blocked for default
interpretation, startup prewarm is a real lifecycle cost that must not be
hidden, and true live ingestion, durable transport, cross-process workers,
production runtime provider selection, production operator reporting, and
repeatability gates remain outside the accepted default evidence.

The decision is not a rejection of queued-owned runtime behavior. It is a
scoped rollout boundary: queued-owned is ready to be used deliberately and
measured under explicit runtime controls, but not to become the automatic
runtime/live default.

## Decision Matrix

```text
runtime/live queued-owned default readiness:
  not accepted; omitted runtime/live defaults remain unchanged

queued-owned explicit runtime posture:
  accepted for scoped in-process runtime/archive replay surfaces with startup
  prewarm and existing guardrails

startup-prewarmed steady intake posture:
  accepted for bounded deterministic archive replay rows; allocation and
  elapsed ratios passed versus same-scenario borrowed/reference rows

natural first-use runtime posture:
  not accepted for omitted default readiness; allocation ratios remain
  warning/optimize/fail depending on workload

pressure and backpressure posture:
  accepted for scoped in-process surfaces; queue full, retained-byte
  pressure, and wait timeout are visible and bounded

cancellation posture:
  accepted for scoped in-process surfaces; enqueue cancellation,
  cancel-queued shutdown, pending cancellation, and active cancellation return
  terminal retained pressure to zero

failure and cleanup posture:
  accepted for scoped in-process surfaces; validation failure, producer
  failure, release failure visibility, drain, and cleanup are gateable

release failure posture:
  accepted as visible and readiness-blocking; not accepted as a successful
  readiness row

fallback/oracle posture:
  accepted and preserved; explicit BlockingBorrowed remains separate where
  used and is not an automatic silent fallback

observability posture:
  sufficient for this decision through lower-level contracts and temporary
  JSONL/Markdown gate output; production operator reporting remains future
  rollout work

follow-up fix posture:
  no production follow-up fix required before decision trace

runtime expansion posture:
  gradual rollout required before any default promotion
```

## Decision Explanations

### Keep Runtime Defaults Unchanged

Decision: do not promote queued-owned to the omitted runtime/live ingestion
default in milestone 018.

Why chosen: the runtime gates support explicit opt-in safety, but not
automatic default selection. Natural first-use queued-owned rows still showed
allocation ratios of `1.196x`, `2.040x`, `1.284x`, and `1.373x` versus
borrowed/reference. Startup prewarm fixes the measured steady allocation
shape, but it carries an explicit up-front cost and requires lifecycle,
configuration, and operator reporting before it can be default behavior.

Alternatives: accept queued-owned as the omitted runtime default, reject
runtime readiness entirely, or defer the decision despite passing scoped
runtime gates.

Rejected because: accepting the omitted default would hide first-use/prewarm
lifecycle cost and overstate the live-ingestion evidence; rejecting readiness
would ignore that scoped in-process gates passed; and deferring would fail to
record the clear explicit-opt-in boundary produced by the evidence.

Trade-offs/debt: future work must add production runtime selection, operator
reporting, repeatability, and live-ingestion evidence before revisiting
default promotion.

Review explanation: "Queued-owned is runtime-safe when selected explicitly,
but not yet the automatic runtime/live default."

### Accept Explicit Startup-Prewarmed Candidate

Decision: accept the startup-prewarmed queued-owned contour as the explicit
runtime candidate for scoped in-process archive/runtime replay surfaces.

Why chosen: the startup-prewarmed candidate passed all bounded steady intake
rows. Elapsed ratios versus same-scenario borrowed/reference were `0.910x`,
`0.980x`, `0.955x`, and `0.997x`. Allocation ratios were `1.000x`,
`1.001x`, `1.000x`, and `1.002x`. Processing completeness failures, worker
failure rows, release failure rows, and terminal pressure failure rows were
all `0`.

Alternatives: require natural first-use only, keep prewarm as benchmark-only
evidence, or hide prewarm inside runtime startup without attribution.

Rejected because: natural first-use remains allocation-blocked; treating
prewarm as benchmark-only would discard the runtime-shaped evidence that
prewarm works as an explicit lifecycle; and hidden prewarm would make runtime
allocation and startup behavior misleading.

Trade-offs/debt: startup prewarm cost remains real. The gate measured
`71_303_392` allocated bytes and `71_303_168` retained bytes for the selected
prewarm sizing, so production rollout must surface that cost.

Review explanation: "The accepted candidate is prewarmed and explicit; the
cost is not hidden in steady allocation."

### Reject Natural First-Use As Default Evidence

Decision: keep natural first-use queued-owned rows as control evidence, not
as default-readiness proof.

Why chosen: natural first-use passed safety guardrails but did not pass the
allocation posture needed for omitted default readiness. The worst natural
allocation ratios were `2.040x` and `1.373x`, both beyond the fail threshold,
and other rows remained warning or optimize evidence.

Alternatives: accept natural first-use because elapsed ratios mostly passed,
average natural and prewarmed rows together, or raise runtime allocation
thresholds after seeing the rows.

Rejected because: elapsed wins do not erase allocation misses; averaging
would hide the first-use cost the milestone was designed to expose; and
threshold changes after measurement are explicitly disallowed.

Trade-offs/debt: natural first-use remains useful for future optimization
work and for any runtime surface that cannot use startup prewarm.

Review explanation: "Natural first-use is safety-clean but not default-ready
because retained ownership allocation is still visible."

### Accept Pressure, Cancellation, Failure, And Cleanup Gates

Decision: accept the scoped in-process pressure/failure/cancellation evidence
as passing runtime lifecycle guardrails.

Why chosen: the slice 7 gate captured `11` rows, all passing the expected
shape. The rows covered queue full rejection, retained-byte budget rejection,
wait-on-full timeout, enqueue cancellation before start and while waiting,
CancelQueued shutdown for accepted pending work, archive overlap cancellation
after accepted enqueue, active consumer cancellation, drain with pending
work, processing validation failure, retained release failure visibility, and
producer failure cleanup. All rows returned current retained pressure to
zero.

Alternatives: require real live network input for these failure shapes, defer
pressure interpretation, or treat the injected release failure as a failed
gate.

Rejected because: synthetic leased-batch injection is the deterministic way
to force specific lifecycle states in this milestone; pressure outcomes were
visible and gateable; and the release-failure row passed because visibility
and cleanup were correct, while the release failure itself remains
readiness-blocking in a real row.

Trade-offs/debt: the gate proves lifecycle shapes, not every possible worker
failure or real live network behavior.

Review explanation: "Pressure and failure paths are visible and clean up, but
the evidence remains scoped to in-process runtime surfaces."

### Preserve Borrowed Oracle Without Silent Fallback

Decision: preserve explicit BlockingBorrowed as reference/oracle where used
and reject automatic borrowed fallback after queued-owned failure.

Why chosen: borrowed/reference rows are needed to interpret steady cost and
correctness. Slice 6 kept borrowed/reference rows separate and unprewarmed.
Slice 7 failure rows proved queued-owned failure visibility without falling
through to borrowed success.

Alternatives: remove borrowed rows after accepting explicit opt-in safety,
use borrowed as automatic runtime rescue, or prewarm borrowed/reference rows
for symmetry.

Rejected because: removing the oracle weakens future gates; automatic rescue
would hide queued-owned failures; and prewarming borrowed/reference would
change the comparison baseline.

Trade-offs/debt: future rollout gates remain heavier because they should keep
explicit borrowed/reference comparison where meaningful.

Review explanation: "Borrowed remains an explicit control, not a hidden
fallback."

### Require Gradual Rollout Before Default Promotion

Decision: make gradual runtime rollout the next work direction before any
default promotion.

Why chosen: milestone 018 did not add a production runtime provider selection
surface, production operator reporting, true live-ingestion evidence,
repeatability classification, or durable/cross-process coverage. Those are
default-readiness requirements, not defects in the scoped in-process runtime
surface.

Alternatives: close the milestone as runtime default-ready, add production
rollout wiring inside slice 9, or expand the milestone into durable/live
implementation.

Rejected because: default-ready would overclaim; production rollout wiring is
a distinct milestone with operator and configuration decisions; and
durable/live implementation was explicitly out of scope.

Trade-offs/debt: milestone 018 closes with a narrower but defensible answer.
The next milestone should decide whether to target archive-runtime explicit
opt-in rollout, true live adapter evidence, production reporting/config, or
repeatability/default promotion gates.

Review explanation: "The next step is controlled rollout, not automatic
default expansion."

## Included Surface

Included in-process runtime/archive surfaces:

```text
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingOwnedBatchQueue
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
RadarProcessingRetainedPayloadFactory
```

Included explicit runtime candidate contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
retained payload prewarm: explicit startup candidate lifecycle
prewarm event count: 65_536
prewarm payload bytes: 67_108_864
prewarm retained batch count: 1
```

Included gate input shapes:

```text
deterministic local archive replay as steady live-input stand-in
synthetic leased-batch injection for pressure, cancellation, failure, drain,
  release, and cleanup states
explicit BlockingBorrowed/reference rows where steady comparison is
  meaningful
```

Excluded:

```text
omitted runtime/live queued-owned default promotion
hidden or implicit runtime prewarm
natural first-use queued-owned default readiness
automatic silent borrowed fallback
durable queues or brokers
cross-process provider or worker transport
ordered concurrent rebalance
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
distributed workers
product-facing live radar workflows
production deployment, alerting, rollback, or operator runbooks
```

## Evidence

Primary source documents:

```text
docs/milestones/018-runtime-live-ingestion-readiness.md
docs/milestones/018-runtime-live-ingestion-readiness-plan.md
docs/milestones/018-runtime-live-ingestion-readiness-lifecycle-audit.md
docs/milestones/018-runtime-live-ingestion-readiness-gate-matrix.md
docs/milestones/018-runtime-live-ingestion-readiness-reporting-harness.md
docs/milestones/018-runtime-live-ingestion-readiness-prewarm-posture.md
docs/milestones/018-runtime-live-ingestion-readiness-lifecycle-guardrails.md
docs/milestones/018-runtime-live-ingestion-readiness-steady-intake-gate.md
docs/milestones/018-runtime-live-ingestion-readiness-pressure-failure-gate.md
docs/milestones/018-runtime-live-ingestion-readiness-gate-interpretation.md
```

Milestone 017 direct benchmark evidence:

```text
direct MeasureFile()/MeasureCache() default-equivalent contour accepted with
retained payload prewarm

interpretation in milestone 018:
  input evidence only, not automatic runtime/live default approval
```

Slice 5 lifecycle guardrail fix:

```text
ShutdownMode.CancelQueued clears accepted pending queued work before dequeue
queued processing and rebalance sessions record canceled pending sequence ids
archive queued overlap runner applies the same cancellation shutdown policy
provider queue telemetry permits canceled accepted work before dequeue
```

Slice 5 focused verification:

```text
focused queue, session, rebalance, telemetry, and archive-overlap guardrail
tests:
  passed: 54
  failed: 0
```

Full-project verification note from slice 5:

```text
full test project was attempted twice and both runs had one allocation
threshold failure in:
  RadarProcessingSyntheticRebalanceBenchmarkTests.
    AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

isolated rerun of that failing test passed
interpretation:
  full-suite allocation sensitivity outside the slice 5 runtime guardrail
  surface
```

Slice 6 steady intake gate:

```text
temporary runner:
  data\temp\m018-runtime-gate-runner

raw output:
  data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.jsonl
  data\temp\m018-runtime-gate-runner\output\m018-runtime-20260522-134534.md

rows: 12
pass safety guardrails: 12
processing completeness failures: 0
worker failure rows: 0
release failure rows: 0
terminal pressure failure rows: 0
max queue depth high-watermark: 1
max combined retained bytes high-watermark: 48_342_240
```

Startup-prewarmed steady candidate:

```text
elapsed ratios versus borrowed/reference:
  0.910x, 0.980x, 0.955x, 0.997x

allocation ratios versus borrowed/reference:
  1.000x, 1.001x, 1.000x, 1.002x

prewarm allocated bytes per row:
  71_303_392

prewarm retained bytes per row:
  71_303_168
```

Natural first-use control:

```text
allocation ratios versus borrowed/reference:
  1.196x, 2.040x, 1.284x, 1.373x

interpretation:
  warning/optimize/fail control evidence; not omitted default proof
```

Slice 7 pressure/failure gate:

```text
temporary runner:
  data\temp\m018-runtime-pressure-gate-runner

raw output:
  data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.jsonl
  data\temp\m018-runtime-pressure-gate-runner\output\m018-pressure-20260522-135835.md

rows: 11
pass: 11
fail: 0
operator-visible rows: 11
terminal pressure clean rows: 11
backpressure rows: 3
cancellation rows: 4
failure rows: 6
release-failure visible rows: 1
max queue depth high-watermark: 3
max combined retained bytes high-watermark: 6
```

Slice 7 focused verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests"

passed: 56
failed: 0
skipped: 0
```

## Operational Posture

Runtime default posture:

```text
omitted runtime/live defaults remain unchanged
queued-owned runtime/live default promotion is not accepted
```

Explicit queued-owned posture:

```text
queued-owned may be used deliberately for scoped in-process runtime/archive
replay surfaces with startup prewarm and the existing guardrails
```

Prewarm posture:

```text
startup retained payload prewarm is accepted only as explicit lifecycle cost
prewarm sizing, elapsed time, allocated bytes, and retained bytes must remain
visible
prewarm must not be hidden inside steady measured allocation
```

Fallback posture:

```text
BlockingBorrowed remains explicit fallback/oracle where supported
queued-owned failure does not silently fall back to borrowed success
```

Failure posture:

```text
queued-owned failures fail closed
processing completeness is required for accepted rows
processing validation failed batches block readiness
worker failed batches/items remain readiness blockers
release failures remain readiness blockers
retained pressure must return to zero after drain, cancellation, failure, and
cleanup paths
```

Reporting posture:

```text
existing lower-level contracts and temporary JSONL/Markdown output are
sufficient for this decision
production operator-facing runtime reporting remains future rollout work
```

Gradual rollout posture:

```text
next work should add production runtime provider selection, operator
reporting, explicit startup prewarm lifecycle wiring, repeatability gates, and
true live ingestion or narrower archive-runtime rollout evidence
```

## Residual Risks And Limits

```text
true live ingestion:
  not proven; deterministic archive replay was used as live-input stand-in

durable/cross-process:
  durable queues, brokers, and cross-process provider/worker behavior remain
  out of scope

natural first-use:
  remains allocation-blocked for omitted default readiness

startup prewarm:
  accepted only as explicit lifecycle cost, not as hidden runtime behavior

repeatability:
  bounded steady gate did not include repeated variance classification

synthetic failure gates:
  pressure/failure rows prove lifecycle shapes, not real network input
  behavior

operator reporting:
  production runtime reporting and default selection surfaces were not added

worker failure breadth:
  all possible worker failure shapes were not exhausted

runtime default promotion:
  blocked by first-use allocation misses, missing omitted-default prewarm
  lifecycle, missing true live ingestion evidence, missing production rollout
  surface, and missing durable/cross-process evidence
```

## Decision

Milestone 018 answers the closeout question with **explicit opt-in only**:

```text
queued-owned is runtime-safe when selected explicitly for the scoped
in-process runtime/archive replay surfaces with startup prewarm and existing
guardrails
```

Milestone 018 answers **no** for omitted runtime/live default readiness:

```text
queued-owned is not accepted as the omitted runtime/live ingestion default
```

Milestone 018 answers **accepted as explicit lifecycle cost** for runtime
prewarm:

```text
startup retained payload prewarm is accepted only when selected explicitly and
reported separately from steady measured allocation
```

Milestone 018 answers **no** for natural first-use default readiness:

```text
natural first-use queued-owned rows remain allocation warning/optimize/fail
evidence and do not support omitted default promotion
```

Milestone 018 answers **yes** for scoped pressure, cancellation, failure,
drain, release, and cleanup guardrails:

```text
the scoped in-process queue/session/archive-overlap surfaces passed
deterministic lifecycle gates and returned terminal retained pressure to zero
```

Milestone 018 answers **yes** for preserving explicit borrowed fallback and
same-run oracle coverage:

```text
BlockingBorrowed remains explicit where used; no automatic silent fallback is
accepted
```

Milestone 018 answers **no** for durable/cross-process/live default expansion:

```text
true live ingestion, durable queues, brokers, cross-process providers/workers,
ordered concurrent rebalance, builder-transfer, and production runtime default
promotion remain out of scope
```

Recommended next milestone input:

```text
design gradual runtime rollout for queued-owned explicit opt-in; add
production runtime provider selection, operator-visible provider/prewarm/
pressure/failure reporting, explicit startup prewarm lifecycle wiring,
repeatability gates, and true live ingestion or narrower archive-runtime
rollout evidence before revisiting runtime default promotion
```

Milestone 018 can proceed to closeout without additional production runtime
changes, threshold changes, or borrowed fallback behavior changes.

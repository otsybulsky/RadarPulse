# Milestone 018: Runtime And Live Ingestion Readiness Implementation Plan

Status: draft.

This plan implements the milestone 018 architecture defined in
`018-runtime-live-ingestion-readiness.md`.

The plan is intentionally scoped to runtime/live ingestion readiness for the
already accepted in-process queued-owned foundation. It should not implement
durable broker integration, cross-process workers, ordered concurrent
rebalance, builder-transfer, source-level migration, partition splitting,
product-facing radar analysis, synthetic benchmark defaults, or broad
production deployment work.

## Goal

Milestone 018 decides whether queued-owned can move beyond direct benchmark
surfaces into runtime/live ingestion defaults, or whether queued-owned must
remain benchmark-scoped or explicit opt-in until runtime lifecycle, pressure,
fallback, cancellation, cleanup, and observability requirements are met.

The milestone starts from the milestone 017 accepted direct benchmark contour:

```text
surface:
  RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
  CLI rebalance-archive benchmark paths that use the same direct APIs

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912
  overlap consumer delay: 0
  retained payload prewarm: enabled for the direct benchmark
    default-equivalent contour
```

Milestone 017 accepted that contour only for direct benchmark/file/cache
surfaces. Milestone 018 must not treat that decision as automatic runtime
approval.

The most important rules are:

```text
preserve direct benchmark readiness as evidence, not automatic runtime default
preserve explicit BlockingBorrowed as fallback/oracle where supported
do not silently fall back from queued-owned failure to borrowed success
do not hide retained payload prewarm cost inside runtime startup or row
  allocation
do not claim provider enqueue success as processing completion
require processing completeness for runtime success
require worker failed batches/items to be visible and gateable
require processing validation failed batches to be visible and gateable
require retained cleanup and release health through success, failure,
  cancellation, drain, and dispose paths
define runtime provider default versus explicit opt-in before decision trace
define runtime prewarm policy before readiness interpretation
define runtime pressure and backpressure policy before gate interpretation
use deterministic archive replay as live-input stand-in only through
  runtime-shaped lifecycle gates
do not broaden into durable/cross-process/ordered-concurrent surfaces
do not raise thresholds after seeing gate measurements
```

The implementation target is evidence-driven:

```text
inventory runtime/live/archive-provider surfaces before changing behavior
record current lifecycle, queue, retained pressure, worker, cancellation, and
  reporting gaps
define non-negotiable runtime readiness guardrails before gate capture
define runtime cost interpretation before gate capture
prove result contracts expose enough fields for decision review
capture runtime-shaped steady, pressure, cancellation, failure, and drain
  evidence
classify readiness as accepted, accepted with warnings, explicit opt-in only,
  optimization-bound, architecture-blocked, rejected, coverage-insufficient,
  or deferred
```

## Starting Point

Milestone 017 is complete and provides:

```text
direct MeasureFile()/MeasureCache() omitted defaults:
  queued-owned rollout contour plus retained payload prewarm

explicit fallback/oracle:
  providerMode: BlockingBorrowed

prewarmed file-level readiness:
  accepted with named scoped warnings

prewarmed small-file readiness:
  accepted with named scoped warnings

broader cache-level readiness:
  accepted with named scoped warnings

processing completeness:
  archive rebalance result/CLI reporting exposes processing completeness,
  processing validation failed batches, and worker failure counts as blockers

mixed-radar source-universe fix:
  unfiltered MeasureCache() self-sizes mixed-radar source universes and
  mixed-cache-all passes with worker failed batches/items 0/0
```

Milestone 017 also leaves these runtime limits:

```text
runtime/live ingestion defaults:
  not approved

runtime prewarm lifecycle:
  not designed

durable queues or brokers:
  not designed

cross-process providers/workers:
  not designed

ordered concurrent rebalance:
  not designed

builder-transfer:
  unsupported

non-benchmark archive publishing API defaults:
  not migrated by milestone 017
```

Current runtime-related code surfaces to audit first:

```text
src/Application/Archive/IArchiveRadarEventBatchPublisher.cs
src/Infrastructure/Archive/NexradArchiveRadarEventBatchPublisher.cs
src/Infrastructure/Archive/ArchiveOwnedRadarEventBatchQueueingPublisher.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapRunner.cs
src/Infrastructure/Processing/RadarProcessingArchiveQueuedOverlapOptions.cs
src/Infrastructure/Processing/RadarProcessingOwnedBatchQueue.cs
src/Infrastructure/Processing/RadarProcessingQueuedProcessingSession.cs
src/Infrastructure/Processing/RadarProcessingQueuedRebalanceSession.cs
src/Infrastructure/Processing/RadarProcessingRetainedPayloadFactory.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceRolloutDefaults.cs
src/Presentation/Program.cs
```

Current contract and telemetry surfaces to audit first:

```text
src/Domain/Processing/RadarProcessingQueuedSessionResult.cs
src/Domain/Processing/RadarProcessingQueuedSessionStatus.cs
src/Domain/Processing/RadarProcessingQueuedBatchEnqueueResult.cs
src/Domain/Processing/RadarProcessingQueuedBatchProcessingResult.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetrySummary.cs
src/Domain/Processing/RadarProcessingProviderQueueTelemetryRecorder.cs
src/Domain/Processing/RadarProcessingRetainedResourcePressureSummary.cs
src/Domain/Processing/RadarProcessingRetainedResourcePressureRecorder.cs
src/Domain/Processing/RadarProcessingRetainedPayloadTelemetrySummary.cs
src/Domain/Processing/RadarProcessingWorkerTelemetrySummary.cs
src/Domain/Processing/RadarProcessingWorkerGroupStatus.cs
src/Domain/Processing/RadarProcessingAsyncCancellationKind.cs
src/Domain/Processing/RadarProcessingAsyncFailureKind.cs
```

Current focused test areas to audit first:

```text
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingOwnedBatchQueueTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedPayloadFactoryTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingRetainedBatchResourceTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingProviderQueueTelemetryRecorderTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingWorkerLifecycleContractTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingWorkerTelemetryContractTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Known local NEXRAD corpus carried forward:

```text
data\nexrad\level2\2026\05\04\KTLX:
  total files 244, base-data 220, MDM 24, metadata 0

data\nexrad\level2\2026\05\04\KINX:
  total files 462, base-data 207, MDM 24, metadata 231

data\nexrad\level2\2026\05\05\KTLX:
  total files 848, base-data 401, MDM 23, metadata 424

data\nexrad total:
  1_554 files, 4_984_572_136 bytes
```

These local archive inputs are allowed as deterministic runtime-shaped
stand-ins. They do not prove live network input, durable broker behavior, or
cross-process semantics.

## Scope

The implementation should explicitly scope milestone 018 to:

```text
runtime/live/archive-provider surface inventory
runtime provider default versus explicit opt-in decision
runtime lifecycle contract over configure/start/prewarm/intake/process/drain/
  cancel/fault/stop/dispose
runtime retained payload prewarm policy
startup, first-use, steady-state, drain, cancellation, fault, and dispose
  cost attribution
runtime pressure and backpressure policy
processing completeness as runtime success requirement
worker failure and processing validation failure visibility
explicit BlockingBorrowed fallback/oracle preservation where supported
runtime-shaped deterministic archive replay gates as live-input stand-ins
steady intake, mixed-radar, backpressure, cancellation, failure, drain,
  cleanup, release, pressure, ordering, and observability gates
result or CLI reporting changes required to make runtime evidence reviewable
decision trace, closeout, project-progress update, and handoff
```

The implementation should not change:

```text
direct benchmark accepted contour unless a regression requires a fix
synthetic processing benchmark defaults
durable or cross-process execution semantics
ordered concurrent rebalance semantics
builder-transfer unsupported posture
domain enum numeric values
source-universe meaning except for bug fixes needed to preserve processing
  completeness
topology publication ordering
automatic fallback semantics
thresholds after gate capture
```

## Readiness Thresholds

Milestone 018 must record runtime thresholds and interpretation bands before
Release gate measurements are interpreted. Direct benchmark thresholds from
milestone 017 are input evidence, but runtime readiness needs lifecycle
thresholds.

Non-negotiable runtime guardrails:

```text
provider selection provenance:
  runtime provider mode and source must be visible

fallback separation:
  explicit BlockingBorrowed must be visibly separate where used, and
  queued-owned failures must not be overwritten by borrowed success

processing completeness:
  ProcessingSucceeded must be true for accepted readiness rows

processing validation failed batches:
  must equal 0 for accepted readiness rows

worker failed batches/items:
  must equal 0/0 for accepted readiness rows

release failures:
  retained payload failed releases and provider overlap failed releases must
  equal 0

retained cleanup:
  current pending, active, and combined retained batch/byte counts must return
  to 0 after success, cancellation, failure, drain, stop, and dispose gates

retained pressure:
  combined retained payload high-water must be <= 536870912 bytes unless the
  configured retained-byte budget is explicitly changed in a future contour

topology ordering:
  one processed batch observes one topology snapshot; accepted migrations
  publish only after successful processing

queued session completion:
  provider enqueue success must not be reported as processing completion

fail-closed behavior:
  queued-owned provider, worker, validation, release, or retained pressure
  failures must fail closed and remain operator-visible

observability:
  runtime results or CLI output must expose enough fields to explain provider
  mode, prewarm, pressure, queue, worker, processing completeness, release,
  cancellation, fault, and drain outcomes
```

Runtime prewarm thresholds and interpretation:

```text
prewarm attribution:
  required if prewarm is used; enabled state, source, sizing, elapsed time,
  allocated bytes, and retained bytes must be visible

prewarm failure:
  must block runtime startup or leave runtime in an explicit non-ready state;
  intake must not proceed as if queued-owned were healthy

prewarm retained pressure:
  must respect retained-byte budget and return to zero after stop/dispose

prewarm cost:
  startup or first-use prewarm cost may be accepted only as a named runtime
  lifecycle cost, not as hidden measured-row allocation
```

Runtime steady-state cost interpretation:

```text
steady-state allocation:
  after any accepted runtime prewarm, queued-owned steady-state allocation
  should remain in the milestone 017 accepted direct/cache range unless a
  runtime-specific warning is named before closeout

steady-state elapsed:
  runtime queued-owned rows should remain at parity or faster than the
  explicit borrowed/reference contour for accepted throughput rows, or the
  decision trace must classify the miss as a warning, optimization target, or
  blocker

session variance:
  repeated runtime-shaped rows should have enough stability to distinguish
  startup/prewarm cost, first-use cost, steady processing cost, and local file
  I/O variance

backpressure cost:
  provider blocked time, enqueue wait, queue drain time, and retained pressure
  must be reported separately from processing execution time
```

Runtime backpressure thresholds:

```text
queue full behavior:
  must produce wait, timeout, cancellation, or rejection according to explicit
  options; no silent drops

retained-byte pressure behavior:
  must reject, wait, cancel, or fault visibly when budget would be exceeded;
  no unbounded retention

post-fault intake:
  later enqueue attempts after terminal processing fault must be rejected or
  explicitly shaped by a documented recovery policy

drain:
  normal drain must complete accepted work and release retained resources

cancellation:
  cancellation before enqueue, after accepted enqueue, while queued, while
  active, and during drain must produce explicit result states and cleanup
```

Per-surface status vocabulary:

```text
pass:
  all non-negotiable guardrails pass, runtime cost thresholds pass, and
  observability is sufficient

warning:
  correctness, processing completeness, release, cleanup, pressure, and
  fail-closed behavior pass, but timing, prewarm cost, startup cost,
  backpressure cost, variance, or coverage needs named scoped acceptance

explicit-opt-in:
  queued-owned runtime behavior is safe when selected explicitly, but omitted
  runtime defaults should not select it yet

optimize:
  lifecycle guardrails pass, but runtime cost, pressure, or backpressure
  behavior is too high or poorly attributed for default readiness

architecture-blocked:
  a required runtime contract or result surface is missing

fail:
  correctness, processing completeness, release, cleanup, pressure,
  fail-closed behavior, topology ordering, validation, or required
  observability fails

coverage-only:
  row is useful evidence but cannot support readiness by itself because it is
  direct benchmark only, too narrow, not lifecycle-shaped, or not repeatable

defer:
  test health, instrumentation, local corpus, or runner stability prevents a
  defensible decision
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason before interpreting the gate.
```

## Target Implementation Shape

The preferred implementation shape is audit and contract work first, then
small targeted code changes only where runtime behavior or reporting is not
reviewable enough to make the decision.

Expected implementation posture:

```text
use existing queued-owned, owned-batch, provider queue, worker, retained
  pressure, and archive replay infrastructure where possible
use existing direct benchmark readiness evidence as baseline context
use deterministic local archive replay as runtime-shaped input stand-in
build temporary gate runners under data\temp when structured capture is
  easier than CLI-only capture
commit product result/reporting fixes only when needed for runtime readiness
  observability
preserve benchmark direct defaults unless a regression fix is required
preserve fallback/oracle behavior
do not introduce durable/cross-process abstractions
do not change runtime defaults until the decision trace accepts that posture
```

Potential code work, only if justified by audit or gate needs:

```text
runtime result contract additions for lifecycle status, prewarm attribution,
  pressure, queue, worker, processing completeness, drain, cancellation, or
  fault visibility

runtime options/resolver additions for default versus explicit opt-in
  provider selection

runtime prewarm lifecycle support if the milestone chooses startup or
  operator-triggered prewarm as the candidate contour

guardrail tests for provider enqueue versus processing completion separation

guardrail tests for cancellation after accepted enqueue, active processing,
  drain, fault, release, and dispose cleanup

temporary local runtime gate runner under data\temp\m018-runtime-gate-runner
  or equivalent ignored path
```

Unsafe implementation directions:

```text
queuing borrowed RadarEventBatch payload beyond callback lifetime
silently rerunning failed queued-owned runtime work as borrowed
reporting provider enqueue success as processing success
turning off telemetry needed by runtime gates
raising retained-byte budget to hide pressure
prewarming without explicit lifecycle/result attribution
changing topology publication order to improve throughput
adding durable or cross-process semantics inside this milestone
```

## Planned Slices

### 1. Runtime Surface Inventory And Lifecycle Audit

Audit existing runtime, archive provider, queued-owned, retained pressure, and
worker surfaces before designing changes.

Questions:

```text
which surfaces are direct benchmark only?
which surfaces are runtime/archive-provider shaped?
which surfaces could plausibly be live-ingestion shaped later?
where does provider enqueue success currently stop?
where is processing completion reported?
where are start, drain, cancel, fault, stop, and dispose semantics visible?
where can retained payload prewarm be owned safely, if anywhere?
which paths already expose processing completeness?
which paths expose worker failures, processing validation failures, retained
  pressure, queue telemetry, and release failures?
which runtime concepts are missing versus merely unreported?
```

Tasks:

```text
inspect the code surfaces listed in Starting Point
inventory runtime/provider entry points and classify them as direct benchmark,
  runtime archive provider, live-ingestion candidate, or out of scope
map provider enqueue result to processing completion result for queued-owned
  paths
map lifecycle states currently represented in code
map retained payload factory ownership and release behavior
map pressure and backpressure option/result surfaces
map cancellation and failure result surfaces
map CLI/result contract reporting gaps
record current test coverage and missing guardrail tests
```

Expected output:

```text
runtime surface inventory
lifecycle state audit
provider enqueue versus processing completion map
prewarm ownership audit
pressure/backpressure surface map
observability gap list
test coverage gap list
decision on whether existing runtime surfaces are enough for gate capture or
  require targeted reporting/contract work
```

Slice 1 status:

```text
status: pending
runtime behavior changes: none expected
```

### 2. Runtime Readiness Contract And Gate Matrix Design

Define the exact runtime readiness contract and workload matrix before gate
capture.

Questions:

```text
what is the candidate runtime provider posture: queued-owned default,
  queued-owned explicit opt-in, borrowed default with queued-owned diagnostic,
  or undecided pending evidence?
what lifecycle states must be captured by the gate?
which runtime-shaped archive workloads are enough for a first readiness
  decision?
which live-only gaps must be named as coverage limits?
which cost thresholds and warning bands apply to startup, prewarm, first-use,
  steady-state, backpressure, and drain?
which failure and cancellation paths are mandatory before closeout?
```

Tasks:

```text
select runtime-shaped workload matrix before interpretation
select steady intake rows
select mixed-radar/source-universe rows
select prewarm/startup rows if prewarm is a candidate
select first-use rows if no startup prewarm is selected
select backpressure rows
select cancellation rows
select failure/fault rows
select drain/shutdown rows
record runtime thresholds and warning bands before Release gate capture
record explicit pass/warning/optimize/fail/coverage vocabulary
```

Candidate runtime-shaped workload matrix:

```text
single-radar steady runtime archive session:
  KTLX 2026-05-04 bounded cache shape

named-risk runtime archive session:
  KTLX 2026-05-05 bounded cache shape

cross-radar runtime archive session:
  KINX 2026-05-04 bounded cache shape

mixed-radar runtime archive session:
  unfiltered local mixed KINX/KTLX selected base-data files

small-file first-use runtime session:
  2/4/8 published base-data slices aligned with milestone 017 small-cache
  evidence

backpressure runtime session:
  provider queue capacity pressure with retained-byte pressure visible

cancellation runtime sessions:
  before enqueue, while waiting for enqueue capacity, after accepted enqueue,
  while active processing, and during drain

failure runtime sessions:
  worker failure, processing validation failure, retained release failure, or
  equivalent deterministic failure injectors already supported by tests
```

Expected output:

```text
runtime readiness contract
runtime gate matrix
threshold and warning-band section in this plan
coverage statement for true live ingestion if no live adapter is implemented
```

Slice 2 status:

```text
status: pending
runtime behavior changes: none expected
```

### 3. Reporting, Contract, And Harness Gap Closure

Make runtime evidence reviewable before running Release gates.

Questions:

```text
can existing result contracts capture every required runtime gate field?
does CLI output need runtime-specific attribution or is direct result capture
  enough?
does a temporary runner need structured JSONL and Markdown output?
are processing completeness and worker failures visible for runtime archive
  sessions, not only direct benchmark rows?
are prewarm, pressure, queue, worker, cancellation, fault, drain, and release
  outcomes visible in one result shape?
```

Tasks:

```text
close small result/reporting gaps found in slice 1
add focused contract tests for new or clarified result fields
build temporary runtime gate runner under data\temp if direct structured
  capture is required
make runner emit JSONL rows and Markdown summaries
preserve ignored/local harness posture unless a product surface is needed
prove omitted/default and explicit/fallback provenance can be captured
prove processing completeness is included in runtime gate rows
```

Allowed code changes:

```text
small result contract additions
small CLI/reporting additions where operator visibility is required
focused tests for provenance, processing completeness, pressure, prewarm,
  drain, cancellation, fault, and release visibility
temporary gate runner in ignored workspace path
```

Not allowed without replanning:

```text
runtime default migration
durable queue or broker abstraction
cross-process transport
ordered concurrent rebalance
automatic fallback
```

Expected output:

```text
reviewable runtime result fields
focused reporting/contract tests
runtime gate runner location, if used
no unresolved observability blocker before gate capture, or an explicit
  architecture-blocked posture
```

Slice 3 status:

```text
status: pending
runtime behavior changes: possible reporting/contract only
```

### 4. Runtime Prewarm Lifecycle Decision And Guardrails

Decide and test the candidate runtime prewarm posture before performance
interpretation.

Questions:

```text
should runtime prewarm be disabled, startup-owned, lazy first-use,
  operator-triggered, or explicit opt-in only?
who owns the retained payload factory in runtime?
what happens if prewarm fails?
does intake wait for prewarm completion?
how is prewarm cost attributed?
how are prewarmed resources released on stop, dispose, cancellation, or fault?
does runtime prewarm consume retained-byte budget?
is milestone 017 fixed sizing acceptable for runtime gates or should the
  runtime candidate derive sizing from options?
```

Tasks:

```text
record chosen candidate prewarm posture before runtime Release gate capture
add guardrail tests for prewarm success, failure, cancellation, and cleanup if
  prewarm is used
add guardrail tests proving borrowed/fallback paths remain unprewarmed
verify prewarm attribution is separated from steady-state processing cost
verify retained pressure returns to zero after stop/dispose
verify prewarm failure does not allow hidden degraded success
```

Valid candidate outcomes:

```text
no runtime prewarm:
  runtime gate measures natural first-use cost and likely keeps queued-owned
  explicit opt-in unless first-use cost is accepted

startup prewarm:
  runtime start performs visible prewarm before intake

operator-triggered prewarm:
  explicit operation prepares retained resources before runtime queued-owned
  intake is enabled

lazy first-use:
  first queued-owned intake pays visible prewarm/first-use cost

explicit opt-in only:
  runtime defaults do not prewarm, but explicit callers can provide a
  retained payload factory or prewarm policy
```

Expected output:

```text
runtime prewarm decision input
prewarm guardrail tests
prewarm attribution evidence
prewarm cleanup evidence
named blocker if runtime prewarm cannot be made reviewable
```

Slice 4 status:

```text
status: pending
runtime behavior changes: possible scoped prewarm lifecycle work only after
  slice 1-3 audit
```

### 5. Backpressure, Failure, Cancellation, And Cleanup Guardrails

Prove runtime lifecycle safety before steady-state readiness gates.

Questions:

```text
does provider queue full behavior match the selected policy?
does retained-byte pressure block, wait, cancel, or fault visibly?
does cancellation after accepted enqueue release pending retained resources?
does active processing cancellation produce an explicit result?
does drain finish accepted work and leave retained pressure at zero?
does failure stop or reject later intake?
do worker failures and processing validation failures block success claims?
do release failures fail the gate?
does dispose with queued work release resources deterministically?
```

Tasks:

```text
add or verify focused tests for provider queue full behavior
add or verify focused tests for retained-byte pressure behavior
add or verify focused tests for cancellation before enqueue
add or verify focused tests for cancellation while waiting for enqueue
add or verify focused tests for cancellation after accepted enqueue
add or verify focused tests for cancellation while active
add or verify focused tests for drain and shutdown with pending work
add or verify focused tests for worker failure and validation failure
add or verify focused tests for release failure reporting
add or verify focused tests for no automatic borrowed fallback
```

Expected output:

```text
focused lifecycle guardrail tests
documented runtime failure and cancellation result shapes
documented cleanup behavior for success, cancellation, fault, drain, and
  dispose
no known lifecycle blocker before Release gate, or a named blocker
```

Slice 5 status:

```text
status: pending
runtime behavior changes: possible guardrail fixes only
```

### 6. Runtime Steady Intake Gate

Capture runtime-shaped steady intake evidence against deterministic archive
input.

Questions:

```text
does a long-enough runtime session preserve correctness, ordering, processing
  completeness, release health, cleanup, pressure, and worker health?
does queued-owned runtime behavior stay within cost bands after startup or
  first-use cost is attributed?
does mixed-radar runtime processing keep source-universe sizing correct?
does prewarm, if selected, remove measured retained pool misses without
  hiding startup cost?
does explicit borrowed/reference remain available for comparison?
```

Tasks:

```text
run selected single-radar runtime archive sessions
run selected named-risk runtime archive sessions
run selected cross-radar runtime archive sessions
run selected mixed-radar runtime sessions
run selected small-file first-use or prewarm-sensitive sessions
capture provider, processing, queue, pressure, worker, prewarm, release, and
  processing completeness fields
compare against explicit borrowed/reference rows where available
classify every workload shape individually before aggregate interpretation
```

Expected output:

```text
runtime steady intake raw JSONL output
runtime steady intake Markdown summary
per-shape pass/warning/optimize/fail/coverage classification
startup/prewarm/first-use/steady-state cost attribution
processing completeness and worker failure summary
```

Slice 6 status:

```text
status: pending
runtime behavior changes: none expected during capture
```

### 7. Runtime Pressure, Backpressure, Cancellation, And Failure Gate

Capture runtime-shaped non-happy-path evidence.

Questions:

```text
does queue pressure produce the selected backpressure result?
does retained-byte pressure stay bounded?
does cancellation clean up pending and active resources?
does failure block processing success claims?
does drain/stop/dispose leave retained pressure at zero?
does failure remain fail-closed without automatic borrowed fallback?
are all non-happy-path outcomes operator-visible?
```

Tasks:

```text
run queue capacity pressure gate
run retained-byte pressure gate
run cancellation before enqueue gate
run cancellation while waiting for enqueue capacity gate
run cancellation after accepted enqueue gate
run cancellation while active processing gate
run drain with pending work gate
run worker or validation failure gate
run release failure gate if an existing deterministic injector is available
capture result fields and retained pressure after every terminal state
```

Expected output:

```text
runtime pressure/backpressure/cancellation/failure raw output
runtime pressure/backpressure/cancellation/failure Markdown summary
terminal-state cleanup table
failure and cancellation result-shape table
operator visibility table
named blocker if any cleanup/release/pressure/fail-closed invariant fails
```

Slice 7 status:

```text
status: pending
runtime behavior changes: none expected during capture
```

### 8. Gate Interpretation And Follow-Up Fixes

Interpret runtime evidence before writing the decision trace. Only implement
fixes if the evidence names a blocker that can be resolved inside the
milestone without changing scope.

Questions:

```text
is queued-owned runtime default ready, ready with warnings, explicit opt-in
  only, optimization-bound, architecture-blocked, rejected,
  coverage-insufficient, or deferred?
does prewarm belong in runtime default lifecycle?
does startup or first-use cost remain a runtime warning or blocker?
does any row fail processing completeness?
does any row fail release, cleanup, pressure, worker health, or validation?
does any backpressure or cancellation row have ambiguous behavior?
does runtime observability support an operator-visible decision?
does true live ingestion remain a named coverage gap?
```

Allowed follow-up fixes:

```text
test or reporting fixes required to make existing behavior reviewable
small guardrail fixes if an existing invariant is not covered or is broken
small attribution fixes if missing attribution is the only blocker
small prewarm lifecycle fixes if prewarm is selected and the ownership model
  remains in-process and benchmark/runtime scoped
small backpressure or cancellation result-shape fixes if semantics are already
  present but not visible or not deterministic
```

Not allowed as follow-up fixes without replanning:

```text
durable queue or broker integration
cross-process worker/provider transport
ordered concurrent rebalance
automatic borrowed fallback
builder-transfer retained payload execution
broad retained payload architecture rewrite
new product-facing live radar feature work
```

Expected output:

```text
gate interpretation notes
follow-up fix list, or explicit no-fix posture
decision-trace input
residual warnings and blockers
```

Slice 8 status:

```text
status: pending
runtime behavior changes: possible scoped fixes only after gate evidence
```

### 9. Runtime Readiness Decision Trace

Write the formal decision trace.

Expected document:

```text
docs/milestones/018-runtime-live-ingestion-readiness-decision-trace.md
```

Decision trace must include:

```text
date
top-level decision
included runtime/archive/live-shaped surfaces
excluded durable/cross-process/ordered-concurrent surfaces
direct benchmark evidence from milestone 017 as input, not automatic approval
runtime provider default versus explicit opt-in posture
runtime prewarm posture
runtime lifecycle posture
pressure and backpressure posture
fallback/oracle posture
failure and cancellation posture
processing completeness posture
steady intake gate summary
pressure/backpressure/cancellation/failure gate summary
operator/observability posture
accepted warnings, optimization targets, blockers, or coverage gaps
recommended next milestone input
```

Valid decision-trace outcomes:

```text
accept runtime/live queued-owned default readiness
accept runtime/live queued-owned default readiness with named scoped warnings
keep queued-owned runtime-safe as explicit opt-in only
require named optimization before runtime default expansion
record architecture blocker
reject runtime/live queued-owned readiness with named blocker
record coverage insufficient for runtime/live default readiness
defer because gate health or repeatability regressed
```

Slice 9 status:

```text
status: pending
runtime behavior changes: none expected
```

### 10. Closeout, Handoff, And Project Progress

Finalize milestone documentation and project handoff.

Expected document:

```text
docs/milestones/018-runtime-live-ingestion-readiness-closeout.md
```

Expected updates:

```text
docs/handoff.md
docs/project-progress.md
```

Closeout must include:

```text
final status
implemented changes
not implemented
final runtime/live readiness posture
runtime provider default or explicit opt-in answer
prewarm lifecycle answer
pressure/backpressure answer
failure/cancellation/cleanup answer
gate summary
decision trace pointer
preserved invariants
residual risks and limits
recommended next milestone input
final verification
```

Handoff must include:

```text
current milestone status
current runtime/default posture
direct benchmark carry-forward posture
runtime prewarm posture
fallback/oracle posture
processing completeness posture
pressure/backpressure posture
live/durable/cross-process out-of-scope posture
final verification
recommended next milestone input
```

Project progress must include:

```text
milestone 018 final answer
what was achieved
what it prepared
important warnings or scope limits
verification summary
recommended next milestone input
whether the project chain changed
```

Slice 10 status:

```text
status: pending
runtime behavior changes: none expected
```

## Verification Strategy

Use focused tests before runtime gate capture, then Release build and
runtime-shaped Release gates for the readiness decision.

Focused regression candidate:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingOwnedBatchQueueTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests|FullyQualifiedName~RadarProcessingProviderQueueContractTests|FullyQualifiedName~RadarProcessingProviderQueueTelemetryRecorderTests|FullyQualifiedName~RadarProcessingWorkerLifecycleContractTests|FullyQualifiedName~RadarProcessingWorkerTelemetryContractTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Full test project before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Runtime Release gates:

```text
run runtime-shaped deterministic archive replay steady intake rows
run runtime-shaped mixed-radar/source-universe rows
run runtime-shaped prewarm or first-use rows according to selected posture
run runtime-shaped queue pressure and retained-byte pressure rows
run runtime-shaped cancellation rows
run runtime-shaped failure/fault rows
run runtime-shaped drain/stop/dispose cleanup rows
keep explicit borrowed/reference rows where comparison is meaningful
```

The runtime gates should capture:

```text
runtime surface and input shape
provider mode and provider mode source
fallback/oracle contour yes/no
default candidate contour yes/no
execution mode
worker count and worker queue capacity
provider queue capacity
retained-byte budget
retention strategy
provider overlap mode
prewarm enabled state and source
prewarm sizing, elapsed time, allocated bytes, retained bytes
provider enqueue attempts, accepted, rejected, waited, timed out, canceled,
  and faulted counts
queue depth high-water
retained pending, active, and combined current/high-water values
retained payload pool telemetry
event-array pool telemetry
byte-array pool telemetry
worker telemetry and worker health
processing completeness
processing validation failed batches
worker failed batches/items
validation status and checksum/reference parity
topology versions and rebalance counters
release attempts and failed releases
provider overlap failed releases
shutdown/drain/cancellation/fault result
startup/prewarm/first-use/steady-state/backpressure/drain cost attribution
per-row or per-scenario status
```

## Completion Checklist

Milestone 018 is complete when:

```text
[ ] runtime/live/archive-provider surfaces under review are inventoried
[ ] runtime default versus explicit opt-in provider posture is defined
[ ] direct benchmark readiness from milestone 017 is treated as evidence, not
    automatic runtime approval
[ ] runtime lifecycle states are documented and gateable
[ ] runtime prewarm policy is accepted, rejected, explicit opt-in only, or
    deferred with named reasons
[ ] startup/prewarm/first-use costs are attributed separately from
    steady-state processing cost
[ ] provider queue and worker queue backpressure behavior is defined
[ ] retained-byte pressure policy is defined and gateable
[ ] explicit BlockingBorrowed remains available where required and is not used
    as silent fallback
[ ] queued-owned failures remain fail-closed with operator-visible failure
    details
[ ] processing completeness is required for runtime success
[ ] processing validation failed batches and worker failed batches/items are
    readiness blockers unless explicitly accepted with named scope
[ ] retained cleanup returns pending, active, and combined pressure to zero
    after success, cancellation, drain, fault, and dispose gates
[ ] release failures remain 0 or block readiness
[ ] runtime-shaped gates cover steady intake, backpressure, cancellation,
    failure, drain, cleanup, release, pressure, ordering, and observability
[ ] runtime cost thresholds or interpretation bands are recorded before gate
    interpretation
[ ] decision trace records whether runtime/live queued-owned readiness is
    accepted, accepted with warnings, explicit opt-in only,
    optimization-bound, architecture-blocked, rejected,
    coverage-insufficient, or deferred
[ ] closeout records verification, gate results, residual risks, and carry
    forward items
[ ] handoff and project-progress state the current runtime readiness posture
    and recommended next milestone unambiguously
```

## Non-Goals

Milestone 018 does not implement:

```text
durable queue or broker integration
cross-process provider or worker transport
ordered concurrent rebalance commit barrier
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
physical worker-local state transfer
distributed workers
complex radar algorithms
visualization or product-facing radar analysis features
automatic silent fallback from queued-owned failure to borrowed success
threshold changes after gate capture
synthetic processing benchmark default migration
production deployment, alerting, or rollback runbooks beyond runtime readiness
```

## Closeout Question

The milestone closes by answering:

```text
Is the queued-owned contour ready for runtime/live ingestion defaults?
```

Valid answers:

```text
yes, runtime/live queued-owned default readiness is accepted

yes with warnings, runtime/live queued-owned default readiness is accepted
with named scoped warnings

explicit opt-in only, queued-owned is runtime-safe when selected explicitly but
not accepted as the omitted/default runtime posture

optimize, runtime/live queued-owned default readiness requires a named
optimization before default expansion

architecture blocker, runtime/live readiness cannot be decided or accepted
because a required lifecycle, pressure, backpressure, prewarm, fallback,
failure, cancellation, cleanup, or observability contract is missing

no, runtime/live queued-owned readiness is rejected with a named lifecycle,
validation, processing completeness, release, cleanup, pressure, worker,
fallback, or attribution blocker

coverage insufficient, runtime/live queued-owned readiness cannot be decided
from the available workload evidence

defer, runtime/live queued-owned readiness cannot be decided because test
health, instrumentation, local corpus, gate repeatability, or runtime harness
stability regressed
```

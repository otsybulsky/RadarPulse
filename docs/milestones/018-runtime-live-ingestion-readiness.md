# Milestone 018: Runtime And Live Ingestion Readiness Architecture

Status: draft.

RadarPulse milestone 018 starts from the closed milestone 017 file-level and
small-file direct benchmark default readiness result.

Milestone 017 accepted direct benchmark default readiness for:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
CLI rebalance-archive benchmark paths that use the same direct APIs
```

The accepted direct/default contour is:

```text
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

Milestone 017 closed with this answer:

```text
yes with warnings, file-level and small-file default readiness is accepted for
the queued-owned direct benchmark default-equivalent contour with retained
payload prewarm
```

The important scope limit carried forward is:

```text
direct benchmark readiness is evidence, not approval for live ingestion,
runtime provider defaults, durable queues, cross-process workers, ordered
concurrent rebalance, builder-transfer, or non-benchmark archive publishing
API defaults
```

This document is intentionally not an implementation plan. It records the
milestone 018 concept, runtime/live ingestion readiness model, runtime
lifecycle boundaries, prewarm policy questions, pressure and backpressure
model, fallback and failure posture, observability requirements, validation
gate boundaries, documentation requirements, and expected closeout decision
before task breakdown is written.

The core decision is:

```text
017 accepted the queued-owned direct benchmark file/cache default contour with
    retained payload prewarm and named scoped warnings.
018 decides whether queued-owned can move beyond direct benchmark surfaces
    into runtime/live ingestion defaults, or whether runtime must remain on a
    borrowed/explicit/non-default posture until lifecycle, pressure, fallback,
    and observability requirements are met.
```

Milestone 018 should not broaden into durable queues, broker integration,
cross-process workers, ordered concurrent rebalance, builder-transfer,
physical worker-local state transfer, source-level migration, partition
splitting, or product-facing radar analysis. It should be a runtime readiness
milestone over the already-proven in-process queued-owned foundation.

## Milestone Goal

Milestone 018 should turn the benchmark-only queued-owned readiness answer
into a reviewable runtime/live ingestion readiness decision.

The output of the milestone is the architectural definition of:

```text
runtime/live ingestion surface inventory
runtime input and lifecycle contract
runtime provider selection rules
runtime retained payload prewarm policy
startup, first-use, and steady-state cost attribution
runtime pressure and backpressure policy
operator-visible fallback and failure behavior
cleanup, cancellation, drain, and release guardrails
processing completeness and worker failure requirements
runtime-shaped validation gates
explicit statement of whether direct benchmark readiness is sufficient input,
  insufficient input, or one accepted input among other runtime evidence
decision trace that records whether runtime/live defaults are accepted
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Only owned or retained-owned input may enter provider queues.
Owned or retained-owned resources release only after final use.
Provider enqueue success remains distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Processing validation failed batches are processing-completeness blockers.
Worker failed batches/items are processing-completeness blockers.
Controlled consumer delay remains mechanics-only proof.
Builder-transfer remains unsupported.
BlockingBorrowed remains explicitly selectable.
Same-run BlockingBorrowed remains the benchmark oracle where benchmark gates
  are used.
Queued-owned failures fail closed.
No automatic borrowed fallback follows queued-owned failure.
Retained payload prewarm cost remains explicit when prewarm is used.
Direct benchmark default readiness remains scoped to benchmark APIs until a
  runtime decision trace says otherwise.
```

The key milestone boundary is:

```text
safe in 018:
  audit runtime/live ingestion entry points and current archive provider paths
  define runtime provider default selection without assuming benchmark
    default inheritance
  decide whether retained payload prewarm is allowed, required, optional, or
    rejected for runtime
  define startup, first-use, shutdown, drain, cancellation, and faulted
    lifecycle semantics
  define runtime pressure and backpressure behavior for bounded provider and
    worker queues
  keep explicit BlockingBorrowed as fallback/oracle where the surface supports
    it
  keep queued-owned failure fail-closed and operator-visible
  build runtime-shaped gates that use deterministic archive replay as a live
    input stand-in if no live adapter exists yet
  require processing completeness, cleanup, release, pressure, worker health,
    and validation visibility
  record readiness, warning, blocker, coverage insufficiency, or deferral in a
    decision trace

not safe in 018 unless explicitly reprioritized:
  treating direct benchmark readiness as automatic runtime approval
  hiding runtime prewarm allocation or startup cost
  adding automatic silent borrowed fallback after queued-owned failure
  implementing durable queue or broker semantics
  adding cross-process provider or worker transport
  adding ordered concurrent rebalance commit semantics
  implementing builder-transfer retained payload execution
  making non-benchmark archive publishing APIs default to queued-owned without
    a runtime decision trace
  changing source-universe, topology, or rebalance ordering semantics to make
    a runtime gate pass
  raising runtime thresholds after seeing gate measurements
```

## Expected Outcome

At the end of milestone 018, RadarPulse should have a clear answer to this
question:

```text
Is the queued-owned contour ready for runtime/live ingestion defaults?
```

The acceptable outcomes are:

```text
ready:
  runtime/live ingestion defaults can use queued-owned with named lifecycle,
  pressure, prewarm, cleanup, fallback, cancellation, and observability
  contracts; gates pass and the decision trace names the accepted contour

ready with scoped warnings:
  queued-owned runtime defaults are accepted, but one or more warnings remain
  explicitly assigned to a named lifecycle stage, workload shape, timing
  contour, prewarm cost, pressure condition, or operator surface

explicit opt-in only:
  queued-owned is runtime-safe when selected explicitly, but it is not accepted
  as the omitted/default runtime provider posture

optimize before runtime default:
  correctness and lifecycle guardrails pass, but prewarm cost, startup cost,
  backpressure behavior, retained allocation, timing, or attribution is too
  costly or too poorly bounded for runtime defaults

architecture blocker:
  a required runtime concept is missing, such as lifecycle ownership, pressure
  policy, backpressure behavior, cancellation semantics, observability, or
  operator fallback policy

not ready:
  runtime-shaped gates fail correctness, cleanup, release, pressure,
  processing completeness, worker health, validation, or fail-closed behavior

coverage insufficient:
  the available runtime/live-shaped evidence is too narrow to support a
  default decision; the decision trace names the missing workload evidence

defer:
  test health, instrumentation, repeatability, local corpus condition, or
  runtime harness reliability regresses and readiness cannot be decided safely
```

The milestone should not close with a vague monitoring posture. It should
accept runtime readiness, accept it with named warnings, keep queued-owned as
explicit opt-in, require a named optimization, reject readiness with a named
blocker, or state exactly what workload coverage is missing.

## Starting Position

Milestone 017 closed the current direct benchmark default contour:

```text
surface:
  RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  RadarProcessingArchiveRebalanceBenchmark.MeasureCache()

omitted controls:
  providerMode
  executionMode
  asyncExecution
  queueCapacity
  providerOverlapMode
  retentionStrategy
  queueRetainedPayloadBytes
  overlapConsumerDelay

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
  retained payload prewarm sizing:
    65_536 events
    67_108_864 payload bytes
    1 retained batch
```

Milestone 017 evidence carried forward:

```text
broader cache-level readiness:
  accepted with named scoped warnings

file-level and small-file readiness:
  accepted with retained payload prewarm as the scoped direct benchmark
  default-equivalent path

natural unprewarmed file and low-count cache rows:
  safety-clean but allocation-blocked

prewarmed file rows:
  measured allocation ratios 0.980x to 1.026x borrowed
  retained pool misses 0
  release failures 0
  worker failed batches/items 0/0

prewarmed small-cache rows:
  measured allocation ratios 0.818x to 1.002x
  elapsed ratios 0.454x to 0.979x
  retained pool misses 0

post-default cache regression:
  16/16 group rows passed
  28/28 safety pairs passed
  processing completeness failures 0
  worker failed batches/items 0/0
  release failures 0
  current retained bytes after rows 0
```

Milestone 017 warnings and limits carried forward:

```text
prewarm cost:
  retained payload prewarm is a real up-front default cost and must remain
  explicitly reported outside measured row allocation

natural cold allocation:
  natural unprewarmed MeasureFile and low-count MeasureCache rows remain
  allocation-blocked and are not the accepted readiness contour

filesystem timing:
  local file I/O timing variance remains visible

runtime scope:
  direct benchmark readiness does not approve live ingestion/runtime defaults
```

The current runtime-related foundation already exists in pieces:

```text
retained async shard transport
owned payload provider decoupling
bounded owned batch queue
queued processing and queued rebalance sessions
retained payload factory and retained resource pressure telemetry
provider queue telemetry
worker lifecycle, health, timeout, failure, and cancellation vocabulary
archive replay and archive RadarEventBatch publisher surfaces
CLI benchmark provenance and retained payload prewarm attribution
processing completeness reporting for archive rebalance results
```

Milestone 018 starts from these foundations, but it must not assume that a
direct benchmark API decision has already selected the runtime provider
default.

## Architectural Principles

Milestone 018 should follow these principles:

```text
runtime readiness is a lifecycle decision, not only a benchmark ratio decision
direct benchmark readiness is accepted evidence, not automatic inheritance
the runtime provider default must be named by a runtime decision trace
prewarm must be a lifecycle policy, not a hidden measurement trick
startup, first-use, steady-state, drain, shutdown, and faulted states must be
  separately observable
bounded queues and retained-byte budgets are runtime contracts
backpressure is preferred over unbounded buffering or silent dropping
provider enqueue success and processing completion remain separate facts
processing completeness is required for any runtime success claim
worker failures and processing validation failures block readiness
queued-owned failures fail closed and remain operator-visible
fallback is explicit operator or caller action, not automatic recovery
cleanup and release health are non-negotiable
runtime-shaped gates must exercise cancellation, fault, drain, and pressure
durable/cross-process semantics remain future work
```

The milestone should separate these concerns:

```text
provider default:
  which provider mode is selected when runtime callers omit provider controls

runtime safety:
  whether queued-owned can run continuously through lifecycle transitions
  without leaking retained resources, hiding failures, or corrupting
  processing state

prewarm policy:
  whether retained payload resources are prewarmed at startup, lazily, on
  demand, never, or only by explicit operator action

backpressure policy:
  how provider intake reacts when queues, workers, retained-byte budgets, or
  processing sessions cannot keep up

operator posture:
  what is visible to a runtime operator when default selection, fallback,
  prewarm, pressure, failure, or shutdown occurs

next decision:
  whether durable/cross-process runtime can be designed next, queued-owned
  remains explicit opt-in, or a named runtime blocker must be fixed first
```

## Runtime Surface Vocabulary

Milestone 018 should use precise vocabulary for runtime surfaces.

### Direct Benchmark Surface

The direct benchmark surface is:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
CLI rebalance-archive benchmark commands using those APIs
```

This surface is already accepted with retained payload prewarm. It is a source
of evidence and a regression baseline for milestone 018. It is not itself a
runtime/live ingestion surface.

### Runtime Archive Provider Surface

The runtime archive provider surface means archive replay or archive
`RadarEventBatch` publishing paths used as a long-running provider into
processing, not just one-shot direct benchmark calls.

This surface may use deterministic local archive replay as a stand-in for live
input because archive replay gives repeatable data, checksums, and failure
diagnosis. If used as a stand-in, the gate must still run through runtime
session lifecycle semantics rather than isolated direct benchmark rows.

### Live Ingestion Surface

The live ingestion surface means a future or existing provider path where
RadarPulse receives incoming radar batches as an ongoing stream.

Milestone 018 may decide that true live ingestion is not implemented yet. If
so, it should define the minimum live-ingestion contract and use a
runtime-shaped archive harness as evidence, while naming any live-only gaps as
coverage limits.

### Runtime Default

A runtime default is the provider, retention, queue, execution, prewarm, and
failure policy selected when runtime callers or operators omit those controls.

Runtime default readiness requires a runtime decision trace. The direct
benchmark default contour from milestone 017 is not automatically the runtime
default.

### Explicit Runtime Opt-In

Explicit runtime opt-in means queued-owned is supported when selected
deliberately by caller configuration or operator controls, but omitted runtime
provider controls do not select it yet.

This may be the correct milestone 018 outcome if queued-owned is safe but its
default lifecycle policy is not ready.

## Runtime Input And Ownership Model

Milestone 018 should preserve the existing input and ownership boundaries.

Required input contract:

```text
RadarEventBatch remains the processing input
borrowed/leased RadarEventBatch values may not outlive the publish callback
owned RadarEventBatch values may be queued after callback return
retained-owned payload resources may be queued only with explicit ownership
  and release semantics
source universe must match the selected radar/source set
source-order validation failures block processing completeness
processing validation failed batches block runtime success
worker failed batches/items block runtime success
```

Runtime provider intake must prove:

```text
borrowed input is processed before callback return or converted to owned input
owned or retained-owned input is released after final use
queued input uses provider sequence ordering
processing captures topology at processing time
failed processing does not claim provider success
shutdown drains, cancels, or releases queued input according to an explicit
  policy
```

The runtime architecture should avoid these mistakes:

```text
queuing borrowed input past callback lifetime
describing provider enqueue as processing completion
allowing source-universe drift to become worker failures
making validation success ignore processing-result invalidity
using benchmark prewarm as hidden runtime allocation
letting retained resources survive shutdown or faulted sessions
```

## Runtime Provider Selection Model

Milestone 018 must define how runtime provider mode is selected.

Candidate provider modes:

```text
BlockingBorrowed:
  explicit borrowed fallback/oracle path
  low ownership complexity
  limited provider/processing decoupling

QueuedOwned:
  candidate runtime provider posture
  requires owned or retained-owned input
  requires bounded queues, retained pressure, release, cleanup, and
  processing-completeness guardrails
```

Runtime selection must report provenance:

```text
provider mode
provider mode source:
  omitted runtime default
  explicit operator/caller selection
  rollout default
  diagnostic fallback/oracle
provider overlap mode
retention strategy
execution mode
worker count and worker queue capacity
provider queue capacity
retained-byte budget
prewarm enabled state and source
fallback contour yes/no
default candidate contour yes/no
```

Fallback remains explicit:

```text
BlockingBorrowed may remain available as an operator-selectable fallback or
diagnostic oracle.

Queued-owned failure must not silently rerun the same runtime batch as borrowed
and overwrite the failure with success.
```

If milestone 018 accepts queued-owned as a runtime default, it must also state
how explicit BlockingBorrowed remains available, visible, and testable.

If milestone 018 does not accept queued-owned as a runtime default, it should
state whether queued-owned is still runtime-safe as explicit opt-in.

## Runtime Lifecycle Model

Runtime readiness requires lifecycle states, not only per-row benchmark
success.

The runtime lifecycle should define:

```text
configure:
  validate provider, execution, queue, retention, prewarm, pressure, and
  telemetry options before intake starts

start:
  create provider queues, retained payload factories, worker groups, telemetry
  recorders, pressure recorders, and runtime session state

prewarm:
  optionally allocate and retain runtime resources before first intake if the
  runtime policy chooses prewarm

accept intake:
  receive or publish incoming RadarEventBatch values through the selected
  provider boundary

apply backpressure:
  wait, reject, time out, or cancel intake when queues, workers, or retained
  budgets cannot accept more work

process:
  dequeue input, capture topology, dispatch work, validate, aggregate, and
  run rebalance only after successful processing

drain:
  stop accepting new input and finish accepted queued work according to the
  selected shutdown mode

cancel:
  stop intake and cancel pending or active work according to explicit policy

fault:
  stop intake, release pending retained resources, expose failure details, and
  prevent later success claims

stop/dispose:
  release retained resources, dispose worker groups, close queues, and leave
  retained pressure at zero
```

Every state transition should have a result shape. Runtime code should not
depend on benchmark process lifetime to clean up retained resources.

## Runtime Prewarm Policy

Milestone 017 accepted prewarm for the direct benchmark default-equivalent
contour. Milestone 018 must decide whether that idea belongs in runtime.

Possible runtime prewarm postures:

```text
no runtime prewarm:
  runtime accepts natural first-use cost or keeps queued-owned non-default

startup prewarm:
  retained payload resources are prewarmed during runtime start before intake
  begins

lazy first-use prewarm:
  the first queued-owned runtime intake pays or triggers prewarm explicitly
  with visible attribution

operator-triggered prewarm:
  prewarm is an explicit operation before enabling queued-owned runtime intake

explicit opt-in prewarm only:
  runtime default does not prewarm, but callers can provide a retained payload
  factory or prewarm policy deliberately
```

Any accepted runtime prewarm policy must define:

```text
when prewarm runs
who owns the retained payload factory
what happens if prewarm fails
whether intake starts before prewarm completes
how prewarm allocation and elapsed time are reported
how retained prewarm resources are released
how prewarm interacts with retained-byte budget
whether prewarm is repeated after fault, dispose, or configuration change
whether prewarm sizing is fixed, derived from config, or workload-sensitive
whether borrowed fallback/oracle paths remain unprewarmed
```

The direct benchmark default prewarm sizing is input evidence:

```text
event count: 65_536
payload bytes: 67_108_864
retained batch count: 1
```

It is not automatically the runtime prewarm sizing. Runtime sizing may need to
account for:

```text
continuous intake
provider queue capacity
worker queue capacity
expected batch shape
source universe size
retained-byte budget
startup latency budget
operator memory budget
failure recovery policy
```

The milestone should not hide prewarm cost. If runtime prewarm is accepted,
its cost must be operator-visible.

## Pressure And Backpressure Model

Runtime readiness requires bounded pressure behavior.

Required pressure surfaces:

```text
provider queue depth
provider queue retained payload bytes
worker queue depth
retained pending batch count and bytes
retained active batch count and bytes
combined retained batch count and bytes
combined retained high-water
retained event-array pool rent/return/miss counts
retained byte-array pool rent/return/miss counts
release attempts and failed releases
processing backlog and drain latency
```

Backpressure policy must define:

```text
what happens when the provider queue is full
what happens when retained-byte budget would be exceeded
what happens when worker queues are full
what happens when processing faults while provider intake continues
what happens when cancellation is requested before enqueue
what happens when cancellation is requested after enqueue
what happens when shutdown starts with pending queued batches
what happens when release fails
```

Allowed first-runtime policies:

```text
wait with cancellation
wait with timeout and visible rejection
reject new intake after fault
stop accepting and drain accepted work
cancel pending work and release retained resources
fail closed with telemetry
```

Policies to avoid in milestone 018:

```text
unbounded queues
silent drops
silent borrowed fallback
continuing intake after processing fault without an explicit recovery policy
returning success while retained resources remain pending or active
hiding retained pressure by disabling telemetry
raising retained-byte budget to make a gate pass without a decision trace
```

## Ordering And Rebalance Model

Milestone 018 should preserve the ordered rebalance contract.

Runtime queued-owned processing should keep this flow:

```text
provider sequence N is accepted
processing dequeues sequence N
processing captures topology version T
workers process sequence N against topology T
processing completeness succeeds
validation succeeds
rebalance evaluates after successful processing
accepted topology changes publish after sequence N completes
provider sequence N+1 captures the latest topology when it is processed
```

Milestone 018 should not introduce ordered concurrent rebalance. The following
remains future work:

```text
multiple active rebalance-enabled batches
out-of-order worker completion across batch sequences
topology publication while older accepted batches are still processing
commit barriers for concurrent rebalance
retry/recovery semantics over partially processed ordered batches
```

The runtime gate may include queue-ahead provider behavior, but processing and
rebalance publication should remain sequence-ordered unless the milestone is
explicitly replanned.

## Failure And Cancellation Model

Runtime readiness must make failure and cancellation semantics explicit.

Required failure cases:

```text
invalid configuration
prewarm failure
owned snapshot or retained payload creation failure
provider enqueue rejection
provider enqueue timeout
provider enqueue cancellation
provider queue fault
worker queue rejection
worker exception
worker timeout or unhealthy transition
processing validation failure
source-order violation
rebalance validation failure
release failure
provider overlap release failure
retained pressure budget exceedance
shutdown during pending intake
shutdown during active processing
dispose with queued work
```

Required cancellation cases:

```text
canceled before runtime start
canceled during prewarm
canceled before enqueue
canceled while waiting for enqueue capacity
canceled after accepted enqueue but before dequeue
canceled while active batch is processing
canceled during drain
canceled during stop/dispose
```

The default failure posture should be:

```text
stop or reject later intake after terminal queued-owned failure
release pending retained resources
let active retained resources release after final use or cancellation
mark processing completeness failed when worker or validation failures occur
avoid topology publication from failed or partial processing
report failure through bounded diagnostics
do not automatically claim borrowed success
```

If any of these cases cannot be tested or observed, the decision trace should
record an architecture blocker or coverage insufficiency.

## Observability And Operator Surface

Runtime readiness requires operator-visible facts.

The runtime surface should expose:

```text
runtime provider mode and source
runtime default candidate yes/no
runtime fallback/oracle contour yes/no
execution mode
worker count and worker queue capacity
provider queue capacity
retained-byte budget
retention strategy
provider overlap mode
prewarm enabled state and source
prewarm sizing
prewarm elapsed time
prewarm allocated bytes
prewarm retained bytes
provider enqueue attempts, waits, rejects, timeouts, cancellations, and faults
queue depth high-water
retained pressure current and high-water
retained payload pool telemetry
worker telemetry and health transitions
processing completeness
processing validation failed batches
worker failed batches/items
release attempts and failed releases
provider overlap failed releases
shutdown/drain/cancellation result
final runtime status
```

Operator language should distinguish:

```text
configured default:
  what the runtime was configured to use

effective default:
  what the runtime actually used after default expansion

explicit fallback:
  a caller or operator selected borrowed/fallback behavior

automatic fallback:
  not allowed in milestone 018

prewarm cost:
  startup or first-use work paid before or during intake and reported
  separately from processing row allocation

processing completeness:
  validation succeeded and no processing validation failed batches or worker
  failures occurred
```

If runtime readiness is accepted without these fields, the decision trace must
explain why the missing field is not required. The default posture should be
to require visibility.

## Runtime-Shaped Workload Model

Milestone 018 should define runtime-shaped evidence before interpreting
readiness.

Runtime-shaped workload evidence differs from direct benchmark rows:

```text
the session has explicit start and stop
prewarm, if used, happens through runtime lifecycle
multiple batches pass through the same runtime session
provider enqueue and processing completion are separate
backpressure can be observed
drain or shutdown can be observed
faults stop or shape later intake
retained pressure is measured across the session, not only one row
worker health is measured across session lifecycle
operator-visible provenance is captured
```

Deterministic local archive replay is acceptable as the first live-input
stand-in if the gate states that:

```text
archive replay provides deterministic input, checksums, and corpus repeatability
the runtime harness exercises runtime lifecycle, not direct benchmark APIs only
the evidence does not prove network, radar feed, external IO, durable broker,
or cross-process behavior
```

Candidate workload shapes:

```text
single-radar continuous archive replay over KTLX 2026-05-04
cross-radar runtime session over KTLX and KINX selected files
small-file first-use runtime session where prewarm and first-use cost matter
broader cache-level runtime session using accepted milestone 016/017 contours
mixed-cache runtime session that proves source-universe sizing and processing
  completeness
backpressure contour with queue capacity pressure
cancellation contour before enqueue, after enqueue, and during active
  processing
fault contour for validation or worker failure
shutdown/drain contour with pending queued work
```

The implementation plan may narrow or expand this list, but it should select
runtime-shaped workloads before gate interpretation.

## Validation Gate

Milestone 018 should use a runtime/live ingestion readiness gate.

Required dimensions:

```text
surface inventory:
  name the runtime/live/archive provider surfaces under review and the
  surfaces explicitly excluded

default expansion:
  show whether omitted runtime provider controls choose queued-owned,
  borrowed, or explicit opt-in only

fallback separation:
  explicit BlockingBorrowed remains visible where available and is not
  automatic fallback

lifecycle:
  start, optional prewarm, intake, pressure, processing, drain, cancellation,
  fault, stop, and dispose behavior are tested or explicitly scoped

prewarm attribution:
  startup or first-use retained payload prewarm cost is visible if prewarm is
  used

correctness parity:
  deterministic output and validation match the selected reference path where
  a borrowed/reference path exists

processing completeness:
  processing validation failed batches and worker failed batches/items are 0
  for accepted readiness rows

cleanup:
  current pending, active, and combined retained counts/bytes return to zero
  after drain, cancellation, fault, and dispose

release health:
  retained payload and provider overlap failed releases remain 0

pressure:
  combined retained high-water remains within the configured retained-byte
  budget

backpressure:
  full queues, retained-byte pressure, timeout, and cancellation produce
  documented result states

worker health:
  worker failures, cancellation, timeout, and unhealthy transitions are
  visible and block readiness unless explicitly accepted

ordering:
  provider sequence ordering and topology publication ordering remain valid

observability:
  runtime result and/or CLI output exposes the fields needed to explain the
  decision
```

Benchmark ratios may remain useful, but they are not sufficient by themselves.
Runtime readiness depends on lifecycle and failure behavior as much as elapsed
and allocation ratios.

## Runtime Cost Model

Milestone 018 should treat runtime cost as a lifecycle cost.

Required cost categories:

```text
startup configuration and validation
retained payload prewarm elapsed time and allocation
first-use retained allocation if no startup prewarm is used
owned snapshot or retained payload creation cost
provider enqueue wait cost
queue drain cost
worker dispatch and queue wait cost
worker execution cost
rebalance/control-plane cost
shutdown/drain/cancellation cost
retained resource cleanup and release cost
steady-state allocation and elapsed time
```

Runtime readiness may accept a cost only if:

```text
the lifecycle stage paying the cost is named
the cost is visible to result contracts or operator output
the cost is bounded against a recorded threshold or accepted warning
the cost does not hide processing failures, release failures, or retained
  pressure leaks
```

The implementation plan should define runtime thresholds before gate
interpretation. It may use milestone 017 benchmark thresholds as inputs, but
runtime thresholds must account for startup/prewarm and long-running session
behavior.

## Operator And Documentation Surface

Milestone 018 should update operator and documentation language only after the
runtime decision is made.

The operator/documentation surface should make these statements true:

```text
direct benchmark defaults remain accepted as of milestone 017
runtime/live defaults are accepted, explicit opt-in only, rejected, deferred,
  or coverage-insufficient according to milestone 018
retained payload prewarm is described as benchmark-only, runtime startup
  policy, runtime first-use policy, explicit operator action, or not accepted
  for runtime
BlockingBorrowed remains explicit fallback/oracle where supported
queued-owned failure remains fail-closed
processing completeness is a runtime success requirement
worker failed batches/items are runtime blockers
release and retained pressure cleanup remain runtime blockers
durable queues, cross-process workers, ordered concurrent rebalance, and
  builder-transfer remain out of scope unless a future milestone accepts them
```

Expected milestone documents:

```text
docs/milestones/018-runtime-live-ingestion-readiness.md
docs/milestones/018-runtime-live-ingestion-readiness-plan.md
docs/milestones/018-runtime-live-ingestion-readiness-performance-gate.md
docs/milestones/018-runtime-live-ingestion-readiness-decision-trace.md
docs/milestones/018-runtime-live-ingestion-readiness-closeout.md
docs/handoff.md
docs/project-progress.md
```

The implementation plan may add specialized gate documents if runtime
evidence needs to be split, for example lifecycle audit, prewarm gate,
backpressure gate, or cancellation/failure gate.

## In Scope

Milestone 018 includes:

```text
runtime/live/archive-provider surface inventory
runtime lifecycle architecture
runtime provider selection and default policy
runtime queued-owned explicit opt-in versus default decision
runtime retained payload prewarm policy
startup, first-use, steady-state, drain, cancellation, fault, and dispose
  contracts
runtime pressure and backpressure policy
explicit BlockingBorrowed fallback/oracle preservation where supported
runtime processing completeness requirements
runtime source-universe and mixed-radar processing completeness guardrails
runtime observability and operator output requirements
runtime-shaped deterministic archive replay gates as live-input stand-ins
runtime cancellation, failure, drain, cleanup, and release guardrails
runtime cost attribution and threshold language
decision trace that records runtime/live readiness posture
handoff and project-progress updates for runtime readiness and next milestone
  recommendation
```

## Out Of Scope

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
non-benchmark archive publishing API default migration unless explicitly
  included by the runtime decision trace
synthetic processing benchmark default migration
production deployment, alerting, or rollback runbooks beyond the runtime
  readiness evidence needed for this milestone
```

## Completion Criteria

Milestone 018 is complete when:

```text
runtime/live/archive-provider surfaces under review are inventoried

runtime default versus explicit opt-in provider posture is defined

direct benchmark readiness from milestone 017 is treated as evidence, not as
  automatic runtime approval

runtime lifecycle states are documented: configure, start, prewarm, intake,
  pressure, process, drain, cancel, fault, stop, and dispose

runtime prewarm policy is accepted, rejected, explicit opt-in only, or
  deferred with named reasons

startup/prewarm/first-use costs are attributed separately from steady-state
  processing cost

provider queue and worker queue backpressure behavior is defined

retained-byte pressure policy is defined and gateable

explicit BlockingBorrowed remains available where required and is not used as
  silent fallback

queued-owned failures remain fail-closed with operator-visible failure
  details

processing completeness is required for runtime success

processing validation failed batches and worker failed batches/items are
  readiness blockers unless explicitly accepted with named scope

retained cleanup returns pending, active, and combined pressure to zero after
  success, cancellation, drain, fault, and dispose gates

release failures remain 0 or block readiness

runtime-shaped gates cover steady intake, backpressure, cancellation, failure,
  drain, cleanup, release, pressure, ordering, and observability

runtime cost thresholds or interpretation bands are recorded before gate
  interpretation

the decision trace records whether runtime/live queued-owned readiness is
  accepted, accepted with warnings, explicit opt-in only, optimization-bound,
  architecture-blocked, rejected, coverage-insufficient, or deferred

the closeout records verification, gate results, residual risks, and carry
  forward items

handoff and project-progress state the current runtime readiness posture and
  recommended next milestone unambiguously
```

## Likely Next Milestone Input

If milestone 018 accepts runtime/live ingestion readiness, the next milestone
can consider:

```text
durable queue or broker contract
cross-process retained payload ownership model
runtime recovery and retry policy
operator-visible production pipeline integration
ordered concurrent rebalance architecture
production deployment and observability profile
```

If milestone 018 accepts queued-owned as explicit opt-in only, the next
milestone should target the named blocker preventing runtime defaults:

```text
runtime prewarm policy
startup cost
first-use retained allocation
backpressure policy
operator fallback policy
observability gaps
shutdown or drain behavior
failure or cancellation coverage
runtime threshold uncertainty
```

If milestone 018 rejects readiness or finds a blocker, the next milestone
should target the named blocker:

```text
retained resource cleanup leak
release failure
worker failure or unhealthy transition
processing validation failure
source-universe sizing problem
queue pressure or retained-byte pressure exceedance
prewarm lifecycle failure
hidden or unattributed startup cost
automatic fallback ambiguity
insufficient runtime workload coverage
runtime harness instability
```

Still deferred unless explicitly reprioritized:

```text
durable queues
brokers
cross-process workers
ordered concurrent rebalance
builder-transfer
source-level migration
partition splitting
complex radar algorithms
product-facing radar workflows
```

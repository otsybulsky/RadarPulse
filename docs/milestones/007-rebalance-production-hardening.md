# Milestone 007: Rebalance Production Hardening Architecture

Status: draft.

RadarPulse milestone 007 starts from the closed milestone 006
partition-level shard rebalance baseline and defines the architecture for
production hardening of the synchronous rebalance control plane.

This document is intentionally not an implementation plan. It records the
hardening concept, control-plane boundaries, quarantine lifecycle, telemetry
retention model, allocation-reduction target, validation profiles, benchmark
scope, and expected result before any task breakdown is written.

Milestone 007 should harden the validated milestone 006 model before adding
real async worker transport. The synchronous `PartitionedBarrier` path remains
the reference correctness boundary.

## Milestone Goal

Milestone 007 should make the existing synchronous rebalance controller safe to
run for longer production-shaped sessions without unbounded diagnostic growth,
permanent hot-partition quarantine, avoidable allocation churn, or ambiguous
policy outcomes.

The output of the milestone is the architectural definition of:

```text
automatic quarantine lifecycle
bounded rebalance telemetry retention
allocation-attributed rebalance control plane
repeatable real-data benchmark contour
policy tuning surface for longer runs
validation profiles for diagnostic and steady-state use
production scheduler boundary that still preserves batch-safe publication
```

The resulting design must preserve the milestone 004, 005, and 006 contracts:

```text
RadarEventBatch remains the processing input.
SourceId -> PartitionId remains stable.
PartitionId -> ShardId changes only through versioned topology publication.
One batch is processed against one topology snapshot.
Accepted topology changes are published only between batch boundaries.
Leased payload storage is not retained past the synchronous processing callback.
Skipped rebalance decisions remain explainable telemetry.
```

Milestone 007 is not about retained async worker queues, live ingestion,
durable broker integration, partition splitting, source-level migration,
complex radar algorithms, or changing `RadarEventBatch`. It is about making
the synchronous rebalance control plane bounded, observable, and tunable enough
to be a credible reference for those later milestones.

## Expected Outcome

At the end of milestone 007, RadarPulse should have a clear architecture for a
production-hardened synchronous rebalance session.

The expected result is:

```text
quarantined hot partitions decay, clear, or become retry-eligible automatically
rebalance decisions are retained through bounded counters and recent-history windows
long-running telemetry can explain no-move behavior without storing every detail forever
allocation sources in rebalance sampling, planning, telemetry, and validation are attributed
avoidable allocation is reduced without changing the processing input contract
benchmark output separates replay cost, processing callback cost, and control-plane cost
policy defaults remain conservative until repeated real-data contours justify tuning
the synchronous path remains the correctness reference for future worker transport
```

The core idea is:

```text
006 proved that cautious partition-level rebalance works.
007 makes that control plane durable under longer runs.
It should not broaden rebalance behavior until telemetry and lifecycle rules
can explain and bound the consequences.
```

Milestone 007 should make the controller boring in the operational sense:
bounded memory, bounded diagnostic output, explicit lifecycle transitions,
repeatable measurements, and no hidden retention of leased data.

## Starting Position

Milestone 006 produced this closed baseline:

```text
RadarEventBatch
  -> synchronous processing against one topology snapshot
  -> partitioned telemetry with topology version
  -> windowed pressure sample
  -> cautious direct hot relief or cold evacuation
  -> validated migration between batches
  -> topology version N+1 for the next batch
  -> skipped-reason telemetry for every no-move path
```

The milestone 006 closeout accepted the following important signals:

```text
correctness:
  synthetic, single-file real-data, parallel real-data, and cache-wide real-data
  runs validated successfully with zero failed migrations

cautious behavior:
  cache-wide rebalance accepted only 2 direct-hot-relief moves across 220 real
  batches, then policy gates blocked further churn

callback throughput:
  cache-wide rebalance callback throughput remained in line with the milestone
  005 processing-only baseline

known cost:
  cache-wide real-data allocation was about 0.23 bytes/payload value versus
  0.03 in the milestone 005 processing-only synthetic baseline
```

Milestone 006 also intentionally deferred:

```text
retained async worker queues and real multi-core shard execution
timer-driven or production scheduler integration
automatic quarantine lifecycle integration
long-running production telemetry retention and sampling policy
cache-wide allocation profiling and reduction
repeated cache-wide benchmark runs and longer multi-radar scenarios
policy tuning from broader real-data contours
```

Those deferrals are the input to milestone 007.

## Architectural Principles

Milestone 007 should follow these principles:

```text
harden the control plane before adding async transport
preserve the synchronous correctness reference
make quarantine temporary unless pressure evidence says otherwise
keep skipped decisions visible but bounded
separate hot-path counters from diagnostic detail
measure allocation before changing policy behavior
reduce allocation without weakening immutable public results
keep replay timing separate from processing callback timing
prefer conservative policy defaults until real-data evidence supports changes
```

The most important separation is:

```text
data plane:
  route and process RadarEventBatch values against one captured topology

control plane:
  sample pressure, advance lifecycle state, plan movement, publish migration,
  validate invariants, and emit diagnostics

diagnostic plane:
  retain bounded summaries and optional recent detail for review, benchmark,
  and production troubleshooting
```

Milestone 007 should not blur these planes. Allocation and telemetry work must
not create a hidden dependency on leased payload storage or worker lifetime.

## Production Hardening Definition

For this milestone, "production hardening" does not mean live ingestion or a
durable distributed runtime. It means that the current in-process synchronous
controller can be exercised for longer realistic runs while preserving:

```text
bounded memory growth
bounded diagnostic detail
deterministic policy outcomes
observable no-move reasons
automatic stale-quarantine recovery
repeatable benchmark interpretation
clear validation cost controls
```

The milestone should be considered successful when a reviewer can answer:

```text
why did the controller move or not move?
how long can a hot partition remain quarantined?
what evidence makes a quarantined partition retry-eligible?
which allocations are caused by replay, processing, rebalance, telemetry, or validation?
what telemetry is retained in a long-running session?
what validation checks are always on, optional, or benchmark-only?
```

This is a control-plane maturity milestone, not a throughput-only milestone.

## Control-Plane Boundary

Milestone 006 introduced a rebalance-aware session that composes processing,
pressure sampling, planning, migration, policy recording, and validation.
Milestone 007 should keep that composition explicit.

The hardened session flow should remain:

```text
capture topology snapshot
process one RadarEventBatch against that snapshot
derive partitioned telemetry
derive or update pressure sample/window
advance hot-partition lifecycle state
evaluate rebalance policy at a safe logical boundary
publish at most bounded accepted migrations
record policy and lifecycle outcomes
emit bounded telemetry
return result and diagnostics
```

The control plane may grow new state, but it must stay outside the batch
payload lifetime:

```text
allowed to retain:
  numeric pressure samples
  topology versions
  partition ids
  shard ids
  logical evaluation sequence
  skipped-reason counters
  compact accepted-move summaries
  compact lifecycle evidence

not allowed to retain:
  leased RadarEventBatch payload spans
  mutable route buffers owned by processing
  caller-owned arrays without explicit copy/ownership rules
  string-formatted diagnostic messages as primary state
```

The session should expose enough hooks for benchmark and future production
schedulers without requiring timers or background workers in this milestone.

## Quarantine Lifecycle

Milestone 006 can classify hot partitions and record quarantine state, but
automatic lifecycle handling is deferred. Milestone 007 should define and
implement the lifecycle that prevents quarantine from becoming an eternal ban.

Quarantine should represent current evidence:

```text
the partition was too hot for safe direct movement
or a recent move produced insufficient actual relief
or the target risk was too high under the observed pressure window
```

It should not represent a permanent fact about the source range.

The lifecycle should be driven by logical rebalance evaluations rather than
wall-clock time in the reference path. Logical sequence keeps replay,
synthetic workloads, and tests deterministic.

Recommended conceptual states:

```text
unclassified:
  no special hot-partition evidence is retained

movable:
  partition can be considered for direct hot relief

intrinsic-hot:
  partition appears too large or too costly for safe direct movement now

quarantined:
  partition should not be retried immediately because recent evidence says the
  move is unsafe, ineffective, or churn-prone

retry-eligible:
  quarantine evidence has decayed enough that the planner may reconsider the
  partition under current pressure
```

These do not need to become separate public enum values if the existing model
can represent them cleanly. The architecture requirement is that the behavior
is explicit, deterministic, and visible in telemetry.

### Lifecycle Inputs

The lifecycle should advance from compact numeric evidence:

```text
current logical evaluation sequence
latest topology version
partition pressure score and pressure band
source shard pressure score and pressure band
target shard pressure projection if a move was attempted
observed relief after a completed move
number of sustained cooled samples
number of repeated unsafe-target or no-safe-target outcomes
time since quarantine in logical evaluations
pressure delta since quarantine started
```

The lifecycle should not depend on formatted logs, benchmark output, or
retained payload data.

### Lifecycle Transitions

Milestone 007 should support three ways out of quarantine:

```text
TTL expiry:
  after a configured number of logical evaluations, stale quarantine evidence
  expires and the partition becomes retry-eligible

sustained cooling:
  after a configured count of cooled or normal pressure samples, the partition
  clears quarantine because the hot condition is no longer active

material pressure change:
  if the partition or shard pressure changes enough relative to the quarantine
  baseline, the previous evidence is no longer reliable and retry is allowed
```

The controller should also support re-entry:

```text
retry-eligible partition is reconsidered
planner rejects it again for intrinsic-hot or unsafe-target reasons
or migration outcome shows insufficient actual relief
partition re-enters quarantine with fresh evidence and sequence numbers
```

This keeps retries bounded without hiding changed workload conditions.

### Lifecycle Ordering

Lifecycle advancement should happen before planning on each evaluation:

```text
sample pressure
advance lifecycle state from the latest pressure evidence
then let planners inspect the current effective classification
```

This ordering allows a cooled or expired partition to become retry-eligible in
the same evaluation where the new pressure sample proves the old quarantine is
stale.

### Lifecycle Telemetry

Lifecycle transitions should be visible as compact telemetry:

```text
partition id
previous classification
new classification
logical evaluation sequence
topology version
transition reason
pressure score at transition
quarantine age when cleared or retried
```

Transition detail should obey the same retention policy as rebalance decisions:
bounded recent detail plus aggregate counters.

## Telemetry And Retention

Milestone 006 made skipped rebalance decisions first-class telemetry. Milestone
007 should keep that property while preventing unbounded long-running detail.

The architecture should distinguish three telemetry layers:

```text
hot counters:
  aggregate counts updated cheaply on every evaluation

recent detail:
  bounded ring-buffer style history for the latest decisions and lifecycle
  transitions

diagnostic capture:
  opt-in full detail used by tests, benchmark snapshots, or manual debugging
```

The default production-shaped session should not retain every decision forever.

### Required Counters

The retained aggregate view should include:

```text
evaluations
accepted moves
direct hot relief moves
cold evacuation moves
rejected candidates
no-action decisions
skipped reasons by reason code
migration validation failures
state handoff validation failures
quarantine entries
quarantine clears
quarantine retries
quarantine rejections after retry
policy budget rejections
cooldown rejections
unsafe target rejections
no-hot-shard evaluations
```

Counters should use stable enum/code values rather than formatted strings.

### Recent Decision Detail

Recent retained detail should be bounded by option:

```text
max retained decisions
max retained lifecycle transitions
max retained accepted move summaries
max retained validation failures
```

When a limit is reached, the oldest retained detail should be dropped while
aggregate counters continue. Dropped-detail counts should be visible so
operators know that the summary is complete but the detail window is partial.

### Diagnostic Capture

Diagnostic capture can retain richer detail, but it must be explicit:

```text
disabled:
  counters only

recent:
  counters plus bounded recent decision/lifecycle windows

diagnostic:
  larger bounded windows and validation detail for benchmark or troubleshooting
```

Even diagnostic mode should remain bounded. A future file or external telemetry
exporter may persist longer history, but the in-process session should not
become an unbounded log store.

### Telemetry Shape

Telemetry should be compact and numeric:

```text
logical evaluation sequence
topology version
partition id
source shard id
target shard id
move kind
decision kind
skipped reason
pressure scores before/after
policy rejection code
validation error code
```

Formatting for CLI output should happen at the presentation boundary, not as
part of retained domain state.

## Allocation Model

Milestone 007 should profile and reduce allocation in the rebalance control
plane without changing the milestone 004 stream contract.

The first requirement is attribution. Benchmark output should make it clear
whether allocation comes from:

```text
archive replay and batch construction
processing route construction
pressure sampling
rebalance planning
decision and skipped-reason telemetry
migration and state handoff validation
result aggregation
CLI formatting
```

The milestone should avoid treating replay-dominated allocation as a rebalance
regression. The accepted comparison remains callback processing cost when
archive data is involved.

### Allocation Targets

Milestone 007 should not promise an arbitrary final allocation number before
profiling. It should set directional targets:

```text
no-action evaluations should avoid per-evaluation collection churn after warmup
bounded telemetry should avoid unbounded list growth
skipped-reason aggregation should update counters without allocating strings
decision detail should be retained only when the retention policy asks for it
validation should be optional or profiled separately when it allocates heavily
CLI formatting should not be included in processing callback timing
```

A useful closeout result would be:

```text
allocation is attributed by subsystem
the largest avoidable rebalance-control-plane sources are removed or justified
cache-wide callback allocation is compared before and after the hardening pass
remaining allocation debt is specific enough to plan
```

### Ownership And Reuse

Allocation reduction should preserve immutable public result semantics. The
preferred pattern is:

```text
reuse internal scratch buffers inside a session
copy only compact numeric detail into retained telemetry
clear and reuse builder-like structures between evaluations
avoid exposing pooled mutable collections to callers
keep public snapshots immutable or copy-on-publication
```

Pooling should not weaken the leased batch lifetime rule. No retained scratch
buffer may point into a leased `RadarEventBatch` payload.

## Policy Tuning Boundary

Milestone 007 should support tuning but avoid turning policy into aggressive
load equalization.

The controller should remain conservative:

```text
rebalance requires sustained pressure
target shard must have projected headroom
projected relief must be meaningful
cooldown and residency still protect against churn
move budgets still cap action per evaluation/window
cold evacuation remains a fallback from a hot shard
```

The tuning surface should make policy defaults explicit:

```text
pressure thresholds and hysteresis
minimum sample count
quarantine TTL
sustained cooled-sample count
material pressure-change threshold
retained decision-window sizes
validation profile
move budgets
cooldown and residency lengths
```

Milestone 007 should use broader benchmark evidence to decide whether defaults
need small adjustments. It should not make the controller more aggressive
merely because hardening work touched the policy code.

## Scheduler Boundary

Milestone 006 uses a logical evaluation sequence. Milestone 007 may define the
boundary for future production scheduling, but it should keep the reference
path deterministic.

The architecture should support:

```text
logical evaluation:
  deterministic sequence advanced by completed processing batches

timer-driven caller:
  future integration point that may decide when to request evaluation

safe publication:
  topology changes still publish only between completed batch boundaries
```

The session should not require a background thread to be correct. A future
runtime can call into the same control-plane boundary after completed telemetry
is available.

The important invariant remains:

```text
no batch observes mixed topology
no migration publishes while the processed batch is still active
no retained payload is needed to finish a scheduled rebalance
```

## Validation Profiles

Milestone 006 validation is intentionally strong. Milestone 007 should define
when validation is always required and when it is an explicit diagnostic cost.

Recommended validation profiles:

```text
off:
  no read-side validation beyond construction-time guardrails

essential:
  validate topology sequence, migration publication, and state handoff result

diagnostic:
  validate routes, telemetry parity, pressure sample parity, decisions,
  migration results, and state handoff diagnostics

benchmark:
  diagnostic validation plus deterministic checksums and result summaries used
  to compare runs
```

The default for tests and closeout benchmarks should remain diagnostic or
benchmark. The default for a production-shaped long-running session can be
essential once the hardening work is verified.

Validation output should follow telemetry retention rules. Repeated validation
failures should increment counters and retain bounded recent detail.

## Benchmark Boundary

Milestone 007 benchmarks should measure hardening effects without confusing
replay construction with processing and control-plane work.

The benchmark matrix should preserve the milestone 006 accepted baseline:

```text
synthetic rebalance workloads:
  deterministic behavior contour and same-run static comparison

single-file real-data benchmark:
  leased RadarEventBatch callback behavior over actual Archive Two data

comparable parallel real-data benchmark:
  replay and callback contour with archive parallelism comparable to stream
  benchmark settings

cache-wide real-data benchmark:
  long enough to exercise repeated evaluations, skipped reasons, and retention
```

Milestone 007 should add longer-run contours where practical:

```text
repeat cache-wide runs over the same local corpus
multi-radar cache selections when local data is available
long synthetic runs that stress telemetry retention limits
quarantine lifecycle workloads that cover TTL, cooling, pressure-change retry,
and re-entry after failed retry
```

Benchmark output should report:

```text
processing callback elapsed time
archive end-to-end elapsed time when archive input is used
control-plane evaluations
accepted moves
skipped-reason counters
quarantine lifecycle counters
retained detail counts and dropped-detail counts
validation profile
allocation by measured contour
deterministic checksum or validation status
```

The closeout should compare milestone 007 against the accepted milestone 006
baseline and explain differences by subsystem.

## Real-Data Evidence

Milestone 006 used one local KTLX corpus capture for cache-wide validation.
Milestone 007 should treat that as a baseline, not a final production proof.

Useful additional evidence:

```text
same corpus repeated runs:
  identify noise and establish confidence intervals for allocation and timing

different radar/day selections:
  expose different pressure distributions and missing-data shapes

multi-radar selections:
  exercise source-universe and pressure behavior under broader source identity
  sets

longer synthetic control-plane runs:
  prove telemetry retention bounds and quarantine lifecycle behavior without
  requiring huge local corpora
```

The milestone can remain valuable even if local multi-radar data is limited.
The architecture should make missing corpus coverage explicit rather than
pretending one KTLX run represents production.

## CLI And Diagnostics

The CLI should continue to be a smoke and benchmark surface, not the owner of
domain policy.

Milestone 007 CLI output should make hardening state visible:

```text
retention mode
validation profile
quarantine TTL and cooling thresholds
retained decision counts
dropped decision detail counts
skipped-reason counters
quarantine transition counters
allocation ratio for the measured contour
callback timing versus archive end-to-end timing
```

Detailed decision samples should remain capped. The CLI may print recent
decisions and lifecycle transitions, but it should not dump an unbounded
history by default.

Presentation formatting should stay outside the domain state. Domain telemetry
should expose codes and numeric values; CLI formatting can convert them into
human-readable rows.

## Architectural Boundaries

Milestone 007 should keep these boundaries explicit:

```text
stream boundary:
  RadarEventBatch remains unchanged

processing boundary:
  process one batch against one topology snapshot

rebalance boundary:
  evaluate and publish ownership changes only between batch boundaries

quarantine boundary:
  retain compact lifecycle evidence, not payloads or route buffers

telemetry boundary:
  retain bounded counters and recent detail, not unbounded decision logs

allocation boundary:
  attribute and reduce control-plane allocation separately from replay

validation boundary:
  make validation profile explicit and keep expensive diagnostics outside the
  mandatory hot path where possible

scheduler boundary:
  support future timer-driven callers without requiring background workers in
  the reference path
```

These boundaries are the reason 007 should precede async worker transport.
Once the control plane is bounded and observable, a later milestone can add
worker scheduling with fewer hidden policy and diagnostic questions.

## Non-Goals For This Document

Milestone 007 does not define:

```text
retained async worker queues
physical worker-local state transfer
multi-core shard execution runtime
live ingestion
durable broker integration
partition splitting
source-level migration
source-universe repartitioning
complex radar algorithms
visualization
new long-term storage format
```

It also should not treat broader policy aggression as a goal. If repeated
real-data runs show that thresholds or budgets need adjustment, those
adjustments should be small, measured, and explained through benchmark output.

## Baseline Architectural Position

The baseline position for milestone 007 is:

```text
RadarPulse already has a correct cautious rebalance controller.
The next architectural risk is not whether a partition can move.
The next risk is whether the control plane stays bounded, explainable, and
self-correcting during longer production-shaped runs.
```

Milestone 007 is successful when the production-hardened synchronous rebalance
path can be reasoned about as:

```text
deterministic:
  logical evaluation sequence and bounded policy state drive decisions

temporary:
  quarantine decays, clears, or retries when evidence changes

bounded:
  telemetry detail and allocation do not grow without limit

observable:
  accepted moves, skipped reasons, lifecycle transitions, and validation
  outcomes are visible through compact counters and recent detail

measured:
  synthetic and real-data benchmarks explain replay, processing callback,
  control-plane, validation, and allocation contours separately

conservative:
  rebalance remains pressure relief, not mechanical equalization
```

The next milestone after 007 can then make an informed choice: either introduce
the first retained async worker transport over this hardened control plane, or
address partition splitting for intrinsically hot partitions if real-data
evidence says movement alone is not enough.

# Milestone 013: Post-Rollout Hardening And Broader Validation Architecture

Status: complete.

RadarPulse milestone 013 starts from the closed milestone 012 scoped default
rollout. Milestone 012 changed omitted-provider behavior for the
`processing benchmark rebalance-archive` CLI surface to the measured
`queued-owned + pooled-copy + producer-consumer` contour, while preserving
`blocking-borrowed` as explicit fallback and same-run oracle.

This document is intentionally not an implementation plan. It records the
post-rollout hardening concept, workload validation model, compatibility
posture, allocation attribution goal, operator-surface cleanup, gate posture,
and expected closeout decision before any task breakdown is written.

Milestone 012 proved that the scoped default rollout is acceptable under the
measured local contours. It deliberately did not migrate direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` or `MeasureCache()`
defaults, synthetic benchmark defaults, non-benchmark archive publishing APIs,
or live ingestion/runtime defaults.

The core decision is:

```text
012 made queued-owned the scoped CLI default.
013 proves whether that rollout is stable enough to keep broadening from.
```

Milestone 013 should not rush into live ingestion, durable transport, or
ordered concurrent rebalance. It should first make the new default hard to
regress, validate it across broader workload shapes, and turn the remaining
allocation and compatibility risks into measured, operator-visible facts.

## Milestone Goal

Milestone 013 should turn the scoped default rollout from a successful
decision into a hardened baseline for later default expansion.

The output of the milestone is the architectural definition of:

```text
post-rollout workload validation matrix
same-run blocking-borrowed oracle requirements after default rollout
default-contour drift guardrails
operator-visible provenance and fallback compatibility checks
allocation attribution for the remaining queued-owned overhead
retained pressure and cleanup regression thresholds
Release gate evidence across broader local data shapes
decision trace that names whether the rollout remains stable enough to use as
  the base for direct API migration or runtime integration
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Only owned or retained-owned input may enter the provider queue.
Retained resources release only after final use.
Provider enqueue success remains distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Controlled consumer delay remains mechanics-only proof.
Builder-transfer remains unsupported.
Blocking-borrowed remains explicitly selectable.
Same-run blocking-borrowed remains the benchmark oracle.
Queued-owned failures fail closed.
No automatic borrowed fallback follows queued-owned failure.
```

The key milestone boundary is:

```text
safe in 013:
  harden the scoped default contour that 012 accepted
  validate the default across broader local archive shapes
  add regression tests that prevent default drift and fallback ambiguity
  attribute residual allocation overhead without changing the default contour
  clean up operator documentation and output around default/fallback behavior
  produce a decision trace for whether direct API migration is a reasonable
    next milestone

not safe in 013 unless explicitly reprioritized:
  changing direct MeasureFile()/MeasureCache() defaults
  changing live ingestion/runtime provider defaults
  adding durable broker integration or cross-process transport
  adding ordered concurrent rebalance commit semantics
  implementing builder-transfer retained payload execution
  silently falling back from queued-owned failure to borrowed success
  tuning the default contour to make a gate pass without recording a new
    rollout decision
```

## Expected Outcome

At the end of milestone 013, RadarPulse should have a clear answer to this
question:

```text
Is the milestone 012 scoped queued-owned default stable enough, across broader
workload and regression evidence, to serve as the baseline for the next
expansion decision?
```

The acceptable outcomes are:

```text
stable baseline:
  the scoped CLI default remains queued-owned, broader workload validation
  passes, residual allocation overhead is attributed or bounded, and the next
  milestone can consider direct API default migration or runtime integration

stabilize first:
  the scoped CLI default remains in place, but the decision trace names a
  concrete blocker such as allocation growth, retained pressure regression,
  variance, operator ambiguity, or insufficient workload coverage

rollback candidate:
  a named correctness, cleanup, release, or pressure regression is severe
  enough that the scoped default should be revisited before further expansion
```

The expected result, if the rollout remains stable, is:

```text
omitted-provider rebalance-archive CLI runs still expand to the milestone 012
  queued-owned default contour
explicit --provider blocking-borrowed still selects the fallback path
same-run borrowed rows remain available for every validation gate
broader workload rows preserve correctness, topology, rebalance, and checksum
  parity
retained cleanup returns current pending, active, and combined pressure to zero
release failures remain 0
combined retained payload high-water remains within the configured budget
allocation overhead stays within the accepted rollout threshold and is broken
  down by meaningful source where practical
operator output continues to distinguish rollout default, explicit fallback,
  explicit queued-owned diagnostic rows, and controlled mechanics proof
```

The core idea is:

```text
012 changed the scoped default deliberately.
013 makes that default boring, bounded, and reproducible before broadening it.
```

## Starting Position

Milestone 012 closed this scoped default posture:

```text
surface:
  processing benchmark rebalance-archive CLI omitted-provider path

provider mode:
  queued-owned

retention strategy:
  pooled-copy

provider overlap:
  producer-consumer

execution:
  async

workers:
  4

provider queue capacity:
  8

retained-byte budget:
  536_870_912

queue telemetry:
  summary

overlap telemetry:
  summary

overlap consumer delay:
  0

provider source:
  rollout-default
```

The explicit fallback remains:

```text
--provider blocking-borrowed
```

Milestone 012 gate facts carried forward:

```text
primary KTLX default elapsed ratio:
  0.855x borrowed

primary KTLX default allocation ratio:
  1.072x borrowed

primary KTLX default run spread:
  2.39% of candidate average

mixed-cache default elapsed ratio:
  0.777x borrowed

mixed-cache default allocation ratio:
  1.064x borrowed

max observed combined retained payload high-water:
  54_413_280 bytes of 536_870_912 byte budget

release failures:
  0

retained pressure at completion:
  current pending, active, and combined counts/bytes returned to 0
```

Milestone 012 intentionally left these decisions for later milestones:

```text
direct MeasureFile()/MeasureCache() default migration
synthetic processing benchmark default changes
non-benchmark archive publishing API default changes
live ingestion/runtime provider defaults
durable queue or broker integration
cross-process provider or worker transport
ordered concurrent rebalance commit barrier
builder-transfer retained payload execution
physical worker-local state transfer
automatic fallback from queued-owned to blocking-borrowed
```

Those deferrals remain out of scope for milestone 013 unless this architecture
is explicitly revised before implementation starts.

## Architectural Principles

Milestone 013 should follow these principles:

```text
post-rollout hardening precedes broader runtime expansion
the milestone 012 default contour is the subject, not a tuning starting point
blocking-borrowed remains the oracle and explicit fallback
operator output must make default provenance auditable
correctness, cleanup, and release health remain non-negotiable
allocation overhead is acceptable only when bounded and explainable
retained pressure must remain within budget and return to zero
controlled consumer delay stays separate from natural evidence
broader workload evidence should explain variance instead of hiding it
direct API and live ingestion defaults require separate future decisions
```

The milestone should separate these concerns:

```text
rollout stability:
  whether the milestone 012 scoped default still passes broader evidence

default drift:
  whether omitted provider options still resolve to the accepted contour

fallback compatibility:
  whether explicit blocking-borrowed remains visibly separate and selectable

allocation attribution:
  which parts of queued-owned account for residual allocation overhead

operator surface:
  whether CLI output and documentation make the default and fallback obvious

future expansion readiness:
  whether the next milestone can safely target direct API migration or should
  first address a named blocker
```

## Core Concepts

### Post-Rollout Baseline

The post-rollout baseline is the milestone 012 scoped default contour treated
as a fixed subject of validation.

The baseline is:

```text
queued-owned + pooled-copy + producer-consumer
async execution
workers 4
provider queue capacity 8
retained-byte budget 536_870_912
summary queue telemetry
summary overlap telemetry
zero overlap consumer delay
rollout-default provenance
```

Milestone 013 should not change that baseline as part of ordinary hardening.
If evidence shows the contour itself needs to change, the decision trace must
name the new contour and explain why the milestone stopped being pure
post-rollout validation.

### Default Drift Guard

Default drift is any accidental change where omitted provider-related CLI
options no longer expand to the accepted milestone 012 contour.

Drift can happen through:

```text
changed parser defaults
changed constructor defaults
renamed or reinterpreted option provenance
worker or queue defaults inherited from unrelated async execution changes
retention strategy changes hidden behind provider defaults
telemetry defaults that remove rollout evidence
help text or output that no longer tells operators what happened
```

Milestone 013 should make drift visible through tests and output checks. The
guard is not only the provider enum; it is the entire effective contour.

### Same-Run Borrowed Oracle

After rollout, `blocking-borrowed` is still required as the comparison oracle.
It has two separate roles:

```text
operator fallback:
  explicit old behavior selected through --provider blocking-borrowed

validation oracle:
  same-run reference used to compare correctness, topology, rebalance,
  allocation, elapsed time, and output checksums
```

Milestone 013 should preserve both roles. A future benchmark helper may make
same-run paired capture easier, but it must not blur fallback with automatic
failure recovery.

### Broader Workload Validation

Broader validation means the default is measured across more than the milestone
012 primary and mixed-cache rows when local data allows it.

The workload matrix should vary:

```text
radar/date shape
file count
skipped/published file mix
payload value volume
rebalance activity shape
cache-wide mixed input
single-shape repeated timing rows
larger local corpus rows when available
```

The goal is not to claim universal production readiness. The goal is to find
whether the accepted default is stable across the data shapes the repository
can actually validate now.

### Allocation Attribution

Milestone 012 accepted residual allocation overhead below the rollout
threshold. Milestone 013 should make that overhead easier to reason about.

Attribution should distinguish, where practical:

```text
retained payload pooled-copy allocation
retained-resource accounting and pressure telemetry
provider queue and overlap telemetry
async worker dispatch and aggregation
benchmark result aggregation
archive replay/build allocation that is common to both contours
```

Attribution does not require eliminating the overhead in this milestone. It
does require avoiding a vague "queued-owned allocates more" conclusion when
future default expansion depends on understanding the cost.

### Compatibility Cleanup

The milestone 012 rollout introduced a scoped default difference between CLI
option parsing and direct infrastructure method defaults.

That split is intentional:

```text
CLI omitted-provider rebalance-archive default:
  queued-owned rollout contour

direct MeasureFile()/MeasureCache() defaults:
  blocking-borrowed compatibility posture
```

Milestone 013 should make this split explicit and testable. Compatibility
cleanup may include clearer help text, documentation, or output labels, but it
should not silently migrate direct method defaults.

### Future Expansion Readiness

Milestone 013 should prepare, not perform, the next expansion decision.

The likely next expansion choices are:

```text
direct API default migration
broader benchmark default rollout
live or durable ingestion architecture
ordered concurrent rebalance execution
```

The closeout should recommend the next one based on evidence, not preference.
If the scoped rollout shows instability, the next milestone should address the
named instability first.

## Validation Gate

Milestone 013 should use a post-rollout gate. This gate is not a new default
rollout decision; it is a stability decision for the already-scoped default.

Required dimensions:

```text
default expansion:
  omitted provider-related flags resolve to the milestone 012 contour

fallback separation:
  explicit --provider blocking-borrowed remains borrowed and visibly fallback

correctness parity:
  published file count, payload values, raw checksum, validation checksum,
  topology versions, accepted moves, skipped decisions, failed migrations, and
  validation status match same-run borrowed rows

cleanup:
  current pending, active, and combined retained counts/bytes return to zero
  at completion

release health:
  retained payload and provider overlap failed releases remain 0

retained pressure:
  combined retained payload high-water remains within the configured retained
  byte budget

allocation:
  default-to-borrowed allocation ratio remains within the accepted threshold
  and attribution explains the dominant overhead sources where practical

performance:
  elapsed ratio remains within the rollout threshold or any miss is recorded
  as a blocker before broader expansion

variance:
  repeated natural rows show candidate spread clearly enough to interpret the
  timing result

operator provenance:
  output distinguishes rollout-default, explicit fallback, explicit queued
  owned diagnostic contours, and controlled mechanics proof
```

Milestone 013 should start from the milestone 012 thresholds unless the
architecture or plan records a reason to change them before new measurements
are interpreted:

```text
release failures:
  must equal 0

current retained pressure at completion:
  pending, active, and combined counts/bytes must return to 0

combined retained payload high-water:
  must stay within 536_870_912 bytes unless the configured budget changes in a
  documented contour

allocation ratio:
  default allocated bytes should remain <= 1.10x same-run borrowed

elapsed ratio:
  default elapsed time should remain <= 1.00x same-run borrowed on primary
  repeated natural rows

candidate run spread:
  repeated natural default spread should remain <= 7.50% of candidate average
```

Threshold changes are allowed only if they are recorded before gate
interpretation. Do not tune thresholds after seeing a favorable or unfavorable
row.

## Benchmark Scope

The benchmark scope should remain natural Release evidence.

Required benchmark posture:

```text
Release build before capture
same-run blocking-borrowed reference rows
omitted-provider rollout-default rows
controlled consumer delay disabled
retained pressure telemetry enabled
queue and overlap telemetry visible
default/fallback provenance printed
deterministic output comparison captured
repeated rows for at least one primary contour
broader single-row or repeated rows for additional local cache shapes
```

Candidate contours:

```text
primary KTLX 2026-05-04 contour retained from milestone 012
mixed-cache contour over all local NEXRAD cache shapes
additional radar/date shapes available in the local cache
larger max-files rows where local data and runtime budget allow
small smoke rows for output and fallback compatibility
```

The gate should report:

```text
effective configuration and option provenance
borrowed elapsed and allocated bytes
default elapsed and allocated bytes
default-to-borrowed elapsed ratio
default-to-borrowed allocation ratio
published/skipped/examined file counts
payload values and raw checksum
validation checksum and validation status
topology versions and rebalance counters
skipped reason counters
queue depth and overlap indicators
retained pending, active, and combined high-water values
current retained pressure at completion
release attempts and failed releases
dominant allocation attribution categories where available
```

Controlled proof rows may remain useful for mechanics regressions, but they
must stay separate from natural rollout stability evidence.

## Operator Surface

Milestone 013 should keep the operator surface reproducible.

Scoped default output should continue to expose:

```text
provider mode and source
provider overlap mode and source
retention strategy and source
execution mode and source
worker count and source
provider queue capacity and source
retained-byte budget and source
queue telemetry and source
overlap telemetry and source
provider default rollout contour
provider rollout default expansion
provider fallback contour
default-candidate or rollout evidence contour
provider overlap evidence scope
retained-resource pressure telemetry
release telemetry
validation status and deterministic result summary
```

Operator-facing compatibility should make these statements true:

```text
omitting provider flags selects the scoped queued-owned default
explicit --provider blocking-borrowed selects the fallback path
explicit queued-owned runs are visible as explicit, not rollout-default
controlled consumer-delay rows are labeled mechanics proof
direct API defaults remain borrowed until a separate migration decision
```

If the CLI help text is updated, it should avoid vague "optional provider"
language that hides the new default. It should name the default contour and
fallback path clearly enough that benchmark output can be reproduced later.

## Failure Policy

Milestone 013 preserves milestone 012 failure behavior.

Required policy:

```text
retention failure:
  stop intake, release accepted resources, and report candidate failure

queue acceptance failure:
  stop or reject deterministically without claiming successful default output

processing failure:
  fault the queued session, release accepted resources, and prevent later
  success claims

validation failure:
  report default-run validation failure, not hidden fallback success

release failure:
  increment failed-release telemetry and fail rollout stability evidence

cancellation:
  stop intake deterministically and release accepted retained resources
```

Any future automatic fallback design must be a separate milestone with visible
operator semantics. It is not part of post-rollout hardening.

## Documentation And Handoff

Expected milestone documents:

```text
docs/milestones/013-post-rollout-hardening-broader-validation.md
docs/milestones/013-post-rollout-hardening-broader-validation-plan.md
docs/milestones/013-post-rollout-hardening-broader-validation-performance-gate.md
docs/milestones/013-post-rollout-hardening-broader-validation-decision-trace.md
docs/milestones/013-post-rollout-hardening-broader-validation-closeout.md
docs/handoff.md
```

The handoff should state one of:

```text
the milestone 012 scoped queued-owned default remains stable after broader
  validation, and the recommended next milestone is direct API migration or
  another named expansion

or

the scoped default remains in place but broader expansion is blocked by a
  named issue that should be addressed next

or

the scoped default should be revisited because broader validation found a
  named correctness, cleanup, release, pressure, or compatibility regression
```

## In Scope

Milestone 013 includes:

```text
post-rollout validation of the milestone 012 scoped default
default contour drift guardrails
explicit blocking-borrowed fallback compatibility checks
same-run borrowed oracle preservation
broader natural Release workload matrix
retained cleanup, pressure, release, allocation, timing, and variance gates
allocation attribution for residual queued-owned overhead
operator output/help/documentation cleanup around default and fallback posture
decision trace that recommends the next milestone scope
handoff update for the post-rollout stability outcome
```

## Out Of Scope

Milestone 013 does not implement:

```text
direct MeasureFile()/MeasureCache() default migration
synthetic benchmark default migration
non-benchmark archive publishing API default migration
live ingestion/runtime provider defaults
durable queue or broker integration
cross-process provider or worker transport
ordered concurrent rebalance commit barrier
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
automatic silent fallback from queued-owned failure to borrowed success
```

## Completion Criteria

Milestone 013 is complete when:

```text
the milestone 012 scoped default contour is pinned against drift
broader workload validation is captured and interpreted
same-run blocking-borrowed oracle rows remain available and documented
explicit fallback remains visibly separate from rollout-default behavior
retained cleanup returns current pressure to zero in natural default rows
release failures remain visible and fail the gate
retained pressure stays within the configured budget or a blocker is recorded
allocation overhead is bounded and attributed enough to guide future work
timing and variance are interpreted against recorded thresholds
operator output and documentation make default/fallback provenance clear
the decision trace records whether the scoped default is stable enough for the
  next expansion decision
the closeout records verification, gate results, residual risks, and carry
  forward items
handoff states the current default posture and recommended next milestone
  unambiguously
```

The milestone should close with a stability decision, not with another vague
"continue monitoring" statement.

## Likely Next Milestone Input

If milestone 013 closes with a stable baseline, the next milestone can choose
one of:

```text
direct API default migration for MeasureFile()/MeasureCache()
broader benchmark surface default rollout
live or durable ingestion architecture over the retained-owned boundary
ordered concurrent rebalance architecture
targeted allocation reduction if attribution identifies a high-value source
```

If milestone 013 finds a blocker, the next milestone should target the named
blocker:

```text
default contour drift
operator-surface ambiguity
allocation threshold miss
retained pressure threshold miss
release or cleanup failure
validation parity failure
performance or variance regression
insufficient workload coverage for the next expansion decision
```

Still deferred unless explicitly reprioritized:

```text
durable queues
live ingestion
cross-process workers
concurrent rebalance processing
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```

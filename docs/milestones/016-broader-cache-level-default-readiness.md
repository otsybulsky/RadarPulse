# Milestone 016: Broader Cache-Level Default Readiness Architecture

Status: draft.

RadarPulse milestone 016 starts from the closed milestone 015 queued-owned
allocation readiness result. Milestone 015 accepted cache-level allocation
readiness for the queued-owned direct/default archive rebalance contour used by
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
when provider/execution/queue/retention controls are omitted.

Milestone 015 closed with this answer:

```text
yes, the queued-owned direct/default allocation profile is ready to support
the next broader cache-level benchmark/default-readiness decision
```

The milestone 015 gate also preserved a visible scope limit:

```text
single-file cold allocation remains expected retained-ownership cost for
queued-owned + pooled-copy; it does not block cache-level readiness, but it is
not a file-level default readiness claim
```

This document is intentionally not an implementation plan. It records the
milestone 016 concept, cache-level default-readiness model, broader workload
coverage posture, fallback/oracle requirements, validation gate boundaries,
documentation requirements, and expected closeout decision before task
breakdown is written.

The core decision is:

```text
015 reduced and bounded the queued-owned direct/default allocation warning on
    the measured local cache-level contours.
016 decides whether the queued-owned direct/default cache-level benchmark
    posture is ready across broader cache evidence, or whether readiness must
    remain limited to the milestone 015 measured contours.
```

Milestone 016 should not broaden into live ingestion, durable queues,
cross-process workers, ordered concurrent rebalance, builder-transfer,
source-level migration, partition splitting, or product-facing radar analysis.
It should be an evidence-breadth milestone for the already-accepted
queued-owned direct/default benchmark contour.

## Milestone Goal

Milestone 016 should turn the milestone 015 cache-level allocation readiness
answer into a broader cache-level default-readiness decision.

The output of the milestone is the architectural definition of:

```text
broader cache corpus selection and coverage requirements
same-run BlockingBorrowed oracle requirements for every readiness row
cache-level queued-owned default readiness thresholds
per-cache-shape pass/warning/fail interpretation
Release gate evidence for direct MeasureCache() omitted defaults
CLI omitted-provider cache benchmark alignment checks
single-file cold warning scope language
coverage limits that still constrain future default expansion
decision trace that records whether broader cache-level readiness is accepted
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
BlockingBorrowed remains explicitly selectable.
Same-run BlockingBorrowed remains the benchmark oracle.
Queued-owned failures fail closed.
No automatic borrowed fallback follows queued-owned failure.
Direct MeasureFile()/MeasureCache() omitted defaults remain queued-owned.
CLI omitted-provider rebalance-archive remains aligned with direct defaults.
```

The key milestone boundary is:

```text
safe in 016:
  inventory the local or explicitly approved NEXRAD cache corpus
  select broader cache-level shapes before gate interpretation
  keep the milestone 015 cache shapes as the minimum comparison baseline
  add small benchmark harness or reporting improvements if needed to make
    cache-level gate evidence repeatable and reviewable
  run direct MeasureCache() omitted-default rows against same-run explicit
    BlockingBorrowed oracle rows
  keep CLI omitted-provider cache rows aligned with direct defaults
  classify each cache shape individually instead of averaging away warnings
  carry the single-file cold warning as file-level scope, not cache-level debt
  record readiness, warning, blocker, or coverage insufficiency in the
    decision trace

not safe in 016 unless explicitly reprioritized:
  changing the accepted queued-owned rollout contour
  changing live ingestion/runtime provider defaults
  changing synthetic processing benchmark defaults
  changing non-benchmark archive publishing API defaults
  optimizing file-level cold latency/allocation as the main milestone target
  adding durable broker integration or cross-process transport
  adding ordered concurrent rebalance commit semantics
  implementing builder-transfer retained payload execution
  silently falling back from queued-owned failure to borrowed success
  raising thresholds after seeing gate results
  hiding a shape-specific warning behind mixed-cache aggregate success
```

## Expected Outcome

At the end of milestone 016, RadarPulse should have a clear answer to this
question:

```text
Is the queued-owned direct/default contour ready as the broader cache-level
benchmark/default posture for available cache workloads?
```

The acceptable outcomes are:

```text
ready:
  broader cache-level rows pass correctness, release, cleanup, pressure,
  elapsed, spread, allocation, and attribution thresholds; same-run
  BlockingBorrowed rows remain available; the next milestone can consider a
  broader runtime/default architecture decision

ready with scoped warnings:
  one or more warnings remain, but each warning is explicitly assigned to a
  named surface or cache shape, the warning is deliberately accepted for the
  next named decision, and the warning remains visible in handoff

not ready:
  a cache-level row fails or stays too close to a threshold without enough
  attribution; the decision trace names the blocking shape and the next
  optimization or coverage target

coverage insufficient:
  available local or approved cache shapes are too narrow to support the
  broader decision; the decision trace names the missing workload evidence

defer:
  correctness, release health, cleanup, retained pressure, fail-closed
  behavior, timing variance, or benchmark repeatability regresses and broader
  readiness cannot be decided safely
```

The milestone should not close with a vague monitoring posture. It should
accept broader cache-level readiness, accept it with named warnings, reject it
with a named blocker, or state exactly what workload coverage is missing.

## Starting Position

Milestone 015 closed this direct/default contour:

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
```

Explicit fallback and oracle:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed

same-run BlockingBorrowed rows remain required for benchmark gates,
performance regressions, allocation follow-up, correctness parity, and
rollback diagnosis
```

Milestone 015 gate facts carried forward:

```text
primary KTLX 2026-05-04:
  elapsed ratio: 0.889x borrowed
  allocation ratio: 1.042x borrowed
  timing spread: 1.10%

KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.0392x borrowed average
  row 1 allocation ratio: 1.0404x borrowed
  row 2 allocation ratio: 1.0381x borrowed

KINX 2026-05-04:
  elapsed ratio: 0.899x borrowed
  allocation ratio: 1.042x borrowed

mixed cache:
  elapsed ratio: 0.871x borrowed
  allocation ratio: 1.021x borrowed

single-file cold smoke:
  elapsed ratio: 1.072x borrowed
  allocation ratio: 1.512x borrowed
  interpretation: file-level warning, not cache-level blocker
```

Allocation movement versus milestone 014:

```text
primary KTLX 2026-05-04: 1.071x -> 1.042x borrowed
KTLX 2026-05-05 average: 1.0997x -> 1.0392x borrowed
KINX 2026-05-04: 1.069x -> 1.042x borrowed
mixed cache: 1.066x -> 1.021x borrowed
```

Known local NEXRAD cache at milestone start:

```text
data/nexrad/level2/2026/05/04/KTLX: 244 files
data/nexrad/level2/2026/05/04/KINX: 462 files
data/nexrad/level2/2026/05/05/KTLX: 848 files
```

Residual limits carried from milestone 015:

```text
single-file cold allocation:
  expected retained-ownership cost; file-level target if that surface is
  chosen later

local gate only:
  the milestone 015 Release gate used locally available NEXRAD cache shapes
  captured on 2026-05-21

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than natural cache rows

mixed-cache worker failure counters:
  mixed-cache rows reported matching worker failed batch/item counters in
  borrowed and direct default rows while validation still succeeded
```

These limits are not automatic blockers for 016, but each must remain visible
when broader readiness is interpreted.

## Architectural Principles

Milestone 016 should follow these principles:

```text
broader readiness is evidence breadth, not a new default migration
the accepted queued-owned rollout contour is preserved unless a new decision
  trace explicitly changes it
BlockingBorrowed remains the same-run oracle for every cache-level row
cache-shape warnings are interpreted individually before aggregate summaries
correctness, cleanup, release health, and fail-closed behavior remain
  non-negotiable
allocation and elapsed thresholds are recorded before gate capture
the single-file cold warning remains a file-level concern unless this
  milestone is explicitly reprioritized
local corpus limitations are documented rather than hidden
new cache downloads, if needed, require a manifest and explicit gate scope
live/runtime default expansion remains a separate future milestone
```

The milestone should separate these concerns:

```text
coverage:
  whether the available cache shapes are broad enough to support a broader
  cache-level readiness decision

readiness:
  whether queued-owned direct/default cache rows pass the required thresholds
  against same-run BlockingBorrowed oracle rows

scope warnings:
  whether a cost is real but belongs to another named surface, such as
  file-level cold default latency/allocation

default posture:
  whether direct defaults and CLI omitted-provider defaults remain aligned
  with the accepted queued-owned rollout contour

next decision:
  whether the next milestone should broaden toward runtime/default
  architecture, target a named cache-level blocker, or acquire more workload
  evidence
```

## Broader Cache Coverage Model

Milestone 016 should define cache coverage before interpreting readiness.
The minimum useful gate cannot be narrower than milestone 015.

Required coverage posture:

```text
corpus inventory:
  record every local or explicitly approved cache root used by the gate, with
  radar, date, file count, and selection limit

primary retained contour:
  repeat the milestone 015 KTLX 2026-05-04 cache-level contour so movement
  and drift are visible

named risk contour:
  repeat KTLX 2026-05-05 as the formerly borderline allocation contour

cross-radar contour:
  include KINX 2026-05-04 or a documented replacement radar/date if the local
  corpus changes

mixed-cache contour:
  include a multi-radar or multi-date cache shape so aggregate traversal and
  cache discovery behavior remain covered

size contour:
  include at least one cache-level size variation where the plan can do so
  without turning the milestone into a single-file readiness claim

coverage statement:
  state whether the selected corpus is broad enough for the milestone
  decision, and name missing workload classes if it is not
```

Candidate cache shapes:

```text
KTLX 2026-05-04 bounded cache, retained from milestone 015
KTLX 2026-05-05 bounded cache, retained as the named risk contour
KINX 2026-05-04 bounded cache, retained as the cross-radar contour
mixed local cache across available NEXRAD roots
larger local cache slices where runtime cost is acceptable
smaller cache-level slices where the row still represents cache behavior, not
  single-file cold behavior
```

The gate should avoid these coverage mistakes:

```text
using only the easiest milestone 015 row
using a mixed-cache pass to hide a failing radar/date row
using a single-file smoke row as proof of cache-level default readiness
adding new data without recording provenance, file count, and selection rules
changing max-files limits after seeing results without recording why
```

## Default Readiness Model

Default readiness means the queued-owned direct/default cache contour has a
cost, correctness, and lifecycle profile that is broad enough to support the
next default-surface decision. It does not require zero overhead relative to
borrowed.

The target readiness posture is:

```text
correctness parity:
  required against same-run BlockingBorrowed rows

release health:
  retained payload failed releases and provider overlap failed releases must
  remain 0

retained cleanup:
  current pending, active, and combined counts/bytes must return to 0 at
  completion

retained pressure:
  combined retained payload high-water must stay within the configured
  536870912 byte budget

allocation:
  queued-owned direct/default allocated bytes should remain <= 1.10x same-run
  borrowed rows for cache-level readiness rows, or the decision trace must
  classify the row as a warning or blocker

elapsed timing:
  queued-owned direct/default elapsed time should remain <= 1.00x same-run
  borrowed on ready cache-level rows, or the decision trace must classify the
  row as a warning or blocker

variance:
  repeated natural rows must be stable enough to interpret shape-level
  readiness

attribution:
  remaining overhead should have enough telemetry to explain whether it is
  retained payload, event-array, byte-array, callback, queue, overlap, replay,
  or validation cost

coverage:
  selected cache shapes must be broad enough that the readiness answer is not
  merely a restatement of milestone 015
```

Milestone 016 may choose stricter targets in its implementation plan, for
example:

```text
preferred allocation target:
  every cache-level queued-owned direct/default row remains materially below
  the 1.10x borrowed allocation threshold

preferred elapsed target:
  every cache-level queued-owned direct/default row remains faster than or
  equal to same-run borrowed

preferred coverage target:
  at least one repeated primary row, one repeated named risk row, one
  cross-radar row, one mixed-cache row, and one size-variation row

readiness target:
  if a row remains borderline, the decision trace names why the remaining
  cost is acceptable for the next named surface
```

Thresholds should not be raised after measurements are captured. If the
milestone needs a different threshold, the implementation plan should record
why before the gate is interpreted.

## Fallback And Oracle Model

BlockingBorrowed keeps the same two roles from milestone 015:

```text
fallback:
  direct callers and CLI users can still request the borrowed provider
  explicitly

oracle:
  same-run borrowed rows remain the comparison baseline for correctness,
  allocation, elapsed time, pressure, and rollback diagnosis
```

Required comparison shape:

```text
direct MeasureCache() queued-owned omitted-default row
same-run direct MeasureCache() explicit BlockingBorrowed oracle row
CLI omitted-provider cache spot-check where direct/CLI alignment is at risk
explicit queued-owned rollout spot-check where contour drift is possible
```

Borrowed rows must stay explicit enough that a gate cannot accidentally compare
queued-owned against queued-owned. Any automatic fallback from queued-owned
failure to borrowed success remains disallowed.

## Validation Gate

Milestone 016 should use a broader cache-level readiness gate. This gate is
not a runtime ingestion claim.

Required dimensions:

```text
corpus inventory:
  list the cache roots, radar/date pairs, file counts, selection limits, and
  any skipped or unavailable workload classes

fallback separation:
  explicit BlockingBorrowed remains borrowed and visibly separate in result
  contracts and CLI output

default contour:
  omitted direct controls still resolve to the accepted queued-owned rollout
  contour

CLI/direct alignment:
  scoped CLI omitted-provider cache benchmark stays aligned with the direct
  API contour

correctness parity:
  published file count, payload values, raw checksum, validation checksum,
  topology versions, accepted moves, skipped decisions, failed migrations, and
  skipped reason counters match same-run borrowed rows

cleanup:
  current pending, active, and combined retained counts/bytes return to zero
  at completion

release health:
  retained payload and provider overlap failed releases remain 0

retained pressure:
  combined retained payload high-water remains within the configured retained
  byte budget

allocation:
  every cache-level row is classified against the same-run borrowed allocation
  ratio threshold

elapsed timing:
  every cache-level row is classified against the same-run borrowed elapsed
  ratio threshold

variance:
  repeated rows are stable enough to interpret shape-level readiness

attribution:
  retained payload pool, event-array pool, byte-array pool, queue, overlap,
  and processing allocation evidence remains visible

scope:
  single-file cold retained-ownership cost is carried as a file-level warning,
  not used as cache-level proof or cache-level failure
```

Milestone 016 should start from the milestone 015 thresholds unless the
architecture or implementation plan records a reason to change them before new
measurements are interpreted:

```text
release failures:
  must equal 0

current retained pressure at completion:
  pending, active, and combined counts/bytes must return to 0

combined retained payload high-water:
  must stay within 536870912 bytes unless the configured budget changes in a
  documented future contour decision

allocation ratio:
  queued-owned direct/default allocated bytes should remain <= 1.10x same-run
  borrowed for cache-level readiness rows

elapsed ratio:
  queued-owned direct/default elapsed time should remain <= 1.00x same-run
  borrowed for cache-level readiness rows, or be recorded as a warning or
  blocker with attribution

candidate run spread:
  repeated natural queued-owned direct/default spread should remain <= 7.50%
  of candidate average, or the decision trace must explain why the spread does
  not block the readiness conclusion
```

## Benchmark Scope

The benchmark scope should remain natural Release evidence.

Required benchmark posture:

```text
Release build before capture
focused regression pass before capture
direct API same-run BlockingBorrowed reference rows
direct API omitted-provider queued-owned default rows
CLI omitted-provider cache spot-check
controlled consumer delay disabled
retained pressure telemetry enabled
queue and overlap telemetry visible
allocation attribution visible
direct result contracts inspected for effective contour
deterministic output comparison captured
repeated rows for primary and named-risk contours
broader rows for additional local or approved cache shapes
per-shape interpretation before aggregate interpretation
```

The primary surface is:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
processing benchmark rebalance-archive --cache
```

`MeasureFile()` remains useful only for drift checks and for keeping the
single-file cold warning visible. It should not define cache-level readiness.

The gate should report:

```text
cache root, radar/date selector, max-files selector, and effective file count
effective direct configuration
borrowed elapsed and allocated bytes
queued-owned direct/default elapsed and allocated bytes
queued-owned-to-borrowed elapsed ratio
queued-owned-to-borrowed allocation ratio
published/skipped/examined file counts
payload values and raw checksum
validation checksum and validation status
topology versions and rebalance counters
skipped reason counters
worker failed batch/item counters
queue depth and overlap indicators
retained pending, active, and combined high-water values
current retained pressure at completion
release attempts and failed releases
retained payload pool rent/return/miss telemetry
event-array pool rent/return/miss telemetry
byte-array pool rent/return/miss telemetry
dominant allocation attribution categories
whether direct default equals explicit rollout contour
whether direct default remains aligned with CLI rollout contour
per-shape status: pass, warning, fail, or coverage-only
```

## Single-File Cold Warning

Milestone 015 recorded this warning:

```text
representative KTLX single-file cold smoke allocation ratio: 1.512x borrowed
representative KTLX single-file cold smoke elapsed ratio: 1.072x borrowed
```

Milestone 016 should keep that warning visible without allowing it to distort
the cache-level decision.

Required interpretation:

```text
the warning is real retained-ownership cost for the first pooled retained
event-array and byte-array snapshot

cache-level rows can amortize that cost across many retained batches

the warning does not block broader cache-level readiness by itself

the warning does block any claim that file-level default latency/allocation is
ready

if file-level default readiness is selected instead, that becomes a different
milestone target
```

The milestone should not try to hide the warning through prewarm, shared-pool
contracts, or measured-window shifting unless such a policy is explicitly
designed and reviewed as a separate default-surface decision.

## Operator And Documentation Surface

Milestone 016 should not change the operator story about defaults unless the
implementation plan deliberately includes a documentation-only readiness
statement.

The operator/documentation surface should make these statements true:

```text
CLI omitted-provider rebalance-archive remains queued-owned rollout contour
direct MeasureFile()/MeasureCache() omitted controls remain the same
  queued-owned rollout contour
explicit BlockingBorrowed remains the fallback/oracle path
broader cache-level readiness is accepted, scoped with warnings, rejected, or
  deferred with named evidence
single-file cold retained-ownership cost remains a file-level concern
controlled consumer-delay rows remain mechanics proof, not natural default
  evidence
live ingestion/runtime provider defaults remain out of scope
```

Expected milestone documents:

```text
docs/milestones/016-broader-cache-level-default-readiness.md
docs/milestones/016-broader-cache-level-default-readiness-plan.md
docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md
docs/milestones/016-broader-cache-level-default-readiness-decision-trace.md
docs/milestones/016-broader-cache-level-default-readiness-closeout.md
docs/handoff.md
```

The handoff should state one of:

```text
broader cache-level default readiness accepted

or

broader cache-level default readiness accepted with named warnings

or

broader cache-level default readiness rejected with a named cache shape,
threshold, or attribution blocker

or

broader cache-level default readiness could not be decided because workload
coverage was insufficient

or

broader cache-level default readiness could not be decided because correctness,
cleanup, release health, pressure, fail-closed behavior, timing variance, or
benchmark repeatability regressed
```

## In Scope

Milestone 016 includes:

```text
broader local or explicitly approved NEXRAD cache corpus inventory
cache-level contour selection before gate interpretation
same-run explicit BlockingBorrowed oracle preservation
direct MeasureCache() omitted-default readiness rows
CLI omitted-provider cache alignment checks
explicit queued-owned rollout drift checks where needed
correctness, validation, cleanup, release, retained pressure, timing, spread,
  allocation, and attribution interpretation
per-cache-shape pass/warning/fail classification
single-file cold warning scope preservation
natural Release gate over broader cache-level rows
decision trace that records broader cache-level default readiness
handoff update for readiness posture and next milestone recommendation
```

## Out Of Scope

Milestone 016 does not implement:

```text
new default rollout contour
file-level default latency/allocation optimization
synthetic processing benchmark default migration
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
threshold changes after gate capture
```

## Completion Criteria

Milestone 016 is complete when:

```text
cache corpus inventory is captured with radar/date/file-count/selection
  details

selected cache-level shapes are broad enough for a readiness decision, or
  coverage insufficiency is explicitly recorded

same-run explicit BlockingBorrowed oracle rows remain available, documented,
  and visibly separate

direct MeasureCache() omitted defaults still resolve to the accepted
  queued-owned rollout contour

CLI omitted-provider cache benchmark remains aligned with direct API defaults

correctness parity against borrowed rows is preserved for every readiness row

retained cleanup returns current pressure to zero in natural direct/default
  rows

release failures remain 0

retained pressure stays within the configured 536870912 byte budget

allocation overhead is classified per cache shape against the recorded
  threshold

elapsed timing and variance are classified per cache shape against the
  recorded thresholds

single-file cold retained-ownership cost remains explicitly scoped as
  file-level concern, not cache-level blocker

queued-owned failures remain fail-closed with no automatic borrowed fallback

the decision trace records whether broader cache-level default readiness is
  accepted, accepted with warnings, rejected, coverage-insufficient, or
  deferred

the closeout records verification, gate results, residual risks, and carry
  forward items

handoff states the current broader cache-level readiness posture and
  recommended next milestone unambiguously
```

## Likely Next Milestone Input

If milestone 016 accepts broader cache-level default readiness, the next
milestone can choose one of:

```text
runtime/provider default architecture over the retained-owned boundary
live or durable ingestion architecture
ordered concurrent rebalance architecture
broader benchmark operator/documentation rollout
file-level default latency/allocation optimization if that surface matters
```

If milestone 016 finds a blocker, the next milestone should target the named
blocker:

```text
specific radar/date cache shape exceeds allocation threshold
specific radar/date cache shape exceeds elapsed threshold
retained pressure approaches or exceeds the configured budget
release or cleanup failure
validation parity failure
queue or overlap behavior differs from borrowed correctness expectations
allocation attribution is too coarse to support readiness
benchmark variance makes readiness uninterpretable
direct/CLI contour drift
insufficient workload coverage for broader cache-level default readiness
```

Still deferred unless explicitly reprioritized:

```text
durable queues
live ingestion
cross-process workers
ordered concurrent rebalance
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```

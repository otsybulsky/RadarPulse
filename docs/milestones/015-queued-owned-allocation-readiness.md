# Milestone 015: Queued-Owned Allocation Readiness Architecture

Status: draft.

RadarPulse milestone 015 starts from the closed milestone 014 direct archive
rebalance API default migration. Milestone 014 accepted the queued-owned rollout
contour as the default for direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
calls when provider/execution/queue/retention controls are omitted.

Milestone 014 also carried a repeated allocation warning:

```text
KTLX 2026-05-05 allocation:
  direct gate average: 1.0997x borrowed
  row 1: 1.1018x borrowed
  row 2: 1.0976x borrowed
  threshold: <= 1.10x borrowed
  interpretation: accepted as warning, not clean green
```

This document is intentionally not an implementation plan. It records the
milestone 015 concept, allocation-readiness model, architectural boundaries,
candidate optimization surfaces, gate posture, documentation requirements, and
expected closeout decision before task breakdown is written.

The core decision is:

```text
014 accepted queued-owned as the direct benchmark API default with allocation
    warning.
015 decides whether that allocation warning can be reduced, bounded, or made
    readiness-grade before any live/runtime default expansion.
```

Milestone 015 should not broaden into live ingestion, durable queues,
cross-process workers, ordered concurrent rebalance, builder-transfer, or
non-benchmark archive publishing defaults. It should be a focused allocation
risk-reduction milestone for the already-accepted queued-owned direct/default
contour.

## Milestone Goal

Milestone 015 should make the queued-owned direct/default contour safer to use
as the foundation for later expansion by addressing the repeated allocation
warning directly.

The output of the milestone is the architectural definition of:

```text
allocation baseline for the queued-owned direct/default contour
same-run BlockingBorrowed oracle requirements for allocation work
candidate allocation sources that are safe to optimize
candidate allocation sources that are intentionally left alone
allocation-readiness threshold interpretation
Release gate evidence for before/after direct default rows
KTLX 2026-05-05 warning handling after the allocation pass
decision trace that records whether allocation is ready for broader work
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
safe in 015:
  audit allocation attribution for queued-owned direct/default rows
  reduce allocation in processing callback and retained/owned snapshot work
  improve allocation attribution if current categories are too coarse
  keep same-run BlockingBorrowed rows as oracle evidence
  repeat KTLX 2026-05-05 and keep the warning visible
  prove correctness, cleanup, release health, pressure, and fail-closed
    behavior after any allocation change
  record a readiness decision before closeout

not safe in 015 unless explicitly reprioritized:
  changing the accepted queued-owned rollout contour
  changing live ingestion/runtime provider defaults
  changing synthetic processing benchmark defaults
  changing non-benchmark archive publishing API defaults
  adding durable broker integration or cross-process transport
  adding ordered concurrent rebalance commit semantics
  implementing builder-transfer retained payload execution
  silently falling back from queued-owned failure to borrowed success
  raising allocation thresholds after seeing gate results
  hiding KTLX 2026-05-05 behind aggregate rows
```

## Expected Outcome

At the end of milestone 015, RadarPulse should have a clear answer to this
question:

```text
Is the queued-owned direct/default allocation profile ready to support the next
broader benchmark or runtime-default decision?
```

The acceptable outcomes are:

```text
ready:
  allocation warning is reduced or bounded below the accepted threshold on the
  repeated risk contour, all safety gates pass, and the next milestone can
  consider broader benchmark readiness or runtime-default architecture

ready with warning:
  allocation remains near the threshold, but attribution is stronger, the cost
  is deliberately accepted for the next named surface, and the warning remains
  visible in handoff

not ready:
  allocation remains above or too close to threshold without enough reduction
  or attribution, and the decision trace names the next optimization blocker

defer:
  correctness, release health, cleanup, retained pressure, fail-closed
  behavior, or benchmark variance regresses and allocation readiness cannot be
  decided safely
```

The milestone should not close with a vague monitoring posture. It should
either reduce the allocation warning, deliberately accept it with stronger
evidence, or name the blocker that must be handled next.

## Starting Position

Milestone 014 closed this direct/default contour:

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

same-run BlockingBorrowed rows remain required for allocation gates,
performance regressions, correctness parity, and rollback diagnosis
```

Milestone 014 gate facts carried forward:

```text
primary KTLX 2026-05-04:
  elapsed ratio: 0.911x borrowed
  allocation ratio: 1.071x borrowed
  all-row timing spread: 10.41% because of a favorable outlier
  stabilized rows 2-4 timing spread: 0.39%

KTLX 2026-05-05:
  elapsed ratio: 0.960x borrowed average
  allocation ratio: 1.0997x borrowed average
  run 1 allocation ratio: 1.1018x borrowed
  run 2 allocation ratio: 1.0976x borrowed

KINX 2026-05-04:
  elapsed ratio: 0.906x borrowed
  allocation ratio: 1.069x borrowed

mixed cache:
  elapsed ratio: 0.878x borrowed
  allocation ratio: 1.066x borrowed

retained pressure:
  max observed combined retained payload high-water 54413280 bytes of the
  536870912 byte budget

release and cleanup:
  release failures 0
  current pending, active, and combined retained pressure returned to 0
```

Milestone 014 allocation attribution:

```text
direct default allocation overhead is concentrated in processing callback
allocation and retained/owned snapshot work
```

That attribution is the primary starting hypothesis for milestone 015. It
should be verified before optimization work is treated as successful.

## Architectural Principles

Milestone 015 should follow these principles:

```text
allocation readiness is risk reduction, not a default expansion
the accepted queued-owned rollout contour is preserved unless a new decision
  trace explicitly changes it
BlockingBorrowed remains the same-run oracle for cost interpretation
correctness, cleanup, release health, and fail-closed behavior remain
  non-negotiable
allocation changes must not weaken retained resource lifetime rules
allocation improvements must be attributed, not just observed in aggregate
KTLX 2026-05-05 remains the named risk contour until the warning is removed
or deliberately accepted for a broader surface
thresholds should be recorded before measurements are interpreted
live/runtime default expansion remains a separate future milestone
```

The milestone should separate these concerns:

```text
cost reduction:
  actual reduction in allocated bytes for queued-owned direct/default rows

cost attribution:
  clearer evidence about where the remaining overhead lives

readiness:
  whether the remaining overhead is acceptable for the next named expansion

contour stability:
  whether direct defaults, CLI omitted-provider defaults, and explicit rollout
  calls still resolve to the same accepted queued-owned contour

fallback/oracle stability:
  whether explicit BlockingBorrowed remains available and separate
```

## Allocation Readiness Model

Allocation readiness means the queued-owned direct/default contour has a cost
profile that is understood well enough to support a next expansion decision.
It does not require zero overhead relative to borrowed.

The target readiness posture is:

```text
correctness parity:
  required against same-run BlockingBorrowed rows

release health:
  retained payload failed releases and provider overlap failed releases must
  remain 0

retained cleanup:
  current pending, active, and combined retained counts/bytes must return to 0
  at completion

retained pressure:
  combined retained payload high-water must stay within the configured
  536870912 byte budget

allocation:
  queued-owned direct/default allocated bytes should remain <= 1.10x same-run
  borrowed rows, with KTLX 2026-05-05 interpreted explicitly

elapsed timing:
  allocation reduction must not buy lower allocation by creating a timing
  regression on the primary natural matrix

variance:
  repeated natural rows must be stable enough to interpret before/after
  results

attribution:
  remaining overhead should be assigned to useful categories rather than
  treated as unexplained aggregate allocation
```

Milestone 015 may choose a stricter allocation target in its implementation
plan, for example:

```text
preferred target:
  KTLX 2026-05-05 repeated rows are below 1.10x borrowed individually and on
  average

strong target:
  KTLX 2026-05-05 average moves materially away from the threshold, not merely
  from 1.0997x to another borderline value

readiness target:
  if the ratio remains borderline, the decision trace names why the remaining
  overhead is acceptable for the next named surface
```

Thresholds should not be raised after measurements are captured. If the
milestone needs a different threshold, the plan should record why before the
gate is interpreted.

## Candidate Allocation Surfaces

Milestone 014 names two likely overhead areas:

```text
processing callback allocation
retained/owned snapshot work
```

Milestone 015 should audit these areas first:

```text
retained payload creation and release path:
  whether pooled-copy retained payloads allocate avoidable wrappers,
  snapshots, arrays, or per-batch helper objects

owned snapshot construction:
  whether queued-owned rows duplicate snapshot work already done by replay,
  processing input validation, or retained resource capture

processing callback path:
  whether queued-owned processing creates callback-local collections,
  closures, enumerators, or defensive copies that can be removed without
  weakening lifetime boundaries

queue and overlap telemetry:
  whether summary telemetry allocates per batch where counters or reusable
  summaries would be sufficient

allocation attribution:
  whether existing allocation summary categories are precise enough to prove
  the source of changes after optimization
```

Candidate optimization directions are intentionally phrased as hypotheses:

```text
reduce duplicate snapshot construction where one owned representation can
  safely serve the queued provider and validation path

reuse or pool short-lived retained/owned helper structures only when ownership
  and release timing remain obvious

avoid per-batch allocations in telemetry recorders when aggregate counters can
  preserve the same result contract

avoid closure or LINQ-style allocation on hot queued-owned paths if direct
  loops match the local code style

make allocation attribution more granular if a reduction cannot be trusted
  from aggregate allocated bytes alone
```

Unsafe optimization directions:

```text
borrowing leased payload past the publish callback
sharing mutable batch storage across queued workers without ownership transfer
skipping retained resource release or validation to save allocation
collapsing queued-owned into blocking-borrowed behavior
turning off telemetry needed by migration gates
changing the accepted rollout contour to reduce cost
raising retained-byte budget to hide pressure
```

## Fallback And Oracle Model

BlockingBorrowed keeps the same two roles from milestone 014:

```text
fallback:
  direct callers can still request the borrowed provider explicitly

oracle:
  same-run borrowed rows remain the comparison baseline for correctness,
  allocation, elapsed time, pressure, and rollback diagnosis
```

Allocation work should not make borrowed behavior disappear. It also should not
introduce automatic fallback.

Required comparison shape:

```text
queued-owned direct/default row
same-run explicit BlockingBorrowed oracle row
explicit queued-owned rollout spot-check where drift is possible
CLI omitted-provider rollout spot-check where direct/CLI alignment is at risk
```

Borrowed rows must stay explicit enough that a gate cannot accidentally compare
queued-owned against queued-owned.

## Validation Gate

Milestone 015 should use an allocation readiness gate. This gate is not a
runtime ingestion claim.

Required dimensions:

```text
before/after allocation:
  show queued-owned direct/default allocation ratio against same-run borrowed
  rows before and after the milestone changes, or explain why the milestone
  uses the milestone 014 gate as the before baseline

fallback separation:
  explicit BlockingBorrowed remains borrowed and visibly separate in result
  contracts

direct default contour:
  omitted direct controls still resolve to the accepted queued-owned rollout
  contour

CLI/direct alignment:
  scoped CLI rollout default stays aligned with the direct API contour

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

allocation attribution:
  allocation summary explains where the remaining delta lives

performance:
  elapsed time remains acceptable on primary repeated natural rows

variance:
  repeated rows are stable enough to interpret before/after allocation
```

Milestone 015 should start from the milestone 014 thresholds unless the
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
  borrowed, with KTLX 2026-05-05 explicitly carried as the known borderline
  risk contour

elapsed ratio:
  queued-owned direct/default elapsed time should remain <= 1.00x same-run
  borrowed on the primary repeated natural matrix

candidate run spread:
  repeated natural queued-owned direct/default spread should remain <= 7.50%
  of candidate average, or the decision trace must explain why the spread does
  not block the allocation conclusion
```

## Benchmark Scope

The benchmark scope should remain natural Release evidence.

Required benchmark posture:

```text
Release build before capture
direct API same-run BlockingBorrowed reference rows
direct API omitted-provider queued-owned default rows
controlled consumer delay disabled
retained pressure telemetry enabled
queue and overlap telemetry visible
allocation attribution visible
direct result contracts inspected for effective contour
deterministic output comparison captured
repeated rows for at least one primary contour
repeated KTLX 2026-05-05 allocation-risk row
broader rows for additional local cache shapes
```

Candidate contours:

```text
primary KTLX 2026-05-04 contour retained from milestone 014
borderline KTLX 2026-05-05 contour retained as the named risk shape
KINX 2026-05-04 broader contour
mixed-cache contour over all local NEXRAD cache shapes
single-file MeasureFile() smoke contour for file-level allocation sanity
small fake-data tests for deterministic allocation-sensitive contracts
```

The gate should report:

```text
effective direct configuration
borrowed elapsed and allocated bytes
queued-owned direct/default elapsed and allocated bytes
queued-owned-to-borrowed elapsed ratio
queued-owned-to-borrowed allocation ratio
before/after queued-owned allocation ratio where available
published/skipped/examined file counts
payload values and raw checksum
validation checksum and validation status
topology versions and rebalance counters
skipped reason counters
queue depth and overlap indicators
retained pending, active, and combined high-water values
current retained pressure at completion
release attempts and failed releases
dominant allocation attribution categories
whether direct default equals explicit rollout contour
whether direct default remains aligned with CLI rollout contour
```

## Operator And Documentation Surface

Milestone 015 should not change the operator story about defaults unless the
implementation changes a documented behavior.

The operator/documentation surface should make these statements true:

```text
CLI omitted-provider rebalance-archive remains queued-owned rollout contour
direct MeasureFile()/MeasureCache() omitted controls remain the same
  queued-owned rollout contour
explicit BlockingBorrowed remains the fallback/oracle path
KTLX 2026-05-05 allocation warning is either reduced, bounded, or still
  carried as explicit debt
controlled consumer-delay rows remain mechanics proof, not natural default
  evidence
live ingestion/runtime provider defaults remain out of scope
```

Expected milestone documents:

```text
docs/milestones/015-queued-owned-allocation-readiness.md
docs/milestones/015-queued-owned-allocation-readiness-plan.md
docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
docs/milestones/015-queued-owned-allocation-readiness-decision-trace.md
docs/milestones/015-queued-owned-allocation-readiness-closeout.md
docs/handoff.md
```

The handoff should state one of:

```text
allocation warning reduced enough for the next named expansion

or

allocation warning remains but is deliberately accepted with stronger
attribution for the next named surface

or

allocation warning remains a blocker and the next milestone should target the
named allocation source

or

allocation readiness could not be decided because correctness, cleanup,
release health, pressure, fail-closed behavior, or variance regressed
```

## In Scope

Milestone 015 includes:

```text
queued-owned direct/default allocation baseline audit
allocation attribution audit for processing callback and retained/owned
  snapshot work
targeted allocation reduction on safe queued-owned direct/default hot paths
allocation attribution refinement if needed to make the gate reviewable
same-run explicit BlockingBorrowed oracle preservation
direct default contour drift checks
CLI/direct rollout contour alignment checks
retained cleanup, release health, pressure, cancellation, and failure
  regression coverage after allocation changes
natural Release gate with KTLX 2026-05-05 warning repeated and interpreted
decision trace that records allocation readiness
handoff update for readiness posture and next milestone recommendation
```

## Out Of Scope

Milestone 015 does not implement:

```text
new default rollout contour
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

Milestone 015 is complete when:

```text
allocation baseline and attribution are captured for the queued-owned
  direct/default contour

KTLX 2026-05-05 allocation warning is repeated and interpreted after the
  allocation pass

same-run explicit BlockingBorrowed oracle rows remain available, documented,
  and visibly separate

direct MeasureFile()/MeasureCache() omitted defaults still resolve to the
  accepted queued-owned rollout contour

CLI omitted-provider rollout contour remains aligned with direct API defaults

correctness parity against borrowed rows is preserved

retained cleanup returns current pressure to zero in natural direct/default
  rows

release failures remain 0

retained pressure stays within the configured 536870912 byte budget

allocation overhead is reduced, bounded, or explicitly accepted with stronger
  attribution for the next named surface

timing and variance are interpreted against recorded thresholds

queued-owned failures remain fail-closed with no automatic borrowed fallback

the decision trace records whether allocation readiness is accepted

the closeout records verification, gate results, residual risks, and carry
  forward items

handoff states the current allocation posture and recommended next milestone
  unambiguously
```

## Likely Next Milestone Input

If milestone 015 closes with allocation readiness accepted, the next milestone
can choose one of:

```text
broader benchmark surface readiness/default rollout
runtime/provider default architecture
live or durable ingestion architecture over the retained-owned boundary
ordered concurrent rebalance architecture
```

If milestone 015 finds a blocker, the next milestone should target the named
blocker:

```text
processing callback allocation remains above threshold
retained/owned snapshot allocation remains above threshold
allocation attribution is too coarse to support readiness
KTLX 2026-05-05 remains over threshold without a deliberate acceptance
retained pressure threshold miss
release or cleanup failure
validation parity failure
performance or variance regression
direct/CLI contour drift
insufficient workload coverage for the next expansion decision
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

# Milestone 017: File-Level Default Readiness And Cold Retained-Ownership Cost Architecture

Status: complete.

Closed by
`017-file-level-default-readiness-and-cold-retained-ownership-cost-closeout.md`.

RadarPulse milestone 017 starts from the closed milestone 016 broader
cache-level default readiness result. Milestone 016 accepted broader
cache-level benchmark/default readiness for the queued-owned direct/default
archive rebalance contour used by
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and `MeasureCache()`
when provider/execution/queue/retention controls are omitted.

Milestone 016 closed with this answer:

```text
yes with warnings, broader cache-level default readiness is accepted with
named scoped warnings
```

The important scope limit carried forward is:

```text
current single-file smoke did not reproduce the milestone 015 cold warning,
but file-level default readiness remains outside the milestone 016 decision
```

Milestone 015 had exposed the named file-level risk:

```text
representative KTLX single-file cold smoke allocation ratio: 1.512x borrowed
representative KTLX single-file cold smoke elapsed ratio: 1.072x borrowed
interpretation: expected cold retained-ownership price for the current
  queued-owned pooled-copy architecture, not a cache-level blocker
```

Milestone 016 added a coverage-only file smoke:

```text
representative single-file smoke elapsed ratio: 0.675x borrowed
representative single-file smoke allocation ratio: 1.041x borrowed
interpretation: useful visibility, not file-level default readiness proof
```

This document is intentionally not an implementation plan. It records the
milestone 017 concept, file-level readiness model, cold retained-ownership cost
model, small-file workload coverage posture, fallback/oracle requirements,
validation gate boundaries, documentation requirements, and expected closeout
decision before task breakdown is written.

The core decision is:

```text
016 accepted the queued-owned direct/default contour for broader cache-level
    benchmark/default readiness, with named scoped warnings.
017 decides whether that same contour is ready for file-level MeasureFile()
    and small-file workloads, or whether file-level needs a scoped
    optimization/default decision before runtime expansion.
```

Milestone 017 should not broaden into live ingestion, durable queues,
cross-process workers, ordered concurrent rebalance, builder-transfer,
source-level migration, partition splitting, or product-facing radar analysis.
It should be a file-level and small-file evidence milestone over the already
accepted direct/default benchmark contour.

## Milestone Goal

Milestone 017 should turn the known file-level cold retained-ownership warning
into a reviewable default-readiness decision.

The output of the milestone is the architectural definition of:

```text
file-level corpus and small-file workload selection requirements
cold versus repeated/warm MeasureFile() interpretation
small-file cache slice interpretation where cold cost is only partially
  amortized
same-run BlockingBorrowed oracle requirements for every readiness row
file-level queued-owned default readiness thresholds
cold retained-ownership cost attribution requirements
per-file or per-small-slice pass/warning/fail interpretation
Release gate evidence for direct MeasureFile() omitted defaults
CLI omitted-provider file benchmark alignment checks
cache-level readiness scope preservation
decision trace that records whether file-level default readiness is accepted
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
Broader cache-level readiness remains accepted with named scoped warnings.
```

The key milestone boundary is:

```text
safe in 017:
  inventory local or explicitly approved file-level NEXRAD samples before
    interpreting readiness
  select cold file-level rows before gate interpretation
  select repeated/warm file-level rows before gate interpretation
  select small-file cache slices where retained cold cost is only partially
    amortized
  run direct MeasureFile() omitted-default rows against same-run explicit
    BlockingBorrowed oracle rows
  use direct MeasureCache() only for small-file slices and regression context,
    not as substitute proof for MeasureFile()
  keep CLI omitted-provider file rows aligned with direct defaults
  classify each file or small slice individually before aggregate summaries
  decide whether cold retained-ownership cost is acceptable, a warning, a
    blocker, or an optimization/default-posture target
  preserve the accepted cache-level contour unless evidence justifies a new
    scoped file-level decision
  record readiness, warning, blocker, coverage insufficiency, or optimization
    target in the decision trace

not safe in 017 unless explicitly reprioritized:
  changing live ingestion/runtime provider defaults
  changing synthetic processing benchmark defaults
  changing non-benchmark archive publishing API defaults
  using cache-level readiness as proof of file-level readiness
  using one passing file smoke as proof of file-level readiness
  prewarming, shifting measurement windows, or hiding first-use retained cost
    unless that policy is explicitly designed
  adding durable broker integration or cross-process transport
  adding ordered concurrent rebalance commit semantics
  implementing builder-transfer retained payload execution
  silently falling back from queued-owned failure to borrowed success
  raising thresholds after seeing gate results
  hiding a shape-specific file warning behind small-cache aggregate success
```

## Expected Outcome

At the end of milestone 017, RadarPulse should have a clear answer to this
question:

```text
Is the queued-owned direct/default contour ready for file-level MeasureFile()
and small-file workloads?
```

The acceptable outcomes are:

```text
ready:
  cold, repeated/warm, and small-file rows pass correctness, release, cleanup,
  pressure, elapsed, spread, allocation, and attribution thresholds; same-run
  BlockingBorrowed rows remain available; runtime readiness can use file-level
  evidence as an input

ready with scoped warnings:
  one or more file-level or small-file warnings remain, but each warning is
  assigned to a named file, small slice, threshold, or attribution surface; the
  warning is deliberately accepted for the next named decision and remains
  visible in handoff

optimize before expansion:
  correctness and lifecycle guardrails pass, but cold retained-ownership cost,
  file-level timing, allocation, or variance is too costly or too poorly
  attributed to accept as a default posture; the decision trace names the
  optimization target

change file-level default posture:
  cache-level defaults can remain queued-owned, but file-level omitted
  defaults need a different scoped posture, explicit opt-in, fallback default,
  or separate contour before runtime expansion

not ready:
  a file-level or small-file row fails a non-negotiable correctness, release,
  cleanup, pressure, fail-closed, validation, or attribution requirement; the
  decision trace names the blocker

coverage insufficient:
  available file-level or small-file evidence is too narrow to support the
  decision; the decision trace names the missing workload evidence

defer:
  benchmark repeatability, local corpus condition, test health, or gate
  instrumentation regresses and readiness cannot be decided safely
```

The milestone should not close with a vague monitoring posture. It should
accept file-level readiness, accept it with named warnings, require a named
optimization or default-posture change, reject it with a named blocker, or
state exactly what workload coverage is missing.

## Starting Position

Milestone 016 closed this direct/default contour:

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
performance regressions, allocation follow-up, correctness parity, retained
pressure interpretation, and rollback diagnosis
```

Milestone 016 cache-level facts carried forward:

```text
broader cache-level readiness:
  accepted with named scoped warnings

primary KTLX 2026-05-04 max-files 220:
  elapsed ratio: 0.881x borrowed average
  allocation ratio: 1.028x borrowed average
  candidate spread: 12.01%, above the 7.50% threshold
  interpretation: accepted scoped spread warning

KTLX 2026-05-05 named-risk max-files 220:
  elapsed ratio: 0.822x borrowed average
  allocation ratio: 1.021x borrowed average
  one individual pair elapsed ratio: 1.001x borrowed
  interpretation: accepted timing note

KINX 2026-05-04 max-files 220:
  elapsed ratio: 0.769x borrowed
  allocation ratio: 1.007x borrowed

mixed local cache:
  elapsed ratio: 0.873x borrowed
  allocation ratio: 1.006x borrowed
  candidate worker failed batches/items: 221/881
  validation succeeded and failed migrations remained 0

representative single-file smoke:
  elapsed ratio: 0.675x borrowed
  allocation ratio: 1.041x borrowed
  interpretation: coverage-only, not file-level proof
```

Milestone 015 file-level risk carried forward:

```text
representative KTLX single-file cold smoke:
  allocation ratio: 1.512x borrowed
  elapsed ratio: 1.072x borrowed

interpretation:
  cold retained representation cost plus retained event-array and byte-array
  snapshot work is real on the first retained-owned file-level row
```

Known local NEXRAD cache at milestone 016 closeout:

```text
data/nexrad/level2/2026/05/04/KTLX:
  files: 244
  bytes: 1_347_625_897

data/nexrad/level2/2026/05/04/KINX:
  files: 462
  bytes: 1_404_452_903

data/nexrad/level2/2026/05/05/KTLX:
  files: 848
  bytes: 2_232_493_336

data/nexrad total:
  files: 1_554
  bytes: 4_984_572_136
```

Residual limits carried from milestone 016:

```text
local corpus only:
  prior readiness decisions cover available local NEXRAD cache shapes, not
  absent radar sites, absent dates, or non-local corpora

single-file scope:
  milestone 016 file smoke did not reproduce the milestone 015 cold warning,
  but file-level default readiness remains undecided

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than natural gates

mixed-cache worker counters:
  candidate worker failed batches/items were visible while validation
  succeeded; if worker counters become a file-level readiness criterion, the
  gate must capture the comparison explicitly

runtime scope:
  live ingestion, durable queues, brokers, cross-process providers, ordered
  concurrent rebalance, builder-transfer, and runtime defaults remain outside
  the current decision chain
```

These limits are not automatic blockers for 017, but each must remain visible
when file-level readiness is interpreted.

## Architectural Principles

Milestone 017 should follow these principles:

```text
file-level readiness is a separate default-surface decision, not an inference
  from cache-level readiness
the accepted queued-owned rollout contour is preserved unless a new decision
  trace explicitly changes the file-level posture
BlockingBorrowed remains the same-run oracle for every file-level and
  small-file row
cold and warm behavior must be separated before readiness is interpreted
file-specific warnings are interpreted individually before aggregate summaries
correctness, cleanup, release health, pressure, validation, and fail-closed
  behavior remain non-negotiable
allocation and elapsed thresholds are recorded before gate capture
cold retained-ownership cost is measured directly rather than hidden by
  prewarm or cache amortization
cache-level acceptance remains valid but does not certify MeasureFile()
live/runtime default expansion remains a separate future milestone
```

The milestone should separate these concerns:

```text
cold file-level cost:
  first-use retained ownership, retained event-array, byte-array, wrapper,
  queue, overlap, and processing callback cost for MeasureFile()

warm file-level behavior:
  repeated same-file or same-shape rows after the runtime and retained pools
  have already observed similar sizes

small-file amortization:
  short cache slices where cold retained cost is partially amortized across
  several files but still materially closer to file-level behavior than
  broad cache-level behavior

readiness:
  whether queued-owned direct/default rows pass the required thresholds
  against same-run BlockingBorrowed oracle rows

default posture:
  whether MeasureFile() omitted defaults can stay aligned with MeasureCache()
  omitted defaults, or whether file-level needs a scoped exception

next decision:
  whether runtime readiness can proceed, file-level needs optimization, or
  more workload coverage is required
```

## File-Level Coverage Model

Milestone 017 should define file-level coverage before interpreting readiness.
The minimum useful gate cannot be a single representative smoke.

Required coverage posture:

```text
corpus inventory:
  record every local or explicitly approved file used by the gate, with cache
  root, radar, date, filename, file size, compressed record count where
  available, batch count, event count, payload bytes, and selection reason

named prior-warning file:
  include the milestone 015 representative KTLX file if it is still available,
  or document the replacement and why it is the closest available risk row

cold file-level rows:
  include direct MeasureFile() omitted-default rows that make first-use
  retained ownership visible instead of relying only on warmed repetitions

repeated/warm file-level rows:
  include repeated same-file or same-shape rows so cold cost and warm steady
  behavior can be separated

cross-radar file rows:
  include files from at least KTLX and KINX where available, or document why
  local corpus coverage is narrower

file-size variation:
  include small, representative, and larger file samples from the available
  corpus where local data makes that practical

small-file cache slices:
  include low-max-files MeasureCache() rows over selected radar/date shapes
  where retained cold cost is only partially amortized

coverage statement:
  state whether the selected file-level and small-file corpus is broad enough
  for the milestone decision, and name missing workload classes if it is not
```

Candidate file-level shapes:

```text
KTLX 2026-05-04 representative single-file retained from milestone 015/016
KTLX 2026-05-04 additional small and larger files from the same local root
KINX 2026-05-04 representative file-level rows
KTLX 2026-05-05 named-risk date file-level rows
small-file cache slices such as max-files 2, 4, 8, or another documented
  low-count selector chosen before gate interpretation
```

The gate should avoid these coverage mistakes:

```text
using only the milestone 016 passing file smoke
using only warmed repeated rows and calling cold cost resolved
using only broad cache-level rows to certify MeasureFile()
using a small-cache aggregate pass to hide a failing individual file
changing selected files after seeing results without recording why
adding new data without recording provenance, file count, and selection rules
```

## Cold Retained-Ownership Cost Model

File-level readiness must explicitly model cold retained ownership.

The current queued-owned direct/default contour uses retained-owned payloads
so provider work can outlive the synchronous archive publish callback. That
means the first queued-owned file-level row can pay visible setup and retained
snapshot costs that broad cache-level rows amortize across many retained
batches and files.

The expected cost surfaces are:

```text
retained payload creation:
  retained-owned event and payload representation created before queueing

retained event-array work:
  first-use or same-shape array allocation/reuse behavior for retained events

retained byte-array work:
  payload byte pool rent, miss, return, and reuse behavior

retained resource wrapper and lifecycle:
  queue-owned and consumer-owned transfer records, release callbacks, and
  release state transitions

provider queue and overlap:
  enqueue, dequeue, producer/consumer overlap, queue telemetry, and overlap
  telemetry cost

processing callback:
  callback-local allocation and validation work needed to process retained
  batches

benchmark measurement:
  cold versus repeated/warm measurement windows must be named so benchmark
  output does not blur first-use cost with steady behavior
```

Valid interpretations:

```text
cold cost acceptable:
  cold rows remain within file-level thresholds, lifecycle guardrails pass,
  and attribution is clear enough to accept the current file-level default

cold cost warning:
  cold rows are near a threshold or vary by shape, but the cost is bounded,
  named, and acceptable for the next explicitly named surface

cold cost optimization target:
  cold rows exceed thresholds or remain too expensive for file-level default
  posture, while correctness and lifecycle guardrails still pass

cold cost default-posture blocker:
  cold rows make omitted MeasureFile() defaults inappropriate even if
  MeasureCache() defaults remain accepted

cold cost coverage gap:
  the gate cannot distinguish cold retained cost from noise, corpus bias, or
  warm-pool behavior
```

The milestone should not try to hide cold retained cost through artificial
prewarm, shared-pool reuse, ignored first iteration, or shifted measurement
windows unless the decision explicitly designs that as the intended
file-level operator posture.

## File-Level Default Readiness Model

Default readiness means the queued-owned direct/default file-level contour has
a correctness, lifecycle, and cost profile that is acceptable for
`MeasureFile()` and small-file benchmark workloads. It does not require zero
overhead relative to borrowed.

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
  queued-owned direct/default allocated bytes should remain within the
  file-level threshold recorded in the implementation plan, or the decision
  trace must classify the row as warning, optimization target, blocker, or
  coverage-only

elapsed timing:
  queued-owned direct/default elapsed time should remain within the
  file-level threshold recorded in the implementation plan, or the decision
  trace must classify the row as warning, optimization target, blocker, or
  coverage-only

variance:
  repeated file-level rows must be stable enough to distinguish cold cost,
  warm behavior, and benchmark noise

attribution:
  remaining overhead should have enough telemetry to explain whether it is
  retained payload, event-array, byte-array, callback, queue, overlap, replay,
  validation, or measurement cost

small-file amortization:
  small-cache rows should show whether retained cold cost becomes acceptable
  after a few files, and where the transition from file-level to cache-level
  behavior starts
```

Milestone 017 may choose thresholds in its implementation plan, but they must
be recorded before the gate is interpreted. The plan should start from the
milestone 016 cache-level safety thresholds and add file-level-specific
interpretation rather than silently reusing cache-level conclusions.

Suggested threshold posture to evaluate before gate capture:

```text
release failures:
  must equal 0

current retained pressure at completion:
  pending, active, and combined counts/bytes must return to 0

combined retained payload high-water:
  must stay within 536870912 bytes unless the configured budget changes in a
  documented future contour decision

correctness and validation:
  direct default rows must match same-run borrowed output and validation
  expectations

allocation ratio:
  cold MeasureFile() rows should be classified against a file-level threshold
  chosen before measurement interpretation; cache-level <= 1.10x borrowed is
  not automatically sufficient or required for every cold file row unless the
  implementation plan deliberately adopts it

elapsed ratio:
  cold MeasureFile() rows should be classified against a file-level threshold
  chosen before measurement interpretation; cache-level <= 1.00x borrowed is
  not automatically sufficient or required for every cold file row unless the
  implementation plan deliberately adopts it

candidate run spread:
  repeated natural queued-owned direct/default spread should be low enough to
  interpret file-level readiness, or the decision trace must explain why the
  spread does not block the conclusion
```

Thresholds should not be raised after measurements are captured. If file-level
requires different thresholds from cache-level readiness, the implementation
plan should state why before the gate is interpreted.

## Fallback And Oracle Model

BlockingBorrowed keeps the same two roles from milestone 016:

```text
fallback:
  direct callers and CLI users can still request the borrowed provider
  explicitly

oracle:
  same-run borrowed rows remain the comparison baseline for correctness,
  allocation, elapsed time, pressure, retained telemetry, and rollback
  diagnosis
```

Required comparison shape:

```text
direct MeasureFile() queued-owned omitted-default row
same-run direct MeasureFile() explicit BlockingBorrowed oracle row
direct MeasureCache() queued-owned omitted-default row for small-file slices
same-run direct MeasureCache() explicit BlockingBorrowed oracle row for
  small-file slices
CLI omitted-provider file spot-check where direct/CLI alignment is at risk
explicit queued-owned rollout spot-check where contour drift is possible
```

Borrowed rows must stay explicit enough that a gate cannot accidentally compare
queued-owned against queued-owned. Any automatic fallback from queued-owned
failure to borrowed success remains disallowed.

## Validation Gate

Milestone 017 should use a file-level and small-file readiness gate. This gate
is not a runtime ingestion claim.

Required dimensions:

```text
corpus inventory:
  list file paths, radar/date pairs, file sizes, selection rules, small-file
  cache slice selectors, and skipped or unavailable workload classes

fallback separation:
  explicit BlockingBorrowed remains borrowed and visibly separate in result
  contracts and CLI output

default contour:
  omitted direct controls still resolve to the accepted queued-owned rollout
  contour unless the milestone makes a scoped file-level decision to change it

CLI/direct alignment:
  scoped CLI omitted-provider file benchmark stays aligned with the direct API
  contour

cold/warm separation:
  cold file-level rows and repeated/warm rows are reported separately

small-file transition:
  low-count cache slices show whether retained cold cost is amortized enough
  to behave like accepted cache-level rows

correctness parity:
  file size, compressed records, payload values, raw checksum, validation
  checksum, topology versions, accepted moves, skipped decisions, failed
  migrations, and skipped reason counters match same-run borrowed rows where
  the result contract exposes them

cleanup:
  current pending, active, and combined retained counts/bytes return to zero
  at completion

release health:
  retained payload and provider overlap failed releases remain 0

retained pressure:
  combined retained payload high-water remains within the configured retained
  byte budget

allocation:
  every file-level and small-file row is classified against the recorded
  file-level allocation threshold

elapsed timing:
  every file-level and small-file row is classified against the recorded
  file-level elapsed threshold

variance:
  repeated rows are stable enough to interpret cold versus warm readiness

attribution:
  retained payload pool, event-array pool, byte-array pool, queue, overlap,
  replay, processing callback, and validation evidence remains visible

scope:
  cache-level acceptance remains a baseline input, not proof of file-level
  acceptance
```

## Benchmark Scope

The benchmark scope should remain natural Release evidence.

Required benchmark posture:

```text
Release build before capture
focused regression pass before capture
direct API same-run BlockingBorrowed reference rows
direct API omitted-provider queued-owned default rows
CLI omitted-provider file spot-check
controlled consumer delay disabled
retained pressure telemetry enabled
queue and overlap telemetry visible
allocation attribution visible
direct result contracts inspected for effective contour
deterministic output comparison captured
cold rows for selected MeasureFile() samples
repeated/warm rows for selected MeasureFile() samples
small-file cache rows for low-count MeasureCache() slices
per-file and per-slice interpretation before aggregate interpretation
```

The primary surfaces are:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
processing benchmark rebalance-archive --file
```

Secondary context surfaces:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureCache() with low max-files
processing benchmark rebalance-archive --cache with low --max-files
```

Broad cache-level rows from milestone 016 should be treated as accepted
baseline context, not re-proven unless drift or regression evidence requires
it.

The gate should report:

```text
file path, radar/date selector, file size, and selection reason
small-file cache root, selector, max-files, and effective file count
effective direct configuration
borrowed elapsed and allocated bytes
queued-owned direct/default elapsed and allocated bytes
queued-owned-to-borrowed elapsed ratio
queued-owned-to-borrowed allocation ratio
cold, repeated, and warm row classification
published/skipped/examined file counts where applicable
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
per-file or per-slice status: pass, warning, optimize, fail, or coverage-only
```

## Operator And Documentation Surface

Milestone 017 should not change the operator story about defaults unless the
decision explicitly changes the file-level default posture.

The operator/documentation surface should make these statements true:

```text
CLI omitted-provider rebalance-archive --file remains queued-owned rollout
  contour unless this milestone makes a scoped file-level default decision
direct MeasureFile() omitted controls remain queued-owned unless this
  milestone makes a scoped file-level default decision
direct MeasureCache() omitted controls retain the milestone 016 accepted
  cache-level posture
explicit BlockingBorrowed remains the fallback/oracle path
file-level readiness is accepted, accepted with warnings, optimization-bound,
  posture-changing, rejected, or coverage-insufficient with named evidence
cold retained-ownership cost remains visible in handoff
controlled consumer-delay rows remain mechanics proof, not natural default
  evidence
live ingestion/runtime provider defaults remain out of scope
```

Expected milestone documents:

```text
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-plan.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-performance-gate.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-decision-trace.md
docs/milestones/017-file-level-default-readiness-and-cold-retained-ownership-cost-closeout.md
docs/handoff.md
docs/project-progress.md
```

The handoff should state one of:

```text
file-level default readiness accepted

or

file-level default readiness accepted with named warnings

or

file-level default readiness requires a named optimization before runtime
expansion

or

file-level default posture changed or split from cache-level posture by an
explicit scoped decision

or

file-level default readiness rejected with a named file, small-file slice,
threshold, lifecycle, validation, or attribution blocker

or

file-level default readiness could not be decided because workload coverage
was insufficient

or

file-level default readiness could not be decided because correctness,
cleanup, release health, pressure, fail-closed behavior, timing variance, or
benchmark repeatability regressed
```

## In Scope

Milestone 017 includes:

```text
file-level local or explicitly approved NEXRAD corpus inventory
single-file sample selection before gate interpretation
small-file cache slice selection before gate interpretation
cold MeasureFile() direct/default readiness rows
repeated/warm MeasureFile() direct/default readiness rows
same-run explicit BlockingBorrowed oracle preservation
direct MeasureFile() omitted-default readiness interpretation
low-count MeasureCache() small-file transition interpretation
CLI omitted-provider file alignment checks
explicit queued-owned rollout drift checks where needed
correctness, validation, cleanup, release, retained pressure, timing, spread,
  allocation, and attribution interpretation
per-file and per-small-slice pass/warning/optimize/fail classification
cold retained-ownership cost attribution and decision language
natural Release gate over file-level and small-file rows
decision trace that records file-level default readiness
handoff and project-progress updates for readiness posture and next milestone
  recommendation
```

## Out Of Scope

Milestone 017 does not implement:

```text
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
prewarm-as-policy unless explicitly designed as a file-level default decision
new broad cache-level readiness campaign unless regression evidence requires it
```

## Completion Criteria

Milestone 017 is complete when:

```text
file-level corpus inventory is captured with file path, radar/date,
  size/count, and selection details

small-file cache slices are selected with max-files and selection rules before
  interpretation

selected file-level and small-file shapes are broad enough for a readiness
  decision, or coverage insufficiency is explicitly recorded

same-run explicit BlockingBorrowed oracle rows remain available, documented,
  and visibly separate

direct MeasureFile() omitted defaults still resolve to the accepted
  queued-owned rollout contour unless a scoped file-level decision changes
  that posture

direct MeasureCache() omitted defaults preserve the milestone 016 accepted
  cache-level posture

CLI omitted-provider file benchmark remains aligned with direct API defaults
  unless a scoped file-level decision changes that posture

correctness parity against borrowed rows is preserved for every readiness row

retained cleanup returns current pressure to zero in natural direct/default
  rows

release failures remain 0

retained pressure stays within the configured 536870912 byte budget

cold file-level allocation and elapsed cost are classified against thresholds
  recorded before gate interpretation

repeated/warm file-level behavior is classified separately from cold behavior

small-file cache slices show whether cold retained cost is partially
  amortized or remains a blocker

allocation and timing attribution are sufficient to explain remaining cost or
  the decision trace names attribution as a blocker

queued-owned failures remain fail-closed with no automatic borrowed fallback

the decision trace records whether file-level default readiness is accepted,
  accepted with warnings, optimization-bound, posture-changing, rejected,
  coverage-insufficient, or deferred

the closeout records verification, gate results, residual risks, and carry
  forward items

handoff and project-progress state the current file-level readiness posture
  and recommended next milestone unambiguously
```

## Likely Next Milestone Input

If milestone 017 accepts file-level default readiness, the next milestone can
consider:

```text
runtime/provider default architecture over the retained-owned boundary
live ingestion/runtime readiness architecture
durable or cross-process ingestion architecture
ordered concurrent rebalance architecture
broader benchmark operator/documentation rollout
```

If milestone 017 accepts readiness with warnings, the next milestone should
carry the named warnings explicitly and decide whether they matter for the
next selected surface.

If milestone 017 finds a blocker, the next milestone should target the named
blocker:

```text
cold MeasureFile() allocation exceeds threshold
cold MeasureFile() elapsed time exceeds threshold
repeated/warm MeasureFile() rows remain unstable
small-file cache slices fail to amortize retained ownership cost
retained event-array or byte-array cold allocation remains too high
retained resource wrapper or release lifecycle cost remains too high
processing callback allocation remains too high
provider queue or overlap cost dominates file-level rows
retained pressure approaches or exceeds the configured budget
release or cleanup failure
validation parity failure
allocation attribution is too coarse to support readiness
direct/CLI contour drift
insufficient workload coverage for file-level default readiness
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
product-facing radar workflows
```

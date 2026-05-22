# Milestone 016 Decision Trace

Date: 2026-05-22

Decision: accept broader cache-level default readiness for the queued-owned
direct/default archive rebalance contour, with named scoped warnings and
explicit `BlockingBorrowed` preserved as fallback and same-run oracle.

This decision accepts the queued-owned direct/default contour as the broader
cache-level benchmark/default posture for the available local cache workloads.
The Release gate preserved correctness, validation, release health, retained
cleanup, retained pressure budget, allocation threshold, elapsed averages,
fallback separation, and CLI/direct contour alignment.

The decision is not clean-green. It carries a primary timing-spread warning,
a named-risk borderline individual elapsed pair note, a mixed-cache worker
counter note, and a file-smoke coverage-only scope limit.

The representative single-file smoke did not reproduce the milestone 015
single-file cold warning in this run, but milestone 016 still does not certify
file-level default readiness. Live ingestion/runtime defaults, durable queues,
cross-process workers, ordered concurrent rebalance, and builder-transfer
remain out of scope.

## Decision Matrix

```text
broader cache-level readiness posture:
  accepted with named scoped warnings for the available local cache workloads

primary KTLX 2026-05-04 posture:
  accepted with scoped spread warning; candidate spread was 12.01%, above the
  7.50% threshold, but all individual candidate runs remained faster than
  same-run borrowed and all correctness, lifecycle, pressure, and allocation
  guardrails passed

KTLX 2026-05-05 named-risk posture:
  accepted with timing note; one individual pair was 1.001x borrowed, while
  the repeated average passed at 0.822x and the larger risk-440 row passed at
  0.810x

KINX 2026-05-04 posture:
  accepted cleanly for the captured 220-file and 440-file rows

mixed-cache posture:
  accepted with worker-counter note; candidate worker failed batches/items
  were 221/881 while validation succeeded and failed migrations remained 0

file-smoke posture:
  coverage-only; current row passed allocation and elapsed checks but does not
  establish file-level default readiness

fallback/oracle posture:
  accepted; explicit BlockingBorrowed remains selectable and required for
  same-run gate comparisons

CLI/direct contour alignment:
  accepted; CLI omitted-provider cache benchmark and direct omitted defaults
  remain aligned to the shared queued-owned rollout contour

follow-up posture:
  no targeted rerun, borrowed worker-counter recapture, runtime fix, reporting
  fix, or threshold change is required before closeout

runtime expansion posture:
  not approved; live/runtime defaults remain out of scope for milestone 016
```

## Decision Explanations

### Accept Broader Cache-Level Default Readiness

Decision: accept broader cache-level default readiness for direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureCache()` omitted defaults on
the available local cache workloads, with named scoped warnings.

Why chosen: the Release gate passed correctness, validation, release health,
retained cleanup, retained pressure, allocation, direct/CLI alignment, and
elapsed averages across the selected cache-level rows. The primary KTLX row
averaged `0.881x` borrowed elapsed and `1.028x` borrowed allocation. The named
risk row averaged `0.822x` borrowed elapsed and `1.021x` borrowed allocation.
KINX measured `0.769x` elapsed and `1.007x` allocation. Mixed cache measured
`0.873x` elapsed and `1.006x` allocation. Optional larger rows also passed.

Alternatives: defer the decision for more local reruns, reject readiness
because the primary spread exceeded threshold, require borrowed worker-counter
recapture before any decision, or call the gate clean green.

Rejected because: the remaining warnings are scoped and attributable enough
for decision trace; the primary spread warning did not coincide with an
elapsed, allocation, correctness, release, cleanup, or pressure failure; the
mixed-cache worker-counter shape was already known from milestone 015; and
calling the gate clean green would hide real review notes.

Trade-offs/debt: readiness remains scoped to cache-level benchmark/default
posture over the available local corpus. It does not certify every radar/date
shape, file-level cold behavior, runtime ingestion, durable transport, or
cross-process execution.

Review explanation: "Broader cache-level default readiness is accepted, but
the decision keeps the spread, timing, worker-counter, and file-smoke notes
visible."

### Accept Primary Spread As Scoped Warning

Decision: accept the primary KTLX 2026-05-04 spread result as a scoped warning,
not a blocker.

Why chosen: the primary row used three same-run borrowed/default pairs over
`data\nexrad --date 2026-05-04 --radar KTLX --max-files 220`. Candidate
spread was `12.01%`, above the `7.50%` threshold, but the candidate stayed
faster than same-run borrowed in every individual pair: `0.852x`, `0.885x`,
and `0.906x`. Allocation stayed below threshold, with a `1.028x` average and
individual ratios up to `1.040x`. Validation, checksums, release health,
retained cleanup, and retained pressure all passed.

Alternatives: rerun the primary row before decision trace, fail the milestone
on spread, raise the variance threshold, or ignore the warning.

Rejected because: rerun is not required to interpret the current evidence;
failing the milestone would over-weight a variance warning that did not harm
same-run relative timing or safety; raising thresholds after measurement is
not allowed; and ignoring the warning would make the decision less reviewable.

Trade-offs/debt: future closeout or regression gates should keep primary
spread visible. If a later primary row combines high spread with slower
candidate timing, allocation movement, or lifecycle warnings, it should be
treated as a fresh blocker instead of relying on this acceptance.

Review explanation: "The primary spread exceeded threshold, but it stayed a
timing-repeatability warning rather than a readiness blocker."

### Accept Named-Risk Timing Note

Decision: accept the KTLX 2026-05-05 named-risk row with a timing note.

Why chosen: one individual KTLX 2026-05-05 pair measured `1.001x` borrowed
elapsed, but the repeated average passed at `0.822x`, the first pair measured
`0.688x`, and the larger `risk-440` row measured `0.810x`. Allocation passed
at `1.021x` average for the repeated row and `1.008x` for `risk-440`.
Validation, release health, cleanup, and pressure passed.

Alternatives: require a third KTLX 2026-05-05 220-file pair, make the
individual `1.001x` pair a blocker, or remove the timing note because the
average passed.

Rejected because: the larger same-shape optional row already provides
supporting evidence; `1.001x` is a borderline individual timing result rather
than a correctness or lifecycle failure; and removing the note would hide a
near-threshold observation on a named risk shape.

Trade-offs/debt: KTLX 2026-05-05 remains a useful future sentinel for cache
gates because it has carried historical allocation and timing review notes.

Review explanation: "The named-risk shape passes overall, with one borderline
individual timing row carried for visibility."

### Accept Mixed-Cache Worker Counter Note

Decision: accept the mixed-cache row with an explicit worker-counter note and
no borrowed worker-counter recapture before closeout.

Why chosen: the mixed-cache direct/default candidate reported worker failed
batches/items as `221/881` while validation succeeded, failed migrations
remained `0`, elapsed ratio passed at `0.873x`, allocation passed at `1.006x`,
release failures stayed `0`, and retained cleanup returned to `0`. The same
counter shape was already visible in milestone 015. Slice 5 did not recapture
borrowed worker failed counters for this row, so the decision records that
limit explicitly.

Alternatives: recapture the mixed-cache row with borrowed worker counters
before decision trace, treat worker counters as a blocker, remove mixed-cache
from readiness evidence, or hide the counter note behind aggregate success.

Rejected because: no correctness, migration, validation, release, cleanup,
pressure, elapsed, or allocation failure accompanied the worker counters; the
pattern is known from milestone 015; mixed-cache remains valuable broader
coverage; and hiding the note would violate the milestone rule not to bury
shape-specific warnings in aggregate success.

Trade-offs/debt: if worker failed counters become a primary readiness
criterion in a later milestone, the borrowed/default counter capture should be
made explicit in the runner or product reporting surface.

Review explanation: "The mixed-cache worker counters remain visible, accepted,
and scoped; they did not invalidate the cache-level result."

### Preserve File-Smoke Coverage Scope

Decision: keep the representative single-file smoke as coverage-only and out
of the broader cache-level readiness claim.

Why chosen: the milestone 016 file-smoke row measured `0.675x` borrowed
elapsed and `1.041x` borrowed allocation, so it did not reproduce the
milestone 015 single-file cold warning. However, one representative file is
not enough to certify file-level default readiness, and the milestone target
is broader cache-level readiness.

Alternatives: declare file-level default readiness from the smoke row, keep
the milestone 015 file warning as an active blocker despite the new row, or
start a file-level optimization campaign inside milestone 016.

Rejected because: a single smoke row is not a file-level readiness corpus;
the old file warning did not block cache-level readiness and did not reproduce
in this run; and file-level optimization would be a separate milestone target.

Trade-offs/debt: file-level default latency/allocation remains a valid future
surface if selected. Such work should use file-level corpus design and
thresholds rather than borrowing cache-level evidence.

Review explanation: "The file smoke is useful visibility, not a file-level
default readiness decision."

### Preserve Explicit Borrowed Fallback And Oracle

Decision: preserve explicit `BlockingBorrowed` as fallback and same-run oracle
for archive rebalance benchmark gates.

Why chosen: the Release gate used same-run borrowed/default pairs to interpret
correctness parity, elapsed time, allocation, retained telemetry, and fallback
separation. Explicit borrowed remains the clearest oracle and rollback
diagnostic path.

Alternatives: remove borrowed comparison after accepting broader readiness,
compare only against historical milestone 015 numbers, silently fall back to
borrowed after queued-owned failure, or treat borrowed as obsolete.

Rejected because: broader readiness does not eliminate the need for same-run
oracle rows; historical rows cannot replace same-input comparison; silent
fallback would hide queued-owned failures; and explicit fallback remains a
useful operator and benchmark control.

Trade-offs/debt: future gates remain heavier because they carry borrowed and
candidate rows. That cost is accepted because it keeps decisions reviewable.

Review explanation: "Borrowed remains the explicit comparison baseline, not a
silent rescue path."

### Keep CLI And Direct Contours Aligned

Decision: keep CLI omitted-provider cache benchmarking aligned with direct
`MeasureFile()` and `MeasureCache()` omitted defaults through the shared
queued-owned rollout contour.

Why chosen: the CLI omitted-provider spot-check reported queued-owned provider
mode, rollout-default source, default rollout contour yes, rollout default
expansion yes, fallback contour no, natural-readiness evidence, retained pool
telemetry, overlap telemetry, allocation attribution, and validation success.
The explicit `--provider blocking-borrowed` spot-check reported explicit
borrowed fallback provenance and no queued/retained telemetry, as expected.

Alternatives: split CLI and direct defaults, require CLI-only Release gates,
or move CLI provenance concepts into direct API result contracts before
decision.

Rejected because: default divergence would make operator behavior and gates
harder to explain; CLI-only capture would increase manual transcription risk;
and direct API structured result contracts already expose the data needed for
temporary gate runners.

Trade-offs/debt: CLI provenance remains presentation-owned. Direct gates
should continue to rely on structured result fields and explicit contour
arguments.

Review explanation: "CLI and direct omitted-provider behavior still tell the
same queued-owned rollout story."

### Keep Runtime Expansion Out Of Scope

Decision: do not use milestone 016 to approve live ingestion/runtime provider
defaults, durable queues, cross-process workers, ordered concurrent rebalance,
or builder-transfer.

Why chosen: milestone 016 measured local archive cache benchmark readiness.
Runtime ingestion and durable transport require separate contracts for
durability, recovery, ordering, backpressure, observability, operator control,
and lifecycle.

Alternatives: treat broader cache-level benchmark readiness as runtime
readiness, start durable broker design inside milestone 016, or approve
runtime defaults from archive benchmark evidence.

Rejected because: those alternatives would overstate what was measured and
would collapse separate architecture decisions into a cache-level benchmark
milestone.

Trade-offs/debt: runtime/default work remains future scope. It can use this
decision as an input, but it still needs its own architecture, tests, and
Release gates.

Review explanation: "Milestone 016 certifies cache-level benchmark/default
readiness, not runtime ingestion."

## Included Surface

Included direct API defaults:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
  omitted providerMode
  omitted executionMode
  omitted asyncExecution
  omitted queueCapacity
  omitted providerOverlapMode
  omitted retentionStrategy
  omitted queueRetainedPayloadBytes
  omitted overlapConsumerDelay

RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  omitted providerMode
  omitted executionMode
  omitted asyncExecution
  omitted queueCapacity
  omitted providerOverlapMode
  omitted retentionStrategy
  omitted queueRetainedPayloadBytes
  omitted overlapConsumerDelay
```

Effective direct default:

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
```

Explicit fallback/oracle:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Aligned CLI surface:

```text
processing benchmark rebalance-archive omitted-provider cache path remains
aligned with the same queued-owned rollout contour
```

Excluded:

```text
synthetic processing benchmark defaults
non-benchmark archive publishing APIs
live ingestion/runtime provider defaults
durable queue or broker integration
cross-process provider or worker transport
ordered concurrent rebalance commit barrier
builder-transfer retained payload execution
automatic failover from queued-owned to blocking-borrowed
source-level migration or partition splitting
file-level default latency/allocation certification
```

## Evidence

Primary source documents:

```text
docs/milestones/016-broader-cache-level-default-readiness.md
docs/milestones/016-broader-cache-level-default-readiness-plan.md
docs/milestones/016-broader-cache-level-default-readiness-performance-gate.md
```

Focused regression before the gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests|FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~RadarProcessingRetainedBatchResourceTests"

112 passed, 0 failed, 0 skipped
```

Release build before the gate:

```text
dotnet build RadarPulse.sln -c Release --no-restore
succeeded, 0 warnings, 0 errors
```

Temporary gate runner build:

```text
dotnet build data\temp\m016-gate-runner\M016GateRunner.csproj -c Release --no-restore
succeeded, 0 warnings, 0 errors
```

Release gate matrix:

```text
primary repeated contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  three borrowed/default pairs

named-risk repeated contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-05 --radar KTLX --max-files 220
  two borrowed/default pairs

cross-radar contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KINX --max-files 220

mixed-cache contour:
  direct MeasureCache()
  data\nexrad --max-files 1000000

optional size-extension contours:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 244
  data\nexrad --date 2026-05-04 --radar KINX --max-files 440
  data\nexrad --date 2026-05-05 --radar KTLX --max-files 440

file-level smoke contour:
  direct MeasureFile()
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
```

Local corpus:

```text
data\nexrad\level2\2026\05\04\KTLX:
  files: 244
  bytes: 1_347_625_897

data\nexrad\level2\2026\05\04\KINX:
  files: 462
  bytes: 1_404_452_903

data\nexrad\level2\2026\05\05\KTLX:
  files: 848
  bytes: 2_232_493_336

data\nexrad total:
  files: 1_554
  bytes: 4_984_572_136
```

## Threshold Decisions

Correctness parity: accepted.

```text
validation succeeded across captured rows
same-run borrowed/candidate counters and checksums matched in gate output
failed migrations: 0 across captured rows
```

Release health: accepted.

```text
retained payload failed releases: 0
provider overlap failed releases: 0
```

Retained cleanup: accepted.

```text
current combined retained bytes at completion: 0
```

Retained pressure budget: accepted.

```text
budget: 536_870_912 bytes
maximum observed retained high-water: 54_413_280 bytes
maximum observed pressure share: 10.14%

primary high watermark: 48_257_280 bytes
named-risk high watermark: 52_676_640 bytes
KINX high watermark: 48_342_240 bytes
mixed-cache high watermark: 54_413_280 bytes
```

Elapsed threshold: accepted on cache-level averages.

```text
threshold: <= 1.00x borrowed
primary KTLX 2026-05-04 average elapsed ratio: 0.881x borrowed
KTLX 2026-05-05 named-risk average elapsed ratio: 0.822x borrowed
KINX 2026-05-04 elapsed ratio: 0.769x borrowed
mixed-cache elapsed ratio: 0.873x borrowed
KTLX 2026-05-04 full-root elapsed ratio: 0.887x borrowed
KINX 2026-05-04 larger-slice elapsed ratio: 0.782x borrowed
KTLX 2026-05-05 larger-slice elapsed ratio: 0.810x borrowed

named-risk individual pair 2 elapsed ratio:
  1.001x borrowed, accepted as timing note
```

Run spread threshold: accepted with scoped warning.

```text
threshold: candidate spread <= 7.50% of candidate average
primary candidate spread: 12.01%, accepted as scoped warning
named-risk candidate spread: 7.42%, accepted with timing note
```

Allocation threshold: accepted for cache-level readiness.

```text
threshold: <= 1.10x borrowed
maximum cache-level average allocation ratio: 1.028x borrowed
maximum cache-level individual measured pair ratio: 1.040x borrowed

primary KTLX 2026-05-04 average allocation ratio: 1.028x borrowed
KTLX 2026-05-05 named-risk average allocation ratio: 1.021x borrowed
KINX 2026-05-04 allocation ratio: 1.007x borrowed
mixed-cache allocation ratio: 1.006x borrowed
KTLX 2026-05-04 full-root allocation ratio: 1.008x borrowed
KINX 2026-05-04 larger-slice allocation ratio: 1.000x borrowed
KTLX 2026-05-05 larger-slice allocation ratio: 1.008x borrowed
```

File-smoke threshold posture: coverage-only.

```text
representative file elapsed ratio: 0.675x borrowed
representative file allocation ratio: 1.041x borrowed
interpretation: useful visibility, not file-level default readiness proof
```

Direct default expansion evidence: accepted.

```text
direct default rows omitted provider, execution, async execution, queue,
overlap, retention, and retained-byte controls
direct default rows resolved to queued-owned, producer-consumer, pooled-copy,
async shard transport, workers 4, worker queue capacity 8, provider queue
capacity 8, retained-byte budget 536870912, and overlap consumer delay 0
```

Fallback separation: accepted.

```text
borrowed rows used explicit providerMode: BlockingBorrowed
candidate rows used omitted provider-related arguments
CLI explicit borrowed spot-check kept fallback provenance visible
no row used automatic fallback from direct default queued-owned to borrowed
```

## Operational Posture

Direct default posture:

```text
Direct MeasureFile()/MeasureCache() calls that omit provider-related controls
select the queued-owned rollout contour.
```

Fallback posture:

```text
Direct callers can still request borrowed behavior explicitly with:
  providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Oracle posture:

```text
Same-run BlockingBorrowed rows remain the comparison oracle for future direct
API gates, broader cache coverage, performance regressions, allocation
follow-up, and rollback diagnosis.
```

CLI posture:

```text
The CLI omitted-provider rebalance-archive cache path remains aligned with
the same queued-owned rollout contour.
```

Failure posture:

```text
queued-owned failures fail closed
there is no automatic borrowed fallback after queued-owned failure
fallback is an explicit provider choice only
```

Runtime posture:

```text
live ingestion/runtime defaults remain unchanged and out of scope
durable queues, brokers, cross-process providers, ordered concurrent
rebalance, and builder-transfer require future milestones
```

## Residual Risks And Limits

```text
local corpus only:
  the decision covers the available local NEXRAD cache shapes captured in the
  milestone 016 gate; it does not certify absent radar sites, absent dates, or
  non-local corpora

primary spread warning:
  primary candidate spread was 12.01%, above the 7.50% threshold; accepted as
  scoped warning because relative timing and safety guardrails still passed

named-risk timing note:
  one KTLX 2026-05-05 individual pair measured 1.001x borrowed; accepted
  because repeated average and larger same-shape row passed

mixed-cache worker counters:
  candidate worker failed batches/items were 221/881 while validation
  succeeded; slice 5 did not recapture borrowed worker failed counters for
  that row

single-file scope:
  current single-file smoke did not reproduce the milestone 015 cold warning,
  but file-level default readiness remains outside this decision

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than this natural gate

no runtime ingestion claim:
  live ingestion, durable queues, brokers, cross-process providers, ordered
  concurrent rebalance, builder-transfer, and runtime defaults remain outside
  milestone 016
```

## Decision

Milestone 016 answers the closeout question with **yes, with warnings** for
broader cache-level default readiness:

```text
the queued-owned direct/default contour is ready as the broader cache-level
benchmark/default posture for available cache workloads, with named scoped
warnings
```

Milestone 016 answers **accepted as scoped warning** for the primary spread:

```text
primary candidate spread was 12.01%, above the 7.50% threshold, but every
individual candidate run remained faster than same-run borrowed and all
correctness, lifecycle, pressure, and allocation guardrails passed
```

Milestone 016 answers **accepted as timing note** for the named-risk row:

```text
one individual KTLX 2026-05-05 pair was 1.001x borrowed, but the repeated
average was 0.822x and the larger same-shape row was 0.810x
```

Milestone 016 answers **accepted with explicit note** for the mixed-cache
worker counters:

```text
candidate worker failed batches/items were 221/881 while validation succeeded
and failed migrations remained 0; borrowed worker failed counters were not
recaptured in slice 5
```

Milestone 016 answers **coverage-only** for the file-smoke row:

```text
the file-smoke row is useful visibility and did not reproduce the milestone
015 cold warning, but it does not certify file-level default readiness
```

Milestone 016 answers **yes** for preserving explicit borrowed fallback and
same-run oracle coverage:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Milestone 016 answers **no** for live/runtime default migration:

```text
live ingestion, durable queues, brokers, cross-process providers, ordered
concurrent rebalance, builder-transfer, and runtime defaults remain out of
scope
```

Recommended next milestone input:

```text
close out milestone 016, then use broader cache-level readiness as input for
the next explicitly scoped milestone; likely candidates are either file-level
default readiness/optimization or a separately designed runtime/durable
default-readiness milestone, but neither is approved by milestone 016 itself
```

The milestone can proceed to closeout without additional runtime changes,
targeted reruns, borrowed worker-counter recapture, or threshold changes.

# Milestone 015 Decision Trace

Date: 2026-05-21

Decision: accept cache-level allocation readiness for the queued-owned
direct/default archive rebalance contour, with explicit `BlockingBorrowed`
preserved as fallback and same-run oracle.

This decision accepts allocation readiness for the measured cache-level direct
benchmark surface because milestone 015 reduced the repeated KTLX 2026-05-05
allocation warning materially below the 1.10x threshold while preserving
correctness, release health, retained cleanup, retained pressure budget,
fallback separation, CLI/direct contour alignment, and elapsed timing.

The single-file cold allocation warning is accepted as an expected
retained-ownership cost for the current `queued-owned + pooled-copy`
architecture. It is not a blocker for cache-level readiness, but it is not a
file-level readiness claim. If the next named surface is file-level default
latency/allocation, that cold retained snapshot cost becomes a separate
optimization target or decision.

Live ingestion/runtime defaults, durable queues, cross-process workers,
ordered concurrent rebalance, and builder-transfer remain out of scope.

## Decision Matrix

```text
allocation readiness posture:
  accepted for the measured cache-level direct/default benchmark contours

KTLX 2026-05-05 allocation posture:
  reduced and bounded; two-row average moved from 1.0997x borrowed in
  milestone 014 to 1.0392x borrowed in milestone 015, and both repeated rows
  are below the 1.10x threshold

single-file cold posture:
  accepted as expected retained-ownership cost, not cache-level blocker and
  not file-level readiness proof

standard optimization posture:
  accepted as sufficient together with the adopted experiment for cache-level
  readiness

experimental optimization posture:
  accepted; explicit pooled retained payload release owner and dedicated
  retained event-array pool are adopted because they preserve ownership,
  release, cleanup, correctness, and telemetry guardrails

rejected/deferred optimization posture:
  preserve the rejections and deferrals; do not revisit pooled telemetry,
  struct-backed queue items, or additional source-local probes until a future
  target shows the current attribution is insufficient

fallback/oracle posture:
  accepted; explicit BlockingBorrowed remains selectable and required for
  same-run gate comparisons

CLI/direct contour alignment:
  accepted; CLI omitted-provider rollout contour and direct omitted defaults
  remain aligned to the shared queued-owned rollout contour

broader benchmark expansion posture:
  broader cache-level benchmark/default-readiness work is approved as a next
  input if it keeps same-run BlockingBorrowed oracle rows and carries the
  single-file cold warning as a scope limit

runtime expansion posture:
  not approved; live/runtime defaults remain out of scope for milestone 015
```

## Decision Explanations

### Accept Cache-Level Allocation Readiness

Decision: accept cache-level allocation readiness for direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureCache()` omitted defaults on
the measured local contours.

Why chosen: the Release gate passed correctness, validation, release health,
retained cleanup, retained pressure, elapsed timing, and primary variance
thresholds. Primary KTLX 2026-05-04 allocation measured `1.042x` borrowed,
KTLX 2026-05-05 measured `1.0392x` borrowed average, KINX 2026-05-04 measured
`1.042x` borrowed, and mixed-cache measured `1.021x` borrowed. The primary
direct-default timing spread was `1.10%`, below the `7.50%` threshold.

Alternatives: continue optimizing before making any readiness decision, roll
back queued-owned direct defaults, raise thresholds, or accept readiness only
after a clean file-level result.

Rejected because: continuing optimization without a named blocker would
ignore the gate evidence; rollback would over-weight a resolved cache-level
allocation warning; raising thresholds is prohibited after measurement; and
requiring file-level cleanliness would conflate a one-batch cold ownership cost
with cache-level readiness.

Trade-offs/debt: readiness is scoped to cache-level benchmark surfaces and the
locally measured NEXRAD shapes. It does not certify every input shape, every
radar/date corpus, file-level cold behavior, or runtime ingestion.

Review explanation: "Cache-level allocation readiness is accepted because the
named risk shape moved well below threshold while all safety gates stayed
green."

### Bound KTLX 2026-05-05 Allocation Risk

Decision: treat the KTLX 2026-05-05 allocation warning as reduced and bounded
for cache-level readiness.

Why chosen: milestone 014 recorded KTLX 2026-05-05 at `1.0997x` borrowed on
average, with one row at `1.1018x` and one at `1.0976x`. Milestone 015
recorded the same named risk contour at `1.0392x` borrowed on average, with
rows at `1.0404x` and `1.0381x`. Both repeated rows are below the `1.10x`
threshold, and the average moved materially away from the threshold.

Alternatives: keep KTLX 2026-05-05 as an active blocker, call the whole
milestone clean green without naming the old warning, or require more KTLX
2026-05-05 runs before any decision.

Rejected because: the blocker label no longer matches the measured evidence;
calling the gate clean green would hide the history and the remaining
file-level warning; and more runs may be useful later but are not required to
interpret the current two-row repeated risk contour.

Trade-offs/debt: this conclusion is still local to the captured data and
measurement date. Future broader gates should keep KTLX-like high-risk shapes
visible, but they no longer need to treat KTLX 2026-05-05 as the current
allocation blocker.

Review explanation: "The old KTLX warning has been reduced enough to stop
blocking cache-level readiness."

### Accept Single-File Cold Cost As Scope Limit

Decision: accept the representative single-file cold allocation result as an
expected retained-ownership cost and scope limit, not as a cache-level
readiness blocker.

Why chosen: the single-file cold smoke measured `1.512x` borrowed allocation
and `1.072x` borrowed elapsed time, with one retained event-array pool miss
and one retained byte-array pool miss for one retained batch. That is the
current `queued-owned + pooled-copy` architecture paying for the first owned
snapshot. Cache-level contours amortize that cost across many retained
batches and passed allocation thresholds.

Alternatives: block cache-level readiness on the single-file row, add implicit
prewarm, share pools across direct calls without a contract, create a
file-only fast path immediately, or switch file-level defaults back to
borrowed.

Rejected because: blocking cache-level readiness would misclassify an
unamortized one-batch cost; implicit prewarm or shared pools would hide
allocation outside the measured contract unless made explicit; a file-only
fast path is a separate design; and changing file-level defaults would reopen
direct file/cache symmetry without current evidence that cache readiness needs
it.

Trade-offs/debt: file-level default latency/allocation remains a named future
target if that surface matters. Any prewarm, shared long-lived pool,
file-only fast path, or file-level fallback/default change must be handled as
an explicit contract decision, not as a measurement trick.

Review explanation: "The single-file warning is real, expected, and scoped:
it does not block cache readiness, but it would matter for a file-level
default decision."

### Accept Standard And Experimental Optimizations

Decision: accept the milestone 015 standard and experimental allocation
optimizations as the allocation-readiness implementation.

Why chosen: the accepted standard optimizations reduced bounded recent-detail
copying and not-required retained resource delegate allocation without
changing public result contracts. The adopted experiments removed
closure-backed pooled retained payload release ownership and added a dedicated
retained `RadarStreamEvent[]` pool with split event/byte telemetry. The
Release gate shows the retained event-array pool as the dominant accepted
allocation improvement.

Alternatives: revert the experimental retained event-array pool, continue
optimizing retained byte-pool policy, adopt pooled telemetry accumulators,
adopt struct-backed queued work items, or add new allocation probe fields
before deciding.

Rejected because: reverting the event-array pool would discard the measured
KTLX and cache-level allocation improvement; retained byte-pool misses were
too low to justify tuning; pooled telemetry and struct-backed queue items
would widen ownership and contract risk; and current attribution is adequate
for the cache-level decision.

Trade-offs/debt: the processing callback allocation bucket remains large and
global-counter overlap attribution remains coarse. These are acceptable for
the current decision because the cache-level ratios passed and retained pool
telemetry explains the dominant improvement.

Review explanation: "The adopted optimizations are enough for cache-level
readiness; deeper experiments should wait for a named blocker."

### Preserve Rejected And Deferred Optimization Posture

Decision: keep the rejected and deferred optimization decisions from the plan;
do not reopen them during milestone 015 closeout.

Why chosen: the Release gate did not expose a remaining cache-level
allocation blocker. The deferred ideas, including pooled telemetry
accumulators, struct-backed queued work items, and additional source-local
allocation probes, carry broader ownership, mutability, or result-contract
cost than the current evidence requires.

Alternatives: implement all remaining allocation ideas before closeout, add
more attribution fields immediately, or treat the single-file warning as a
reason to change retained pool behavior now.

Rejected because: broadening implementation after the gate would add risk
without a cache-level blocker; extra attribution fields would churn public
contracts; and the single-file cold cost is expected ownership cost rather
than evidence of retained pool churn.

Trade-offs/debt: future milestones may revisit these ideas if they choose
file-level default optimization, a larger mixed-cache gate, runtime ingestion,
or a new contour where current attribution cannot explain allocation movement.

Review explanation: "The remaining experiments stay parked until a future
surface gives them a concrete job."

### Preserve Explicit Borrowed Fallback And Oracle

Decision: preserve explicit `BlockingBorrowed` as fallback and same-run oracle
for direct archive rebalance benchmark calls.

Why chosen: the Release gate depended on same-run explicit borrowed rows to
interpret correctness parity, elapsed time, allocation, and telemetry. The
borrowed path remains the clearest fallback posture and comparison baseline.

Alternatives: remove borrowed comparison, compare only against historical
milestone 014 numbers, silently fall back to borrowed after queued-owned
failure, or treat borrowed as obsolete after cache-level readiness.

Rejected because: removing borrowed would weaken future gates; historical
numbers cannot replace same-input comparison; silent fallback would hide
queued-owned failures; and readiness does not make the fallback/oracle path
obsolete.

Trade-offs/debt: future gates remain heavier because they must keep
borrowed/default pairs. That is intentional because it keeps allocation and
correctness evidence reviewable.

Review explanation: "Borrowed remains the explicit comparison baseline, not a
silent rescue path."

### Keep CLI And Direct Contours Aligned

Decision: keep the CLI omitted-provider rebalance-archive contour and direct
`MeasureFile()`/`MeasureCache()` omitted defaults aligned through the shared
queued-owned rollout contour.

Why chosen: direct default rows, explicit rollout spot-checks, and CLI drift
coverage all point to the same effective contour: queued-owned,
producer-consumer, pooled-copy, async shard transport, workers 4, worker queue
capacity 8, provider queue capacity 8, retained-byte budget `536870912`, and
delay 0.

Alternatives: split CLI and direct defaults, treat file and cache direct
defaults differently, or move CLI provenance concepts into infrastructure
result contracts.

Rejected because: default divergence would make gates and operator behavior
harder to explain; direct file/cache asymmetry is not required by the
cache-level decision; and CLI provenance remains presentation-owned.

Trade-offs/debt: direct API result contracts still do not expose CLI-style
provenance fields. Effective contour fields and tests remain the direct API
source of truth.

Review explanation: "One shared queued-owned rollout contour remains the
benchmark default story."

### Approve Broader Cache-Level Benchmark Input

Decision: approve broader cache-level benchmark/default-readiness work as the
next named expansion input, while keeping same-run borrowed oracle rows and
the single-file cold warning visible.

Why chosen: cache-level safety and allocation gates passed, and the old KTLX
allocation warning moved materially below threshold. The next useful evidence
is broader cache-level coverage, not more untargeted allocation work on the
same local shapes.

Alternatives: move directly to live/runtime defaults, close the milestone with
no next input, target file-level optimization next, or keep optimizing the
current cache contour indefinitely.

Rejected because: live/runtime defaults require different durability,
ordering, recovery, and operator contracts; no-next-input would waste the
readiness result; file-level optimization is valid only if file-level default
latency/allocation is the chosen product surface; and indefinite optimization
would lack a named blocker.

Trade-offs/debt: broader cache-level work must not imply runtime readiness.
It also must preserve the explicit borrowed oracle and must state that
single-file cold allocation remains a separate file-level concern.

Review explanation: "The next expansion can be broader cache benchmarking,
not runtime defaults."

### Keep Live Runtime Expansion Out Of Scope

Decision: do not use milestone 015 to approve live ingestion/runtime
provider defaults, durable queues, cross-process workers, ordered concurrent
rebalance, or builder-transfer.

Why chosen: milestone 015 measured direct archive benchmark allocation
readiness. Runtime ingestion and durable transport need separate contracts for
durability, recovery, operator control, ordering, backpressure, and lifecycle.

Alternatives: treat cache-level allocation readiness as runtime readiness,
start a durable broker design inside milestone 015, or use the direct
benchmark gate to approve runtime defaults.

Rejected because: those alternatives would overstate what was measured and
would collapse separate architecture decisions into an allocation milestone.

Trade-offs/debt: runtime/default work remains future scope. It can use this
decision as one input, but it still needs its own architecture, tests, and
Release gates.

Review explanation: "Milestone 015 certifies cache-level benchmark
allocation readiness; it does not certify runtime ingestion."

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
processing benchmark rebalance-archive omitted-provider path remains aligned
with the same queued-owned rollout contour
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
docs/milestones/015-queued-owned-allocation-readiness.md
docs/milestones/015-queued-owned-allocation-readiness-plan.md
docs/milestones/015-queued-owned-allocation-readiness-performance-gate.md
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

Release gate:

```text
primary repeated contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  three borrowed/default pairs

borderline repeated contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-05 --radar KTLX --max-files 220
  two borrowed/default pairs

broader single-shape contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KINX --max-files 220

mixed-cache contour:
  direct MeasureCache()
  data\nexrad --max-files 1000000

file-level smoke contour:
  direct MeasureFile()
  data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06

explicit rollout spot-check:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  explicit queued-owned rollout controls
```

## Threshold Decisions

Correctness parity: accepted.

```text
validation succeeded across captured rows
failed migrations: 0
stable totals and validation checksums matched by shape
topology versions, accepted moves, skipped decisions, and skipped reason
  counters matched by shape
```

Release health: accepted.

```text
retained payload failed releases: 0 across direct default rows
provider overlap failed releases: 0 across direct default rows
all retained event-array and byte-array pool rents were returned
```

Retained cleanup: accepted.

```text
current pending retained payload bytes at completion: 0
current active retained payload bytes at completion: 0
current combined retained payload bytes at completion: 0
```

Retained pressure budget: accepted.

```text
budget: 536_870_912 bytes
primary KTLX 2026-05-04 high watermark: 48_257_280 bytes, 8.99%
KINX 2026-05-04 high watermark: 48_342_240 bytes, 9.00%
KTLX 2026-05-05 high watermark: 52_676_640 bytes, 9.81%
mixed-cache high watermark: 54_413_280 bytes, 10.14%
```

Elapsed threshold: accepted for cache-level readiness.

```text
threshold: <= 1.00x borrowed on the primary repeated natural matrix
primary KTLX 2026-05-04 elapsed ratio: 0.889x borrowed
KTLX 2026-05-05 elapsed ratio: 0.943x borrowed average
KINX 2026-05-04 elapsed ratio: 0.899x borrowed
mixed-cache elapsed ratio: 0.871x borrowed
file-level cold smoke elapsed ratio: warning, 1.072x borrowed
```

Run spread threshold: accepted.

```text
threshold: candidate spread <= 7.50% of candidate average
primary direct-default timing spread: 1.10%
```

Allocation threshold: accepted for cache-level readiness.

```text
threshold: <= 1.10x borrowed, with KTLX 2026-05-05 explicitly interpreted
primary KTLX 2026-05-04 allocation ratio: 1.042x borrowed
KTLX 2026-05-05 allocation ratio: 1.0392x borrowed average
KTLX 2026-05-05 run 1 allocation ratio: 1.0404x borrowed
KTLX 2026-05-05 run 2 allocation ratio: 1.0381x borrowed
KINX 2026-05-04 allocation ratio: 1.042x borrowed
mixed-cache allocation ratio: 1.021x borrowed
file-level cold smoke allocation ratio: warning, 1.512x borrowed
```

Direct default expansion evidence: accepted.

```text
direct default rows omitted provider, execution, async execution, queue,
overlap, retention, and retained-byte controls
direct default rows resolved to queued-owned, producer-consumer, pooled-copy,
async shard transport, workers 4, worker queue capacity 8, provider queue
capacity 8, retained-byte budget 536870912, and overlap consumer delay 0
explicit queued-owned rollout spot-check matched direct default deterministic
output and contour fields
```

Fallback separation: accepted.

```text
borrowed rows used explicit providerMode: BlockingBorrowed
borrowed rows used async shard transport workers 4 with worker queue capacity 1
borrowed rows reported no provider queue, retention, overlap, or retained
pressure telemetry
direct default rows reported queue, retention, overlap, worker, and retained
pressure telemetry
no row used automatic fallback from direct default queued-owned to borrowed
```

## Allocation Decision

The queued-owned direct/default cache-level allocation profile is ready for
the next broader cache-level benchmark/default-readiness input.

Allocation movement versus milestone 014:

```text
primary KTLX 2026-05-04: 1.071x -> 1.042x borrowed
KTLX 2026-05-05 average: 1.0997x -> 1.0392x borrowed
KINX 2026-05-04: 1.069x -> 1.042x borrowed
mixed cache: 1.066x -> 1.021x borrowed
```

Interpretation:

```text
the named KTLX allocation warning is reduced and bounded for cache-level
readiness
the retained event-array pool is the dominant accepted improvement visible in
the Release gate
the single-file cold cost remains visible, expected, and scoped to file-level
default decisions
standard and accepted experimental optimizations are sufficient for this
decision
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
API gates, allocation follow-up, performance regressions, and rollback
diagnosis.
```

CLI posture:

```text
The CLI omitted-provider rebalance-archive path remains aligned with the same
queued-owned rollout contour.
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
durable queues, brokers, cross-process providers, ordered concurrent rebalance,
and builder-transfer require future milestones
```

## Residual Risks And Limits

```text
single-file cold allocation:
  expected retained-ownership cost for queued-owned pooled-copy; not a
  cache-level blocker, but a future file-level default target if that surface
  matters

local gate only:
  the Release gate used locally available NEXRAD cache shapes on 2026-05-21

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than this natural gate

processing callback allocation attribution:
  remains the largest candidate-side bucket; accepted because cache-level
  ratios passed and retained pool telemetry explains the dominant improvement

mixed-cache worker failure counters:
  mixed-cache rows reported matching worker failed batch/item counters in both
  borrowed and direct default rows while validation still succeeded

no runtime ingestion claim:
  live ingestion, durable queues, brokers, cross-process providers, ordered
  concurrent rebalance, builder-transfer, and runtime defaults remain outside
  milestone 015
```

## Decision

Milestone 015 answers the closeout question with **yes** for cache-level
allocation readiness:

```text
the queued-owned direct/default allocation profile is ready to support the
next broader cache-level benchmark/default-readiness decision
```

Milestone 015 answers **reduced and bounded** for the KTLX 2026-05-05
allocation warning:

```text
the named risk contour moved from 1.0997x borrowed to 1.0392x borrowed, and
both repeated rows are below the 1.10x threshold
```

Milestone 015 answers **yes, with scope limit** for the single-file cold
allocation cost:

```text
the file-level cold warning is expected retained-ownership cost and does not
block cache-level readiness, but file-level default latency/allocation remains
a separate future target if that surface is chosen next
```

Milestone 015 answers **yes** for preserving explicit borrowed fallback and
same-run oracle coverage:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Milestone 015 answers **no** for live/runtime default migration:

```text
live ingestion, durable queues, brokers, cross-process providers, ordered
concurrent rebalance, builder-transfer, and runtime defaults remain out of
scope
```

Recommended next milestone input:

```text
broader cache-level benchmark/default-readiness work with same-run
BlockingBorrowed oracle rows and explicit scope language for the single-file
cold retained-ownership warning; if file-level default latency/allocation is
chosen instead, treat that warning as the named optimization target
```

The milestone can proceed to closeout without additional allocation
optimization on the measured cache-level contours.

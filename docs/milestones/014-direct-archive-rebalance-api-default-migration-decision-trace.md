# Milestone 014 Decision Trace

Date: 2026-05-21

Decision: direct `RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` and
`MeasureCache()` omitted provider/execution/queue/retention defaults are
accepted as the queued-owned rollout contour, with explicit
`BlockingBorrowed` preserved as fallback and same-run oracle.

This decision accepts the direct API default migration because the milestone
014 implementation and Release gate preserved correctness, release health,
retained cleanup, retained pressure budget, fallback separation, CLI/direct
contour alignment, and elapsed timing. The KTLX 2026-05-05 allocation warning
is accepted as a visible direct-migration cost, not as a clean green result and
not as a rollback blocker.

Live ingestion/runtime defaults, durable queues, cross-process workers,
ordered concurrent rebalance, and builder-transfer remain out of scope.

## Decision Matrix

```text
direct API default posture:
  accepted; omitted MeasureFile()/MeasureCache() provider-related arguments
  now resolve to the queued-owned rollout contour

file/cache symmetry:
  accepted; both direct surfaces migrated to the same shared rollout contour

fallback/oracle posture:
  accepted; explicit BlockingBorrowed remains selectable, tested, documented,
  and required for same-run comparison gates

CLI/direct contour alignment:
  accepted; CLI omitted-provider rollout constants and direct omitted defaults
  share the same accepted contour

failure posture:
  accepted; queued-owned direct failures fail closed and do not automatically
  fall back to borrowed success

Release gate posture:
  accepted with allocation warning; correctness, release health, cleanup,
  pressure budget, and elapsed timing passed captured natural rows

KTLX 2026-05-05 allocation posture:
  accepted as warning; two-row average was 1.0997x borrowed, with one row at
  1.1018x and one row at 1.0976x

primary timing variance posture:
  accepted with note; all four primary direct-default rows spread 10.41%
  because of a favorable outlier, while stabilized rows 2-4 spread 0.39% and
  every direct-default row was faster than same-run borrowed

residual allocation posture:
  accepted as debt for direct API migration; recommended next milestone input
  is targeted allocation reduction or allocation-readiness before any
  live/runtime default expansion

broader expansion posture:
  broader benchmark work may be considered only with the same borrowed-oracle
  and warning discipline; live/runtime default expansion is not approved by
  milestone 014
```

## Decision Explanations

### Accept Direct API Default Migration

Decision: accept queued-owned + pooled-copy + producer-consumer as the direct
`MeasureFile()` and `MeasureCache()` omitted-default contour.

Why chosen: focused regression proved file/cache direct omitted defaults,
explicit queued-owned equivalence, explicit blocking-borrowed fallback, and
fail-closed behavior. The Release gate then proved the direct `MeasureCache()`
natural rows against same-run explicit borrowed rows without correctness,
release, cleanup, or pressure failures.

Alternatives: keep direct defaults on blocking-borrowed, defer the migration
until allocation is optimized, migrate only the CLI-facing surface, or require
all direct callers to pass explicit provider options.

Rejected because: keeping borrowed as the omitted direct default would leave
the already-proved rollout contour split between CLI and direct API usage;
deferring solely for allocation would treat a known warning as stronger than
the passing safety gates; the CLI surface already migrated in milestone 012;
and requiring explicit provider options would avoid the migration rather than
resolve it.

Trade-offs/debt: direct source callers recompiled against the current code now
observe the queued-owned rollout contour when provider-related arguments are
omitted. Already-compiled external assemblies may still carry old optional
argument constants; a future stable binary package boundary should consider an
options object or overload-based resolver if that becomes a public contract.

Review explanation: "The direct API now follows the same queued-owned rollout
contour that the CLI already proved, while the old borrowed path remains
explicitly selectable."

### Migrate File And Cache Symmetrically

Decision: treat direct `MeasureFile()` and `MeasureCache()` as one migrated
API posture rather than accepting an asymmetric default.

Why chosen: both methods now resolve omitted provider, execution, async
execution, queue, overlap, retention, and retained-byte controls through the
shared rollout contour. Tests cover both surfaces, and no slice found a
file-only or cache-only blocker.

Alternatives: migrate only `MeasureCache()`, migrate only `MeasureFile()`, or
leave one surface borrowed until a later milestone.

Rejected because: an asymmetric direct default would make benchmark behavior
harder to explain, weaken CLI/direct alignment, and force future gates to
carry a compatibility split that the current tests did not require.

Trade-offs/debt: the natural Release gate used direct `MeasureCache()` rows.
`MeasureFile()` natural behavior is covered by focused direct API regression
tests rather than separate large-file gate rows.

Review explanation: "File and cache direct callers get one default story:
omitted direct controls mean the accepted queued-owned rollout contour."

### Preserve Explicit Borrowed Fallback And Oracle

Decision: preserve explicit `BlockingBorrowed` as fallback and same-run oracle
for direct archive rebalance benchmark calls.

Why chosen: same-run borrowed rows are still the clearest way to interpret
correctness parity, elapsed time, allocation, telemetry differences, and
rollback posture. The direct default migration does not make borrowed behavior
obsolete; it changes what omitted arguments select.

Alternatives: remove borrowed coverage, keep borrowed only through old
compiled call sites, replace same-run comparison with historical gate numbers,
or silently retry borrowed after queued-owned failure.

Rejected because: removing borrowed coverage would make regressions harder to
classify; compiled-call-site compatibility is not an operator model;
historical numbers are weaker than same-input comparison; and silent fallback
would hide provider-specific failure behavior.

Trade-offs/debt: future gates remain heavier because they must keep
borrowed/default pairs. That cost is intentional because it keeps migration
evidence reviewable.

Review explanation: "Borrowed is no longer the omitted direct default, but it
remains the explicit comparison baseline."

### Accept KTLX 2026-05-05 Allocation Warning

Decision: accept the repeated KTLX 2026-05-05 allocation warning as a visible
direct API migration cost.

Why chosen: the warning is allocation-only. The same shape passed correctness,
validation, release health, retained cleanup, retained pressure budget, and
elapsed timing. The two-row direct gate average was `1.0997x` borrowed, with
one row over and one row under the `1.10x` allocation threshold.

Alternatives: block the direct API migration until every KTLX 2026-05-05 row
is below threshold, roll back the direct default migration, raise the
threshold after seeing the result, or call the gate clean green because the
average is below threshold.

Rejected because: blocking or rollback would over-weight a narrow allocation
warning relative to the passing safety gates; raising the threshold after
measurement would weaken the gate; and calling the result clean green would
hide the exact threshold tension carried from milestone 013.

Trade-offs/debt: allocation optimization remains debt. The next milestone
should target allocation reduction or allocation-readiness before any
live/runtime default expansion.

Review explanation: "The allocation warning is accepted, but it stays named:
KTLX 2026-05-05 is not a clean green row."

### Keep CLI And Direct Contours Aligned

Decision: keep the CLI omitted-provider rollout contour and direct omitted
default contour aligned through the shared rollout contract.

Why chosen: milestone 012 moved the scoped CLI omitted-provider path, milestone
013 hardened it, and milestone 014 moved the direct API to the same contour.
One shared contour avoids drift between operator commands, direct benchmark
calls, tests, and documentation.

Alternatives: let CLI and direct defaults diverge, duplicate literals in
presentation and infrastructure, or make infrastructure depend on CLI
provenance.

Rejected because: divergent defaults would confuse operators and benchmarks;
duplicated literals are drift-prone; and CLI provenance is presentation-owned,
not infrastructure policy.

Trade-offs/debt: the direct API does not have CLI-style provenance fields.
Result fields and tests remain the source of truth for direct effective
contour evidence.

Review explanation: "CLI provenance stays in the CLI, but the actual rollout
contour is one shared policy."

### Keep Live Runtime Expansion Out Of Scope

Decision: do not use milestone 014 to approve live/runtime provider defaults,
durable queues, cross-process workers, ordered concurrent rebalance, or
builder-transfer.

Why chosen: milestone 014 is a direct benchmark API compatibility migration.
Runtime ingestion and durable transport need different durability, operator,
failure, ordering, and recovery contracts.

Alternatives: broaden immediately into live ingestion defaults, add durable
transport as part of the direct migration, or treat the benchmark direct API
gate as sufficient runtime readiness.

Rejected because: those alternatives would collapse separate architecture
decisions into a benchmark default migration and would overstate what the
Release gate measured.

Trade-offs/debt: a future runtime/default milestone remains necessary if the
project wants queued-owned outside benchmark APIs.

Review explanation: "Milestone 014 answers the direct benchmark API question;
it does not certify runtime ingestion."

### Recommend Allocation Work Before Runtime Expansion

Decision: carry residual allocation as the recommended next milestone input
before any live/runtime default expansion.

Why chosen: direct migration is accepted, but the KTLX 2026-05-05 warning
repeated across milestones 013 and 014. Targeted allocation reduction is the
most concrete next blocker to remove before expanding the provider posture
beyond benchmark APIs.

Alternatives: proceed directly to live/runtime default migration, treat
allocation as indefinite debt, or start a broad benchmark rollout without a
clear allocation objective.

Rejected because: runtime expansion would need stronger cost confidence than a
benchmark direct API migration; indefinite debt would normalize a repeated
threshold warning; and a broad benchmark rollout without allocation objectives
would risk repeating the same warning without reducing it.

Trade-offs/debt: a future milestone may still choose broader benchmark
readiness, but it should carry explicit borrowed oracle rows and the KTLX
allocation warning until the allocation cost is reduced or deliberately
accepted for that broader surface.

Review explanation: "The direct API can move now; the next serious expansion
should address the repeated allocation warning."

## Included Surface

Included direct API defaults:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  omitted providerMode
  omitted executionMode
  omitted asyncExecution
  omitted queueCapacity
  omitted providerOverlapMode
  omitted retentionStrategy
  omitted queueRetainedPayloadBytes
  omitted overlapConsumerDelay

RadarProcessingArchiveRebalanceBenchmark.MeasureCache()
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
processing benchmark rebalance-archive omitted-provider path remains the
queued-owned rollout contour accepted in milestone 012 and hardened in
milestone 013
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
complex radar algorithms or visualization
```

## Evidence

Primary source documents:

```text
docs/milestones/014-direct-archive-rebalance-api-default-migration.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-plan.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
```

Focused regression before the gate:

```text
direct MeasureFile()/MeasureCache() default migration tests passed
explicit blocking-borrowed fallback/oracle tests passed
explicit queued-owned equivalence tests passed
CLI help and rollout contour alignment tests passed
queued-owned failure, cancellation, cleanup, and fallback tests passed
readiness threshold interpretation tests passed
allocation summary attribution tests passed

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests|FullyQualifiedName~RadarProcessingRebalanceAllocationSummaryTests"

84 passed, 0 failed, 0 skipped
```

Release gate:

```text
Release build succeeded with 0 warnings and 0 errors.

primary repeated contour:
  direct MeasureCache()
  data\nexrad --date 2026-05-04 --radar KTLX --max-files 220
  four borrowed/default pairs

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

Elapsed threshold: accepted.

```text
threshold: <= 1.00x borrowed on the primary repeated natural matrix
primary KTLX 2026-05-04 elapsed ratio: 0.911x borrowed
KTLX 2026-05-05 elapsed ratio: 0.960x borrowed average
KINX 2026-05-04 elapsed ratio: 0.906x borrowed
mixed-cache elapsed ratio: 0.878x borrowed
```

Run spread threshold: accepted with favorable outlier note.

```text
threshold: candidate spread <= 7.50% of candidate average
all four primary direct-default rows: 10.41% spread
stabilized primary direct-default rows 2-4: 0.39% spread
interpretation: the all-row miss is caused by a favorable first-row timing
outlier; every direct-default row was faster than same-run borrowed
```

Allocation threshold: accepted for direct API migration with warning.

```text
threshold: <= 1.10x borrowed, with KTLX 2026-05-05 warning explicitly named
primary KTLX 2026-05-04 allocation ratio: 1.071x borrowed
KINX 2026-05-04 allocation ratio: 1.069x borrowed
mixed-cache allocation ratio: 1.066x borrowed
KTLX 2026-05-05 run 1 allocation ratio: 1.1018x borrowed
KTLX 2026-05-05 run 2 allocation ratio: 1.0976x borrowed
KTLX 2026-05-05 two-run average allocation ratio: 1.0997x borrowed
decision: accepted as explicit warning, not clean green
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

The residual allocation overhead is acceptable for direct API default migration
and should become the next concrete risk-reduction target before runtime
default expansion.

Primary attribution:

```text
borrowed average processing callback allocated bytes: 262_906_694
borrowed average replay and batch construction allocated bytes: 1_712_719_592

direct default average processing callback allocated bytes: 1_841_523_536
direct default average replay and batch construction allocated bytes:
  274_673_124
direct default average owned snapshot allocated bytes: 125_881_832
direct default average processing callback non-owned snapshot bytes:
  1_715_641_704
```

Interpretation:

```text
direct default allocation overhead is concentrated in processing callback
allocation and retained/owned snapshot work
the cost profile matches the known queued-owned retained-payload behavior
KTLX 2026-05-05 remains the allocation-risk shape
the migration proceeds, but the repeated allocation warning should be the next
optimization/readiness input before live/runtime expansion
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
Same-run BlockingBorrowed rows remain the comparison oracle for direct API
gates, allocation follow-up, performance regressions, and rollback diagnosis.
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
KTLX 2026-05-05 allocation warning:
  the direct gate averaged 1.0997x borrowed with one row above the 1.10x
  threshold; this is accepted for direct API migration and should be targeted
  before runtime expansion

primary favorable timing outlier:
  all-row timing spread was 10.41%, while stabilized rows 2-4 were 0.39%;
  this is not a slowdown blocker, but the variance note remains part of the
  record

local gate only:
  the Release gate used locally available NEXRAD cache shapes on 2026-05-21

cache natural gate:
  direct MeasureCache() received natural Release gate rows; direct
  MeasureFile() is covered by focused regression tests

natural queue depth:
  natural direct default rows kept queue depth at 1; queue-ahead mechanics
  remain covered by controlled tests rather than this natural gate

mixed-cache worker failure counters:
  mixed-cache rows reported matching worker failed batch counters in both
  borrowed and direct default rows while validation still succeeded

optional parameter compatibility:
  source callers recompiled after this milestone observe the new direct
  omitted defaults; already-compiled external assemblies may retain old
  optional argument constants

no runtime ingestion claim:
  live ingestion, durable queues, brokers, cross-process providers, and runtime
  defaults remain outside milestone 014
```

## Decision

Milestone 014 answers the closeout question with **yes** for direct archive
rebalance benchmark API defaults:

```text
direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and
MeasureCache() omitted defaults migrate symmetrically to the accepted
queued-owned rollout contour
```

Milestone 014 answers **yes** for preserving explicit borrowed fallback and
same-run oracle coverage:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

Milestone 014 answers **yes, with warning** for accepting the KTLX 2026-05-05
allocation cost:

```text
the direct API migration proceeds, but KTLX 2026-05-05 remains an explicit
allocation warning and should be carried into the next allocation/readiness
milestone
```

Milestone 014 answers **no** for live/runtime default migration:

```text
live ingestion, durable queues, brokers, cross-process providers, ordered
concurrent rebalance, builder-transfer, and runtime defaults remain out of
scope
```

Recommended next milestone input:

```text
targeted allocation reduction or allocation-readiness for the queued-owned
direct/default contour before any live/runtime default expansion; if broader
benchmark expansion is chosen first, it must keep same-run BlockingBorrowed
oracle rows and the KTLX 2026-05-05 warning visible
```

The milestone can proceed to closeout without reverting the direct default
migration. The allocation warning remains tracked debt, not a blocker to the
direct API default decision.

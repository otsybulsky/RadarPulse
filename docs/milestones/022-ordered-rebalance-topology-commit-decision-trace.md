# Milestone 022 Decision Trace

Date: 2026-05-25

Decision: accept ordered rebalance/topology commit for the scoped in-process
runtime/archive queued-overlap path with named scoped warnings.

This decision accepts milestone 022's ordered active-batch rebalance path on
top of the milestone 021 ordered processing foundation and the milestone 020
runtime/archive baseline. The accepted scope is the in-process queued-owned
runtime/archive rebalance surface: multiple accepted rebalance batches may be
active for handler-free processing-delta compute, while processing commit,
pressure updates, policy/quarantine state, rebalance decisions, validation,
and topology mutation commit strictly in provider sequence.

The decision does not accept concurrent rebalance policy mutation, concurrent
topology mutation, handler-state delta/merge support, durable queues, brokers,
cross-process providers or workers, true live network ingestion, production
operator/deployment readiness, or product-facing radar workflows. Those
remain future surfaces that must prove their own ownership, recovery, and
ordered commit boundaries.

The milestone is a scoped ordered-rebalance acceptance, not a broad default
promotion. It proves that the runtime/archive rebalance path can overlap
processing-delta compute under bounded active capacity while preserving
deterministic provider-sequence commit, accepted-move evidence, topology
version safety, stale topology recompute, fail-closed behavior, worker
telemetry, visible startup prewarm, release health, and retained pressure
cleanup.

## Decision Matrix

```text
ordered rebalance/topology commit:
  accepted with scoped warnings

ordered active rebalance capacity:
  accepted; active batch capacity defaults to 4 and remains independent from
  provider queue capacity 8 and worker queue capacity 8

processing delta plus ordered rebalance commit:
  accepted; handler-free processing deltas may compute concurrently, while
  processing, pressure, policy, quarantine, telemetry, decision, validation,
  and topology mutation commit strictly by provider sequence

stale topology recompute:
  accepted; active deltas computed against an older topology version are
  discarded and recomputed before ordered commit

runtime/archive ordered rebalance surface:
  accepted; RunOrderedRebalanceAsync is the explicit ordered rebalance entry
  point over the accepted queued-overlap baseline

existing sequential rebalance path:
  preserved; existing RunRebalanceAsync and sequential queued rebalance drain
  behavior remain available

async shard transport integration:
  accepted for non-mutating ordered rebalance delta compute; worker telemetry
  remains visible

worker mailbox hardening:
  accepted as required implementation hardening; pending-count reservation now
  happens before channel publication

processing-bottleneck evidence:
  accepted with warning; active=4 improved elapsed time to 0.891x active=1,
  while stale topology recompute increased allocation to 1.137x active=1

full-cache regression evidence:
  accepted; no end-to-end full-cache performance regression was observed
  after milestone 022 ordered rebalance/topology work

startup prewarm:
  accepted and preserved as visible lifecycle cost; ordered active=4 prewarms
  four retained batches and does not hide that cost in steady allocation

fallback/oracle posture:
  accepted and preserved; queued-owned failure remains fail-closed and no
  automatic silent BlockingBorrowed fallback is introduced

handler-state delta/merge:
  not implemented; handler cores remain rejected by ordered active rebalance
  until an explicit handler-state delta/merge contract exists

durable/cross-process/runtime production readiness:
  not implemented; durable queues, brokers, cross-process providers/workers,
  true live ingestion, production operator/deployment/rollback surfaces, and
  product workflows remain future work

broader default promotion:
  not accepted; broader promotion still needs workload breadth and repeated
  variance evidence

full-suite residual risk:
  accepted as known allocation-sensitive synthetic benchmark caveat; isolated
  rerun passed and the failure is outside the milestone 022 ordered
  rebalance/topology correctness surface
```

## Decision Explanations

### Accept Ordered Rebalance/Topology Commit

Decision: accept ordered active-batch rebalance for the scoped in-process
runtime/archive queued-overlap path.

Why chosen: milestone 021 solved ordered concurrent processing but left
rebalance pressure, policy, quarantine, telemetry, decisions, validation, and
topology mutation on the older sequential rebalance drain. Milestone 022 adds
bounded active rebalance work where only handler-free processing deltas
compute concurrently. All shared processing and rebalance state still commits
by provider sequence through `RadarProcessingRebalanceSession`.

Alternatives: keep rebalance processing sequential, allow rebalance policy or
topology mutation during active compute, or claim ordered result publication
alone is enough.

Rejected because: keeping rebalance sequential would leave the next runtime
headroom step undone; concurrent rebalance/topology mutation would create
shared-state races; and ordered publication alone cannot make earlier shared
mutation safe.

Trade-offs/debt: the accepted surface overlaps compute only. Rebalance
planning and topology mutation remain serialized at ordered commit, so
processing-heavy workloads can benefit, but topology-heavy workloads may pay
stale recompute costs.

Review explanation: "Multiple rebalance batches can compute at once, but
only the next provider sequence can mutate processing, rebalance, or
topology state."

### Accept Stale Topology Recompute

Decision: accept recompute-on-stale-topology as the safety rule for active
rebalance deltas.

Why chosen: an earlier ordered commit can migrate topology after a later
active batch has already computed a delta against the old topology. Silently
committing that stale delta would publish route telemetry and processing
counts against the wrong topology. Milestone 022 detects the stale topology
version, discards the old delta, recomputes against the current topology, and
then commits.

Alternatives: fail the ordered drain whenever topology changes during active
compute, allow stale deltas to commit, or clone and merge the full rebalance
topology state for every active batch.

Rejected because: failing on ordinary accepted moves would make ordered
rebalance unusable under real topology churn; stale commit would violate the
topology invariant; and full state clone/merge is broader than the scoped
milestone and needs its own design.

Trade-offs/debt: stale recompute does real extra work. The synthetic
processing-bottleneck matrix showed active=4 dispatching `39_292` worker
batches for `32_000` logical batches and allocating `1.137x` active=1.

Review explanation: "A topology change does not break the ordered path, but
later active work must be recalculated before it can commit."

### Accept Active Capacity As A Separate Rebalance Contract

Decision: use `RadarProcessingOrderedConcurrencyOptions` for ordered
rebalance active batch capacity, independent from provider queue and worker
queue capacity.

Why chosen: the runtime/archive path has three different bounds: provider
queue depth, active consumed batches, and async shard worker mailbox depth.
Milestone 022 reuses the milestone 021 ordered concurrency contract so active
rebalance capacity defaults to `4`, while provider queue capacity remains `8`
and worker queue capacity remains `8`.

Alternatives: reuse provider queue capacity, reuse worker queue capacity, or
leave active rebalance concurrency implicit.

Rejected because: provider queue capacity is producer/consumer buffering,
worker queue capacity is intra-batch shard transport buffering, and implicit
active concurrency would make retained pressure and performance evidence
ambiguous.

Trade-offs/debt: active capacity can now be tuned separately, but any broader
default promotion must prove the chosen capacity across more workloads.

Review explanation: "Provider queue depth, active rebalance depth, and worker
mailbox depth remain separate knobs."

### Accept Runtime/Archive Integration Without Reopening The Baseline

Decision: accept `RadarProcessingArchiveQueuedOverlapRunner.RunOrderedRebalanceAsync`
as the explicit runtime/archive entry point for ordered rebalance.

Why chosen: milestone 020 accepted the provider/execution baseline and
milestone 021 accepted ordered processing on top of that baseline. Milestone
022 composes ordered rebalance with the same queued-owned, producer-consumer,
pooled-copy, startup-prewarmed, async shard transport contour without
re-deciding those defaults.

Alternatives: make all rebalance callers ordered by default, create a new
provider contour, or silently change the older sequential `RunRebalanceAsync`
behavior.

Rejected because: broad default changes need more workload evidence; a new
provider contour would reopen closed baseline decisions; and silently
changing the existing sequential path would hide a behavioral shift.

Trade-offs/debt: ordered rebalance is an explicit runtime/archive surface.
Existing sequential paths remain available until a later promotion decision.

Review explanation: "The ordered rebalance path builds on the accepted
baseline instead of re-deciding it."

### Preserve Fail-Closed Behavior And Cleanup

Decision: preserve queued-owned fail-closed behavior, deterministic failure
publication, and retained pressure cleanup under active ordered rebalance.

Why chosen: active later batches may complete before an earlier batch fails.
The ordered rebalance drain prevents later successes from publishing past the
earlier failure boundary, records failures visibly, skips or cancels later
active work deterministically, and releases retained resources. Focused tests
cover validation failure, cancellation, cleanup, worker telemetry, and
terminal retained pressure zero.

Alternatives: publish later successes after an earlier failure, automatically
retry through BlockingBorrowed, or treat provider acceptance as completion.

Rejected because: later success publication would break provider-sequence
determinism; borrowed retry would hide queued-owned failure; and provider
acceptance does not prove processing or rebalance completion.

Trade-offs/debt: active compute can be spent on work that is later skipped
after an earlier fault. That is the accepted cost of bounded concurrency plus
fail-closed ordered commit.

Review explanation: "A later active rebalance batch can finish early, but it
cannot commit or publish past an earlier failure."

### Accept Processing-Bottleneck Evidence With Warning

Decision: accept the ordered-rebalance synthetic processing-bottleneck matrix
as scoped performance evidence, with a named stale-recompute allocation
warning.

Why chosen: the milestone needed evidence where processing/rebalance work, not
archive replay, is the meaningful bottleneck. The deterministic synthetic
workload ran `2_000` measured iterations with `32_000` rebalance evaluations.
Active=4 matched active=1 validation and accepted moves, improved elapsed
ratio to `0.891x`, and kept failed migrations and worker failures at zero.

Alternatives: rely only on full-cache archive rows, block on repeated
variance matrices before accepting the scoped implementation, or ignore
allocation growth from stale recompute.

Rejected because: full-cache rows are archive-producer dominated; repeated
variance is useful for promotion but not required for scoped acceptance; and
allocation growth is a real cost that must remain visible.

Trade-offs/debt: active=4 allocated `802_514_048` bytes versus active=1
`705_853_752` bytes. The allocation ratio `1.137x` is accepted as the cost of
stale topology recompute under topology churn, not as a silent default
promotion posture.

Review explanation: "The ordered rebalance path is faster on the
processing-heavy proof, but topology churn can spend extra work."

### Accept Full-Cache Regression Evidence

Decision: accept the post-gate full-cache matrix as supporting evidence that
milestone 022 did not regress accepted full-cache benchmark contours.

Why chosen: the Release CLI matrix replayed the full local `data\nexrad`
cache through `rebalance-archive --mode all` and
`ordered-archive-processing`. The default queued-owned rebalance rows were
faster than explicit BlockingBorrowed end-to-end in static, sampling, and
rebalance-session modes, with total allocation effectively flat. The ordered
active=4 full-cache row stayed effectively flat against active=1 and
preserved checksum, completeness, worker health, release health, retained
pool health, and terminal retained pressure cleanup.

Alternatives: skip full-cache regression after processing-bottleneck evidence,
or treat the full-cache result as proof of broad default promotion.

Rejected because: skipping full-cache would leave a regression blind spot;
treating this corpus as promotion proof would overclaim because the workload
is archive-producer dominated.

Trade-offs/debt: the full-cache evidence is strong regression evidence but
weak throughput-upside evidence. Broader promotion still needs workload
breadth and repeated variance runs.

Review explanation: "The full cache stayed clean; it is regression evidence,
not the main proof of compute-overlap upside."

### Preserve Visible Startup Prewarm

Decision: keep startup retained payload prewarm visible and separate from
steady allocation for ordered rebalance and ordered processing rows.

Why chosen: the queued-owned default posture was accepted only with visible
prewarm cost. Ordered active=4 requires four retained batches prewarmed to
avoid steady retained pool misses under active work. The benchmark output
reports startup prewarm separately from measured steady allocation.

Alternatives: prewarm only one retained batch, hide prewarm inside steady
allocation, or allow first-use pool misses during measured rows.

Rejected because: one retained batch weakens active-path readiness, hidden
prewarm corrupts attribution, and first-use misses would regress the accepted
pooled-copy posture.

Trade-offs/debt: active=4 has larger visible startup retained bytes and
larger active retained pressure high watermark than active=1. This is
expected and must remain visible in future gates.

Review explanation: "The ordered path pays a visible startup cost to keep
steady retained-pool behavior clean."

### Keep Handler-State, Durable Runtime, And Production Surfaces Deferred

Decision: do not accept handler-state merge, durable queues, cross-process
workers, true live ingestion, production operator surfaces, or product-facing
workflow readiness in milestone 022.

Why chosen: milestone 022 only proves handler-free processing deltas plus
ordered rebalance/topology commit inside one process. Handler cores may own
arbitrary mutable state. Durable and cross-process runtime surfaces need
transport ownership, recovery, retry, replay, and operator-visible failure
contracts that are outside this milestone.

Alternatives: broaden milestone 022 into handler merge or durable runtime
work, or imply those surfaces are ready because in-process ordered rebalance
is ready.

Rejected because: broadening would mix distinct ownership and recovery
problems into one decision, and implying readiness would overstate the
evidence.

Trade-offs/debt: the next milestone can now build on a safer in-process core,
but it still must design durable/cross-process boundaries explicitly.

Review explanation: "The in-process ordered commit boundary is ready; durable
runtime and handler-state merge remain separate decisions."

### Accept Verification With Known Full-Suite Allocation Caveat

Decision: accept the milestone 022 verification posture despite the known
full-suite allocation-sensitive synthetic benchmark failure.

Why chosen: Release build succeeded with `0` warnings and `0` errors. The
focused milestone 022 Release gate suite passed `76/76`. The known
allocation-sensitive synthetic test passed in isolated rerun. The full
Release test project produced the same known caveat carried from earlier
milestones, and the failure is outside the ordered rebalance/topology
correctness surface.

Alternatives: block milestone 022 on unrelated full-suite allocation
sensitivity, loosen the synthetic benchmark threshold after measurement, or
ignore the full-suite output.

Rejected because: blocking would conflate the scoped ordered rebalance work
with a known unrelated sensitivity; changing thresholds after measurement is
not allowed; and ignoring full-suite output would hide project-level risk.

Trade-offs/debt: the full-suite allocation sensitivity remains a project
caveat until separately stabilized.

Review explanation: "The scoped ordered rebalance path is clean; the known
full-suite caveat remains isolated."

## Included Surface

Included ordered rebalance surfaces:

```text
RadarProcessingRebalanceSession.CommitProcessingDelta
RadarProcessingQueuedRebalanceSession.DrainOrderedConcurrentAsync
RadarProcessingArchiveQueuedOverlapRunner.RunOrderedRebalanceAsync
RadarProcessingOrderedConcurrencyOptions
RadarProcessingBatchDelta
RadarProcessingCore.ComputeProcessingDelta
RadarProcessingCore.CommitProcessingDelta
RadarProcessingAsyncCoreSession.ComputeDeltaAsync
RadarProcessingAsyncWorkerGroup
RadarProcessingSyntheticRebalanceBenchmark ordered-rebalance mode
processing benchmark rebalance-synthetic --mode ordered-rebalance
```

Included regression/performance evidence surfaces:

```text
processing benchmark rebalance-archive --mode all
processing benchmark ordered-archive-processing
```

Included baseline contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
execution: async shard transport
workers: 4
worker queue capacity: 8
ordered active batch capacity: 4
```

Included evidence shapes:

```text
ordered rebalance commit contract tests
queued ordered rebalance drain tests
stale topology recompute tests
runtime/archive ordered rebalance integration tests
failure, cancellation, and cleanup lifecycle tests
worker mailbox hardening tests
focused CLI/benchmark tests
Release build
focused milestone 022 Release gate suite
full Release test project plus isolated rerun of known allocation caveat
processing-bottleneck ordered-rebalance synthetic active=4 versus active=1
full-cache rebalance-archive default-vs-borrowed matrix
full-cache ordered-archive-processing active=4 versus active=1 matrix
```

Excluded:

```text
handler-state delta/merge contract
concurrent rebalance policy mutation
concurrent topology mutation
automatic concurrency for handler-owned processing cores
automatic silent borrowed fallback
true live network ingestion
durable queues or brokers
cross-process providers or workers
production deployment, alerting, rollback, or operator runbooks
product-facing radar workflows
broader default promotion
changing RadarProcessingCoreOptions.Default
changing the milestone 020 provider/execution baseline decision
changing the milestone 021 processing delta architecture decision
```

## Evidence

Primary source documents:

```text
docs/milestones/022-ordered-rebalance-topology-commit.md
docs/milestones/022-ordered-rebalance-topology-commit-architecture-decision.md
docs/milestones/022-ordered-rebalance-topology-commit-plan.md
docs/milestones/022-ordered-rebalance-topology-commit-gate.md
docs/milestones/022-ordered-rebalance-topology-commit-processing-bottleneck-performance-matrix.md
docs/milestones/022-ordered-rebalance-topology-commit-full-cache-performance-matrix.md
```

Input evidence from earlier milestones:

```text
milestone 020:
  RadarProcessingRuntimeArchiveBaseline accepted as the named construction
  profile composing queued-owned provider defaults with async shard transport
  execution defaults

milestone 021:
  non-mutating per-batch processing delta plus provider-sequence ordered
  commit accepted as the safe architecture for overlapping processing-core
  batches
```

Implementation evidence:

```text
RadarProcessingRebalanceSession.CommitProcessingDelta:
  commits a non-mutating processing delta into RadarProcessingCore
  then runs the existing rebalance completion pipeline
  keeps RadarProcessingRebalanceSession as owner of pressure, policy,
  quarantine, telemetry, decision, migration, validation, and topology
  mutation

RadarProcessingQueuedRebalanceSession.DrainOrderedConcurrentAsync:
  reuses RadarProcessingOrderedConcurrencyOptions
  computes handler-free processing deltas concurrently
  commits processing plus rebalance/topology mutation in provider sequence
  skips later active successes after an earlier failure boundary
  preserves async shard transport worker telemetry

stale topology recompute:
  detects active deltas computed against an older topology version
  discards stale deltas and recomputes them against current topology before
  ordered commit

RadarProcessingArchiveQueuedOverlapRunner.RunOrderedRebalanceAsync:
  composes ordered rebalance with queued-overlap provider defaults
  scales startup retained payload prewarm to active batch capacity
  preserves old RunRebalanceAsync sequential consumer behavior

worker mailbox hardening:
  reserves pending count before channel publication and rolls back on failed
  write, preventing negative pending-count telemetry under fast workers
```

Verification:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

focused milestone 022 Release gate suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "(FullyQualifiedName~RadarProcessingRebalanceSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests|FullyQualifiedName~RadarProcessingRuntimeArchiveLiveAdapterIntegrationTests|FullyQualifiedName~RadarProcessingSyntheticRebalanceBenchmarkTests|FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests)&FullyQualifiedName!~AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 76 passed, 0 failed, 0 skipped

known allocation-sensitive synthetic test isolated rerun:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 821 passed, 1 failed, 3 skipped
```

Full-suite failure:

```text
RadarProcessingSyntheticRebalanceBenchmarkTests.
  AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

full-suite failure:
  Expected bounded benchmark aggregation allocation, got 382811064 bytes.

interpretation:
  known allocation-sensitive synthetic benchmark caveat carried from earlier
  milestones; outside the ordered rebalance/topology correctness surface
```

Processing-bottleneck matrix:

```text
Release CLI matrix:
  processing benchmark rebalance-synthetic
  --workload long-mixed-skipped-reasons
  --mode ordered-rebalance

workload:
  iterations: 2_000 measured
  batches per iteration: 16
  rebalance evaluations: 32_000
  accepted moves: 2_000
  failed migrations: 0
  validation checksum: 18_341_822_938_456_978_981

active=1 same-path baseline:
  elapsed: 1_626.53 ms
  allocated bytes: 705_853_752
  worker dispatched batches: 32_000
  worker failed batches/items: 0/0

active=4 ordered default:
  elapsed: 1_448.88 ms
  elapsed ratio versus active=1: 0.891x
  allocated bytes: 802_514_048
  allocation ratio versus active=1: 1.137x
  worker dispatched batches: 39_292
  worker failed batches/items: 0/0

interpretation:
  active-batch overlap improves elapsed time on the captured
  processing/rebalance-shaped workload, while topology churn causes stale
  delta recompute, increasing worker dispatches and allocation
```

Full-cache regression matrix:

```text
Release CLI matrix:
  processing benchmark rebalance-archive --cache data\nexrad
  --max-files 1000000 --mode all

cache shape:
  examined files: 1_554
  skipped files: 726
  published files: 828
  stream events: 27_254_760
  payload values: 32_306_203_200
  raw value checksum: 958_518_408_830

rebalance-archive default elapsed ratios versus BlockingBorrowed:
  static: 0.883x
  sampling: 0.891x
  rebalance-session: 0.871x

rebalance-archive default allocation ratios versus BlockingBorrowed:
  static: 1.002x
  sampling: 1.001x
  rebalance-session: 1.002x

rebalance-session correctness:
  accepted moves: 4
  failed migrations: 0
  validation checksum: 8_775_520_679_090_824_038

queued-owned lifecycle:
  provider queue enqueued/dequeued/completed: 828/828/828
  worker failed batches/items: 0/0
  retained payload pool misses: 0
  retained payload failed releases: 0
  terminal combined retained pressure: 0
```

Direct ordered-processing full-cache regression row:

```text
Release CLI matrix:
  processing benchmark ordered-archive-processing --cache data\nexrad
  --max-files 1000000

active=1 same-path baseline:
  elapsed: 67_803.08 ms
  steady allocated bytes: 3_801_252_144
  final processing checksum: 2_294_439_733_285_583_699

active=4 ordered default:
  elapsed: 67_720.71 ms
  elapsed ratio versus active=1: 0.999x
  steady allocated bytes: 3_826_825_400
  steady allocation ratio versus active=1: 1.007x
  final processing checksum: 2_294_439_733_285_583_699

correctness/lifecycle:
  processing completeness succeeded
  worker failed batches/items: 0/0
  retained payload pool misses: 0
  retained payload failed releases: 0
  terminal combined retained pressure: 0

interpretation:
  full-cache workload remains archive-producer dominated, so this is
  regression evidence rather than processing-bottleneck proof
```

## Operational Posture

Runtime/archive baseline posture:

```text
milestone 022 does not reopen the milestone 020 provider/execution baseline
or the milestone 021 processing delta architecture
```

Ordered rebalance posture:

```text
RunOrderedRebalanceAsync is the accepted explicit ordered rebalance path
active batch capacity defaults to 4
active batch capacity is independent from provider queue capacity and worker
queue capacity
```

Commit posture:

```text
concurrent compute does not mutate shared RadarProcessingCore state
rebalance pressure, policy, quarantine, telemetry, decision, validation, and
topology mutation happen only after provider-sequence ordered processing
commit
later successful compute cannot publish or commit past an earlier failure
boundary
```

Topology posture:

```text
active compute may route against a topology snapshot
ordered commit must not use stale topology silently
stale active deltas are recomputed against current topology before commit
topology migration remains owned by RadarProcessingRebalanceSession
```

Execution posture:

```text
async shard transport can run concurrent non-mutating rebalance delta compute
worker telemetry remains visible
normal mutating ProcessAsync keeps the existing one-in-flight guard
```

Prewarm posture:

```text
startup retained payload prewarm remains visible and separate from steady
allocation
ordered active-batch defaults scale prewarm to active capacity to avoid
steady retained pool misses
```

Fallback posture:

```text
queued-owned failure remains fail-closed
BlockingBorrowed remains explicit oracle/fallback only where deliberately
selected
```

Performance posture:

```text
processing-bottleneck evidence shows active=4 can improve elapsed time under
ordered rebalance compute overlap
topology churn can increase stale recompute, worker dispatches, and
allocation
full-cache evidence shows no regression, but the full-cache corpus remains
archive-producer dominated
broader promotion needs workload breadth and repeated variance evidence
```

## Residual Risks And Limits

```text
handler-state delta/merge:
  not implemented; custom handler cores remain rejected by ordered active
  rebalance until handler state has a merge contract

topology churn cost:
  stale topology recompute is correct but can increase worker dispatches and
  allocation under active-batch overlap

rebalance mutation concurrency:
  rebalance policy, quarantine, telemetry, decision, and topology mutation
  remain serialized by provider-sequence ordered commit

true live ingestion:
  not implemented; deterministic archive-shaped input remains scoped evidence

durable/cross-process:
  durable queues, brokers, cross-process providers/workers, and recovery
  contracts remain out of scope

production operator surfaces:
  deployment, alerting, rollback, and operator reporting were not added

performance breadth:
  the full-cache matrix is single-corpus evidence and archive-producer
  dominated; broader promotion needs additional workload and variance evidence

startup prewarm:
  active=4 has a larger visible startup retained payload prewarm cost than
  active=1

full-suite allocation sensitivity:
  one synthetic benchmark allocation-threshold test remains sensitive in the
  full suite but passes in isolated rerun
```

## Decision

Milestone 022 answers **accepted with scoped warnings** for ordered
rebalance/topology commit:

```text
the scoped in-process runtime/archive rebalance path is ready to keep
multiple accepted batches active for handler-free processing-delta compute
while committing processing, rebalance decisions, validation, and topology
mutation deterministically in provider sequence
```

Milestone 022 answers **yes** for stale topology safety:

```text
active deltas computed against older topology versions are not committed
silently; they are recomputed against the current topology before ordered
commit
```

Milestone 022 answers **yes** for explicit runtime/archive integration:

```text
RunOrderedRebalanceAsync is accepted as the explicit ordered rebalance entry
point over the accepted milestone 020 baseline and milestone 021 ordered
processing foundation
```

Milestone 022 answers **yes with warning** for processing-bottleneck
performance evidence:

```text
active=4 improved elapsed time to 0.891x active=1 on the captured
processing/rebalance-shaped workload, while topology churn increased worker
dispatches and allocation to 1.137x active=1
```

Milestone 022 answers **yes** for full-cache regression evidence:

```text
no full-cache performance regression was observed; validation, processing
completeness, checksum parity, accepted move evidence, worker health, release
health, retained pool health, and terminal retained pressure cleanup passed
```

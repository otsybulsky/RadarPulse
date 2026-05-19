# Milestone 008: Retained Async Shard Transport Plan

Status: draft.

This plan implements the milestone 008 architecture defined in
`008-retained-async-shard-transport.md`.

The plan is intentionally scoped to the first retained async shard worker
transport over the closed milestone 007 synchronous rebalance baseline. It
should not introduce retained `RadarEventBatch` payload snapshots, live
ingestion, durable broker integration, physical worker-local state transfer,
source-level migration, partition splitting, or complex radar algorithms.

## Goal

Milestone 008 adds a worker-shaped execution runtime while preserving the
batch-safe processing and rebalance semantics already proven by milestones
005, 006, and 007.

The target execution shape is:

```text
provider callback publishes one borrowed RadarEventBatch
  -> capture one topology snapshot
  -> route the batch against that snapshot
  -> dispatch shard-scoped work to retained workers
  -> wait for a deterministic completion barrier before callback exit
  -> aggregate processing telemetry in stable topology order
  -> optionally run the milestone 007 rebalance control plane
  -> publish topology N+1 only after worker completion
```

The milestone must preserve the milestone 004 stream contract, the milestone
005 processing-core lifetime boundary, the milestone 006 topology/migration
semantics, and the milestone 007 bounded telemetry and validation surfaces.

The most important rule is:

```text
retained workers are allowed.
retained borrowed RadarEventBatch payload is not allowed.
```

The first implementation target is deliberately conservative:

```text
one in-flight borrowed batch per worker group
bounded retained worker resources
coarse shard or partition-group work items
no baseline payload copying
same-run synchronous versus async benchmark comparison
```

## Starting Point

Milestone 007 is complete and provides:

```text
RadarProcessingCore
RadarProcessingTopologySnapshot
RadarProcessingTopologyManager
RadarProcessingBatchRouter
RadarProcessingBatchRoute
RadarProcessingTelemetry
RadarProcessingResult
RadarProcessingRebalanceSession
RadarProcessingRebalanceSessionResult
RadarProcessingRebalanceValidator
RadarProcessingRebalanceHardeningOptions
RadarProcessingValidationProfile
RadarProcessingDiagnosticRetentionMode
RadarProcessingRebalanceTelemetrySummary
RadarProcessingRebalanceTelemetryRecorder
RadarProcessingRebalanceRetentionStats
RadarProcessingArchiveRebalanceBenchmark
RadarProcessingSyntheticRebalanceBenchmark
processing benchmark synthetic
processing benchmark rebalance-synthetic
processing benchmark rebalance-archive
```

Important existing constraints:

```text
RadarEventBatch is the processing input boundary.
Leased batches are valid only during the synchronous publish callback.
RadarStreamEvent is a 64-byte unmanaged value type.
SourceId -> PartitionId remains stable for a processing session.
PartitionId -> ShardId may change only through topology publication.
One batch is processed against one topology snapshot.
Rebalance publishes accepted moves only between completed batches.
Validation profiles make diagnostic cost explicit.
Bounded telemetry keeps counters and capped recent detail.
Archive benchmark callback timing is separate from replay end-to-end timing.
```

Milestone 008 must not weaken these constraints while adding retained workers.

## Target Implementation Shape

Most worker transport contracts should live under
`RadarPulse.Domain.Processing` when they describe processing execution
semantics, validation, result shape, or telemetry. In-process worker runtime
mechanics can live under infrastructure if they depend on threading primitives.

Candidate layering:

```text
src/Domain/Processing
  execution mode/options, async result contracts, worker telemetry summaries,
  validation errors, batch-scope contracts, lifecycle state enums

src/Infrastructure/Processing
  retained worker group implementation, bounded worker mailboxes, dispatch
  barrier, synthetic benchmark integration, archive benchmark integration

src/Presentation
  CLI execution-mode, worker-count, queue-capacity, validation, retention, and
  worker telemetry output
```

The implementation should keep these responsibilities separate:

```text
execution transport:
  starts retained workers, dispatches batch-scoped work, waits for completion

processing core:
  routes and processes RadarEventBatch values against one captured topology

rebalance control plane:
  consumes completed processing telemetry and may publish topology N+1

telemetry:
  records bounded worker scheduling, execution, failure, and barrier summaries

validation:
  proves async execution matches synchronous reference behavior where selected

benchmarking:
  measures dispatch, queue wait, execution, barrier, replay, allocation, and
  rebalance costs without mixing contours
```

The router should not own worker lifecycle, and workers should not publish
topology or run rebalance policy directly.

## Implementation Slices

### 1. Execution Options And Mode Contracts

Introduce explicit execution-mode and worker-transport options.

Candidate types:

```text
RadarProcessingExecutionMode
RadarProcessingAsyncExecutionOptions
RadarProcessingWorkerAffinity
RadarProcessingWorkerTimeoutPolicy
```

Required behavior:

```text
execution modes distinguish sequential, synchronous partitioned, and async
worker count is explicit and validated
queue capacity is explicit and validated
one-in-flight borrowed-batch behavior is the default
timeout settings are health diagnostics, not permission to release payload
defaults preserve existing synchronous behavior for existing callers
```

Expected tests:

```text
default options are valid
invalid worker counts are rejected
invalid queue capacities are rejected
invalid timeout values are rejected
enum values are stable where public
existing synchronous processing defaults remain unchanged
```

Guardrail:

```text
Do not make async execution the hidden default before the milestone has
benchmark evidence. It should be selectable and measurable.
```

### 2. Worker Lifecycle Contracts

Define worker lifecycle and health state before implementing the runtime.

Candidate types:

```text
RadarProcessingWorkerGroupState
RadarProcessingWorkerHealth
RadarProcessingWorkerLifecycleError
RadarProcessingWorkerGroupStatus
RadarProcessingWorkerId
```

Required behavior:

```text
worker group has explicit not-started, running, stopping, stopped, faulted,
and disposed states or equivalent validated transitions
dispatch before start is invalid
dispatch after dispose is invalid
double start and double dispose are deterministic
faulted or unhealthy worker group behavior is explicit
health status can explain why later dispatch is rejected
```

Expected tests:

```text
valid lifecycle transitions succeed
invalid lifecycle transitions are rejected
disposed group rejects dispatch
faulted group rejects or reports later dispatch according to policy
status snapshots cannot mutate internal state
```

Implementation note:

```text
Keep lifecycle contracts small and numeric. Formatting belongs to CLI output.
```

### 3. Batch Scope, Work Item, And Completion Contracts

Add explicit contracts for one borrowed batch scope.

Candidate types:

```text
RadarProcessingAsyncBatchScope
RadarProcessingAsyncWorkItem
RadarProcessingAsyncWorkCompletion
RadarProcessingAsyncBatchCompletion
RadarProcessingAsyncBatchFailure
```

Required behavior:

```text
batch scope records logical batch sequence and topology version
work item records worker id, shard id, topology version, and partition ids or
source range descriptors
completion records success, failure, cancellation, timing, and metrics
batch completion is immutable or defensively copied
duplicate work item completion is invalid
completion for the wrong batch scope is invalid
```

Expected tests:

```text
work item rejects mismatched topology version
completion rejects wrong batch scope
duplicate completion is rejected
batch completion reports all expected work items
failed work item makes batch completion failed
empty batch/work shapes are handled explicitly
```

Lifetime note:

```text
These contracts may identify borrowed batch scope, but retained public
summaries must not retain payload spans or RadarEventBatch references.
```

### 4. Bounded Worker Mailbox Foundation

Implement bounded in-process mailboxes used by retained workers.

Candidate types:

```text
RadarProcessingWorkerMailbox<TWork>
RadarProcessingWorkerMailboxOptions
RadarProcessingWorkerMailboxResult
```

Required behavior:

```text
mailbox has bounded capacity
enqueue fails deterministically when closed, disposed, or over capacity
dequeue supports cooperative cancellation
close prevents later enqueue
dispose releases waiting workers
mailbox does not allocate unbounded per-batch storage
```

Expected tests:

```text
bounded capacity is enforced
closed mailbox rejects enqueue
disposed mailbox releases waiters
canceled dequeue returns canceled result
enqueue/dequeue preserve FIFO order where required
mailbox does not retain completed work after drain
```

Performance note:

```text
Prefer a simple bounded primitive first. Avoid generic scheduler complexity
unless the implementation proves it is needed.
```

### 5. Retained Worker Group Runtime

Implement the retained worker group over the mailbox foundation.

Candidate types:

```text
RadarProcessingAsyncWorkerGroup
RadarProcessingAsyncWorker
RadarProcessingAsyncWorkerGroupOptions
RadarProcessingAsyncWorkerGroupResult
```

Required behavior:

```text
workers are created once and reused
worker group owns start, stop, cancel, drain, and dispose
workers do bounded CPU work only in milestone 008
worker group supports one in-flight borrowed batch by default
work item execution reports completion to the batch barrier
worker exceptions are captured and do not silently disappear
```

Expected tests:

```text
workers process accepted work
workers can be started and stopped deterministically
worker exception fails the batch
worker group can reject new dispatch while stopping
worker group disposal releases resources
one-in-flight rule rejects overlapping borrowed batch dispatch
```

Guardrail:

```text
Do not use per-batch Task.Run as the transport model. The milestone goal is a
retained worker runtime.
```

### 6. Borrowed Batch Lifetime Guardrails

Make the borrowed batch boundary explicit and testable.

Candidate changes:

```text
async batch scope owns a completion barrier
worker group exposes no fire-and-forget borrowed batch API
dispatch returns only after accepted work is registered to the barrier
drain must complete before callback returns
```

Required behavior:

```text
no async API can intentionally continue borrowed batch work after drain
pending work count is zero after successful drain
failed drain records pending/running work diagnostics
timeout does not make it safe to release borrowed payload while work may still run
borrowed batch scope cannot be reused after completion
```

Expected tests:

```text
successful dispatch leaves no pending work after drain
failed dispatch leaves no accepted untracked work
scope reuse after completion is rejected
overlapping borrowed batch dispatch is rejected
timeout marks batch unhealthy without pretending payload can be released
```

Important limitation:

```text
Non-cooperative worker hangs cannot be made memory-safe by timeout alone. The
safe options are cooperative cancellation, worker group unhealthy/fail-fast, or
a future owned snapshot protocol.
```

### 7. Async Processing Dispatcher

Build the dispatcher that maps a routed batch to worker work items.

Candidate types:

```text
RadarProcessingAsyncBatchDispatcher
RadarProcessingAsyncDispatchPlan
RadarProcessingAsyncDispatchResult
```

Required behavior:

```text
captures one topology snapshot before dispatch
routes one RadarEventBatch against that snapshot
builds coarse work items by shard or partition group
submits work to the retained worker group
waits for completion before returning
does not copy RadarEventBatch payload in the baseline path
```

Expected tests:

```text
dispatch uses one topology version
dispatch produces one work item per expected shard or group
dispatch rejects route/topology mismatches
dispatch completes before returning to caller
dispatch reports worker timing and completion status
```

Compatibility note:

```text
Existing synchronous processing APIs should remain available. Async dispatch
is an execution mode over the same processing contract.
```

### 8. Deterministic Aggregation And Telemetry Parity

Aggregate worker completions into the existing processing result shape.

Candidate changes:

```text
worker completion aggregation helper
async telemetry projection into RadarProcessingTelemetry
async processing result construction
```

Required behavior:

```text
aggregation order is stable by topology, not worker completion order
all sources/events assigned to the captured topology are processed exactly once
async telemetry carries the captured topology version
async result can be compared with synchronous partitioned result
partial or failed completions do not produce successful processing telemetry
```

Expected tests:

```text
out-of-order worker completion still aggregates deterministically
async and synchronous result metrics match for deterministic workloads
missing completion fails validation
duplicate completion fails validation
failed worker prevents successful telemetry projection
```

Performance note:

```text
Keep per-worker counters local while workers run. Aggregate after completion to
avoid hot shared counter contention.
```

### 9. Failure, Cancellation, Timeout, And Health Semantics

Implement the failure surface defined by the architecture.

Candidate types:

```text
RadarProcessingAsyncFailureKind
RadarProcessingAsyncCancellationKind
RadarProcessingAsyncTimeoutResult
RadarProcessingWorkerGroupHealthTransition
```

Required behavior:

```text
worker exception fails the batch
cancellation before dispatch is distinct from cancellation while queued/running
timeout requests cooperative cancellation and records unhealthy status
failed or timed-out batch skips rebalance publication
worker group health after failure is explicit
diagnostics are bounded and numeric
```

Expected tests:

```text
canceled before dispatch returns canceled result
canceled while queued returns canceled work item
canceled while running is observed at safe processing points
worker exception fails batch and records failure code
timeout does not report successful completion
failed batch cannot publish topology migration
```

Guardrail:

```text
Do not implement "timeout returns callback while worker may still read borrowed
payload" semantics.
```

### 10. Worker Telemetry Contracts And Recorder

Add bounded worker execution telemetry.

Candidate types:

```text
RadarProcessingWorkerTelemetrySummary
RadarProcessingWorkerTelemetryCounters
RadarProcessingRecentWorkerBatch
RadarProcessingRecentWorkerFailure
RadarProcessingWorkerRetentionStats
RadarProcessingWorkerTelemetryRecorder
```

Required behavior:

```text
summary reports dispatched/completed/failed/canceled batches
summary reports submitted/completed/failed/canceled work items
summary reports worker count and queue capacity
summary reports dispatch, queue wait, execution, aggregation, and barrier time
recent worker detail is bounded by retention options
dropped worker detail counts are visible
```

Expected tests:

```text
counters increment deterministically
recent detail respects configured limit
counters-only retention keeps aggregate counts
dropped detail counts increase under stress
snapshot cannot mutate recorder state
failure samples retain codes, not formatted text
```

Integration note:

```text
Reuse milestone 007 retention discipline where practical, but do not overload
rebalance skipped-decision counters with worker execution failures.
```

### 11. Processing Core Integration

Expose async execution through the processing core without breaking existing
synchronous callers.

Candidate changes:

```text
processing options include execution mode
processing result includes optional worker telemetry
processing core composes with async dispatcher when selected
```

Required behavior:

```text
sequential and synchronous partitioned modes keep current behavior
async mode processes against the same topology snapshot contract
async mode returns the same processing metrics for deterministic workloads
existing tests pass without selecting async mode
async worker group lifetime is explicit and disposable
```

Expected tests:

```text
existing processing tests pass unchanged
async mode matches synchronous partitioned metrics
async mode rejects invalid worker options
async mode exposes worker telemetry
async mode disposes worker resources when owned by the core/session
```

Design question to resolve during this slice:

```text
worker group ownership may belong to a processing session, a benchmark harness,
or an explicitly supplied runtime. The default should avoid hidden global
workers.
```

### 12. Rebalance Session Integration

Compose async processing with the milestone 007 rebalance session.

Candidate changes:

```text
RadarProcessingRebalanceSession accepts execution mode or processing executor
RadarProcessingRebalanceSessionResult exposes worker telemetry when present
rebalance archive/synthetic benchmark can choose async execution
```

Required behavior:

```text
rebalance sees completed processing telemetry independent of execution mode
worker failures skip rebalance planning for that batch
accepted migrations publish only after async work completes
topology version in route, telemetry, pressure sample, decision, and migration
remains consistent
hardening telemetry and validation profiles continue to work
```

Expected tests:

```text
async rebalance session processes one batch against one topology snapshot
async accepted migration publishes N+1 only after worker completion
failed async processing does not evaluate or publish rebalance
async session result carries worker telemetry and existing hardening telemetry
sync and async rebalance deterministic checksums match where expected
```

Guardrail:

```text
Workers should not own rebalance policy. Rebalance remains a post-processing
control-plane step.
```

### 13. Async Validation Extensions

Extend validation to cover async transport invariants.

Candidate types:

```text
RadarProcessingAsyncValidationError
RadarProcessingAsyncValidationResult
RadarProcessingAsyncValidator
```

Profile behavior:

```text
Off:
  construction guardrails and lifecycle checks only

Essential:
  batch completion, worker failure propagation, topology version consistency,
  and no migration after failed processing

Diagnostic:
  essential checks plus route ownership, worker assignment, aggregation, and
  processing telemetry parity

Benchmark:
  diagnostic checks plus deterministic sync-versus-async comparison markers
```

Expected tests:

```text
essential profile catches failed completion and topology mismatch
diagnostic profile catches missing/duplicate work
diagnostic profile catches assignment outside shard ownership
benchmark profile can compare sync and async checksums
validation failures obey bounded retention
```

Guardrail:

```text
Async correctness must not depend only on benchmark profile. Essential runtime
guards should protect the payload and topology boundary.
```

### 14. Synthetic Processing Benchmark Extension

Extend the processing-only synthetic benchmark to measure async transport.

Candidate changes:

```text
RadarProcessingSyntheticBenchmark supports async execution mode
benchmark result reports worker timing and allocation fields
CLI supports processing benchmark synthetic --mode async-partitioned or
equivalent execution option
```

Required behavior:

```text
same-run sequential, synchronous partitioned, and async contours are comparable
callback/replay costs are not present in processing-only benchmark
worker count and queue capacity are reported
dispatch, execution, aggregation, and barrier timing are reported where reliable
allocation per payload value is reported
```

Expected tests:

```text
benchmark result includes async worker telemetry
all execution modes populate comparable throughput fields
invalid worker CLI options are rejected
async benchmark validates against synchronous metrics in benchmark profile
```

Interpretation rule:

```text
Async being slower on tiny contours is not automatically a failure. The
benchmark must label the contour and explain the overhead.
```

### 15. Synthetic Rebalance Benchmark Extension

Run the synthetic rebalance catalog through async execution.

Required behavior:

```text
existing static, sampling, and rebalance modes remain available
execution mode can be synchronous or async
worker telemetry appears for async rows
accepted moves, skipped decisions, validation status, checksum, retention mode,
and hardening telemetry remain comparable
```

Expected tests:

```text
async synthetic rebalance rows validate successfully
accepted/skipped behavior matches synchronous rows where deterministic
failed async worker path suppresses rebalance publication
worker telemetry and rebalance telemetry both appear in result
```

Performance note:

```text
The milestone 006/007 synthetic rebalance workloads are behavioral
microscopes. Same-run sync-versus-async ratios are the useful overhead signal.
```

### 16. Archive Rebalance Benchmark Extension

Expose async execution through archive rebalance benchmarks.

Required behavior:

```text
rebalance-archive keeps --file and --cache inputs
execution mode can be synchronous or async
archive end-to-end timing remains separate from processing callback timing
callback allocation remains separate from replay allocation
worker telemetry is reported for async callback execution
pressure skew remains benchmark-only and explicit
```

Expected tests:

```text
archive benchmark result includes execution mode
archive benchmark result includes worker count and worker telemetry
all mode still reports comparable static/sampling/rebalance rows
legacy commands keep working with synchronous defaults
invalid worker options are rejected by CLI/tests
```

Real-data smoke requirement:

```text
Run at least one single-file async archive benchmark and one cache-wide async
benchmark against the local corpus if available, comparing callback throughput
and allocation with synchronous rows from the same run.
```

### 17. CLI Execution Surface

Expose async transport through benchmark and smoke commands.

Candidate CLI additions:

```text
--execution synchronous|async
--workers n
--queue-capacity n
--worker-affinity none|shard
--worker-timeout-ms n
```

Required output additions:

```text
execution mode
worker count
queue capacity
submitted/completed/failed/canceled work items
dispatch time
queue wait time
worker execution time
aggregation time
completion barrier wait time
worker health status
callback allocation contour
```

Expected tests:

```text
CLI accepts async execution option
CLI rejects invalid worker counts
CLI rejects invalid queue capacity
CLI prints worker telemetry for async rows
CLI keeps existing synchronous commands compatible
CLI does not print unbounded worker history
```

Output discipline:

```text
Detailed worker samples should remain capped. Large cache runs must remain
readable and should emphasize aggregate worker counters and timing.
```

### 18. Performance Guardrail And Allocation Pass

Measure and reduce avoidable async overhead before closeout.

Initial target areas:

```text
per-batch work item allocation
mailbox enqueue/dequeue allocation
barrier synchronization cost
worker telemetry aggregation
shared counter contention
result snapshot copying
CLI formatting outside measured callback timing
```

Required behavior:

```text
retained workers avoid per-batch Task.Run scheduling churn
baseline async path does not copy RadarEventBatch payload
work item granularity remains coarse
per-worker metrics aggregate after completion
same-run synchronous and async rows are captured
any regression is attributed to measured contour where practical
```

Expected tests and checks:

```text
counters-only worker telemetry does not retain unbounded detail
recent worker telemetry keeps only configured detail
snapshot stability tests protect public result immutability
benchmark allocation fields remain populated for sync and async rows
```

Benchmark requirement:

```text
Capture before/after or baseline/final async contours for processing-only,
synthetic rebalance, single-file archive, and cache-wide archive where local
data is available. The closeout must state whether async improved, stayed
flat, or regressed versus synchronous execution.
```

### 19. Documentation, Decision Trace, Closeout, And Handoff

Close the milestone with documentation once implementation and benchmarks are
complete.

Required documentation:

```text
docs/milestones/008-retained-async-shard-transport-decision-trace.md
docs/milestones/008-retained-async-shard-transport-closeout.md
docs/handoff.md update
```

Decision trace should record:

```text
why retained workers were added before owned payload snapshots
why one-in-flight borrowed batch was the first implementation target
worker lifecycle and health semantics
slow/stalled worker decision
failure and cancellation model
payload lifetime boundary
topology/rebalance publication boundary
performance guardrails and benchmark interpretation
remaining risks and deferred work
```

Closeout should record:

```text
implemented slices
verification commands and results
processing-only sync versus async benchmark table
synthetic rebalance sync versus async benchmark table
single-file archive async benchmark
cache-wide archive async benchmark if local data is available
worker telemetry and allocation comparison
known remaining debt
next milestone recommendation
```

Handoff should record:

```text
current milestone state
new important files
latest verified commands
accepted async/sync performance contours
preserved invariants for future owned snapshots, live ingestion, or
worker-local state transfer
```

### 20. Final Comprehensive Performance Comparison And Regression Gate

End milestone 008 with a comprehensive performance comparison before closeout.

Required comparison set:

```text
milestone 005:
  processing-only sequential and partitioned baselines

milestone 006:
  accepted rebalance callback and synthetic behavioral baselines

milestone 007:
  hardened synchronous rebalance cache-wide and skew stress baselines

milestone 008:
  synchronous versus async processing-only benchmark
  synchronous versus async synthetic rebalance benchmark
  synchronous versus async single-file archive callback benchmark
  synchronous versus async cache-wide archive callback benchmark where available
```

Required metrics:

```text
payload values/s
stream events/s where meaningful
processing callback elapsed time
archive end-to-end elapsed time for archive inputs
dispatch time
queue wait time
worker execution time
aggregation time
completion barrier wait time
allocation per payload value
accepted moves
skipped decisions by reason
failed worker items
failed migrations
validation profile
retention mode
execution mode
worker count
queue capacity
deterministic checksum or validation marker
```

Regression policy:

```text
do not close milestone 008 with unexplained async callback throughput loss
do not close milestone 008 with hidden payload copying in the baseline path
do not close milestone 008 with unbounded worker telemetry growth
do not close milestone 008 with ambiguous timeout semantics
do not present archive end-to-end timing as processing execution throughput
do not make async the default if synchronous remains faster on accepted contours
```

Expected final artifact:

```text
a closeout performance table that states whether async transport improved,
preserved, or regressed each comparable contour, with scheduler overhead
attributed to dispatch, queueing, execution, aggregation, barrier, validation,
replay, or measured unknown debt
```

This gate exists to make sure the new worker-shaped runtime does not quietly
trade away the throughput, allocation, and lifetime guarantees established by
milestones 005, 006, and 007.

## Milestone 008 Completion Criteria

Milestone 008 completion criteria are satisfied when:

```text
[x] execution mode and async options are implemented and tested
[x] worker lifecycle contracts are implemented and tested
[x] batch scope, work item, and completion contracts are implemented and tested
[x] bounded worker mailbox foundation is implemented and tested
[x] retained worker group runtime is implemented and tested
[x] borrowed batch lifetime guardrails are implemented and tested
[x] async processing dispatcher is implemented and tested
[x] deterministic aggregation and telemetry parity are implemented and tested
[x] failure, cancellation, timeout, and health semantics are implemented and tested
[x] worker telemetry contracts and recorder are implemented and tested
[x] processing core can run async execution without breaking synchronous callers
[x] rebalance session can consume async processing results safely
[x] async validation extensions are implemented and tested
[x] processing-only synthetic benchmark exposes async execution
[x] synthetic rebalance benchmark exposes async execution
[ ] archive rebalance benchmark exposes async execution
[ ] CLI exposes execution, worker, queue, and worker telemetry options
[ ] performance guardrail and allocation pass is captured and interpreted
[ ] decision trace is written
[ ] closeout is written
[ ] handoff is updated
[ ] final comprehensive performance comparison is captured and interpreted
```

## Non-Goals

Milestone 008 does not implement:

```text
retained RadarEventBatch payload snapshots
owned payload copy protocol
durable broker integration
live ingestion
timer-owned production scheduler
physical worker-local state transfer
source-level migration
partition splitting or repartitioning
complex radar algorithms
visualization
long-term storage format changes
distributed workers
cross-process transport
```

Milestone 008 should also avoid broad rebalance policy tuning. If async
execution changes observed timing, policy behavior should still be compared
under the existing milestone 007 conservative defaults before any tuning is
considered.

## Risks And Watchpoints

### Borrowed Payload Escapes Callback

Risk:

```text
worker queues retain work items that still reference leased RadarEventBatch
payload after the provider callback returns
```

Mitigation:

```text
one in-flight borrowed batch per worker group
completion barrier before callback return
no fire-and-forget borrowed batch API
pending work validation after drain
owned snapshots deferred to a separate milestone
```

### Async Scheduler Is Slower

Risk:

```text
retained workers add queueing, synchronization, and barrier overhead that makes
the synchronous path faster
```

Mitigation:

```text
same-run sync versus async benchmarks
dispatch/queue/execution/barrier timing attribution
coarse work items
retained workers instead of per-batch Task.Run
execution mode remains selectable
```

### Worker Hang Blocks Provider

Risk:

```text
non-cooperative worker hang blocks the provider callback because borrowed
payload cannot be released safely
```

Mitigation:

```text
workers do bounded CPU work only
cooperative cancellation at safe points
timeouts are health diagnostics, not payload-release permission
unhealthy worker group handling is explicit
future owned snapshots can address temporal decoupling
```

### Rebalance Publishes From Partial Telemetry

Risk:

```text
failed or partial async processing produces telemetry that rebalance treats as
valid
```

Mitigation:

```text
failed batch result shape is explicit
rebalance planning is skipped after failed processing
topology publication remains after successful worker completion only
validation checks topology and completion consistency
```

### Completion Order Changes Results

Risk:

```text
worker completion order leaks into processing result ordering or telemetry
```

Mitigation:

```text
aggregate by stable topology order
stress out-of-order completion in tests
compare async output with synchronous reference checksums
```

### Worker Telemetry Becomes An Unbounded Log

Risk:

```text
per-batch worker timing and failure samples grow without bound in long sessions
```

Mitigation:

```text
aggregate counters plus bounded recent detail
dropped detail counts
counters-only retention support
stress tests over long synthetic runs
```

## Final Performance Comparison Requirement

The milestone must not close until the final comparison is captured and
interpreted.

Required conclusion:

```text
state whether async transport preserved deterministic results
state whether borrowed payload lifetime remained safe
state whether callback throughput improved, stayed flat, or regressed
state whether allocation improved, stayed flat, or regressed
state whether scheduler overhead is dispatch, queue, execution, aggregation,
barrier, validation, replay, or unknown measured debt
state whether synchronous execution remains the recommended/default mode
state whether worker telemetry stayed bounded
state whether rebalance behavior remained conservative and topology-safe
```

If the comparison shows a meaningful throughput or allocation regression, the
closeout must either fix it or record a specific measured reason before the
milestone is considered complete.

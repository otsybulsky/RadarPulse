# RadarPulse Project Progress

Status: current during milestone 025 after decision trace with optimized
full-cache handler matrix evidence accepted.

This file is the project-level progress ledger. Milestone documents remain the
source of detailed architecture, implementation plans, gates, decisions, and
closeouts. This file records the broader arc: what has been achieved, what it
prepared, where the project is now, and what remains before the intended
production-ready result.

## Current Position

RadarPulse has completed the direct archive benchmark default-readiness path,
the first runtime/live ingestion readiness decision, the prewarmed queued-owned
runtime/archive default-baseline promotion, the default-baseline
runtime/archive owned-construction integration milestone, the scoped ordered
concurrent runtime/archive processing milestone, the ordered
rebalance/topology commit milestone, the durable/cross-process runtime
readiness milestone, and the custom handler output contract and BFF readiness
milestone. Milestone 025 handler delta/merge implementation slices, gate
evidence, full-cache handler performance matrix, merge-state optimization,
and decision trace are captured. Closeout is not written yet.

Current state:

```text
completed milestones: 001-024
active milestone: 025 handler delta/merge contract for fast custom analytics
active milestone status:
  implementation slices complete
  pre-decision gate captured
  full-cache handler matrix captured
  merge-state optimization captured
  decision trace written
  closeout not written

current accepted benchmark/default posture:
  queued-owned direct/default contour for broader cache-level archive
  rebalance benchmark workloads, file-level MeasureFile workloads, and
  small-file MeasureCache workloads, accepted with named scoped warnings
  retained payload prewarm is enabled for the direct benchmark
  default-equivalent contour and is explicitly attributed outside measured row
  allocation

current runtime/live posture:
  prewarmed queued-owned default baseline accepted with warnings
  startup-prewarmed queued-owned is accepted as the omitted default for the
  scoped in-process runtime/archive queued-overlap provider path
  RadarProcessingRuntimeArchiveBaseline is accepted as the named
  runtime/archive construction profile for composing queued-owned provider
  defaults with async shard transport execution defaults
  owned construction can use async shard transport with worker count 4 and
  worker queue capacity 8
  scoped processing-core runtime/archive path can keep multiple accepted
  batches active, compute them concurrently, and publish externally visible
  processing results in deterministic provider sequence order
  ordered active batch capacity defaults to 4 and is separate from provider
  queue capacity 8 and worker queue capacity 8
  non-mutating per-batch delta compute plus provider-sequence ordered commit
  is accepted as the safe architecture for overlapping processing-core
  batches
  scoped runtime/archive rebalance can keep multiple accepted batches active
  for handler-free processing-delta compute while committing processing,
  rebalance decisions, validation, and topology mutation deterministically in
  provider sequence
  caller-supplied processing cores and rebalance sessions remain explicit and
  are not silently rewritten
  durable/cross-process runtime readiness is accepted with scoped warnings
  over the broker-neutral durable envelope contract and deterministic
  in-process durable harness
  product-facing custom handler output and BFF readiness is accepted with
  scoped warnings for deterministic archive-shaped MVP workloads
  snapshot-only stateful handler output keeps committed snapshot export and
  explicit sequential fallback
  explicitly mergeable stateful handlers now have scoped in-process handler
  delta/merge implementation, pre-decision gate evidence, and full-cache
  handler matrix evidence
  high-volume custom analytics correctness is proven for benchmark handler
  sets on the local full cache, and optimized active=4 handler delta/merge
  elapsed time is flat versus active=1 handler-aware rows; allocation remains
  higher than active=1 and stays a scoped warning unless parity is required
  persistent durable adapter readiness is now the recommended next milestone
  input because the immediate MVP analytics handler delta/merge path has a
  decision trace
  true live network ingestion and production deployment/rollback/operator
  surfaces are not implemented yet

current next action:
  write milestone 025 closeout, then start the recommended persistent durable
  adapter readiness milestone
```

The current accepted direct benchmark contour is:

```text
surface:
  RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
  RadarProcessingArchiveRebalanceBenchmark.MeasureCache()

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
  retained payload prewarm: enabled for the direct benchmark
    default-equivalent contour
  retained payload prewarm sizing: 65_536 events, 67_108_864 payload bytes,
    1 retained batch
```

The current runtime accepted default-baseline contour is:

```text
surface:
  scoped in-process runtime/archive queued-overlap provider path
  RadarProcessingRuntimeArchiveBaseline owned-construction profile

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  provider queue capacity: 8
  retained-byte budget: 536870912
  startup retained payload prewarm: accepted default lifecycle
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8

ownership note:
  execution defaulting applies only through surfaces that own processing core
  or rebalance session construction; caller-owned cores and sessions remain
  explicit
```

The current accepted ordered runtime/archive processing contour is:

```text
surface:
  RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync
  RadarProcessingQueuedProcessingSession.DrainOrderedConcurrentAsync
  RadarProcessingCore.ComputeProcessingDelta
  RadarProcessingCore.CommitProcessingDelta
  processing benchmark ordered-archive-processing

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  provider queue capacity: 8
  retained-byte budget: 536870912
  startup retained payload prewarm: enabled and visible
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  ordered active batch capacity: 4

commit note:
  active batches may compute concurrently, but shared RadarProcessingCore
  state mutates only through provider-sequence ordered commit
```

The current accepted ordered runtime/archive rebalance contour is:

```text
surface:
  RadarProcessingArchiveQueuedOverlapRunner.RunOrderedRebalanceAsync
  RadarProcessingQueuedRebalanceSession.DrainOrderedConcurrentAsync
  RadarProcessingRebalanceSession ordered-delta commit path
  rebalance benchmark ordered active-batch evidence

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  provider queue capacity: 8
  retained-byte budget: 536870912
  startup retained payload prewarm: enabled and visible
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  ordered active batch capacity: 4

commit note:
  active rebalance batches may compute handler-free processing deltas
  concurrently, but processing, pressure, policy, quarantine, telemetry,
  decision, validation, and topology mutation commit only in provider
  sequence
```

The current accepted MVP output/BFF contour is:

```text
surface:
  RadarProcessingHandlerOutputContract
  RadarProcessingRunReadModel
  RadarProcessingRunReadModelBuilder
  RadarProcessingBffReadModelStore
  RadarProcessingMvpRuntimePlan
  RadarProcessingArchiveQueuedOverlapRunner.RunMvpProcessingAsync

handler posture:
  handler-free processing may use the accepted ordered concurrent runtime
  surfaces
  stateful custom handlers are exported from committed deterministic
  snapshots
  stateful custom handlers use explicit sequential fallback while no handler
  delta/merge contract exists

BFF query shape:
  latest run
  run detail
  batch list
  batch detail
  source output
  handler output
  handler catalog
  diagnostics

diagnostic note:
  processing completeness, provider sequence, checksums, retained pressure,
  release health, readiness status, warnings, and first blocking reason remain
  visible
```

The current accepted readiness answers are:

```text
direct benchmark readiness:
  yes with warnings, broader cache-level, file-level, and small-file direct
  benchmark default readiness is accepted with named scoped warnings

runtime/archive readiness:
  yes with scoped warnings, startup-prewarmed queued-owned is accepted as the
  omitted default for the scoped in-process runtime/archive queued-overlap
  provider path, and scoped owned-construction runtime/archive surfaces can
  consume the accepted provider plus async execution baseline through
  RadarProcessingRuntimeArchiveBaseline

ordered runtime/archive processing readiness:
  yes with scoped warnings, the scoped in-process runtime/archive
  processing-core path can keep multiple accepted batches active, compute them
  concurrently, and publish externally visible processing results in
  deterministic provider sequence order over the accepted milestone 020
  baseline

ordered runtime/archive rebalance readiness:
  yes with scoped warnings, the scoped in-process runtime/archive rebalance
  path can keep multiple accepted batches active for handler-free
  processing-delta compute while committing processing, rebalance decisions,
  validation, and topology mutation deterministically in provider sequence

durable/cross-process runtime readiness:
  yes with scoped warnings, the scoped runtime/archive path is ready to move
  accepted queued-owned batches through a broker-neutral durable envelope
  contract under the deterministic in-process durable harness

custom handler output and BFF readiness:
  yes with scoped warnings, RadarPulse is ready to expose MVP processing
  results through stable custom handler output contracts and application-level
  BFF read models for a future frontend over deterministic archive-shaped
  workloads
```

The named warnings carried forward are:

```text
direct benchmark prewarm cost:
  retained payload prewarm is a real up-front direct benchmark default cost;
  it is not folded into measured row allocation and remains visible in result
  contracts and CLI output

natural cold/default allocation:
  natural unprewarmed MeasureFile, low-count MeasureCache, and runtime
  first-use rows remain allocation-blocked for default-readiness
  interpretation

runtime startup prewarm:
  accepted as visible default lifecycle cost for the runtime/archive default
  baseline; it must not be hidden inside steady measured allocation

runtime and product coverage:
  durable queues and cross-process runtime readiness are accepted milestone
  023 work over the broker-neutral durable envelope contract; custom handler
  output export and BFF read models are accepted milestone 024 work for
  deterministic archive-shaped MVP workloads; persistent adapter readiness,
  true live ingestion, production runtime selection/reporting, and repeated
  variance gates remain future work that should inherit the accepted default
  baseline unless a concrete surface incompatibility is proven

handler analytics throughput:
  stateful custom handlers use committed snapshot export and explicit
  sequential fallback until a handler delta/merge contract exists; high-volume
  custom analytics performance readiness is not accepted yet

ordered processing performance breadth:
  direct full-cache ordered-processing evidence is clean, but the measured
  full-cache workload is archive-producer dominated; processing-bottleneck
  matrices remain useful before broad default promotion

BFF and frontend boundary:
  milestone 024 accepts an application-level BFF read-model query surface,
  not a production HTTP API host or concrete frontend implementation

callback attribution:
  full-cache milestone 020 rows did not regress end-to-end, but queued-owned
  processing callback allocation and elapsed attribution remain heavier than
  borrowed and must stay visible in future performance reviews

full-suite allocation sensitivity:
  earlier milestones carried one allocation-sensitive synthetic benchmark
  caveat in full-suite runs; milestone 024 full Release test project passed
  with 865 passed, 0 failed, and 3 skipped
```

## Completed Arc

### 1. Historical Data Foundation

Milestones:

```text
001 Historical Loader
002 NEXRAD Archive Inspection
003 Historical Replay Publisher
```

Achieved:

```text
historical archive loading foundation
NEXRAD cache inspection and selection vocabulary
sequential and parallel archive replay/publish path
counting/checksum verification for archive replay
CLI smoke and benchmark paths for archive workflows
```

Prepared:

```text
real cached NEXRAD data could be inspected, replayed, counted, checksummed,
and benchmarked before processing/rebalance work began
```

### 2. Processing Core Foundation

Milestones:

```text
004 Processing Core Input Contract
005 Processing Core Architecture
006 Partition-Level Shard Rebalance
007 Rebalance Production Hardening
```

Achieved:

```text
compact deterministic processing input contract
static processing core architecture
partition, shard, pressure, topology, and rebalance vocabulary
hot/cold shard movement planning
rebalance validation, migration, hardening, and guardrails
production-shaped synchronous rebalance behavior
```

Prepared:

```text
the project had a deterministic processing/rebalance core that could be
tested, hardened, benchmarked, and later connected to retained async transport
and queued-owned provider mechanics
```

### 3. Async, Retained, And Owned Transport Foundation

Milestones:

```text
008 Retained Async Shard Transport
009 Owned Payload Provider Decoupling
010 Owned Provider Overlap Cost Reduction
```

Achieved:

```text
retained async shard worker transport foundation
owned payload provider decoupling
owned batch queue and provider/consumer separation
retained-resource pressure, cleanup, and release vocabulary
provider overlap cost measurement and reduction
mechanics needed for queued-owned default candidate work
```

Prepared:

```text
queued-owned provider execution could be evaluated as a real candidate instead
of a pure design idea, with retention, ownership, pressure, release, and
telemetry concepts in place
```

### 4. Queued-Owned Default Candidate And Rollout

Milestones:

```text
011 Queued-Owned Default Readiness
012 Queued-Owned Default Rollout
013 Post-Rollout Hardening And Broader Validation
```

Achieved:

```text
queued-owned candidate readiness evidence
explicit rollout contour and threshold contracts
operator-visible default versus explicit provider provenance
same-run BlockingBorrowed oracle preservation
failure, cleanup, release, and fallback guardrails
broader natural Release validation after rollout
```

Prepared:

```text
the queued-owned contour was stable enough to become the direct archive
benchmark default candidate, while live/runtime/durable surfaces remained
explicitly out of scope
```

### 5. Direct Archive Benchmark Default Migration And Readiness

Milestones:

```text
014 Direct Archive Rebalance API Default Migration
015 Queued-Owned Allocation Readiness
016 Broader Cache-Level Default Readiness
017 File-Level Default Readiness And Cold Retained-Ownership Cost
```

Achieved:

```text
MeasureFile()/MeasureCache() omitted defaults migrated to queued-owned
explicit BlockingBorrowed preserved as fallback and same-run oracle
cache-level allocation warning reduced and bounded
broader cache-level default readiness accepted with named scoped warnings
file-level and small-file default readiness accepted with scoped retained
payload prewarm and named warnings
natural unprewarmed file/small-file allocation blocker recorded and scoped
mixed-cache worker counters diagnosed and fixed as source-universe sizing
processing completeness made a gate/reporting requirement
CLI omitted-provider cache path aligned with direct defaults
full test project passed before milestone 016 closeout:
  768 passed, 0 failed, 3 skipped
full test project passed before milestone 017 closeout:
  771 passed, 0 failed, 3 skipped
```

Prepared:

```text
the direct benchmark path is ready enough across cache-level, file-level, and
small-file workloads to stop treating direct archive benchmark defaults as the
main blocker
```

### 6. Runtime And Live Ingestion Readiness

Milestone:

```text
018 Runtime And Live Ingestion Readiness
```

Achieved:

```text
runtime/archive-provider surface and lifecycle audit
runtime readiness gate matrix and threshold posture
runtime prewarm lifecycle posture
CancelQueued cancellation guardrail fix
runtime steady intake gate over deterministic archive replay
runtime pressure/backpressure/cancellation/failure gate over deterministic
synthetic lifecycle shapes
gate interpretation and formal decision trace
explicit opt-in only runtime/live readiness answer
```

Final answer:

```text
explicit opt-in only, queued-owned is runtime-safe when selected explicitly
for scoped in-process runtime/archive replay surfaces with startup prewarm and
existing guardrails, but it is not accepted as the omitted runtime/live
ingestion default
```

Superseded by milestone 019 for the scoped queued-overlap provider path:

```text
milestone 019 accepts startup-prewarmed queued-owned as the omitted default
for scoped runtime/archive queued-overlap and as the default baseline for
remaining runtime/archive integration work
```

Verification summary:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused runtime guardrail suite:
  56 passed, 0 failed

full test project:
  774 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark test failed in full suite
  isolated rerun of the same test passed
```

Prepared:

```text
the project now has an accepted prewarmed queued-owned runtime/archive default
baseline. Future runtime/archive work should integrate this contour and prove
only the new surface boundary, not reopen the provider default decision.
```

### 7. Runtime Default Baseline Promotion

Milestone:

```text
019 Prewarmed Queued-Owned Runtime Default Promotion
```

Achieved:

```text
promoted RadarProcessingArchiveQueuedOverlapOptions.Default to the accepted
runtime rollout contour
wired startup retained payload prewarm before steady overlap allocation
capture
surfaced retained payload prewarm result on
RadarProcessingArchiveQueuedOverlapResult
preserved explicit diagnostic/no-prewarm options
recorded focused runtime default gate evidence
recorded decision trace and closeout
```

Final answer:

```text
accepted with scoped warnings, startup-prewarmed queued-owned is accepted as
the omitted default for the scoped in-process runtime/archive queued-overlap
provider path and as the default baseline for remaining runtime/archive work
```

Verification summary:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused Debug runtime/prewarm suite:
  41 passed, 0 failed

focused Release runtime/prewarm suite:
  41 passed, 0 failed

full test project:
  776 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark test failed in full suite
  isolated rerun of the same test passed
```

Prepared:

```text
remaining runtime/archive work should now use the accepted prewarmed
queued-owned default contour and prove only its own integration boundary
```

### 8. Runtime Default Baseline Integration

Milestone:

```text
020 Default-Baseline Runtime/Archive Integration
```

Achieved:

```text
added RadarProcessingRuntimeArchiveBaseline as the named runtime/archive
owned-construction baseline profile
composed the milestone 019 queued-overlap provider default with async shard
transport execution defaults
created owned-construction helpers for async execution options, core options,
processing cores, and rebalance sessions
kept provider defaulting and execution defaulting separately assertable
preserved caller-supplied processing cores and rebalance sessions as explicit
added deterministic live-adapter-shaped steady intake evidence
added deterministic live-adapter-shaped validation failure cleanup evidence
recorded provenance audit, gate evidence, full-cache performance matrix,
decision trace, and closeout
```

Final answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
integration boundary is ready to consume the accepted prewarmed queued-owned
plus async execution default baseline without reopening the provider default
decision
```

Verification summary:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused milestone 020 gate suite:
  24 passed, 0 failed, 0 skipped

full test project:
  787 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark test failed in full suite
  isolated rerun of the same test passed

full-cache performance matrix:
  no end-to-end full-cache regression versus explicit BlockingBorrowed oracle
  default elapsed ratios: 0.793x static, 0.890x sampling,
    0.881x rebalance-session
  default allocation ratios: 1.000x static, 1.002x sampling,
    1.003x rebalance-session
```

Prepared:

```text
runtime/archive owned construction now has a named accepted default baseline.
The next performance/architecture lever is ordered concurrent multi-batch
runtime/archive processing over this baseline, while preserving deterministic
ordering, topology safety, failure cleanup, and no silent borrowed fallback.
```

### 9. Ordered Concurrent Runtime/Archive Processing

Milestone:

```text
021 Ordered Concurrent Runtime/Archive Processing
```

Achieved:

```text
added explicit ordered active batch capacity with default 4
kept active batch capacity separate from provider queue capacity 8 and worker
  queue capacity 8
added RadarProcessingOrderedResultCoordinator for out-of-order completion and
  deterministic provider-sequence publication
implemented handler-free non-mutating per-batch processing deltas
implemented provider-sequence ordered RadarProcessingCore delta commit
kept shared RadarProcessingCore mutation out of concurrent compute
added ordered concurrent processing session drain
preserved async shard transport worker telemetry through non-mutating delta
  compute
added RadarProcessingArchiveQueuedOverlapRunner.RunProcessingAsync as the
  explicit ordered runtime/archive processing path
added processing benchmark ordered-archive-processing as the direct
  RunProcessingAsync full-cache CLI benchmark
recorded gate evidence, full-cache matrices, decision trace, and closeout
```

Final answer:

```text
accepted with scoped warnings, the scoped in-process runtime/archive
processing-core path is ready to keep multiple accepted batches active,
compute them concurrently, and publish externally visible processing results
in deterministic provider sequence order over the accepted milestone 020
baseline
```

Verification summary:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused milestone 021 Release gate suite:
  46 passed, 0 failed, 0 skipped

full Release test project:
  805 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark test failed in full suite
  isolated rerun of the same test passed

rebalance-archive full-cache matrix:
  no end-to-end full-cache regression versus explicit BlockingBorrowed oracle
  default elapsed ratios: 0.965x static, 0.878x sampling,
    0.884x rebalance-session
  default allocation ratios: 1.003x static, 1.001x sampling,
    1.000x rebalance-session

ordered-archive-processing direct full-cache matrix:
  active=4 elapsed ratio versus active=1: 0.994x
  active=4 steady allocation ratio versus active=1: 1.006x
  final processing checksum matched
  processing completeness passed
  worker failed batches/items 0/0
  retained payload pool misses 0
  release failures 0
  terminal combined retained pressure 0
```

Prepared:

```text
the processing-core runtime/archive path now has a proven ordered active-batch
compute and ordered commit foundation. Future work can build ordered
rebalance/topology commit on top of this foundation instead of first solving
shared processing-state mutation.
```

## Current And Remaining Arc

Milestone 022 is now complete. The following section records that completed
stage and the remaining route from the current state to the intended
production-ready result.

### 10. Ordered Rebalance/Topology Commit And Processing-Bottleneck Evidence

Status:

```text
complete as milestone 022
architecture and implementation plan written
implementation complete through gate capture
post-gate full-cache performance matrix captured
decision trace written
closeout written
```

Closeout:

```text
docs/milestones/022-ordered-rebalance-topology-commit-closeout.md

accepted with scoped warnings, the scoped in-process runtime/archive
rebalance path is ready to keep multiple accepted batches active for
handler-free processing-delta compute while committing processing,
rebalance decisions, validation, and topology mutation deterministically in
provider sequence
```

Latest full-cache regression evidence:

```text
docs/milestones/022-ordered-rebalance-topology-commit-full-cache-performance-matrix.md

rebalance-archive full-cache matrix:
  no full-cache performance regression observed
  default elapsed ratios versus explicit BlockingBorrowed:
    0.883x static, 0.891x sampling, 0.871x rebalance-session
  default allocation ratios versus explicit BlockingBorrowed:
    1.002x static, 1.001x sampling, 1.002x rebalance-session

ordered-archive-processing direct full-cache matrix:
  active=4 elapsed ratio versus active=1: 0.999x
  active=4 steady allocation ratio versus active=1: 1.007x
  final processing checksum matched
  processing completeness passed
  worker failed batches/items 0/0
  retained payload pool misses 0
  release failures 0
  terminal combined retained pressure 0
```

Recommended next milestone:

```text
durable/cross-process runtime readiness
```

Goal:

```text
implemented ordered rebalance/topology commit over the ordered processing
foundation and captured processing-bottleneck plus full-cache regression
evidence before broader default promotion
```

Completed work:

```text
rebalance pressure, policy, quarantine, telemetry, decision, and topology
  ordered commit design implemented for handler-free processing deltas
accepted-move evidence preservation across ordered active batches
topology-version validation under overlapping compute
failure, cancellation, release, and retained pressure cleanup with
  rebalance/topology work in flight
processing-bottleneck synthetic matrix captured
full-cache regression matrix captured
decision trace written
```

Prepared by current state:

```text
non-mutating processing delta compute is implemented
provider-sequence ordered processing commit is implemented
ordered result publication is implemented
RunProcessingAsync is available as the explicit ordered runtime/archive path
RunOrderedRebalanceAsync is accepted as the explicit ordered
  runtime/archive rebalance path
processing-bottleneck synthetic evidence is captured
full-cache regression evidence is captured after ordered rebalance/topology
  commit
startup prewarm, worker telemetry, release health, processing completeness,
and retained pressure cleanup are visible
```

Still not implemented at milestone 024 closeout:

```text
durable queues or brokers
cross-process providers/workers
production operator/deployment/rollback surfaces
true live network ingestion
```

### 11. Durable And Cross-Process Runtime

Status:

```text
complete as milestone 023
architecture document written
architecture decision written
implementation plan written
slice 1 durable envelope contract and queue harness complete
slice 2 durable ordered processing runtime complete
slice 3 retry, recovery, cancellation, and cleanup complete
slice 4 durable ordered rebalance runtime complete
slice 5 operator summary and gate evidence complete
slice 6 pre-decision trace review point reached
decision trace written
closeout written
```

Milestone documents:

```text
docs/milestones/023-durable-cross-process-runtime-readiness.md
docs/milestones/023-durable-cross-process-runtime-readiness-architecture-decision.md
docs/milestones/023-durable-cross-process-runtime-readiness-plan.md
docs/milestones/023-durable-cross-process-runtime-readiness-gate.md
docs/milestones/023-durable-cross-process-runtime-readiness-decision-trace.md
docs/milestones/023-durable-cross-process-runtime-readiness-closeout.md
```

Goal:

```text
implement durable/cross-process runtime readiness using the accepted
prewarmed queued-owned default baseline, ordered processing commit, and
ordered rebalance/topology commit unless a concrete ownership-boundary
incompatibility is proven
```

Planned work:

```text
durable envelope contract and queue harness
durable ordered processing runtime
retry, recovery, cancellation, and cleanup semantics
durable ordered rebalance runtime
operator-visible summary and gate evidence
pre-decision trace review point
```

Closeout:

```text
accepted with scoped warnings for durable/cross-process runtime readiness over
the broker-neutral durable envelope contract and deterministic in-process
durable harness
```

Prepared by current state:

```text
direct benchmark readiness is accepted across cache, file, and small-file
workloads
in-process queued-overlap runtime default promotion is accepted
ordered processing commit is accepted for runtime/archive processing
ordered rebalance/topology commit is accepted for runtime/archive rebalance
retained pressure, cleanup, release, telemetry, validation, prewarm
attribution, and processing-completeness guardrails can inform durable design
```

Still not implemented:

```text
production broker adapters
true live network ingestion
production deployment/rollback/runbooks
handler-state delta/merge
exactly-once production delivery claims
```

Historical closeout recommendation:

```text
persistent durable adapter readiness
```

Post-closeout MVP planning update:

```text
defer persistent durable adapter readiness until after a product-facing
custom handler output and BFF readiness milestone
```

### 12. Custom Handler Output Contract And BFF Readiness

Status:

```text
complete as milestone 024
architecture document written
implementation plan written
slice 1 handler output contract audit complete
slice 2 processing output read models complete
slice 3 BFF application read surface complete
slice 4 handler execution posture gate complete
slice 5 archive-shaped MVP gate complete
optional full-cache performance matrix captured
decision trace written
closeout written
```

Milestone documents:

```text
docs/milestones/024-custom-handler-output-contract-and-bff-readiness.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-plan.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-gate.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-full-cache-performance-matrix.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-decision-trace.md
docs/milestones/024-custom-handler-output-contract-and-bff-readiness-closeout.md
```

Goal:

```text
turn the accepted processing/runtime foundations into an MVP-facing result
surface by defining safe custom handler output contracts, explicit
handler-state posture, and backend-for-frontend read models for a future UI
```

Completed work:

```text
custom handler output DTOs and export contract
handler descriptor metadata suitable for frontend-facing result discovery
explicit handler-state posture:
  handler-free ordered concurrent remains allowed
  stateful custom handlers use committed snapshot export and sequential
  fallback until a handler delta/merge contract exists
stable processing result read model for batch status, source summaries,
  handler outputs, diagnostics, checksums, and readiness state
BFF application surface that can serve latest run, batch list, batch detail,
  source summary, handler output, and runtime diagnostics
operator/developer diagnostics that preserve provider sequence, processing
  completeness, release health, retained pressure, and first blocking reason
focused tests over handler output projection, BFF DTO stability, and
  deterministic ordered/sequential behavior
Release gate over an archive-shaped MVP workload
optional full-cache performance matrix as regression evidence
```

Closeout:

```text
accepted with scoped warnings for custom handler output contract and BFF
readiness over deterministic archive-shaped MVP workloads
```

Verification summary:

```text
Release build:
  succeeded, 0 warnings, 0 errors

focused milestone 024 Release gate:
  17 passed, 0 failed, 0 skipped

full Release test project:
  865 passed, 0 failed, 3 skipped

optional full-cache performance matrix:
  no full-cache regression observed
  rebalance-archive default elapsed ratios versus explicit BlockingBorrowed:
    0.812x static, 0.931x sampling, 0.885x rebalance-session
  rebalance-archive default allocation ratios versus explicit
    BlockingBorrowed:
    1.002x static, 1.003x sampling, 1.000x rebalance-session
  ordered-archive-processing active=4 elapsed ratio versus active=1:
    0.982x
  ordered-archive-processing active=4 steady allocation ratio versus
    active=1:
    1.007x
```

Prepared by current state:

```text
processing core already supports configured handlers and source-local handler
state snapshots
milestone 021 and 022 ordered concurrent paths intentionally reject handler
delta compute until a handler-state contract exists
milestone 020 runtime/archive baseline provides the accepted owned
construction profile for MVP runtime surfaces
milestone 023 durable readiness summary provides an operator-readable shape
that the BFF can reuse without requiring a persistent adapter first
milestone 024 gives future frontend/API work a stable result shape without
leaking processing queue or durable session internals
```

Still not implemented:

```text
handler delta/merge
ordered concurrent execution for arbitrary stateful handlers
high-volume custom analytics performance readiness
persistent durable adapter implementation
true live network ingestion
frontend application implementation
production HTTP BFF host
production deployment, rollback, autoscaling, alerts, and runbooks
exactly-once production delivery claims
```

Post-closeout selected milestone:

```text
handler delta/merge contract for fast custom analytics
```

### 13. Handler Delta/Merge Contract For Fast Custom Analytics

Status:

```text
active as milestone 025
architecture/concept document written
implementation plan written
handler classification contract complete
per-batch handler delta contract complete
deterministic ordered merge coordinator complete
MVP runtime integration and fallback policy complete
BFF compatibility and diagnostics complete
handler-heavy performance gate complete
pre-decision gate captured
full-cache handler matrix captured and optimized
decision trace written
closeout not written
```

Milestone documents:

```text
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-plan.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-gate.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-full-cache-performance-matrix.md
docs/milestones/025-handler-delta-merge-contract-for-fast-custom-analytics-decision-trace.md
```

Goal:

```text
make stateful custom analytics fast on large volumes without weakening the
accepted ordered commit and handler output contracts
```

Implemented work:

```text
mergeable, snapshot-only, and unsupported handler classification
non-mergeable snapshot-only sequential fallback policy
per-batch handler delta identity, validation, serialization, and versioning
deterministic provider-sequence merge contract
retry, replay, and idempotency behavior for handler deltas
failure diagnostics and first blocking reason for unsupported handler work
sequential fallback parity gates
BFF output compatibility with merged handler results
handler-heavy large-volume performance gate
```

Pre-decision gate summary:

```text
focused milestone 025 Release gate:
  26 passed, 0 failed, 0 skipped

Release build:
  succeeded, 0 warnings, 0 errors

full Release test project:
  890 passed, 1 failed, 3 skipped
  known allocation-sensitive synthetic benchmark caveat isolated rerun:
    1 passed, 0 failed, 0 skipped

optimized full-cache handler matrix:
  counter-checksum active=4:
    61_588.17 ms, 8_188_695_464 allocated bytes
  counter-checksum-heavy active=4:
    62_687.17 ms, 12_209_454_512 allocated bytes
  correctness:
    4/4 rows completed
    processing completeness succeeded
    terminal retained pressure: 0
```

Prepared by milestone 025 implementation:

```text
handler-free work keeps existing ordered concurrent processing-delta posture
snapshot-only handlers keep explicit sequential fallback and committed
  snapshot export
mergeable handlers can compute immutable per-batch handler deltas and merge
  them by provider sequence
unsupported handlers fail closed through handler output diagnostics and
  readiness blocking
merged handler output projects through milestone 024 BFF read models
```

Prepared by current state:

```text
milestone 024 defines stable handler output descriptors and BFF read models
stateful handler output is already deterministic through committed snapshot
export
sequential fallback gives the correctness oracle for delta/merge parity
milestones 021 and 022 provide ordered concurrent handler-free delta compute
and provider-sequence ordered commit foundations
milestone 023 provides durable retry/recovery semantics that future handler
delta work must not contradict
```

Decision trace:

```text
accepted with scoped warnings for handler delta/merge contract and fast
custom analytics over deterministic archive-shaped MVP workloads

warnings:
the fast path applies only to explicitly mergeable handlers
mergeable handlers must provide deterministic handler-owned merge semantics
delta serialization is an in-process/versioned contract gate, not production
  persistent adapter proof
the performance gate is deterministic in-process evidence, not cross-machine
  or production throughput certification
optimized full-cache active=4 elapsed time is flat versus active=1, but
  allocation remains higher than active=1
persistent durable adapter readiness remains future reliability work
true live network ingestion remains future work
production HTTP BFF host, frontend, deployment, rollback, autoscaling,
  alerts, and runbooks remain future work
exactly-once production delivery is not claimed

recommended next milestone input:
  persistent durable adapter readiness
```

Out of scope unless explicitly pulled forward:

```text
production HTTP BFF host
frontend application implementation
persistent durable adapter implementation
true live network ingestion
production deployment, rollback, autoscaling, alerts, and runbooks
exactly-once production delivery claims
```

### 14. Persistent Durable Adapter Readiness

Recommended next reliability milestone after milestone 025 closeout unless
priorities change.

Goal:

```text
validate one concrete persistent or broker-like adapter against the milestone
023 durable envelope contract
```

Likely required work:

```text
serialized durable envelope schema and compatibility checks
persistent accept, claim, complete, fail, abandon, retry, poison, commit, and
  release transitions
restart recovery from pending, claimed, completed, failed, poison, canceled,
  and released states
duplicate delivery and idempotent accept behavior
lease or abandoned-attempt recovery policy
poison/dead-letter mapping
provider-sequence ordered commit from adapter-backed state
retained ownership cleanup across restart and adapter failure
operator-readable adapter summary and first blocking envelope state
Release gates over adapter-backed durable workloads
```

Prepared by current state:

```text
milestone 023 defines and tests the broker-neutral durable envelope contract,
ordered processing commit, ordered rebalance/topology commit, retry/recovery,
poison, cancellation cleanup, and operator-readable readiness summary that
the adapter must preserve
milestone 024 provides product-facing output and BFF contracts that a future
persistent adapter can serve without changing the MVP result shape
milestone 025 should define how fast handler analytics deltas interact with
retry, replay, and ordered commit before persistent adapter work hardens
those semantics
```

### 15. Production Pipeline Integration

Future milestone after persistent durable adapter readiness.

Goal:

```text
connect the accepted runtime provider posture into an end-to-end operational
pipeline with deployable defaults, diagnostics, and acceptance gates
```

Likely required work:

```text
live archive/runtime ingestion path
configuration defaults and override policy
deployment/operator profile
observability, telemetry, diagnostics, and alerting
end-to-end validation on representative workloads
rollback and fallback procedures
performance budget and capacity planning
```

Prepared by current state:

```text
benchmark and runtime default-baseline evidence provide default expectations
for runtime integration, but production integration still needs separate
surface evidence
```

### 16. Product-Facing Completion

Future milestone after production pipeline integration.

Goal:

```text
turn the processing pipeline into the intended user-facing RadarPulse product
surface
```

Likely required work:

```text
product-facing radar workflows
higher-level analysis outputs
visualization or inspection surfaces if selected
user-facing operational controls
end-to-end acceptance criteria
documentation and release packaging
```

Prepared by current state:

```text
the backend data, replay, processing, rebalance, direct benchmark, and
runtime default-baseline foundations are increasingly stable, and milestone
024 has added the first MVP-facing backend output contract; full product
workflows still need their own milestone gates
```

## Project Chain Summary

```text
[done] historical archive foundation
[done] replay/publish foundation
[done] processing/rebalance core
[done] retained async and owned queued transport foundation
[done] queued-owned default candidate and rollout
[done] direct archive benchmark default migration
[done] cache-level allocation readiness
[done] broader cache-level default readiness
[done] file-level/small-file default readiness
[done] runtime/live ingestion readiness decision
[done] prewarmed queued-owned runtime default baseline promotion
[done] default-baseline runtime/archive integration
[done] ordered concurrent runtime/archive processing
[done] ordered rebalance/topology commit and processing-bottleneck evidence
[done] durable/cross-process runtime
[done] custom handler output contract and BFF readiness
[active] handler delta/merge contract for fast custom analytics
  (decision trace written; closeout pending)
[recommended next] persistent durable adapter readiness
[later] production pipeline integration
[later] product-facing completion
```

## Update Rules

When a milestone closes, update this file with:

```text
milestone number and name
what was achieved
what it prepared
final closeout answer
important warnings or scope limits
verification summary
recommended next milestone input
whether the project chain changed
```

Do not treat this file as a replacement for milestone documents. It is the
project map; milestone docs remain the detailed record.

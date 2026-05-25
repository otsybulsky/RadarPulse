# RadarPulse Project Progress

Status: current after milestone 022 start.

This file is the project-level progress ledger. Milestone documents remain the
source of detailed architecture, implementation plans, gates, decisions, and
closeouts. This file records the broader arc: what has been achieved, what it
prepared, where the project is now, and what remains before the intended
production-ready result.

## Current Position

RadarPulse has completed the direct archive benchmark default-readiness path,
the first runtime/live ingestion readiness decision, the prewarmed queued-owned
runtime/archive default-baseline promotion, the default-baseline
runtime/archive owned-construction integration milestone, and the scoped
ordered concurrent runtime/archive processing milestone.

Current state:

```text
completed milestones: 001-021
active milestone: 022 ordered rebalance/topology commit and
  processing-bottleneck evidence

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
  caller-supplied processing cores and rebalance sessions remain explicit and
  are not silently rewritten
  true live network ingestion, durable queues, cross-process runtime, and
  ordered concurrent rebalance/topology commit are not implemented yet, but
  they inherit this default baseline unless a concrete incompatibility is
  proven

current active milestone:
  ordered rebalance/topology commit and processing-bottleneck performance
  evidence
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

runtime coverage:
  true live ingestion, durable queues, cross-process workers, production
  runtime selection/reporting, ordered concurrent rebalance/topology commit,
  handler-state delta/merge, and repeated variance gates remain future
  implementation work that should inherit the accepted default baseline unless
  a concrete surface incompatibility is proven

ordered processing performance breadth:
  direct full-cache ordered-processing evidence is clean, but the measured
  full-cache workload is archive-producer dominated; processing-bottleneck
  matrices remain useful before broad default promotion

callback attribution:
  full-cache milestone 020 rows did not regress end-to-end, but queued-owned
  processing callback allocation and elapsed attribution remain heavier than
  borrowed and must stay visible in future performance reviews

full-suite allocation sensitivity:
  one synthetic benchmark allocation-threshold test remains sensitive in the
  full suite but passes in isolated rerun
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

## Remaining Arc

The following stages are not complete. They are the recommended route from the
current state to the intended production-ready result.

### 10. Ordered Rebalance/Topology Commit And Processing-Bottleneck Evidence

Status:

```text
active as milestone 022
architecture and implementation plan written
decision trace intentionally pending until implementation and gate review
```

Recommended next milestone:

```text
ordered rebalance/topology commit and processing-bottleneck performance
evidence
```

Goal:

```text
extend the ordered processing foundation toward rebalance/topology safety and
collect workload evidence where processing, not archive replay, is the
dominant bottleneck before broader default promotion
```

Likely required work:

```text
rebalance pressure, policy, quarantine, telemetry, decision, and topology
  ordered commit design
handler-state delta/merge decision if handler cores need ordered concurrency
accepted-move evidence preservation across ordered active batches
topology-version validation under overlapping compute
failure, cancellation, release, and retained pressure cleanup with
  rebalance/topology work in flight
processing-bottleneck synthetic or representative archive-shaped matrices
repeated variance evidence if the ordered path is considered for broader
default promotion
```

Prepared by current state:

```text
non-mutating processing delta compute is implemented
provider-sequence ordered processing commit is implemented
ordered result publication is implemented
RunProcessingAsync is available as the explicit ordered runtime/archive path
startup prewarm, worker telemetry, release health, processing completeness,
and retained pressure cleanup are visible
```

Still not implemented:

```text
durable queues or brokers
cross-process providers/workers
production operator/deployment/rollback surfaces
true live network ingestion
```

### 11. Durable And Cross-Process Runtime

Future milestone after ordered rebalance/topology commit readiness.

Goal:

```text
implement durable queues, brokers, or cross-process providers/workers using
the accepted prewarmed queued-owned default baseline unless a concrete
ownership-boundary incompatibility is proven
```

Likely required work:

```text
durable queue or broker contract
cross-process retained payload ownership model
worker-local state and transfer boundaries
durable-safe ordered commit and recovery policy
failure, retry, cancellation, and cleanup semantics across process boundaries
operator-visible recovery and fallback policy
Release gates over durable/cross-process workloads
```

Prepared by current state:

```text
direct benchmark readiness is accepted across cache, file, and small-file
workloads
in-process queued-overlap runtime default promotion is accepted
retained pressure, cleanup, release, telemetry, validation, prewarm
attribution, and processing-completeness guardrails can inform durable design
```

### 12. Production Pipeline Integration

Future milestone after durable/runtime readiness.

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

### 13. Product-Facing Completion

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
runtime default-baseline foundations are increasingly stable, but
product-facing scope has not yet been the main milestone target
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
[active] ordered rebalance/topology commit and processing-bottleneck evidence
[later] durable/cross-process runtime
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

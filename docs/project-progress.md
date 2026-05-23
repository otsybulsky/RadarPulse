# RadarPulse Project Progress

Status: current after milestone 019 closeout.

This file is the project-level progress ledger. Milestone documents remain the
source of detailed architecture, implementation plans, gates, decisions, and
closeouts. This file records the broader arc: what has been achieved, what it
prepared, where the project is now, and what remains before the intended
production-ready result.

## Current Position

RadarPulse has completed the direct archive benchmark default-readiness path,
the first runtime/live ingestion readiness decision, and the prewarmed
queued-owned runtime/archive default-baseline promotion.

Current state:

```text
completed milestones: 001-019
active milestone: none selected

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
  future runtime/archive work should start from this accepted default contour
  and prove only its surface-specific integration boundary
  true live network ingestion, durable queues, cross-process runtime, and
  ordered concurrent rebalance are not implemented yet, but they inherit this
  default baseline unless a concrete incompatibility is proven
  milestone 019 does not automatically rewrite processing core execution mode
  or async worker sizing; future processing-core default work should adopt the
  baseline explicitly

current recommended next milestone:
  default-baseline runtime/archive integration
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
  default baseline for remaining runtime/archive integration work

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  provider queue capacity: 8
  retained-byte budget: 536870912
  startup retained payload prewarm: accepted default lifecycle

execution note:
  processing execution mode and async worker sizing remain owned by the
  processing core/rebalance session until a dedicated defaulting surface
  adopts the baseline explicitly
```

The current accepted readiness answers are:

```text
direct benchmark readiness:
  yes with warnings, broader cache-level, file-level, and small-file direct
  benchmark default readiness is accepted with named scoped warnings

runtime/archive readiness:
  yes with scoped warnings, startup-prewarmed queued-owned is accepted as the
  omitted default for the scoped in-process runtime/archive queued-overlap
  provider path and as the default baseline for remaining runtime/archive
  integration work
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
  runtime selection/reporting, and repeated variance gates remain future
  implementation work that should inherit the accepted default baseline unless
  a concrete surface incompatibility is proven

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

## Remaining Arc

The following stages are not complete. They are the recommended route from the
current state to the intended production-ready result.

### 8. Runtime Default Baseline Integration

Recommended next milestone:

```text
default-baseline runtime/archive integration
```

Goal:

```text
stand on the accepted defaults and integrate them into the remaining
runtime/archive path surfaces without re-proving queued-owned as the default
```

Likely required work:

```text
make new runtime/archive surfaces consume the accepted default contour by
default
add processing-core execution defaulting only in the surface that owns core
construction
add live adapter evidence as integration evidence, not as a new provider
default decision
keep prewarm, pressure, cancellation, failure, release, and cleanup visible
preserve BlockingBorrowed as explicit fallback/oracle, not automatic silent
fallback
```

Prepared by current state:

```text
direct benchmark default readiness is accepted
scoped queued-overlap runtime default promotion is accepted
startup prewarm is wired and reported separately from steady allocation
runtime lifecycle and pressure/failure guardrails are documented and gated
the decision trace defines future work as integration of the accepted default
baseline
```

Still not implemented:

```text
durable queues or brokers
cross-process providers/workers
ordered concurrent rebalance
processing-core execution defaulting from construction sites that own the core
production operator/deployment/rollback surfaces
```

### 9. Durable And Cross-Process Runtime

Future milestone after default-baseline runtime/archive integration.

Goal:

```text
implement durable queues, brokers, cross-process providers/workers, or
ordered concurrent runtime rebalance using the accepted prewarmed
queued-owned default baseline unless a concrete ownership-boundary
incompatibility is proven
```

Likely required work:

```text
durable queue or broker contract
cross-process retained payload ownership model
worker-local state and transfer boundaries
ordered concurrent rebalance commit policy
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

### 10. Production Pipeline Integration

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

### 11. Product-Facing Completion

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
[next] default-baseline runtime/archive integration
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

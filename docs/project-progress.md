# RadarPulse Project Progress

Status: current during milestone 019 decision-trace checkpoint.

This file is the project-level progress ledger. Milestone documents remain the
source of detailed architecture, implementation plans, gates, decisions, and
closeouts. This file records the broader arc: what has been achieved, what it
prepared, where the project is now, and what remains before the intended
production-ready result.

## Current Position

RadarPulse has completed the direct archive benchmark default-readiness path
and the first runtime/live ingestion readiness decision.

Current state:

```text
completed milestones: 001-018
active milestone:
  019 prewarmed queued-owned runtime default promotion
  complete through implementation and gate
  decision trace pending review

current accepted benchmark/default posture:
  queued-owned direct/default contour for broader cache-level archive
  rebalance benchmark workloads, file-level MeasureFile workloads, and
  small-file MeasureCache workloads, accepted with named scoped warnings
  retained payload prewarm is enabled for the direct benchmark
  default-equivalent contour and is explicitly attributed outside measured row
  allocation

current runtime/live posture:
  explicit opt-in only
  queued-owned is runtime-safe when selected explicitly for scoped in-process
  runtime/archive replay surfaces with startup prewarm and existing guardrails
  queued-owned is not accepted as the omitted runtime/live ingestion default
  milestone 019 has implemented and gated scoped queued-overlap omitted-default
  provider/retention/prewarm promotion, but the decision trace has not been
  written yet
  milestone 019 does not automatically rewrite processing core execution mode
  or async worker sizing

current recommended next milestone:
  finish milestone 019 review and decision trace
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

The current runtime explicit candidate contour is:

```text
surface:
  scoped in-process runtime/archive replay surfaces

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async shard transport
  worker count: 4
  worker queue capacity: 8
  provider queue capacity: 8
  retained-byte budget: 536870912
  startup retained payload prewarm: explicit candidate lifecycle only
```

The current accepted readiness answers are:

```text
direct benchmark readiness:
  yes with warnings, broader cache-level, file-level, and small-file direct
  benchmark default readiness is accepted with named scoped warnings

runtime/live readiness:
  explicit opt-in only, queued-owned is runtime-safe when selected explicitly
  for scoped in-process runtime/archive replay surfaces, but it is not
  accepted as the omitted runtime/live ingestion default
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
  accepted only as explicit lifecycle cost; not accepted as hidden omitted
  runtime default behavior

runtime coverage:
  true live ingestion, durable queues, cross-process workers, production
  runtime selection/reporting, and repeated variance gates remain future work

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
the project now has an explicit runtime rollout boundary. Queued-owned can be
used deliberately under runtime guardrails, while default promotion waits for
production runtime selection, operator reporting, repeatability, and true live
or narrower archive-runtime rollout evidence
```

## Remaining Arc

The following stages are not complete. They are the recommended route from the
current state to the intended production-ready result.

### 7. Gradual Runtime Explicit Opt-In Rollout

Recommended next milestone:

```text
gradual runtime rollout for queued-owned explicit opt-in
```

Goal:

```text
turn the milestone 018 explicit-opt-in decision into a production-shaped
runtime rollout path without promoting queued-owned to omitted runtime/live
default
```

Likely required work:

```text
production runtime provider selection surface
operator-visible provider, prewarm, pressure, cancellation, failure, and
cleanup reporting
explicit startup prewarm lifecycle wiring where selected
repeatability gates for startup-prewarmed runtime rows
true live ingestion evidence or a narrower named archive-runtime rollout
target
BlockingBorrowed preserved as explicit fallback/oracle, not automatic silent
fallback
```

Prepared by current state:

```text
runtime lifecycle and pressure/failure guardrails are documented and gated
steady startup-prewarmed candidate rows passed bounded evidence
natural first-use default-readiness blocker is explicit
decision trace already defines the rollout boundary
```

Still not approved:

```text
omitted runtime/live queued-owned default
hidden runtime prewarm
durable queues or brokers
cross-process providers/workers
ordered concurrent rebalance
```

### 8. Durable And Cross-Process Runtime

Future milestone after runtime explicit opt-in rollout.

Goal:

```text
decide whether queued-owned should move into durable queues, brokers,
cross-process providers/workers, or ordered concurrent runtime rebalance
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
in-process runtime explicit-opt-in behavior is bounded and gated
retained pressure, cleanup, release, telemetry, validation, prewarm
attribution, and processing-completeness guardrails can inform durable design
```

### 9. Production Pipeline Integration

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
benchmark and runtime explicit-opt-in evidence provide baseline expectations
for runtime integration, but production integration still needs separate
evidence
```

### 10. Product-Facing Completion

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
runtime explicit-opt-in foundations are increasingly stable, but
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
[next] gradual runtime explicit opt-in rollout
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

# RadarPulse Project Progress

Status: current after milestone 017 closeout.

This file is the project-level progress ledger. Milestone documents remain the
source of detailed architecture, implementation plans, gates, decisions, and
closeouts. This file records the broader arc: what has been achieved, what it
prepared, where the project is now, and what remains before the intended
production-ready result.

## Current Position

RadarPulse has completed the direct archive benchmark default-readiness path
through broader cache-level, file-level, and small-file evidence.

Current state:

```text
completed milestones: 001-017
active milestone: none selected
current accepted benchmark/default posture:
  queued-owned direct/default contour for broader cache-level archive
  rebalance benchmark workloads, file-level MeasureFile workloads, and
  small-file MeasureCache workloads, accepted with named scoped warnings
  retained payload prewarm is enabled for the direct benchmark
  default-equivalent contour and is explicitly attributed outside measured row
  allocation

current recommended next milestone:
  runtime/live ingestion readiness, scoped separately from direct benchmark
  defaults
```

The current accepted direct/default contour is:

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
  retained payload prewarm: enabled for the direct benchmark
    default-equivalent contour
  retained payload prewarm sizing: 65_536 events, 67_108_864 payload bytes,
    1 retained batch
```

The current accepted readiness answer is:

```text
yes with warnings, broader cache-level, file-level, and small-file direct
benchmark default readiness is accepted with named scoped warnings
```

The named warnings carried forward are:

```text
cache-level primary spread warning:
  KTLX 2026-05-04 max-files 220 candidate spread was 12.01%, above the 7.50%
  threshold, while all individual candidate rows remained faster than same-run
  borrowed and safety/allocation guardrails passed

cache-level named-risk timing note:
  KTLX 2026-05-05 max-files 220 had one individual elapsed pair at 1.001x
  borrowed, while the repeated average passed at 0.822x and the larger
  same-shape row passed at 0.810x

file/small-cache prewarm cost:
  retained payload prewarm is a real up-front direct benchmark default cost;
  it is not folded into measured row allocation and remains visible in result
  contracts and CLI output

natural cold allocation note:
  natural unprewarmed MeasureFile and low-count MeasureCache rows remain
  allocation-blocked; the accepted readiness contour is queued-owned rollout
  default plus retained payload prewarm

file-level filesystem timing note:
  fail-level prewarmed elapsed outliers did not reproduce in targeted timing
  repeats, but local file I/O timing variance remains visible

mixed-cache worker-counter follow-up:
  milestone 017 follow-up diagnosed the 221/881 worker failed
  batches/items as SourceOrderViolation from running mixed KINX/KTLX cache
  rows through DefaultSingleRadar; MeasureCache now self-sizes mixed-radar
  source universes and archive rebalance reporting treats processing-invalid
  batches and worker failures as processing-completeness blockers; post-fix
  mixed-cache-all passes with worker failed batches/items 0/0

runtime scope:
  direct benchmark readiness does not approve live ingestion/runtime defaults,
  durable queues, cross-process workers, or ordered concurrent rebalance
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

the next concrete risk is runtime/live ingestion behavior, where prewarm
lifecycle, backpressure, cleanup, fallback, and operator-visible failure
policy must be designed explicitly
```

## Remaining Arc

The following stages are not complete. They are the recommended route from the
current state to the intended production-ready result.

### 6. Runtime And Live Ingestion Readiness

Recommended next milestone:

```text
runtime/live ingestion readiness
```

Goal:

```text
decide whether queued-owned can move beyond direct archive benchmark surfaces
into live ingestion/runtime defaults
```

Why this is next:

```text
milestone 017 closed the file-level and small-file direct benchmark readiness
question with scoped retained payload prewarm
direct benchmark readiness is evidence, not automatic runtime approval
runtime surfaces need explicit lifecycle, startup/prewarm timing,
backpressure, fallback, cleanup, cancellation, and observability contracts
```

Expected evidence:

```text
runtime input and lifecycle contract audit
live ingestion provider selection rules
runtime pressure and backpressure policy
operator-visible fallback and failure behavior
runtime cleanup, cancellation, release, and validation guardrails
Release gates over runtime-shaped workloads
explicit statement that direct benchmark readiness is only one input
```

Prepared by current state:

```text
cache-level, file-level, and small-file benchmark/default readiness are
accepted with named scoped warnings
BlockingBorrowed oracle posture and fail-closed behavior are preserved
prewarm attribution exists in result contracts and CLI output
processing completeness is now visible and gateable
```

### 7. Durable And Cross-Process Runtime

Future milestone after runtime/live ingestion readiness.

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
direct API fallback/oracle semantics are clear
retained pressure, cleanup, release, telemetry, validation, prewarm
attribution, and processing-completeness guardrails can inform durable design
```

Still not approved:

```text
live ingestion/runtime defaults
durable queues
brokers
cross-process providers/workers
ordered concurrent rebalance
builder-transfer
```

### 8. Durable And Cross-Process Runtime

Future milestone after runtime readiness architecture.

Goal:

```text
make the runtime provider/worker story durable and operationally safe across
process boundaries, failures, restarts, and operator actions
```

Likely required work:

```text
durable queue or broker design
cross-process provider/worker transport
message ownership and acknowledgement semantics
restart/recovery behavior
poison/failure policy
ordering and idempotency policy
operator controls and observability
performance gates that include durable transport overhead
```

Prepared by current state:

```text
the in-process queued-owned contour and fallback/oracle posture are understood
benchmark and cache-level behavior is bounded enough to serve as a baseline
```

Still unresolved:

```text
durability semantics are not designed
cross-process worker state is not designed
runtime recovery is not certified
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
benchmark evidence and guardrails will provide baseline expectations for
runtime integration, but production integration still needs separate evidence
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
the backend data, replay, processing, rebalance, and provider default
foundation is increasingly stable, but product-facing scope has not yet been
the main milestone target
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
[next] runtime/live ingestion readiness
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

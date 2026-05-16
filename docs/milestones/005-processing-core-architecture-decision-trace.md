# Milestone 005: Decision Trace

## 1. What Was Implemented

Milestone 005 implemented the first static processing core over the milestone
004 normalized stream:

- `RadarProcessingCore` with explicit options, execution modes, metrics,
  validation result, processing result, and optional partitioned telemetry.
- Direct consumption of `RadarEventBatch` without changing the stream contract,
  archive replay, identity normalization, or batch construction.
- Static `SourceId -> PartitionId -> ShardId` topology with contiguous source
  blocks and externally observable partition assignments.
- Dense source-local processing state sized by `RadarSourceUniverse.SourceCount`.
- Processing payload reader helpers for 8-bit and 16-bit big-endian payloads.
- Sequential processing mode as the first correctness baseline.
- Synchronous `PartitionedBarrier` mode that routes events to static shards and
  completes all shard loops before returning.
- Partition and shard telemetry for routed work.
- Processing-output validation helpers outside the hot path.
- Source-local handler slots with declared snapshot fields and dense per-source
  `long`/`double` storage.
- Synthetic processing-only benchmark support over prebuilt deterministic
  `RadarEventBatch` workloads.
- CLI benchmark command for manually exercising the processing core:
  `radarpulse processing benchmark synthetic`.

Verified Release processing-only result:

```text
workload:
  synthetic prebuilt RadarEventBatch
  sources: 32_400
  stream events per iteration: 32_400
  payload values per stream event: 1_196
  payload values per iteration: 38_750_400
  measured iterations: 20
  warmup iterations: 3

mode              handlers          payload values/s   stream events/s   allocated bytes / payload value
sequential        none              2_559_218_888.23   2_139_815.12      0.00
partitioned 24/24 none              2_622_669_443.85   2_192_867.43      0.03
sequential        counter-checksum  1_630_968_124.27   1_363_685.72      0.03
partitioned 24/24 counter-checksum  1_745_635_000.27   1_459_561.04      0.06
```

Compared with milestone 004 normalized stream throughput, the processing-only
baseline is roughly `2.95x` to `4.74x` faster than the single-file normalized
stream baseline and `3.20x` to `5.15x` faster than the cache-wide normalized
stream baseline on the comparable payload-value metric.

## 2. Decision Matrix

### Processing Core Consumes RadarEventBatch Directly

Decision: make `RadarProcessingCore` consume `RadarEventBatch` as its input
boundary.

Why chosen: milestone 004 already defined a compact, deterministic, versioned,
payload-aware stream contract. The processing core should build on that
boundary instead of reopening archive parsing or identity normalization.

Alternatives: parse Archive Two data inside the core, consume milestone 003
publisher events, or introduce a second processing-specific event shape.

Rejected because: archive parsing would couple layers, publisher events are too
semantic for the hot path, and another event shape would duplicate the
milestone 004 contract.

Trade-offs/debt: processing correctness depends on upstream stream validation.
File-backed processing benchmarks must keep replay construction timing separate
from processing timing.

Review explanation: "The core starts where the normalized stream ends. That
keeps archive replay, input construction, and processing independently
measurable."

### Static Source-To-Shard Topology First

Decision: implement immutable static ownership:
`SourceId -> PartitionId -> ShardId`.

Why chosen: the first processing baseline needed deterministic ownership,
direct diagnostics, and stable source-local state before live movement of
partitions could be designed safely.

Alternatives: implement live shard rebalance now, assign sources dynamically by
load, route by modulo only, or let handlers choose ownership.

Rejected because: live rebalance requires measured baseline behavior first,
dynamic assignment complicates ordering and state ownership, modulo-only
routing weakens diagnostics, and handler-owned routing would blur core
responsibility.

Trade-offs/debt: skewed source load is not corrected in milestone 005.
Partition-level shard rebalance is deliberately the next milestone.

Review explanation: "I made ownership boring and visible first. Rebalance can
then move partitions later without changing what a source-local state owner
means."

### Dense Source-Local State

Decision: store processing state in dense arrays indexed directly by
`SourceId`.

Why chosen: `RadarSourceUniverse` gives dense source IDs, so direct indexing
keeps state updates cache-conscious and avoids per-source object lookup on the
hot path.

Alternatives: dictionaries keyed by source, per-source objects, handler-owned
state maps, or state grouped only by physical radar.

Rejected because: those options add lookup overhead, allocation, weaker
locality, or too-coarse ownership for tens of thousands of logical sources.

Trade-offs/debt: source-universe size directly controls state-array size.
Future persisted state will need migration rules when source-universe layout
changes.

Review explanation: "The dense source universe pays off only if processing
state is also dense and directly indexed."

### Source-Local Handler Slots

Decision: add a small source-local handler extension model with precomputed
slot layout and declared snapshot fields.

Why chosen: milestone 005 needed a way to exercise real per-source algorithm
state without inventing a full algorithm framework. Dense `long`/`double` slots
give handlers mutable state while keeping ownership local to a source.

Alternatives: hard-code only counters in the core, let handlers allocate their
own dictionaries, expose raw state arrays directly, or build a broad plugin
system now.

Rejected because: hard-coded counters do not test extensibility, handler maps
add hot-path overhead, raw arrays expose too much internal layout, and a plugin
system is premature.

Trade-offs/debt: supported slot types are intentionally narrow. More complex
algorithm state will need deliberate expansion rather than ad hoc object
storage.

Review explanation: "Handlers can prove source-local mutation and snapshots
without turning the processing core into an algorithm framework."

### Payload Reader Boundary

Decision: use processing-side payload reader helpers to compute payload value
counts and raw checksums from `RadarEventBatch` payload references.

Why chosen: the core needs deterministic payload metrics and handler payload
spans, but the input contract already owns payload storage and word-size
metadata.

Alternatives: trust only cached batch metrics, decode payloads in each handler,
or copy payload values into per-event arrays.

Rejected because: aggregate batch metrics cannot update source-local state,
per-handler decoding duplicates work, and per-event arrays would inflate memory
and allocation.

Trade-offs/debt: payload reader validation remains mandatory. The reader passes
`RadarStreamEvent` by value to avoid by-reference lifetime ambiguity around
returned spans.

Review explanation: "Payload decoding stays close to the batch contract and
does not become hidden handler work."

### Sequential Baseline Before Partitioned Mode

Decision: implement and test sequential processing before partitioned
execution.

Why chosen: sequential mode provides the simplest correctness reference for
metrics, snapshots, validation, timestamp ordering, and handler behavior.

Alternatives: build partitioned execution first, or only test partitioned mode
against aggregate batch metrics.

Rejected because: partitioned failures are harder to diagnose without a simple
reference path, and aggregate metrics alone cannot prove source-local behavior.

Trade-offs/debt: sequential mode is not the final scaling strategy, but it
remains useful for validation and regression tests.

Review explanation: "The sequential path is the reference implementation. The
partitioned path must earn parity against it."

### Synchronous PartitionedBarrier Mode

Decision: implement `PartitionedBarrier` as a synchronous completion-barrier
mode in milestone 005.

Why chosen: the milestone needed to validate static routing, shard ownership,
telemetry, source-local ordering, and leased batch safety before adding worker
queues or retained async transport.

Alternatives: introduce real worker threads now, queue retained work past the
call boundary, or delay partitioned mode entirely.

Rejected because: worker execution would mix scheduling concerns with routing
correctness, retained queues require a new payload lifetime protocol, and
delaying partitioned mode would leave the topology unproven.

Trade-offs/debt: current partitioned benchmarks measure routing/barrier/shard
loop cost, not multi-core scaling. Worker execution remains future work.

Review explanation: "The barrier mode proves the partitioning contract without
breaking the leased batch lifetime rule."

### Telemetry And Output Validation Outside The Hot Path

Decision: expose processing telemetry and validation as read-side result
surfaces and helper APIs.

Why chosen: milestone 005 needed diagnostics for partition/shard load,
missing work, duplicate work, source ordering, and metric parity without
forcing heavyweight validation into every hot-path operation.

Alternatives: validate every invariant inside the hot loop, expose routed event
indexes publicly, or rely only on tests.

Rejected because: hot-loop validation would distort the measured core cost,
public event indexes would leak implementation details, and tests alone would
not provide runtime diagnostics.

Trade-offs/debt: validation helpers are explicit calls. Production callers must
decide when to pay the diagnostic cost.

Review explanation: "Telemetry tells us what the core did; validation can audit
that result without becoming the steady-state processing path."

### Processing-Only Benchmark Boundary

Decision: measure processing over prebuilt deterministic `RadarEventBatch`
workloads and exclude replay construction work.

Why chosen: milestone 004 already measured normalized stream construction. The
milestone 005 question is whether the core can process already-built batches
fast enough.

Alternatives: benchmark end-to-end archive replay plus processing, use external
process timing only, or compare stream events/s directly with older per-gate
publisher numbers.

Rejected because: end-to-end timings hide the processing core behind upstream
cost, process timing includes startup noise, and stream events/s is not
comparable to payload-value publisher metrics.

Trade-offs/debt: synthetic workloads are controlled and deterministic, not a
replacement for future file-backed measurements. File-backed measurements must
report replay and processing timing separately.

Review explanation: "The benchmark answers one question: once a batch exists,
how expensive is processing it?"

### Performance Interpretation

Decision: compare milestone 005 processing-only throughput against milestone
004 payload values/s, while explicitly stating that `PartitionedBarrier` is not
yet a multi-core scaling result.

Why chosen: payload values/s is the comparable denominator across the stream
and processing milestones. The partitioned path currently routes work and
iterates shard loops synchronously.

Alternatives: report partitioned throughput as parallel speedup, compare
against stream events/s, or ignore allocation ratios while throughput is high.

Rejected because: that would overstate the concurrency result, use the wrong
denominator, or hide the routing-buffer allocation cost.

Trade-offs/debt: processing throughput has a large margin over normalized
stream construction, but routing allocation remains visible in the partitioned
path.

Review explanation: "The processing core is not the current throughput
bottleneck, but the benchmark also shows exactly where the next allocation debt
lives."

## 3. Remaining Risks And Debt

- `PartitionedBarrier` is synchronous; it validates routing and barrier
  semantics but does not prove multi-core worker scaling.
- Partitioned routing allocates visible buffer storage: latest measurements are
  `40.33` allocated bytes per stream event without handlers and `72.33` with
  the counter/checksum handler workload.
- Live shard rebalance, partition migration, and source-state transfer are not
  implemented in milestone 005. They are the planned milestone 006 focus.
- Retained async transport is not implemented. Leased batches remain valid only
  during the synchronous processing call.
- The handler slot model is intentionally narrow and source-local. Complex
  radar algorithms will need explicit design rather than hidden object state.
- File-backed processing benchmarks remain future work unless they keep replay
  construction and processing timing separate.

## 4. Portfolio Review Summary

Milestone 005 converted the milestone 004 normalized stream into a measured
processing-core boundary. The main decisions were direct `RadarEventBatch`
consumption, static source-to-shard ownership, dense source-local state,
source-local handler slots, sequential reference processing, synchronous
partitioned barrier execution, read-side telemetry and validation, and a
processing-only benchmark contour. The result is a core that processes prebuilt
batches several times faster than the current normalized stream construction
baseline while preserving explicit payload lifetime and leaving partition-level
shard rebalance as the next clean milestone.

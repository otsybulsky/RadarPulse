# Milestone 005: Processing Core Architecture

Status: draft.

RadarPulse milestone 005 starts from the closed milestone 004 normalized stream
contract and defines the first architecture for the high-performance processing
core that consumes that stream.

This document is intentionally not an implementation plan. It records the core
model, architectural boundaries, concurrency constraints, processing-state
shape, and measurement rules before any task breakdown is written.

## Milestone Goal

Milestone 005 should define the architecture of the first RadarPulse processing
core over the normalized `RadarEventBatch` stream.

The output of the milestone is the architectural definition of:

```text
processing-core batch consumption boundary
source-local dense state layout
partition and shard ownership model
explicit leased-batch lifetime and retention boundary
algorithm and feature-handler extension boundary
processing telemetry and backpressure signals
processing-only benchmark boundary
```

The resulting core must consume already-normalized numeric stream batches, must
keep per-source state dense and cache-conscious, must preserve the milestone
004 batch lifetime contract, and must provide a measured foundation for later
radar algorithms.

Milestone 005 is not about new Archive Two parsing, stream normalization,
dictionary registration, live ingestion, durable transport, visualization, or a
full meteorological algorithm suite. It is about defining the processing engine
that receives the canonical input stream produced by milestone 004.

## Expected Outcome

At the end of milestone 005, RadarPulse should have a clear processing-core
architecture that can be implemented without reopening the milestone 004 stream
contract.

The expected result is:

```text
an accepted processing-core boundary over RadarEventBatch
a source-based partition and shard model
a dense source-local state model sized by RadarSourceUniverse
an explicit payload lifetime model for leased and retained batches
a safe first execution baseline with completion-barrier semantics
a future-compatible retained-lease async model
a source-local algorithm and feature-handler extension model
a processing-only benchmark contract
a processing validation contract
a telemetry model for shard load, partition load, and backpressure
a handoff position that makes partition-level shard rebalance the next milestone
```

The most important architectural result is the separation of three concerns:

```text
stream construction:
  Archive replay creates normalized RadarEventBatch values.

processing:
  The core consumes RadarEventBatch values and mutates source-local state.

load management:
  Static partition-to-shard assignment is established now; live rebalance is
  deliberately left to the next milestone.
```

Milestone 005 should leave RadarPulse with a stable answer to these questions:

```text
What is the processing-core input?
Who owns payload memory while the core reads it?
How does SourceId map to processing ownership?
Where does mutable source state live?
How do algorithms attach source-local state without per-event allocation?
What telemetry proves the core is keeping up?
What benchmark measures processing rather than replay construction?
What invariants must 006 preserve when rebalance is added?
```

Milestone 005 is successful when the processing core can be reasoned about as a
consumer of the milestone 004 stream, with static partition ownership and
explicit payload lifetime semantics. It should not require a final answer for
live migration, retained transport, or complex radar algorithms.

## Current Status

Milestone 004 produced a deterministic, versioned, source-addressable input
surface:

```text
Archive replay
  -> normalized RadarEventBatch stream
  -> dense SourceId on every RadarStreamEvent
  -> versioned dictionary and source-universe visibility
  -> owned or leased payload lifetime
```

That surface is ready for a processing core, but the existing stream benchmark
still measures replay construction throughput. It includes archive file I/O,
BZip2 decompression, Archive Two scanning, Type 31 projection, identity
normalization, batch construction, payload copying or leasing, and counting.

Milestone 005 must introduce a separate processing-only interpretation:

```text
already-built RadarEventBatch
  -> processing-core routing
  -> source-local state updates
  -> algorithm or feature handler updates
  -> processing telemetry and snapshots
```

The next core benchmark must therefore avoid claiming archive replay throughput
as processing-core throughput. The comparable measurement is how quickly the
core consumes existing `RadarEventBatch` values under a defined algorithm load.

## Architectural Principles

The processing core should follow these architectural principles:

```text
dense numeric source ids make direct array state practical
shard-owned state avoids cross-thread mutation for one logical source
source-local SPSC queues can provide strong streaming throughput
custom handlers should use preallocated typed slots, not per-source objects
snapshot projection should stay outside the per-event hot path
backpressure telemetry must be treated as a first-class correctness signal
live rebalance should be partition-oriented, not arbitrary source ownership flips
```

The central constraint is batch payload lifetime. RadarPulse stream events
reference payload bytes owned by a `RadarEventBatch`. A leased
`RadarEventBatch` is valid only during the synchronous publication callback
unless an ownership protocol explicitly extends that lifetime.

Therefore RadarPulse must not enqueue `RadarStreamEvent` values for background
processing unless the referenced payload storage is also retained safely. The
first processing-core baseline can use a synchronous completion barrier:

```text
producer invokes processing core with a RadarEventBatch
core routes and processes all required work for the batch
core returns only after all consumers are done reading leased payload storage
producer may then reuse leased buffers
```

This is a safety baseline, not a permanent architectural limit. Asynchronous
queues can be introduced later, but they require owned snapshots or an explicit
retained-lease protocol. Hidden queueing of leased payload references is not a
valid optimization.

## Fundamental Model

The processing core is organized around logical sources, not physical radar
feeds and not archive records.

```text
SourceId = dense source-local processing identity
MomentId = channel inside a source
RadarStreamEvent = source-local gate-run update
RadarEventBatch = chronological multi-source input unit
Source state = mutable processing state owned by one partition or shard
Algorithm state = handler-owned state attached to source state
```

One physical radar can produce tens of thousands of logical sources. The core
should treat `SourceId` as the primary state key and should use the
`RadarSourceUniverse` to size direct arrays, validate source dimensions, and
interpret source blocks.

The processing core should not repeat milestone 004 normalization work:

```text
no radar-code text lookup
no moment-name text lookup
no source-id derivation from text
no dictionary mutation during processing
```

If a consumer needs text, it belongs at a diagnostics, snapshot, or publication
boundary using the batch dictionary version and published dictionary snapshot.

## Core Boundary

The processing core consumes `RadarEventBatch` values as the canonical input.

Conceptually:

```text
RadarEventBatch
  -> validate compatible schema/source universe if requested
  -> route each RadarStreamEvent by SourceId
  -> read raw payload values through the event payload reference
  -> update source-local state
  -> update configured algorithm or feature handlers
  -> publish metrics or snapshots outside the hot path
```

The core boundary should be stream-oriented, not archive-oriented. It should not
know about Archive Two compressed records, NEXRAD file paths, replay sessions,
or decompressor choices.

The processing boundary should also be deterministic. For the same batch
sequence, same source universe, same configured algorithms, and same initial
state, the core should produce the same counters, checksums, and snapshots.

## Batch Lifetime Boundary

Milestone 004 introduced explicit batch lifetime:

```text
Owned:
  safe to retain after publication

Leased:
  valid only during the synchronous publish callback
```

Milestone 005 must preserve this rule as a processing-core invariant.

The first core baseline may treat `Publish(batch)` or equivalent synchronous
delivery as a completion barrier. In that mode, all payload reads from a leased
batch must complete before control returns to the producer.

Valid barrier-based leased-batch processing:

```text
receive leased batch
route work to owned worker threads for this batch
wait for all workers to finish reading batch events and payload
return to producer
```

Invalid leased-batch processing:

```text
receive leased batch
enqueue RadarStreamEvent references to a background queue
return to producer
worker later reads PayloadOffset/PayloadLength against reused payload storage
```

If retained or asynchronous processing is required, the retaining component must
convert to an owned snapshot or use a retained-lease protocol with explicit
ownership and release semantics.

Retained asynchronous processing should be modeled as an ownership protocol, not
as an implicit queueing detail:

```text
producer creates or receives a leased batch
retaining component acquires a batch lease
work items reference the lease plus event ranges or partition ids
workers release the lease after their final payload read
producer-owned buffers return to reuse only after the last release
```

The retained mode must be bounded. It should expose maximum in-flight batches,
maximum retained payload bytes, release-on-exception behavior, cancellation
semantics, and shutdown behavior. A retained lease that can be forgotten is
equivalent to an unbounded memory leak and is not an acceptable processing
contract.

## Source State Layout

The processing core should prefer dense state arrays over per-source objects.

For a configured source universe:

```text
SourceCount = RadarSourceUniverse.SourceCount
source state arrays are sized by SourceCount
SourceId is the direct index or direct offset input
```

This gives the core a stable, cache-conscious layout:

```text
sourceState[sourceId]
sourceMetrics[sourceId]
sourceAlgorithmSlots[sourceId * slotWidth + localSlot]
activeSourceFlags[sourceId]
```

The exact state fields belong to later design, but the architectural rule is
that source-owned mutable state should be directly addressable by dense
`SourceId`. Sparse maps, text keys, and per-source object graphs should stay
outside the hot path unless a later algorithm proves they are necessary.

The source state should be owned by one processing partition or shard at a
time. Cross-thread mutation of the same source state is not a valid default.

## Event Interpretation

`RadarStreamEvent` represents a source-local run of raw radar values.

The processing core should interpret an event by combining:

```text
event.SourceId
event.RadarOrdinal
event.MomentId
event.ElevationSlot
event.AzimuthBucket
event.RangeBand
event.GateStart
event.GateCount
event.WordSize
event.Scale
event.Offset
event.StatusModel
batch.Payload[event.PayloadOffset..event.PayloadOffset + event.PayloadLength]
```

The canonical payload remains raw radar values. Calibration is a derived
operation from explicit event metadata. The first core should make this
distinction visible so algorithms can choose whether to work over raw values,
classified values, calibrated values, or aggregate statistics.

Event interpretation should avoid per-gate allocations. A handler that needs
per-gate work should iterate over the payload span directly and update dense
state or counters in place.

## Partition And Shard Model

The first processing-core partition model should be source based.

The simplest stable model is:

```text
SourceId -> PartitionId -> ShardId
```

For an initial core, `PartitionId` can be derived cheaply:

```text
PartitionId = SourceId % PartitionCount
```

or by contiguous source blocks:

```text
PartitionId = SourceId / SourcesPerPartition
```

The exact mapping can be chosen later, but the architectural requirements are:

```text
one source is owned by one partition at a time
one partition is assigned to one shard at a time
state for a source moves only through an explicit ownership protocol
routing reads are cheap and deterministic
partition assignment is externally observable for diagnostics
```

For the first architecture baseline, live migration is not required. A static
partition-to-shard assignment is enough to establish the core boundary,
processing-state layout, telemetry, and benchmark method.

## Parallelism And Retention

Milestone 005 should distinguish parallel processing from payload retention.
Synchronous completion is one safe baseline for leased payloads, but it is not
the only possible future execution model.

Parallelism may happen inside the completion barrier:

```text
split batch events by partition or shard
process shard-local work on worker threads
wait for all shard workers to finish
return to producer
```

This allows the core to use multiple cores without retaining leased payload
references after the callback returns.

The first design should prefer bounded, explicit fan-out over hidden
producer-consumer queues. The core should be able to report whether the current
batch completed inside the barrier, whether workers lagged, and whether any
shard became a bottleneck.

Later low-latency ingestion may still use shard-local SPSC queues, but only if
the input payload lifetime is owned by the queue, moved into queue-owned
storage, or protected by a retained-lease protocol.

The future asynchronous shape should be explicit:

```text
BatchLease
  RadarEventBatch
  retained payload ownership
  bounded reference count or equivalent completion tracking
  release path that returns storage to the producer or pool

Partition work item
  BatchLease
  event range or partition id
```

This lets the producer and processing workers overlap work without copying the
payload by default. It also makes the real cost visible: retained memory,
in-flight work, cancellation, and worker failure handling.

## Ordering Semantics

Milestone 004 defines the input batch as a chronological multi-source sequence.
Milestone 005 should preserve two separate ordering ideas:

```text
batch order:
  canonical radar chronology across all events

source order:
  event order observed by one SourceId
```

For source-local state, preserving source order is mandatory. Events for the
same `SourceId` must be applied in the same order they appear in the canonical
batch sequence.

Across different sources, the core may process independent partitions in
parallel if the configured algorithms do not require cross-source chronology.
Any future algorithm that needs cross-source chronological joins must declare
that requirement explicitly.

The first processing core should not reorder the canonical batch as a side
effect of routing. If routing groups work internally, the externally visible
processing metrics and validation checksums should still be explainable against
the original batch order.

## Algorithm And Feature Handler Boundary

The core should support a small, explicit extension boundary for source-local
algorithms and feature extraction.

The source-local handler model should use this shape:

```text
one configured handler instance
many source-local state slots
handler metadata prepared once during core construction
handler hot path receives event metadata, payload span, and source state
handler snapshot projection happens outside per-event mutation
```

Handlers should not store source-specific mutable state in instance fields.
Per-source mutable state belongs in dense slot storage owned by the processing
core.

The handler boundary should be allocation-free in the hot path:

```text
no per-event dictionary lookup
no per-event field-name lookup
no per-event boxing
no per-source handler object allocation
```

Snapshot flexibility is allowed on the read side. A diagnostic snapshot may
project handler slots into named fields, but those names should not participate
in hot-path mutation.

The first handlers should be deliberately simple. Good candidates are
deterministic counters, payload checksums, source activity windows, raw-value
classification counts, or small aggregate statistics. Complex meteorological
algorithms should build on this boundary after the core behavior is measured.

## Source Snapshots

The processing core should expose source snapshots for diagnostics and later
downstream use.

A source snapshot is a read-side projection of:

```text
source identity and version metadata
base source processing state
algorithm or feature-handler state
recent activity counters
optional validation checksums
```

Snapshots should be externally interpretable with:

```text
StreamSchemaVersion
DictionaryVersion
SourceUniverseVersion
RadarStreamDictionarySnapshot
RadarSourceUniverse
```

Snapshot building is not the hot path. It may allocate modest read-model
objects if needed, as long as per-event processing state remains dense and
allocation-free.

## Telemetry And Backpressure

The processing core should make backpressure visible before it becomes a
correctness or benchmark problem.

Core telemetry should distinguish:

```text
input batch count
input stream event count
input payload value count
processed batch count
processed stream event count
processed payload value count
per-shard event count
per-shard payload value count
per-shard processing time
max shard batch time
active source count
hot source or hot partition indicators
worker wait time
queue depth or deferred-work depth, if queues exist
processing errors
```

For barrier-based processing, the key backpressure signal is not a channel
depth alone. It is whether processing time per batch approaches or exceeds the
producer's ability to deliver batches.

For any future queued processing mode, queue depth, wait time, dropped work,
retained batch count, retained payload bytes, lease wait time, and retained
payload ownership must all be reported explicitly.

## Benchmark Boundary

Milestone 005 benchmarks should be processing-only.

The benchmark input should be one of:

```text
prebuilt owned RadarEventBatch values
reusable leased batch delivery inside a controlled completion barrier
retained-lease async delivery with bounded in-flight batches
synthetic RadarEventBatch values with deterministic payload and source layout
```

The benchmark should exclude:

```text
archive file enumeration
BZip2 decompression
Archive Two message scanning
identity normalization
dictionary registration
batch construction
CLI output formatting
```

The benchmark should report at least:

```text
processed batches/s
processed stream events/s
processed payload values/s
allocated bytes / stream event
allocated bytes / payload value
per-shard event distribution
per-shard processing-time distribution
active source count
algorithm load configuration
validation checksum
```

The benchmark should always name the configured handler set. A no-op core,
counter-only core, and calibrated-value aggregate core are different workloads
and should not be compared without labeling the algorithm load.

## Validation Boundary

The first processing core should have deterministic validation hooks.

Validation should be able to prove:

```text
all input events were processed exactly once
all payload values required by the configured workload were read exactly once
source-local order was preserved
source ownership was respected
sequential and parallel core modes produce equivalent counters/checksums
owned and leased batch inputs produce equivalent results
```

The validation layer should remain outside the hot path unless explicitly
enabled. Checksums and diagnostics are essential for confidence, but they should
not silently define the production performance cost.

## Rebalance Boundary

Live rebalance is not part of milestone 005.

The architecture should still leave room for future rebalance by using the
`SourceId -> PartitionId -> ShardId` model. Partition-level movement is the
preferred future direction because it gives a bounded ownership unit.

Direct live source-level ownership flips are not the default direction. They
are risky in streaming topologies because old events for a source can still be
queued on the previous owner while the ownership table already points to a new
owner.

Future rebalance must be explicit:

```text
mark partition as migrating
stop or buffer routing for that partition
drain or quiesce source ownership
move source state
publish new partition owner
replay buffered work in order
resume normal routing
```

This is future architecture work. Milestone 005 should avoid paying migration
overhead on every event before a baseline static-partition core is measured.

The next milestone after the static processing-core baseline should focus on
partition-level shard rebalance. That milestone should build on the telemetry,
ownership, and partition substrate defined here instead of adding richer radar
algorithms first. The intended sequence is:

```text
005:
  static partitioned processing core
  explicit payload lifetime boundary
  source-local state ownership
  per-shard and per-partition telemetry

006:
  partition-level shard rebalance architecture
  migration safety protocol
  bounded buffering or quiescence rules
  state handoff validation
  overload-relief telemetry
```

This keeps load-leveling concerns close to the processing-core architecture
while still avoiding rebalance complexity before the static baseline is
measured.

## Architectural Boundaries

Milestone 005 should keep these boundaries explicit:

```text
Input stream boundary:
  The core consumes RadarEventBatch, not Archive Two records.

Lifetime boundary:
  Leased payloads are read only while a completion barrier or retained lease
  keeps their storage valid.

Source ownership boundary:
  One source is mutated by one partition or shard at a time.

Partition boundary:
  Routing uses SourceId -> PartitionId -> ShardId.

State boundary:
  Source and handler state are dense arrays, not per-source object graphs.

Algorithm boundary:
  Handlers receive numeric event metadata and payload spans, not text identities.

Snapshot boundary:
  Read models may be flexible; hot-path mutation remains fixed and typed.

Telemetry boundary:
  Processing throughput and backpressure are measured separately from replay.

Rebalance boundary:
  Static partitioning comes first; partition-level migration is the next
  milestone after the baseline core.
```

Keeping these boundaries visible prevents the archive replay producer, the
normalized stream contract, the processing core, and future live transport from
collapsing into one hard-to-measure subsystem.

## Non-Goals For This Document

This document does not define:

```text
implementation tasks
class or method names
CLI commands
file formats
specific worker-thread scheduling APIs
full meteorological algorithms
live ingestion
durable broker integration
shared-memory transport
visualization
live partition migration implementation
```

Those belong in later design or plan documents once the processing-core
architecture is accepted.

## Baseline Architectural Position

RadarPulse should move from a stable normalized input stream to a processing
core with dense source-local state and an explicit payload lifetime boundary:

```text
RadarEventBatch
  -> explicit processing-core lifetime boundary
  -> SourceId-based partition routing
  -> shard-owned dense source state
  -> allocation-free handler updates over event metadata and payload spans
  -> deterministic processing metrics and source snapshots
```

The first core should prioritize correctness of ownership, batch lifetime,
source-local ordering, and benchmark separation. The first baseline may use a
synchronous completion barrier, but the architecture should leave room for a
bounded retained-lease asynchronous mode. Once that baseline is measured, the
next milestone should address partition-level shard rebalance before richer
algorithm work or downstream transport is allowed to complicate the processing
core.

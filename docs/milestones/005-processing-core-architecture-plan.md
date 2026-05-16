# Milestone 005: Processing Core Architecture Plan

Status: draft.

This plan implements the milestone 005 architecture defined in
`005-processing-core-architecture.md`.

The plan is intentionally scoped to the first static partitioned processing core.
It should not implement live shard rebalance, live ingestion, durable transport,
visualization, or complex radar algorithms. Those depend on a measured and
validated processing baseline.

## Goal

Milestone 005 implements the first RadarPulse processing core over the
milestone 004 normalized stream:

```text
RadarEventBatch
  -> explicit payload lifetime boundary
  -> SourceId partition routing
  -> shard-owned dense source state
  -> source-local handler updates
  -> processing-only metrics, validation, snapshots, and benchmarks
```

The milestone must preserve the milestone 004 stream contract. The processing
core consumes `RadarEventBatch`; it does not change stream normalization,
dictionary registration, source-universe derivation, Archive Two replay, or
batch construction.

## Starting Point

Milestone 004 is complete and provides:

```text
RadarEventBatch
RadarStreamEvent
RadarEventBatchLifetime
RadarEventBatchMetrics
RadarEventBatchValidator
RadarSourceUniverse
RadarStreamDictionarySnapshot
IArchiveRadarEventBatchPublisher
NexradArchiveRadarEventBatchPublishSession
archive stream and archive benchmark stream commands
```

Important existing constraints:

```text
RadarStreamEvent is a 64-byte unmanaged value type
RadarEventBatch payload references are batch-local
Leased RadarEventBatch values are valid only during the synchronous callback
retained consumers must call ToOwnedSnapshot() unless a later lease protocol exists
stream benchmark throughput measures replay construction, not core processing
```

## Target Implementation Shape

The implementation should introduce a new processing boundary without
entangling it with archive infrastructure.

Candidate layering:

```text
src/Domain/Processing
  processing state, metrics, snapshots, validation results, handler contracts

src/Application/Processing
  processing-core interfaces and options if an application boundary is needed

src/Infrastructure/Processing
  worker scheduling, reusable routing buffers, benchmark harnesses
```

The exact file layout can follow the repository's current conventions, but the
ownership boundary must remain clear:

```text
Archive infrastructure produces RadarEventBatch.
Processing core consumes RadarEventBatch.
Processing handlers mutate source-local state.
Benchmarks measure processing separately from replay construction.
```

## Implementation Slices

### 1. Processing Core Contracts

Introduce the first processing-core API over `RadarEventBatch`.

Candidate types:

```text
RadarProcessingCore
RadarProcessingCoreOptions
RadarProcessingExecutionMode
RadarProcessingResult
RadarProcessingMetrics
RadarProcessingValidationResult
```

Required behavior:

```text
accept RadarEventBatch as input
require a compatible RadarSourceUniverse
make processing mode explicit
return deterministic counters and checksums
make cancellation explicit
avoid archive-specific names or dependencies
```

Initial execution modes:

```text
Sequential:
  one thread processes the batch in canonical event order

PartitionedBarrier:
  routes events to static partitions/shards and waits for batch completion
```

Expected tests:

```text
core rejects null inputs and invalid options
core rejects unsupported stream schema version
core rejects source-universe mismatch
empty batch produces deterministic empty result
sequential result counters match RadarEventBatchMetrics where applicable
```

### 2. Static Partition Topology

Implement static source ownership:

```text
SourceId -> PartitionId -> ShardId
```

The topology should be immutable for milestone 005. It should support at least
one deterministic partitioning strategy:

```text
contiguous source blocks
```

A modulo strategy can be added if it helps test distribution, but contiguous
blocks should be the preferred baseline because they preserve source-universe
locality and make diagnostics easier to read.

Required behavior:

```text
PartitionCount > 0
ShardCount > 0
PartitionCount >= ShardCount unless a deliberate single-shard mode is used
every SourceId maps to exactly one partition
every partition maps to exactly one shard
assignment is externally observable
topology is validated against RadarSourceUniverse.SourceCount
```

Expected tests:

```text
all SourceId values map inside the partition range
all partitions map inside the shard range
contiguous mapping covers every SourceId exactly once
invalid topology values are rejected
same topology produces stable assignments across runs
```

### 3. Dense Source State Store

Create dense source-local state storage sized by the source universe.

Initial state should stay minimal:

```text
source processed event count
source processed payload value count
source raw value checksum
source last message timestamp
source last event sequence/check value for ordering validation
active source marker
```

The exact fields can be refined during implementation, but storage should remain
dense and directly indexed by `SourceId`.

Required behavior:

```text
state arrays are allocated once per core/session
SourceId is the direct lookup key
state is owned by one shard through the static partition topology
no per-source object allocation on the hot path
snapshots are read-side projections, not hot-path state objects
```

Expected tests:

```text
source state arrays are sized to RadarSourceUniverse.SourceCount
processing one source updates only that source state
processing multiple sources preserves source isolation
active source count is deterministic
snapshot reflects processed state without mutating it
```

### 4. Source-Local Handler Slot Platform

Add a small extension boundary for source-local algorithms.

Candidate contract:

```text
IRadarSourceProcessingHandler
RadarSourceProcessingHandlerDescriptor
RadarSourceProcessingState
RadarSourceProcessingSnapshotBuilder
```

The exact names can change, but the model should be:

```text
one configured handler instance
many source-local state slots
precomputed int64/double slot offsets per handler
handler receives event metadata and payload span
handler mutates only the source-local state view it is given
snapshot projection happens outside the per-event hot path
```

Required hot-path constraints:

```text
no per-event dictionary lookup
no per-event field-name lookup
no per-event boxing
no per-source handler object allocation
no text identity lookup
```

Initial handler candidates:

```text
event count handler
payload value count handler
raw value checksum handler
raw value classification count handler
```

Expected tests:

```text
handler slot offsets do not overlap
duplicate snapshot field names are rejected
handler instance state is not source-specific
per-source handler state remains isolated
snapshot projection can expose handler fields
no configured handlers keeps the base path valid
```

### 5. Payload Reader Helpers

Add processing-side helpers for reading `RadarStreamEvent` payload spans.

Required behavior:

```text
read 8-bit payload values
read 16-bit payload values
honor GateCount and WordSize
avoid per-gate allocation
share interpretation with RadarEventBatchMetrics where possible
```

The helper should make raw-value interpretation explicit. Calibration remains a
derived operation for handlers that need it.

Expected tests:

```text
8-bit payload count matches GateCount
16-bit payload count matches GateCount
raw checksum matches RadarEventBatchMetrics for equivalent batches
invalid payload ranges are caught by existing batch validation or core precheck
```

### 6. Sequential Core Baseline

Implement the simplest processing path first.

Flow:

```text
validate batch/core compatibility
iterate batch.Events in canonical order
resolve SourceId
read payload span
update source state
invoke configured handlers
update aggregate metrics
return RadarProcessingResult
```

This path is the correctness oracle for later parallel processing.

Required behavior:

```text
source-local order is exactly batch order
all events are processed once
all payload values required by configured handlers are read once
owned and leased batches produce the same result
processing-only metrics do not include replay construction work
```

Expected tests:

```text
sequential core processes a synthetic multi-source batch
source state matches expected per-source counters
aggregate result matches sum of source states
owned and leased-equivalent batches produce identical metrics
handler snapshots match processed events
cancellation before processing stops cleanly
```

### 7. Partitioned Completion-Barrier Core

Add the first parallel processing mode without retaining leased payloads after
the call returns.

Flow:

```text
receive RadarEventBatch
route event indexes or ranges to static partitions/shards
signal shard workers for this batch
workers process assigned events and payload spans
main thread waits for all workers
aggregate shard metrics
return only after all payload reads are complete
```

Routing should avoid copying payload bytes. It may use reusable index buffers,
event-range buffers, or a two-pass scan if that is simpler for the first
implementation. The chosen approach should be measured and should not allocate
per event.

Required behavior:

```text
one SourceId is processed by exactly one shard
events for the same SourceId are processed in batch order
parallel result equals sequential result for deterministic handlers
leased batch payload is not read after the completion barrier exits
worker exceptions are surfaced and do not leave the core in a corrupt state
```

Expected tests:

```text
partitioned core matches sequential metrics for synthetic batches
partitioned core matches sequential source snapshots
multi-event same-source ordering is preserved
one hot source does not migrate during milestone 005
worker failure path releases batch work and reports failure
cancellation during barrier processing completes with defined state
```

### 8. Explicit Lifetime Guardrails

Make batch lifetime behavior visible in the processing API.

Milestone 005 should implement the completion-barrier mode and define the
retained-lease shape, but it does not need to implement a production async
retained transport.

Required behavior for the baseline:

```text
leased batch processing completes before return
retention is not hidden inside shard queues
diagnostic capture uses owned snapshots when needed
processing result does not expose spans into leased payload storage
```

Documentation or type-level comments should make the future retained mode
explicit:

```text
retained async processing requires a bounded lease protocol
lease release must happen on success, failure, and cancellation
retained batch count and retained payload bytes must be observable
```

Expected tests:

```text
processing result remains valid after leased batch buffers would be reusable
snapshots do not expose batch-local payload spans
owned snapshot capture is explicit when retention is needed
```

### 9. Processing Telemetry

Add telemetry specific to the processing core.

Required counters:

```text
processed batch count
processed stream event count
processed payload value count
active source count
per-shard event count
per-shard payload value count
per-shard processing time
max shard batch time
hot partition id
hot source id if cheap to compute
processing errors
allocation counters in benchmarks
```

Telemetry should distinguish barrier-based processing from future queued modes:

```text
barrier elapsed time
worker wait time
worker processing time
future retained batch count
future retained payload bytes
future lease wait time
```

Expected tests:

```text
telemetry totals match processing result
per-shard totals sum to aggregate totals
hot partition reflects the largest observed partition load
empty batch telemetry is stable
```

### 10. Processing Validation

Add validation helpers for the processing core output.

Required validation:

```text
input event count == processed event count
input payload values required by workload == processed payload values
per-source event counts sum to aggregate event count
source-local order checks pass
sequential and partitioned results match
owned and leased-equivalent results match
handler snapshot checksums match expected values
```

Validation should be optional for hot-path operation. It can allocate and build
diagnostics outside the measured processing path.

Expected tests:

```text
valid sequential result passes
valid partitioned result passes
missing event is detected
duplicate processing is detected
source ordering violation is detected
metrics mismatch is reported with useful diagnostics
```

### 11. Processing-Only Benchmarks

Add benchmark support that measures core processing separately from replay
construction.

Benchmark inputs:

```text
synthetic RadarEventBatch values with deterministic source distribution
owned batches captured from archive replay if useful
leased delivery through a controlled completion barrier
```

Benchmark modes:

```text
sequential no-handler
sequential counter/checksum handlers
partitioned no-handler
partitioned counter/checksum handlers
```

Required benchmark output:

```text
processing mode
partition count
shard count
handler set
processed batches/s
processed stream events/s
processed payload values/s
allocated bytes / stream event
allocated bytes / payload value
active source count
per-shard event distribution
per-shard time distribution
validation checksum
```

The benchmark must not include:

```text
file enumeration
decompression
Archive Two scanning
identity normalization
batch construction
CLI formatting inside measured loop
```

Expected tests:

```text
benchmark iterations produce stable totals
warmup iterations are excluded from measured totals
sequential and partitioned benchmark checksums match
synthetic workload distribution is deterministic
```

### 12. CLI Smoke Commands

Add minimal manual smoke commands only after the core and benchmark harness are
stable.

Candidate commands:

```text
processing smoke synthetic
processing benchmark synthetic
processing benchmark stream --file <path>
```

The file-backed command may reuse archive replay to obtain batches, but measured
processing time must remain separate from replay construction time.

Expected output:

```text
stream construction counters if file-backed
processing counters
processing mode
partition/shard topology
handler set
processing throughput
allocation ratios
validation checksum
```

Expected tests:

```text
synthetic smoke command exits successfully
synthetic benchmark command reports stable counters
file-backed command separates replay and processing timings
```

### 13. Documentation And Handoff

Close the milestone with the same documentation pattern used by previous
milestones.

Expected documents:

```text
005-processing-core-architecture.md
005-processing-core-architecture-plan.md
005-processing-core-architecture-decision-trace.md
005-processing-core-architecture-closeout.md
docs/handoff.md update
```

The closeout should state:

```text
implemented processing boundary
implemented static topology
implemented source state model
implemented handler model
verified sequential/partitioned parity
verified owned/leased parity
verified processing-only benchmark numbers
remaining risks
006 starts from partition-level shard rebalance
```

## Milestone 005 Completion Criteria

Milestone 005 is complete when:

```text
[ ] processing core consumes RadarEventBatch directly
[ ] static SourceId -> PartitionId -> ShardId topology is implemented
[ ] dense source-local state is implemented
[ ] source-local handler slot model is implemented
[ ] sequential processing mode is implemented and tested
[ ] partitioned completion-barrier mode is implemented and tested
[ ] leased batch lifetime is preserved by the processing boundary
[ ] owned and leased-equivalent inputs produce matching results
[ ] sequential and partitioned modes produce matching deterministic metrics
[ ] processing telemetry reports aggregate, shard, and partition load
[ ] processing validation catches missing/duplicate/out-of-order work
[ ] processing-only benchmark excludes replay construction work
[ ] CLI smoke or benchmark command can manually exercise the core
[ ] decision trace and closeout are written
[ ] handoff identifies milestone 006 as partition-level shard rebalance
```

## Non-Goals

Milestone 005 does not implement:

```text
live shard rebalance
partition migration
retained async transport as a production path
live ingestion
durable broker integration
shared-memory transport
visualization
complex radar algorithms
changes to RadarEventBatch or RadarStreamEvent contract
changes to dense dictionary registration semantics
changes to Archive Two replay ordering
```

The milestone may sketch retained-lease async ownership in comments or docs,
but production retained async delivery belongs after the static processing core
has been measured and validated.

## Risks And Watchpoints

### Payload Lifetime Leakage

Risk:

```text
partition workers accidentally keep references to leased batch payload after the
completion barrier returns
```

Mitigation:

```text
results and snapshots must not expose payload spans
worker queues must contain only work scoped to the current barrier
tests should mutate or reuse backing buffers after processing where practical
```

### Hidden Replay Cost In Benchmarks

Risk:

```text
benchmark numbers accidentally include decompression, scanning, normalization,
or batch construction
```

Mitigation:

```text
use synthetic/prebuilt batches for core benchmarks
separate file-backed replay timing from processing timing
name benchmark workload and measured contour explicitly
```

### Per-Event Allocation

Risk:

```text
routing buffers, handler snapshots, or payload readers allocate per event
```

Mitigation:

```text
use reusable buffers
keep snapshots outside measured hot path
track allocated bytes per stream event and payload value
```

### Source Ordering Regression

Risk:

```text
partitioned routing reorders events for the same SourceId
```

Mitigation:

```text
route one SourceId to one partition
process each partition's event list in original batch order
add source-local chronology/checksum validation
```

### Topology Too Rigid

Risk:

```text
static topology makes 006 rebalance harder
```

Mitigation:

```text
use SourceId -> PartitionId -> ShardId indirection from the start
expose partition assignment diagnostics
keep state ownership explicit and movable in design
```

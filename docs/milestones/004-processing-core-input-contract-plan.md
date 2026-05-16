# Milestone 004: Processing Core Input Contract Plan

Status: complete.

The milestone closeout is recorded in
`004-processing-core-input-contract-closeout.md`.

## Goal

Milestone 004 implements the canonical high-rate input contract for the future
RadarPulse processing core.

The milestone turns the milestone 003 publisher-facing replay foundation into a
compact, deterministic, normalized batch stream:

```text
Archive bytes
  -> decoder
  -> identity normalizer and dense identity catalogs
  -> RadarEventBatch
  -> processing-core input contract
```

The implemented stream must carry dense numeric identifiers instead of text,
support append-only identity catalogs, preserve deterministic replay ordering,
and avoid per-gate identity overhead so the 300M+ event-per-second architecture
is not blocked by stream normalization.

This milestone does not implement processing algorithms, downstream
distribution, live ingestion, durable broker integration, or visualization.

## Starting Point

Milestone 003 is complete and provides:

```text
Archive Two file/cache replay
sequential replay publishing
ordered parallel replay publishing
ArchiveTwoGateMomentEvent semantic publisher shape
ArchiveReplayCountingPublisher checksums and status totals
reusable replay publish session
single-file and cache replay-publish benchmarks
source-order chronology checksum
```

The current limitation is that `ArchiveTwoGateMomentEvent` is a semantic
publisher-facing shape. It contains text identifiers and per-gate semantic
fields that are useful for validation and external publication, but it is not
the desired processing-core hot-path stream.

Milestone 004 should keep milestone 003 behavior intact. The existing publisher
path remains valid and should not be replaced by the new internal stream.

## Target Architecture

The target input contract is:

```text
RadarEventBatch
  StreamSchemaVersion
  DictionaryVersion
  SourceUniverseVersion
  Events: ReadOnlySpan<RadarStreamEvent>
  Payload: raw value storage associated with the batch lifetime
```

Each event is source-addressable and self-contained:

```text
RadarStreamEvent
  SourceId
  RadarOrdinal
  VolumeTimestamp
  MessageTimestamp
  SourceRecord
  SourceMessage
  RadialSequence
  ElevationSlot
  AzimuthBucket
  RangeBand
  MomentId
  GateStart
  GateCount
  WordSize
  Scale
  Offset
  StatusModel
  PayloadOffset
  PayloadLength
```

The concrete field types can change during implementation, but the invariants
must not:

```text
events carry dense numeric identities
events do not carry text
events can be interpreted without neighboring events
payload references are explicit
payload belongs to the batch
batch order is deterministic chronology
batch may contain many SourceId values interleaved
```

## Implementation Slices

### 1. Contract Types And Version Constants

Introduce the first internal stream contracts in the domain/application layer:

```text
RadarEventBatch
RadarStreamEvent
StreamSchemaVersion
DictionaryVersion
SourceUniverseVersion
RadarStreamPayload layout helpers
```

The first version can be intentionally narrow and optimized for Archive Two
Type 31 moment data. It should still be explicit about versioning from the
start, because dictionary growth and source-universe changes are independent of
event-schema changes.

Expected tests:

```text
default version values are stable
batch metadata carries all three version axes
event fields support deterministic equality/checksum use
payload references are range-checked against batch payload storage
```

### 2. Dense Identity Catalogs

Implement append-only dense identity catalogs for textual identities used by
the stream:

```text
radar code  -> RadarOrdinal
moment name -> MomentId
id          -> canonical text
```

Required catalog behavior:

```text
0 <= Id < Count
existing ids never change
new ids append only
unknown valid identity can be registered
invalid identity is rejected or diagnosed according to policy
steady-state lookup is allocation-free
reverse lookup is dense-array based
```

The catalog should expose multiple lookup views where useful:

```text
string
ReadOnlySpan<char>
ReadOnlySpan<byte>
```

The implementation should distinguish hot-path lookup structures from published
catalog state. Optimized buckets, caches, and locks are internal details. The
visible contract is a stable forward/reverse mapping.

Expected tests:

```text
same canonical text maps to same id across lookup views
ids are dense and append-only
reverse lookup is correct for every assigned id
unknown valid identity appends once
invalid identity does not receive an id
registration is serialized under concurrent attempts
readers never observe a partial entry
```

### 3. Catalog Versioning And Persistence

Add logical dictionary versioning without full dictionary copies on every
append.

Required model:

```text
DictionaryVersion = Count or monotonic epoch
base snapshot + append-only delta log
```

The first implementation can choose a simple durable representation, but it
must support these semantics:

```text
batch references a DictionaryVersion
that version can resolve all IDs used by the batch
snapshots or deltas are externally readable
dictionary mappings outlive any durable data that references them
```

Required retention invariant:

```text
event/batch retention <= dictionary mapping retention
```

Expected tests:

```text
version N exposes only entries visible at N
version N+1 exposes appended entries without changing old entries
published snapshot or delta can reconstruct mappings for a batch version
old mappings remain resolvable after later appends
```

### 4. Canonicalization And Error Policy

Define canonicalization rules for every textual dictionary dimension.

Initial dimensions:

```text
radar code
moment name
```

The implementation must explicitly decide:

```text
trim policy
case policy
allowed character set
null and empty handling
padding and trailing-space handling
UTF-8 decoding error handling
```

Unknown and invalid identity must remain separate:

```text
unknown valid identity -> append new dense id
invalid identity       -> reject, fail batch, or emit diagnostic according to policy
```

Expected tests:

```text
canonicalization is identical for string/span/UTF-8 input
case/padding behavior is deliberate and covered
invalid UTF-8 or malformed text cannot register
diagnostics identify invalid dimension and input source
```

### 5. Source Universe Definition

Introduce a versioned source universe that derives dense `SourceId` values from
dense dimension ordinals.

Initial source model:

```text
Source = RadarOrdinal x ElevationSlot x AzimuthBucket x RangeBand
```

Required source-universe metadata:

```text
SourceUniverseVersion
RadarOrdinalCount
ElevationSlotCount
AzimuthBucketCount
RangeBandCount
azimuth bucket layout
range band layout
source-id arithmetic
```

For a single-radar baseline, `RadarOrdinal` can start at `0`, but the source
universe must not assume there will only ever be one radar.

Expected tests:

```text
SourceId is dense: 0 <= SourceId < SourceCount
dimension tuple round-trips to SourceId and back
new radar ordinal maps to a stable contiguous SourceId block
changing source dimensions creates a new SourceUniverseVersion
old SourceId values do not move within the same version
```

### 6. Identity Normalizer

Implement the boundary between decoded radar structures and `RadarEventBatch`
construction.

The normalizer is responsible for:

```text
radar code                    -> RadarOrdinal
moment name                   -> MomentId
radar/elevation/azimuth/range -> SourceId
```

Performance constraints:

```text
no per-gate string allocation
no per-gate dictionary lookup
no shared lock on the steady-state event path
no source-id recomputation from text inside the processing core
```

Lookup work should be amortized:

```text
RadarOrdinal: once per file, volume, or feed segment
MomentId:     once per decoded moment block
SourceId:     once per source-local gate run or stream event
Gate values:  no text lookup
```

For ordered historical replay, dynamic registration must not depend on worker
completion order. Unknown keys discovered by workers should receive IDs only at
a deterministic boundary, such as ordered emission, or through another stable
registration sequence.

Expected tests:

```text
ordered sequential and ordered parallel replay assign identical IDs
worker completion order cannot change DictionaryVersion contents
per-block moment lookup is reused across gate values
per-file or per-volume radar lookup is reused
```

### 7. Batch Builder And Payload Storage

Build `RadarEventBatch` values from decoded Type 31 moment data.

The batch builder should:

```text
emit chronological multi-source batches
split decoded moment blocks into source-local gate-run events when needed
store raw radar values in payload storage associated with the batch lifetime
write PayloadOffset/PayloadLength for each event
keep payload immutable while the batch is visible
```

The implementation should distinguish owned and leased batch lifetimes. Owned
batches may be retained. Leased hot-path batches are valid only during the
synchronous publisher/consumer callback and must be converted to an owned
snapshot before diagnostic capture, asynchronous queuing, or export.

The canonical payload should be raw radar values. Calibrated values remain a
derived interpretation through explicit event metadata:

```text
Scale
Offset
StatusModel
WordSize
```

Expected tests:

```text
one decoded moment block can produce multiple source-local events
events reference valid payload ranges
payload values match raw decoded Archive Two values
event references do not require hidden parser state
batch chronology is stable
```

### 8. Replay Integration

Add a replay path that emits normalized `RadarEventBatch` values without
removing the milestone 003 semantic publisher path.

Candidate internal contract:

```text
IArchiveRadarEventBatchPublisher
  Publish(RadarEventBatch batch, CancellationToken cancellationToken)
```

The exact name can change to match codebase style. The important boundary is
that Archive Two replay can publish normalized batches as a first-class output,
separate from `ArchiveTwoGateMomentEvent`.

Expected implementation notes:

```text
single-file sequential batch replay first
ordered parallel batch replay after sequential parity is proven
cache selection after single-file path is stable
existing archive replay commands remain unchanged
```

Expected tests:

```text
sequential batch replay produces deterministic counts/checksums
parallel batch replay matches sequential chronology and payload checksums
semantic publisher path still passes existing tests
cancellation and invalid input behavior remain covered
```

### 9. Validation And Checksums

Add validation specific to the processing-core input contract.

Required validation shape:

```text
event count
payload value count
batch count
dictionary versions
source-universe version
source-local chronology checks
batch chronology checksum
payload checksum
identity mapping checksum
```

The validator should compare sequential and ordered parallel batch replay. It
should also compare aggregate raw payload counts against the milestone 003
publisher-facing event counts where applicable.

Expected tests:

```text
sequential and parallel batch replay are equivalent
payload value count matches decoded gate values
dictionary snapshot checksum is stable
SourceId distribution is deterministic
```

### 10. CLI Smoke And Benchmark Commands

Add focused commands for manual validation and performance checks.

Candidate smoke command:

```text
archive stream --file <path> [--parallelism n] [--decompressor ...]
```

Candidate cache smoke command:

```text
archive stream --cache <path> [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]
```

Candidate benchmark command:

```text
archive benchmark stream --file <path> [--iterations n] [--warmup-iterations n]
```

Expected output should include:

```text
stream schema version
dictionary version
source-universe version
batches
events
payload values
active sources
dictionary entries
dictionary appends
chronology checksum
payload checksum
allocated bytes per event
allocated bytes per payload value
events/s
payload values/s
```

The benchmark should measure the normalized stream path, not downstream
processing algorithms.

## Achieved Throughput Baseline

The current milestone 004 normalized stream benchmark has exceeded the earlier
milestone 003 count-only `replay-publish` baseline on the comparable payload
metric.

The comparison must use milestone 004 `Payload values/s`, not `Stream events/s`.
One `RadarStreamEvent` is a compact record that can reference a payload range
containing many raw values, while milestone 003 `Published events/s` counted
those raw values directly.

```text
single file, parallelism 24:
  milestone 003 replay-publish: 362_695_693.02 published events/s
  milestone 004 normalized stream: 553_123_110.90 payload values/s
  result: +52.5%

cache-wide KTLX corpus, parallelism 24:
  milestone 003 replay-publish: 310_665_492.15 published events/s
  milestone 004 normalized stream: 509_716_417.97 payload values/s
  result: +64.1%
```

The current normalized stream path does more work than the count-only publisher
path: it constructs `RadarEventBatch` values, emits 64-byte `RadarStreamEvent`
records, normalizes dense identities, maps source-universe IDs, exposes stream
versions, and owns or leases batch payload storage. Leased hot-path delivery
reduced single-file allocation to effectively zero per payload value and reduced
cache-wide allocation from `1.86` to `0.20` allocated bytes/payload value. The
remaining performance target is cache-wide replay overhead outside the
normalized batch buffers, not recovery of the 300M+ payload throughput level.

## Completion Status

Milestone 004 is complete:

```text
[x] RadarEventBatch and RadarStreamEvent contracts are implemented
[x] append-only dense identity catalogs are implemented
[x] dictionary snapshots or deltas are externally visible
[x] source-universe versioning is implemented
[x] identity normalization boundary emits numeric IDs
[x] batch builder emits chronological multi-source batches
[x] payload storage is lifetime-scoped and range-checked
[x] single-file stream replay works sequentially
[x] single-file stream replay works with ordered parallel replay
[x] cache-selected stream replay works
[x] sequential/parallel validation passes
[x] focused unit tests cover identity, versioning, payload, and ordering
[x] CLI smoke and benchmark commands are available
[x] milestone 004 status document is updated with achieved results
[x] handoff is updated for the next milestone
```

## Non-Goals

Milestone 004 does not implement:

```text
processing algorithms
alerting or detection behavior
live ingestion
durable broker integration
shared-memory transport
visualization
long-term storage format optimization
```

The milestone may implement minimal durable dictionary snapshots or deltas if
needed to prove external visibility, but that should not become a general
storage subsystem.

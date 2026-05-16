# Milestone 004: Processing Core Input Contract

Status: complete.

RadarPulse milestone 004 starts from the historical replay publisher foundation
and defines the canonical input contract that a future processing core will
consume.

This document is intentionally not an implementation plan. It records the core
model, architectural boundaries, and design rules for the stream contract before
any task breakdown is written.

The milestone closeout is recorded in
`004-processing-core-input-contract-closeout.md`.

## Milestone Goal

Milestone 004 should define the canonical high-rate input contract for the
future RadarPulse processing core.

The output of the milestone is the architectural definition of:

```text
append-only dense identity dictionaries
identity normalization boundary
deterministic RadarEventBatch
self-contained RadarStreamEvent records
dictionary visibility for downstream and external use
stream schema, dictionary, and source-universe versioning
```

The resulting stream must contain compact numeric identifiers instead of text,
must be deterministic for replay, must allow new dictionary entries during work,
and must avoid per-gate identity overhead so the 300M+ event-per-second target
is not blocked by stream normalization.

Milestone 004 is not about processing algorithms, downstream distribution, live
transport, or visualization. It is about producing the normalized data shape
that those later layers can trust.

## Current Status

Milestone 003 produced a publisher-facing replay path over
`ArchiveTwoGateMomentEvent` values. That shape is useful at the semantic API
boundary, but it is not the desired hot-path transport format for a high-rate
processing engine.

Milestone 004 should focus on the form of the data that leaves replay decoding
and enters the processing core:

```text
Archive replay input
  -> compact internal radar data events
  -> deterministic multi-source event batches
  -> processing-core input stream
```

The goal is to prepare RadarPulse for an engine that can process tens of
thousands of independent logical sources while keeping the input stream compact,
deterministic, and source-addressable.

## Fundamental Model

The physical radar is not the final source identity used by the processing
engine.

```text
Physical radar = feed
Logical source = source-local processing identity
Moment = channel inside a source
Gate values = event payload
Batch = chronological sequence of events from many sources
```

This distinction is central. A single NEXRAD radar is too coarse as a
processing source, but one radar volume contains enough spatial structure to
create tens of thousands of stable logical sources.

The preferred logical source shape is:

```text
Source = RadarId x ElevationSlot x AzimuthBucket x RangeBand
```

The moment name is not part of the preferred source identity. `REF`, `VEL`,
`SW`, `ZDR`, `PHI`, `RHO`, `CFP`, and similar moments should be modeled as
channels inside the same spatial source context. This keeps related moment data
together for future multi-moment radar algorithms.

For a single-radar baseline, the same model still reaches the desired cardinality:

```text
12 elevations x 720 azimuth buckets x 3 range bands = 25,920 sources
12 elevations x 360 azimuth buckets x 6 range bands = 25,920 sources
12 elevations x 180 azimuth buckets x 12 range bands = 25,920 sources
```

The registered source count and the active source count are different things.
Most logical sources may be idle during a short processing window because a
radar scans radials sequentially.

## Source Identity

The hot path should use compact numeric identifiers.

```text
RadarId text     -> dense radar ordinal
MomentName text  -> dense moment id
Logical source   -> dense source id
```

Text must remain at the ingestion, diagnostics, and publisher boundaries. It
should not be repeated inside every hot-path event.

`SourceId` values must be dense inside the configured source universe:

```text
0 <= SourceId < SourceCount
```

There should be no gaps caused by external radar codes, packed text IDs, raw
NEXRAD elevation numbers, or first-seen registration order. Dense IDs let the
processing core use arrays, spans, bitsets, and compact state tables for source
contexts, counters, and validation.

The source registry must be deterministic and dense. If a replay file,
benchmark run, or cache selection is processed more than once with the same
dictionary registration policy, the same logical source should receive the same
`SourceId`.

The source id can be derived directly when the dimensions are bounded:

```text
SourceId =
    (((RadarOrdinal * ElevationSlotCount) + ElevationSlot) * AzimuthBucketCount
        + AzimuthBucket) * RangeBandCount
        + RangeBand
```

`RadarOrdinal`, `ElevationSlot`, `AzimuthBucket`, and `RangeBand` are
zero-based dense ordinals within the selected source universe. For a
single-radar baseline, `RadarOrdinal` is always `0`.

The exact bit packing or arithmetic layout can change later. The invariant is
that the ID is stable, dense, numeric, and cheap to compute.

The source universe must be versioned separately from dictionary entries.
`SourceId` depends on source-dimension rules, not just radar and moment text.

```text
SourceUniverseVersion
  RadarOrdinalCount
  ElevationSlotCount
  AzimuthBucketCount
  RangeBandCount
  azimuth bucket layout
  range band layout
```

A consumer cannot fully interpret `SourceId` without the source-universe version
that produced it. Changes to bucket counts, range boundaries, or source-id
arithmetic must create a new source-universe version.

## Dense Identity Dictionaries

Milestone 004 should define dense identity dictionaries as boundary components
between textual radar metadata and the numeric stream.

These dictionaries are not the stream itself. They exist so the stream can carry
compact IDs while diagnostics, validation, and publisher-facing output can still
recover the original text.

They also must not be private implementation details of the hot path. Dense
dictionaries are shared identity catalogs. Future external applications should
be able to read their published state, resolve IDs to text, and interpret stream
batches without linking to the internal normalizer implementation.

Dictionary input must pass through explicit canonicalization rules before lookup
or registration. The same external text must always become the same canonical
text, regardless of whether it arrived as a `string`, `ReadOnlySpan<char>`, or
`ReadOnlySpan<byte>`.

The milestone should define canonicalization for each dictionary dimension:

```text
trim policy
case policy
allowed character set
null and empty handling
padding and trailing-space handling
UTF-8 decoding error handling
```

Radar and moment names are expected to be exact, compact identifiers. Any
normalization that changes identity, such as case folding or trimming, must be
intentional and documented before IDs are assigned.

Unknown identity and invalid identity are different cases.

An unknown identity is well-formed canonical text that is not currently present
in the dictionary. It may be registered through the cold append-only
registration path:

```text
unknown valid identity -> append new dense id
```

An invalid identity is malformed, unsupported, ambiguous, or impossible to
canonicalize according to the dimension rules. It must not be silently added to
the dictionary:

```text
invalid identity -> reject, fail batch, or emit diagnostic according to policy
```

The error policy should be explicit per dictionary dimension. Radar codes,
moment names, and future text dimensions may have different validity rules, but
all of them must preserve this invariant:

```text
only canonical valid identities receive dense ids
```

Each hot-path dictionary should expose one canonical mapping:

```text
canonical text -> dense id
dense id       -> canonical text
```

The dense id range should be zero-based:

```text
0 <= Id < Count
```

This makes the reverse mapping a direct array lookup and lets the processing
core use arrays, spans, bitsets, and compact counters without sparse-key
indirection.

The dictionary should support multiple lookup views over the same entries:

```text
string              -> id
ReadOnlySpan<char>  -> id
ReadOnlySpan<byte>  -> id
id                  -> string
```

The span paths let parsers map text slices without allocating new strings on the
hot path. The UTF-8 byte path is important when data is read from byte-oriented
input and can be matched before materializing text. The reverse path should be a
simple dense array because reverse lookup is already numeric.

Steady-state lookup should be allocation-free and should not require a shared
lock. Mutation belongs to a cold registration path. The cold path can
materialize canonical text once, store any byte representation needed for future
byte-span lookup, update reverse arrays, and verify that all lookup views agree.

Milestone 004 should allow dictionaries to grow during work. The required shape
is an append-only dense dictionary, not a fixed dictionary:

```text
existing ids never change
new ids are appended only
registration is serialized
readers observe complete entries
round-trip validation stays true
```

The fast path should still be read-mostly:

```text
lookup existing key -> allocation-free read path
unknown key         -> cold append-only registration path
next lookup         -> allocation-free read path
```

Dynamic registration must not make IDs depend on worker race timing. For
historical replay, new IDs should be assigned at a deterministic boundary, such
as ordered emission, or through another deterministic registration sequence. A
parallel decoder may discover unknown keys, but worker completion order must not
decide their numeric IDs.

For continuously arriving input, the accepted input order can define the
registration order. Once an ID is assigned, it is permanent for the lifetime of
the dictionary.

Every emitted batch should be associated with a dictionary version or epoch:

```text
DictionaryVersion
RadarEventBatch
```

A batch may reference only IDs that are visible in its dictionary version. This
lets downstream consumers resolve IDs consistently without requiring the stream
event to carry text.

`DictionaryVersion` should be a cheap logical version, not a full physical copy
of every dictionary on each append. The preferred storage model is:

```text
base snapshot + append-only delta log
```

or, for an in-memory view:

```text
idToText[0..Count)
DictionaryVersion = Count or monotonic epoch
```

When a new entry is registered, existing entries keep their meaning:

```text
version N     sees IDs [0..CountN)
version N + 1 sees IDs [0..CountN + appendedCount)
```

The implementation may resize backing arrays as an internal storage detail, but
versioning must not require cloning or publishing the whole dictionary for every
new entry. External consumers can receive an initial snapshot and then deltas,
or any immutable view that is valid for the requested version.

Dictionary publication must be ordered with batch publication. If a batch
references `DictionaryVersion = N`, the immutable dictionary snapshot or delta
for version `N` must be available to consumers before the batch is consumed, or
atomically alongside the batch.

Dense identity dictionaries are persistent identity catalogs, not temporary
runtime caches. Once an ID has appeared in a batch, the mapping needed to
resolve that ID must remain available indefinitely, or for the full lifetime of
any stored, replayable, or externally visible data that can reference it.

The retention rule is:

```text
event/batch retention <= dictionary mapping retention
```

If RadarPulse keeps a batch, replay artifact, exported result, diagnostic log,
or external reference that contains numeric IDs, it must also keep enough
dictionary state to resolve those IDs later. Old mappings may be compacted into
base snapshots, but they must not be discarded while any durable data can still
refer to them.

Dictionary state should be externally visible through immutable snapshots or
versioned views:

```text
DictionaryVersion -> radar dictionary snapshot
DictionaryVersion -> moment dictionary snapshot
DictionaryVersion -> source-universe metadata
```

Snapshots should expose the stable forward and reverse mappings, counts, and
version metadata. They should not expose mutable internal buckets, caches, locks,
or registration machinery.

External visibility is required for:

```text
diagnostics
validation
publisher-facing expansion
offline inspection
future downstream applications
```

The hot path can use optimized internal lookup structures, but those structures
must project to the same published dictionary state that external consumers see.

Hashing and caches are performance details, not identity rules. A hash can
choose a candidate bucket, but equality must still be confirmed by exact text or
byte comparison. A thread-local cache can reduce repeated byte-span lookups, but
cache hits and misses must not affect assigned IDs or stream contents.

RadarPulse should use dense dictionaries for small textual dimensions first:

```text
radar code   -> RadarOrdinal
moment name  -> MomentId
```

`SourceId` should normally be derived from dense dimension ordinals rather than
registered by first-seen event order. This keeps the source universe stable,
inspectable, and independent of batch boundaries.

If a new radar ordinal is appended dynamically, the configured source dimensions
for one radar should define a contiguous `SourceId` block for that radar:

```text
RadarOrdinal -> SourceId block
```

Existing source IDs must not move when the dictionary grows.

## Identity Normalization Boundary

All textual identity should cross a normalization boundary before the stream is
emitted to the processing core.

The boundary sits between decoded radar structures and `RadarEventBatch`
construction:

```text
Archive bytes
  -> decoder
  -> identity normalizer and dense dictionaries
  -> RadarEventBatch with numeric IDs
  -> processing-core input
```

The normalizer is responsible for replacing text and derived source dimensions
with compact IDs:

```text
radar code                    -> RadarOrdinal
moment name                   -> MomentId
radar/elevation/azimuth/range -> SourceId
```

This boundary is mandatory, but it must not become a heavy per-event
interceptor. RadarPulse must not slow the 300M+ event-per-second stream by doing
repeated text lookup for every gate value.

Lookup work should be amortized at the coarsest deterministic level available:

```text
RadarOrdinal: once per file, volume, or feed segment
MomentId:     once per decoded moment block
SourceId:     once per source-local gate run or stream event
Gate values:  no text lookup
```

The hot stream should already contain numeric IDs. The processing core should
not receive text that it must normalize before processing.

The normalizer may append new dictionary entries when unknown text appears, but
that append belongs to the cold registration path. After registration, repeated
use of the same text should be a cheap read-path lookup.

For historical replay with parallel decoding, registration must happen in a
deterministic sequence. Workers may discover unknown text, but worker completion
order must not assign numeric IDs. Ordered emission or another deterministic
registration boundary should decide new IDs.

The normalizer should make these performance constraints explicit:

```text
no per-gate string allocation
no per-gate dictionary lookup
no shared lock on the steady-state event path
no source-id recomputation from text inside the processing core
```

## Stream Contract Versioning

Stream schema, dictionary state, and source-universe rules are separate version
axes.

```text
StreamSchemaVersion    -> RadarEventBatch and RadarStreamEvent field contract
DictionaryVersion      -> visible dense text/id mappings
SourceUniverseVersion  -> SourceId derivation rules and source dimensions
```

They should not be collapsed into one number. A dictionary can grow without
changing the event schema. The stream schema can evolve without changing current
radar or moment mappings. Source-universe rules can change without redefining
how payload references work.

Each emitted batch should carry enough version metadata for a consumer to choose
the correct interpretation:

```text
RadarEventBatch
  StreamSchemaVersion
  DictionaryVersion
  SourceUniverseVersion
  Events: ReadOnlySpan<RadarStreamEvent>
```

Version changes must preserve deterministic replay interpretation. If the same
input is emitted under the same three versions, it should produce the same event
sequence and the same identity resolution.

## Event Independence

Every stream event must be deterministic and self-contained.

An event must not require any previous or following event to interpret its
identity, chronology, spatial position, moment channel, payload layout, value
classification, or calibration context. The processing core may keep state
across events, but the event record itself must not depend on hidden parser
state or batch-local side effects.

This rule exists so that the same event means the same thing when it is:

```text
processed in a full batch
processed after replay restart
copied into a smaller batch
validated independently
expanded into publisher-facing output
used by a future downstream layer
```

The stream can still be compact. Deterministic independence does not require
duplicating long text values or heavyweight semantic objects. It requires that
all hot-path references resolve through stable numeric IDs and explicit event
fields.

## Data Shape

The milestone 003 event shape is semantic:

```text
ArchiveTwoGateMomentEvent
```

It contains text identifiers, timestamps, nullable calibrated values, and
publisher-facing fields. That is appropriate for external publication and
validation, but it should not be the primary processing-core stream format.

The internal stream should prefer compact batches of structs:

```text
RadarEventBatch
  StreamSchemaVersion
  DictionaryVersion
  SourceUniverseVersion
  Events: ReadOnlySpan<RadarStreamEvent>
```

A batch can contain events for many `SourceId` values. Events may be interleaved
by source, but the batch order remains the deterministic radar chronology
produced by replay.

Conceptually:

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

The event can represent a compact run of gate values instead of one gate. A
gate-run event is still a single source-local event as long as all gates in the
payload belong to the same dense `SourceId`.

One decoded Archive Two moment block can produce several `RadarStreamEvent`
records when it crosses logical-source boundaries, especially range bands. The
decoded block is an input structure; the stream event is the deterministic unit
that the processing core receives.

Payload storage can be decided later. The architectural requirement is that the
event has an explicit payload reference and that the referenced payload bytes or
values have deterministic layout.

The payload is visible through the batch. `PayloadOffset` and `PayloadLength`
are valid only against the payload storage associated with that
`RadarEventBatch` and its lifetime.

Payload storage must be immutable while the batch is visible to consumers. Event
references must not outlive the batch payload storage they point into. This keeps
zero-copy payload handling possible without making event interpretation depend
on hidden mutable state.

The stream contract can support two explicit batch lifetimes:

```text
Owned batch:
  safe to retain after publication
  owns an immutable snapshot of event and payload storage

Leased hot-path batch:
  valid only during the synchronous consumer callback
  backed by reusable buffers owned by the producer/session
  must be copied to an owned snapshot before retention
```

This lifetime distinction must not change event semantics. It only describes
who owns the event and payload buffers, and for how long. Future processing-core
hot paths should consume leased batches synchronously, update their own state,
and release the batch immediately. Diagnostics, export, asynchronous queues, or
debug capture must perform an explicit owned snapshot conversion.

The canonical payload should be raw radar values. Calibrated values are derived
from explicit event metadata such as `Scale`, `Offset`, and `StatusModel`. This
keeps the stream compact and preserves the original measurement payload for
future processing choices.

## Batch Ordering

The processing-core input is a chronological batch, not a source-grouped batch.

Example:

```text
event 0 -> SourceId 17
event 1 -> SourceId 42
event 2 -> SourceId 17
event 3 -> SourceId 901
event 4 -> SourceId 42
```

This is valid when the records appear in deterministic radar chronology.

The replay reader and projector may use parallelism internally, but the emitted
batch order must not depend on worker completion order. The historical replay
rule still applies: parallel work may happen before emission, but emission must
be merged by source record/message/radial/moment/gate chronology.

The primary ordering guarantee of milestone 004 is:

```text
For one replay input and one source-universe configuration,
the same chronological batches contain the same event sequence.
```

Downstream components may later group, distribute, or split batches, but
milestone 004 defines the canonical input sequence before those later
transformations.

## Logical Source Stream Shape

The logical-source stream will be uneven by design.

Structural skew comes from differences in elevation scans, moment availability,
gate counts, word sizes, and range coverage.

Temporal skew comes from the radar scan pattern. A radar does not update all
sources continuously. It emits bursts as the antenna moves through sweeps and
radials.

This unevenness is not a defect. The milestone 004 stream should expose the real
shape of the radar data rather than hiding it behind artificial source grouping.

The stream should make these measurements possible:

```text
events per source per window
payload values per source per window
active sources per window
batch event count
batch payload size
source-local chronology checks
batch chronology checksum
```

## Single-Radar Baseline Strategy

A single downloaded radar volume is sufficient for the first milestone 004
baseline because logical sources are spatial subdivisions, not physical feeds.

The preferred demonstration setup is:

```text
Radar count: 1
Elevation slots: derived from sweeps
Azimuth buckets: 180, 360, or 720
Range bands: enough to reach 20-30K logical sources
Moments: channels inside each source
```

This lets the baseline exercise high logical-source cardinality without
requiring many live radar feeds or a large historical cache.

## Architectural Boundaries

Milestone 004 should keep these boundaries explicit:

```text
Replay decoding boundary:
  Archive Two bytes become decoded radar structures.

Identity boundary:
  Radar text and moment text become dense numeric IDs.

Dictionary visibility boundary:
  Dense identity catalogs are published as immutable versioned views.

Versioning boundary:
  Stream schema, dictionary state, and source-universe rules are versioned
  independently.

Source mapping boundary:
  Decoded radar metadata becomes dense SourceId.

Stream boundary:
  Source-local radar records become deterministic RadarStreamEvent values.

Batch lifetime boundary:
  Hot-path leased batches are consumed synchronously; retained data becomes an
  explicit owned snapshot.

Publication boundary:
  Internal numeric data can be expanded back to semantic output if needed.
```

Keeping these boundaries visible prevents the semantic publisher shape from
leaking into the internal stream and prevents later processing-core concerns
from leaking back into Archive Two parsing.

## Non-Goals For This Document

This document does not define:

```text
implementation tasks
CLI commands
file formats
benchmark targets
processing algorithms
downstream distribution policy
shared-memory transport details
durable broker integration
live ingestion
visualization
```

Those belong in later design or plan documents once the stream shape is
accepted.

## Baseline Architectural Position

RadarPulse should move toward a compact, deterministic, source-addressable
internal stream:

```text
RadarEventBatch
  -> stream schema version
  -> dictionary version visible to the batch
  -> source-universe version
  -> chronological RadarStreamEvent sequence
  -> dense SourceId on every event
  -> explicit payload reference on every event
  -> processing-core input
```

The milestone 003 publisher event remains valuable as an external semantic
shape. It should not be treated as the final hot-path stream format for a
300M+ event-per-second architecture.

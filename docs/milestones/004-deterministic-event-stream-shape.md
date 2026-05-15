# Milestone 004: Deterministic Event Stream Shape

RadarPulse milestone 004 starts from the historical replay publisher foundation
and defines the canonical data stream shape that a future processing core will
consume.

This document is intentionally not an implementation plan. It records the core
model, architectural boundaries, and design rules for the stream contract before
any task breakdown is written.

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

For a one-radar prototype, the same model still reaches the desired cardinality:

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
benchmark run, or cache selection is processed more than once, the same logical
source should receive the same `SourceId`.

The source id can be derived directly when the dimensions are bounded:

```text
SourceId =
    (((RadarOrdinal * ElevationSlotCount) + ElevationSlot) * AzimuthBucketCount
        + AzimuthBucket) * RangeBandCount
        + RangeBand
```

`RadarOrdinal`, `ElevationSlot`, `AzimuthBucket`, and `RangeBand` are
zero-based dense ordinals within the selected source universe. For a one-radar
prototype, `RadarOrdinal` is always `0`.

The exact bit packing or arithmetic layout can change later. The invariant is
that the ID is stable, dense, numeric, and cheap to compute.

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

Downstream components may later group, route, or split batches, but milestone
004 defines the canonical input sequence before those later transformations.

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

## One-Radar Prototype Strategy

A single downloaded radar volume is sufficient for the first milestone 004
prototype because logical sources are spatial subdivisions, not physical feeds.

The preferred demonstration setup is:

```text
Radar count: 1
Elevation slots: derived from sweeps
Azimuth buckets: 180, 360, or 720
Range bands: enough to reach 20-30K logical sources
Moments: channels inside each source
```

This lets the prototype exercise high logical-source cardinality without
requiring many live radar feeds or a large historical cache.

## Architectural Boundaries

Milestone 004 should keep these boundaries explicit:

```text
Replay decoding boundary:
  Archive Two bytes become decoded radar structures.

Identity boundary:
  Radar text and moment text become dense numeric IDs.

Source mapping boundary:
  Decoded radar metadata becomes dense SourceId.

Stream boundary:
  Source-local radar records become deterministic RadarStreamEvent values.

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
  -> chronological RadarStreamEvent sequence
  -> dense SourceId on every event
  -> explicit payload reference on every event
  -> processing-core input
```

The milestone 003 publisher event remains valuable as an external semantic
shape. It should not be treated as the final hot-path stream format for a
300M+ event-per-second architecture.

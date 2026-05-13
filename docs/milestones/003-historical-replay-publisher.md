# Milestone 003: Historical Replay Publisher Foundation

RadarPulse will turn the completed Archive Two inspection/decoder foundation
into a publisher-facing historical replay input path.

## Current Status

Planning started. No runtime implementation has been added yet.

Implemented:

```text
milestone 003 plan document
milestone 003 status document
handoff update pointing from completed milestone 002 into milestone 003
```

Available from previous milestones:

```text
historical archive loader
deterministic local NEXRAD cache layout
Archive Two base-data classification
MDM/compressed-stream classification
Archive Two volume header parsing
compressed record boundary parsing
per-record BZip2 decompression
reusable-workspace radarpulse BZip2 backend
RDA/RPG message scanning
minimal Message Type 31 parsing
raw and calibrated moment value decoding
ArchiveTwoGateMomentEvent replay-facing event shape
sequential replay-shape benchmark
parallel replay-shape benchmark with source-order-preserving aggregation
sequential/parallel replay-shape validation with chronology checksum
cache-wide replay-shape validation on the local KTLX corpus
```

Not yet implemented:

```text
publisher-facing replay API
counting/checksum replay publisher
single-file replay publish command
production-facing ordered parallel replay source
benchmark/validator reuse of the production replay source
downstream event engine integration
```

## Intended Usage

The intended first smoke command is:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --parallelism 1 --decompressor radarpulse
```

Parallel smoke after the ordered merge is implemented:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --parallelism 24 --decompressor radarpulse
```

Expected summary shape:

```text
File: data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
Decompressor: radarpulse
Parallelism: 24
Chronology verification: required
Compressed records: 55
Compressed bytes: 5_406_610
Decompressed bytes: 50_741_824
Published events: 38_759_040
Valid events: 5_523_459
Raw value checksum: 1_063_626_011
Calibrated value scaled checksum: 70_028_121_122
Chronology checksum: 5_257_350_734_454_804_390
```

The exact output can change during implementation. The important behavior is
that the command exercises the real publisher-facing replay path, not a
benchmark-only projection loop.

## Replay Contract

Milestone 003 should expose a clear event publishing boundary:

```text
Archive Two file reader
compressed record decompressor
RDA/RPG message scanner
Type 31 gate-moment event projector
ordered replay publisher
```

The event shape entering the publisher is the existing
`ArchiveTwoGateMomentEvent`:

```text
radar id
volume timestamp
message timestamp
sweep/elevation/radial/gate identity
range in kilometers
moment name
raw value
decoded status
optional calibrated value
source order
```

The first concrete publisher should count events, status classes, checksums,
and chronology. It is a validation publisher, not the final downstream event
engine.

## Ordering Rules

Default historical replay order is:

```text
file
compressed record
message within compressed record
Type 31 radial
moment block
gate index
```

Parallel decompression and projection are allowed only if publication is merged
by this source order. Worker completion order is not a valid replay order.

The milestone 002 chronology checksum should remain part of the verification
surface. Sequential and parallel replay over the same file must produce the
same:

```text
published event count
valid/status counts
raw value checksum
calibrated value scaled checksum
chronology checksum
```

## Design Notes

The benchmark implementation already contains most of the mechanics needed for
ordered parallel projection:

```text
read compressed record descriptors in file order
run a Type 31 radial metadata prepass
build per-record starting projector state
project records concurrently
aggregate results by original compressed record index
combine order-sensitive chronology checksums
```

Milestone 003 should extract this into a reusable replay source instead of
leaving it embedded in `NexradArchiveReplayShapeBenchmark`.

The first implementation should avoid a large event buffer for a full volume
when possible. If the parallel path needs buffering, prefer bounded per-record
buffers or per-record result objects that are drained in original record order.

## Performance Notes

The latest milestone 002 Release smoke reported about
230_347_912 replay-shaped events/s on `KTLX20260504_000245_V06` with
`radarpulse` and `--parallelism 24`. This is roughly 11.5x above the initial
20M events/s target for replay-shaped event preparation on that file.

Milestone 003 should preserve the same design pressure, but the first acceptance
gate is correctness:

```text
explicit publisher contract
ordered publication
sequential/parallel equivalence
clear diagnostics
focused tests
```

Any throughput reported by this milestone should be described as publisher-path
throughput, not full downstream engine throughput.

## Limitations

Milestone 003 should not promise:

```text
rendered radar imagery
geospatial projection
storm/event detection
durable broker publishing
partitioning or sharding strategy
production replay scheduling
live ingestion
automatic archive download during replay
```

Those belong to later milestones after the publisher-facing replay boundary is
stable.

## Done Criteria

Milestone 003 is complete when:

```text
RadarPulse has an explicit replay publisher API for ArchiveTwoGateMomentEvent
one cached Archive Two file can publish ordered events through that API
the CLI can smoke-test the publisher path
parallel replay preserves source order through an ordered merge
sequential and parallel replay produce identical chronology checksums
focused tests cover ordering, status totals, diagnostics, and cancellation
documentation describes the replay contract and remaining limitations
```

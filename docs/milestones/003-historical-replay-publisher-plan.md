# Milestone 003: Historical Replay Publisher Foundation Plan

## Goal

Milestone 003 turns the milestone 002 Archive Two decoder and replay-shape
projection into a publisher-facing historical replay input path.

The milestone should prove that RadarPulse can read cached NOAA NEXRAD Archive
Two base-data files, project ordered `ArchiveTwoGateMomentEvent` values, and
deliver them through an explicit replay publisher contract. The first publisher
can be a counting/checksum sink for validation; the important shift is that
ordered replay is no longer only a benchmark or validator implementation detail.

This milestone does not implement the full downstream event engine, event
detection, partitioning strategy, live ingestion, user-facing visualization, or
production replay operations.

## Starting Point

Milestone 002 completed the archive inspection and decoder foundation.

Available building blocks:

```text
Archive Two base-data file classification
24-byte Archive Two volume header parsing
compressed record boundary parsing
reusable-workspace radarpulse BZip2 backend
SharpZipLib and SharpCompress comparison backends
streaming decompression callback
RDA/RPG message scanner
minimal Message Type 31 parser
raw and calibrated Type 31 moment value decoding
ArchiveTwoGateMomentEvent replay-facing event shape
sequential replay-shape projection
parallel replay-shape projection with ordered aggregation
order-sensitive chronology checksum
cache-wide sequential/parallel replay-shape validation
```

The current replay-shape implementation is validated and fast enough for the
initial target, but it is still exposed primarily through benchmark and
validation commands:

```text
archive benchmark replay-shape
archive validate replay-shape
```

The next step is to extract a production-facing replay source/publisher path so
future downstream pipeline work can consume the same ordered event stream.

## Ordering Contract

Historical replay must preserve deterministic source order unless a later
partitioning milestone explicitly defines a different ordering contract.

The default order is:

```text
cache selection order
file order
compressed record sequence
message sequence within record
Type 31 radial sequence
moment block order
gate index order
```

Parallel decompression and projection may finish out of order internally, but
publication must happen through an ordered merge by original source position.
Worker completion order must never leak into the downstream publisher.

The chronology checksum from milestone 002 should remain a standard validation
gate for sequential/parallel equivalence.

## Initial Capability

Required input for the first implementation slice:

```text
one cached Archive Two base-data file path
```

Optional inputs:

```text
parallelism
decompressor backend
cancellation token
```

Later milestone 003 slices may add cache selection:

```text
cache directory
date
radar id
max files
```

The first implementation should focus on one file until the publisher API and
ordered merge behavior are proven.

## Proposed API Shape

Candidate domain/application-facing concepts:

```text
IArchiveReplayEventPublisher
  Publish(ArchiveTwoGateMomentEvent event, CancellationToken cancellationToken)

ArchiveReplayPublishResult
  compressed records
  compressed bytes
  decompressed bytes
  published events
  valid events
  status counts
  raw checksum
  calibrated checksum
  chronology checksum

NexradArchiveReplayPublisher
  PublishFile(filePath, publisher, options, cancellationToken)
```

The exact names can change to fit the codebase, but the boundary should be
explicit: archive replay produces ordered events and hands them to a publisher
contract. Benchmarks and validators can use this path instead of duplicating the
core replay loop.

The first concrete publisher should be a counting/checksum publisher. It should
be deliberately simple and deterministic, so it can be used by tests and manual
smoke commands without introducing downstream engine complexity.

## Proposed CLI Shape

Candidate single-file smoke command:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]
```

Expected output shape:

```text
File: ...
Decompressor: radarpulse
Parallelism: 24
Chronology verification: required
Compressed records: ...
Compressed bytes: ...
Decompressed bytes: ...
Published events: ...
Valid events: ...
Below-threshold events: ...
Range-folded events: ...
CFP filter-not-applied events: ...
CFP point-clutter-filter events: ...
CFP dual-pol-filtered events: ...
Reserved events: ...
Unsupported events: ...
Raw value checksum: ...
Calibrated value scaled checksum: ...
Chronology checksum: ...
```

The command should be a smoke/validation command for the publisher path, not a
claim of production replay operations.

## Implementation Slices

Step 1: introduce publisher-facing contracts.

```text
event publisher interface
publisher options
publish result model
counting/checksum publisher
```

Step 2: extract reusable replay projection from benchmark-only code.

```text
single-file replay source
shared worker/session helper if useful
reuse ArchiveTwoGateMomentEventProjector
reuse ArchiveTwoMessageStreamScanner
reuse ArchiveTwoFileReader record descriptors
```

Step 3: implement sequential publish.

```text
read volume header
scan compressed records in file order
decompress each record
project Type 31 gate-moment events
publish each event through the publisher contract
return deterministic totals/checksums
```

Step 4: implement ordered parallel publish.

```text
read compressed record descriptors in file order
metadata prepass to compute per-record projector starting state
project records concurrently
buffer per-record publish results or event batches
publish/aggregate in original record order
verify chronology checksum matches sequential behavior
```

The parallel path should avoid publishing from worker callbacks directly unless
the callback writes into a per-record ordered buffer that is later drained in
source order.

Step 5: wire CLI smoke command.

```text
archive replay --file ...
default decompressor: radarpulse
default parallelism: 1 or Environment.ProcessorCount, decided during implementation
clear diagnostics for non-Archive Two files
```

Step 6: migrate benchmark/validation where practical.

```text
keep benchmark-specific timing/allocation code in the benchmark class
reuse the production replay source for projection and ordering semantics
keep validator focused on sequential/parallel equivalence
```

This migration can happen in the same milestone after the first publisher path
is stable. It does not need to be forced into the first implementation commit if
that would make the slice too large.

## Test Plan

Unit tests:

```text
publisher receives projected events in source order
counting/checksum publisher reports expected totals
sequential publish preserves chronology checksum
parallel publish matches sequential publish totals and chronology checksum
parallel publish does not expose worker completion order
non-Archive Two files return clear diagnostics
unsupported or malformed records fail without partial success claims
cancellation stops replay and releases pooled buffers
```

Fixture strategy:

```text
prefer synthetic Archive Two/message fixtures for deterministic unit tests
reuse existing small message-building helpers where practical
keep large real NEXRAD files under data/ and out of source control
use opt-in corpus tests for real cached KTLX files only when needed
```

Manual smoke:

```text
dotnet test RadarPulse.sln --no-restore
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --parallelism 1 --decompressor radarpulse
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --parallelism 24 --decompressor radarpulse
```

The sequential and parallel smoke commands should report the same event counts,
raw checksum, calibrated checksum, and chronology checksum.

## Performance Notes

Milestone 002 already showed that replay-shaped event preparation can exceed
the initial 20 million events/s target on the current KTLX smoke file. Milestone
003 should preserve that design pressure, but correctness and ordering are the
main acceptance gates for the first publisher foundation.

The first publisher may be a counting sink, so benchmark numbers should be
reported carefully:

```text
publisher-path throughput
not full downstream engine throughput
not production replay capacity
```

Allocation pressure still matters. The implementation should preserve the
existing buffer-pooling and reusable decompressor-session approach where it does
not make the publisher boundary unclear.

## Code Boundaries

In scope:

```text
historical replay publisher contract
Archive Two gate-moment event publishing
sequential replay source
ordered parallel replay source
counting/checksum publisher
CLI smoke command
focused tests
documentation updates
```

Out of scope:

```text
event detection engine
forecasting or nowcasting logic
partitioning and sharding strategy
durable queues or broker integration
live SNS/SQS ingestion
map rendering or imagery generation
production replay orchestration
automatic archive download during replay
```

## Done Criteria

Milestone 003 is complete when:

```text
RadarPulse exposes an explicit publisher-facing replay API
one cached Archive Two file can publish ordered ArchiveTwoGateMomentEvent values
sequential and parallel publish paths produce identical counts and chronology checksums
parallel workers cannot publish in completion order
the CLI can smoke-test the publisher path
focused tests cover ordering, totals, diagnostics, and cancellation
documentation describes the supported replay contract and limitations
```

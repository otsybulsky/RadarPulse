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

Completion status: milestone 003 is complete. The implementation proved the
single-file and cache-selected publisher path, ordered parallel replay,
counting/checksum validation, CLI smoke commands, and replay-publish
benchmarks.

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

At milestone start, replay-shape implementation was validated and fast enough
for the initial target, but it was still exposed primarily through benchmark
and validation commands:

```text
archive benchmark replay-shape
archive validate replay-shape
```

Milestone 003 extracted the production-facing replay source/publisher path so
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

Cache selection is now implemented for the publisher smoke path:

```text
cache directory - implemented
date - implemented
radar id - implemented
max files - implemented
```

The first implementation focused on one file until the publisher API and
ordered merge behavior were proven. The cache-selection slice now reuses the
same session across selected files.

Current implementation note:

```text
single-file sequential replay is implemented
archive replay --file ... is implemented for --parallelism n
archive replay --cache ... is implemented for date/radar/max-files selection
archive benchmark replay-publish --file ... is implemented
archive benchmark replay-publish --cache ... is implemented
ordered parallel publishing is implemented
reusable count-only replay publish session is implemented for steady-state benchmarking
```

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

ArchiveReplayCachePublishResult
  selected cache metadata
  examined/skipped/published file counts
  aggregate publish totals
  aggregate chronology checksum in selected file order

ArchiveReplayPublishCacheBenchmarkResult
  cache benchmark selection metadata
  per-iteration cache publish totals
  elapsed time and allocation totals

NexradArchiveReplayPublisher
  PublishFile(filePath, publisher, options, cancellationToken)

NexradArchiveReplayPublishSession
  PublishFile(filePath, cancellationToken)
  PublishCache(cachePath, date, radarId, maxFiles, cancellationToken)
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

Cache-selection smoke command:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n] [--parallelism n] [--decompressor radarpulse|sharpziplib|sharpcompress]
```

Expected cache output includes examined files, skipped files, published files,
aggregate status totals, raw/calibrated checksums, and an aggregate chronology
checksum. `--max-files` limits selected cache files after date/radar filtering.

## Implementation Slices

Step 1: introduce publisher-facing contracts.

```text
event publisher interface - implemented
publisher options - implemented
publish result model - implemented
counting/checksum publisher - implemented
```

Step 2: extract reusable replay projection from benchmark-only code.

```text
single-file sequential replay source - implemented
shared worker/session helper if useful
reuse ArchiveTwoGateMomentEventProjector
reuse ArchiveTwoMessageStreamScanner
reuse ArchiveTwoFileReader record descriptors
```

Step 3: implement sequential publish.

```text
read volume header - implemented
scan compressed records in file order - implemented
decompress each record - implemented
project Type 31 gate-moment events - implemented
publish each event through the publisher contract - implemented
return deterministic totals/checksums - implemented
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

The parallel path does not publish from worker callbacks directly. The count-only
publisher path combines per-record accumulators in source order; the custom
publisher path writes into per-record buffers and drains those buffers in source
order.

Step 5: wire CLI smoke command.

```text
archive replay --file ... - implemented
archive replay --cache ... - implemented
default decompressor: radarpulse
default parallelism: 1
clear diagnostics for non-Archive Two files
```

Step 5b: add cache-selection replay.

```text
reuse one NexradArchiveReplayPublishSession across selected files - implemented
filter by cache path/date/radar/max files - implemented
skip non-Archive Two files without publishing them - implemented
aggregate file publish results in selected cache order - implemented
```

Step 6: migrate benchmark/validation where practical.

```text
keep benchmark-specific timing/allocation code in the benchmark class
reuse the production replay source for projection and ordering semantics
keep validator focused on sequential/parallel equivalence
```

The first publisher-path benchmark is implemented as
`archive benchmark replay-publish`. It now uses a reusable
`NexradArchiveReplayPublishSession` inside the timed section, keeps workers and
decompressor sessions alive across warmup/measured iterations, and validates
stable counts/checksums across iterations. Full migration of the older
`replay-shape` benchmark/validator can happen later if it still reduces
duplication without weakening the existing comparison gates.

The same benchmark command also supports cache selection:

```text
archive benchmark replay-publish --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX] [--max-files n]
```

The cache benchmark uses one replay publish session per benchmark run and
reuses it across warmup/measured cache iterations. It reports
examined/skipped/published file counts, aggregate replay totals, throughput,
and allocated bytes/event.

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
cache replay applies date/radar/max-files selection
cache replay skips non-Archive Two files and aggregates base-data totals
cache benchmark validates stable aggregate totals/checksums across iterations
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
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 2 --parallelism 24 --decompressor radarpulse
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-publish --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 2 --iterations 2 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
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

After the reusable-session `archive benchmark replay-publish` smoke, throughput
is acceptable for milestone 003 and setup allocation pressure is no longer the
dominant concern in the benchmark. Remaining performance follow-up should be
driven by cache-wide replay needs and allocation profiling:

```text
reuse record descriptor, metadata, result, and event buffers where practical
keep one replay session alive across cache-wide file batches
separate cache-wide replay measurement from single-file smoke commands
profile Parallel/ConcurrentStack scheduling only after metadata allocation is understood
```

This follow-up is not required to prove the milestone 003 publisher boundary.
It can be revisited before treating publisher replay as a long-running
production profile.

## Code Boundaries

In scope:

```text
historical replay publisher contract
Archive Two gate-moment event publishing
sequential replay source
ordered parallel replay source
counting/checksum publisher
CLI smoke command
cache-selection smoke command
cache-wide publisher benchmark
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

Completion note: these criteria are met by the implemented single-file replay
publisher, ordered parallel replay path, cache-selection replay, reusable
publish session, replay-publish benchmarks, focused tests, and documentation.

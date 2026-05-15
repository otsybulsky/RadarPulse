# Milestone 003: Historical Replay Publisher Foundation

Status: complete.

RadarPulse turned the completed Archive Two inspection/decoder foundation into
a publisher-facing historical replay input path.

The milestone closeout is recorded in
`003-historical-replay-publisher-closeout.md`.

## Current Status

Milestone 003 is complete. The publisher foundation, reusable steady-state
benchmark session, cache-selection replay smoke path, and cache-wide publisher
benchmark are implemented.

Implemented:

```text
milestone 003 plan document
milestone 003 status document
handoff update pointing from completed milestone 002 into milestone 003
IArchiveReplayEventPublisher contract
ArchiveReplayPublishOptions
ArchiveReplayPublishResult
ArchiveReplayCachePublishResult
ArchiveReplayPublishCacheBenchmarkResult
ArchiveReplayCountingPublisher
NexradArchiveReplayPublisher sequential single-file replay path
NexradArchiveReplayPublisher ordered parallel replay path
NexradArchiveReplayPublishSession reusable count-only replay runner
archive replay --file ... CLI smoke command
archive replay --cache ... CLI cache-selection smoke command
archive benchmark replay-publish --file ... CLI steady-state benchmark command
archive benchmark replay-publish --cache ... CLI cache-wide benchmark command
focused unit tests with synthetic Archive Two framing and fake decompression
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

Deferred beyond milestone 003:

```text
older replay-shape benchmark/validator reuse of the production replay source,
  if it still meaningfully reduces duplication
downstream event engine integration
```

## Intended Usage

The implemented first smoke command is:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --parallelism 1 --decompressor radarpulse
```

Parallel smoke after the ordered merge:

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

Cache-selection replay uses the reusable session across the selected files:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive replay --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 2 --parallelism 24 --decompressor radarpulse
```

Expected cache summary shape:

```text
Cache: data/nexrad
Date: 2026-05-04
Radar: KTLX
Decompressor: radarpulse
Parallelism: 24
Chronology verification: required
Examined files: ...
Skipped files: ...
Published files: ...
Compressed records: ...
Published events: ...
Valid events: ...
Raw value checksum: ...
Calibrated value scaled checksum: ...
Chronology checksum: ...
```

`--max-files` limits selected cache files after date/radar filtering. MDM and
unknown files count as examined and skipped; Archive Two base-data files are
published in cache path order.

## Replay Contract

Milestone 003 exposes a clear event publishing boundary:

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

The first concrete publisher counts events, status classes, checksums, and
chronology. It is a validation publisher, not the final downstream event engine.

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

The sequential path now has a reusable replay publisher source, and the first
ordered parallel publisher path is implemented. The count-only replay overload
uses per-record accumulators and combines them in source order. The custom
publisher overload uses per-record buffers and drains them in source order so
worker completion order does not reach the publisher.

The first implementation should avoid a large event buffer for a full volume
when possible. If the parallel path needs buffering, prefer bounded per-record
buffers or per-record result objects that are drained in original record order.

The cache replay path is deliberately count-only for this milestone. It reuses
`NexradArchiveReplayPublishSession` across files, aggregates
`ArchiveReplayPublishResult` values, and combines file chronology checksums in
selected cache order. The one-shot `NexradArchiveReplayPublisher` remains the
simple single-file API.

## Performance Notes

The latest milestone 002 Release smoke reported about
230_347_912 replay-shaped events/s on `KTLX20260504_000245_V06` with
`radarpulse` and `--parallelism 24`. This is roughly 11.5x above the initial
20M events/s target for replay-shaped event preparation on that file.

The first milestone 003 publisher smoke used the same file and measured the
`archive replay` command with an external PowerShell `Measure-Command` timer
after a Release build:

```text
parallelism 1:
published events: 38_759_040
valid events: 5_523_459
chronology checksum: 5_257_350_734_454_804_390
measured elapsed ms: 1_222.83
measured published events/s: 31_696_112.78

parallelism 24:
published events: 38_759_040
valid events: 5_523_459
chronology checksum: 5_257_350_734_454_804_390
measured elapsed ms: 592.17
measured published events/s: 65_453_053.24
```

This external measurement includes CLI process startup and should be treated as
a smoke measurement, not a stable benchmark. The parallel publisher path reports
the same chronology checksum as the sequential path. For comparison, the
existing in-process replay-shape benchmark on the same Release build measured:

```text
parallelism 1:  50_671_150.52 replay-shaped events/s
parallelism 24: 248_026_584.81 replay-shaped events/s
```

The internal publisher-path benchmark command removes per-command process
startup from the timed section and validates iteration consistency. It now uses
`NexradArchiveReplayPublishSession`, so replay workers, decompressor sessions,
projectors, accumulators, and compressed/output buffers are created once and
reused across warmup and measured iterations:

```text
command: archive benchmark replay-publish --iterations 5 --warmup-iterations 1 --decompressor radarpulse

parallelism 1:
  51_754_463.69 published events/s
  allocated bytes / event: 0.06

parallelism 24:
  362_695_693.02 published events/s
  allocated bytes / event: 0.07
  chronology checksum per iteration: 5_257_350_734_454_804_390
```

The publisher benchmark measures steady-state count-only replay. It is still a
more direct publisher-path benchmark than the CLI smoke command, but it is no
longer a setup-heavy per-file API benchmark. The public one-shot
`NexradArchiveReplayPublisher` API remains available for simple single-file
calls.

Current assessment:

```text
sequential publisher throughput is acceptable for milestone 003 because it is
  above the initial 20M events/s target through the publisher path

parallel publisher throughput is strong for milestone 003 and confirms that the
  ordered merge can preserve chronology while exceeding the target by a wide
  margin on the current KTLX smoke file

parallel allocation pressure from per-iteration worker/session setup has been
  removed from the benchmark profile; remaining allocations are small enough for
  this foundation slice but should still be watched before long-running
  cache-wide replay
```

Likely remaining allocation contributors in the current publisher benchmark:

```text
per-file record descriptor and metadata arrays
per-record metadata radial arrays
Task/Parallel scheduling infrastructure
per-record event buffers in the custom publisher path
```

Potential later performance follow-up before treating replay as a long-running
production profile:

```text
reuse or pool record descriptor and metadata-radial storage where practical
profile whether Parallel/ConcurrentStack scheduling is visible after metadata
  allocation is reduced
```

The cache-wide publisher benchmark uses the same reusable session across each
selected cache iteration and validates stable aggregate totals/checksums across
iterations. On the local KTLX cache for `2026-05-04`, the Release benchmark:

```text
command: archive benchmark replay-publish --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 1000000 --iterations 2 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse

examined files per iteration: 244
skipped files per iteration: 24
published files per iteration: 220
published events per iteration: 8_513_587_200
valid events per iteration: 1_369_194_138
chronology checksum per iteration: 9_060_754_844_693_896_318
published events/s: 310_665_492.15
valid events/s: 49_962_649.20
allocated bytes / event: 0.06
```

Milestone 003 preserved the same design pressure, but the acceptance gate was
correctness:

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

Milestone 003 does not promise:

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

Those belong to later work after the publisher-facing replay boundary.

## Done Criteria

Milestone 003 is complete when:

```text
RadarPulse has an explicit replay publisher API for ArchiveTwoGateMomentEvent
one cached Archive Two file can publish ordered events through that API
selected cached Archive Two files can publish ordered events through that API
the CLI can smoke-test the publisher path
parallel replay preserves source order through an ordered merge
sequential and parallel replay produce identical chronology checksums
focused tests cover ordering, status totals, diagnostics, and cancellation
documentation describes the replay contract and remaining limitations
```

Completion summary:

```text
explicit replay publisher API implemented
single-file sequential publisher path implemented
CLI smoke command implemented for --file/--cache with --parallelism n
tests cover source order, status totals, sequential/parallel equivalence, ordered custom-publisher drain, diagnostics, invalid parallelism, cancellation, reusable session, and cache selection
ordered parallel publish implemented
internal replay-publish benchmark implemented
reusable steady-state replay publish session implemented
cache-selection replay implemented
cache-wide replay-publish benchmark implemented
milestone 003 done criteria met
```

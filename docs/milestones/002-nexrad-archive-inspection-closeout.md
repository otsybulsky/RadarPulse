# Milestone 002: Closeout

## Status

Milestone 002 is complete.

The milestone produced the Archive Two inspection and decoder foundation for
cached NOAA NEXRAD Level II files. RadarPulse can classify cached files, parse
Archive Two framing, decompress internal BZip2 records, scan RDA/RPG messages,
extract Message Type 31 radar structure, benchmark parser paths, and validate
ordered replay-shape projection.

## Final Outcome

Implemented:

- Archive Two, MDM-shaped, compressed-stream, and unknown-binary classification.
- 24-byte Archive Two volume header parsing.
- Compressed record boundary parsing from signed big-endian control words.
- Per-record BZip2 decompression with selectable backends.
- Reusable-workspace `radarpulse` BZip2 backend.
- Differential decompression validation against SharpZipLib.
- Streaming decompression callback and RDA/RPG message scanning.
- Minimal Message Type 31 parsing for moments, sweeps, radials, gate counts,
  word size, first gate, gate spacing, scale, offset, and source order.
- Decompression, parse, raw decode, calibrated decode, and replay-shape
  benchmarks.
- Ordered parallel replay-shape projection with chronology checksum.
- Cache-wide inspection and replay-shape validation.
- Calibrated-data unevenness reporting by compressed record, sweep, radial, and
  minute.
- Decision trace for the main decoder and ordering choices.

## Completion Checklist

```text
[x] cached Archive Two base-data files are classified
[x] MDM and unknown files are classified without accidental base-data parsing
[x] Archive Two volume header parsing is implemented
[x] compressed record boundary parsing is implemented
[x] per-record BZip2 decompression is implemented
[x] reusable low-allocation BZip2 backend is implemented
[x] differential decompression validation is implemented
[x] streaming message scanning is implemented
[x] minimal Message Type 31 parsing is implemented
[x] sweep/radial/moment metadata summaries are implemented
[x] raw and calibrated moment decode benchmarks are implemented
[x] replay-shape projection is implemented
[x] ordered parallel replay-shape validation is implemented
[x] cache-wide inspection and validation are implemented
[x] focused tests and documentation are implemented
```

## Final Verification

Representative recorded verification:

```text
radarpulse decompression, parallelism 24:
  1_086.18 decompressed MB/s

minimal parse, parallelism 24:
  748_824_137.31 estimated gate-moment events/s

raw moment decode, parallelism 24:
  659_912_891.38 decoded values/s

replay-shape, parallelism 24:
  258_930_679.77 replay-shaped events/s
  36_899_597.97 valid events/s
  chronology checksum: 5_257_350_734_454_804_390

cache-wide replay-shape validation:
  examined files: 244
  skipped files: 24
  compared files: 220
  failed files: 0
  replay-shaped events: 8_513_587_200
  valid events: 1_369_194_138
```

The decoder also validated the `radarpulse` backend against SharpZipLib on a
local KTLX cache sample with zero decompression mismatches.

## Deferred Work

The following were intentionally left to later milestones:

- Publisher-facing replay API.
- Downstream event engine integration.
- Partitioning and sharding.
- Durable broker publishing.
- Live ingestion.
- Visualization or rendered imagery.
- Full meteorological product coverage beyond the replay needs proven here.

## Next Milestone Input

Milestone 003 starts from this stable decoder surface:

```text
cached Archive Two file
  -> ordered compressed records
  -> decompressed RDA/RPG messages
  -> Message Type 31 gate/moment structure
  -> replay-shaped ordered events and chronology checksum
```

The next milestone can turn this benchmark/validation-oriented replay shape into
an explicit publisher-facing replay path.

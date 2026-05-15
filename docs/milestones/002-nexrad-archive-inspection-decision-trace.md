# Milestone 002: Decision Trace

## 1. What Was Implemented

Milestone 002 implemented the NEXRAD Archive Two inspection and decoder
foundation:

- Archive file classification for Archive Two base data, MDM-shaped files,
  compressed streams, and unknown binaries.
- Archive Two volume header parsing and compressed record boundary parsing.
- Per-record BZip2 decompression with selectable backends.
- Reusable-workspace `radarpulse` BZip2 backend and differential validation
  against SharpZipLib.
- Streaming RDA/RPG message scanning.
- Minimal Message Type 31 parsing for moments, sweeps, radials, gate counts,
  word size, scale, offset, and source order.
- Decompression, parse, raw decode, calibrated decode, and replay-shape
  benchmarks.
- Ordered parallel replay-shape projection and cache-wide validation with
  chronology checksums.
- Cache-wide inspection and calibrated-data unevenness reporting.

Representative verified results:

```text
radarpulse decompression, p24: 1_086.18 decompressed MB/s
minimal parse, p24: 748_824_137.31 estimated gate-moment events/s
raw moment decode, p24: 659_912_891.38 decoded values/s
replay-shape, p24: 258_930_679.77 replay-shaped events/s
cache replay-shape validation: 220 compared files, 0 failed files
```

## 2. Decision Matrix

### Treat Archive Two As A Container, Not One BZip2 Stream

Decision: parse the 24-byte Archive Two header and repeated signed control-word
records, then decompress each internal BZip2 payload independently.

Why chosen: real cached base-data files start with `AR2V` and contain many
internal BZip2 records. Passing the whole file to a normal decompressor is
wrong.

Alternatives: treat files as text, treat the whole file as one BZip2 stream, or
skip container parsing and delegate to a third-party utility.

Rejected because: those approaches either fail on real files or hide the record
ordering needed for replay.

Trade-offs/debt: RadarPulse owns Archive Two framing logic and must keep it
aligned with the supported ICD format.

Review explanation: "The first key correction was recognizing the file as a
radar archive container, not a generic compressed file."

### Layer The Decoder

Decision: build the path in layers: classifier, volume header, record splitter,
decompressor, message scanner, Type 31 parser, summary/benchmark.

Why chosen: each layer can be validated independently and reused by later
replay milestones.

Alternatives: write one monolithic parser or parse only the fields needed by the
current CLI output.

Rejected because: a monolith is hard to test, and output-driven parsing would
not support future replay/benchmark work cleanly.

Trade-offs/debt: more small types and boundaries, but better diagnostics and
reuse.

Review explanation: "I built the decoder as reusable layers because inspection
was the foundation for replay, not just a reporting command."

### Reusable `radarpulse` BZip2 Backend

Decision: add a reusable-workspace BZip2 backend and make it the default while
keeping SharpZipLib and SharpCompress selectable for comparison.

Why chosen: stream-based third-party paths produced large per-record allocation.
The reusable backend preserved byte counts while reducing allocation by orders
of magnitude.

Alternatives: use only SharpZipLib, use only SharpCompress, or avoid custom
decompression work.

Rejected because: allocation pressure was too high for a future replay source,
and comparison backends were still needed for confidence.

Trade-offs/debt: maintaining a decompressor backend adds responsibility and
must be protected by differential validation.

Review explanation: "I kept third-party backends as references, but optimized
the default path for repeated per-record replay."

### Ordered Parallelism

Decision: scan record boundaries in file order, decompress/project records in
parallel, then aggregate by original record index.

Why chosen: compressed records are independent for decompression, but replay
order must remain deterministic.

Alternatives: process sequentially only, publish worker completion order, or
sort later by timestamps.

Rejected because: sequential-only leaves throughput on the table, completion
order is incorrect, and timestamp sorting is not the source-order contract.

Trade-offs/debt: ordered aggregation and prepasses add complexity.

Review explanation: "Parallelism was allowed only behind an ordered merge, so
performance did not weaken replay correctness."

### Preserve Raw, Calibrated, And Status Semantics

Decision: parse raw 8/16-bit values, apply scale/offset only for valid samples,
and preserve sentinel/status counts.

Why chosen: Message Type 31 raw values include below-threshold, range-folded,
CFP, reserved, and unsupported states. Treating every raw value as calibrated
would corrupt the data.

Alternatives: decode only metadata, calibrate everything, or drop status counts.

Rejected because: metadata-only is insufficient for replay, calibrating all
values is wrong, and dropping status removes important flow diagnostics.

Trade-offs/debt: calibrated valid-event throughput differs from raw decoded
throughput, so benchmark labels must be precise.

Review explanation: "The decoder keeps sensor status semantics intact instead
of flattening everything into a numeric value."

### Add Replay-Shape Before Publisher

Decision: create a replay-facing gate-moment event shape and benchmark it before
building the publisher contract.

Why chosen: this proved that ordered event preparation could exceed the initial
20M events/s target before introducing downstream publishing concerns.

Alternatives: jump directly to a publisher or stop at inspection summaries.

Rejected because: direct publishing would mix concerns too early, while
inspection alone would not prove replay-shaped event cost.

Trade-offs/debt: replay-shape benchmark/validator code later overlapped with
the production publisher path.

Review explanation: "Replay-shape was a measured bridge between binary parsing
and the later publisher milestone."

## 3. Remaining Risks And Debt

- The parser covers the Archive Two and Type 31 structure needed for replay,
  not all possible meteorological products.
- Replay-shape benchmark loops were not the final publisher API.
- Ordered parallel projection requires careful preservation of source order in
  future refactors.
- Real radar data is uneven across records, sweeps, radials, and time buckets;
  later processing must not assume uniform flow.

## 4. Portfolio Review Summary

Milestone 002 turned downloaded binary radar files into a reliable, measured
decoder foundation. The main decisions were container-aware Archive Two parsing,
layered decoder boundaries, reusable low-allocation decompression, ordered
parallelism, status-preserving Type 31 decoding, and replay-shape benchmarking.
This proved the historical archive could become a high-throughput ordered event
source.

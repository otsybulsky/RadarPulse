# Handoff: Milestone 002 NEXRAD Archive Inspection

## Current Goal

Plan and start milestone 002: consume cached NOAA NEXRAD archive files from the
milestone 001 archive loader and build a replay-oriented inspection/decoding
foundation.

## Milestone Status

Done:

- `001` historical archive loader is complete.
- `archive list` supports one radar and explicit `--all-radars`.
- Manifest summary output and JSON write/read are implemented.
- `archive download` supports live AWS listing and saved manifests.
- Saved manifest download can be filtered with `--radar`, `--max-files`, and
  `--max-bytes`.
- Download concurrency, retry/backoff, Ctrl+C cancellation, temp-file writes,
  deterministic cache paths, metadata sidecars, skip/redownload behavior, and
  free-space preflight are implemented.
- Standard unit tests and opt-in live AWS integration tests covered the loader
  milestone at handoff time.

Planned next:

- `002` NEXRAD archive inspection/decoding milestone.
- Continue from completed file classification, 24-byte Archive Two volume header
  parsing, compressed record boundary parsing, and per-record BZip2
  decompression summaries.
- Continue from completed serial/parallel and decoder-comparison
  decompression throughput benchmarks before expanding message parsing.
- Design decompression and parsing as the future historical replay input path,
  with an eventual target of feeding up to 20 million events per second into the
  downstream pipeline.
- Classify `_MDM` files separately before attempting base-data parsing.
- Use ROC ICD 2620010J for the Archive Two container and ROC ICD 2620002Y for
  RDA/RPG message payloads, especially Message Type 31.

Completed in the first milestone 002 implementation slice:

- `archive inspect --file`.
- Archive Two base-data classification for files starting with `AR2V`.
- MDM/compressed-stream classification for `_MDM` and early `BZh` non-`AR2V`
  files.
- Unknown binary classification.
- 24-byte Archive Two volume header parsing.
- Archive Two compressed record boundary parsing from 4-byte signed big-endian
  control words.
- Per-record BZip2 signature detection.
- Per-record BZip2 decompression byte counting through the shared BZip2
  decompressor abstraction.
- `archive benchmark decompress --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- Benchmark path pools compressed-payload and output buffers to avoid measuring
  avoidable local buffer churn.
- Parallel benchmark mode scans compressed record boundaries in file order,
  decompresses independent BZip2 records concurrently, and aggregates results by
  original record index so worker completion order does not mix records.
- `radarpulse` is the default BZip2 backend after adding a reusable-workspace
  decoder to remove per-record managed BZip2 workspace allocations.
  SharpZipLib and SharpCompress remain selectable for comparison.
- BZip2 decompression sessions now expose a streaming/chunk callback so future
  parsing can consume decompressed bytes without materializing full records.
- `archive validate decompress` compares the default `radarpulse` backend
  against SharpZipLib record-by-record with streaming hashes.
- Decompressed Archive Two bytes are now scanned through the streaming callback
  for RDA/RPG message headers.
- Minimal Message Type 31 parsing reports radial counts and gate-count totals
  for generic moment data blocks.
- `archive inspect --file` reports message counts by type, Type 31 radial
  counts, estimated gate-moment events, and moment gate/radial totals.
- `archive inspect --file` reports Type 31 `VOL`/`ELV`/`RAD` constant block
  counts and sweep summaries from radial status, elevation number, cut sector,
  elevation angle, moment membership, and source order.
- Type 31 sweep summaries carry explicit source order as compressed record,
  message-in-record, and Type 31 radial sequence positions for future ordered
  replay publishing.
- Type 31 generic moment descriptors now summarize per-moment gate count range,
  word size, first-gate range, gate spacing, scale, and offset. CLI output
  documents the calibration formula `value=(raw-offset)/scale`.
- `archive benchmark parse` supports `--decode-calibrated-moments`. This mode
  reads raw 8/16-bit moment values, preserves Message Type 31 sentinel/status
  semantics, applies per-block scale/offset only to valid samples, and reports
  calibrated value counts, min/max, and a scaled checksum.
- `archive benchmark parse --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress] [--decode-moments]
  [--decode-calibrated-moments]`
  measures decompress+message-scan+minimal-Type31 throughput in estimated
  gate-moment events/s, and optionally reads actual 8/16-bit moment gate
  values or calibrated moment values with checksums.
- A first reusable Type 31 gate-moment event shape is implemented with radar id,
  volume timestamp, sweep/elevation/radial/gate identity, range, moment name,
  raw value, decoded status, optional calibrated value, and explicit source
  order.
- `archive benchmark replay-shape --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`
  projects ordered Type 31 gate-moment events and measures the cost of creating
  the replay-facing event shape before a downstream publisher exists.
- Replay-shape projection supports parallel compressed-record decoding. The
  parallel path first builds per-record starting projector states from Type 31
  radial transitions, then projects records concurrently and aggregates record
  results in original Archive Two record order.
- Replay-shape benchmark output includes an order-sensitive chronology checksum
  on every run, so parallel runs can be compared against sequential runs for
  event-order preservation, not just commutative totals.
- `archive validate replay-shape (--file path | --cache data/nexrad [--radar
  KTLX] [--max-files n]) [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` compares sequential
  ordered projection against parallel replay-shape projection and reports
  calibrated-data unevenness by compressed record, sweep, radial, and minute.
- `archive inspect (--file path | --cache data/nexrad [--date yyyy-MM-dd]
  [--radar KTLX] [--max-files n])` can inspect a single file or aggregate a
  selected cache slice without failing on MDM/unknown files.
- Archive Two volume/framing helpers are centralized in `ArchiveTwoFileReader`
  instead of duplicated across inspector, benchmarks, and validators.
- The inspection path also uses the shared decompressor abstraction and pooled
  compressed-payload/output buffers.
- CLI output for size, kind, archive filename, version, extension number, radar
  id, volume timestamp, compressed record totals, and decompressed byte totals.
- Unit tests with small synthetic fixtures.

## Current Achievement Summary

The current milestone 002 state is a working NEXRAD Archive Two decoder
foundation, not yet a proven 20M events/s replay pipeline.

Achieved:

- RadarPulse recognizes cached Archive Two base-data files that start with
  `AR2V`.
- The reader parses the 24-byte volume header and reports archive filename,
  version, extension number, radar id, and volume timestamp.
- The reader parses Archive Two compressed record boundaries from 4-byte signed
  big-endian control words.
- Each internal BZip2 payload is decompressed per record. The file is correctly
  treated as an Archive Two container, not as one continuous BZip2 stream.
- `_MDM` and early `BZh` non-`AR2V` files are classified separately so they are
  not accidentally parsed as base-data volumes.
- Parallel decompression is implemented for independent compressed records.
  The implementation preserves order by scanning records in file order and
  writing worker results back by original record index.
- Benchmarking now compares the reusable-workspace `radarpulse` BZip2 backend
  with SharpZipLib and SharpCompress on the same Archive Two framing path.
- The `radarpulse` backend is currently the default because it preserves the
  measured byte counts while reducing measured per-record allocation by roughly
  three orders of magnitude on the current KTLX file.
- A local differential validation gate compares `radarpulse` against
  SharpZipLib across selected cached Archive Two files before parser work.
- The inspection path and benchmark path both use the shared BZip2 decompressor
  abstraction and pooled compressed-payload/output buffers.
- The message scanner now validates the RDA/RPG header enough to avoid
  byte-shift false positives in real KTLX records.
- The current KTLX smoke file reports 6_496 messages, including 6_480 Type 31
  radials, and 38_759_040 estimated gate-moment events.
- The current KTLX smoke file reports 12 Type 31 sweeps, 6_480 `VOL`,
  6_480 `ELV`, and 6_480 `RAD` constant blocks, with sweep source ranges
  ordered by compressed record/message/radial position.
- The current KTLX smoke file reports stable descriptor metadata such as
  `REF scale=2 offset=66`, `VEL scale=2 offset=129`, `ZDR scale=32 offset=418`,
  and 0.25 km gate spacing for the observed moments.
- Calibrated decoding on the current KTLX smoke file reports 5_523_459 valid
  calibrated values per volume, 27_316_941 below-threshold values, 1_355
  range-folded values, 5_794_484 CFP filter-not-applied values, 65_871 CFP
  point-clutter-filter values, 56_930 CFP dual-pol-filtered values, no reserved
  or unsupported values, and a calibrated range of `-31.5..359.649`.
- Replay-shape projection on the current KTLX smoke file generates 38_759_040
  ordered gate-moment events per volume, with the same raw checksum
  `1_063_626_011`, calibrated checksum `70_028_121_122`, valid/status counts,
  calibrated range `-31.5..359.649`, range span `2.125..459.875` km, and
  chronology checksum `5_257_350_734_454_804_390`. Sequential and parallel runs
  produced the same chronology checksum.
- Cache-wide KTLX replay-shape validation examined 244 files, skipped 24
  non-base-data files, compared 220 Archive Two base-data files, found zero
  sequential/parallel mismatches, and reported 8_513_587_200 replay-shaped
  events with 1_369_194_138 valid calibrated events.
- The cache-wide unevenness report found the largest compressed-record valid
  share spread in `KTLX20260504_032003_V06`: record 51 had 8.592% valid events
  while record 13 had 50.437%. The largest sweep spread was also in
  `KTLX20260504_032003_V06`: sweep 11 had 9.187% valid events while sweep 2 had
  44.909%.
- Replay-shape validation also reports radial and minute-bucket valid-share
  spreads using message timestamps from the RDA/RPG message header.
- Cache inspection can aggregate selected local cache files and report file-kind,
  compressed-record, decompressed-byte, message, Type 31 radial, and estimated
  gate-moment totals.
- The parse benchmark now gives a first measured answer against the 20M
  events/s target for decompression plus minimal parsing.
- With `--decode-moments`, the same KTLX file decodes all 38_759_040 raw
  gate-moment values per iteration and measures above the 20M values/s target.
- The latest Release performance rerun measured 910.77 decompressed MB/s,
  501_164_693 minimal-parse estimated events/s, 670_226_077 calibrated-parse
  decoded values/s, and 230_347_912 replay-shaped events/s on
  `KTLX20260504_000245_V06` with `radarpulse` and `--parallelism 24`.
- Calibrated parse is faster than replay-shape because it reads/classifies
  values and updates counters/checksums, while replay-shape also builds the
  publisher-facing event shape, carries source-order/time identity, computes
  order-sensitive chronology, and pays for the parallel projector prepass plus
  ordered aggregation. The slower replay-shape path still remains roughly 11.5x
  above the 20M events/s target on this file.

Not achieved yet:

- No downstream event publisher or ordered parallel merge is implemented yet.
- No downstream event publisher is wired to the ordered record merge yet.
- The parser/replay benchmarks still do not publish downstream engine events.

## Documentation

- `docs/milestones/001-historical-loader-plan.md`
- `docs/milestones/001-historical-loader.md`
- `docs/milestones/002-nexrad-archive-inspection-plan.md`
- `docs/milestones/002-nexrad-archive-inspection.md`
- `docs/handoff.md`

## Verification

Last verified normal command after the current milestone 002 slice:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
55 passed, 3 skipped
```

Manual CLI smoke tests:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_005834_V06_MDM
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 2
```

The first command classified the file as `Archive Two base data` and parsed
`AR2V0006.266`, version `06`, extension `266`, radar `KTLX`, and volume time
`2026-05-04T00:02:45.042Z`. It also found 55 compressed records, 5_406_610
compressed bytes, 55 records with BZip2 signatures, 55 decompressed records,
50_741_824 decompressed bytes, zero decompression diagnostics, 6_496 messages,
6_480 Type 31 radials, 38_759_040 estimated gate-moment events, 6_480 each of
`VOL`/`ELV`/`RAD` constant blocks, 12 sweep summaries, and descriptor metadata
for all observed moments. The second command classified the `_MDM` file as
`MDM or compressed stream`. The cache inspect smoke examined 2 KTLX files,
classified both as Archive Two base data, and aggregated 110 compressed records,
101_483_648 decompressed bytes, 12_992 messages, 12_960 Type 31 radials, and
77_518_080 estimated gate-moment events.

Last verified decompression validation command:

```powershell
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive validate decompress --cache data/nexrad --radar KTLX --max-files 20
```

Result:

```text
Candidate decompressor: radarpulse
Reference decompressor: sharpziplib
Examined files: 22
Skipped files: 2
Compared files: 20
Failed files: 0
Compressed records: 1_100
Compressed bytes: 112_494_786
Decompressed bytes: 1_014_836_480
```

Last verified opt-in corpus command:

```powershell
$env:RADARPULSE_RUN_CORPUS_TESTS='true'; $env:RADARPULSE_NEXRAD_CORPUS='data/nexrad'; $env:RADARPULSE_NEXRAD_CORPUS_RADAR='KTLX'; $env:RADARPULSE_NEXRAD_CORPUS_MAX_FILES='20'; dotnet test RadarPulse.sln --no-restore --filter NexradArchiveDecompressionValidatorCorpusTests
```

Result:

```text
1 passed, 0 skipped
```

Last verified decompression benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 10 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Decompressor: radarpulse
Iterations: 10
Warmup iterations: 1
Parallelism: 24
Compressed records per iteration: 55
Compressed bytes per iteration: 5_406_610
Decompressed bytes per iteration: 50_741_824
Elapsed ms: 467.16
Compressed MB/s: 115.73
Decompressed MB/s: 1_086.18
Records/s: 1_177.33
Allocated bytes: 1_243_568
Allocated bytes / decompressed MB: 2_450.78
Allocated bytes / record: 2_261.03
```

Historical SharpCompress baseline before the decoder comparison:

```text
Elapsed ms: 1_606.65
Compressed MB/s: 10.10
Decompressed MB/s: 94.75
Records/s: 102.70
Allocated bytes: 907_268_368
```

After adding the reusable-workspace `radarpulse` decoder, the Release comparison
on the same machine and file produced:

```text
iterations: 10
warmup iterations: 1

decompressor  parallelism  elapsed ms  decompressed MB/s  records/s  allocated bytes  allocated bytes / record
radarpulse    1            3_800.97    133.50             144.70     43_920           79.85
radarpulse    24           467.16      1_086.18           1_177.33   1_243_568        2_261.03
sharpziplib   24           643.11      789.01             855.22     2_511_390_704    4_566_164.92
```

Parallel decompression improves byte throughput substantially on the current
machine. The next parser slice must preserve file/message order when publishing
data: worker completion order is not a valid stream order.

Last verified parse benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Decompressor: radarpulse
Iterations: 20
Warmup iterations: 2
Parallelism: 24
Messages per iteration: 6_496
Type 31 radials per iteration: 6_480
Estimated gate-moment events per iteration: 38_759_040
Elapsed ms: 1_035.20
Compressed MB/s: 104.46
Decompressed MB/s: 980.33
Messages/s: 125_502.63
Type 31 radials/s: 125_193.51
Estimated gate-moment events/s: 748_824_137.31
Allocated bytes: 25_297_992
Allocated bytes / estimated event: 0.03
```

The sequential Release parse benchmark on the same file and backend measured
about 90_930_375 estimated gate-moment events/s with `--parallelism 1`.

Last verified decoded moment benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-moments
```

Result on the current development machine:

```text
Decode moment values: True
Messages per iteration: 6_496
Type 31 radials per iteration: 6_480
Estimated gate-moment events per iteration: 38_759_040
Decoded gate-moment values per iteration: 38_759_040
Decoded gate-moment value checksum per iteration: 1_063_626_011
Elapsed ms: 1_174.67
Decompressed MB/s: 863.93
Estimated gate-moment events/s: 659_912_891.38
Decoded gate-moment values/s: 659_912_891.38
Allocated bytes: 25_314_800
Allocated bytes / decoded value: 0.03
```

The sequential Release decoded benchmark on the same file and backend measured
about 96_122_482 decoded gate-moment values/s with `--parallelism 1`.

Last verified calibrated moment benchmark command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 10 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-calibrated-moments
```

Result on the current development machine:

```text
Decode calibrated moment values: True
Decoded gate-moment values per iteration: 38_759_040
Calibrated gate-moment values per iteration: 5_523_459
Below-threshold gate-moment values per iteration: 27_316_941
Range-folded gate-moment values per iteration: 1_355
CFP filter-not-applied values per iteration: 5_794_484
CFP point-clutter-filter values per iteration: 65_871
CFP dual-pol-filtered values per iteration: 56_930
Reserved gate-moment values per iteration: 0
Unsupported calibrated gate-moment values per iteration: 0
Calibrated gate-moment value scaled checksum per iteration: 70_028_121_122
Calibrated value range per iteration: -31.5..359.649
Elapsed ms: 578.30
Estimated gate-moment events/s: 670_226_077.21
Decoded gate-moment values/s: 670_226_077.21
Calibrated gate-moment values/s: 95_512_331.01
Allocated bytes / calibrated value: 0.66
```

The sequential Release calibrated benchmark on the same file and backend
measured about 10_851_453 valid calibrated values/s, while still reading all
raw gate-moment values at about 76_146_475 decoded values/s.

Last verified replay-shape benchmark command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Replay-shaped events per iteration: 38_759_040
Valid events per iteration: 5_523_459
Below-threshold events per iteration: 27_316_941
Range-folded events per iteration: 1_355
CFP filter-not-applied events per iteration: 5_794_484
CFP point-clutter-filter events per iteration: 65_871
CFP dual-pol-filtered events per iteration: 56_930
Reserved events per iteration: 0
Unsupported events per iteration: 0
Raw value checksum per iteration: 1_063_626_011
Calibrated value scaled checksum per iteration: 70_028_121_122
Chronology checksum per iteration: 5_257_350_734_454_804_390
Calibrated value range per iteration: -31.5..359.649
Range km per iteration: 2.125..459.875
Replay-shaped events/s: 230_347_912.41
Valid events/s: 32_826_335.48
Allocated bytes / event: 0.07
```

The calibrated parse benchmark is intentionally cheaper than replay-shape:
calibrated parse reads/classifies values and updates aggregate counters, while
replay-shape constructs full ordered event records and computes chronology.
The current replay-shape result is still roughly 11.5x above the 20M events/s
target.

Last verified sequential chronology smoke command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 1 --warmup-iterations 0 --parallelism 1 --decompressor radarpulse
```

Result:

```text
Chronology checksum per iteration: 5_257_350_734_454_804_390
Replay-shaped events per iteration: 38_759_040
Raw value checksum per iteration: 1_063_626_011
Calibrated value scaled checksum per iteration: 70_028_121_122
```

Latest replay-shape validation smoke command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive validate replay-shape --cache data/nexrad --radar KTLX --max-files 1 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Compared files: 1
Failed files: 0
Record valid-share spread: 21.858%
Sweep valid-share spread: 18.336%
Radial valid-share spread: 27.012%
Minute valid-share spread: 14.757%
```

Previously verified full cache-wide replay-shape validation command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive validate replay-shape --cache data/nexrad --radar KTLX --parallelism 24 --decompressor radarpulse
```

Result on the current cache:

```text
Examined files: 244
Skipped files: 24
Compared files: 220
Failed files: 0
Replay-shaped events: 8_513_587_200
Valid events: 1_369_194_138
Valid event share: 16.082%
Reserved events: 0
Unsupported events: 0
Record valid-share spread top file: KTLX20260504_032003_V06, record 51 8.592% -> record 13 50.437%
Sweep valid-share spread top file: KTLX20260504_032003_V06, sweep 11 9.187% -> sweep 2 44.909%
```

Last verified normal command for milestone 001:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
25 passed, 2 skipped
```

The skipped tests are opt-in live AWS integration tests.

Last verified full opt-in command for milestone 001:

```powershell
$env:RADARPULSE_RUN_INTEGRATION_TESTS='true'; dotnet test RadarPulse.sln --no-restore
```

Result:

```text
27 passed, 0 skipped
```

Manual CLI smoke test used by the user:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --date 2026-05-04 --radar KTLX --output data/nexrad
```

The files downloaded successfully. Re-running the same command skipped existing
valid files.

## Cache Layout

Downloaded files are stored deterministically:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Example for the manual smoke test:

```text
data/nexrad/level2/2026/05/04/KTLX/{fileName}
```

## Decoder Observations

Local inspection of the cached KTLX files showed:

```text
KTLX20260504_000245_V06 starts with AR2V0006...KTLX and later contains BZh9
KTLX20260504_005834_V06_MDM does not start with AR2V and contains BZh9 early
```

This supports the milestone 002 plan: first classify files, then parse Archive
II volume structure and its internal compressed records.

Additional documentation search found:

```text
ROC ICD 2620010J Archive II/User, Build 23.0:
  https://www.roc.noaa.gov/public-documents/icds/2620010J.pdf
ROC ICD 2620002Y RDA/RPG, Build 23.0:
  https://www.roc.noaa.gov/public-documents/icds/2620002Y.pdf
ROC ICD index:
  https://www.roc.noaa.gov/interface-control-documents.php
NCEI NEXRAD archive overview:
  https://www.ncei.noaa.gov/products/radar/next-generation-weather-radar
NCEI decoding utilities:
  https://www.ncei.noaa.gov/products/radar/decoding-utilities-examples
```

The expected base-data record shape is:

```text
24-byte Archive Two volume header
repeated records:
  4-byte big-endian signed control word
  abs(control word) bytes of bzip2-compressed Archive Two messages
```

The first compressed record contains metadata messages. Later records contain
radial messages, primarily Message Type 31, and may include Message Type 2 RDA
status messages. Message Type 31 represents one radial and contains pointers to
constant and moment data blocks.

## Constraints

- Live AWS tests remain opt-in because they require network access and public AWS
  availability.
- Do not use the deprecated `noaa-nexrad-level2` bucket for loader work.
- Large downloaded data and generated manifests under `data/` stay outside
  source control.
- Do not commit large real NEXRAD archive binary fixtures unless a deliberate fixture
  strategy is agreed first.
- Milestone 002 should avoid promising visualization, event processing,
  partitioning, production replay benchmarking, or live ingestion.
- The decompression throughput check should guide parser design toward the
  eventual 20M events/s replay target.
- Parallel decompression is allowed only behind an ordered merge or another
  explicit ordering contract; historical replay must not accidentally publish
  messages/events in worker completion order.

## Important Files

- `src/Presentation/Program.cs`
- `src/Application/Archive/IHistoricalArchiveClient.cs`
- `src/Application/Archive/HistoricalArchiveManifestSelector.cs`
- `src/Infrastructure/Archive/AwsNexradArchiveClient.cs`
- `src/Infrastructure/Archive/ArchiveBZip2Decompressors.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloader.cs`
- `src/Infrastructure/Archive/IArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/ArchiveTwoFileReader.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageStreamScanner.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageSummaryBuilder.cs`
- `src/Domain/Archive/ArchiveTwoGateMomentEvent.cs`
- `src/Domain/Archive/NexradArchiveCacheInspection.cs`
- `src/Domain/Archive/ArchiveTwoReplayShapeBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveTwoReplayShapeValidationResult.cs`
- `src/Infrastructure/Archive/IArchiveTwoMessageConsumer.cs`
- `src/Infrastructure/Archive/ArchiveTwoGateMomentChronologyChecksum.cs`
- `src/Infrastructure/Archive/ArchiveTwoGateMomentEventProjector.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveParseBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayShapeBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayShapeValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveCacheInspector.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveFileInspector.cs`
- `src/Infrastructure/Archive/ReusableArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/NexradCachePathMapper.cs`
- `src/Infrastructure/Archive/SharpCompressArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/SharpZipLibArchiveBZip2Decompressor.cs`
- `tests/RadarPulse.Tests/Archive/*`

## Done Criteria For Next Slice

Milestone 002 is now functionally complete for archive inspection/decoder
foundation purposes. The next slice should move into the downstream replay
milestone and should be considered done when:

- The next publisher-facing API consumes the ordered record merge instead of
  letting worker completion order reach the downstream pipeline.
- Parser/replay outputs are wired into the downstream event pipeline rather than
  only benchmarked and validated inside the archive layer.


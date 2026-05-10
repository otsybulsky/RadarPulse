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
- `archive benchmark parse --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` measures
  decompress+message-scan+minimal-Type31 throughput in estimated
  gate-moment events/s.
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
- The parse benchmark now gives a first measured answer against the 20M
  events/s target for decompression plus minimal parsing.

Not achieved yet:

- No real event stream is generated yet.
- Moment sample values are not decoded or calibrated yet.
- Sweep/elevation grouping is not summarized yet.
- The parser benchmark reports estimated gate-moment events from moment block
  metadata; it does not yet publish downstream engine events.

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
48 passed, 3 skipped
```

Manual CLI smoke tests:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_005834_V06_MDM
```

The first command classified the file as `Archive Two base data` and parsed
`AR2V0006.266`, version `06`, extension `266`, radar `KTLX`, and volume time
`2026-05-04T00:02:45.042Z`. It also found 55 compressed records, 5_406_610
compressed bytes, 55 records with BZip2 signatures, 55 decompressed records,
50_741_824 decompressed bytes, zero decompression diagnostics, 6_496 messages,
6_480 Type 31 radials, and 38_759_040 estimated gate-moment events. The second
command classified the `_MDM` file as `MDM or compressed stream`.

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
- `src/Infrastructure/Archive/ArchiveTwoMessageStreamScanner.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageSummaryBuilder.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveParseBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveFileInspector.cs`
- `src/Infrastructure/Archive/ReusableArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/NexradCachePathMapper.cs`
- `src/Infrastructure/Archive/SharpCompressArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/SharpZipLibArchiveBZip2Decompressor.cs`
- `tests/RadarPulse.Tests/Archive/*`

## Done Criteria For Next Slice

The next milestone 002 implementation slice should be considered done when:

- Type 31 sweep/elevation/radial sequencing is summarized from `VOL`/`ELV`/`RAD`
  constant blocks and radial status.
- Message order is represented explicitly enough for a future ordered replay
  publisher.
- Tests cover the next Type 31 metadata fields with small fixtures.


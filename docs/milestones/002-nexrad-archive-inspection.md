# Milestone 002: NEXRAD Archive Inspection

RadarPulse will add a replay-oriented inspection layer for cached NOAA NEXRAD
archive files downloaded by milestone 001.

## Current Status

Initial file classification and single-file CLI inspection are implemented.

The first cached KTLX base-data files look like Archive Two volumes: they start
with an `AR2V` header and contain internal BZip2-compressed records. These files
are binary by design and should not be treated as plain text or as a single
standard BZip2 stream.

Some cached `_MDM` files do not start with `AR2V`; they should be identified as
a separate file kind before the base-data volume parser attempts to read them.

Implemented:

```text
archive inspect --file
Archive Two base-data file classification
MDM/compressed-stream classification
unknown binary classification
24-byte Archive Two volume header parsing
Archive Two compressed record boundary parsing
per-record BZip2 signature detection
per-record BZip2 decompression byte counting
decompression throughput benchmark
pooled compressed-payload and output buffers in the benchmark path
parallel per-record decompression benchmark with ordered result aggregation
selectable radarpulse/SharpZipLib/SharpCompress BZip2 benchmark backends
radarpulse as the default reusable-workspace BZip2 backend
streaming/chunk decompression callback for future parsers
differential decompression validation against SharpZipLib
streaming RDA/RPG message header scanning
minimal Message Type 31 moment metadata parsing
message counts, Type 31 radial counts, and estimated gate-moment event counts
Type 31 VOL/ELV/RAD constant block counts
Type 31 sweep/elevation/radial sequencing summaries with source order
parse throughput benchmark for decompress+message-scan+minimal-Type31
optional raw 8/16-bit Type 31 moment value decode benchmark
CLI output for file kind, size, archive filename, version, extension, radar id, volume time, compressed record count, compressed bytes, BZip2 signature count, decompressed record count, and decompressed bytes
unit tests with small synthetic fixtures
```

Not yet implemented:

```text
moment sample calibration
ordered event publishing
```

## Intended Usage

Inspect one cached file:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Expected summary shape:

```text
File: data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
Kind: Archive Two volume
Radar: KTLX
Volume time: 2026-05-04T00:02:45Z
Messages: 31=...
Sweeps: ...
Moments: REF, VEL, SW, ZDR, CC, PHI
```

Current implemented output is intentionally smaller:

```text
File: data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06
Size bytes: 5_406_854
Kind: Archive Two base data
Archive filename: AR2V0006.266
Version: 06
Extension number: 266
Radar: KTLX
Volume time: 2026-05-04T00:02:45.042Z
Compressed records: 55
Compressed bytes: 5_406_610
Records with BZip2 signature: 55
Decompressed records: 55
Decompressed bytes: 50_741_824
Records with decompression diagnostics: 0
First record compressed bytes: 2_357
First record decompressed bytes: 325_888
Messages: 6_496
Message types: 2=4, 3=1, 5=1, 15=5, 18=4, 31=6_480, 32=1
Type 31 radials: 6_480
Estimated gate-moment events: 38_759_040
Type 31 constant blocks: VOL=6_480, ELV=6_480, RAD=6_480
Moments: CFP=6_219_360 gates/4_320 radials, PHI=4_749_120 gates/4_320 radials, REF=8_794_080 gates/6_480 radials, RHO=4_749_120 gates/4_320 radials, SW=4_749_120 gates/4_320 radials, VEL=4_749_120 gates/4_320 radials, ZDR=4_749_120 gates/4_320 radials
Sweeps: 12
Sweep 1: elevation=1, cutSector=1, radials=720, angle=0.44-0.46 deg avg=0.44 deg, status=start volume (3)->end elevation (2), source=2/1/1->7/120/720, moments=CFP,PHI,REF,RHO,ZDR
```

Inspect a small cache selection after cache selectors are added:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 1
```

Validate the reusable BZip2 backend against SharpZipLib on a local cache sample:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive validate decompress --cache data/nexrad --radar KTLX --max-files 20
```

## Supported File Kinds

Initial support should distinguish:

```text
Archive Two base-data volume
MDM-shaped file
external gzip/bzip2 wrapper, if encountered
unknown binary
```

Only Archive Two base-data volumes are expected to produce full inspection
summaries in the first implementation slice.

## Decoder Notes

NEXRAD Archive Two files are binary radar volumes. Modern files commonly
contain a 24-byte volume header followed by internally compressed records. The
presence of `BZh` inside the file does not mean the entire file can be passed to
a normal BZip2 decompressor.

The primary format reference is ROC ICD 2620010J, "Interface Control Document for
Archive II/User", Build 23.0. It describes the Archive Two application layer,
including the volume header and LDM compressed record layout. ROC ICD 2620002Y,
"Interface Control Document for RDA/RPG", Build 23.0, describes the message
payloads inside those records, including Message Type 31.

Expected base-data container shape:

```text
24-byte Archive Two volume header
4-byte big-endian signed control word
abs(control word) bytes of bzip2-compressed messages
repeat compressed records until end of file
```

The first compressed record contains metadata messages. Later records contain
radial messages, primarily Message Type 31, and may include Message Type 2 RDA
status messages. Message Type 31 represents one radial and includes pointers to
constant and moment data blocks.

The first parser should work in layers:

```text
file classifier
volume header reader
record splitter
record decompressor
message header reader
Message 31 metadata reader
inspection summary builder
```

Reference links:

```text
https://www.roc.noaa.gov/public-documents/icds/2620010J.pdf
https://www.roc.noaa.gov/public-documents/icds/2620002Y.pdf
https://www.roc.noaa.gov/interface-control-documents.php
https://www.ncei.noaa.gov/products/radar/next-generation-weather-radar
https://www.ncei.noaa.gov/products/radar/decoding-utilities-examples
```

## Performance Notes

The historical replay path is expected to become a high-throughput event source.
Decompression and parsing should be evaluated against an eventual target of
feeding up to 20 million events per second into the downstream pipeline.

The current inspection command verifies correctness and byte counts. The
benchmark command measures decompression throughput for cached files before
deeper message parsing is expanded:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 3 --warmup-iterations 1 --parallelism 1 --decompressor sharpcompress
```

Initial baseline on the current development machine:

```text
Compressed records per iteration: 55
Compressed bytes per iteration: 5_406_610
Decompressed bytes per iteration: 50_741_824
Elapsed ms: 1_664.43
Compressed MB/s: 9.74
Decompressed MB/s: 91.46
Records/s: 99.13
Allocated bytes: 923_734_856
```

After pooling the benchmark compressed-payload and output buffers, the same
Release benchmark produced:

```text
Elapsed ms: 1_606.65
Compressed MB/s: 10.10
Decompressed MB/s: 94.75
Records/s: 102.70
Allocated bytes: 907_268_368
```

Parallel per-record decompression now accepts `--parallelism n`, and the
benchmark accepts `--decompressor radarpulse|sharpziplib|sharpcompress`. The current
implementation first scans the Archive Two record boundaries in file order,
then decompresses each independent BZip2 payload in parallel. Results are stored
by original record index before aggregation, so worker completion order does not
change record order. The same rule must be preserved when this becomes a real
event producer: parallel stages may finish out of order, but publication to any
ordered stream must use an ordered merge by source record/message position.

Release comparison on the current development machine with the same KTLX file:

```text
iterations: 10
warmup iterations: 1

decompressor  parallelism  elapsed ms  decompressed MB/s  records/s  allocated bytes  allocated bytes / record
radarpulse    1            3_800.97    133.50             144.70     43_920           79.85
radarpulse    24           467.16      1_086.18           1_177.33   1_243_568        2_261.03
sharpziplib   24           643.11      789.01             855.22     2_511_390_704    4_566_164.92
```

The baseline is useful but not sufficient for the eventual 20M events/s replay
target. Parallel decompression significantly improves byte throughput on this
machine. The reusable-workspace `radarpulse` backend is the default because it
preserves byte counts while removing the large per-record managed BZip2
workspace allocations seen in the stream-based SharpZipLib path. Parser work
after this should avoid unnecessary copies and should keep lower allocation
pressure and ordered parallelism in view.

The first parse benchmark measures decompression plus streaming message scan and
minimal Message Type 31 moment metadata extraction. On the same KTLX file, the
current development machine produced:

```text
command: archive benchmark parse --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse

messages per iteration: 6_496
Type 31 radials per iteration: 6_480
estimated gate-moment events per iteration: 38_759_040
elapsed ms: 1_035.20
decompressed MB/s: 980.33
messages/s: 125_502.63
Type 31 radials/s: 125_193.51
estimated gate-moment events/s: 748_824_137.31
allocated bytes / estimated event: 0.03
```

The same path with `--parallelism 1` measured about 90_930_375 estimated
gate-moment events/s. These are parser-front-end measurements only: they do not
publish downstream engine events yet.

With `--decode-moments`, the parse benchmark also reads actual 8/16-bit moment
gate values and accumulates a checksum so the loop cannot be treated as
metadata-only counting. On the same file:

```text
command: archive benchmark parse --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-moments

decoded gate-moment values per iteration: 38_759_040
decoded gate-moment value checksum per iteration: 1_063_626_011
elapsed ms: 1_174.67
decompressed MB/s: 863.93
decoded gate-moment values/s: 659_912_891.38
allocated bytes / decoded value: 0.03
```

The same decoded path with `--parallelism 1` measured about 96_122_482 decoded
gate-moment values/s. These values are raw encoded moment samples, not calibrated
meteorological values.

The validation command compares `radarpulse` against SharpZipLib per compressed
record using streaming hashes. On the local KTLX corpus sample it compared 20
Archive Two files, 1_100 compressed records, and 1_014_836_480 decompressed bytes
with zero failures.

## Limitations

Milestone 002 should not promise:

```text
rendered radar imagery
full moment value calibration
geospatial projection
event detection
benchmark-ready replay
live ingestion
```

Those belong to later milestones after the binary file reader is stable.


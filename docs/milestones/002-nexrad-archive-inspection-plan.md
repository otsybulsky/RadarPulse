# Milestone 002: NEXRAD Archive Inspection Plan

## Goal

Milestone 002 consumes cached NOAA NEXRAD archive files from milestone 001 and
turns them into inspectable radar-volume metadata.

This milestone does not implement event processing, partitioning, live
ingestion, production replay benchmarking, or a user-facing visualization. Its
purpose is to prove that RadarPulse can recognize historical NEXRAD archive
files, decompress their internal records, parse enough structure to understand
volumes/sweeps/radials, and report what is inside a cached file.

## Starting Point

Milestone 001 downloads files into the deterministic cache:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

The current smoke-test cache includes KTLX files from 2026-05-04. A typical base
data file begins with an `AR2V` Archive Two volume header and contains internal
BZip2-compressed records. `_MDM` files have a different shape and should be
classified separately before RadarPulse tries to parse them as base data
volumes.

## Format References

Primary references for this milestone:

```text
ROC ICD 2620010J: Interface Control Document for Archive II/User, Build 23.0
ROC ICD 2620002Y: Interface Control Document for RDA/RPG, Build 23.0
NCEI NEXRAD archive overview
NCEI decoding utilities and examples
```

Useful links:

```text
https://www.roc.noaa.gov/public-documents/icds/2620010J.pdf
https://www.roc.noaa.gov/public-documents/icds/2620002Y.pdf
https://www.roc.noaa.gov/interface-control-documents.php
https://www.ncei.noaa.gov/products/radar/next-generation-weather-radar
https://www.ncei.noaa.gov/products/radar/decoding-utilities-examples
```

The Archive II/User ICD describes the historical NEXRAD archive container used by the
downloaded files. The RDA/RPG ICD describes the radar messages inside that
container, especially Message Type 31, Digital Radar Data Generic Format.

## Initial Capability

Required input:

```text
one cached NEXRAD archive file path
```

Optional inputs:

```text
cache directory
date
radar id
max files
include unsupported files
```

The first implementation should focus on one file at a time. Cache-wide
inspection can be added after the single-file reader is reliable.

Current status:

```text
single-file archive inspect command implemented
file classification implemented
24-byte Archive Two volume header parsing implemented
compressed record boundary parsing implemented
BZip2 signature detection implemented
per-record BZip2 decompression byte counting implemented
decompression throughput benchmark implemented
pooled compressed payload and output buffers implemented for benchmark path
parallel per-record decompression benchmark implemented
selectable SharpCompress/SharpZipLib BZip2 benchmark backends implemented
SharpZipLib selected as the default managed backend after A/B benchmarking
cache-wide inspection not implemented
message header parsing not implemented
```

## Performance Target

Historical decompression and parsing are not just inspection utilities. They are
the offline replay input path for the event engine. The decompression and parser
pipeline must be designed and measured with an eventual target of feeding up to
20 million events per second.

The decompression benchmark reports:

```text
input file path
compressed bytes read
decompressed bytes produced
compressed records processed
elapsed time
compressed MB/s
decompressed MB/s
records/s
degree of parallelism
decompressor backend
allocation pressure, when practical
```

Initial baseline command:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 3 --warmup-iterations 1 --parallelism 1 --decompressor sharpcompress
```

Initial baseline result on the current development machine:

```text
compressed records per iteration: 55
compressed bytes per iteration: 5_406_610
decompressed bytes per iteration: 50_741_824
elapsed ms: 1_664.43
compressed MB/s: 9.74
decompressed MB/s: 91.46
records/s: 99.13
allocated bytes: 923_734_856
```

After pooling the benchmark compressed-payload and output buffers, the same
Release benchmark produced:

```text
elapsed ms: 1_606.65
compressed MB/s: 10.10
decompressed MB/s: 94.75
records/s: 102.70
allocated bytes: 907_268_368
```

After adding parallel per-record decompression, a longer Release comparison on
the same machine and file produced:

```text
iterations: 10
warmup iterations: 1

decompressor   parallelism  elapsed ms  decompressed MB/s  records/s  allocated bytes  allocated bytes / decompressed MB
sharpcompress  1            5_299.00    95.76              103.79     3_024_135_496    5_959_847.83
sharpcompress  24           689.91      735.48             797.20     3_028_736_312    5_968_914.94
sharpziplib    1            4_545.02    111.64             121.01     2_510_325_344    4_947_250.90
sharpziplib    24           518.16      979.27             1_061.45   2_514_650_928    4_955_775.59
```

The parallel benchmark preserves record order by scanning the Archive Two file
into ordered record descriptors first. Workers decompress records independently,
but each worker stores its result at the original record index. Future event
production must keep the same rule: parallel decompression may finish out of
order, but parsed messages/events must be merged or published according to the
original file order unless a later partitioning design explicitly defines a
different ordering contract.

The 20M events/s target should be interpreted as a downstream throughput
requirement for parsed event generation, not as a claim that the current
inspection command already reaches it. The benchmark shows working
SharpCompress and SharpZipLib paths. SharpZipLib is faster and allocates less,
so it is the default managed backend, but allocation pressure remains high.
Parser design should avoid extra copies and should consider streaming, buffer
reuse, parallel file/record processing, and native/custom-allocator BZip2
options before deeper message parsing is built on top.

## Decoder Workflow

Step 1: classify the file.

```text
Archive Two volume with AR2V header
MDM or compressed message stream
externally compressed gzip/bzip2 wrapper, if encountered
unknown or unsupported binary
```

Step 2: parse the Archive Two container.

```text
volume header
LDM record framing
internal BZip2 record payloads
radar message stream
```

Expected Archive Two raw/LDM record shape for the base-data files:

```text
24-byte volume header
repeated records:
  4-byte big-endian signed control word
  abs(control word) bytes of bzip2-compressed Archive Two messages
```

The first compressed record is expected to contain metadata messages. Later
compressed records contain radial messages, primarily Message Type 31, plus
possible Message Type 2 RDA status messages. The reader must decompress each
record independently.

Step 3: parse minimal radar messages.

```text
generic message header
message type
message timestamp
Message 31 radial header
Message 31 data block pointers
moment block metadata
```

Message Type 31 contains one radial of data. Its data block pointers lead to
constant blocks such as `VOL`, `ELV`, and `RAD`, and moment blocks such as
`REF`, `VEL`, `SW`, `ZDR`, `PHI`, and `RHO`/correlation coefficient depending on
the build and decoder naming.

Step 4: summarize the volume.

```text
station id
volume timestamp
message counts by type
sweep count
radial count per sweep
elevation angles
available moments
gate counts and spacing, when available
```

## Proposed CLI Shape

Candidate command for the first slice:

```text
radarpulse archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Candidate command after cache selection exists:

```text
radarpulse archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 1
```

The command should report unsupported file kinds without failing the whole
inspection run, unless the user explicitly selects one file and that file cannot
be parsed.

## Code Boundaries

In scope:

```text
NEXRAD archive file classification
Archive Two volume header parsing
LDM record framing
internal BZip2 decompression
generic radar message header parsing
minimal Message 31 parsing
inspection summary model
CLI entry point for inspection
focused tests and fixtures
```

Out of scope:

```text
full meteorological product decoding
rendering or map visualization
event engine processing
logical source partitioning
production replay benchmark suite
live SNS/SQS ingestion
automatic download during inspection
```

## Test Plan

Unit tests:

```text
file classifier recognizes AR2V volume files
file classifier separates MDM-shaped files
volume header parser reads station and version fields
LDM record splitter handles compressed record boundaries
BZip2 record decompression produces radar message bytes
generic message header parser reads message type and timestamp fields
Message 31 parser extracts sweep/radial/moment metadata
inspection summary aggregates counts deterministically
unsupported files return clear diagnostics
```

Fixture strategy:

```text
prefer small synthetic byte fixtures for fixed header parsing
use one real cached KTLX file only for opt-in local integration coverage
do not commit large downloaded NEXRAD archive data under data/
```

## Done Criteria

Milestone 002 is complete when:

```text
RadarPulse can inspect one cached Archive Two base-data file
the reader handles internal BZip2 records correctly
the command prints stable volume/sweep/radial/moment metadata
MDM and unknown binary files are classified without accidental base-data parsing
behavior is covered by focused tests
documentation describes the supported file kinds and limitations
```


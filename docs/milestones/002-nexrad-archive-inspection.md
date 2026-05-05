# Milestone 002: NEXRAD Archive Inspection

RadarPulse will add a replay-oriented inspection layer for cached NOAA NEXRAD
NEXRAD archive files downloaded by milestone 001.

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
CLI output for file kind, size, archive filename, version, extension, radar id, volume time, compressed record count, compressed bytes, and BZip2 signature count
unit tests with small synthetic fixtures
```

Not yet implemented:

```text
per-record BZip2 decompression
radar message header parsing
Message Type 31 radial metadata parsing
sweep/radial/moment summaries
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
First record compressed bytes: 2_357
```

Inspect a small cache selection after cache selectors are added:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 1
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


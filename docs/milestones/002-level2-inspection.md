# Milestone 002: Level II Inspection

RadarPulse will add a replay-oriented inspection layer for cached NOAA NEXRAD
Level II files downloaded by milestone 001.

## Current Status

Planned, not implemented.

The first cached KTLX base-data files look like Archive II volumes: they start
with an `AR2V` header and contain internal BZip2-compressed records. These files
are binary by design and should not be treated as plain text or as a single
standard BZip2 stream.

Some cached `_MDM` files do not start with `AR2V`; they should be identified as
a separate file class before the base-data volume parser attempts to read them.

## Intended Usage

Inspect one cached file:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
```

Expected summary shape:

```text
File: data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
Class: Archive II volume
Radar: KTLX
Volume time: 2026-05-04T00:02:45Z
Messages: 31=...
Sweeps: ...
Moments: REF, VEL, SW, ZDR, CC, PHI
```

Inspect a small cache selection after cache selectors are added:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 1
```

## Supported File Classes

Initial support should distinguish:

```text
Archive II base-data volume
MDM-shaped file
external gzip/bzip2 wrapper, if encountered
unknown binary
```

Only Archive II base-data volumes are expected to produce full inspection
summaries in the first implementation slice.

## Decoder Notes

NEXRAD Level II Archive II files are binary radar volumes. Modern files commonly
contain a 24-byte volume header followed by internally compressed records. The
presence of `BZh` inside the file does not mean the entire file can be passed to
a normal BZip2 decompressor.

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

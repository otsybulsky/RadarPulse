# Milestone 002: Level II Inspection Plan

## Goal

Milestone 002 consumes cached NOAA NEXRAD Level II files from milestone 001 and
turns them into inspectable radar-volume metadata.

This milestone does not implement event processing, partitioning, benchmarks,
live ingestion, or a user-facing visualization. Its purpose is to prove that
RadarPulse can recognize historical Level II archive files, decompress their
internal records, parse enough structure to understand volumes/sweeps/radials,
and report what is inside a cached file.

## Starting Point

Milestone 001 downloads files into the deterministic cache:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

The current smoke-test cache includes KTLX files from 2026-05-04. A typical base
data file begins with an `AR2V` Archive II volume header and contains internal
BZip2-compressed records. `_MDM` files have a different shape and should be
classified separately before RadarPulse tries to parse them as base data
volumes.

## Initial Capability

Required input:

```text
one cached Level II file path
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

## Decoder Workflow

Step 1: classify the file.

```text
Archive II volume with AR2V header
MDM or compressed message stream
externally compressed gzip/bzip2 wrapper, if encountered
unknown or unsupported binary
```

Step 2: parse the Archive II container.

```text
volume header
LDM record framing
internal BZip2 record payloads
radar message stream
```

Step 3: parse minimal radar messages.

```text
generic message header
message type
message timestamp
Message 31 radial header
Message 31 data block pointers
moment block metadata
```

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

The command should report unsupported file classes without failing the whole
inspection run, unless the user explicitly selects one file and that file cannot
be parsed.

## Code Boundaries

In scope:

```text
Level II file classification
Archive II volume header parsing
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
throughput benchmarks
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
do not commit large downloaded Level II data under data/
```

## Done Criteria

Milestone 002 is complete when:

```text
RadarPulse can inspect one cached Archive II base-data file
the reader handles internal BZip2 records correctly
the command prints stable volume/sweep/radial/moment metadata
MDM and unknown binary files are classified without accidental base-data parsing
behavior is covered by focused tests
documentation describes the supported file classes and limitations
```

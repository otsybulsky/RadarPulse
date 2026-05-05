# Milestone 001: Historical NEXRAD Loader Plan

## Goal

Milestone 001 is a historical NOAA NEXRAD Level II data loader.

This milestone does not implement radar decoding, event processing,
partitioning, or benchmark execution. Its purpose is to reliably discover,
describe, and download historical radar archive files from public AWS S3 so
later milestones can use a deterministic local dataset.

## Data Source

Primary archive source:

```text
AWS Open Data: NEXRAD Level II archive
Bucket: unidata-nexrad-level2
```

The older `noaa-nexrad-level2` archive bucket is deprecated and should not be
used for new implementation work.

## Initial Capability

The loader must support loading all available radar archive files for a selected
calendar date.

Required inputs:

```text
date
output/cache directory
```

Optional inputs:

```text
radar ids
max files
max bytes
download concurrency
saved manifest path
```

The all-radars mode must be explicit. Downloading a full day across the radar
network can involve large data volume and should not happen accidentally.

## Manifest-First Workflow

The loader separates discovery from download.

Step 1: build a manifest.

```text
radar id
archive date
archive path
file name
object size
last modified timestamp
parsed radar volume timestamp, when available
```

Step 2: summarize the manifest.

```text
radar count
file count
total bytes
per-radar file count
per-radar byte count
```

Step 3: download selected manifest entries.

The downloader is resumable and cache-aware:

```text
skip existing valid files
redownload missing or size-mismatched files
respect cancellation
respect concurrency limits
support disk budget guardrails
```

## Expected Data Volume

A full-day, all-radars download can be large.

NEXRAD Level II files are produced repeatedly throughout the day for roughly
160 radar sites. A full network day can reasonably be tens to hundreds of GB,
depending on date, radar availability, compression, and weather activity.

The loader should therefore default to safe behavior:

```text
list before download
show estimated total size
require explicit all-radars download intent
allow file/byte limits for test runs
```

## Proposed CLI Shape

Implemented commands:

```text
radarpulse archive list --date 2026-05-04 --radar KTLX
radarpulse archive list --date 2026-05-04 --all-radars
radarpulse archive download --date 2026-05-04 --radar KTLX --output data/nexrad
radarpulse archive download --date 2026-05-04 --all-radars --output data/nexrad
radarpulse archive download --manifest data/manifests/2026-05-04-KTLX.json --output data/nexrad
radarpulse archive download --manifest data/manifests/2026-05-04.json --radar KTLX --max-files 10 --output data/nexrad
```

Bulk download prints the manifest summary before starting and requires explicit
all-radars intent.

## Local Cache Layout

Recommended local layout:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

The cache layout is deterministic so future replay and benchmark runs can refer
to local files without depending on live AWS availability.

Large downloaded data should remain outside source control.

## Code Boundaries

This milestone introduces only loader-oriented contracts and implementation.

In scope:

```text
Historical archive request
Historical archive manifest
Historical archive file metadata
S3 archive listing
S3 archive download
Manifest read/write
Local cache path mapping
CLI entry points for list/download
```

Out of scope:

```text
Level II decoding
Radar measurements
Logical source partitioning
Event engine processing
Throughput benchmarks
Realtime SNS/SQS ingestion
```

## Test Plan

Unit tests:

```text
S3 prefix generation
archive key parsing
radar id parsing
volume timestamp parsing, when available
cache path mapping
manifest summary calculation
manifest JSON read/write
saved-manifest filtering
download skip/redownload decision logic
cache metadata sidecar validation
cache metadata backfill for legacy matching files
temp-file write/move behavior
preflight free-space guardrail
```

Integration tests:

```text
opt-in only
disabled by default
require explicit environment variable
use a small radar/date/file limit
verify public S3 listing behavior
verify public S3 object download behavior with a size cap
```

Integration tests should not be required for normal local or CI test runs
because they depend on network access and public AWS availability.

## Documentation Deliverables

This milestone includes:

```text
dedicated loader document
AWS bucket and archive layout notes
CLI examples
cache layout explanation
bulk download warning
test strategy
known limitations
```

## Phase 001A: Manifest Milestone

The first concrete implementation target:

```text
1. Create .NET project structure.
2. Add archive loader contracts.
3. Implement manifest generation for a date.
4. Implement local cache path mapping.
5. Add unit tests for parsing, prefixes, and cache paths.
6. Add documentation for usage and data volume expectations.
```

Current status:

```text
complete
```

## Phase 001B: Download Milestone

The next concrete implementation target:

```text
1. Add archive download command.
2. Require --output for download execution.
3. Download selected manifest entries into the deterministic cache layout.
4. Skip existing valid files by size.
5. Redownload missing or size-mismatched files.
6. Write downloads to temporary files before final move.
7. Respect cancellation and concurrency limits.
8. Enforce byte/disk budget guardrails.
9. Support download from saved manifest JSON when provided.
10. Add unit tests for skip/redownload and path/write decisions.
```

Current status:

```text
complete
```

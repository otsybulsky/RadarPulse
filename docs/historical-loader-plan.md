# Historical NEXRAD Loader Plan

## Goal

The first RadarPulse milestone is a historical NOAA NEXRAD Level II data loader.

This milestone does not implement radar decoding, event processing, partitioning, or
benchmark execution. Its purpose is to reliably discover, describe, and download
historical radar archive files from public AWS S3 so later milestones can use a
deterministic local dataset.

## Data Source

Primary archive source:

```text
AWS Open Data: NEXRAD Level II archive
Bucket: unidata-nexrad-level2
```

The older `noaa-nexrad-level2` archive bucket is deprecated and should not be used
for new implementation work.

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
```

The all-radars mode must be explicit. Downloading a full day across the radar
network can involve large data volume and should not happen accidentally.

## Manifest-First Workflow

The loader should separate discovery from download.

Step 1: build a manifest.

```text
radar id
archive date
S3 bucket
S3 key
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

The downloader should be resumable and cache-aware:

```text
skip existing valid files
redownload missing or size-mismatched files
respect cancellation
respect concurrency limits
support disk budget guardrails
```

## Expected Data Volume

A full-day, all-radars download can be large.

NEXRAD Level II files are produced repeatedly throughout the day for roughly 160
radar sites. A full network day can reasonably be tens to hundreds of GB,
depending on date, radar availability, compression, and weather activity.

The loader should therefore default to safe behavior:

```text
list before download
show estimated total size
require explicit all-radars download intent
allow file/byte limits for test runs
```

## Proposed CLI Shape

Initial commands:

```text
radarpulse archive list --date 2026-05-04
radarpulse archive list --date 2026-05-04 --radar KTLX
radarpulse archive download --date 2026-05-04 --radar KTLX --output data/nexrad
radarpulse archive download --date 2026-05-04 --all-radars --output data/nexrad
```

Bulk download should print the manifest summary before starting and should require
an explicit all-radars option.

## Local Cache Layout

Recommended local layout:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

The cache layout should be deterministic so future replay and benchmark runs can
refer to local files without depending on live AWS availability.

Large downloaded data should remain outside source control.

## Code Boundaries

This milestone should introduce only loader-oriented contracts and implementation.

In scope:

```text
Historical archive request
Historical archive manifest
Historical archive file metadata
S3 archive listing
S3 archive download
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
download skip/redownload decision logic
```

Integration tests:

```text
opt-in only
disabled by default
require explicit environment variable
use a small radar/date/file limit
verify public S3 listing and download behavior
```

Integration tests should not be required for normal local or CI test runs because
they depend on network access and public AWS availability.

## Documentation Deliverables

The first implementation milestone should include:

```text
README usage section or dedicated loader document
AWS bucket and archive layout notes
CLI examples
cache layout explanation
bulk download warning
test strategy
known limitations
```

## First Implementation Milestone

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

Completed before download implementation:

```text
.NET project structure
archive loader contracts
manifest generation for one radar or explicit all-radars mode
manifest summary output
manifest JSON persistence
local cache path mapping
safe --max-files and --max-bytes listing limits
transient S3 listing retry/backoff
standard xUnit unit tests
opt-in live S3 integration test
loader usage documentation
data/ and TestResults/ excluded from source control
```

Download execution can follow from the stable manifest milestone.

## Next Implementation Milestone

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

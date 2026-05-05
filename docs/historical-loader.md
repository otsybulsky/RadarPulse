# Historical NEXRAD Loader

RadarPulse implements a manifest-first historical NOAA NEXRAD Level II loader,
including archive discovery and deterministic cache download.

## Current Status

Implemented:

```text
archive manifest generation from public S3
single-radar and explicit all-radars listing
manifest summary output
manifest JSON persistence
manifest JSON loading for download
partial download/filtering from saved manifest
archive download
download concurrency control
existing-file skip by size
cache metadata sidecar validation
cache metadata backfill for legacy matching files
missing/mismatched-file redownload
temporary-file write then final move
preflight free-space guardrail
preflight required/available byte output
file and byte listing limits
transient S3 listing retry/backoff
deterministic cache path mapping
standard xUnit test coverage
opt-in live S3 listing and object download integration tests
```

## Source

The loader reads public AWS Open Data from:

```text
Bucket: unidata-nexrad-level2
Layout: yyyy/MM/dd/RADAR/file
```

The deprecated `noaa-nexrad-level2` bucket is intentionally not used.

## Usage

List one radar for a date:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive list --date 2026-05-04 --radar KTLX
```

List all radars for a date:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive list --date 2026-05-04 --all-radars
```

Use `--max-files` or `--max-bytes` for small dry runs:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive list --date 2026-05-04 --all-radars --max-files 25
```

`--max-files` stops manifest generation after the selected number of entries.
`--max-bytes` stops before adding the next file that would exceed the byte
budget. It does not split or partially include files.

Persist the discovered manifest to JSON:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive list --date 2026-05-04 --radar KTLX --manifest data/manifests/2026-05-04-KTLX.json
```

Download one radar directly from AWS listing:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --date 2026-05-04 --radar KTLX --output data/nexrad
```

Download all radars for a date:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --date 2026-05-04 --all-radars --output data/nexrad
```

Download from a saved manifest:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --manifest data/manifests/2026-05-04-KTLX.json --output data/nexrad
```

Download a filtered subset from a saved manifest:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --manifest data/manifests/2026-05-04.json --radar KTLX --max-files 10 --output data/nexrad
```

When downloading from a saved manifest, `--radar`, `--max-files`, and
`--max-bytes` are applied locally to the manifest entries. `--date` and
`--all-radars` are not valid with `--manifest` because the manifest already
defines the archive date and selected files.

Increase parallel downloads when needed:

```text
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --date 2026-05-04 --radar KTLX --output data/nexrad --concurrency 8
```

Large local data and generated manifests under `data/` are ignored by source
control.

All-radars mode is explicit because a full network day can represent tens to
hundreds of GB.

Summary output formats large numbers with `_` triad separators:

```text
Files: 22_150
Bytes: 114_330_886_326
Required download bytes: 5_406_854
Available disk bytes: 812_345_678_901
```

## Manifest Fields

Each discovered archive file is represented with:

```text
radar id
archive date
archive path
file name
object size
last modified timestamp
parsed radar volume timestamp, when available
```

Manifest JSON uses camel-case property names. Example:

```json
{
  "archiveDate": "2026-05-04",
  "files": [
    {
      "radarId": "KTLX",
      "archiveDate": "2026-05-04",
      "archivePath": "2026/05/04/KTLX/KTLX20260504_000245_V06",
      "fileName": "KTLX20260504_000245_V06",
      "sizeBytes": 5406854,
      "lastModified": "2026-05-04T00:09:43+00:00",
      "volumeTimestamp": "2026-05-04T00:02:45+00:00"
    }
  ]
}
```

## Resilience

S3 listing and object download retry transient failures before failing the
command. Retried conditions include:

```text
408 Request Timeout
429 Too Many Requests
5xx server errors
HttpRequestException
HTTP timeout when the user has not cancelled the operation
```

User cancellation is not swallowed by retry handling.

## Cache Layout

The deterministic local cache mapping is:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Download behavior uses that deterministic path mapping and applies the following
rules:

```text
existing file with matching size -> skip
existing file with matching metadata -> skip
legacy existing file without metadata -> skip and backfill metadata
existing file with mismatched metadata -> redownload
missing file -> download
existing file with mismatched size -> redownload
write to *.part then move into final path
write cache metadata to *.metadata.json
verify free disk space before starting downloads
print required download bytes and available disk bytes
```

New downloads write a sidecar metadata file next to the cached object:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}.metadata.json
```

The metadata records archive path, expected size, source last-modified timestamp,
and local cache timestamp. Existing legacy files without metadata are accepted
when their size matches the manifest entry, then metadata is backfilled without
redownloading the object.

## Tests

The standard xUnit tests can be run with:

```text
dotnet test
```

They cover prefix generation, key parsing, radar id normalization, volume
timestamp parsing, cache path mapping, manifest summaries, manifest JSON
writing/reading, retry behavior, byte limits, download skip/redownload
decisions, cache metadata write/validation, saved-manifest filtering, free-space
preflight reporting, and the explicit all-radars guardrail.

Live S3 integration tests are opt-in:

```text
$env:RADARPULSE_RUN_INTEGRATION_TESTS = "true"
dotnet test
```

Last verified normal run:

```text
25 passed, 2 skipped
```

Last verified opt-in live run:

```text
27 passed, 0 skipped
```

Normal `dotnet test` runs skip live S3 tests. The live object download test is
capped before download by the selected object's manifest size. Test runner
output under `TestResults/` is ignored by source control.

## Remaining Gaps

No additional loader work is required for the current historical archive
download milestone.

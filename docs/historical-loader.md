# Historical NEXRAD Loader

RadarPulse currently implements the manifest-first part of the historical NOAA
NEXRAD Level II loader. Download execution is intentionally left for the next
milestone.

## Current Status

Implemented:

```text
archive manifest generation from public S3
single-radar and explicit all-radars listing
manifest summary output
manifest JSON persistence
file and byte listing limits
transient S3 listing retry/backoff
deterministic cache path mapping
standard xUnit test coverage
opt-in live S3 integration test
```

Not implemented yet:

```text
archive download
download concurrency
download resume/skip/redownload decisions
download disk budget checks
loading from a saved manifest
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

The command still prints the summary after writing the manifest. Large local data
and generated manifests under `data/` are ignored by source control.

All-radars mode is explicit because a full network day can represent tens to
hundreds of GB once download support is enabled.

Summary output formats large numbers with `_` triad separators:

```text
Files: 22_150
Bytes: 114_330_886_326
```

## Manifest Fields

Each discovered archive file is represented with:

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

Manifest JSON uses camel-case property names. Example:

```json
{
  "archiveDate": "2026-05-04",
  "bucket": "unidata-nexrad-level2",
  "files": [
    {
      "radarId": "KTLX",
      "archiveDate": "2026-05-04",
      "bucket": "unidata-nexrad-level2",
      "s3Key": "2026/05/04/KTLX/KTLX20260504_000245_V06",
      "fileName": "KTLX20260504_000245_V06",
      "sizeBytes": 5406854,
      "lastModified": "2026-05-04T00:09:43+00:00",
      "volumeTimestamp": "2026-05-04T00:02:45+00:00"
    }
  ]
}
```

## Resilience

S3 listing retries transient failures before failing the command. Retried
conditions include:

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

Download execution is the next milestone. The cache layout already exists so
downloaded files can be addressed deterministically by later replay and
benchmark workflows.

## Tests

The standard xUnit tests can be run with:

```text
dotnet test
```

They cover prefix generation, key parsing, radar id normalization, volume
timestamp parsing, cache path mapping, manifest summaries, manifest JSON writing,
retry behavior, byte limits, and the explicit all-radars guardrail.

Live S3 integration tests are opt-in:

```text
$env:RADARPULSE_RUN_INTEGRATION_TESTS = "true"
dotnet test
```

Normal `dotnet test` runs skip live S3 tests. Test runner output under
`TestResults/` is ignored by source control.

## Next Milestone

The next implementation milestone is `archive download`:

```text
download selected manifest entries
support --output
skip existing valid files
redownload missing or size-mismatched files
write through temporary files before final move
respect cancellation
respect download concurrency
enforce download byte/disk budget guardrails
optionally download from saved manifest JSON
```

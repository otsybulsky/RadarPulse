# Handoff: Milestone 002 Level II Inspection

## Current Goal

Plan and start milestone 002: consume cached NOAA NEXRAD Level II files from the
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

- `002` Level II inspection/decoding milestone.
- Start with binary file classification and Archive II container parsing.
- Treat internal BZip2 records as part of the Archive II format, not as one
  whole-file BZip2 stream.
- Classify `_MDM` files separately before attempting base-data parsing.

## Documentation

- `docs/milestones/001-historical-loader-plan.md`
- `docs/milestones/001-historical-loader.md`
- `docs/milestones/002-level2-inspection-plan.md`
- `docs/milestones/002-level2-inspection.md`
- `docs/handoff.md`

## Verification

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

## Constraints

- Live AWS tests remain opt-in because they require network access and public AWS
  availability.
- Do not use the deprecated `noaa-nexrad-level2` bucket for loader work.
- Large downloaded data and generated manifests under `data/` stay outside
  source control.
- Do not commit large real Level II binary fixtures unless a deliberate fixture
  strategy is agreed first.
- Milestone 002 should avoid promising visualization, event processing,
  partitioning, benchmarks, or live ingestion.

## Important Files

- `src/Presentation/Program.cs`
- `src/Application/Archive/IHistoricalArchiveClient.cs`
- `src/Application/Archive/HistoricalArchiveManifestSelector.cs`
- `src/Infrastructure/Archive/AwsNexradArchiveClient.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloader.cs`
- `src/Infrastructure/Archive/NexradCachePathMapper.cs`
- `tests/RadarPulse.Tests/Archive/*`

## Done Criteria For Next Slice

The first milestone 002 implementation slice should be considered done when:

- RadarPulse can classify a cached file as Archive II base data, MDM-shaped, or
  unsupported.
- One cached `AR2V` file can be read through volume header and internal record
  boundaries.
- Internal BZip2 records can be decompressed into radar message bytes.
- The CLI can print a minimal inspection summary for one cached base-data file.
- Tests cover classifier/header/record behavior with small fixtures.

# Handoff: Historical NEXRAD Loader

## Goal

Bring the historical NOAA NEXRAD Level II loader to a working `archive download`
milestone on top of the existing manifest-first workflow.

## Current Status

Done:

- `archive list` for one radar and explicit `--all-radars`.
- Manifest summary output.
- Manifest JSON write/read.
- `archive download` with required `--output`.
- Download from live AWS listing or saved manifest JSON.
- Partial saved-manifest download with `--radar`, `--max-files`, and
  `--max-bytes`.
- `--concurrency` for parallel download.
- Existing-file skip by size.
- Cache metadata sidecar validation for archive path, expected size, and source
  last-modified timestamp.
- Cache metadata backfill for legacy size-valid files during skip.
- Missing or size-mismatched file redownload.
- Preflight free-space check for bytes that actually need downloading.
- CLI output for required download bytes and available disk bytes.
- Temporary write through `*.part`, then final move.
- Ctrl+C cancellation.
- Retry/backoff for S3 listing and object download.
- Deterministic cache path mapping.
- Unit tests and opt-in listing/download integration test coverage.
- Loader documentation updated.

## Verification

Last verified normal command:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
25 passed, 2 skipped
```

The skipped tests are opt-in live AWS integration tests.

Last verified full opt-in command:

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

Saved manifest subset download shape:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive download --manifest data/manifests/2026-05-04.json --radar KTLX --max-files 10 --output data/nexrad
```

## Important Files

- `docs/historical-loader.md`
- `docs/historical-loader-plan.md`
- `docs/handoff.md`
- `src/Presentation/Program.cs`
- `src/Application/Archive/IHistoricalArchiveClient.cs`
- `src/Application/Archive/HistoricalArchiveManifestSelector.cs`
- `src/Infrastructure/Archive/AwsNexradArchiveClient.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloader.cs`
- `src/Infrastructure/Archive/HistoricalArchiveCacheMetadata.cs`
- `src/Infrastructure/Archive/HistoricalArchiveCacheMetadataStore.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloadResult.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloadPreflight.cs`
- `src/Infrastructure/Archive/DriveInfoDiskSpaceProbe.cs`
- `src/Infrastructure/Archive/IDiskSpaceProbe.cs`
- `src/Infrastructure/Archive/HistoricalArchiveManifestReader.cs`
- `src/Infrastructure/Archive/HistoricalArchiveManifestWriter.cs`
- `src/Infrastructure/Archive/NexradCachePathMapper.cs`
- `tests/RadarPulse.Tests/Archive/*`

## Constraints

- Live AWS tests remain opt-in because they require network access and public AWS
  availability.
- Do not use the deprecated `noaa-nexrad-level2` bucket.
- Large downloaded data and generated manifests under `data/` stay outside
  source control.
- Free-space guardrail is a preflight check. It does not atomically reserve disk
  space during parallel download.

## Cache Layout

Downloaded files are stored deterministically:

```text
data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}
```

Example for the manual smoke test:

```text
data/nexrad/level2/2026/05/04/KTLX/{fileName}
```

## Next Work

Possible follow-up:

- Start the next milestone: consume cached Level II files for replay-oriented
  parsing/inspection.

## Done Criteria

Met:

- `archive download` downloads selected manifest entries into deterministic
  cache.
- Valid existing files are skipped safely.
- Existing metadata is validated when present, and new downloads write metadata
  sidecars.
- Legacy size-valid cached files get metadata sidecars without redownload.
- Missing or size-mismatched files are redownloaded.
- Saved manifests can be filtered locally before download.
- Free disk space is checked before download starts.
- CLI reports required download bytes and available disk bytes.
- Writes go through temporary files before final move.
- Cancellation and concurrency are respected.
- Behavior is covered by unit tests, opt-in live AWS integration tests, and
  documentation.

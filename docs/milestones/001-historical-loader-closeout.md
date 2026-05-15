# Milestone 001: Closeout

## Status

Milestone 001 is complete.

The milestone produced a manifest-first historical NOAA NEXRAD Level II loader
with safe discovery, deterministic local cache layout, cache-aware download, and
focused unit and opt-in live integration coverage.

## Final Outcome

Implemented:

- Manifest generation from public AWS Open Data.
- Single-radar listing and explicit all-radars listing.
- Manifest summaries and JSON read/write.
- Download from live listing or saved manifest.
- Manifest-local filtering by radar, max files, and max bytes.
- Deterministic cache path mapping.
- Cache metadata sidecars and legacy metadata backfill.
- Existing-file skip, missing/mismatched-file redownload, temporary-file writes,
  and final move into cache.
- Download concurrency, transient retry/backoff, cancellation, and free-space
  preflight.
- Standard unit tests and opt-in live S3 integration tests.
- Decision trace for the main loader design choices.

## Completion Checklist

```text
[x] archive manifest generation is implemented
[x] single-radar listing is implemented
[x] explicit all-radars listing is implemented
[x] manifest summary output is implemented
[x] manifest JSON persistence is implemented
[x] download from live listing is implemented
[x] download from saved manifest is implemented
[x] manifest filtering by radar/max-files/max-bytes is implemented
[x] deterministic cache layout is implemented
[x] cache skip/redownload rules are implemented
[x] cache metadata sidecars and legacy backfill are implemented
[x] temporary-file writes and final move are implemented
[x] download concurrency and cancellation are implemented
[x] transient retry/backoff is implemented
[x] free-space preflight is implemented
[x] focused unit tests are implemented
[x] opt-in live integration tests are implemented
[x] documentation and decision trace are recorded
```

## Final Verification

Last recorded verification:

```text
normal test run: 25 passed, 2 skipped
opt-in live test run: 27 passed, 0 skipped
```

Normal test runs skip live S3 tests. Live S3 listing and object download tests
are opt-in via environment variable and are not required for local or CI test
reliability.

## Deferred Work

The following were intentionally left to later milestones:

- Archive Two binary decoding.
- Radar message parsing.
- Replay/event publishing.
- Processing engine work.
- Partitioning or sharding.
- Throughput benchmarks.
- Live SNS/SQS ingestion.

## Next Milestone Input

Milestone 002 starts from this stable loader surface:

```text
public NEXRAD archive listing
  -> manifest
  -> deterministic local cache
  -> cached Archive Two files for inspection and replay
```

The loader's deterministic cache layout is the foundation for later inspection,
benchmark, replay, and processing-core work.

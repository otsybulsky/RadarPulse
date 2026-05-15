# Milestone 001: Decision Trace

## 1. What Was Implemented

Milestone 001 implemented the historical NOAA NEXRAD Level II loader:

- Manifest generation from the public AWS Open Data archive.
- Single-radar listing and explicit all-radars listing.
- Manifest summary output and JSON persistence.
- Download from live listing or saved manifest.
- Local manifest filtering by radar, max files, and max bytes.
- Deterministic local cache layout under `data/nexrad/level2/...`.
- Cache-aware download skip/redownload decisions.
- Metadata sidecars, legacy metadata backfill, temporary-file writes, and final
  move into cache.
- Download concurrency, retry/backoff, cancellation, and free-space preflight.
- Unit tests and opt-in live AWS integration tests.

Verified result:

```text
normal tests: 25 passed, 2 skipped
opt-in live tests: 27 passed, 0 skipped
```

## 2. Decision Matrix

### Manifest-First Loader

Decision: separate discovery from download. The archive is first represented as
a manifest, then selected manifest entries are downloaded.

Why chosen: a full radar-network day can be tens to hundreds of GB. A manifest
lets the user inspect size, file count, radar coverage, and limits before
downloading.

Alternatives: download while listing, or make download the primary command.

Rejected because: direct download makes accidental large transfers easier and
does not give later milestones a reusable description of the selected corpus.

Trade-offs/debt: manifests can become stale relative to the remote archive, so
download must still validate size and metadata.

Review explanation: "I made discovery a first-class step so data volume is
visible and later replay work can start from a deterministic manifest."

### Use `unidata-nexrad-level2`

Decision: use the public `unidata-nexrad-level2` bucket and avoid the deprecated
`noaa-nexrad-level2` bucket.

Why chosen: the loader should target the current public archive source instead
of building new work on a deprecated endpoint.

Alternatives: support both buckets or prefer the older bucket for compatibility.

Rejected because: dual-source behavior increases test and cache ambiguity
before there is a real need for it.

Trade-offs/debt: if the public archive layout changes, source-specific listing
code must be updated.

Review explanation: "I chose one current upstream source to keep archive keys,
cache paths, and tests deterministic."

### Explicit All-Radars Mode And Limits

Decision: require explicit all-radars intent and support `--max-files` /
`--max-bytes` limits.

Why chosen: all-radars downloads are large enough that accidental execution
would be expensive in time, bandwidth, and disk space.

Alternatives: make all-radars the default, prompt interactively, or rely on
documentation warnings.

Rejected because: defaults should be safe and commands should remain
non-interactive/scriptable.

Trade-offs/debt: users must provide more explicit arguments for large runs.

Review explanation: "The CLI makes dangerous data-volume choices explicit, but
still supports automation."

### Deterministic Cache Layout

Decision: store downloaded files at
`data/nexrad/level2/{yyyy}/{MM}/{dd}/{radarId}/{fileName}`.

Why chosen: later inspection and replay milestones need stable local paths that
do not depend on live AWS availability.

Alternatives: flat cache, hash-based cache, or user-provided arbitrary paths.

Rejected because: flat paths lose archive structure, hash paths are hard to
inspect manually, and arbitrary paths complicate cache selection.

Trade-offs/debt: this layout encodes the current archive key structure.

Review explanation: "The cache mirrors the archive enough to be understandable,
but keeps local replay deterministic."

### Metadata Sidecars And Safe Writes

Decision: write sidecar metadata, accept matching legacy files, redownload
mismatches, and use temporary files before final move.

Why chosen: downloads must be resumable, cache-aware, and resistant to partial
or mismatched files.

Alternatives: trust file existence, always redownload, or store cache state in a
single database.

Rejected because: existence alone is unsafe, always redownloading wastes time,
and a database is unnecessary for the loader milestone.

Trade-offs/debt: sidecar metadata creates more files and future schema
evolution may need versioning.

Review explanation: "The cache is simple files plus metadata, so it is robust
without introducing a storage subsystem."

### Opt-In Live Integration Tests

Decision: keep AWS integration tests disabled by default and require an explicit
environment variable.

Why chosen: normal tests should be reliable offline and in CI, while still
allowing manual validation against the real public archive.

Alternatives: always run live tests or avoid integration tests entirely.

Rejected because: always-live tests are flaky and slow; no live tests would
leave the real archive path unverified.

Trade-offs/debt: live behavior is not checked on every test run.

Review explanation: "The core logic is unit-tested locally, and live archive
behavior is validated only when explicitly requested."

## 3. Remaining Risks And Debt

- The loader depends on public AWS archive availability and layout.
- Manifest JSON is a snapshot; remote objects can theoretically change.
- Metadata sidecars may need schema versioning if cache validation grows.
- Milestone 001 intentionally does not decode radar files.

## 4. Portfolio Review Summary

Milestone 001 established a safe, reproducible historical data foundation. The
main decisions were manifest-first discovery, explicit large-download intent,
deterministic cache paths, metadata-based cache validation, safe writes, and
opt-in live tests. This gave later milestones a stable local NEXRAD corpus
without coupling them to live network access.

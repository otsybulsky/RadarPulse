# Milestone 003: Closeout

## Status

Milestone 003 is complete.

The milestone produced the publisher-facing historical replay foundation on top
of the Archive Two decoder. RadarPulse can publish ordered
`ArchiveTwoGateMomentEvent` values from single files and cache selections,
validate sequential/parallel equivalence, and benchmark steady-state replay
publishing.

## Final Outcome

Implemented:

- `IArchiveReplayEventPublisher` replay boundary.
- Replay publish options and result models.
- Cache publish and cache benchmark result models.
- `ArchiveReplayCountingPublisher` for deterministic counts and checksums.
- Sequential single-file replay publishing.
- Ordered parallel single-file replay publishing.
- Cache-selection replay with skipped non-base-data files.
- Reusable `NexradArchiveReplayPublishSession`.
- `archive replay --file` and `archive replay --cache` smoke commands.
- `archive benchmark replay-publish --file` and `--cache` benchmark commands.
- Focused tests for ordering, totals, diagnostics, cancellation, reusable
  sessions, cache selection, and benchmark consistency.
- Decision trace for the publisher-path architecture.

## Completion Checklist

```text
[x] explicit replay publisher API is implemented
[x] one cached Archive Two file can publish ordered events
[x] selected cached Archive Two files can publish ordered events
[x] sequential replay path is implemented
[x] ordered parallel replay path is implemented
[x] parallel workers cannot publish in completion order
[x] sequential and parallel replay produce identical chronology checksums
[x] cache replay applies date/radar/max-files selection
[x] non-Archive Two files are skipped with diagnostics
[x] CLI smoke commands are implemented
[x] replay-publish benchmarks are implemented
[x] reusable steady-state publish session is implemented
[x] focused tests cover ordering, totals, diagnostics, cancellation, and cache
    aggregation
[x] documentation and decision trace are recorded
```

## Final Verification

Representative recorded verification:

```text
single file, parallelism 24:
  published events/s: 362_695_693.02
  allocated bytes/event: 0.07
  chronology checksum: 5_257_350_734_454_804_390

cache-wide KTLX corpus, parallelism 24:
  examined files per iteration: 244
  skipped files per iteration: 24
  published files per iteration: 220
  published events per iteration: 8_513_587_200
  valid events per iteration: 1_369_194_138
  published events/s: 310_665_492.15
  valid events/s: 49_962_649.20
  allocated bytes/event: 0.06
  chronology checksum: 9_060_754_844_693_896_318
```

Sequential and ordered parallel replay over the same file produced matching
event counts, status counts, raw checksum, calibrated checksum, and chronology
checksum.

## Deferred Work

The following were intentionally left to later milestones:

- Normalized processing-core input contract.
- Lower-level hot-path batch transport.
- Partitioning and sharding strategy.
- Downstream event engine integration.
- Durable broker publishing.
- Live ingestion.
- Production replay orchestration.
- Full migration of older replay-shape benchmark/validator loops, if still
  worthwhile.

## Next Milestone Input

Milestone 004 starts from this stable publisher-facing replay surface:

```text
Archive Two replay
  -> ordered ArchiveTwoGateMomentEvent values
  -> explicit publisher contract
  -> sequential/parallel chronology validation
  -> cache-wide replay benchmark baseline
```

The next milestone can replace the semantic publisher-facing event shape with a
compact normalized processing-core input stream while preserving replay
determinism and benchmark discipline.

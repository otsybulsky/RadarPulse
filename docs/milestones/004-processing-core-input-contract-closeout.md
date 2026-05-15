# Milestone 004: Closeout

## Status

Milestone 004 is complete.

The milestone produced the canonical normalized input contract for the future
RadarPulse processing core. The implemented stream is deterministic,
source-addressable, versioned, externally interpretable, and benchmarked above
the 300M+ payload-value/s target.

## Final Outcome

Implemented:

- `RadarEventBatch` and `RadarStreamEvent` processing-core input contracts.
- 64-byte unmanaged `RadarStreamEvent` layout for cache-conscious hot-path use.
- Append-only dense identity dictionaries with canonicalization, snapshots, and
  deltas.
- Identity normalization from text radar/moment metadata to dense numeric IDs.
- Versioned source-universe mapping from source dimensions to dense `SourceId`.
- Chronological multi-source batch construction from Archive Two replay.
- Lifetime-scoped payload storage with explicit owned/leased batch semantics.
- Sequential replay, ordered parallel replay, cache replay, validation,
  checksums, CLI smoke commands, and stream benchmarks.
- Decision trace for the main architecture and performance choices.

## Completion Checklist

```text
[x] RadarEventBatch and RadarStreamEvent contracts are implemented
[x] append-only dense identity catalogs are implemented
[x] dictionary snapshots and deltas are externally visible
[x] source-universe versioning is implemented
[x] identity normalization boundary emits numeric IDs
[x] batch builder emits chronological multi-source batches
[x] payload storage is lifetime-scoped and range-checked
[x] single-file stream replay works sequentially
[x] single-file stream replay works with ordered parallel replay
[x] cache-selected stream replay works
[x] sequential/parallel validation passes
[x] focused unit tests cover identity, versioning, payload, and ordering
[x] CLI smoke and benchmark commands are available
[x] milestone 004 achieved results are documented
[x] handoff is updated for the next milestone
```

## Final Verification

Last verified commands:

```powershell
dotnet test RadarPulse.sln --no-restore
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 5 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --cache data\nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Last verified results:

```text
tests: 143 passed, 3 skipped
release build: 0 warnings, 0 errors

single file, parallelism 24:
  stream events/s: 462_374.42
  payload values/s: 553_123_110.90
  allocated bytes: 180_080
  allocated bytes / payload value: 0.00

cache-wide KTLX corpus, parallelism 24:
  examined files: 244
  skipped files: 24
  published files: 220
  stream events/s: 425_955.35
  payload values/s: 509_716_417.97
  allocated bytes / payload value: 0.20
```

The skipped tests are opt-in live AWS integration tests and the opt-in local
corpus validation test.

## Baseline Comparison

The comparable metric is:

```text
milestone 003 Published events/s == milestone 004 Payload values/s
```

Milestone 004 exceeds the milestone 003 count-only replay-publish baseline:

```text
single file:
  milestone 003: 362_695_693.02 published events/s
  milestone 004: 553_123_110.90 payload values/s
  result: +52.5%

cache-wide:
  milestone 003: 310_665_492.15 published events/s
  milestone 004: 509_716_417.97 payload values/s
  result: +64.1%
```

## Deferred Work

The following are not blockers for milestone 004 and should be handled in later
milestones only if they support the next architecture goal:

- processing algorithms
- partitioning
- live ingestion
- durable broker integration
- shared-memory transport
- visualization
- long-term storage format optimization
- further cache-wide allocation reduction outside normalized batch buffers

Likely remaining cache-wide allocation sources:

```text
compressed-record descriptor storage
ordered task scheduling
file enumeration/order materialization
scanner/decompression buffer churn
```

## Next Milestone Input

The next milestone can start from this stable input surface:

```text
Archive replay
  -> deterministic normalized RadarEventBatch stream
  -> dense SourceId on every event
  -> versioned dictionary and source-universe visibility
  -> owned or leased payload lifetime
  -> 500M+ payload values/s replay construction baseline
```

The next milestone should preserve the milestone 004 stream contract and treat
partitioning, processing algorithms, and downstream transport as consumers of
this canonical input shape.

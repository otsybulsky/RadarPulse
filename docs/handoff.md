# Handoff: Milestone 004 Scoped

## Current Goal

Milestone 003 is complete. RadarPulse has a publisher-facing historical replay
input path over Archive Two gate-moment events, including sequential single-file
publishing, ordered parallel publishing, cache-selection replay, a reusable
count-only replay publish session, and replay-publish benchmarks for single
files and cache selections.

Milestone 004 is now scoped as the processing-core input contract milestone.
The goal is to implement a compact, deterministic, normalized `RadarEventBatch`
stream with append-only dense identity catalogs, an identity normalization
boundary, versioned dictionary/source-universe visibility, and batch-owned raw
payload storage.

## Milestone Status

Done:

- `001` historical archive loader is complete.
- `002` NEXRAD archive inspection/decoder foundation is complete.
- `003` historical replay publisher foundation is complete.
- `004` processing-core input contract architecture is scoped.
- `004` processing-core input contract implementation plan is drafted.
- `004` slice 1 contract types and version constants are implemented.
- `004` slice 2 append-only dense identity catalog is implemented.
- `004` slice 3 dictionary version snapshots and deltas are implemented.
- `004` slice 4 canonicalization and error policy is implemented.
- `004` slice 5 source universe definition is implemented.
- `004` slice 6 identity normalizer is implemented.
- `004` slice 7 batch builder and payload storage is implemented.
- `004` slice 8 single-file sequential replay integration is implemented.
- `004` slice 9 batch validation and checksum metrics are implemented.
- `004` ordered-parallel batch replay parity is implemented.
- `004` normalized batch stream CLI smoke command is implemented.
- `004` normalized batch stream benchmark command is implemented.
- `004` first parallel stream buffer-churn reduction pass is implemented.
- `004` stream identity-cache and batch pre-sizing optimization pass is
  implemented.
- `004` no-copy batch finalization and cached payload counters are implemented
  for builder-owned normalized stream batches.
- `004` normalized batch stream cache benchmark command is implemented and
  verified against the full local cache.
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

Next work:

- Start milestone 004 implementation from
  `docs/milestones/004-processing-core-input-contract-plan.md`.
- Preserve milestone 003 replay/publisher behavior while adding the new
  normalized batch stream.
- Continue milestone 004 with the next normalized stream allocation reduction
  pass.
- Treat the current `archive benchmark stream` numbers as full replay
  construction throughput, not as the future processing-core throughput over
  already-built `RadarEventBatch` values.
- Preserve the slice 1 cache-conscious stream event constraint:
  `RadarStreamEvent` is a 64-byte unmanaged value type with no reference
  fields.
- Implement source-universe versioning, identity normalization, batch-owned
  payload storage, replay integration, validation, and focused CLI/benchmark
  smoke commands in the planned order.
- Preserve the ordered parallel projection rule in any future replay work:
  workers may decompress/project records concurrently, but emission must be
  merged by original source order, not worker completion order.
- Keep the order-sensitive chronology checksum as the validation gate for
  sequential/parallel equivalence.
- Avoid implementing processing algorithms, live ingestion, durable broker
  integration, visualization, or a general storage subsystem as part of
  milestone 004.

Completed in milestone 004 planning:

- `docs/milestones/004-processing-core-input-contract.md`.
- `docs/milestones/004-processing-core-input-contract-plan.md`.
- Milestone 004 scope narrowed to the normalized processing-core input contract,
  not downstream processing algorithms or distribution.
- Dense identity catalogs are specified as persistent append-only catalogs with
  external versioned visibility.
- `RadarEventBatch` / `RadarStreamEvent` contract shape is specified with
  `StreamSchemaVersion`, `DictionaryVersion`, and `SourceUniverseVersion`.
- Identity normalization is specified as a mandatory boundary between decoded
  radar structures and batch construction, with no per-gate text lookup.
- Payload rules are specified: raw radar values are canonical, payload storage
  belongs to the batch, and event payload references are explicit.

Completed in milestone 004 implementation so far:

- `RadarEventBatch`.
- `RadarStreamEvent`.
- `StreamSchemaVersion`.
- `DictionaryVersion`.
- `SourceUniverseVersion`.
- `RadarStreamWordSize`.
- `RadarStreamStatusModel`.
- `RadarStreamEvent` is explicitly sized at 64 bytes and contains no reference
  fields.
- `RadarEventBatch` carries stream schema, dictionary, and source-universe
  versions, event memory, and batch-owned payload memory.
- `RadarEventBatch` validates event payload references against batch payload
  storage and rejects mismatched gate-count/word-size payload lengths.
- Focused streaming contract tests cover event layout, version metadata,
  payload range validation, and version value validation.
- `DenseIdentityCatalog` implements append-only dense text-to-id mappings for
  small stream identity dimensions.
- `DenseIdentityCatalog` exposes `string`, `ReadOnlySpan<char>`, and
  `ReadOnlySpan<byte>` lookup views over the same canonical entries.
- Existing catalog ids remain stable; new valid unknown identities append under
  a serialized registration gate.
- Reverse lookup is backed by a dense id-indexed array, so assigned ids satisfy
  `0 <= id < Count`.
- Invalid identity text is not registered. The initial canonical policy accepts
  only non-empty `A-Z`, `0-9`, and underscore text within the configured maximum
  length.
- Focused dense-catalog tests cover lookup-view equivalence, dense append-only
  ids, reverse lookup, invalid identity rejection, concurrent duplicate
  registration, concurrent distinct registration, and partial-entry visibility.
- `DenseIdentityCatalog` now exposes `CurrentVersion`, immutable
  `DenseIdentityCatalogSnapshot` views, and append-only
  `DenseIdentityCatalogDelta` views.
- The empty catalog starts at `DictionaryVersion.Initial`; each new identity
  append advances the catalog version, while duplicate registration keeps the
  current version unchanged.
- A snapshot for version `N` exposes only entries visible at `N`. Later appends
  do not mutate existing snapshots.
- A delta from version `N` to a later version contains only the dense appended
  entries needed to reconstruct the later snapshot.
- Focused versioning tests cover version-scoped snapshot visibility, delta
  reconstruction, published forward/reverse lookup, duplicate registration
  version stability, immutable old snapshots, empty deltas, and rejection of
  versions that are not yet visible.
- `DenseIdentityCanonicalizationPolicy` makes identity validation explicit
  instead of hardcoding one rule inside the catalog.
- Built-in policies now cover radar codes and moment names separately. Radar
  codes require exactly four uppercase ASCII letters or digits; moment names
  allow compact uppercase ASCII letters, digits, and underscores up to eight
  characters.
- Canonicalization intentionally does not trim input and does not fold case.
  Lowercase, padding, trailing spaces, unsupported characters, and non-ASCII
  byte values are invalid rather than silently normalized.
- `DenseIdentityValidationResult` exposes validation error, input kind, length,
  invalid position, and invalid value for diagnostics without registering
  invalid identities.
- Focused canonicalization tests cover radar and moment policy differences,
  no-trim/no-case-fold behavior, validation diagnostics, UTF-8 byte validation,
  and exception messages containing catalog, dimension, and reason.
- `RadarSourceKey` defines the dense source tuple:
  `RadarOrdinal x ElevationSlot x AzimuthBucket x RangeBand`.
- `RadarSourceUniverse` defines source-universe metadata and arithmetic:
  version, dimension counts, per-dimension source strides, source count,
  `RadarSourceKey -> SourceId`, and `SourceId -> RadarSourceKey`.
- `SourceId` values are dense in `0 <= SourceId < SourceCount`, and every radar
  ordinal owns a contiguous source-id block.
- Adding a new radar ordinal with the same per-radar dimensions keeps existing
  radar-zero source IDs stable and starts the new radar at the next contiguous
  block.
- Source-universe layout compatibility is explicit: the same
  `SourceUniverseVersion` can be reused only for the same source layout.
- Focused source-universe tests cover count/stride calculation, dense id space,
  tuple/id round-trip, radar block boundaries, stable existing blocks when a
  radar is added, invalid dimension rejection, and version/layout compatibility.
- `RadarStreamIdentityNormalizer` now resolves radar code, moment name,
  elevation slot, azimuth bucket, and range band into dense `RadarOrdinal`,
  `MomentId`, and `SourceId` values.
- The normalizer owns radar and moment dense catalogs with the radar-code and
  moment-name canonicalization policies.
- Known identities use a read-mostly path. Unknown valid identities append
  through a serialized cold registration path; invalid text and invalid source
  tuples do not mutate dictionaries.
- The normalizer publishes an aggregate `DictionaryVersion` across radar and
  moment catalogs, plus `RadarStreamDictionarySnapshot` views for a requested
  aggregate version.
- Resolved identities carry `DictionaryVersion` and `SourceUniverseVersion`,
  so a future `RadarEventBatch` can use the same version metadata.
- Focused normalizer tests cover stable identity resolution, repeated lookup
  version stability, unknown valid appends, dictionary snapshots for resolution
  versions, invalid radar/moment rejection without mutation, source out-of-range
  rejection without mutation, one-radar source-universe capacity limits, UTF-8
  input equivalence, and throwing normalization failure behavior.
- `RadarEventBatchBuilder` builds normalized `RadarEventBatch` values from
  `RadarStreamIdentity`, event metadata, and raw payload bytes.
- The builder owns event and payload buffers internally and returns batch-owned
  arrays from `Build()`. Appending later events does not mutate previously built
  batches.
- `PayloadOffset` and `PayloadLength` are assigned by the builder. Payload bytes
  are copied at append time so later mutation of parser/external buffers cannot
  change the batch payload.
- The builder validates payload length against `GateCount * WordSize`, rejects
  invalid identity versions, and requires all events in one batch to share the
  same `SourceUniverseVersion`.
- The batch dictionary version is the highest dictionary version visible among
  appended identities. Empty batches use initial dictionary and source-universe
  versions.
- Focused builder tests cover payload copying and offsets, multi-event payload
  accumulation, append-order preservation across different source IDs, owned
  build snapshots, empty build behavior, invalid payload rejection without
  mutation, source-universe version mismatch rejection, and invalid identity
  version rejection.
- `IArchiveRadarEventBatchPublisher` defines the first batch-stream publisher
  boundary without replacing the milestone 003 semantic replay publisher.
- `NexradArchiveRadarEventBatchPublisher` implements sequential single-file
  Archive Two replay into normalized `RadarEventBatch` output.
- `ArchiveTwoRadarEventBatchProjector` parses Type 31 moment blocks directly
  into gate-run stream events, preserving raw 8-bit and 16-bit moment payload
  bytes, word size, scale, offset, source order, and batch-owned payload
  storage.
- The first replay integration publishes one batch per file. It uses the
  identity normalizer and batch builder, and emits numeric `RadarOrdinal`,
  `MomentId`, and `SourceId` values instead of text.
- The default batch replay source universe is a one-radar demonstration layout:
  32 elevation slots x 720 azimuth buckets x 1 range band = 23,040 logical
  sources.
- `ArchiveRadarEventBatchCountingPublisher` records batch count, stream-event
  count, payload bytes, payload value count, raw-value checksum, and visible
  stream/dictionary/source-universe versions.
- Focused replay-integration tests cover sequential single-file batch replay,
  dictionary snapshot visibility, raw payload bytes, 16-bit raw-value checksum,
  and splitting one moment block into range-band gate-run events.
- `RadarEventBatchMetrics` computes deterministic per-batch event count,
  payload byte count, payload value count, raw-value checksum, and structural
  metadata checksum over batch headers and event metadata.
- `RadarStreamDictionarySnapshotMetrics` computes stable externally visible
  dictionary snapshot counts and mapping checksum for versioned radar/moment
  catalog snapshots.
- `RadarEventBatchValidator` validates the processing-core input contract
  outside the hot path: supported stream schema version, source-universe version
  match, optional dictionary snapshot version match, non-decreasing batch
  chronology, contiguous payload references, no unreferenced payload tail,
  source-id/source-dimension agreement, dictionary-id visibility, and optional
  expected metrics/checksum match.
- `ArchiveRadarEventBatchCountingPublisher` now uses `RadarEventBatchMetrics`
  for payload value counts and raw-value checksum, keeping replay counters and
  validation checksums on one shared interpretation.
- Focused validator tests cover valid metrics, out-of-order chronology,
  source-id range errors, source-id/source-dimension mismatch, non-contiguous
  payload references, unreferenced payload tails, dictionary version mismatch,
  invisible dictionary IDs, expected metrics mismatch, and stable dictionary
  snapshot mapping checksums.
- `ArchiveRadarEventBatchPublishOptions` now carries `DegreeOfParallelism` for
  normalized batch replay while preserving the existing one-radar default.
- `NexradArchiveRadarEventBatchPublisher` supports ordered-parallel single-file
  batch replay. Workers read and decompress compressed records concurrently
  into a bounded in-flight window, but the ordered drain feeds decompressed
  records into one `ArchiveTwoRadarEventBatchProjector`.
- Dynamic dictionary registration therefore happens only during ordered
  emission, so worker completion order cannot change `RadarOrdinal`,
  `MomentId`, `DictionaryVersion`, payload bytes, or event metadata.
- Focused parity tests compare sequential and parallel batch replay for the
  same synthetic file, including event sequence, payload bytes, batch metrics,
  dictionary snapshot checksum, validator result, and moment-id registration
  order under an intentionally delayed first record.
- `archive stream --file ... [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` is implemented as the
  first manual CLI smoke command for the normalized `RadarEventBatch` stream.
- The stream command prints stream schema, dictionary version,
  source-universe version, logical source count, compressed/decompressed byte
  counts, batch count, event count, payload bytes, payload value count,
  raw-value checksum, radar/moment dictionary entry counts, and dictionary
  mapping checksum.
- `ArchiveRadarEventBatchStreamBenchmarkResult` and
  `NexradArchiveRadarEventBatchStreamBenchmark` add a repeatable benchmark for
  the normalized batch stream.
- `archive benchmark stream --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` reports per-iteration
  stream counters, total throughput, `Stream events/s`, `Payload values/s`,
  allocation totals, and allocation ratios per stream event and payload value.
- The benchmark verifies that every measured iteration produces the same stream
  versions, counts, raw-value checksum, and dictionary mapping checksum.
- Focused benchmark tests cover stable iteration totals over a synthetic Archive
  Two file.
- The first benchmark result is intentionally conservative: it measures Archive
  Two replay into the normalized batch stream, including BZip2 decompression,
  message scanning, Type 31 parsing, identity normalization, source-id
  calculation, batch event construction, batch-owned payload copying, counting,
  and checksum work.
- The gap between earlier decoder-level throughput and the normalized stream
  benchmark is currently attributed to payload copying, ordered-drain
  serialization after parallel decompression, extra decompressed-record
  buffering in the parallel path, and high allocation pressure.
- The first allocation/buffer-churn pass changed the parallel batch replay path
  so decompressed payload storage belongs to the worker and is reused across
  records. The worker is returned to the available pool only after ordered scan
  consumes its decompressed payload, preserving deterministic dictionary
  registration while avoiding a separate decompressed-record buffer owner per
  compressed record.
- This pass improved parallel stream benchmark throughput materially, but did
  not remove the main allocation sources. The next optimization targets are
  reusable stream publish sessions, batch-builder payload/event buffer reuse,
  and reducing string allocations in Type 31 moment-name extraction.
- The next stream optimization pass removed per-block Type 31 moment-name
  string allocation by reading the 3-byte moment name as a byte span, caches
  radar/moment dimensions per moment code after the first ordered dictionary
  normalization, uses source-universe stride arithmetic in the cached path, and
  pre-sizes archive stream event/payload buffers from compressed file size and
  compressed record count. This keeps deterministic dictionary registration on
  the ordered scan path while avoiding most builder resize churn.
- The follow-up stream optimization pass added a one-shot
  `RadarEventBatchBuilder.BuildAndReset()` path used by Archive Two projection.
  This transfers builder-owned event and payload buffers into the final
  `RadarEventBatch` instead of copying them, while preserving the existing
  snapshot-copy semantics of `Build()`. Builder-created batches also carry
  cached payload value count and raw-value checksum, allowing the archive batch
  counting publisher to avoid a second full payload scan.
- `archive benchmark stream` now supports cache-wide benchmark selection with
  `--cache`, optional `--date`, optional `--radar`, and `--max-files`. It
  reports examined/skipped/published file counts, aggregate stream totals,
  throughput, and allocation ratios for the normalized `RadarEventBatch`
  construction path.
- The latest projector hot-path pass keeps current dictionary version,
  source-universe version, source-universe strides, and volume timestamp ticks
  as projector-local fields. The cached identity path no longer reads the
  normalizer's volatile dictionary version for every stream event.

Completed in milestone 003 so far:

- `docs/milestones/003-historical-replay-publisher-plan.md`.
- `docs/milestones/003-historical-replay-publisher.md`.
- `docs/handoff.md` updated to point at milestone 003.
- `IArchiveReplayEventPublisher`.
- `ArchiveReplayPublishOptions`.
- `ArchiveReplayPublishResult`.
- `ArchiveReplayCachePublishResult`.
- `ArchiveReplayPublishCacheBenchmarkResult`.
- `ArchiveReplayCountingPublisher`.
- `NexradArchiveReplayPublisher` sequential single-file replay path.
- `NexradArchiveReplayPublisher` ordered parallel replay path.
- `NexradArchiveReplayPublishSession` reusable count-only replay runner.
- `archive replay --file ... [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- `archive replay --cache ... [--date yyyy-MM-dd] [--radar KTLX]
  [--max-files n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- `archive benchmark replay-publish --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- `archive benchmark replay-publish --cache ... [--date yyyy-MM-dd]
  [--radar KTLX] [--max-files n] [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- Focused unit tests for source-order publication, counters/checksums,
  sequential/parallel equivalence, custom publisher ordered drain, non-Archive
  Two diagnostics, invalid parallelism, cancellation, and replay-publish
  benchmark iteration consistency, repeated reusable-session parity, and
  reusable-session disposal behavior, plus cache replay selection/skip
  aggregation and cache replay-publish benchmark iteration consistency.

Completed in milestone 002:

- `archive inspect --file`.
- Archive Two base-data classification for files starting with `AR2V`.
- MDM/compressed-stream classification for `_MDM` and early `BZh` non-`AR2V`
  files.
- Unknown binary classification.
- 24-byte Archive Two volume header parsing.
- Archive Two compressed record boundary parsing from 4-byte signed big-endian
  control words.
- Per-record BZip2 signature detection.
- Per-record BZip2 decompression byte counting through the shared BZip2
  decompressor abstraction.
- `archive benchmark decompress --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`.
- Benchmark path pools compressed-payload and output buffers to avoid measuring
  avoidable local buffer churn.
- Parallel benchmark mode scans compressed record boundaries in file order,
  decompresses independent BZip2 records concurrently, and aggregates results by
  original record index so worker completion order does not mix records.
- `radarpulse` is the default BZip2 backend after adding a reusable-workspace
  decoder to remove per-record managed BZip2 workspace allocations.
  SharpZipLib and SharpCompress remain selectable for comparison.
- BZip2 decompression sessions now expose a streaming/chunk callback so future
  parsing can consume decompressed bytes without materializing full records.
- `archive validate decompress` compares the default `radarpulse` backend
  against SharpZipLib record-by-record with streaming hashes.
- Decompressed Archive Two bytes are now scanned through the streaming callback
  for RDA/RPG message headers.
- Minimal Message Type 31 parsing reports radial counts and gate-count totals
  for generic moment data blocks.
- `archive inspect --file` reports message counts by type, Type 31 radial
  counts, estimated gate-moment events, and moment gate/radial totals.
- `archive inspect --file` reports Type 31 `VOL`/`ELV`/`RAD` constant block
  counts and sweep summaries from radial status, elevation number, cut sector,
  elevation angle, moment membership, and source order.
- Type 31 sweep summaries carry explicit source order as compressed record,
  message-in-record, and Type 31 radial sequence positions for future ordered
  replay publishing.
- Type 31 generic moment descriptors now summarize per-moment gate count range,
  word size, first-gate range, gate spacing, scale, and offset. CLI output
  documents the calibration formula `value=(raw-offset)/scale`.
- `archive benchmark parse` supports `--decode-calibrated-moments`. This mode
  reads raw 8/16-bit moment values, preserves Message Type 31 sentinel/status
  semantics, applies per-block scale/offset only to valid samples, and reports
  calibrated value counts, min/max, and a scaled checksum.
- `archive benchmark parse --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress] [--decode-moments]
  [--decode-calibrated-moments]`
  measures decompress+message-scan+minimal-Type31 throughput in estimated
  gate-moment events/s, and optionally reads actual 8/16-bit moment gate
  values or calibrated moment values with checksums.
- A first reusable Type 31 gate-moment event shape is implemented with radar id,
  volume timestamp, sweep/elevation/radial/gate identity, range, moment name,
  raw value, decoded status, optional calibrated value, and explicit source
  order.
- `archive benchmark replay-shape --file ... [--iterations n]
  [--warmup-iterations n] [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]`
  projects ordered Type 31 gate-moment events and measures the cost of creating
  the replay-facing event shape before a downstream publisher exists.
- Replay-shape projection supports parallel compressed-record decoding. The
  parallel path first builds per-record starting projector states from Type 31
  radial transitions, then projects records concurrently and aggregates record
  results in original Archive Two record order.
- Replay-shape benchmark output includes an order-sensitive chronology checksum
  on every run, so parallel runs can be compared against sequential runs for
  event-order preservation, not just commutative totals.
- `archive validate replay-shape (--file path | --cache data/nexrad [--radar
  KTLX] [--max-files n]) [--parallelism n]
  [--decompressor radarpulse|sharpziplib|sharpcompress]` compares sequential
  ordered projection against parallel replay-shape projection and reports
  calibrated-data unevenness by compressed record, sweep, radial, and minute.
- `archive inspect (--file path | --cache data/nexrad [--date yyyy-MM-dd]
  [--radar KTLX] [--max-files n])` can inspect a single file or aggregate a
  selected cache slice without failing on MDM/unknown files.
- Archive Two volume/framing helpers are centralized in `ArchiveTwoFileReader`
  instead of duplicated across inspector, benchmarks, and validators.
- The inspection path also uses the shared decompressor abstraction and pooled
  compressed-payload/output buffers.
- CLI output for size, kind, archive filename, version, extension number, radar
  id, volume timestamp, compressed record totals, and decompressed byte totals.
- Unit tests with small synthetic fixtures.

## Current Achievement Summary

The handoff state is a completed milestone 003 publisher-facing replay
foundation on top of the completed milestone 002 NEXRAD Archive Two decoder
foundation. Milestone 003 supports sequential single-file replay publishing,
ordered parallel replay publishing, cache-selection replay, and a reusable
steady-state count-only replay session used by the internal benchmarks. The
internal replay-publish benchmark also supports cache-wide measurement.

Achieved:

- RadarPulse recognizes cached Archive Two base-data files that start with
  `AR2V`.
- The reader parses the 24-byte volume header and reports archive filename,
  version, extension number, radar id, and volume timestamp.
- The reader parses Archive Two compressed record boundaries from 4-byte signed
  big-endian control words.
- Each internal BZip2 payload is decompressed per record. The file is correctly
  treated as an Archive Two container, not as one continuous BZip2 stream.
- `_MDM` and early `BZh` non-`AR2V` files are classified separately so they are
  not accidentally parsed as base-data volumes.
- Parallel decompression is implemented for independent compressed records.
  The implementation preserves order by scanning records in file order and
  writing worker results back by original record index.
- Benchmarking now compares the reusable-workspace `radarpulse` BZip2 backend
  with SharpZipLib and SharpCompress on the same Archive Two framing path.
- The `radarpulse` backend is currently the default because it preserves the
  measured byte counts while reducing measured per-record allocation by roughly
  three orders of magnitude on the current KTLX file.
- A local differential validation gate compares `radarpulse` against
  SharpZipLib across selected cached Archive Two files before parser work.
- The inspection path and benchmark path both use the shared BZip2 decompressor
  abstraction and pooled compressed-payload/output buffers.
- The message scanner now validates the RDA/RPG header enough to avoid
  byte-shift false positives in real KTLX records.
- The current KTLX smoke file reports 6_496 messages, including 6_480 Type 31
  radials, and 38_759_040 estimated gate-moment events.
- The current KTLX smoke file reports 12 Type 31 sweeps, 6_480 `VOL`,
  6_480 `ELV`, and 6_480 `RAD` constant blocks, with sweep source ranges
  ordered by compressed record/message/radial position.
- The current KTLX smoke file reports stable descriptor metadata such as
  `REF scale=2 offset=66`, `VEL scale=2 offset=129`, `ZDR scale=32 offset=418`,
  and 0.25 km gate spacing for the observed moments.
- Calibrated decoding on the current KTLX smoke file reports 5_523_459 valid
  calibrated values per volume, 27_316_941 below-threshold values, 1_355
  range-folded values, 5_794_484 CFP filter-not-applied values, 65_871 CFP
  point-clutter-filter values, 56_930 CFP dual-pol-filtered values, no reserved
  or unsupported values, and a calibrated range of `-31.5..359.649`.
- Replay-shape projection on the current KTLX smoke file generates 38_759_040
  ordered gate-moment events per volume, with the same raw checksum
  `1_063_626_011`, calibrated checksum `70_028_121_122`, valid/status counts,
  calibrated range `-31.5..359.649`, range span `2.125..459.875` km, and
  chronology checksum `5_257_350_734_454_804_390`. Sequential and parallel runs
  produced the same chronology checksum.
- Cache-wide KTLX replay-shape validation examined 244 files, skipped 24
  non-base-data files, compared 220 Archive Two base-data files, found zero
  sequential/parallel mismatches, and reported 8_513_587_200 replay-shaped
  events with 1_369_194_138 valid calibrated events.
- The cache-wide unevenness report found the largest compressed-record valid
  share spread in `KTLX20260504_032003_V06`: record 51 had 8.592% valid events
  while record 13 had 50.437%. The largest sweep spread was also in
  `KTLX20260504_032003_V06`: sweep 11 had 9.187% valid events while sweep 2 had
  44.909%.
- Replay-shape validation also reports radial and minute-bucket valid-share
  spreads using message timestamps from the RDA/RPG message header.
- Cache inspection can aggregate selected local cache files and report file-kind,
  compressed-record, decompressed-byte, message, Type 31 radial, and estimated
  gate-moment totals.
- Cache-selection replay can publish a selected cache slice with
  `archive replay --cache data/nexrad [--date yyyy-MM-dd] [--radar KTLX]
  [--max-files n]`, reusing one replay publish session across files, skipping
  non-Archive Two files, and aggregating status totals and checksums in selected
  cache order.
- Full local KTLX cache replay for `2026-05-04` examined 244 files, skipped 24
  non-base-data files, published 220 Archive Two files, and reported
  8_513_587_200 published events with 1_369_194_138 valid events.
- Full local KTLX cache replay-publish benchmark for `2026-05-04` validated two
  full cache iterations with the same chronology checksum and measured
  310_665_492.15 published events/s with 0.06 allocated bytes/event.
- The parse benchmark now gives a first measured answer against the 20M
  events/s target for decompression plus minimal parsing.
- With `--decode-moments`, the same KTLX file decodes all 38_759_040 raw
  gate-moment values per iteration and measures above the 20M values/s target.
- The latest Release performance rerun measured 910.77 decompressed MB/s,
  501_164_693 minimal-parse estimated events/s, 670_226_077 calibrated-parse
  decoded values/s, and 230_347_912 replay-shaped events/s on
  `KTLX20260504_000245_V06` with `radarpulse` and `--parallelism 24`.
- Calibrated parse is faster than replay-shape because it reads/classifies
  values and updates counters/checksums, while replay-shape also builds the
  publisher-facing event shape, carries source-order/time identity, computes
  order-sensitive chronology, and pays for the parallel projector prepass plus
  ordered aggregation. The slower replay-shape path still remains roughly 11.5x
  above the 20M events/s target on this file.

Deferred beyond milestone 003:

- No downstream event publisher is implemented yet.
- The parser/replay benchmarks still do not publish downstream engine events.
- The existing replay-shape benchmark/validator still have their own parallel
  projection loops instead of reusing the new production replay publisher path;
  this is not required for milestone 003 closure.

## Documentation

- `docs/milestones/001-historical-loader-plan.md`
- `docs/milestones/001-historical-loader.md`
- `docs/milestones/002-nexrad-archive-inspection-plan.md`
- `docs/milestones/002-nexrad-archive-inspection.md`
- `docs/milestones/003-historical-replay-publisher-plan.md`
- `docs/milestones/003-historical-replay-publisher.md`
- `docs/milestones/004-processing-core-input-contract.md`
- `docs/milestones/004-processing-core-input-contract-plan.md`
- `docs/handoff.md`

## Verification

Latest milestone 004 normalized stream benchmark verification:

```powershell
dotnet test RadarPulse.sln --no-restore
dotnet build src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet run --no-build --project src\Presentation\RadarPulse.Cli.csproj -- archive stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --parallelism 1 --decompressor radarpulse
dotnet run --no-build --project src\Presentation\RadarPulse.Cli.csproj -- archive stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --parallelism 4 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 3 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 3 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_002334_V06 --iterations 5 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse
dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- archive benchmark stream --cache data\nexrad --max-files 1000000 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Result:

```text
tests: 140 passed, 3 skipped
debug build: 0 warnings, 0 errors
release build: 0 warnings, 0 errors

parallelism 1 and 4 both produced:
Stream schema version: 1
Dictionary version: 9
Source-universe version: 1
Logical sources: 23_040
Compressed records: 55
Compressed bytes: 5_500_904
Decompressed bytes: 50_741_824
Batches: 1
Events: 32_400
Payload bytes: 48_257_280
Payload values: 38_759_040
Raw value checksum: 1_091_828_328
Radar dictionary entries: 1
Moment dictionary entries: 7
Dictionary mapping checksum: 15_566_013_436_132_944_234

Release benchmark stream after no-copy batch finalization and cached counters, parallelism 1:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 1_319.05
Stream events/s: 73_689.31
Payload values/s: 88_152_063.19
Allocated bytes / payload value: 1.61

Release benchmark stream after no-copy batch finalization and cached counters, parallelism 24:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 357.59
Stream events/s: 271_822.42
Payload values/s: 325_172_098.27
Allocated bytes / payload value: 4.51

Release benchmark stream after projector-local hot-path caches, parallelism 24, longer 5-iteration check:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 390.54
Stream events/s: 414_805.91
Payload values/s: 496_218_480.83
Allocated bytes / payload value: 4.39

Release benchmark stream cache-wide after projector-local hot-path caches, parallelism 24:
Examined files per iteration: 244
Skipped files per iteration: 24
Published files per iteration: 220
Compressed records per iteration: 12_087
Decompressed bytes per iteration: 11_145_331_584
Stream events per iteration: 7_114_560
Payload values per iteration: 8_513_587_200
Elapsed ms: 17_779.37
Stream events/s: 400_158.13
Payload values/s: 478_846_352.92
Allocated bytes / payload value: 4.68
```

The skipped tests are the opt-in live AWS integration tests and opt-in local
corpus validation test.

Latest milestone 003 implementation verification:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
66 passed, 3 skipped
```

The skipped tests are the opt-in live AWS integration tests and opt-in local
corpus validation test.

Latest milestone 003 publisher smoke commands:

```powershell
$ReplayOutput = $null
$elapsed = Measure-Command {
    $script:ReplayOutput = & dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --parallelism 1 --decompressor radarpulse
}
$ParallelReplayOutput = $null
$parallelElapsed = Measure-Command {
    $script:ParallelReplayOutput = & dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --parallelism 24 --decompressor radarpulse
}
```

Result:

```text
parallelism 1:
Published events: 38_759_040
Valid events: 5_523_459
Raw value checksum: 1_063_626_011
Calibrated value scaled checksum: 70_028_121_122
Chronology checksum: 5_257_350_734_454_804_390
Measured elapsed ms: 1_222.83
Measured published events/s: 31_696_112.78

parallelism 24:
Published events: 38_759_040
Valid events: 5_523_459
Raw value checksum: 1_063_626_011
Calibrated value scaled checksum: 70_028_121_122
Chronology checksum: 5_257_350_734_454_804_390
Measured elapsed ms: 592.17
Measured published events/s: 65_453_053.24
```

This is an external CLI smoke measurement after a Release build, so it includes
process startup overhead.

Latest milestone 003 cache replay smoke command:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 2 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Examined files: 2
Skipped files: 0
Published files: 2
Compressed records: 110
Compressed bytes: 10_848_033
Decompressed bytes: 101_483_648
Published events: 77_518_080
Valid events: 11_076_025
Raw value checksum: 2_135_395_556
Calibrated value scaled checksum: 140_796_164_125
Chronology checksum: 10_768_380_537_427_882_607
Chronology verification: required
```

The exact throughput is not measured by this smoke command; use
`archive benchmark replay-publish` for single-file steady-state performance.

Latest milestone 003 full cache replay smoke command:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive replay --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1000000 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Examined files: 244
Skipped files: 24
Published files: 220
File size bytes: 1_330_687_937
Compressed records: 12_087
Compressed bytes: 1_330_634_309
Decompressed bytes: 11_145_331_584
Published events: 8_513_587_200
Valid events: 1_369_194_138
Valid event share: 16.082%
Below-threshold events: 5_841_331_993
Range-folded events: 842_331
CFP filter-not-applied events: 1_277_128_201
CFP point-clutter-filter events: 14_296_674
CFP dual-pol-filtered events: 10_793_863
Reserved events: 0
Unsupported events: 0
Raw value checksum: 266_648_133_947
Calibrated value scaled checksum: 21_398_534_126_880
Chronology checksum: 9_060_754_844_693_896_318
```

Latest milestone 003 replay-shape comparison commands:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-shape --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-shape --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result:

```text
parallelism 1:  50_671_150.52 replay-shaped events/s
parallelism 24: 248_026_584.81 replay-shaped events/s
chronology checksum per iteration: 5_257_350_734_454_804_390
```

Latest milestone 003 internal publisher benchmark commands:

```powershell
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-publish --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 1 --decompressor radarpulse
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-publish --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
dotnet .\src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll archive benchmark replay-publish --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 1000000 --iterations 2 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Result:

```text
parallelism 1:
Published events per iteration: 38_759_040
Published events/s: 51_754_463.69
Allocated bytes / event: 0.06

parallelism 24:
Published events per iteration: 38_759_040
Published events/s: 362_695_693.02
Allocated bytes / event: 0.07
Chronology checksum per iteration: 5_257_350_734_454_804_390

cache KTLX 2026-05-04, parallelism 24:
Iterations: 2
Examined files per iteration: 244
Skipped files per iteration: 24
Published files per iteration: 220
Published events per iteration: 8_513_587_200
Valid events per iteration: 1_369_194_138
Chronology checksum per iteration: 9_060_754_844_693_896_318
Published events/s: 310_665_492.15
Valid events/s: 49_962_649.20
Allocated bytes / event: 0.06
```

This benchmark now uses `NexradArchiveReplayPublishSession` inside the timed
loop. It removes per-command process startup and also reuses replay workers,
decompressor sessions, projectors, accumulators, and compressed/output buffers
across warmup and measured iterations. The older `replay-shape` benchmark keeps
its own benchmark workers outside its timed iteration window, so allocation and
throughput numbers are not one-to-one.

Current milestone 003 performance assessment:

- Sequential publisher throughput is acceptable for the milestone because
  `51_754_463.69` published events/s is above the initial 20M events/s target
  through the publisher path.
- Parallel publisher throughput is strong for the milestone because
  `362_695_693.02` published events/s confirms the ordered merge can preserve
  chronology while exceeding the target by a wide margin on the current KTLX
  smoke file.
- Worker/decompressor-session setup allocation is no longer visible as a major
  benchmark cost. Parallel allocation pressure is now about `0.07` bytes/event
  on the current smoke file; remaining allocation work should be driven by
  cache-wide replay profiling.

Likely remaining allocation contributors in the current publisher benchmark:

```text
per-file record descriptor and metadata arrays
per-record metadata radial arrays
Task/Parallel scheduling infrastructure
per-record event buffers in the custom publisher path
```

Potential later performance slice before treating replay as long-running
production profile:

```text
reuse or pool record descriptor and metadata-radial storage where practical
compare cache benchmark allocation before/after metadata storage reuse
profile whether Parallel/ConcurrentStack scheduling is visible after metadata allocation is reduced
```

Earlier milestone 003 planning slice changed documentation only.

Last verified normal command after the current milestone 002 slice:

```powershell
dotnet test RadarPulse.sln --no-restore
```

Result:

```text
55 passed, 3 skipped
```

Manual CLI smoke tests:

```powershell
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_005834_V06_MDM
dotnet run --project src/Presentation/RadarPulse.Cli.csproj -- archive inspect --cache data/nexrad --date 2026-05-04 --radar KTLX --max-files 2
```

The first command classified the file as `Archive Two base data` and parsed
`AR2V0006.266`, version `06`, extension `266`, radar `KTLX`, and volume time
`2026-05-04T00:02:45.042Z`. It also found 55 compressed records, 5_406_610
compressed bytes, 55 records with BZip2 signatures, 55 decompressed records,
50_741_824 decompressed bytes, zero decompression diagnostics, 6_496 messages,
6_480 Type 31 radials, 38_759_040 estimated gate-moment events, 6_480 each of
`VOL`/`ELV`/`RAD` constant blocks, 12 sweep summaries, and descriptor metadata
for all observed moments. The second command classified the `_MDM` file as
`MDM or compressed stream`. The cache inspect smoke examined 2 KTLX files,
classified both as Archive Two base data, and aggregated 110 compressed records,
101_483_648 decompressed bytes, 12_992 messages, 12_960 Type 31 radials, and
77_518_080 estimated gate-moment events.

Last verified decompression validation command:

```powershell
dotnet run --no-restore --project src/Presentation/RadarPulse.Cli.csproj -- archive validate decompress --cache data/nexrad --radar KTLX --max-files 20
```

Result:

```text
Candidate decompressor: radarpulse
Reference decompressor: sharpziplib
Examined files: 22
Skipped files: 2
Compared files: 20
Failed files: 0
Compressed records: 1_100
Compressed bytes: 112_494_786
Decompressed bytes: 1_014_836_480
```

Last verified opt-in corpus command:

```powershell
$env:RADARPULSE_RUN_CORPUS_TESTS='true'; $env:RADARPULSE_NEXRAD_CORPUS='data/nexrad'; $env:RADARPULSE_NEXRAD_CORPUS_RADAR='KTLX'; $env:RADARPULSE_NEXRAD_CORPUS_MAX_FILES='20'; dotnet test RadarPulse.sln --no-restore --filter NexradArchiveDecompressionValidatorCorpusTests
```

Result:

```text
1 passed, 0 skipped
```

Last verified decompression benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark decompress --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 10 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Decompressor: radarpulse
Iterations: 10
Warmup iterations: 1
Parallelism: 24
Compressed records per iteration: 55
Compressed bytes per iteration: 5_406_610
Decompressed bytes per iteration: 50_741_824
Elapsed ms: 467.16
Compressed MB/s: 115.73
Decompressed MB/s: 1_086.18
Records/s: 1_177.33
Allocated bytes: 1_243_568
Allocated bytes / decompressed MB: 2_450.78
Allocated bytes / record: 2_261.03
```

Historical SharpCompress baseline before the decoder comparison:

```text
Elapsed ms: 1_606.65
Compressed MB/s: 10.10
Decompressed MB/s: 94.75
Records/s: 102.70
Allocated bytes: 907_268_368
```

After adding the reusable-workspace `radarpulse` decoder, the Release comparison
on the same machine and file produced:

```text
iterations: 10
warmup iterations: 1

decompressor  parallelism  elapsed ms  decompressed MB/s  records/s  allocated bytes  allocated bytes / record
radarpulse    1            3_800.97    133.50             144.70     43_920           79.85
radarpulse    24           467.16      1_086.18           1_177.33   1_243_568        2_261.03
sharpziplib   24           643.11      789.01             855.22     2_511_390_704    4_566_164.92
```

Parallel decompression improves byte throughput substantially on the current
machine. Future parser/replay work must preserve file/message order when
publishing data: worker completion order is not a valid stream order.

Last verified parse benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Decompressor: radarpulse
Iterations: 20
Warmup iterations: 2
Parallelism: 24
Messages per iteration: 6_496
Type 31 radials per iteration: 6_480
Estimated gate-moment events per iteration: 38_759_040
Elapsed ms: 1_035.20
Compressed MB/s: 104.46
Decompressed MB/s: 980.33
Messages/s: 125_502.63
Type 31 radials/s: 125_193.51
Estimated gate-moment events/s: 748_824_137.31
Allocated bytes: 25_297_992
Allocated bytes / estimated event: 0.03
```

The sequential Release parse benchmark on the same file and backend measured
about 90_930_375 estimated gate-moment events/s with `--parallelism 1`.

Last verified decoded moment benchmark command:

```powershell
dotnet run --no-restore -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 20 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-moments
```

Result on the current development machine:

```text
Decode moment values: True
Messages per iteration: 6_496
Type 31 radials per iteration: 6_480
Estimated gate-moment events per iteration: 38_759_040
Decoded gate-moment values per iteration: 38_759_040
Decoded gate-moment value checksum per iteration: 1_063_626_011
Elapsed ms: 1_174.67
Decompressed MB/s: 863.93
Estimated gate-moment events/s: 659_912_891.38
Decoded gate-moment values/s: 659_912_891.38
Allocated bytes: 25_314_800
Allocated bytes / decoded value: 0.03
```

The sequential Release decoded benchmark on the same file and backend measured
about 96_122_482 decoded gate-moment values/s with `--parallelism 1`.

Last verified calibrated moment benchmark command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark parse --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 10 --warmup-iterations 2 --parallelism 24 --decompressor radarpulse --decode-calibrated-moments
```

Result on the current development machine:

```text
Decode calibrated moment values: True
Decoded gate-moment values per iteration: 38_759_040
Calibrated gate-moment values per iteration: 5_523_459
Below-threshold gate-moment values per iteration: 27_316_941
Range-folded gate-moment values per iteration: 1_355
CFP filter-not-applied values per iteration: 5_794_484
CFP point-clutter-filter values per iteration: 65_871
CFP dual-pol-filtered values per iteration: 56_930
Reserved gate-moment values per iteration: 0
Unsupported calibrated gate-moment values per iteration: 0
Calibrated gate-moment value scaled checksum per iteration: 70_028_121_122
Calibrated value range per iteration: -31.5..359.649
Elapsed ms: 578.30
Estimated gate-moment events/s: 670_226_077.21
Decoded gate-moment values/s: 670_226_077.21
Calibrated gate-moment values/s: 95_512_331.01
Allocated bytes / calibrated value: 0.66
```

The sequential Release calibrated benchmark on the same file and backend
measured about 10_851_453 valid calibrated values/s, while still reading all
raw gate-moment values at about 76_146_475 decoded values/s.

Last verified replay-shape benchmark command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 5 --warmup-iterations 1 --parallelism 24 --decompressor radarpulse
```

Result on the current development machine:

```text
Replay-shaped events per iteration: 38_759_040
Valid events per iteration: 5_523_459
Below-threshold events per iteration: 27_316_941
Range-folded events per iteration: 1_355
CFP filter-not-applied events per iteration: 5_794_484
CFP point-clutter-filter events per iteration: 65_871
CFP dual-pol-filtered events per iteration: 56_930
Reserved events per iteration: 0
Unsupported events per iteration: 0
Raw value checksum per iteration: 1_063_626_011
Calibrated value scaled checksum per iteration: 70_028_121_122
Chronology checksum per iteration: 5_257_350_734_454_804_390
Calibrated value range per iteration: -31.5..359.649
Range km per iteration: 2.125..459.875
Replay-shaped events/s: 230_347_912.41
Valid events/s: 32_826_335.48
Allocated bytes / event: 0.07
```

The calibrated parse benchmark is intentionally cheaper than replay-shape:
calibrated parse reads/classifies values and updates aggregate counters, while
replay-shape constructs full ordered event records and computes chronology.
The current replay-shape result is still roughly 11.5x above the 20M events/s
target.

Last verified sequential chronology smoke command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive benchmark replay-shape --file data/nexrad/level2/2026/05/04/KTLX/KTLX20260504_000245_V06 --iterations 1 --warmup-iterations 0 --parallelism 1 --decompressor radarpulse
```

Result:

```text
Chronology checksum per iteration: 5_257_350_734_454_804_390
Replay-shaped events per iteration: 38_759_040
Raw value checksum per iteration: 1_063_626_011
Calibrated value scaled checksum per iteration: 70_028_121_122
```

Latest replay-shape validation smoke command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive validate replay-shape --cache data/nexrad --radar KTLX --max-files 1 --parallelism 24 --decompressor radarpulse
```

Result:

```text
Compared files: 1
Failed files: 0
Record valid-share spread: 21.858%
Sweep valid-share spread: 18.336%
Radial valid-share spread: 27.012%
Minute valid-share spread: 14.757%
```

Previously verified full cache-wide replay-shape validation command:

```powershell
dotnet run --no-build -c Release --project src/Presentation/RadarPulse.Cli.csproj -- archive validate replay-shape --cache data/nexrad --radar KTLX --parallelism 24 --decompressor radarpulse
```

Result on the current cache:

```text
Examined files: 244
Skipped files: 24
Compared files: 220
Failed files: 0
Replay-shaped events: 8_513_587_200
Valid events: 1_369_194_138
Valid event share: 16.082%
Reserved events: 0
Unsupported events: 0
Record valid-share spread top file: KTLX20260504_032003_V06, record 51 8.592% -> record 13 50.437%
Sweep valid-share spread top file: KTLX20260504_032003_V06, sweep 11 9.187% -> sweep 2 44.909%
```

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

Additional documentation search found:

```text
ROC ICD 2620010J Archive II/User, Build 23.0:
  https://www.roc.noaa.gov/public-documents/icds/2620010J.pdf
ROC ICD 2620002Y RDA/RPG, Build 23.0:
  https://www.roc.noaa.gov/public-documents/icds/2620002Y.pdf
ROC ICD index:
  https://www.roc.noaa.gov/interface-control-documents.php
NCEI NEXRAD archive overview:
  https://www.ncei.noaa.gov/products/radar/next-generation-weather-radar
NCEI decoding utilities:
  https://www.ncei.noaa.gov/products/radar/decoding-utilities-examples
```

The expected base-data record shape is:

```text
24-byte Archive Two volume header
repeated records:
  4-byte big-endian signed control word
  abs(control word) bytes of bzip2-compressed Archive Two messages
```

The first compressed record contains metadata messages. Later records contain
radial messages, primarily Message Type 31, and may include Message Type 2 RDA
status messages. Message Type 31 represents one radial and contains pointers to
constant and moment data blocks.

## Constraints

- Live AWS tests remain opt-in because they require network access and public AWS
  availability.
- Do not use the deprecated `noaa-nexrad-level2` bucket for loader work.
- Large downloaded data and generated manifests under `data/` stay outside
  source control.
- Do not commit large real NEXRAD archive binary fixtures unless a deliberate fixture
  strategy is agreed first.
- Milestone 002 should avoid promising visualization, event processing,
  partitioning, production replay benchmarking, or live ingestion.
- The decompression throughput check should guide parser design toward the
  eventual 20M events/s replay target.
- Parallel decompression is allowed only behind an ordered merge or another
  explicit ordering contract; historical replay must not accidentally publish
  messages/events in worker completion order.

## Important Files

- `docs/milestones/004-processing-core-input-contract.md`
- `docs/milestones/004-processing-core-input-contract-plan.md`
- `src/Domain/Streaming/DenseIdentityAllowedCharacters.cs`
- `src/Domain/Streaming/DenseIdentityCanonicalizationPolicy.cs`
- `src/Domain/Streaming/DenseIdentityCatalog.cs`
- `src/Domain/Streaming/DenseIdentityCatalogDelta.cs`
- `src/Domain/Streaming/DenseIdentityCatalogEntry.cs`
- `src/Domain/Streaming/DenseIdentityCatalogSnapshot.cs`
- `src/Domain/Streaming/DenseIdentityValidationError.cs`
- `src/Domain/Streaming/DenseIdentityValidationInputKind.cs`
- `src/Domain/Streaming/DenseIdentityValidationResult.cs`
- `src/Domain/Streaming/RadarEventBatch.cs`
- `src/Domain/Streaming/RadarEventBatchBuilder.cs`
- `src/Domain/Streaming/RadarEventBatchMetrics.cs`
- `src/Domain/Streaming/RadarEventBatchValidationError.cs`
- `src/Domain/Streaming/RadarEventBatchValidationResult.cs`
- `src/Domain/Streaming/RadarEventBatchValidator.cs`
- `src/Domain/Streaming/RadarSourceKey.cs`
- `src/Domain/Streaming/RadarSourceUniverse.cs`
- `src/Domain/Streaming/RadarStreamChecksum.cs`
- `src/Domain/Streaming/RadarStreamDictionarySnapshot.cs`
- `src/Domain/Streaming/RadarStreamDictionarySnapshotMetrics.cs`
- `src/Domain/Streaming/RadarStreamEvent.cs`
- `src/Domain/Streaming/RadarStreamIdentity.cs`
- `src/Domain/Streaming/RadarStreamIdentityNormalizationError.cs`
- `src/Domain/Streaming/RadarStreamIdentityNormalizationResult.cs`
- `src/Domain/Streaming/RadarStreamIdentityNormalizer.cs`
- `src/Domain/Streaming/StreamSchemaVersion.cs`
- `src/Domain/Streaming/DictionaryVersion.cs`
- `src/Domain/Streaming/SourceUniverseVersion.cs`
- `src/Domain/Streaming/RadarStreamWordSize.cs`
- `src/Domain/Streaming/RadarStreamStatusModel.cs`
- `src/Application/Archive/ArchiveRadarEventBatchPublishOptions.cs`
- `src/Application/Archive/IArchiveRadarEventBatchPublisher.cs`
- `src/Domain/Archive/ArchiveRadarEventBatchPublishResult.cs`
- `src/Domain/Archive/ArchiveRadarEventBatchStreamCacheBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveRadarEventBatchStreamBenchmarkResult.cs`
- `src/Infrastructure/Archive/ArchiveRadarEventBatchCountingPublisher.cs`
- `src/Infrastructure/Archive/ArchiveTwoRadarEventBatchProjector.cs`
- `src/Infrastructure/Archive/NexradArchiveRadarEventBatchPublisher.cs`
- `src/Infrastructure/Archive/NexradArchiveRadarEventBatchStreamBenchmark.cs`
- `tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarEventBatchBuilderTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarEventBatchValidatorTests.cs`
- `tests/RadarPulse.Tests/Streaming/DenseIdentityCanonicalizationPolicyTests.cs`
- `tests/RadarPulse.Tests/Streaming/DenseIdentityCatalogTests.cs`
- `tests/RadarPulse.Tests/Streaming/DenseIdentityCatalogVersioningTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarStreamIdentityNormalizerTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarSourceUniverseTests.cs`
- `tests/RadarPulse.Tests/Streaming/RadarStreamContractTests.cs`
- `src/Presentation/Program.cs`
- `src/Application/Archive/IHistoricalArchiveClient.cs`
- `src/Application/Archive/HistoricalArchiveManifestSelector.cs`
- `src/Infrastructure/Archive/AwsNexradArchiveClient.cs`
- `src/Infrastructure/Archive/ArchiveBZip2Decompressors.cs`
- `src/Infrastructure/Archive/HistoricalArchiveDownloader.cs`
- `src/Infrastructure/Archive/IArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/ArchiveTwoFileReader.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageStreamScanner.cs`
- `src/Infrastructure/Archive/ArchiveTwoMessageSummaryBuilder.cs`
- `src/Domain/Archive/ArchiveTwoGateMomentEvent.cs`
- `src/Domain/Archive/NexradArchiveCacheInspection.cs`
- `src/Domain/Archive/ArchiveTwoReplayShapeBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveTwoReplayShapeValidationResult.cs`
- `src/Domain/Archive/ArchiveReplayPublishResult.cs`
- `src/Domain/Archive/ArchiveReplayCachePublishResult.cs`
- `src/Domain/Archive/ArchiveReplayPublishBenchmarkResult.cs`
- `src/Domain/Archive/ArchiveReplayPublishCacheBenchmarkResult.cs`
- `src/Infrastructure/Archive/IArchiveTwoMessageConsumer.cs`
- `src/Application/Archive/IArchiveReplayEventPublisher.cs`
- `src/Application/Archive/ArchiveReplayPublishOptions.cs`
- `src/Infrastructure/Archive/ArchiveReplayEventAccumulator.cs`
- `src/Infrastructure/Archive/ArchiveReplayCountingPublisher.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayPublisher.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayPublishSession.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayPublishBenchmark.cs`
- `src/Infrastructure/Archive/ArchiveTwoGateMomentChronologyChecksum.cs`
- `src/Infrastructure/Archive/ArchiveTwoGateMomentEventProjector.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveParseBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayShapeBenchmark.cs`
- `src/Infrastructure/Archive/NexradArchiveReplayShapeValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveCacheInspector.cs`
- `src/Infrastructure/Archive/NexradArchiveDecompressionValidator.cs`
- `src/Infrastructure/Archive/NexradArchiveFileInspector.cs`
- `src/Infrastructure/Archive/ReusableArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/NexradCachePathMapper.cs`
- `src/Infrastructure/Archive/SharpCompressArchiveBZip2Decompressor.cs`
- `src/Infrastructure/Archive/SharpZipLibArchiveBZip2Decompressor.cs`
- `tests/RadarPulse.Tests/Archive/*`

## Milestone 003 Done Criteria

Milestone 003 is complete:

- RadarPulse exposes an explicit replay publisher API for
  `ArchiveTwoGateMomentEvent`. (Implemented.)
- One cached Archive Two file can publish ordered events through that API.
  (Implemented for sequential and parallel replay.)
- A counting/checksum publisher can verify status totals, raw checksum,
  calibrated checksum, and chronology checksum. (Implemented.)
- The production-facing parallel replay path publishes through an ordered merge
  rather than worker completion order. (Implemented.)
- Sequential and parallel replay over the same file produce identical counts
  and chronology checksums. (Implemented.)
- The CLI can smoke-test the publisher path. (Implemented for
  `--file`, `--cache`, and `--parallelism n`.)
- The CLI can benchmark cache-wide replay-publish throughput and allocations.
  (Implemented for `archive benchmark replay-publish --cache`.)
- Focused tests cover ordering, totals, diagnostics, and cancellation.
  (Implemented for sequential, parallel, custom-publisher, benchmark, and
  reusable-session/cache-selection paths.)


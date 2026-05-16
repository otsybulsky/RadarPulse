# Milestone 004: Decision Trace

## 1. What Was Implemented

Milestone 004 implemented the normalized processing-core input contract:

- `RadarEventBatch` with stream schema, dictionary version, source-universe
  version, event memory, payload memory, cached payload metrics, and explicit
  owned/leased lifetime.
- `RadarStreamEvent` as a 64-byte unmanaged hot-path record with dense source
  identity, chronology, moment metadata, and payload references.
- Append-only dense identity catalogs with canonicalization, validation,
  versioned snapshots, and deltas.
- `RadarStreamIdentityNormalizer` for radar and moment text to dense numeric
  IDs.
- `RadarSourceUniverse` for deterministic dense `SourceId` derivation from
  `RadarOrdinal x ElevationSlot x AzimuthBucket x RangeBand`.
- Sequential replay, ordered parallel replay, cache replay, validation,
  deterministic metrics, checksums, CLI smoke commands, and stream benchmarks.
- Leased hot-path batch delivery for reusable stream sessions, plus
  `ToOwnedSnapshot()` for retained batches.

Verified result:

```text
single file: 553_123_110.90 payload values/s
cache-wide:  509_716_417.97 payload values/s
cache-wide allocation: 0.20 allocated bytes/payload value
```

## 2. Decision Matrix

### Batched Struct Stream

Decision: use `RadarEventBatch` with compact `RadarStreamEvent` structs, where
one event can reference a run of gate values.

Why chosen: this keeps the hot path cache-conscious, deterministic, and free of
per-gate object/text overhead while meeting the 300M+ payload-value target.

Alternatives: one semantic event per gate, reuse the milestone 003 external
event shape, or let the processing core parse Archive Two data directly.

Rejected because: those options multiply event count, carry boundary-level
semantics through the hot path, or couple the core to archive parsing.

Trade-offs/debt: consumers must understand payload ranges, not just single
values. Diagnostics need expansion tools for human-readable inspection.

Review explanation: "I separated the external semantic API from the internal
transport. The engine gets compact deterministic batches; semantic expansion
stays at the boundary."

### Dense Versioned Dictionaries

Decision: normalize text identities into dense append-only dictionaries with
versioned snapshots and deltas.

Why chosen: dense IDs allow array/span/bitset based state tables, while
versioned visibility lets external consumers interpret numeric IDs correctly.

Alternatives: prebuild dictionaries, keep strings in every event, use hashes as
IDs, or assign unversioned first-seen IDs.

Rejected because: identities cannot be fully known upfront, strings are too
expensive, hashes are not dense and need collision handling, and unversioned
dynamic IDs are hard to explain outside the process.

Trade-offs/debt: registration is a cold serialized path. Snapshot/delta state
must remain available for future consumers and may need durable publication
later.

Review explanation: "The hot path sees only numbers, but the system keeps
identity explainability through versioned dictionaries."

### Versioned Source Universe

Decision: derive `SourceId` from
`RadarOrdinal x ElevationSlot x AzimuthBucket x RangeBand`; keep moment as an
event channel, not part of the preferred source identity.

Why chosen: one downloaded radar can produce tens of thousands of logical
sources, and dense arithmetic IDs support direct indexing in future state
tables.

Alternatives: one source per physical radar, include moment in source identity,
assign IDs by first-seen order, or use raw metadata values as IDs.

Rejected because: one physical radar is too coarse, moment-in-source inflates
source cardinality, first-seen assignment can become nondeterministic, and raw
metadata values are not dense.

Trade-offs/debt: bucket/range layout changes require a new
`SourceUniverseVersion`; persisted future state will need migration rules.

Review explanation: "I turned one radar feed into a stable dense source
universe, so the single-radar baseline can exercise 20K-30K logical-source
behavior."

### Chronological Multi-Source Batches

Decision: allow one batch to contain interleaved events from many sources, while
preserving deterministic radar chronology.

Why chosen: chronology is part of replay correctness, and downstream processing
can update per-source state as events arrive.

Alternatives: group by source first, partition during milestone 004, or make
consumers reorder events.

Rejected because: grouping hides chronology, partitioning belongs to a later
milestone, and consumer-side reordering duplicates work.

Trade-offs/debt: later partitioning must preserve the ordering contract.

Review explanation: "The batch is the canonical chronological input sequence,
not a source-grouped container."

### Raw Payload With Explicit Ranges

Decision: store raw radar values in batch payload storage and reference them
with `PayloadOffset`/`PayloadLength`; calibration remains derived from event
metadata.

Why chosen: raw payload is compact, deterministic, and preserves original
measurement data for future algorithms.

Alternatives: store calibrated floats, store one decoded value per event, or
reference parser-owned Archive Two buffers.

Rejected because: calibrated floats expand memory and lock in one
interpretation, per-value events hurt throughput, and parser-owned buffers make
lifetime unsafe.

Trade-offs/debt: consumers must apply calibration correctly; payload reference
validation remains mandatory.

Review explanation: "The stream keeps sensor data compact and defers
interpretation to explicit metadata."

### Owned And Leased Batch Lifetimes

Decision: make batch lifetime explicit: `Owned` batches may be retained;
`Leased` batches are valid only during the synchronous callback and require
`ToOwnedSnapshot()` for retention.

Why chosen: reusable hot-path buffers remove the main batch allocation cost
without changing the logical data shape.

Alternatives: always allocate owned arrays, hide pooled buffers behind the same
contract, or introduce reference-counted async buffers now.

Rejected because: always-owned batches were allocation-heavy, hidden pooling is
unsafe, and reference counting adds complexity before async transport exists.

Trade-offs/debt: consumers must obey the callback lifetime rule. Async queues
will need owned snapshots or a separate retained-buffer protocol.

Review explanation: "I made lifetime a first-class contract instead of a hidden
optimization, which made buffer reuse safe and explicit."

### Scope Boundary

Decision: milestone 004 stops at the input contract and replay publication
path; it does not implement processing algorithms, live ingestion, durable
transport, shared memory, or visualization.

Why chosen: later engine work needs a stable, measured, deterministic input
surface first.

Alternatives: start partitioning, implement early algorithms, or add durable
transport now.

Rejected because: those layers depend on the stream contract and would obscure
whether the input shape itself can meet throughput goals.

Trade-offs/debt: milestone 004 proves input-stream construction throughput, not
end-to-end processing latency.

Review explanation: "I deliberately finished the contract before the engine, so
later work starts from a stable and benchmarked input surface."

### Performance Decisions

Decision: optimize around the comparable payload-value metric, a 64-byte
unmanaged event record, dense IDs, cached hot-path identity dimensions,
pre-sized buffers, no-copy batch finalization, cached payload metrics, reusable
publish sessions, and explicit leased batch delivery.

Why chosen: `Stream events/s` is not comparable to earlier per-gate publisher
numbers because one `RadarStreamEvent` can reference many raw values. The real
throughput target is payload values/s, while allocation pressure is dominated
by event/payload buffer ownership.

Alternatives: compare against `Stream events/s`, allocate owned arrays for every
batch, keep text lookup in each event, scan payload again for metrics, or hide
pooled buffer lifetime from consumers.

Rejected because: those options misstate throughput, add avoidable allocation,
put string/dictionary work in the hot path, duplicate payload scans, or make
buffer retention unsafe.

Trade-offs/debt: leased batches require consumers to obey synchronous callback
lifetime rules. Cache-wide allocation is reduced but not zero; remaining cost is
outside normalized batch buffers.

Review explanation: "I optimized the metric that actually maps to the old
event denominator: raw payload values per second. The result kept the stream
contract deterministic while raising throughput above 500M values/s and cutting
cache-wide allocation to 0.20 bytes/value."

## 3. Remaining Risks And Debt

- Cache-wide allocation is much lower but not zero. Likely remaining sources:
  compressed-record descriptors, ordered task scheduling, file enumeration/order
  materialization, and scanner/decompression churn.
- Leased batches are safe only if consumers respect the synchronous callback
  lifetime rule.
- Dictionary snapshots/deltas are externally visible in memory; durable
  publication remains a future design topic.
- Source-universe changes will need migration rules for future persisted state.

## 4. Portfolio Review Summary

Milestone 004 converted archive replay output into a deterministic,
source-addressable, high-throughput input stream for a future processing core.
The core decisions were compact batched structs, dense versioned dictionaries,
dense source-universe arithmetic, raw payload ranges, chronological multi-source
batches, and explicit owned/leased batch lifetime. Together, these choices kept
the stream explainable and externally interpretable while achieving more than
500M payload values/s on the current benchmark corpus.

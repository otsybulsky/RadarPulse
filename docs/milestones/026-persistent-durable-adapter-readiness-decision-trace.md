# Milestone 026 Decision Trace

Date: 2026-05-26

Decision: accept persistent durable adapter readiness for deterministic
archive-shaped MVP workloads with named scoped warnings, stopping milestone
026 at the deterministic local file-based adapter.

This decision accepts milestone 026's versioned persistent durable envelope
schema, deterministic local file-backed durable envelope store, optional
persistent backend for `RadarProcessingDurableEnvelopeQueue`, restart
recovery semantics, adapter-backed ordered processing commit, handler delta
replay compatibility, operator-readable adapter summary, and focused Release
gate on top of the milestone 020 runtime/archive baseline, the milestone 021
ordered processing foundation, the milestone 022 ordered rebalance/topology
foundation, the milestone 023 durable envelope contract, the milestone 024
custom handler output/BFF surface, and the milestone 025 handler delta/merge
contract.

The accepted scope is a local file-based persistent adapter that proves
RadarPulse-owned durable state can survive adapter/session recreation while
preserving provider-sequence ordered commit, explicit recovery policy,
handler delta idempotency, retained cleanup posture, and operator-visible
blocking diagnostics.

The milestone deliberately stops at the file-based adapter. Kafka, RabbitMQ,
cloud queue, database-backed adapters, production broker retention behavior,
cross-machine throughput certification, true live ingestion, production BFF
hosting, frontend implementation, deployment operations, and exactly-once
production delivery are not accepted by this decision and require separate
future milestone decisions if selected.

## Decision Matrix

```text
persistent durable adapter readiness:
  accepted with scoped warnings

file-based durable adapter:
  accepted; deterministic local file-backed persistence is the milestone 026
  adapter and the milestone stops at this adapter

durable envelope schema:
  accepted; schema version, durable batch id, provider sequence, attempt,
  state, worker id, message, timestamps, counters, and serialized batch
  payload are explicit

schema compatibility:
  accepted; unsupported or corrupt persisted state fails closed instead of
  being interpreted silently

idempotent duplicate accept:
  accepted; duplicate accept after restart returns the original envelope and
  provider sequence

restart recovery:
  accepted; pending, claimed, completed, failed, abandoned, poison, canceled,
  and released states preserve milestone 023 semantics after adapter/session
  recreation

claimed-envelope recovery posture:
  accepted; claimed envelopes remain claimed after restart and require
  explicit abandon, fail, complete, or recovery policy action

adapter-backed ordered processing commit:
  accepted; completed envelopes can recover scoped completion material and
  commit by provider sequence after adapter/session recreation

handler delta replay compatibility:
  accepted; durable batch id and restored provider sequence preserve
  milestone 025 handler delta idempotency, duplicate replay behavior,
  conflict rejection, and ordered merge

operator adapter summary:
  accepted; adapter kind, schema, storage identity, compatibility, queue
  counts, first blocking envelope, first blocking state, and first blocking
  reason are visible

focused Release gate:
  accepted; 57 focused Release tests passed with no failures or skips

Release build:
  accepted; Release build succeeded with zero warnings and zero errors

Kafka/RabbitMQ/cloud queue/database adapter:
  not accepted; these are not automatic continuation work after milestone 026

production broker durability:
  not accepted; file-based persistence is a restart-recovery contract gate,
  not broker operations or retention certification

completed-result persistence:
  accepted with warning; scoped completed-envelope recovery recomputes
  completion material from the persisted batch for the gate

true live network ingestion:
  not implemented; deterministic archive-shaped workloads remain the gate
  input

production HTTP BFF host:
  not implemented; milestone 024 read models remain application contracts

frontend application:
  not implemented; future product milestone required

production deployment and operations:
  not implemented; deployment, rollback, autoscaling, alerts, and runbooks
  remain future work

exactly-once production delivery:
  not claimed; future storage, adapter, and downstream idempotency gates would
  be required for that claim
```

## Decision Explanations

### Accept File-Based Adapter As The Milestone Stop Point

Decision: accept deterministic local file-based persistence as the milestone
026 adapter and stop the milestone at that adapter.

Why chosen: the immediate reliability gap after milestone 025 was not a
specific broker product; it was whether RadarPulse's durable envelope state
could be persisted, restored, and used for ordered commit and handler replay
without relying on process-local memory. The file-based adapter proves that
contract with deterministic local gates and no mandatory external service.

Alternatives: implement Kafka, RabbitMQ, a cloud queue, or a database adapter
now; keep only the in-process harness; or treat the file adapter as a
temporary step that automatically rolls into broker work.

Rejected because: broker/database work brings service operations,
credentials, retention policy, cross-process delivery behavior, failure
injection, and deployment questions that belong in their own milestone. The
in-process harness alone was already accepted in milestone 023 and no longer
answers the persistence question. Automatic broker continuation would blur a
deliberate scope boundary.

Trade-offs/debt: the accepted adapter is not a production broker. Later
broker/database adapters can reuse the contract but need separate acceptance
criteria.

Review explanation: "Milestone 026 proves persisted RadarPulse state; it does
not select or certify a production broker."

### Accept Versioned Persistent Envelope Schema

Decision: accept the persistent durable envelope record schema as the
adapter-backed state boundary.

Why chosen: restart recovery needs more than an in-memory envelope snapshot.
The persisted record carries schema version, batch id, provider sequence,
attempt, state, worker id, message, timestamps, counters, owned snapshot
metadata, and serialized batch payload so the queue can restore state without
process-local identity.

Alternatives: serialize runtime objects directly, persist only metadata, or
defer schema compatibility until a production adapter exists.

Rejected because: runtime object serialization is brittle and leaks
implementation details; metadata-only persistence cannot recover scoped
processing commit; and deferring compatibility would allow silent
misinterpretation of persisted durable state.

Trade-offs/debt: the schema is intentionally local and deterministic. Future
transport/storage schemas may map it differently but must preserve the same
state semantics.

Review explanation: "The durable unit on disk is an envelope record, not a
process object."

### Accept Fail-Closed Compatibility

Decision: accept fail-closed behavior for unsupported schema versions,
corrupt persisted content, and incompatible records.

Why chosen: durable recovery must not guess when state is ambiguous. A
misread durable record can publish out of order, lose failure state, or retry
work incorrectly. The adapter summary exposes compatibility and storage
messages so operators can see the blocking condition.

Alternatives: ignore incompatible records, best-effort load partial state, or
silently reset the adapter store.

Rejected because: ignoring or resetting durable state can lose accepted work;
partial best-effort recovery can violate ordered commit; and silent recovery
would hide a readiness blocker.

Trade-offs/debt: incompatible stores block readiness until repaired or
migrated. Future schema migrations need explicit gates.

Review explanation: "Unknown durable state blocks; it does not become fresh
empty state."

### Accept Restart Recovery State Semantics

Decision: accept adapter-backed restart recovery for pending, claimed,
completed, failed, abandoned, poison, canceled, and released envelopes.

Why chosen: milestone 023 defined the state machine. Milestone 026 proves
those states survive adapter/session recreation. Pending remains claimable;
claimed remains claimed; completed remains ordered commit input; failed and
poison remain readiness blockers; abandoned can retry by bounded policy;
canceled requires cleanup; released is terminal.

Alternatives: reset non-terminal states on restart, retry claimed envelopes
automatically, or treat completed envelopes as already committed.

Rejected because: reset would lose durable progress; automatic claimed retry
can duplicate in-flight work without policy ownership; and completion is not
ordered commit.

Trade-offs/debt: operators or policy must handle stale claimed envelopes.
That is intentional visibility, not a hidden runtime shortcut.

Review explanation: "Restart preserves the envelope's truth; policy decides
what happens next."

### Accept Adapter-Backed Ordered Processing Commit

Decision: accept that adapter-backed completed envelopes can recover scoped
completion material and commit in provider sequence after restart.

Why chosen: a persistent adapter is useful only if completed work can still
reach ordered commit after adapter/session recreation. The implemented gate
recomputes scoped processing completion material from the persisted batch,
then commits only through the existing provider-sequence boundary.

Alternatives: persist full processing result material now, mark completed
envelopes committed on restart, or require all completed envelopes to rerun
through claim/retry.

Rejected because: full result persistence broadens the milestone into a
larger result-store design; marking completed as committed would violate the
worker-completion versus ordered-commit boundary; and forced retry loses the
completed-state semantics accepted in milestone 023.

Trade-offs/debt: recompute-on-recovery is accepted for deterministic scoped
processing gates. Production adapters may need stronger persisted completion
or result materialization if recompute cost or input retention becomes a
readiness issue.

Review explanation: "Completed can resume commit after restart, but it still
does not become committed until ordered commit runs."

### Accept Handler Delta Replay Compatibility

Decision: accept adapter-backed compatibility with milestone 025 handler
delta identity, duplicate replay, conflict rejection, and ordered merge.

Why chosen: milestone 025 made handler deltas replayable and idempotent.
Milestone 026 must not weaken that when durable batch id and provider
sequence are restored from persistent state. The focused tests prove stable
delta id across retry, duplicate replay without double-counting, conflicting
duplicate rejection, and out-of-order handler delta merge by restored
provider sequence.

Alternatives: leave handler delta replay out of the adapter milestone, or
defer it to a later production adapter milestone.

Rejected because: durable recovery and handler delta replay interact through
batch identity, retry, and provider sequence. Ignoring that seam would weaken
the milestone 025 contract before production pipeline work.

Trade-offs/debt: the evidence uses deterministic benchmark/test handler
surfaces. Arbitrary future plugin persistence remains a future plugin or
production adapter concern.

Review explanation: "The same durable batch produces the same handler delta
identity after restart."

### Accept Operator Adapter Summary

Decision: accept adapter summary and readiness integration as sufficient
operator visibility for the scoped adapter.

Why chosen: persistent recovery issues must be visible. The adapter summary
reports adapter kind, schema version, storage identity, compatibility status,
storage message, queue counts, first blocking batch, first blocking sequence,
first blocking state, and first blocking reason. It composes with the
existing durable runtime readiness summary.

Alternatives: expose only queue counts, keep adapter storage details private,
or wait for production API work before shaping diagnostics.

Rejected because: queue counts alone do not identify storage compatibility
or blocking state; hiding storage identity makes local recovery harder; and
waiting for API work would leave the backend readiness boundary vague.

Trade-offs/debt: the summary is still an application/backend contract. HTTP
DTOs, auth, and frontend presentation remain future work.

Review explanation: "Operators can see which adapter is blocking and why."

### Keep Broker, Live, Frontend, And Operations Deferred

Decision: keep broker/database adapters, true live ingestion, production BFF,
frontend, deployment operations, and exactly-once delivery outside milestone
026.

Why chosen: milestone 026 answers the persistent durable adapter contract for
a deterministic local file-based adapter. Pulling in live ingestion,
production APIs, frontend workflows, broker operations, and exactly-once
claims would combine several independent risk surfaces into one decision.

Alternatives: continue directly into Kafka/RabbitMQ/database work, combine
production pipeline integration with frontend, or claim production delivery
from the file adapter.

Rejected because: each surface needs its own failure model, diagnostics,
acceptance gates, and operating assumptions. File-based persistence does not
prove broker retention, cross-machine delivery, live network behavior, or
end-to-end exactly-once semantics.

Trade-offs/debt: the next project step should integrate the accepted backend
runtime surfaces into a production-shaped pipeline, not silently expand this
adapter milestone into broker selection.

Review explanation: "The file adapter closes this reliability slice; product
and operations readiness still need their own milestones."

## Included Surface

Included:

```text
versioned persistent durable envelope schema
persistent RadarEventBatch payload record for local recovery proof
IRadarProcessingPersistentDurableEnvelopeStore contract
RadarProcessingFileDurableEnvelopeStore
optional persistent backend for RadarProcessingDurableEnvelopeQueue
adapter summary with kind, schema version, storage identity, compatibility,
  storage message, and queue summary
idempotent duplicate accept after restart
persistent claim, complete, fail, abandon, retry, poison, commit, release,
  cancel, CancelOpen, and ReleaseCanceled transitions
restart recovery for pending, claimed, completed, failed, abandoned, poison,
  canceled, and released states
completed-envelope recovery hook for ordered processing commit after
  adapter/session recreation
handler delta replay/idempotency/ordered merge compatibility over restored
  durable batch id and provider sequence
durable runtime readiness summary integration
focused Release gate and Release build evidence
```

Excluded:

```text
Kafka adapter
RabbitMQ adapter
cloud queue adapter
database-backed adapter
production broker operations and retention certification
true live network ingestion
production HTTP BFF host
frontend application
deployment, rollback, autoscaling, alerts, or runbooks
cross-machine throughput certification
exactly-once end-to-end production delivery claim
full processing result store or production run history store
changing RadarProcessingCoreOptions.Default
changing the milestone 020 provider/execution baseline decision
changing the milestone 021 ordered processing delta decision
changing the milestone 022 ordered rebalance/topology decision
changing the milestone 023 durable envelope state decision
changing the milestone 024 custom handler output/BFF decision
changing the milestone 025 handler delta/merge decision
```

## Evidence

Primary source documents:

```text
docs/milestones/026-persistent-durable-adapter-readiness.md
docs/milestones/026-persistent-durable-adapter-readiness-plan.md
docs/milestones/026-persistent-durable-adapter-readiness-gate.md
```

Input evidence from earlier milestones:

```text
milestone 020:
  RadarProcessingRuntimeArchiveBaseline accepted as the named construction
  profile composing queued-owned provider defaults with async shard transport
  execution defaults

milestone 021:
  non-mutating per-batch processing delta plus provider-sequence ordered
  commit accepted as the safe architecture for overlapping processing-core
  batches

milestone 022:
  ordered rebalance/topology commit accepted for handler-free processing
  deltas, including stale topology recompute before provider-sequence
  topology mutation

milestone 023:
  broker-neutral durable envelope contract and deterministic in-process
  durable harness accepted as durable/cross-process runtime readiness

milestone 024:
  committed custom handler output, processing read models, BFF query shapes,
  diagnostics, and MVP readiness outputs accepted

milestone 025:
  handler delta identity, duplicate replay idempotency, conflicting duplicate
  rejection, and provider-sequence ordered handler merge accepted for
  explicitly mergeable handlers
```

Implementation evidence:

```text
RadarProcessingPersistentDurableEnvelopeRecord:
  carries schema version, durable identity, provider sequence, attempt, state,
  worker id, message, timestamps, counters, owned snapshot metadata, and
  serialized batch payload

RadarProcessingPersistentRadarEventBatchRecord:
  stores local serialized RadarEventBatch payloads for deterministic recovery
  gates

IRadarProcessingPersistentDurableEnvelopeStore:
  defines adapter kind, schema version, storage identity, load, save, and
  adapter summary behavior

RadarProcessingFileDurableEnvelopeStore:
  implements deterministic local file-backed persistence with fail-closed
  load behavior for incompatible or corrupt content

RadarProcessingDurableEnvelopeQueue:
  can run in memory by default or use an optional persistent store while
  preserving existing durable queue operations

RadarProcessingDurableProcessingSession:
  can recover completed adapter-backed envelopes and commit them in provider
  sequence after adapter/session recreation

RadarProcessingDurableAdapterSummary:
  exposes adapter identity, compatibility, storage message, and queue summary
  for readiness diagnostics

RadarProcessingDurableRuntimeReadinessSummary:
  composes with adapter-backed queue summaries and preserves first blocking
  envelope diagnostics
```

Verification:

```text
slice 1 persistent envelope schema suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests"
  result: 4 passed, 0 failed, 0 skipped

slice 2 persistent queue suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests"
  result: 8 passed, 0 failed, 0 skipped

slice 3 restart recovery suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests"
  result: 13 passed, 0 failed, 0 skipped

slice 4 adapter-backed processing suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests"
  result: 10 passed, 0 failed, 0 skipped

slice 5 handler delta compatibility suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests"
  result: 17 passed, 0 failed, 0 skipped

readiness summary suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
    --filter "FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
  result: 5 passed, 0 failed, 0 skipped

focused milestone 026 Release gate:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore
    --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests|FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests|FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
  result: 57 passed, 0 failed, 0 skipped

Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors
```

## Final Decision

Decision:

```text
accepted with scoped warnings for persistent durable adapter readiness over
deterministic archive-shaped MVP workloads, stopping milestone 026 at the
deterministic local file-based adapter
```

Accepted readiness answer:

```text
yes with scoped warnings, RadarPulse is ready to treat the accepted durable
envelope contract as file-adapter-backed recoverable state for deterministic
archive-shaped MVP workloads, preserving restart recovery, idempotent accept,
explicit claimed recovery policy, provider-sequence ordered commit, handler
delta replay semantics, terminal release behavior, and operator-readable
blocking diagnostics
```

Named warnings:

```text
milestone 026 stops at deterministic local file-based persistence
Kafka, RabbitMQ, cloud queue, and database-backed adapters are not included
production broker durability, broker retention, and cross-machine delivery
  are not claimed
completed-envelope recovery recomputes scoped processing completion material
  from the persisted batch for the gate
claimed envelopes remain claimed after restart until explicit policy action
true live network ingestion remains future work
production HTTP BFF host and frontend remain future work
production deployment, rollback, autoscaling, alerts, and runbooks remain
  future work
exactly-once production delivery is not claimed
```

Recommended next milestone input:

```text
production pipeline integration.

Use the accepted runtime/archive baseline, ordered processing/rebalance
commit contracts, durable file-based adapter, custom handler output/BFF read
models, and handler delta/merge semantics to connect RadarPulse into an
end-to-end operational backend pipeline with configuration defaults,
diagnostics, representative workload gates, restart/recovery validation,
rollback/fallback posture, and capacity evidence. Do not silently expand the
next milestone into Kafka/RabbitMQ/cloud queue/database adapter work unless
that adapter decision is explicitly selected first.
```

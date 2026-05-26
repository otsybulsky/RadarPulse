# Milestone 026: Persistent Durable Adapter Readiness Implementation Plan

Status: pre-decision review.

This plan implements the milestone 026 architecture defined in
`026-persistent-durable-adapter-readiness.md`.

The plan is scoped to one deterministic local persistent adapter shape for
the accepted durable envelope contract. It does not introduce a mandatory
external broker dependency and does not claim production exactly-once
delivery.

## Goal

Validate that RadarPulse durable runtime state can be adapter-backed and
recoverable across adapter/session recreation.

The milestone must prove these concrete outcomes:

```text
durable envelope persistence has an explicit schema version
persisted adapter state fails closed on incompatible schema
accepted envelopes, provider sequences, attempts, worker identity, messages,
  timestamps, and terminal states survive restart
duplicate accept remains idempotent after restart
pending envelopes remain claimable after restart
claimed envelopes remain claimed until an explicit recovery policy acts
completed envelopes can still reach ordered commit after restart
failed, abandoned, poison, canceled, and released envelopes keep their
  milestone 023 semantics after restart
ordered commit remains provider-sequence deterministic after restart
handler delta replay/idempotency remains compatible with adapter-backed
  execution
operator summaries expose adapter kind, schema, storage identity, counts,
  first blocking envelope, and first blocking reason
```

Scope note:

```text
the first concrete adapter is a deterministic local file-backed adapter. It
is a contract and restart-recovery gate, not a production broker claim.
milestone 026 stops at this file-based adapter; Kafka, RabbitMQ, cloud queue,
database-backed adapters, and production broker certification are not
automatic continuation work.
```

## Slice 1: Persistent Envelope Schema And Adapter Contract

Status: complete.

Implementation:

```text
add a durable adapter summary shape with adapter kind, schema version,
  storage identity, compatibility status, and first storage error
add a persisted envelope record shape for schema version, batch id, provider
  sequence, attempt, state, worker id, message, timestamps, counters, owned
  snapshot metadata, and serialized batch payload
add a storage-neutral local adapter contract that can load and save the full
  durable envelope record set atomically from the queue perspective
add schema compatibility validation that fails closed on unsupported versions
avoid per-event durable records in public durable operations
```

Tests:

```text
persisted records reject missing batch id and invalid state
schema version mismatch reports compatibility failure
adapter summary exposes kind, schema version, and storage identity
serialized batch payload roundtrips through the record shape
```

Exit criteria:

```text
the adapter persistence boundary is explicit and versioned before queue
operations write to it
```

Commit:

```text
Add persistent durable envelope schema
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 2: File-Backed Durable Envelope Queue

Status: complete.

Implementation:

```text
add a deterministic file-backed adapter implementation for local gates
wire the existing RadarProcessingDurableEnvelopeQueue to an optional
  persistence adapter without changing the default in-memory behavior
persist accept, claim, complete, fail, abandon, retry, poison, commit,
  release, cancel, CancelOpen, and ReleaseCanceled transitions
restore queue entries and next provider sequence from persisted records
preserve idempotent duplicate accept after restart
make released envelopes terminal and non-claimable after restart
```

Tests:

```text
accepted envelopes are restored with stable provider sequences
duplicate accept after restart returns Duplicate with the original sequence
pending envelope can be claimed after restart
released envelope remains terminal after restart
corrupt or incompatible persisted state fails closed with diagnostics
```

Exit criteria:

```text
the existing durable queue contract can run either in memory or against the
local persistent adapter while preserving milestone 023 state semantics
```

Commit:

```text
Add file backed durable envelope queue
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests"

result:
  8 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 3: Restart Recovery Transitions

Status: complete.

Implementation:

```text
prove restart behavior for pending, claimed, completed, failed, abandoned,
  poison, canceled, and released states
keep claimed envelopes claimed after restart unless explicit abandon/fail or
  recovery policy acts
allow abandoned or failed envelopes to retry through the existing bounded
  retry policy
preserve retry attempt increments across restart
preserve poison blocking state across restart
preserve cancellation cleanup and release counts across restart
surface first blocking state and reason from adapter-backed summaries
```

Tests:

```text
claimed envelope blocks after restart until explicitly abandoned
abandoned attempt can retry and increments attempt after restart
retry exhaustion marks poison after restart
completed but uncommitted envelope remains first ordered commit input
canceled envelope cleanup releases after restart
summary reports first blocking batch, sequence, state, and reason
```

Exit criteria:

```text
restart recovery is explicit, bounded, and operator-visible; no claimed
envelope is silently retried by adapter recreation
```

Commit:

```text
Add durable adapter restart recovery gates
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests"

result:
  5 passed, 0 failed, 0 skipped

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests"

result:
  13 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 4: Adapter-Backed Ordered Processing Commit

Status: complete.

Implementation:

```text
compose the file-backed durable queue with RadarProcessingDurableProcessingSession
prove out-of-order worker completion still commits only by provider sequence
  when state is adapter-backed
support completed-before-restart ordered commit by persisting or safely
  recomputing enough completion material for the scoped processing gate
preserve processing validation failure blocking behavior after restart
preserve committed and released transition persistence after ordered commit
```

Tests:

```text
out-of-order completed envelopes commit in provider sequence with the
  file-backed adapter
later completed envelope waits behind earlier claimed envelope after restart
completed envelope can be committed after adapter/session recreation
earlier validation failure blocks later publication after restart
committed/released states survive another adapter/session recreation
```

Exit criteria:

```text
adapter-backed state does not change the externally visible ordered
processing commit contract
```

Commit:

```text
Preserve ordered processing with persistent durable adapter
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests"

result:
  10 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 5: Handler Delta Replay Compatibility

Status: complete.

Implementation:

```text
add adapter-backed tests that compose durable batch ids and provider
  sequences with milestone 025 handler delta identity
prove equivalent duplicate handler delta replay remains idempotent after
  adapter-backed retry/replay
prove conflicting duplicate handler delta replay fails closed
prove out-of-order mergeable handler deltas still merge only by provider
  sequence when durable state is adapter-backed
keep merged handler output projected through milestone 024 read models
```

Tests:

```text
handler delta id remains stable for the same durable batch after restart
equivalent duplicate handler delta does not double-count output after restart
conflicting duplicate handler delta blocks merge with first blocking reason
adapter-backed ordered handler merge output matches sequential fallback for
  the deterministic benchmark handler set
```

Exit criteria:

```text
milestone 026 does not weaken milestone 025 handler delta identity,
idempotency, replay, or ordered merge behavior
```

Commit:

```text
Validate handler delta replay with persistent durable adapter
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests"

result:
  4 passed, 0 failed, 0 skipped

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests"

result:
  17 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 6: Operator Summary And Release Gate

Status: complete.

Implementation:

```text
add or extend adapter-backed readiness summary tests
capture focused Release gate over persistent adapter, durable recovery,
  durable processing, and handler delta compatibility suites
run Release build
record gate evidence in
  026-persistent-durable-adapter-readiness-gate.md
update handoff with completed implementation slices and latest verification
```

Tests:

```text
adapter summary includes adapter kind, schema, storage identity, compatibility
  status, state counts, oldest uncommitted sequence, first blocking envelope,
  and first blocking reason
focused Release suite passes
Release build succeeds with zero warnings
```

Exit criteria:

```text
all implementation evidence needed for the pre-decision review is captured,
and no decision trace has been written yet
```

Commit:

```text
Capture persistent durable adapter gate
```

Verification:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
  --filter "FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"

result:
  5 passed, 0 failed, 0 skipped

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests|FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests|FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"

result:
  57 passed, 0 failed, 0 skipped

dotnet build RadarPulse.sln -c Release --no-restore

result:
  succeeded, 0 warnings, 0 errors
```

## Slice 7: Pre-Decision Trace Review Point

Status: reached.

Implementation:

```text
stop before writing the decision trace
summarize completed slices, gate evidence, warnings, and remaining questions
ask for review of whether the adapter-backed readiness evidence is accepted,
  needs follow-up, or should change scope before decision trace
```

Exit criteria:

```text
the milestone is ready for decision trace discussion, but no decision trace
or closeout document has been created
```

Commit:

```text
none before review unless handoff-only corrections are required
```

## Gate Command Shape

Expected focused Release gate:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests|FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Full Release test project can be run if implementation touches shared runtime
behavior beyond the scoped persistent adapter path. The known allocation-
sensitive synthetic benchmark caveat from earlier milestones should remain
attributed separately if it appears again outside the focused adapter gate.

## Stop Rule

Stop before decision trace.

Do not create:

```text
026-persistent-durable-adapter-readiness-decision-trace.md
026-persistent-durable-adapter-readiness-closeout.md
```

until the pre-decision evidence and warnings have been reviewed.

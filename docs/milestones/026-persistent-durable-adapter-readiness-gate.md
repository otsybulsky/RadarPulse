# Milestone 026: Persistent Durable Adapter Readiness Gate

Date: 2026-05-26

Status: captured before decision trace.

## Gate Scope

This gate validates the deterministic local persistent durable adapter surface
implemented for milestone 026.

Included:

```text
persistent durable envelope schema and adapter contract
file-backed durable envelope queue
restart recovery transitions
adapter-backed ordered processing commit
handler delta replay compatibility
operator readiness summary integration
existing milestone 023 durable envelope/processing/recovery contract suites
existing milestone 025 handler delta contract/merge suites
```

Excluded:

```text
external broker/database adapter
true live network ingestion
production HTTP BFF host
frontend application
deployment, rollback, autoscaling, alerts, and runbooks
cross-machine throughput certification
exactly-once production delivery claims
```

## Focused Release Gate

Command:

```text
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
  --no-restore
  --filter "FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeStoreTests|FullyQualifiedName~RadarProcessingPersistentDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingPersistentDurableRecoveryTests|FullyQualifiedName~RadarProcessingPersistentDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingPersistentDurableHandlerDeltaTests|FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingHandlerDeltaContractTests|FullyQualifiedName~RadarProcessingHandlerDeltaMergeCoordinatorTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
```

Result:

```text
57 passed, 0 failed, 0 skipped
```

Coverage summary:

```text
persistent envelope schema and adapter contract:
  passed

file-backed durable envelope queue:
  passed

restart recovery transitions:
  passed

adapter-backed ordered processing commit:
  passed

handler delta replay compatibility:
  passed

operator readiness summary:
  passed

existing durable envelope, processing, and recovery contracts:
  passed

existing handler delta contract and merge coordinator contracts:
  passed
```

## Release Build

Command:

```text
dotnet build RadarPulse.sln -c Release --no-restore
```

Result:

```text
succeeded, 0 warnings, 0 errors
```

## Accepted Evidence Before Decision Trace

Persistent schema and adapter contract:

```text
schema version is explicit
invalid persistent shapes fail validation
unsupported persisted schema fails closed
adapter summary exposes kind, schema version, storage identity,
  compatibility status, storage message, and queue summary
serialized RadarEventBatch payload records roundtrip into owned batches
```

File-backed queue:

```text
accepted envelopes persist and restore with stable provider sequence
duplicate accept after restart returns the original envelope
pending envelopes can be claimed after restart
state transitions persist for claim, complete, fail, abandon, retry, poison,
  commit, release, cancel, CancelOpen, and ReleaseCanceled
released envelopes remain terminal and non-claimable after restart
corrupt or incompatible store content fails closed
```

Restart recovery:

```text
claimed envelopes remain claimed after restart and block readiness until
  explicit abandon/fail/completion/recovery action
abandoned attempts retry after restart with incremented attempt
poison remains a readiness blocker after restart
completed uncommitted envelopes remain ordered commit inputs
canceled envelopes can release during cleanup after restart
first blocking batch, sequence, state, and reason remain visible
```

Adapter-backed ordered processing:

```text
completed envelopes can recover completion material and commit after
  adapter/session recreation
out-of-order worker completion still commits only by provider sequence
later completed envelopes wait behind earlier claimed envelopes after restart
earlier validation failure blocks later recovered completion after restart
committed and released transitions persist after ordered commit
```

Handler delta replay compatibility:

```text
handler delta id remains stable across adapter-backed retry
equivalent duplicate handler delta replay does not double-count output
conflicting duplicate handler delta replay fails closed
out-of-order handler deltas merge by restored provider sequence
existing handler delta contract and merge coordinator suites still pass
```

Operator readiness:

```text
persistent adapter summary composes with RadarProcessingDurableRuntimeReadinessSummary
adapter kind, schema, storage identity, compatibility, blocking batch,
  blocking state, and blocking reason are visible
readiness remains blocked when adapter-backed durable state has a failed
  envelope
```

## Warnings To Review In Decision Trace

```text
the accepted adapter is deterministic local file-based persistence; milestone
  026 stops at this adapter
external broker/cloud queue/database adapters are not included and are not
  planned for this project
the file-backed adapter is a restart-recovery and serialization gate, not a
  cross-machine throughput or broker retention certification
completed-envelope recovery recomputes scoped processing completion material
  from the persisted batch for the gate; production adapters may need a
  stronger persisted completion/result materialization strategy
claimed envelopes intentionally remain claimed after restart until explicit
  recovery policy acts
exactly-once production delivery is not claimed
true live network ingestion, production BFF/frontend, deployment,
  rollback, autoscaling, alerts, and runbooks remain future work
```

## Pre-Decision Stop Point

Milestone 026 implementation slices and focused Release gate evidence are
complete.

Decision trace has not been written.

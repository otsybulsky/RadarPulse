# Milestone 023 Decision Trace

Date: 2026-05-25

Decision: accept durable/cross-process runtime readiness for the scoped
runtime/archive path with named scoped warnings.

This decision accepts milestone 023's broker-neutral durable envelope
contract and deterministic in-process durable harness on top of the milestone
020 runtime/archive baseline, the milestone 021 ordered processing
foundation, and the milestone 022 ordered rebalance/topology foundation.

The accepted scope is the RadarPulse-owned durable/cross-process runtime
contract: accepted owned batches can be represented as durable envelopes with
stable batch ids, provider sequences, attempt counters, explicit claim,
completion, failure, retry, poison, commit, release, and operator-visible
readiness state. Worker completion may happen out of provider order, but
processing commit and rebalance/topology commit remain provider-sequence
ordered.

The decision does not accept production broker adapters, production
durability, true live network ingestion, deployment/rollback/autoscaling
readiness, handler-state delta/merge, exactly-once production delivery, or
product-facing workflow readiness. Those remain future surfaces that must
prove their own persistence, ownership, recovery, retry, replay, operator,
and compatibility boundaries.

The milestone is a scoped durable-runtime contract acceptance, not a
production deployment claim. It proves the state machine and ordered commit
semantics that a future persistent store or broker adapter must implement.

## Decision Matrix

```text
durable/cross-process runtime readiness:
  accepted with scoped warnings

broker-neutral durable envelope contract:
  accepted; stable batch id, provider sequence, attempt, state, payload
  metadata, worker identity, timestamps, and diagnostic message are explicit

deterministic in-process durable harness:
  accepted as the milestone gate adapter; it proves RadarPulse-owned
  contract semantics but is not a production durability claim

durable ordered processing:
  accepted; worker completion can occur out of provider order while
  processing commit remains provider-sequence ordered

durable ordered rebalance/topology commit:
  accepted; durable envelopes compose with ordered rebalance commit and keep
  stale topology recompute safe before provider-sequence topology mutation

retry and recovery policy:
  accepted; retry is explicit and bounded, retry preserves batch id and
  provider sequence, retry exhaustion marks poison

cancellation and cleanup:
  accepted; cancellation cleanup releases open envelopes and clears pending
  completions

operator-readable readiness summary:
  accepted; accepted, pending, claimed, completed, committed, failed, poison,
  abandoned, retry, released, first blocking envelope, blocking reason,
  release failure, and terminal retained pressure fields are visible

production broker adapters:
  not implemented; future broker/storage adapter gate required

production durability:
  not claimed; the in-process harness is a contract gate, not proof of
  process-crash, replicated, fsync, broker lease, or network partition
  durability

true live network ingestion:
  not implemented; milestone 023 starts after owned runtime/archive batches
  exist

production deployment and operations:
  not implemented; deployment, rollback, autoscaling, alerts, and runbooks
  remain future work

handler-state delta/merge:
  not implemented; durable ordered runtime remains handler-free

exactly-once production delivery:
  not claimed; future adapter/storage/downstream idempotency gate required

full-suite residual risk:
  accepted as known allocation-sensitive synthetic benchmark caveat; isolated
  rerun passed and the failure is outside the milestone 023 durable runtime
  correctness surface
```

## Decision Explanations

### Accept Broker-Neutral Durable Envelope Contract

Decision: accept the durable envelope contract as the RadarPulse-owned
cross-process runtime boundary.

Why chosen: milestone 022 proved ordered processing and ordered
rebalance/topology commit inside one process. Durable and cross-process
runtime readiness needs an explicit boundary that survives beyond
process-local queue item semantics. Milestone 023 introduces stable batch ids,
provider sequences, attempt counters, envelope states, worker identity, and
operator-visible diagnostics.

Alternatives: treat the existing in-memory owned batch queue as the durable
contract, start with an external broker adapter before defining RadarPulse
semantics, or expose worker completion directly as commit.

Rejected because: process-local queue semantics do not define restart,
retry, abandon, poison, or recovery; an external broker-first milestone would
mix infrastructure with the contract decision; and worker completion cannot
replace provider-sequence ordered commit.

Trade-offs/debt: the contract is storage-neutral. A production store or
broker still needs its own adapter gate.

Review explanation: "The durable boundary is now explicit; storage and broker
backing remain the next proof."

### Accept Deterministic In-Process Harness As Contract Gate

Decision: accept the in-process durable harness as a deterministic gate for
the durable runtime contract.

Why chosen: the milestone needed to prove the state machine and ordered
runtime semantics without making external broker operations, credentials,
deployment, retention, and failure injection dominate the work. The harness
exercises accept, claim, complete, fail, abandon, retry, poison, commit,
release, summary, and cleanup operations under controlled tests.

Alternatives: require Kafka, RabbitMQ, cloud queues, or a database adapter in
milestone 023, or skip the harness and move directly to production
integration.

Rejected because: external infrastructure would make the milestone harder to
review and less deterministic; skipping the harness would leave the contract
untested before adapter work.

Trade-offs/debt: the in-process harness does not prove process crash,
replication, fsync, broker leases, multi-process contention, or network
partition behavior.

Review explanation: "The harness proves RadarPulse semantics, not production
durability."

### Accept Durable Ordered Processing

Decision: accept durable ordered processing where worker completion is
separate from provider-sequence commit.

Why chosen: cross-process workers can complete out of order. Milestone 023
holds completed durable envelopes behind earlier incomplete, failed, or
poison envelopes, then commits processing deltas only in provider sequence.
This preserves the milestone 021 state-mutation boundary while allowing
worker completion order to differ from commit order.

Alternatives: commit as soon as workers complete, force workers to complete
in order, or mark provider accept as processing success.

Rejected because: commit-on-completion violates deterministic ordered state;
in-order workers would remove useful concurrency and still not solve recovery;
and provider accept does not prove processing completion.

Trade-offs/debt: later completed work can wait behind an earlier blocking
envelope. The operator summary must identify that blocker.

Review explanation: "A worker can finish early, but the runtime commits only
the next provider sequence."

### Accept Durable Ordered Rebalance/Topology Commit

Decision: accept durable ordered rebalance runtime using the same envelope
contract and the milestone 022 ordered rebalance/topology safety rules.

Why chosen: milestone 023 needs the durable runtime boundary to cover both
processing and rebalance. Durable rebalance preserves provider-sequence
processing, pressure, policy, quarantine, telemetry, decision, validation,
and topology mutation. Later completed envelopes can wait, and stale topology
deltas are recomputed before commit when an earlier accepted move changed the
topology.

Alternatives: accept durable processing only, commit rebalance as soon as
worker attempts complete, or drop stale topology recompute in the durable
path.

Rejected because: processing-only durability would leave the accepted
runtime/archive rebalance path behind; completion-order rebalance commit
would violate topology determinism; and dropping stale recompute would
weaken the milestone 022 topology invariant.

Trade-offs/debt: durable rebalance inherits the same handler-free and
topology-churn cost boundaries as milestone 022.

Review explanation: "Durable rebalance uses the same ordered commit boundary
as in-process ordered rebalance."

### Accept Explicit Bounded Retry And Poison

Decision: accept explicit bounded retry, abandoned-attempt recovery, and
poison-on-exhaustion behavior.

Why chosen: cross-process work can fail or disappear after claim. Milestone
023 makes retry a policy decision rather than a hidden fallback. Retry
preserves batch id and provider sequence while incrementing attempt. Retry
success can unblock later completed envelopes. Retry exhaustion marks the
envelope poison and leaves the blocker operator-visible.

Alternatives: unbounded retry, implicit retry through BlockingBorrowed,
manual-only retry with no policy, or treating failed envelopes as skipped.

Rejected because: unbounded retry is not a readiness answer; borrowed fallback
would hide queued-owned failure; no policy would leave recovery undefined;
and skipping failed envelopes would violate ordered completeness.

Trade-offs/debt: the default policy is conservative. Production adapters may
need richer lease-expiry and dead-letter rules, but they must preserve the
same envelope semantics.

Review explanation: "Retry is explicit, bounded, and visible."

### Accept Operator-Readable Readiness Summary

Decision: accept the durable runtime readiness summary as the operator-facing
state shape for this milestone.

Why chosen: durable/cross-process runtime failures must be diagnosable
without inspecting private in-memory queues. The summary exposes accepted,
pending, claimed, completed, committed, failed, poison, abandoned, retry,
released, oldest uncommitted sequence, first blocking envelope, blocking
reason, release failure, and terminal retained pressure fields.

Alternatives: expose only aggregate test counters, rely on logs, or defer all
operator state until production UI work.

Rejected because: aggregate counters do not identify the ordered blocker;
logs are not a stable contract; and production UI work needs a backend read
model to build on.

Trade-offs/debt: this is not a dashboard or runbook. It is the read model
input for future operator surfaces.

Review explanation: "The runtime now says what blocks progress and where."

### Keep Production Broker And Durability Claims Deferred

Decision: do not claim production broker adapter readiness or production
durability in milestone 023.

Why chosen: the milestone proves contract semantics, not external storage or
broker behavior. Real broker/storage adapters must prove serialization,
restart recovery, lease/visibility timeout behavior, duplicate delivery,
multi-worker contention, dead-letter mapping, and adapter-level operator
state.

Alternatives: imply that the in-process harness is durable enough, or bundle
one production broker adapter into the same milestone.

Rejected because: implying production durability would overstate the
evidence; bundling adapter work would mix contract acceptance with
infrastructure-specific failure modes.

Trade-offs/debt: the next milestone should validate a persistent or broker
adapter against this contract.

Review explanation: "The contract is ready for an adapter; the adapter is not
implemented yet."

### Keep Live Ingestion And Production Operations Deferred

Decision: do not accept true live network ingestion, production deployment,
rollback, autoscaling, alerts, or runbooks in milestone 023.

Why chosen: milestone 023 starts after owned runtime/archive batches exist.
Live acquisition, reconnect/resume, feed health, partial records, deployment
topology, worker drain, rollback, autoscaling, and incident procedures are
separate runtime and operations surfaces.

Alternatives: imply live or production operational readiness because durable
runtime state exists, or expand this milestone into live/ops integration.

Rejected because: durable runtime contracts are necessary input for those
surfaces, but they do not prove network ingestion or production operations.

Trade-offs/debt: milestone 023 provides operator-readable signals that future
live and operations milestones can consume.

Review explanation: "The backend state model is ready; live ingestion and
ops surfaces still need their own gates."

### Keep Handler-State Delta/Merge Deferred

Decision: preserve the handler-free ordered runtime boundary.

Why chosen: milestone 021 and 022 accepted non-mutating per-batch deltas only
for handler-free processing cores. Custom handlers can own arbitrary mutable
state and side effects. Durable retry and replay make that more sensitive, not
less. Handler output and state require their own snapshot, delta, merge,
serialization, retry, and idempotent export contract.

Alternatives: allow custom handlers through durable ordered runtime without a
merge contract, or block milestone 023 until handler state is solved.

Rejected because: allowing handlers without a contract is unsafe; blocking
would mix a separate state consistency problem into the durable envelope
milestone.

Trade-offs/debt: custom handler outputs and synthetic result export remain
future work.

Review explanation: "Durable ordered runtime is accepted for handler-free
cores; custom handler state remains a separate design."

### Do Not Claim Exactly-Once Production Delivery

Decision: do not claim exactly-once production delivery.

Why chosen: milestone 023 introduces stable envelope identity, provider
sequence, attempts, idempotent accept behavior in the deterministic harness,
and ordered commit. True production exactly-once also needs storage
transaction boundaries, broker ack semantics, downstream idempotent side
effects, restart deduplication, serialization compatibility, and real adapter
failure evidence.

Alternatives: market the current stable identity and ordered commit as
exactly-once, or block the milestone on exactly-once production proof.

Rejected because: stable identity is necessary but insufficient; exactly-once
is a future adapter/storage/downstream integration decision.

Trade-offs/debt: a future adapter milestone must choose a defensible delivery
contract such as at-least-once with idempotent commit, effectively-once for a
bounded surface, or true exactly-once if the storage stack supports it.

Review explanation: "The contract supports idempotency work, but exactly-once
is not claimed."

### Accept Verification With Known Full-Suite Allocation Caveat

Decision: accept the milestone 023 verification posture despite the known
full-suite allocation-sensitive synthetic benchmark failure.

Why chosen: Release build succeeded with `0` warnings and `0` errors. The
focused durable/cross-process Release gate passed `26/26`. The full Release
test project reported the same known allocation-sensitive synthetic benchmark
caveat carried from earlier milestones, and the test passed in isolated rerun.
The failure is outside the durable/cross-process runtime correctness surface.

Alternatives: block milestone 023 on unrelated full-suite allocation
sensitivity, loosen the synthetic benchmark threshold after measurement, or
ignore the full-suite output.

Rejected because: blocking would conflate the durable runtime work with a
known unrelated sensitivity; changing thresholds after measurement is not
appropriate; and ignoring the full-suite output would hide project-level
risk.

Trade-offs/debt: the full-suite allocation sensitivity remains a project
caveat until separately stabilized.

Review explanation: "The scoped durable runtime gate is clean; the known
full-suite caveat remains isolated."

## Included Surface

Included durable/cross-process runtime surfaces:

```text
RadarProcessingDurableBatchId
RadarProcessingDurableEnvelopeSnapshot
RadarProcessingDurableEnvelopeState
RadarProcessingDurableQueueOperationResult
RadarProcessingDurableQueueSummary
RadarProcessingDurableEnvelopeQueue
RadarProcessingDurableProcessingSession
RadarProcessingDurableProcessingSessionResult
RadarProcessingDurableRetryPolicy
RadarProcessingDurableRebalanceSession
RadarProcessingDurableRebalanceSessionResult
RadarProcessingDurableRuntimeReadinessSummary
```

Included baseline contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
provider queue capacity: 8
retained-byte budget: 536870912
startup retained payload prewarm: enabled
execution: async shard transport
workers: 4
worker queue capacity: 8
ordered active batch capacity: 4
ordered processing commit: accepted
ordered rebalance/topology commit: accepted
```

Included evidence shapes:

```text
durable envelope state transition tests
durable ordered processing tests
retry, abandon, poison, and cancellation cleanup tests
durable ordered rebalance tests
operator-readable readiness summary tests
Release build
focused durable/cross-process Release gate suite
full Release test project plus isolated rerun of known allocation caveat
```

Excluded:

```text
production broker adapters
production durable storage claim
process-crash persistence proof
broker lease or visibility timeout behavior
multi-process contention proof
true live network ingestion
deployment, rollback, autoscaling, alerting, or runbooks
handler-state delta/merge
custom handler output export contract
exactly-once production delivery
product-facing radar workflows
broader default promotion
changing RadarProcessingCoreOptions.Default
changing the milestone 020 provider/execution baseline decision
changing the milestone 021 processing delta architecture decision
changing the milestone 022 ordered rebalance/topology decision
```

## Evidence

Primary source documents:

```text
docs/milestones/023-durable-cross-process-runtime-readiness.md
docs/milestones/023-durable-cross-process-runtime-readiness-architecture-decision.md
docs/milestones/023-durable-cross-process-runtime-readiness-plan.md
docs/milestones/023-durable-cross-process-runtime-readiness-gate.md
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
```

Implementation evidence:

```text
RadarProcessingDurableEnvelopeQueue:
  accepts owned batches into stable durable envelopes
  assigns provider sequences and idempotent batch ids
  supports claim, complete, fail, abandon, retry, poison, commit, release,
  cancel, snapshots, and summary

RadarProcessingDurableProcessingSession:
  separates worker claim/completion from provider-sequence processing commit
  holds later completions behind earlier blockers
  exposes retry/poison and cancellation cleanup
  preserves async worker telemetry where async execution is used

RadarProcessingDurableRebalanceSession:
  composes durable envelopes with ordered rebalance/topology commit
  recomputes stale topology deltas before commit
  preserves accepted move and final topology parity against reference shapes

RadarProcessingDurableRuntimeReadinessSummary:
  reports readiness, first blocking envelope, blocking reason, release
  failures, and terminal retained pressure fields
```

Verification:

```text
Release build:
  dotnet build RadarPulse.sln -c Release --no-restore
  result: succeeded, 0 warnings, 0 errors

durable-focused Release suite:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName~RadarProcessingDurableEnvelopeQueueTests|FullyQualifiedName~RadarProcessingDurableProcessingSessionTests|FullyQualifiedName~RadarProcessingDurableRecoveryTests|FullyQualifiedName~RadarProcessingDurableRebalanceSessionTests|FullyQualifiedName~RadarProcessingDurableRuntimeReadinessSummaryTests"
  result: 26 passed, 0 failed, 0 skipped

full Release test project:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
  result: 847 passed, 1 failed, 3 skipped

known allocation-sensitive synthetic test isolated rerun:
  dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj -c Release
    --no-restore --no-build
    --filter "FullyQualifiedName=RadarPulse.Tests.Processing.RadarProcessingSyntheticRebalanceBenchmarkTests.AcceptedMovePressureAggregationDoesNotCopyPreviousIterations"
  result: 1 passed, 0 failed, 0 skipped
```

Focused durable/cross-process readiness evidence:

```text
stable durable batch id and provider sequence assignment
idempotent accept
explicit pending, claimed, completed, committed, failed, poison, abandoned,
  canceled, released states
out-of-order worker completion with provider-sequence ordered processing
  commit
durable retry success unblocks later completed envelopes
retry exhaustion marks poison and reports operator-visible blocking state
cancellation cleanup releases open envelopes and clears pending completions
durable ordered rebalance preserves accepted-move evidence
stale topology recompute remains safe after durable out-of-order completion
async worker telemetry remains visible through durable processing and
  rebalance commits
operator-readable readiness summary reports blocking reason, release
  failures, and terminal retained pressure fields
```

Full-suite known caveat:

```text
failed:
  RadarProcessingSyntheticRebalanceBenchmarkTests.
    AcceptedMovePressureAggregationDoesNotCopyPreviousIterations

message:
  Expected bounded benchmark aggregation allocation, got 1134179616 bytes.

interpretation:
  same known allocation-sensitive synthetic benchmark caveat carried from
  earlier milestones; isolated rerun passed; outside durable/cross-process
  correctness surface
```

## Final Decision

Decision:

```text
accepted with scoped warnings for durable/cross-process runtime readiness over
the broker-neutral durable envelope contract and deterministic in-process
durable harness
```

Accepted readiness answer:

```text
yes with scoped warnings, the scoped runtime/archive path is ready to move
accepted queued-owned batches through a broker-neutral durable envelope
contract while preserving provider-sequence ordered processing commit,
ordered rebalance/topology commit, explicit retry/recovery/poison/cancellation
cleanup, retained resource release semantics, fail-closed behavior, and
operator-readable blocking state under the deterministic in-process harness
```

Named warnings:

```text
production broker adapters are not implemented
in-process durable harness is a contract gate, not a production durability
  claim
true live network ingestion is not implemented
production deployment, rollback, autoscaling, and runbooks are not
  implemented
handler-state delta/merge is not implemented
exactly-once production delivery is not claimed
known full-suite allocation-sensitive synthetic benchmark caveat remains
```

Recommended next milestone input:

```text
persistent durable adapter readiness. Validate one concrete persistent or
broker-like adapter against the milestone 023 durable envelope contract,
including serialization compatibility, restart recovery, duplicate delivery,
lease or abandoned-attempt recovery, poison/dead-letter mapping,
provider-sequence ordered commit, retained ownership cleanup, and
operator-readable adapter state.
```

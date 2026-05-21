# Milestone 014: Direct Archive Rebalance API Default Migration Architecture

Status: draft.

RadarPulse milestone 014 starts from the closed milestone 013 post-rollout
hardening result. Milestone 013 kept the scoped
`processing benchmark rebalance-archive` CLI omitted-provider default as
`queued-owned + pooled-copy + producer-consumer`, accepted the KTLX 2026-05-05
allocation warning as a visible migration cost, and recommended direct archive
rebalance API default migration as the next milestone input.

This document is intentionally not an implementation plan. It records the
direct API default-migration concept, compatibility posture, fallback/oracle
model, allocation-risk handling, gate posture, documentation requirements, and
expected closeout decision before any task breakdown is written.

Milestone 013 deliberately did not change direct
`RadarProcessingArchiveRebalanceBenchmark.MeasureFile()` or `MeasureCache()`
defaults. Those direct methods still default to `blocking-borrowed` in the
current code, while explicit queued-owned rollout options are available for
both file and cache measurement.

The core decision is:

```text
013 proved the scoped CLI rollout is stable enough to broaden from.
014 decides whether the same rollout contour becomes the direct
    MeasureFile()/MeasureCache() default.
```

Milestone 014 should not broaden into live ingestion, durable transport,
cross-process workers, builder-transfer, ordered concurrent rebalance, or
non-benchmark archive APIs. It should perform one focused compatibility
migration for the direct archive rebalance benchmark API and keep the same
borrowed oracle discipline that made milestones 012 and 013 reviewable.

## Milestone Goal

Milestone 014 should turn the milestone 013 recommendation into a deliberate
direct API default migration.

The output of the milestone is the architectural definition of:

```text
direct MeasureFile()/MeasureCache() default contour
compatibility posture for direct callers that require blocking-borrowed
same-run blocking-borrowed oracle requirements after direct default migration
direct API default-drift guardrails
direct file/cache regression contracts for correctness and telemetry
Release gate evidence for direct default rows against explicit borrowed rows
KTLX 2026-05-05 allocation warning handling during migration
operator and documentation cleanup after direct defaults change
decision trace that records whether direct default migration is accepted
```

The resulting design must preserve these closed contracts:

```text
RadarEventBatch remains the processing input.
Leased payload storage is valid only during the synchronous publish callback.
Only owned or retained-owned input may enter the provider queue.
Retained resources release only after final use.
Provider enqueue success remains distinct from processing completion.
Queued batches drain in provider sequence order.
One rebalance-enabled batch is processed and committed at a time.
Queued batches capture topology at processing time, not enqueue time.
Accepted topology changes publish only after successful processing.
Failed processing prevents later success claims.
Controlled consumer delay remains mechanics-only proof.
Builder-transfer remains unsupported.
Blocking-borrowed remains explicitly selectable.
Same-run blocking-borrowed remains the benchmark oracle.
Queued-owned failures fail closed.
No automatic borrowed fallback follows queued-owned failure.
```

The key milestone boundary is:

```text
safe in 014:
  change direct MeasureFile()/MeasureCache() omitted provider/execution/
    queue/retention options to the accepted rollout contour
  preserve explicit blocking-borrowed direct calls as fallback and oracle
  keep same-run direct borrowed rows in the migration gate
  repeat KTLX 2026-05-05 and keep the 1.1005x allocation warning visible
  prove direct file and direct cache defaults resolve to the same contour as
    the scoped CLI rollout
  update help/docs/tests that previously said direct defaults remain borrowed
  record a decision trace with the final direct API default posture

not safe in 014 unless explicitly reprioritized:
  changing live ingestion/runtime provider defaults
  changing synthetic processing benchmark defaults
  changing non-benchmark archive publishing API defaults
  adding durable broker integration or cross-process transport
  adding ordered concurrent rebalance commit semantics
  implementing builder-transfer retained payload execution
  silently falling back from queued-owned failure to borrowed success
  tuning the rollout contour to make a gate pass without recording a new
    rollout decision
  hiding the KTLX 2026-05-05 allocation warning behind an aggregate pass
```

## Expected Outcome

At the end of milestone 014, RadarPulse should have a clear answer to this
question:

```text
Should direct RadarProcessingArchiveRebalanceBenchmark.MeasureFile() and
MeasureCache() calls that omit provider-related options now default to the
milestone 012/013 queued-owned rollout contour?
```

The acceptable outcomes are:

```text
migrate:
  direct MeasureFile()/MeasureCache() omitted options resolve to queued-owned
  rollout defaults, explicit blocking-borrowed remains available, direct
  same-run gates pass or accepted warnings are recorded, and docs/handoff
  name the new direct API posture

defer:
  direct defaults remain blocking-borrowed, the decision trace names the
  blocker, and the scoped CLI default remains unchanged

partial defer:
  one direct surface, file or cache, is not ready; the decision trace records
  why the migration did not apply uniformly and what must be fixed next
```

The expected result, if the migration is accepted, is:

```text
direct MeasureFile() without provider/execution/queue/retention arguments
  uses the accepted queued-owned rollout contour

direct MeasureCache() without provider/execution/queue/retention arguments
  uses the accepted queued-owned rollout contour

explicit providerMode: BlockingBorrowed remains the direct fallback/oracle

explicit providerMode: QueuedOwned remains supported and produces the same
  effective contour when the rollout options are supplied explicitly

same-run borrowed rows remain required for direct API migration gates

direct and CLI rollout contours stay equivalent for the shared benchmark
  surface, while CLI provenance still identifies rollout-default expansion

queued-owned failure, validation failure, cancellation, retention failure, or
  release failure does not produce automatic borrowed success
```

The core idea is:

```text
012 changed the scoped CLI default.
013 proved that default is stable enough to broaden.
014 applies the same default to direct archive rebalance benchmark calls.
```

## Starting Position

Milestone 013 closed this scoped CLI default posture:

```text
surface:
  processing benchmark rebalance-archive CLI omitted-provider path

effective contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async
  worker count: 4
  provider queue capacity: 8
  retained-byte budget: 536870912
  queue telemetry: summary
  overlap telemetry: summary
  overlap consumer delay: 0
  provider source: rollout-default
```

Current direct API posture before milestone 014 implementation:

```text
RadarProcessingArchiveRebalanceBenchmark.MeasureFile() default:
  provider mode: blocking-borrowed
  provider overlap: none
  retention strategy: snapshot-copy
  execution: partitioned barrier
  queue capacity: 1 parameter value, reported as 0 for borrowed result
  retained-byte budget: none
  queue telemetry: not applicable
  overlap telemetry: not applicable

RadarProcessingArchiveRebalanceBenchmark.MeasureCache() default:
  provider mode: blocking-borrowed
  provider overlap: none
  retention strategy: snapshot-copy
  execution: partitioned barrier
  queue capacity: 1 parameter value, reported as 0 for borrowed result
  retained-byte budget: none
  queue telemetry: not applicable
  overlap telemetry: not applicable
```

Current direct explicit rollout posture:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async
async worker count: 4
async worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
```

Milestone 013 gate facts carried forward:

```text
primary KTLX 2026-05-04:
  elapsed ratio: 0.911x borrowed
  allocation ratio: 1.071x borrowed
  candidate spread: 5.41%

KINX 2026-05-04:
  elapsed ratio: 0.939x borrowed
  allocation ratio: 1.070x borrowed

KTLX 2026-05-05:
  elapsed ratio: 0.943x borrowed average
  allocation ratio: 1.1005x borrowed average
  run 1 allocation ratio: 1.101x borrowed
  run 2 allocation ratio: 1.0996x borrowed

mixed cache:
  elapsed ratio: 0.907x borrowed
  allocation ratio: 1.062x borrowed

max observed combined retained payload high-water:
  54_413_280 bytes of the 536_870_912 byte budget

release failures:
  0

retained pressure at completion:
  current pending, active, and combined counts/bytes returned to 0
```

The KTLX 2026-05-05 allocation warning is accepted as a migration cost, not as
a rollback reason and not as a hidden pass.

## Architectural Principles

Milestone 014 should follow these principles:

```text
direct API migration is a compatibility decision, not a runtime expansion
the accepted milestone 012/013 contour is the migration subject
blocking-borrowed remains explicit fallback and same-run oracle
direct file and cache defaults should migrate together unless evidence blocks
one default contour should be shared conceptually by CLI and direct API
correctness, cleanup, release health, and fail-closed behavior remain
  non-negotiable
allocation overhead is acceptable only when carried as visible gate evidence
operator help and docs must stop saying direct defaults remain borrowed after
  the migration is accepted
controlled consumer delay remains separate from natural migration evidence
live ingestion and durable transport require separate future milestones
```

The milestone should separate these concerns:

```text
direct default migration:
  whether omitted direct API provider options now select queued-owned

fallback compatibility:
  whether direct callers can still request blocking-borrowed explicitly

oracle coverage:
  whether borrowed rows remain easy enough to run in the same gate

default drift:
  whether direct API, CLI rollout constants, and documentation stay aligned

allocation risk:
  whether the KTLX 2026-05-05 warning remains acceptable after direct migration

future runtime readiness:
  whether the next milestone can consider a broader benchmark or runtime
  expansion, or must address a named blocker first
```

## Direct API Default Model

The direct default model is the source-level behavior of calls that omit
provider-related direct benchmark arguments.

The target direct default contour is:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution mode: async shard transport
async worker count: 4
async worker queue capacity: 8
provider queue capacity: 8
queue timeout: none
retained-byte budget: 536870912
queue telemetry: summary where result telemetry is available
overlap telemetry: summary where result telemetry is available
overlap consumer delay: 0
```

The migration is not just changing `providerMode` from `BlockingBorrowed` to
`QueuedOwned`. The entire direct contour must move together. A direct default
that changes provider mode but keeps partitioned execution, snapshot-copy
retention, no producer-consumer overlap, or a queue capacity of 1 would not be
the accepted rollout contour.

Direct default calls should resolve as:

```text
new RadarProcessingArchiveRebalanceBenchmark().MeasureFile(
    filePath,
    mode,
    iterations,
    warmupIterations,
    partitionCount,
    shardCount,
    degreeOfParallelism,
    cancellationToken)

-> queued-owned rollout contour
```

```text
new RadarProcessingArchiveRebalanceBenchmark().MeasureCache(
    cachePath,
    date,
    radarId,
    maxFiles,
    mode,
    iterations,
    warmupIterations,
    partitionCount,
    shardCount,
    degreeOfParallelism,
    cancellationToken)

-> queued-owned rollout contour
```

Explicit fallback calls should remain possible:

```text
providerMode: RadarProcessingArchiveProviderMode.BlockingBorrowed
```

When a gate needs provider-only comparison, the borrowed oracle should be made
explicit enough to avoid accidental contour drift. The preferred comparison
shape is:

```text
provider mode: blocking-borrowed
execution mode: async shard transport
async worker count: 4
same mode, partition count, shard count, parallelism, iterations, and input
```

If a caller needs the old partitioned-barrier borrowed behavior, it should be
selected explicitly. Milestone 014 should not make old borrowed behavior
disappear; it should stop being the omitted direct default.

## Optional Parameter Compatibility

`MeasureFile()` and `MeasureCache()` currently expose direct defaults through
C# optional parameters. That matters for compatibility:

```text
source callers recompiled after the migration observe the new default values
already-compiled external assemblies may keep old optional argument constants
explicit argument values remain explicit and should continue to be honored
```

Milestone 014 targets the repository's direct benchmark API behavior for
source-level callers and tests compiled with the current code. It should not
pretend that changing optional parameter defaults is a binary compatibility
mechanism for already-compiled external assemblies.

If RadarPulse later treats this API as a stable binary package boundary, a
future milestone should consider an options object or overload-based default
resolver. That is not required for the current internal benchmark migration,
but the decision trace should not overstate binary compatibility.

## Shared Default Constants

Milestone 012 and 013 made the scoped CLI rollout contour visible through CLI
option parsing and output provenance. Milestone 014 should avoid creating a
second, drifting copy of the same policy.

The architecture target is one conceptual contour:

```text
queued-owned rollout contour:
  provider mode: queued-owned
  provider overlap: producer-consumer
  retention strategy: pooled-copy
  execution: async
  workers: 4
  queue capacity: 8
  retained-byte budget: 536870912
  overlap consumer delay: 0
```

The implementation may choose constants, a resolver, or another local helper,
but tests must make drift visible across:

```text
CLI omitted-provider rebalance-archive defaults
direct MeasureFile() omitted provider defaults
direct MeasureCache() omitted provider defaults
explicit queued-owned rollout contour calls
explicit blocking-borrowed fallback calls
operator help text and milestone documentation
```

The direct API does not have the same option-provenance model as the CLI.
Therefore the result object fields and tests become the source of truth for
direct default behavior:

```text
ProviderMode
ProviderOverlapMode
RetentionStrategy
ExecutionMode
WorkerTelemetry
QueueCapacity
QueueRetainedPayloadBytes
QueueTelemetry
RetentionTelemetry
OverlapTelemetry
retained pressure summary
allocation summary
```

## Fallback And Oracle Model

Blocking-borrowed keeps two roles after the migration:

```text
fallback:
  direct callers can still request the old provider explicitly

oracle:
  same-run borrowed rows remain the comparison baseline for correctness,
  allocation, elapsed time, pressure, and provenance
```

Fallback is not automatic recovery. If queued-owned fails, the default result
should fail closed and report the queued-owned failure. A borrowed row may be
run separately by the caller or gate harness, but it must not overwrite a
failed default row.

The direct migration gate should keep these comparisons:

```text
direct default queued-owned row
direct explicit blocking-borrowed oracle row
direct explicit queued-owned rollout row where useful for drift checks
CLI omitted-provider rollout row where useful for contour equivalence
```

The decision trace should state whether borrowed remains:

```text
available
documented
tested
used in the Release gate
not used as automatic fallback
```

## File And Cache Symmetry

Milestone 014 should treat `MeasureFile()` and `MeasureCache()` as one direct
API migration unless evidence says otherwise.

The file surface proves:

```text
single archive file behavior
direct default contour
explicit borrowed fallback
explicit queued-owned equivalence
validation checksum parity
retained cleanup and release behavior for one-file sessions
```

The cache surface proves:

```text
multi-file aggregation behavior
shared queue/overlap telemetry across files
file skip/publish counters
cache filtering by date/radar
mixed-cache behavior
retained cleanup and release behavior after aggregate sessions
```

If one surface passes and the other fails, the milestone should not silently
ship an asymmetric default. The decision trace must either keep both direct
defaults borrowed, migrate both, or explicitly justify a partial result.

## Validation Gate

Milestone 014 should use a direct API migration gate. This gate is a default
change gate for direct benchmark calls, not a new runtime ingestion claim.

Required dimensions:

```text
direct default expansion:
  omitted provider/execution/queue/retention options resolve to the accepted
  rollout contour for both MeasureFile() and MeasureCache()

fallback separation:
  explicit providerMode: BlockingBorrowed remains borrowed and visibly
  separate in result contracts

explicit queued-owned equivalence:
  explicit rollout contour calls match direct default contour result fields

CLI equivalence:
  scoped CLI rollout default stays aligned with the direct API target contour

correctness parity:
  published file count, payload values, raw checksum, validation checksum,
  topology versions, accepted moves, skipped decisions, failed migrations,
  and validation status match same-run borrowed rows

cleanup:
  current pending, active, and combined retained counts/bytes return to zero
  at completion

release health:
  retained payload and provider overlap failed releases remain 0

retained pressure:
  combined retained payload high-water remains within the configured retained
  byte budget

allocation:
  direct default-to-borrowed allocation ratio remains interpreted against the
  accepted threshold and the KTLX 2026-05-05 warning stays visible

performance:
  direct default elapsed time remains within the accepted threshold or any
  miss is recorded as a blocker before broader expansion

variance:
  repeated direct default rows show candidate spread clearly enough to
  interpret the timing result

failure policy:
  queued-owned failure remains fail-closed, with no automatic borrowed success
```

Milestone 014 should start from the milestone 012/013 thresholds unless the
architecture or plan records a reason to change them before new measurements
are interpreted:

```text
release failures:
  must equal 0

current retained pressure at completion:
  pending, active, and combined counts/bytes must return to 0

combined retained payload high-water:
  must stay within 536870912 bytes unless the configured budget changes in a
  documented contour

allocation ratio:
  direct default allocated bytes should remain <= 1.10x same-run borrowed,
  with KTLX 2026-05-05 explicitly carried as a known borderline migration
  cost

elapsed ratio:
  direct default elapsed time should remain <= 1.00x same-run borrowed on
  primary repeated natural rows

candidate run spread:
  repeated natural direct default spread should remain <= 7.50% of candidate
  average
```

Threshold changes are allowed only if they are recorded before gate
interpretation. Do not tune thresholds after seeing a favorable or unfavorable
row.

## Benchmark Scope

The benchmark scope should remain natural Release evidence.

Required benchmark posture:

```text
Release build before capture
direct API same-run blocking-borrowed reference rows
direct API omitted-provider rollout-default rows
controlled consumer delay disabled
retained pressure telemetry enabled
queue and overlap telemetry visible
direct result contracts inspected for effective contour
deterministic output comparison captured
repeated rows for at least one primary contour
repeated KTLX 2026-05-05 allocation-risk row
broader single-row or repeated rows for additional local cache shapes
```

Candidate contours:

```text
primary KTLX 2026-05-04 contour retained from milestones 012 and 013
borderline KTLX 2026-05-05 contour retained from milestone 013
KINX 2026-05-04 broader contour
mixed-cache contour over all local NEXRAD cache shapes
single-file direct smoke contour for file-level default expansion
small fake-data tests for deterministic direct API contracts
```

The gate should report:

```text
effective direct configuration
borrowed elapsed and allocated bytes
direct default elapsed and allocated bytes
direct default-to-borrowed elapsed ratio
direct default-to-borrowed allocation ratio
published/skipped/examined file counts
payload values and raw checksum
validation checksum and validation status
topology versions and rebalance counters
skipped reason counters
queue depth and overlap indicators
retained pending, active, and combined high-water values
current retained pressure at completion
release attempts and failed releases
dominant allocation attribution categories where available
whether direct default equals explicit rollout contour
whether direct default remains aligned with CLI rollout contour
```

The existing CLI benchmark can remain a useful cross-check for the shared
contour. It should not be the only proof that direct API defaults migrated,
because the CLI passes parsed effective options into the benchmark and is not
the same as a direct omitted-argument call.

## KTLX 2026-05-05 Allocation Warning

Milestone 013 accepted KTLX 2026-05-05 as a tracked migration cost:

```text
two-run average allocation ratio: 1.1005x borrowed
one row above the 1.10x threshold
one row below the 1.10x threshold
correctness, release health, cleanup, pressure, and elapsed timing passed
```

Milestone 014 must carry that warning forward. The gate should not report
"allocation passed" without naming the threshold tension.

Decision options for milestone 014:

```text
accepted migration cost:
  KTLX remains close to the milestone 013 warning, other dimensions pass, and
  the decision trace accepts the cost for direct API migration

stabilize first:
  allocation grows materially beyond the known warning or appears on broader
  rows in a way that weakens the direct default decision

optimize before broader runtime:
  direct migration can proceed, but the next milestone should target
  allocation reduction before live/runtime expansion

defer migration:
  allocation, cleanup, release health, correctness, or pressure evidence is
  strong enough to keep direct defaults borrowed
```

The decision trace should use concrete row data rather than phrases such as
"close enough" or "still monitored".

## Operator And Documentation Surface

Milestone 013 updated CLI help to state:

```text
rebalance-archive CLI default is scoped; direct MeasureFile()/MeasureCache()
defaults remain blocking-borrowed.
```

If milestone 014 accepts direct migration, that statement becomes wrong and
must be updated.

The operator/documentation surface should make these statements true:

```text
CLI omitted-provider rebalance-archive remains queued-owned rollout contour
direct MeasureFile()/MeasureCache() omitted provider options now use the same
  queued-owned rollout contour
explicit blocking-borrowed remains the fallback/oracle path
controlled consumer-delay rows remain mechanics proof, not default evidence
live ingestion/runtime provider defaults remain out of scope
```

The milestone should update:

```text
CLI help text that describes direct defaults
tests that assert the help text
direct API tests that previously asserted borrowed defaults
milestone docs and handoff
any decision trace language that names direct compatibility posture
```

The direct API does not need CLI-style provenance fields in this milestone.
Result contracts and tests are enough if they expose the effective contour
unambiguously.

## Failure Policy

Milestone 014 preserves milestone 012 and 013 failure behavior.

Required policy:

```text
retention failure:
  stop intake, release accepted resources, and report candidate failure

queue acceptance failure:
  stop or reject deterministically without claiming successful default output

processing failure:
  fault the queued session, release accepted resources, and prevent later
  success claims

validation failure:
  report direct default validation failure, not hidden fallback success

release failure:
  increment failed-release telemetry and fail migration evidence

cancellation:
  stop intake deterministically and release accepted retained resources
```

Any future automatic fallback design must be a separate milestone with visible
operator semantics. It is not part of direct API default migration.

## Documentation And Handoff

Expected milestone documents:

```text
docs/milestones/014-direct-archive-rebalance-api-default-migration.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-plan.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-performance-gate.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-decision-trace.md
docs/milestones/014-direct-archive-rebalance-api-default-migration-closeout.md
docs/handoff.md
```

The handoff should state one of:

```text
direct MeasureFile()/MeasureCache() defaults migrated to queued-owned rollout
  contour, explicit blocking-borrowed remains fallback/oracle, and the next
  milestone can consider a named broader expansion or targeted allocation work

or

direct defaults remain blocking-borrowed because the migration gate found a
  named blocker that should be addressed next

or

direct defaults migrated only partially, with the asymmetric surface and
  follow-up requirement explicitly named
```

## In Scope

Milestone 014 includes:

```text
direct MeasureFile()/MeasureCache() default migration
direct default contour drift guardrails
explicit direct blocking-borrowed fallback compatibility checks
same-run direct borrowed oracle preservation
direct file/cache correctness and telemetry regression coverage
direct explicit queued-owned equivalence checks
CLI/direct contour alignment checks
natural Release gate with KTLX 2026-05-05 allocation warning carried forward
operator help and documentation cleanup around direct default posture
decision trace that records whether direct API migration is accepted
handoff update for the direct API default outcome
```

## Out Of Scope

Milestone 014 does not implement:

```text
synthetic processing benchmark default migration
non-benchmark archive publishing API default migration
live ingestion/runtime provider defaults
durable queue or broker integration
cross-process provider or worker transport
ordered concurrent rebalance commit barrier
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
automatic silent fallback from queued-owned failure to borrowed success
allocation optimization unless required to interpret or unblock the migration
```

## Completion Criteria

Milestone 014 is complete when:

```text
direct MeasureFile() omitted provider/execution/queue/retention arguments
  resolve to the accepted queued-owned rollout contour

direct MeasureCache() omitted provider/execution/queue/retention arguments
  resolve to the accepted queued-owned rollout contour

explicit direct blocking-borrowed remains selectable and covered

same-run borrowed oracle rows remain available and documented

direct explicit queued-owned rollout calls remain equivalent to direct default
  calls for the shared contour

CLI omitted-provider rollout contour remains aligned with direct API defaults

retained cleanup returns current pressure to zero in natural direct default rows

release failures remain visible and fail the gate

retained pressure stays within the configured budget or a blocker is recorded

allocation overhead is interpreted against the recorded threshold and the KTLX
  2026-05-05 warning is visible in the gate and decision trace

timing and variance are interpreted against recorded thresholds

help text and documentation no longer claim direct defaults remain borrowed
  after migration

queued-owned failures remain fail-closed with no automatic borrowed fallback

the decision trace records whether direct API defaults changed

the closeout records verification, gate results, residual risks, and carry
  forward items

handoff states the current direct API default posture and recommended next
  milestone unambiguously
```

The milestone should close with a direct API default decision, not with a vague
"continue monitoring" statement.

## Likely Next Milestone Input

If milestone 014 closes with accepted direct API migration, the next milestone
can choose one of:

```text
broader benchmark surface default rollout
targeted allocation reduction before runtime expansion
live or durable ingestion architecture over the retained-owned boundary
ordered concurrent rebalance architecture
runtime/provider default readiness gate
```

If milestone 014 finds a blocker, the next milestone should target the named
blocker:

```text
direct default contour drift
file/cache direct API asymmetry
operator-surface ambiguity
allocation threshold miss beyond the accepted KTLX warning
retained pressure threshold miss
release or cleanup failure
validation parity failure
performance or variance regression
insufficient direct API workload coverage for the next expansion decision
```

Still deferred unless explicitly reprioritized:

```text
durable queues
live ingestion
cross-process workers
ordered concurrent rebalance
builder-transfer
source-level migration
partition splitting
complex radar algorithms
```

# Handoff: Milestone 006 Slice 11 Complete

## Current Goal

Milestone 004 is complete. RadarPulse now has a compact, deterministic,
normalized `RadarEventBatch` stream with append-only dense identity catalogs, an
identity normalization boundary, versioned dictionary/source-universe
visibility, explicit raw payload storage lifetime, sequential and ordered
parallel replay integration, cache replay, validation, CLI smoke commands, and
benchmarks.

Milestone 005 is complete. RadarPulse now has the first static processing core
over the closed milestone 004 stream contract: processing contracts, static
partition topology, dense source-local state, processing payload readers,
sequential processing, synchronous `PartitionedBarrier` routing, partition and
shard telemetry, processing-output validation helpers, source-local handler
slots, a synthetic processing-only benchmark harness, CLI benchmark command,
decision trace, closeout, and Release processing-only benchmark numbers.

Milestone 006 architecture and implementation planning are complete. The
milestone scope is cautious, synchronous, partition-level shard rebalance over
the measured static baseline: versioned `PartitionId -> ShardId` topology,
windowed pressure detection, direct hot-partition relief, cold-partition
evacuation when the hot partition cannot move safely, anti-churn policy,
migration lifecycle, state handoff validation, and rebalance telemetry.

Milestone 006 slice 1 is implemented in the current working tree. RadarPulse now
has the first versioned topology foundation: `RadarProcessingTopologyVersion`,
an immutable public topology partition view, monotonic topology snapshots,
validated partition owner move requests/results, and a
`RadarProcessingTopologyManager` that publishes version `N+1` snapshots while
preserving the stable `SourceId -> PartitionId` mapping.

Milestone 006 slice 2 is implemented in the current working tree. Routes,
partitioned telemetry, and processing results now record the topology version
captured for a batch. Partitioned telemetry and result construction validate
that telemetry topology version matches the result topology version.

Milestone 006 slice 3 is implemented in the current working tree. RadarPulse now
has pressure sample and score contracts over partitioned telemetry:
`RadarProcessingPressureSample`, shard and partition pressure samples,
`RadarProcessingPressureScore`, `RadarProcessingPressureBand`, and
`RadarProcessingPressureOptions`. Pressure samples copy numeric telemetry only
and do not retain `RadarEventBatch` payload references.

Milestone 006 slice 4 is implemented in the current working tree. RadarPulse now
has rolling pressure windows with explicit hysteresis:
`RadarProcessingPressureWindow`, window options, shard pressure state, and
partition pressure state. The window tracks recent pressure samples, exposes
rebalance eligibility only after the configured minimum sample count, preserves
latest topology version, and applies enter/exit thresholds so short spikes do
not automatically trigger rebalance.

Milestone 006 slice 5 is implemented in the current working tree. RadarPulse now
has deterministic anti-churn policy state:
`RadarProcessingRebalancePolicyState`, rebalance options, move policy input,
policy result/rejection contracts, partition residency/cooldown state, shard
cooldown state, and move budgets. The policy evaluates candidate moves without
mutating state, records only accepted moves, and applies logical-sequence based
minimum residency, cooldown, global/source/target budget, projected-benefit,
and target-headroom gates.

Milestone 006 slice 6 is implemented in the current working tree. RadarPulse now
has a stable rebalance decision and skipped-reason telemetry contract:
`RadarProcessingRebalanceDecision`, decision kind, move kind, skipped reason,
candidate, and projected pressure types. Decisions can now represent no-action,
accepted move, and rejected-candidate outcomes, carry topology/evaluation/window
context, expose move telemetry, and map policy rejections into explicit skipped
reasons.

Milestone 006 slice 7 is implemented in the current working tree. RadarPulse now
has a deterministic direct hot relief planner:
`RadarProcessingDirectHotReliefPlanner`. The planner reads the rolling pressure
window and anti-churn policy state, finds hot or super-hot source shards, builds
direct hot-partition relief candidates against cold target shards, projects
source/target pressure before and after, rejects unsafe target projections,
applies policy gates, and returns rebalance decisions without mutating topology
or policy state.

Milestone 006 slice 8 is implemented in the current working tree. RadarPulse now
has hot-partition classification state:
`RadarProcessingHotPartitionClassification`,
`RadarProcessingHotPartitionState`, and
`RadarProcessingHotPartitionClassifier`. Direct hot relief planning can now
record intrinsic hot partitions, skip intrinsic or quarantined partitions on
later evaluations, surface skipped classification reasons in decision telemetry,
and quarantine partitions whose recent movement produced insufficient actual
relief.

Milestone 006 slice 9 is implemented in the current working tree. RadarPulse now
has a cold evacuation fallback planner:
`RadarProcessingColdEvacuationPlanner`. The planner runs only against sustained
hot or super-hot source shards, selects low-pressure non-hot partitions on the
hot shard, projects source/target pressure before and after, rejects cosmetic
or unsafe target moves, applies anti-churn policy gates, and returns
`ColdEvacuation` rebalance decisions without mutating topology or policy state.

Milestone 006 slice 10 is implemented in the current working tree. RadarPulse
now has a synchronous migration lifecycle and coordinator:
`RadarProcessingPartitionMigrationState`, migration validation errors,
partition migration requests, migration validation results, migration results,
and `RadarProcessingMigrationCoordinator`. The coordinator accepts only
accepted rebalance decisions, validates the current topology version and source
shard ownership before publication, applies moves through
`RadarProcessingTopologyManager`, records previous/current topology versions,
and leaves topology unchanged on rejected or failed validation.

Milestone 006 slice 11 is implemented in the current working tree. RadarPulse
now has state handoff validation contracts:
`RadarProcessingPartitionStateSnapshot`,
`RadarProcessingPartitionStateChecksum`,
`RadarProcessingStateHandoffValidator`, validation result, and validation
errors. The validator captures partition-owned source-state summaries before
and after a migration and allows owner shard changes while rejecting partition
id, source range, count, raw checksum, processing checksum, last timestamp
checksum, and handler snapshot checksum mismatches.

The next implementation focus is milestone 006 slice 12: rebalance-aware
processing loop. The next slice should process one batch against one immutable
topology snapshot, feed telemetry into pressure tracking, evaluate rebalance
after the barrier, and apply at most bounded accepted migrations before the
next batch.

Planning note: quarantine is not intended to be permanent. The current slice 8
state supports explicit clear/effective-outcome reset, but the controller
integration should add automatic lifecycle handling on logical evaluations:
quarantine TTL, sustained cooled-sample reset, or downgrade when pressure has
changed enough that retrying is safe.

## Milestone Status

Done:

- `001` historical archive loader is complete.
- `002` NEXRAD archive inspection/decoder foundation is complete.
- `003` historical replay publisher foundation is complete.
- `004` processing-core input contract architecture is scoped.
- `004` processing-core input contract implementation plan is complete.
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
- `004` reusable normalized batch publish session is implemented for stream
  benchmarks.
- `004` leased hot-path batch delivery is implemented for reusable stream
  sessions, with explicit owned snapshot conversion for retained batches.
- `004` normalized stream throughput now exceeds the milestone 003 count-only
  replay-publish baseline on the comparable payload-value metric.
- `004` processing-core input contract milestone is closed.
- `005` processing-core architecture is complete.
- `005` processing-core implementation plan is complete.
- `005` processing-core contracts are implemented and tested.
- `005` static partition topology is implemented and tested.
- `005` dense source-local state store is implemented and tested.
- `005` processing payload reader helpers are implemented and tested.
- `005` sequential processing core baseline is implemented and tested.
- `005` sequential lifetime and parity guardrails are tested.
- `005` partitioned batch routing substrate is implemented and tested.
- `005` first synchronous `PartitionedBarrier` execution path is implemented
  and tested.
- `005` partitioned telemetry and route-summary validation are implemented and
  tested.
- `005` processing-output validation helpers are implemented and tested.
- `005` source-local handler slot model is implemented and tested.
- `005` synthetic processing-only benchmark harness is implemented and tested.
- `005` synthetic processing CLI benchmark command is implemented and
  smoke-tested.
- `005` Release processing-only benchmark baseline is captured for sequential
  and partitioned synthetic modes, with and without the counter/checksum
  handler workload.
- `005` processing-core decision trace and closeout are written.
- `006` partition-level shard rebalance architecture is complete.
- `006` partition-level shard rebalance implementation plan is complete.
- `006` slice 1 versioned topology contracts and publication boundary are
  implemented and tested.
- `006` slice 2 route topology-version integration is implemented and tested.
- `006` slice 3 pressure sample and pressure score contracts are implemented
  and tested.
- `006` slice 4 pressure window and hysteresis tracking is implemented and
  tested.
- `006` slice 5 anti-churn policy state is implemented and tested.
- `006` slice 6 rebalance decision and skipped-reason telemetry contracts are
  implemented and tested.
- `006` slice 7 direct hot relief planner is implemented and tested.
- `006` slice 8 intrinsic hot partition classification is implemented and
  tested.
- `006` slice 9 cold evacuation planner is implemented and tested.
- `006` slice 10 migration lifecycle and coordinator is implemented and
  tested.
- `006` slice 11 state handoff validation is implemented and tested.
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

Next milestone focus:

- Implement milestone 006 slice 12 rebalance-aware processing loop.
- Process one batch against one topology snapshot, evaluate rebalance after the
  synchronous barrier, and apply accepted migrations only between batches.
- Preserve state handoff validation when migration is integrated into the
  processing session.
- Preserve migration lifecycle semantics: no partial ownership changes after
  failed validation.
- Keep cold evacuation as a pressure-relief fallback, not general load
  shuffling when it is integrated into the controller.
- Preserve the follow-up requirement that quarantined hot partitions must decay
  or clear by logical evaluation state after sustained cooling; quarantine must
  not become an eternal ban.
- Keep skipped rebalance decisions visible through telemetry so "no move" can
  be explained by policy gates rather than ambiguity.
- Preserve synchronous `PartitionedBarrier` processing as the first rebalance
  correctness boundary: process one batch against one topology snapshot, then
  evaluate and apply rebalance before the next batch.
- Preserve the `SourceId -> PartitionId -> ShardId` ownership model when
  adding partition movement and source-state transfer. The
  source-to-partition mapping remains stable; only partition-to-shard ownership
  moves.
- Implement rebalance as cautious pressure relief, not mechanical equalization.
  The controller should require sustained pressure, projected benefit,
  headroom on the target shard, cooldown, minimum residency, and move budgets.
- Support direct hot-partition relief first, then cold-partition evacuation from
  a hot shard when the hot partition cannot move safely.
- Treat the current `archive benchmark stream` numbers as replay construction
  throughput, not as the future processing-core throughput over
  already-built `RadarEventBatch` values.
- Preserve the leased batch lifetime rule: hot-path consumers may inspect a
  leased `RadarEventBatch` only during the synchronous publish callback; any
  retained batch must be converted with `ToOwnedSnapshot()`.
- Preserve the slice 1 cache-conscious stream event constraint:
  `RadarStreamEvent` is a 64-byte unmanaged value type with no reference
  fields.
- Preserve the ordered parallel projection rule in any future replay work:
  workers may decompress/project records concurrently, but emission must be
  merged by original source order, not worker completion order.
- Keep the order-sensitive chronology checksum as the validation gate for
  sequential/parallel equivalence.
- Keep processing benchmark commands explicit that measured time excludes
  decompression, Archive Two scanning, identity normalization, and
  `RadarEventBatch` construction.
- Consider the remaining cache-wide allocation sources only if they block the
  next milestone goal: compressed-record descriptor storage, ordered task
  scheduling, file enumeration/order materialization, and scanner/decompression
  buffer churn.
- Avoid implementing processing algorithms, live ingestion, durable broker
  integration, visualization, or a general storage subsystem as retroactive
  milestone 004 work.

Completed in milestone 004 planning:

- `docs/milestones/004-processing-core-input-contract.md`.
- `docs/milestones/004-processing-core-input-contract-plan.md`.
- `docs/milestones/004-processing-core-input-contract-decision-trace.md`.
- `docs/milestones/004-processing-core-input-contract-closeout.md`.
- Milestone 004 scope narrowed to the normalized processing-core input contract,
  not downstream processing algorithms or distribution.
- Dense identity catalogs are specified as persistent append-only catalogs with
  external versioned visibility.
- `RadarEventBatch` / `RadarStreamEvent` contract shape is specified with
  `StreamSchemaVersion`, `DictionaryVersion`, and `SourceUniverseVersion`.
- Identity normalization is specified as a mandatory boundary between decoded
  radar structures and batch construction, with no per-gate text lookup.
- Payload rules are specified: raw radar values are canonical, payload storage
  is associated with the visible batch lifetime, and event payload references
  are explicit.

Completed in milestone 005 planning:

- `docs/milestones/005-processing-core-architecture.md`.
- `docs/milestones/005-processing-core-architecture-plan.md`.
- `docs/milestones/005-processing-core-architecture-decision-trace.md`.
- `docs/milestones/005-processing-core-architecture-closeout.md`.
- Milestone 005 scope is the first static partitioned processing core over
  `RadarEventBatch`, not live shard rebalance or complex radar algorithms.
- The expected result is an accepted processing-core boundary over
  `RadarEventBatch`, static source-based partition/shard ownership, dense
  source-local state, explicit leased/retained payload lifetime rules,
  source-local handler slots, processing-only telemetry, validation, and
  benchmark contracts.
- The implementation plan is broken into processing contracts, static
  topology, dense state, handler slots, payload readers, sequential baseline,
  partitioned completion-barrier mode, lifetime guardrails, telemetry,
  validation, benchmarks, CLI smoke commands, and closeout/handoff.
- Milestone 006 is identified as the next milestone for partition-level shard
  rebalance after the static processing core baseline is measured.

Completed in milestone 006 planning:

- `docs/milestones/006-partition-level-shard-rebalance.md`.
- `docs/milestones/006-partition-level-shard-rebalance-plan.md`.
- Milestone 006 scope is cautious partition-level shard rebalance over the
  synchronous milestone 005 `PartitionedBarrier` baseline, not retained async
  processing, live ingestion, source-level migration, partition splitting, or
  complex radar algorithms.
- The accepted architecture preserves stable `SourceId -> PartitionId` mapping
  and makes only `PartitionId -> ShardId` movable through versioned topology
  snapshots.
- Rebalance is defined as pressure relief, not mechanical equalization:
  decisions require sustained shard pressure, projected benefit, target
  headroom, cooldown, minimum residency, and move-budget gates.
- The implementation plan is broken into versioned topology contracts,
  topology publication, route topology-version integration, pressure samples,
  pressure windows, anti-churn state, decision/skipped-reason telemetry, direct
  hot relief, intrinsic hot partition classification, cold evacuation,
  migration lifecycle, state handoff validation, rebalance validation,
  synthetic workloads, benchmarks, CLI smoke/benchmark command, and
  closeout/handoff.
- The first implementation slice should add versioned topology contracts while
  preserving the existing contiguous source-range partition mapping.

Completed in milestone 006 implementation:

- `RadarProcessingTopologyVersion`.
- `RadarProcessingTopologyMoveError`.
- `RadarProcessingTopologyMoveRequest`.
- `RadarProcessingTopologyMoveResult`.
- `RadarProcessingTopologyManager`.
- `RadarProcessingTopology` now exposes `Version`, starting at
  `RadarProcessingTopologyVersion.Initial`.
- `RadarProcessingTopology.Partitions` now exposes an immutable read-only view
  over the partition assignments.
- A valid partition owner move creates a new topology snapshot with version
  `N+1`.
- Old topology snapshots remain unchanged after a partition owner move.
- The source-to-partition mapping is preserved across partition owner moves.
- Only the requested `PartitionId -> ShardId` owner changes during a move.
- Topology move publication rejects stale topology versions, out-of-range
  partition ids, out-of-range source/target shard ids, no-op moves, and source
  shard ownership mismatches.
- `RadarProcessingTopologyVersioningTests` cover initial versioning, valid
  publication, old snapshot immutability, stable source-to-partition mapping,
  single-partition owner changes, stale requests, invalid requests, and negative
  topology version guardrails.
- Verification after milestone 006 slice 1:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 106 tests passed.
- Verification after milestone 006 slice 1:
  `dotnet test RadarPulse.sln --no-restore` passed with 249 tests passed and 3
  skipped.
- Verification after milestone 006 slice 1:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingBatchRoute` now exposes `TopologyVersion`, captured from the
  `RadarProcessingTopology` used by `RadarProcessingBatchRouter.Route`.
- `RadarProcessingTelemetry` now exposes `TopologyVersion`, copied from the
  route used to build partitioned telemetry.
- `RadarProcessingResult` now exposes `TopologyVersion`. When telemetry is
  supplied, the result validates that telemetry topology version matches result
  topology version.
- `RadarProcessingCore` now returns valid and invalid results with the core
  topology version.
- Router tests verify that routes capture topology version and that a route
  built from an old topology snapshot remains explainable after the manager
  publishes a newer snapshot.
- Telemetry tests verify that partitioned result topology version and telemetry
  topology version match.
- Contract tests verify the default initial result topology version and reject
  telemetry/result topology-version mismatch.
- Verification after milestone 006 slice 2:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 108 tests passed.
- Verification after milestone 006 slice 2:
  `dotnet test RadarPulse.sln --no-restore` passed with 251 tests passed and 3
  skipped.
- Verification after milestone 006 slice 2:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPressureBand`.
- `RadarProcessingPressureScore`.
- `RadarProcessingPressureOptions`.
- `RadarProcessingShardPressureSample`.
- `RadarProcessingPartitionPressureSample`.
- `RadarProcessingPressureSample`.
- `RadarProcessingPressureSample.FromTelemetry` projects partitioned telemetry
  into immutable shard and partition pressure samples.
- Pressure samples preserve topology version, batch metrics, per-shard metrics,
  per-partition metrics, event counts, payload value counts, and raw-value
  checksums.
- Pressure scoring is deterministic and currently uses configurable event,
  payload-value, and raw-checksum weights.
- Pressure band classification supports `Cold`, `Normal`, `Warm`, `Hot`, and
  `SuperHot`.
- Pressure numeric options reject negative, NaN, infinity, and non-monotonic
  threshold values.
- Pressure sample tests cover empty telemetry, topology version and metric
  projection, score growth by event and payload count, deterministic band
  classification, leased payload lifetime safety, stability after later
  processing, and numeric guardrails.
- Verification after milestone 006 slice 3:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 116 tests passed.
- Verification after milestone 006 slice 3:
  `dotnet test RadarPulse.sln --no-restore` passed with 259 tests passed and 3
  skipped.
- Verification after milestone 006 slice 3:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPressureWindowOptions`.
- `RadarProcessingShardPressureState`.
- `RadarProcessingPartitionPressureState`.
- `RadarProcessingPressureWindow`.
- Pressure windows retain the last configured number of
  `RadarProcessingPressureSample` values and expose current shard and partition
  pressure state.
- Pressure windows expose `IsRebalanceEligible` only after the configured
  minimum sample count is reached.
- Pressure windows preserve the latest observed topology version.
- Shard pressure state tracks sample count, latest partition counts, total
  route metrics across the window, average score, band, hot flag, and super-hot
  flag.
- Partition pressure state tracks partition id, latest owner shard, sample
  count, total route metrics across the window, average score, band, hot flag,
  and super-hot flag.
- Window hysteresis uses explicit warm/hot/super-hot enter and exit thresholds.
- Pressure window tests cover minimum sample eligibility, sustained hot
  detection, preserving hot band between enter/exit thresholds, leaving hot
  below the exit threshold, cold empty samples, partition pressure ownership,
  latest topology version, mismatched sample shape rejection, and option
  guardrails.
- Verification after milestone 006 slice 4:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 125 tests passed.
- Verification after milestone 006 slice 4:
  `dotnet test RadarPulse.sln --no-restore` passed with 268 tests passed and 3
  skipped.
- Verification after milestone 006 slice 4:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingRebalanceOptions`.
- `RadarProcessingRebalanceBudget`.
- `RadarProcessingPartitionResidency`.
- `RadarProcessingPartitionCooldown`.
- `RadarProcessingShardCooldown`.
- `RadarProcessingRebalanceMovePolicyInput`.
- `RadarProcessingRebalancePolicyRejection`.
- `RadarProcessingRebalancePolicyResult`.
- `RadarProcessingRebalancePolicyState`.
- Rebalance options define deterministic anti-churn gates: budget window,
  global move budget, source-shard move budget, target-shard receive budget,
  minimum partition residency, partition move cooldown, source-shard move
  cooldown, target-shard receive cooldown, minimum projected benefit, and
  target headroom threshold.
- Rebalance policy state is driven by logical `EvaluationSequence`, not wall
  clock time.
- `EvaluateMove` validates a candidate move and returns all active rejection
  reasons without mutating policy state.
- `RecordAcceptedMove` applies budget, cooldown, and residency state only when
  the candidate passes every policy gate.
- Policy gates cover minimum partition residency, partition cooldown,
  source-shard cooldown, target-shard cooldown, global move budget,
  source-shard move budget, target-shard receive budget, projected benefit, and
  target headroom.
- Policy state exposes read-side accessors for partition residency, partition
  cooldown, source/target shard cooldowns, and source/target shard budgets.
- Rebalance policy tests cover residency, cooldown expiry, source/target shard
  cooldowns, global/source/target budgets, projected-benefit rejection, target
  headroom rejection, non-mutating evaluation, rejected-record non-mutation,
  deterministic evaluation advancement, accessors, option guardrails, and move
  input guardrails.
- Verification after milestone 006 slice 5:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 139 tests passed.
- Verification after milestone 006 slice 5:
  `dotnet test RadarPulse.sln --no-restore` passed with 282 tests passed and 3
  skipped.
- Verification after milestone 006 slice 5:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingRebalanceDecisionKind`.
- `RadarProcessingRebalanceMoveKind`.
- `RadarProcessingRebalanceSkippedReason`.
- `RadarProcessingProjectedPressure`.
- `RadarProcessingRebalanceCandidate`.
- `RadarProcessingRebalanceDecision`.
- Rebalance decisions now represent three stable outcomes: `NoAction`,
  `AcceptedMove`, and `RejectedCandidate`.
- Rebalance move kinds now classify direct hot relief, cold evacuation, and a
  reserved room-making move kind.
- Skipped reasons are explicit telemetry values for no sustained pressure, no
  hot shard, no cold target shard, unsafe direct hot relief, insufficient
  projected benefit, target pressure risk, residency/cooldown/budget gates,
  intrinsic hot partition classification, cold evacuation benefit failure, and
  migration validation failure.
- Rebalance candidates carry move kind, partition id, source shard id, target
  shard id, projected source/target pressure before and after, and expected
  relief.
- Rebalance candidates can be converted into policy inputs and topology move
  requests without the future planner duplicating move shape logic.
- Rejected-candidate decisions can be created from
  `RadarProcessingRebalancePolicyResult`; policy rejections are copied into
  decision telemetry and mapped to skipped reasons.
- Decision contracts copy and de-duplicate reason collections so later caller
  mutation does not affect recorded telemetry.
- Rebalance decision tests cover no-pressure no-action, hot shard with no
  target, rejected candidate with multiple policy gates, accepted move
  telemetry, deterministic construction, collection copying, decision
  guardrails, and candidate guardrails.
- Verification after milestone 006 slice 6:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 147 tests passed.
- Verification after milestone 006 slice 6:
  `dotnet test RadarPulse.sln --no-restore` passed with 290 tests passed and 3
  skipped.
- Verification after milestone 006 slice 6:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingDirectHotReliefPlanner`.
- Direct hot relief planning reads pressure windows and policy state without
  mutating topology or consuming anti-churn budgets/cooldowns.
- The planner returns `NoAction` when the pressure window has not reached its
  minimum sample count, when no shard is hot, or when no cold target shard is
  available.
- The planner ranks direct hot relief candidates deterministically by projected
  max-pressure relief, partition pressure, partition id, and target shard id.
- Candidate projection records source shard pressure before/after, target shard
  pressure before/after, and expected relief.
- Candidate target projection rejects direct moves that would make the target
  shard warm, hot, or super-hot before policy gates are evaluated.
- Anti-churn gates are applied through `RadarProcessingRebalancePolicyState`
  evaluation before an accepted move decision is returned.
- Direct hot relief tests cover sustained hot shard candidate creation,
  deterministic largest useful partition selection, target-hot rejection,
  insufficient projected benefit rejection, cooldown rejection, accepted move
  projected max-pressure relief, ineligible window no-action, and eligible
  no-hot-shard no-action.
- Verification after milestone 006 slice 7:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 155 tests passed.
- Verification after milestone 006 slice 7:
  `dotnet test RadarPulse.sln --no-restore` passed with 298 tests passed and 3
  skipped.
- Verification after milestone 006 slice 7:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingHotPartitionClassification`.
- `RadarProcessingHotPartitionState`.
- `RadarProcessingHotPartitionClassifier`.
- `RadarProcessingRebalanceSkippedReason.PartitionQuarantined`.
- Hot partition classification tracks `None`, `MovableHot`, `IntrinsicHot`,
  and `Quarantined` states per partition.
- Intrinsic and quarantined hot partition states block direct hot relief
  selection for that partition.
- The direct hot relief planner can receive optional hot-partition
  classification state. It records intrinsic hot classification when the
  selected direct-hot candidate has no safe target, skips previously intrinsic
  or quarantined partitions, and returns diagnostic skipped reasons when all
  hot partitions are classification-blocked.
- Hot partition move outcomes can quarantine a partition after repeated
  ineffective movement attempts, using configurable ineffective-move count and
  minimum effective relief ratio.
- Quarantine is intentionally conservative in slice 8, but the milestone plan
  now requires later controller lifecycle handling so quarantined partitions can
  decay, clear, or downgrade after sustained cooling on logical evaluations.
- Hot partition classifier tests cover initial unclassified state, intrinsic
  blocking, movable-hot non-blocking, ineffective-move quarantine, effective
  outcome reset, clearing classification, and guardrails.
- Direct hot relief tests now cover intrinsic classification recording,
  skipping an intrinsic partition in favor of another direct candidate, and
  diagnostic no-action when every hot partition is classification-blocked.
- Verification after milestone 006 slice 8:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 164 tests passed.
- Verification after milestone 006 slice 8:
  `dotnet test RadarPulse.sln --no-restore` passed with 307 tests passed and 3
  skipped.
- Verification after milestone 006 slice 8:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingColdEvacuationPlanner`.
- Cold evacuation planning reads pressure windows and policy state without
  mutating topology or consuming anti-churn budgets/cooldowns.
- The planner returns `NoAction` when the pressure window has not reached its
  minimum sample count, when no shard is hot, when no cold target shard is
  available, or when no useful non-hot partition can be evacuated.
- The planner selects low-pressure non-hot partitions currently owned by hot or
  super-hot source shards and emits `ColdEvacuation` candidates.
- Cold evacuation candidate projection records source shard pressure
  before/after, target shard pressure before/after, and expected relief.
- Target projection rejects cold evacuation moves that would make the target
  shard warm, hot, or super-hot before policy gates are evaluated.
- Anti-churn gates are applied through `RadarProcessingRebalancePolicyState`
  evaluation before an accepted cold evacuation decision is returned.
- Cold evacuation tests cover direct-hot unsafe fallback, moving a cold
  partition off a hot shard, deterministic smallest useful cold-partition
  selection, target headroom, insufficient projected relief, target-warm
  rejection, and source-shard move budget rejection.
- Verification after milestone 006 slice 9:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 170 tests passed.
- Verification after milestone 006 slice 9:
  `dotnet test RadarPulse.sln --no-restore` passed with 313 tests passed and 3
  skipped.
- Verification after milestone 006 slice 9:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPartitionMigrationState`.
- `RadarProcessingMigrationValidationError`.
- `RadarProcessingPartitionMigration`.
- `RadarProcessingMigrationValidationResult`.
- `RadarProcessingMigrationResult`.
- `RadarProcessingMigrationCoordinator`.
- Migration coordination accepts only accepted rebalance decisions and rejects
  no-action or rejected-candidate decisions before topology validation.
- Migration validation checks current topology version, partition id range,
  source shard id range, target shard id range, no-op moves, and current source
  shard ownership before publishing a topology move.
- Valid migrations are applied through `RadarProcessingTopologyManager`, so
  published moves still reuse the monotonic topology snapshot boundary.
- Migration results record lifecycle state, validation result, migration
  request, previous topology version, current topology version, and topology
  move error when one is surfaced.
- Failed migration validation leaves the current topology snapshot unchanged
  and does not publish a partial ownership change.
- Migration coordinator tests cover accepted decision publication to topology
  `N+1`, stale decision rejection, wrong old-owner rejection, invalid target
  rejection, non-accepted decision rejection, validation without publication,
  lifecycle state/version fields, and migration contract guardrails.
- Verification after milestone 006 slice 10:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 177 tests passed.
- Verification after milestone 006 slice 10:
  `dotnet test RadarPulse.sln --no-restore` passed with 320 tests passed and 3
  skipped.
- Verification after milestone 006 slice 10:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- `RadarProcessingPartitionStateSnapshot`.
- `RadarProcessingPartitionStateChecksum`.
- `RadarProcessingStateHandoffValidationError`.
- `RadarProcessingStateHandoffValidationResult`.
- `RadarProcessingStateHandoffValidator`.
- Partition state snapshots capture the partition id, current owner shard,
  source range, active source count, processed event count, processed payload
  value count, raw value checksum, aggregate processing checksum, order-sensitive
  last message timestamp checksum, and handler snapshot checksum.
- State handoff validation intentionally allows owner shard id changes, because
  moving ownership is the successful handoff path.
- State handoff validation rejects partition id, source range, active source
  count, processed event count, processed payload value count, raw value
  checksum, processing checksum, last timestamp checksum, and handler snapshot
  checksum mismatches.
- Handler snapshot checksums include active source id, snapshot field names,
  field types, and field values so configured processing handlers participate
  in handoff validation.
- Empty partition source ranges with no active source state produce empty
  handoff checksums and validate across owner shard changes.
- State handoff validator tests cover owner-only shard changes, active source
  count mismatch, processed event count mismatch, processed payload value count
  mismatch, raw checksum mismatch, processing checksum mismatch, last timestamp
  checksum mismatch, handler checksum mismatch, and empty partition handoff.
- Verification after milestone 006 slice 11:
  `dotnet test RadarPulse.sln --no-restore --filter FullyQualifiedName~Processing`
  passed with 186 tests passed.
- Verification after milestone 006 slice 11:
  `dotnet test RadarPulse.sln --no-restore` passed with 329 tests passed and 3
  skipped.
- Verification after milestone 006 slice 11:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.

Completed in milestone 005 implementation:

- `RadarProcessingExecutionMode`.
- `RadarProcessingCoreOptions`.
- `RadarProcessingMetrics`.
- `RadarProcessingValidationError`.
- `RadarProcessingValidationResult`.
- `RadarProcessingResult`.
- Processing contracts are isolated under `RadarPulse.Domain.Processing`.
- Initial execution modes are explicit: `Sequential` and
  `PartitionedBarrier`.
- Core options validate execution mode, partition count, shard count, and the
  initial `PartitionCount >= ShardCount` topology constraint.
- Processing results carry execution mode, topology shape, deterministic
  metrics, and validation state.
- Processing results can optionally carry partitioned telemetry. Telemetry is
  accepted only for `PartitionedBarrier` results and must match the result's
  execution mode, partition count, and shard count.
- `RadarProcessingPartitionAssignment`.
- `RadarProcessingTopology`.
- Static topology maps `SourceId -> PartitionId -> ShardId` with contiguous
  source blocks.
- Topology construction validates against `RadarSourceUniverse.SourceCount`.
- Partition assignments expose partition id, shard id, source range start,
  source range end, source count, and source containment checks.
- Source ids and partition ids are range-checked at lookup boundaries.
- `RadarSourceProcessingSnapshot`.
- `RadarSourceProcessingStateStore`.
- Dense source-local state arrays are sized by
  `RadarSourceUniverse.SourceCount`.
- State updates are direct by `SourceId` and track processed event count,
  processed payload value count, raw value checksum, last message timestamp,
  active source marker, and deterministic source checksum.
- Source-local timestamp regression is rejected during state updates.
- State snapshots are read-side projections and aggregate
  `RadarProcessingMetrics` can be produced from active source state.
- `RadarProcessingPayloadMetrics`.
- `RadarProcessingPayloadReader`.
- Processing payload reader helpers compute event-level and batch-level payload
  value counts and raw value checksums.
- Payload readers support 8-bit values and 16-bit big-endian values, matching
  the existing `RadarEventBatchMetrics` raw-value contract.
- Payload reader guardrails reject null batches, unsupported word sizes,
  payload length mismatches, and out-of-range payload references.
- Payload reader signatures pass `RadarStreamEvent` by value to avoid
  ref-safety/tooling ambiguity around returned spans.
- `RadarProcessingCore`.
- The first core baseline supports `Sequential` execution mode.
- `RadarProcessingCore.Process` consumes `RadarEventBatch`, honors
  `CancellationToken`, validates stream schema and source-universe version, and
  returns `RadarProcessingResult`.
- Sequential processing iterates `RadarEventBatch.Events` in canonical order,
  reads event payload metrics through `RadarProcessingPayloadReader`, updates
  `RadarSourceProcessingStateStore`, and exposes source snapshots.
- Sequential metrics accumulate across processed batches and are available
  through both the processing result and the core state.
- Sequential baseline returns invalid processing results for unsupported stream
  schema, source-universe mismatch, source id outside universe, source
  ownership mismatch, and source-local timestamp regression.
- Sequential lifetime/parity guardrails are covered without additional
  production-code changes.
- Guardrails verify owned and leased-equivalent batch parity, result/snapshot
  stability after leased builder buffer reuse, result counters matching
  `RadarEventBatchMetrics`, invalid processing not incrementing processed batch
  count, and invalid source validation not mutating state.
- `RadarProcessingBatchRouter`.
- `RadarProcessingBatchRoute`.
- `RadarProcessingPartitionBatchRoute`.
- `RadarProcessingShardBatchRoute`.
- `RadarProcessingRoutedEvent`.
- `RadarProcessingRouteMetrics`.
- `RadarProcessingPartitionTelemetry`.
- `RadarProcessingShardTelemetry`.
- `RadarProcessingTelemetry`.
- `RadarProcessingOutputValidator`.
- `RadarSourceProcessingChecksum`.
- `IRadarSourceProcessingHandler`.
- `RadarSourceProcessingHandlerContext`.
- `RadarSourceProcessingState`.
- `RadarSourceProcessingHandlerDescriptor`.
- `RadarSourceProcessingHandlerSlotAssignment`.
- `RadarSourceProcessingHandlerSlotLayout`.
- `RadarSourceProcessingSnapshotFieldDescriptor`.
- `RadarSourceProcessingSnapshotFieldType`.
- `RadarSourceProcessingSnapshotValue`.
- `RadarSourceProcessingHandlerSnapshot`.
- `RadarProcessingBenchmarkHandlerSet`.
- `RadarProcessingBenchmarkResult`.
- `RadarProcessingBenchmarkShardDistribution`.
- `RadarProcessingSyntheticWorkloadOptions`.
- `RadarProcessingSyntheticWorkload`.
- `RadarProcessingSyntheticBenchmark`.
- Partitioned routing maps each batch event index to `PartitionId` and
  `ShardId` through `RadarProcessingTopology`.
- Routing stores event indexes and per-partition/per-shard counters without
  copying payload bytes or starting worker execution.
- Route metrics track event count, payload value count, and raw value checksum
  and aggregate consistently through partitions and shards.
- Source ids outside the topology are rejected before a route is returned.
- Partitioned telemetry is a read-side snapshot over the route: batch metrics,
  per-partition metrics, per-shard metrics, shard partition counts, active
  partition counts, deterministic hot partition id, and deterministic hot shard
  id.
- Telemetry does not expose event indexes, payload spans, or references to
  leased batch storage.
- Telemetry construction validates that partition and shard metric totals match
  the batch route metrics.
- `RadarProcessingCore` now supports both `Sequential` and
  `PartitionedBarrier` execution modes.
- The first `PartitionedBarrier` path uses `RadarProcessingBatchRouter`, then
  synchronously iterates shard event indexes and returns only after all shard
  loops finish.
- `RadarProcessingRoutedEvent` carries precomputed payload metrics so the
  partitioned path does not read payload bytes a second time after routing.
- Valid `PartitionedBarrier` results now carry partitioned telemetry; sequential
  and invalid results do not carry partitioned telemetry.
- Processing-output validation is available as a read-side helper outside the
  hot path. It validates a processed batch against `RadarProcessingResult`,
  before/after source snapshots, and optional previous metrics.
- Output validation detects missing work, duplicate work, source-local order
  violations, result/snapshot metric mismatches, and missing partitioned
  telemetry for valid `PartitionedBarrier` results.
- Processing checksum construction is shared between the runtime state store and
  the output validator so both compare the same source/event checksum contract.
- Source-local handler slots are available as a small extension platform for
  future source-local algorithms. Options can carry configured handler
  instances; the state store precomputes handler slot layout and allocates dense
  `long`/`double` slots per `SourceId`.
- Handlers receive event metadata, payload span, and precomputed payload metrics
  through `RadarSourceProcessingHandlerContext`, then mutate only their
  source-local `RadarSourceProcessingState` view.
- Handler snapshot projection is read-side. Snapshot fields are declared by
  descriptors, duplicate field names are rejected across handlers, and the hot
  path does not do per-event field-name lookup.
- `RadarProcessingCore` exposes `GetSourceHandlerSnapshot` and
  `CreateSourceHandlerSnapshots`. The no-handler path remains valid and does
  not require payload span materialization for handlers.
- Synthetic processing-only benchmarks are available through
  `RadarProcessingSyntheticBenchmark` over prebuilt deterministic
  `RadarEventBatch` workloads.
- Benchmark setup constructs the synthetic workload before the measured loop;
  warmup iterations also run before stopwatch and allocation snapshots.
- Benchmark results report execution mode, topology, handler set, iterations,
  warmup iterations, per-iteration and total batch/event/payload counters,
  raw checksum, active source count, validation checksum, shard distribution,
  elapsed time, throughput, and allocated bytes per event/value.
- Benchmark handler sets currently support `None` and `CounterChecksum`, giving
  a stable no-handler baseline and a simple source-local handler workload.
- Latest Release processing-only benchmark used a synthetic prebuilt
  `RadarEventBatch` workload shaped close to the milestone 004 single-file
  normalized stream benchmark: 32_400 sources, 1 batch, 32_400 stream events per
  iteration, 1_196 payload values per stream event, 38_750_400 payload values
  per iteration, 20 measured iterations, and 3 warmup iterations.
- Latest Release processing-only benchmark results:

  ```text
  mode              handlers          payload values/s   stream events/s   allocated bytes / payload value
  sequential        none              2_559_218_888.23   2_139_815.12      0.00
  partitioned 24/24 none              2_622_669_443.85   2_192_867.43      0.03
  sequential        counter-checksum  1_630_968_124.27   1_363_685.72      0.03
  partitioned 24/24 counter-checksum  1_745_635_000.27   1_459_561.04      0.06
  ```

- Compared with milestone 004 normalized stream single-file throughput
  (`553_123_110.90` payload values/s), the latest processing-only baseline is
  roughly `4.63x` faster for sequential/no-handler, `4.74x` faster for
  partitioned/no-handler, `2.95x` faster for sequential/counter-checksum, and
  `3.16x` faster for partitioned/counter-checksum.
- Compared with milestone 004 cache-wide normalized stream throughput
  (`509_716_417.97` payload values/s), the same processing-only results are
  roughly `5.02x`, `5.15x`, `3.20x`, and `3.42x` faster respectively.
- The processing-only result is not directly measuring multi-core scaling:
  current `PartitionedBarrier` execution is still synchronous and measures the
  static routing/barrier/shard-loop contour. Worker execution and live
  partition-level rebalance remain future milestones.
- The visible remaining performance risk inside milestone 005 is routing-buffer
  allocation in the partitioned path: the measured allocation ratios are
  `40.33` bytes per stream event without handlers and `72.33` bytes per stream
  event with the counter/checksum handler workload.
- `processing benchmark synthetic` is available as a manual CLI command over
  the synthetic processing benchmark harness.
- The CLI command accepts execution mode, source count, batch shape, topology,
  handler set, iteration count, and warmup iteration options.
- CLI output names the measured contour explicitly and states that measured
  time excludes decompression, Archive Two scanning, identity normalization,
  and `RadarEventBatch` construction.
- Source validation runs before execution for both modes, so invalid source ids
  and ownership mismatches do not mutate state.
- Partitioned and sequential results/snapshots are verified for parity on the
  current deterministic workload.
- Focused contract tests cover default options, invalid execution modes,
  invalid topology counts, validation result invariants, result shape, and empty
  result construction.
- Focused topology tests cover contiguous source blocks, complete source-id
  coverage, stable assignments, shard range mapping, null inputs, too many
  partitions, invalid source/partition ids, and invalid partition ranges.
- Focused state-store tests cover source-universe sizing, single-source update
  isolation, unique active-source counting, snapshot projection, aggregate
  metrics, empty metrics, invalid inputs, and timestamp regression.
- Focused payload-reader tests cover 8-bit payload reads, 16-bit big-endian
  payload reads, batch-level parity with `RadarEventBatchMetrics`, empty batch
  metrics, metric addition, null batch rejection, length mismatch rejection,
  out-of-range payload rejection, and unsupported word size rejection.
- Focused sequential-core tests cover constructor validation, null batch,
  cancellation before processing, unsupported stream schema, source-universe
  version mismatch, empty batch result, multi-source metrics/snapshots,
  cumulative processing across batches, source id outside universe, source
  ownership mismatch, and source-local timestamp regression.
- Focused guardrail tests cover owned/leased parity, leased-buffer reuse,
  sequential counter parity with `RadarEventBatchMetrics`, invalid batch
  accounting, and invalid-source state isolation.
- Focused batch-router tests cover empty routes, every event routed exactly
  once, topology assignment parity, same-source order preservation inside
  partition/shard routes, metrics parity with `RadarEventBatchMetrics`,
  payload-mutation stability after routing, null/mismatched input rejection,
  source id outside topology, and route lookup bounds.
- Focused partitioned-barrier tests cover sequential metric/snapshot parity,
  owned/leased parity, same-source ordering, unsupported stream schema,
  source-universe mismatch, invalid source id before mutation, source ownership
  mismatch before mutation, and source-local timestamp regression.
- Focused telemetry tests cover partitioned result telemetry, empty-batch
  telemetry, deterministic hot partition/shard ids, leased-buffer stability,
  and sequential results staying free of partitioned telemetry.
- Focused output-validator tests cover valid sequential output, valid
  partitioned output with telemetry, missing processed event detection,
  duplicate processed event detection, source-local order violation detection,
  and missing partitioned telemetry detection.
- Focused handler-slot tests cover non-overlapping slot offsets, duplicate
  snapshot field rejection, source-local handler state isolation, core handler
  invocation and snapshot projection, payload-span delivery, payload-aware
  apply requirements when handlers are configured, and no-handler base-path
  behavior.
- Focused synthetic benchmark tests cover stable sequential iteration totals,
  partitioned shard distribution, warmup exclusion from measured totals,
  sequential/partitioned validation-checksum parity, and invalid input
  guardrails.
- Verification after slice 1:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 151 tests passed and 3 skipped.
- Verification after slice 2:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 160 tests passed and 3 skipped.
- Verification after slice 3:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 168 tests passed and 3 skipped.
- Verification after slice 4:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore`
  passed with 177 tests passed and 3 skipped.
- Verification after slice 5:
  `dotnet test --no-restore` passed with 188 tests passed and 3 skipped.
- Verification after slice 6:
  `dotnet test --no-restore` passed with 193 tests passed and 3 skipped.
- Verification after slice 7:
  `dotnet test --no-restore` passed with 202 tests passed and 3 skipped.
- Verification after slice 8:
  `dotnet test --no-restore` passed with 211 tests passed and 3 skipped.
- Verification after slice 9:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 73 tests passed.
- Verification after slice 9:
  `dotnet test --no-restore` passed with 216 tests passed and 3 skipped.
- Verification after slice 9:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with the scoped
  `dotnet format --verify-no-changes --no-restore --include ...` command. The
  repo-wide formatting command still reports pre-existing whitespace in
  `tests\RadarPulse.Tests\Archive\NexradArchiveReplayPublisherTests.cs`.
- Verification after slice 10:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 79 tests passed.
- Verification after slice 10:
  `dotnet test --no-restore` passed with 222 tests passed and 3 skipped.
- Verification after slice 10:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with
  `dotnet format --verify-no-changes --no-restore --include ...`.
- Verification after slice 11:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 86 tests passed.
- Verification after slice 11:
  `dotnet test --no-restore` passed with 229 tests passed and 3 skipped.
- Verification after slice 11:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with
  `dotnet format --verify-no-changes --no-restore --include ...`.
- Verification after slice 12:
  `dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulse.Tests.Processing`
  passed with 91 tests passed.
- Verification after slice 12:
  `dotnet test --no-restore` passed with 234 tests passed and 3 skipped.
- Verification after slice 12:
  `git diff --check` passed. Scoped formatting verification for the changed
  processing files passed with
  `dotnet format --verify-no-changes --no-restore --include ...`.
- Verification after the CLI benchmark command:
  `dotnet run --no-restore --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 4 --batches 1 --events-per-batch 8 --payload-values 2 --partitions 4 --shards 2 --handlers counter-checksum --iterations 1 --warmup-iterations 0`
  passed.
- Verification after the CLI benchmark command:
  `dotnet test --no-restore` passed with 234 tests passed and 3 skipped.
- Verification after the CLI benchmark command:
  `git diff --check` passed with only Git line-ending warnings for touched
  files.
- Latest Release processing-only benchmark verification:

  ```powershell
  dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode sequential --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 1 --shards 1 --handlers none --iterations 20 --warmup-iterations 3
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 24 --shards 24 --handlers none --iterations 20 --warmup-iterations 3
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode sequential --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 1 --shards 1 --handlers counter-checksum --iterations 20 --warmup-iterations 3
  dotnet run --no-build -c Release --project src\Presentation\RadarPulse.Cli.csproj -- processing benchmark synthetic --mode partitioned --sources 32400 --batches 1 --events-per-batch 32400 --payload-values 1196 --partitions 24 --shards 24 --handlers counter-checksum --iterations 20 --warmup-iterations 3
  ```

  Result: Release build passed; measured payload values/s were
  `2_559_218_888.23`, `2_622_669_443.85`, `1_630_968_124.27`, and
  `1_745_635_000.27` for the four commands above.
- Verification at milestone 005 closeout:
  `dotnet test --no-restore` passed with 234 tests passed and 3 skipped.
- Verification at milestone 005 closeout:
  `dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore`
  passed with 0 warnings and 0 errors.
- Milestone 005 committed checkpoints before closeout:
  `d9106b0 Add processing core contracts`;
  `4639ec0 Add static processing topology`;
  `33c437a Add dense source processing state`;
  `3a3ce88 Add processing payload reader`;
  `e04265d Avoid ref parameter in payload reader`;
  `981afa1 Add sequential processing core`;
  `38296b6 Add processing core guardrail tests`;
  `5b65852 Add partitioned batch routing substrate`;
  `e900c54 Add partitioned barrier processing path`;
  `5b573d1 Add partitioned processing telemetry`;
  `c6cdd94 Add processing output validation`;
  `eb22723 Add source processing handler slots`;
  `e40aece Add processing synthetic benchmark`.

Completed in milestone 004 implementation:

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
  versions, event memory, payload memory, and explicit owned/leased lifetime.
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
- The builder owns event and payload buffers internally. `Build()` returns owned
  snapshot arrays; leased hot-path publication borrows the current buffers only
  during the synchronous callback.
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
  bytes, word size, scale, offset, source order, and lifetime-scoped payload
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
  calculation, batch event construction, lifetime-scoped payload copying,
  counting, and checksum work.
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
- `NexradArchiveRadarEventBatchPublishSession` now gives stream benchmarks a
  reusable file-publish context. It reuses decompression workers, worker-owned
  buffers, the available-worker stack, and the in-flight task dictionary across
  repeated single-file and cache-wide benchmark runs.
- `RadarEventBatch` now exposes an explicit `Lifetime`: `Owned` batches may be
  retained, while `Leased` batches are valid only during the synchronous publish
  callback. Consumers that need to keep a leased batch must call
  `ToOwnedSnapshot()`.
- `RadarEventBatchBuilder.ConsumeLeased()` gives the hot path a borrowed batch
  view and then resets counters while retaining event and payload buffer
  capacity for reuse.
- `NexradArchiveRadarEventBatchPublishSession` now reuses the
  `ArchiveTwoRadarEventBatchProjector` and its builder buffers across file
  publishes. The non-session `NexradArchiveRadarEventBatchPublisher` keeps the
  owned batch behavior for safer external capture tests and one-off publishing.

Completed in milestone 003:

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
- `docs/milestones/001-historical-loader-decision-trace.md`
- `docs/milestones/001-historical-loader-closeout.md`
- `docs/milestones/002-nexrad-archive-inspection-plan.md`
- `docs/milestones/002-nexrad-archive-inspection.md`
- `docs/milestones/002-nexrad-archive-inspection-decision-trace.md`
- `docs/milestones/002-nexrad-archive-inspection-closeout.md`
- `docs/milestones/003-historical-replay-publisher-plan.md`
- `docs/milestones/003-historical-replay-publisher.md`
- `docs/milestones/003-historical-replay-publisher-decision-trace.md`
- `docs/milestones/003-historical-replay-publisher-closeout.md`
- `docs/milestones/004-processing-core-input-contract.md`
- `docs/milestones/004-processing-core-input-contract-plan.md`
- `docs/milestones/004-processing-core-input-contract-decision-trace.md`
- `docs/milestones/004-processing-core-input-contract-closeout.md`
- `docs/milestones/005-processing-core-architecture.md`
- `docs/milestones/005-processing-core-architecture-plan.md`
- `docs/milestones/005-processing-core-architecture-decision-trace.md`
- `docs/milestones/005-processing-core-architecture-closeout.md`
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
tests: 142 passed, 3 skipped
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

Release benchmark stream after leased hot-path delivery and reusable projector buffers, parallelism 24, longer 5-iteration check:
Stream events per iteration: 32_400
Payload values per iteration: 38_759_040
Elapsed ms: 350.37
Stream events/s: 462_374.42
Payload values/s: 553_123_110.90
Allocated bytes: 180_080
Allocated bytes / payload value: 0.00

Release benchmark stream cache-wide after leased hot-path delivery and reusable projector buffers, parallelism 24:
Examined files per iteration: 244
Skipped files per iteration: 24
Published files per iteration: 220
Compressed records per iteration: 12_087
Decompressed bytes per iteration: 11_145_331_584
Stream events per iteration: 7_114_560
Payload values per iteration: 8_513_587_200
Elapsed ms: 16_702.60
Stream events/s: 425_955.35
Payload values/s: 509_716_417.97
Allocated bytes: 1_710_792_384
Allocated bytes / payload value: 0.20
```

Milestone 004 throughput achievement versus the milestone 003 count-only
`replay-publish` baseline:

```text
Comparable metric:
  milestone 003 Published events/s == milestone 004 Payload values/s
  milestone 004 Stream events/s is not comparable because one stream event
  references a payload range that can contain many raw values.

single file, parallelism 24:
  milestone 003 replay-publish: 362_695_693.02 published events/s
  milestone 004 normalized stream: 553_123_110.90 payload values/s
  delta: +190_427_417.88 values/s, +52.5%

cache-wide KTLX corpus, parallelism 24:
  milestone 003 replay-publish: 310_665_492.15 published events/s
  milestone 004 normalized stream: 509_716_417.97 payload values/s
  delta: +199_050_925.82 values/s, +64.1%

allocation tradeoff:
  milestone 003 single-file: 0.07 allocated bytes/event
  milestone 004 single-file: 0.00 allocated bytes/payload value
  milestone 003 cache-wide: 0.06 allocated bytes/event
  milestone 004 cache-wide: 0.20 allocated bytes/payload value
```

Assessment: milestone 004 has recovered and exceeded the earlier 300M+
throughput level while doing more structural work: normalized batch creation,
dense identity normalization, source-universe mapping, version visibility, and
explicit payload lifetime. The leased hot-path delivery pass removed the main
single-file batch buffer allocation cost and reduced cache-wide allocation from
`1.86` to `0.20` allocated bytes/payload value. Remaining performance work
should focus on cache-wide replay overhead outside the normalized batch buffers.

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
- `docs/milestones/004-processing-core-input-contract-decision-trace.md`
- `docs/milestones/004-processing-core-input-contract-closeout.md`
- `docs/milestones/005-processing-core-architecture.md`
- `docs/milestones/005-processing-core-architecture-plan.md`
- `docs/milestones/005-processing-core-architecture-decision-trace.md`
- `docs/milestones/005-processing-core-architecture-closeout.md`
- `docs/milestones/006-partition-level-shard-rebalance.md`
- `docs/milestones/006-partition-level-shard-rebalance-plan.md`
- `src/Domain/Processing/RadarProcessingTopologyVersion.cs`
- `src/Domain/Processing/RadarProcessingTopologyManager.cs`
- `src/Domain/Processing/RadarProcessingTopologyMoveRequest.cs`
- `src/Domain/Processing/RadarProcessingTopologyMoveResult.cs`
- `src/Domain/Processing/RadarProcessingTopologyMoveError.cs`
- `src/Domain/Processing/RadarProcessingBatchRoute.cs`
- `src/Domain/Processing/RadarProcessingBatchRouter.cs`
- `src/Domain/Processing/RadarProcessingTelemetry.cs`
- `src/Domain/Processing/RadarProcessingResult.cs`
- `src/Domain/Processing/RadarProcessingPressureBand.cs`
- `src/Domain/Processing/RadarProcessingPressureScore.cs`
- `src/Domain/Processing/RadarProcessingPressureOptions.cs`
- `src/Domain/Processing/RadarProcessingPressureSample.cs`
- `src/Domain/Processing/RadarProcessingShardPressureSample.cs`
- `src/Domain/Processing/RadarProcessingPartitionPressureSample.cs`
- `src/Domain/Processing/RadarProcessingPressureWindowOptions.cs`
- `src/Domain/Processing/RadarProcessingPressureWindow.cs`
- `src/Domain/Processing/RadarProcessingShardPressureState.cs`
- `src/Domain/Processing/RadarProcessingPartitionPressureState.cs`
- `src/Domain/Processing/RadarProcessingRebalanceOptions.cs`
- `src/Domain/Processing/RadarProcessingRebalanceBudget.cs`
- `src/Domain/Processing/RadarProcessingPartitionResidency.cs`
- `src/Domain/Processing/RadarProcessingPartitionCooldown.cs`
- `src/Domain/Processing/RadarProcessingShardCooldown.cs`
- `src/Domain/Processing/RadarProcessingRebalanceMovePolicyInput.cs`
- `src/Domain/Processing/RadarProcessingRebalancePolicyRejection.cs`
- `src/Domain/Processing/RadarProcessingRebalancePolicyResult.cs`
- `src/Domain/Processing/RadarProcessingRebalancePolicyState.cs`
- `src/Domain/Processing/RadarProcessingRebalanceDecisionKind.cs`
- `src/Domain/Processing/RadarProcessingRebalanceMoveKind.cs`
- `src/Domain/Processing/RadarProcessingRebalanceSkippedReason.cs`
- `src/Domain/Processing/RadarProcessingProjectedPressure.cs`
- `src/Domain/Processing/RadarProcessingRebalanceCandidate.cs`
- `src/Domain/Processing/RadarProcessingRebalanceDecision.cs`
- `src/Domain/Processing/RadarProcessingDirectHotReliefPlanner.cs`
- `src/Domain/Processing/RadarProcessingHotPartitionClassification.cs`
- `src/Domain/Processing/RadarProcessingHotPartitionState.cs`
- `src/Domain/Processing/RadarProcessingHotPartitionClassifier.cs`
- `src/Domain/Processing/RadarProcessingColdEvacuationPlanner.cs`
- `src/Domain/Processing/RadarProcessingPartitionMigrationState.cs`
- `src/Domain/Processing/RadarProcessingMigrationValidationError.cs`
- `src/Domain/Processing/RadarProcessingPartitionMigration.cs`
- `src/Domain/Processing/RadarProcessingMigrationValidationResult.cs`
- `src/Domain/Processing/RadarProcessingMigrationResult.cs`
- `src/Domain/Processing/RadarProcessingMigrationCoordinator.cs`
- `src/Domain/Processing/RadarProcessingPartitionStateSnapshot.cs`
- `src/Domain/Processing/RadarProcessingPartitionStateChecksum.cs`
- `src/Domain/Processing/RadarProcessingStateHandoffValidationError.cs`
- `src/Domain/Processing/RadarProcessingStateHandoffValidationResult.cs`
- `src/Domain/Processing/RadarProcessingStateHandoffValidator.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingTopologyVersioningTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingBatchRouterTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingTelemetryTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingContractTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingPressureSampleTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingPressureWindowTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalancePolicyStateTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingRebalanceDecisionTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingDirectHotReliefPlannerTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingHotPartitionClassifierTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingColdEvacuationPlannerTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingMigrationCoordinatorTests.cs`
- `tests/RadarPulse.Tests/Processing/RadarProcessingStateHandoffValidatorTests.cs`
- `src/Domain/Processing/*`
- `tests/RadarPulse.Tests/Processing/*`
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
- `src/Domain/Streaming/RadarEventBatchLifetime.cs`
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
- `src/Infrastructure/Archive/NexradArchiveRadarEventBatchPublishSession.cs`
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


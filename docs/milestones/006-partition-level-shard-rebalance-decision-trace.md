# Milestone 006: Decision Trace

## 1. What Was Implemented

Milestone 006 implemented cautious partition-level shard rebalance over the
milestone 005 static processing core:

- Versioned `PartitionId -> ShardId` ownership with immutable topology
  snapshots and stable `SourceId -> PartitionId` mapping.
- Batch routing, partitioned telemetry, processing results, pressure samples,
  decisions, migrations, and validation diagnostics that all carry topology
  version context.
- Windowed pressure detection with explicit hysteresis, minimum sample counts,
  deterministic pressure scores, and shard/partition pressure states.
- Deterministic anti-churn policy state with logical evaluation sequence,
  minimum residency, cooldowns, source/target/global move budgets, target
  headroom checks, and projected-benefit checks.
- Rebalance decision telemetry for accepted moves, no-action decisions, and
  rejected candidates with explicit skipped reasons.
- Direct hot-partition relief planning, intrinsic-hot/quarantine
  classification, and cold-partition evacuation fallback.
- Synchronous migration coordination that publishes topology version `N+1`
  only after validation succeeds.
- State handoff validation that compares partition-owned source-state
  snapshots before and after a migration.
- A rebalance-aware processing session that processes one batch against one
  topology snapshot, then evaluates and applies bounded rebalance before the
  next batch.
- Read-side rebalance validation helpers for topology, routing, pressure,
  decisions, migrations, and state handoff diagnostics.
- Deterministic synthetic rebalance workloads and benchmark modes for static
  processing, pressure sampling, and full rebalance sessions.
- CLI benchmark commands:
  `processing benchmark rebalance-synthetic` and
  `processing benchmark rebalance-archive`.

Verified Release synthetic benchmark command:

```powershell
dotnet build -c Release src\Presentation\RadarPulse.Cli.csproj --no-restore
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-synthetic --workload all --mode all --iterations 10000 --warmup-iterations 1000
```

Synthetic behavior summary:

```text
workload        rebalance result
balanced        no moves; skipped by no-hot-shard
hot-shard       direct hot relief accepted
intrinsic-hot   hot partition rejected; cold evacuation accepted
oscillating     no churn on short spikes
cooldown-storm  move accepted, then cooldown/budget gates block churn
```

All captured synthetic rows reported successful validation and zero failed
migrations. Same-run static ratios are the useful overhead signal because the
rebalance catalog is intentionally tiny: `8-20` payload values per iteration
instead of the milestone 005 synthetic baseline's `38_750_400` payload values
per iteration.

Verified single-file real-data smoke command:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --file data\nexrad\level2\2026\05\04\KTLX\KTLX20260504_000245_V06 --mode all --partitions 24 --shards 4 --iterations 3 --warmup-iterations 1 --parallelism 1
```

Single-file real-data shape:

```text
file: KTLX20260504_000245_V06
batches: 1
stream events: 32_400
payload values: 38_759_040
topology: 24 partitions / 4 shards
```

Single-file real-data result:

```text
mode       topology versions  evaluations  accepted moves  skipped decisions  callback payload values/s  end-to-end payload values/s  alloc bytes/payload value
static     1                  0            0               0                  2_589_754_314.69           92_333_354.54              0.06
sampling   1                  3            0               0                  2_990_889_752.58           92_347_294.79              0.06
rebalance  2                  3            3               0                  3_061_858_015.59           92_350_954.71              0.06
```

The earlier `92M` concern was explained by the single-thread archive replay
boundary, not by rebalance. A comparable `--parallelism 24` real-data rerun on
`KTLX20260504_002334_V06` produced:

```text
command/result                                      end-to-end payload values/s
archive benchmark stream                            430_859_940.37
processing benchmark rebalance-archive sampling     458_420_311.03
processing benchmark rebalance-archive rebalance    449_250_477.25
```

Verified cache-wide real-data command:

```powershell
dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode all --partitions 24 --shards 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --decompressor radarpulse
```

Cache-wide real-data shape:

```text
examined files: 244
skipped files: 24
published Archive Two base-data files: 220
compressed bytes: 1_330_634_309
decompressed bytes: 11_145_331_584
batches: 220
stream events: 7_114_560
payload values: 8_513_587_200
```

Cache-wide real-data result:

```text
mode       topology versions  evaluations  accepted moves  skipped decisions  callback payload values/s  end-to-end payload values/s  alloc bytes/payload value
static     1                  0            0               0                  2_796_597_485.46           355_001_379.25             0.24
sampling   1                  220          0               0                  2_735_817_941.09           385_154_964.58             0.23
rebalance  2                  220          2               436                2_680_685_752.29           380_667_655.66             0.23
```

Cache-wide rebalance accepted two direct hot relief moves:

```text
source 51_868.80->42_837.12, target 0.00->9_031.68, relief 9_031.68
source 43_966.08->35_038.08, target 0.00->8_928.00, relief 8_928.00
```

Then policy gates blocked further movement with explicit skipped reasons:
`global-move-budget-exhausted`, `source-shard-move-budget-exhausted`,
`no-cold-target-shard`, and `no-hot-shard`.

All cache-wide rows reported successful validation and zero failed migrations.
The cache-wide rebalance callback throughput was `2_680_685_752.29` payload
values/s, about `102.2%` of the milestone 005 `partitioned 24/24 none`
processing-only baseline of `2_622_669_443.85` payload values/s. End-to-end
archive numbers remain replay dominated and should not be compared directly to
milestone 005 processing-only numbers.

## 2. Decision Matrix

### Versioned Partition Ownership

Decision: preserve stable `SourceId -> PartitionId` ownership and make only
`PartitionId -> ShardId` ownership movable through monotonic topology
snapshots.

Why chosen: source-local state, ordering, dense arrays, and handler snapshots
all depend on stable source identity. Moving partitions instead of sources gives
rebalance a clear unit of ownership without reopening the milestone 004 stream
contract or the milestone 005 state model.

Alternatives: source-level migration, repartitioning source ranges, handler-led
ownership, or opaque dynamic routing.

Rejected because: source-level migration creates too many small ownership
events, repartitioning changes the source-universe contract, handler-led
ownership blurs core responsibility, and opaque routing makes diagnostics and
state handoff difficult to audit.

Trade-offs/debt: an intrinsically hot partition cannot be fixed by moving it
between shards. Partition splitting or source-range repartitioning remains a
future milestone.

Review explanation: "A source stays in the same partition; only the partition's
shard owner changes, and every change is visible as a new topology version."

### Synchronous Batch Boundary Remains

Decision: keep milestone 006 rebalance inside the synchronous
`PartitionedBarrier` boundary.

Why chosen: leased `RadarEventBatch` payloads are valid only during the
synchronous processing callback. Processing one batch against one topology
snapshot and moving ownership only between batches preserves that lifetime
rule while making migration behavior testable.

Alternatives: retained worker queues, async shard transport, applying topology
changes while a batch is still processing, or live ingestion integration.

Rejected because: those options need a retained payload protocol, durable
worker scheduling semantics, and new failure handling that would obscure the
rebalance ownership problem.

Trade-offs/debt: milestone 006 proves correctness and policy behavior, not
multi-core worker scaling. Real async worker transport remains future work.

Review explanation: "A batch never observes mixed topology. Rebalance happens
after the barrier, before the next batch."

### Logical Evaluation Scheduler

Decision: evaluate rebalance periodically by logical processing sequence,
currently once per processed partitioned batch in the rebalance session, rather
than by a wall-clock timer.

Why chosen: logical sequence makes tests deterministic and keeps policy state
aligned with completed telemetry samples. Cooldowns, budgets, minimum
residency, and quarantine evidence all advance from completed evaluations.

Alternatives: wall-clock timers, background scheduler threads, or immediate
rebalance inside routing.

Rejected because: wall-clock timers make tests and replay comparisons noisy,
background schedulers reintroduce async lifetime concerns, and router-side
movement would mix load policy with batch routing.

Trade-offs/debt: production integration may later add a real timer or worker
loop, but it should still consume completed telemetry snapshots and publish
topology only at safe boundaries.

Review explanation: "The scheduler is periodic in processing terms: sample,
evaluate, maybe publish, then let the next batch use the new topology."

### Windowed Pressure, Not One-Batch Imbalance

Decision: derive rebalance pressure from rolling samples with minimum sample
counts and hysteresis.

Why chosen: a shard should become rebalance-eligible only under sustained
pressure. Short spikes, one-batch imbalance, or random cache shape should not
trigger migration.

Alternatives: compare raw event counts from the latest batch, rebalance by
equal partition count, or use simple round-robin redistribution.

Rejected because: single-batch signals create churn, partition counts do not
represent real payload work, and round-robin movement ignores actual pressure.

Trade-offs/debt: threshold tuning remains empirical. Longer multi-radar runs
will be needed before calling the default numbers production-ready.

Review explanation: "The controller watches pressure over time; it does not
move partitions just because one sample looks uneven."

### Multiple Hot Shards Are Bounded By Policy

Decision: allow the planners to reason over multiple hot or super-hot source
shards, but apply deterministic ordering and strict movement budgets.

Why chosen: real workloads can make more than one shard hot at once. The
controller should attempt credible relief where possible while avoiding a
global reshuffle.

Alternatives: only ever inspect the single hottest shard, or rebalance every
hot shard in the same evaluation.

Rejected because: inspecting only one shard can ignore useful relief elsewhere,
while moving many partitions in one evaluation can create a migration storm and
make pressure attribution unclear.

Trade-offs/debt: the first implementation is deliberately conservative.
Further tuning can adjust source-shard and global budgets after more
cache-wide and multi-radar data.

Review explanation: "When several shards are boiling, the controller still
opens only bounded relief valves and requires each target to have headroom."

### Rebalance Is Pressure Relief, Not Equalization

Decision: rebalance only when sustained pressure, target headroom, projected
benefit, residency, cooldown, and budget gates all agree.

Why chosen: the user-facing goal was not mechanical balancing. The controller
should reduce load on super-hot shards when it can do so safely, and otherwise
explain why no move happened.

Alternatives: continuously equalize shard pressure, move the largest partition
whenever a shard is above average, or keep reassigning until pressure looks
symmetrical.

Rejected because: those approaches chase cosmetic balance, waste migration
budget, and can move hot partitions repeatedly without reducing total pressure.

Trade-offs/debt: some imbalances are intentionally left alone. That is an
accepted result when projected relief is weak or targets are unsafe.

Review explanation: "Uneven load is not enough. The move has to lower pressure
in a way that is worth the ownership change."

### Direct Hot Relief Before Cold Evacuation

Decision: try direct hot-partition relief first, then cold-partition evacuation
from the hot shard only when the hot partition cannot move safely.

Why chosen: moving the hot partition is the clearest pressure relief if the
target can absorb it. If that partition is too hot for any safe target, moving
cold work away from the hot shard can still reduce pressure without spreading
the intrinsic hot spot.

Alternatives: always move cold partitions first, always move the hottest
partition, or use cold evacuation as general load equalization.

Rejected because: cold-first may waste budget when direct relief is available,
hottest-only can poison target shards, and general cold shuffling recreates
mechanical balancing.

Trade-offs/debt: cold evacuation is a fallback only. It should remain tied to a
hot source shard and credible pressure relief.

Review explanation: "Move the hot partition if that is safe. If it is not, move
cooler work away from the hot shard to lower pressure without exporting the
problem."

### Intrinsic Hot And Quarantine Classification

Decision: classify hot partitions as movable, intrinsic-hot, or quarantined
based on projected and observed relief.

Why chosen: some partitions are too hot for any target. Repeatedly moving them
would just transfer the overload and cause ping-pong. Classification makes that
fact explicit and keeps later evaluations from retrying the same failed move
immediately.

Alternatives: retry every hot partition on every evaluation, permanently ban
failed partitions, or hide the reason inside generic policy rejection.

Rejected because: constant retries create churn, permanent bans ignore changing
load, and generic rejection hides why the controller is not acting.

Trade-offs/debt: quarantine must not be eternal. The current state supports
explicit clear/effective-outcome reset, but controller integration should add
automatic lifecycle handling by logical evaluations: TTL, sustained cooled
samples, or downgrade when pressure changes enough that retrying is safe.

Review explanation: "A failed hot move becomes evidence, not a permanent
sentence. The system should retry when the pressure story changes."

### Migration Coordinator And State Handoff Validation

Decision: publish a topology move only through a migration coordinator and
validate partition state handoff before recording the move as accepted.

Why chosen: ownership changes must preserve source-local state, source ranges,
handler snapshots, timestamps, and checksums. A topology update without state
handoff diagnostics would be too easy to accept incorrectly.

Alternatives: let the planner mutate topology directly, skip state comparison
because dense state is currently global, or validate only topology ownership.

Rejected because: planner mutation mixes policy with publication, global dense
state is an implementation detail that future worker ownership may change, and
topology-only validation cannot prove that processing state survived the move.

Trade-offs/debt: the first handoff validator audits snapshots rather than
moving physical state between worker-local stores. That is enough for the
synchronous global-state baseline and leaves a clear contract for later worker
state movement.

Review explanation: "A move is not just a new owner id. It is accepted only if
the partition's source-local state still matches after ownership changes."

### Decision Telemetry And Skipped Reasons

Decision: make skipped and rejected rebalance decisions first-class telemetry,
not invisible no-ops.

Why chosen: cautious rebalance often decides not to move. Operators and tests
need to distinguish no sustained pressure, no safe target, budget exhaustion,
cooldown, intrinsic-hot classification, and target pressure risk.

Alternatives: return only accepted moves, log skipped decisions out of band, or
collapse all no-move cases into one result.

Rejected because: accepted-only output makes the controller look idle, logs are
harder to validate deterministically, and one generic no-move reason hides the
policy behavior that milestone 006 is meant to prove.

Trade-offs/debt: decision telemetry adds allocation and output volume. Large
benchmark output now caps detailed sample printing, but production telemetry
will need deliberate retention rules.

Review explanation: "No move is still a decision. The result should say which
gate protected the system."

### Rebalance Session Wrapper

Decision: implement rebalance integration as a session around the processing
core instead of turning every core processing call into a policy operation.

Why chosen: the core should remain the processor of `RadarEventBatch` against a
captured topology. The session composes processing, pressure sampling,
planning, migration, policy recording, and validation without hiding those
steps inside routing.

Alternatives: place rebalance directly in `RadarProcessingCore.Process`, let
the router trigger movement, or keep only standalone planners with no
end-to-end session.

Rejected because: core-integrated policy would broaden every processing call,
router-triggered movement breaks separation of concerns, and standalone
planners would not prove the real batch-to-batch publication boundary.

Trade-offs/debt: callers that want rebalance must use the session. Sequential
or simple static callers can continue using the core directly.

Review explanation: "The core processes; the session decides whether the next
batch should see a newer topology."

### Validation Outside The Hot Path

Decision: expose rebalance validation as explicit read-side diagnostics on
session results.

Why chosen: milestone 006 needs strong invariant checks without turning every
hot-loop operation into a validation-heavy path.

Alternatives: validate every invariant inline during routing and processing,
or rely only on unit tests.

Rejected because: inline validation would distort benchmark contours, and tests
alone would not audit real archive runs.

Trade-offs/debt: production callers must decide when to pay validation cost.
Benchmarks and closeout runs should keep validation visible until the policy is
mature.

Review explanation: "Validation audits what happened after the fact without
becoming the steady-state processing cost."

### Benchmark Boundary

Decision: measure both synthetic processing-only behavior and real archive
callback behavior, while keeping archive replay timing separate from processing
callback timing.

Why chosen: synthetic workloads prove policy behavior deterministically, but
real archive data proves that leased batch callbacks, pressure samples,
migration, and validation work on actual NEXRAD-derived batches. The two
measurements answer different questions.

Alternatives: use only synthetic workloads, compare only end-to-end archive
numbers, or compare tiny synthetic rebalance workloads directly to the large
milestone 005 processing-only synthetic shape.

Rejected because: synthetic-only results miss real data shape, end-to-end
archive timings are replay dominated, and tiny behavioral workloads are not a
fair throughput comparison against milestone 005's large processing-only batch.

Trade-offs/debt: cache-wide allocation is higher than the milestone 005
processing-only baseline: about `0.23` bytes/payload value versus `0.03`.
Allocation profiling is a production-hardening follow-up, not a blocker for
closing milestone 006.

Review explanation: "Synthetic runs prove decisions. Real archive runs prove
the same controller survives real batch shape and leased callback lifetime."

## 3. Remaining Risks And Debt

- `PartitionedBarrier` remains synchronous. Milestone 006 validates topology,
  migration, policy, and state handoff, but not retained worker queues or
  multi-core shard execution.
- Automatic quarantine decay is still a follow-up. Quarantined partitions
  should clear or downgrade after TTL, sustained cooling, or enough pressure
  change to make retry safe.
- Intrinsically hot partitions cannot be fixed by movement alone. Future
  partition splitting or source-range repartitioning may be needed.
- Cache-wide allocation is visibly higher than milestone 005 processing-only
  allocation and should be profiled before production hardening.
- The cache-wide result is one local KTLX corpus capture. Repeated runs,
  multi-radar scenarios, and policy tuning remain necessary.
- Benchmark output and decision telemetry need retention discipline before
  long-running production use.
- End-to-end archive benchmark numbers remain replay dominated and should not
  be used as processing-core regressions without the callback timing column.

## 4. Portfolio Review Summary

Milestone 006 moved RadarPulse from static shard ownership to cautious,
versioned partition-level rebalance without changing the milestone 004
`RadarEventBatch` contract or the milestone 005 source-local state model. The
key decisions were stable source-to-partition ownership, movable
partition-to-shard topology, synchronous batch-boundary publication, windowed
pressure detection, conservative anti-churn gates, direct hot relief before
cold evacuation, explicit intrinsic-hot/quarantine state, validated migration,
and first-class skipped-reason telemetry.

The milestone succeeded as a correctness and cautious-behavior milestone.
Synthetic workloads validate the intended decisions, real single-file and
cache-wide archive runs validate the leased-batch integration, accepted moves
reduce source pressure without failed migrations, and cache-wide callback
throughput remains in line with the milestone 005 processing-only baseline.
The remaining work is production hardening: allocation profile, quarantine
lifecycle automation, repeated real-data tuning, and eventual async worker
transport.

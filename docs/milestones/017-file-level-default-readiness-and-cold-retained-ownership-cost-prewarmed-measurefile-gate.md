# Milestone 017: Prewarmed MeasureFile Gate

Status: captured with allocation blocker removed; remaining elapsed jitter
carried as a non-blocking filesystem timing note.

This document records the milestone 017 prewarmed direct `MeasureFile()` gate
captured after the natural slice 5 gate identified retained owned snapshot
allocation as the file-level blocker.

The default queued-owned rollout contour was not changed. Prewarm remained an
explicit opt-in measurement contour.

## Gate Purpose

The gate asks whether explicit retained-pool prewarm removes the file-level
`MeasureFile()` allocation blocker without changing correctness, lifecycle,
fallback, or release semantics.

The prewarmed candidate contour:

```text
provider mode: queued-owned
provider overlap: producer-consumer
retention strategy: pooled-copy
execution: async shard transport
worker count: 4
worker queue capacity: 8
provider queue capacity: 8
retained-byte budget: 536870912
overlap consumer delay: 0
retained payload factory: explicit prewarmed factory
```

Same-run explicit `BlockingBorrowed` rows remained the oracle:

```text
providerMode: BlockingBorrowed
executionMode: AsyncShardTransport
asyncExecution: workerCount 4, queueCapacity 1
```

## Preconditions

```text
natural MeasureFile gate captured: yes
prewarm opt-in mechanics implemented: yes
default rollout contour changed: no
focused regression after prewarm implementation: passed, 54 passed, 0 failed
Release solution build after prewarm implementation: succeeded, 0 warnings
temporary runner build: succeeded, 0 warnings
```

Focused regression:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingRetainedPayloadFactoryTests|FullyQualifiedName~NexradArchiveRadarEventBatchPublisherTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"
```

Release build:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore
```

Temporary direct API runner:

```powershell
dotnet build data\temp\m017-prewarmed-gate-runner\M017PrewarmedGateRunner.csproj -c Release --no-restore
dotnet run --project data\temp\m017-prewarmed-gate-runner\M017PrewarmedGateRunner.csproj -c Release --no-restore
```

The runner lived under `data\temp\m017-prewarmed-gate-runner`, wrote local
ignored reports, and was not committed as a product surface.

Primary stabilized raw local outputs:

```text
data\temp\m017-prewarmed-gate-runner\output\m017-prewarmed-measurefile-20260522-091327.jsonl
data\temp\m017-prewarmed-gate-runner\output\m017-prewarmed-measurefile-20260522-091327.md
```

The runner used one excluded prewarmed candidate plus borrowed oracle warmup
before recording rows. This separated retained-pool prewarm behavior from
process first-use/JIT noise. The earlier non-stabilized local run remained
consistent on allocation and pool misses but had stronger first-row startup
noise:

```text
data\temp\m017-prewarmed-gate-runner\output\m017-prewarmed-measurefile-20260522-091203.jsonl
data\temp\m017-prewarmed-gate-runner\output\m017-prewarmed-measurefile-20260522-091203.md
```

## Runner Configuration

```text
API: RadarProcessingArchiveRebalanceBenchmark.MeasureFile()
mode: RebalanceSession
iterations: 1
warmup iterations: 0
parallelism: 24
partitions: 24
shards: 4
candidate: explicit retained payload factory prewarmed before measured row
prewarm retained batch count: 1
prewarm sizing: selected file expected event count and payload bytes
borrowed oracle: explicit BlockingBorrowed, AsyncShardTransport, workers 4,
  queue capacity 1
```

Candidate rows measured only the `MeasureFile()` call after explicit prewarm.
Prewarm allocation was recorded separately and must not be treated as free.

## Group Summary

This table preserves the primary gate's raw threshold status. The milestone
decision interpretation uses the targeted timing rerun below for elapsed
variance.

| Row | Class | Raw gate status | Elapsed ratio | Measured allocation ratio | Prewarm allocation | Pool misses |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| prior-ktlx-20260504-representative | prewarmed-probe | fail | 3.632x | 1.024x | 69_206_240 | 0 |
| prior-ktlx-20260504-representative | prewarmed-warm | warning | 1.035x | 1.000x | 69_206_240 | 0 |
| ktlx-20260504-representative | prewarmed-probe | optimize | 1.167x | 1.004x | 69_206_240 | 0 |
| ktlx-20260504-representative | prewarmed-warm | warning | 1.058x | 1.000x | 69_206_240 | 0 |
| kinx-20260504-representative | prewarmed-probe | warning | 1.034x | 1.014x | 69_206_240 | 0 |
| kinx-20260504-representative | prewarmed-warm | warning | 1.071x | 1.006x | 69_206_240 | 0 |
| ktlx-20260505-representative | prewarmed-probe | pass | 0.978x | 1.026x | 69_206_240 | 0 |
| ktlx-20260505-representative | prewarmed-warm | warning | 1.073x | 0.980x | 69_206_240 | 0 |
| ktlx-20260504-small | prewarmed-warm | pass | 0.979x | 0.995x | 35_651_808 | 0 |
| ktlx-20260504-large | prewarmed-warm | warning | 1.021x | 0.988x | 69_206_240 | 0 |
| kinx-20260504-small | prewarmed-warm | fail | 1.253x | 1.000x | 69_206_240 | 0 |
| kinx-20260504-large | prewarmed-warm | warning | 1.043x | 1.000x | 69_206_240 | 0 |
| ktlx-20260505-small | prewarmed-warm | optimize | 1.140x | 1.000x | 69_206_240 | 0 |
| ktlx-20260505-large | prewarmed-warm | pass | 0.956x | 0.995x | 71_303_392 | 0 |

## Safety Summary

All 20 borrowed/prewarmed-candidate pairs passed safety guardrails.

```text
validation succeeded: all measurement rows
validation checksum parity: all pairs
raw checksum parity: all pairs
stable totals parity: all pairs
topology/rebalance parity: all pairs
retained payload failed releases: 0
provider overlap failed releases: 0
worker failed batches/items: 0/0
candidate current retained pressure at completion: 0
retained-byte budget exceeded: no
prewarmed candidate retained pool misses: 0 for every row
```

## Allocation Interpretation

Prewarm removed the natural slice 5 allocation blocker from the measured
`MeasureFile()` rows.

```text
natural slice 5 representative measured allocation ratios:
  cold: 1.958x to 2.995x
  warm: 1.916x to 2.186x

prewarmed gate measured allocation ratios:
  all selected rows: 0.980x to 1.026x
  retained pool misses: 0 for every prewarmed candidate row
```

The prewarm allocation remains real:

```text
KTLX 2026-05-04 small:
  35_651_808 bytes
normal 32_400-event rows:
  69_206_240 bytes
KTLX 2026-05-05 large:
  71_303_392 bytes
```

The correct interpretation is that explicit prewarm moves the retained
event-array and byte-array allocation out of the measured row. It does not
make the allocation disappear.

## Timing Interpretation

The primary prewarmed gate exposed elapsed variance, but the repeated timing
rerun did not reproduce the fail-level outliers. The remaining elapsed spread
is treated as a non-blocking filesystem timing note rather than a file-level
readiness blocker:

```text
prior representative prewarmed-probe:
  fail by elapsed, 3.632x borrowed, while measured allocation passed at 1.024x

KTLX 2026-05-04 representative prewarmed-probe:
  optimize by elapsed, 1.167x borrowed, allocation 1.004x

KINX 2026-05-04 small prewarmed-warm:
  fail by elapsed, 1.253x borrowed, allocation 1.000x

KTLX 2026-05-05 small prewarmed-warm:
  optimize by elapsed, 1.140x borrowed, allocation 1.000x
```

Warm repeated representative rows were stable enough for allocation. Their
elapsed ratios stayed near borrowed and were later rechecked by the targeted
timing rerun:

```text
prior representative warm:
  elapsed 1.035x, allocation 1.000x
KTLX 2026-05-04 representative warm:
  elapsed 1.058x, allocation 1.000x
KINX representative warm:
  elapsed 1.071x, allocation 1.006x
KTLX 2026-05-05 representative warm:
  elapsed 1.073x, allocation 0.980x
```

## Targeted Timing Rerun

After the prewarmed gate, the elapsed outlier rows were repeated with five
pairs each and one excluded prewarmed candidate plus borrowed oracle
stabilization row before capture.

Temporary local outputs:

```text
data\temp\m017-prewarmed-timing-runner\output\m017-prewarmed-timing-20260522-092557.md
data\temp\m017-prewarmed-timing-runner\output\m017-prewarmed-timing-20260522-092557.csv
```

Rerun summary:

| Scenario | Row | Class | Pairs | Interpreted status | Avg elapsed ratio | Max elapsed ratio | Avg allocation ratio | Prewarm avg alloc | Candidate spread | Pool misses |
| --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| prior-probe | prior-ktlx-20260504-representative | prewarmed-probe | 5 | filesystem timing note | 1.034x | 1.097x | 0.995x | 69_206_240 | 12.15% | 0 |
| prior-warm | prior-ktlx-20260504-representative | prewarmed-warm | 5 | filesystem timing note | 1.043x | 1.143x | 0.999x | 69_206_240 | 13.32% | 0 |
| ktlx-representative-probe | ktlx-20260504-representative | prewarmed-probe | 5 | filesystem timing note | 1.046x | 1.085x | 1.000x | 69_206_240 | 8.07% | 0 |
| ktlx-representative-warm | ktlx-20260504-representative | prewarmed-warm | 5 | filesystem timing note | 1.017x | 1.040x | 1.000x | 69_206_240 | 7.23% | 0 |
| kinx-small-warm | kinx-20260504-small | prewarmed-warm | 5 | filesystem timing note | 1.042x | 1.051x | 1.003x | 69_206_240 | 5.39% | 0 |
| ktlx-20260505-small-warm | ktlx-20260505-small | prewarmed-warm | 5 | filesystem timing note | 1.056x | 1.125x | 1.000x | 69_206_240 | 13.01% | 0 |

The local raw timing outputs preserve the runner's threshold classifications.
For milestone interpretation, those classifications are downgraded to a
filesystem timing note because the fail-level outliers did not reproduce and
allocation, safety, release, and pool-miss evidence stayed stable.

Rerun interpretation:

```text
the prior representative 3.632x elapsed fail did not reproduce
the KINX small 1.253x elapsed fail did not reproduce
the KTLX representative 1.167x optimize row did not reproduce
the KTLX 2026-05-05 small 1.140x optimize row reduced to near-borrowed
  elapsed timing
all targeted scenarios remained safety-clean
all targeted scenarios retained measured allocation around 1.0x borrowed
all targeted scenarios had retained pool misses 0
elapsed spread is non-blocking filesystem jitter/spread, not a repeated
  fail-level timing regression
```

## Gate Result

```text
allocation blocker:
  removed for measured prewarmed MeasureFile() rows

prewarm cost:
  real and must remain explicitly attributed

safety blocker:
  none

timing posture:
  no blocker after targeted repeats; remaining elapsed jitter and spread are
  carried as a non-blocking filesystem timing note

readiness implication:
  prewarm is a valid optimization direction for the allocation blocker; the
  prewarmed file-level contour is best interpreted as allocation-ready with a
  non-blocking filesystem timing note, subject to slice 7 decision trace
  policy

next interpretation input:
  decide whether prewarm becomes an explicit accepted contour, whether file
  defaults split from natural cold behavior, and whether slice 6 small-file
  MeasureCache should run natural only or natural plus prewarmed comparison
  rows
```

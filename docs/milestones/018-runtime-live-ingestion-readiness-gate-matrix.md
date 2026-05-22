# Milestone 018: Runtime Readiness Contract And Gate Matrix

Status: captured for slice 2.

This document records milestone 018 slice 2. It defines the runtime readiness
contract, candidate gate posture, workload matrix, thresholds, and coverage
limits before runtime gate implementation or measurement.

Runtime behavior changes in this slice:

```text
none
```

## Decision Boundary

Slice 2 does not migrate runtime defaults.

The runtime gate candidate is:

```text
provider posture:
  queued-owned candidate selected explicitly by the runtime-shaped gate harness
  or existing archive queued overlap surface

default posture:
  undecided; omitted runtime controls must not be described as queued-owned
  default until milestone 018 decision trace accepts that posture

fallback/reference:
  explicit BlockingBorrowed or borrowed blocking reference remains separate
  where a comparable reference path exists
```

The gate should answer whether queued-owned is:

```text
ready as runtime default
ready as runtime default with warnings
runtime-safe as explicit opt-in only
optimization-bound before runtime default
architecture-blocked
not ready
coverage-insufficient
deferred
```

## Included And Excluded Surfaces

Included for milestone 018 gates:

```text
in-process archive provider decoupling
ArchiveOwnedRadarEventBatchQueueingPublisher
RadarProcessingArchiveQueuedOverlapRunner
RadarProcessingOwnedBatchQueue
RadarProcessingQueuedProcessingSession
RadarProcessingQueuedRebalanceSession
deterministic local archive replay as runtime-shaped input stand-in
direct benchmark milestone 017 evidence as baseline context
focused queue/session/retained/worker tests as guardrails
```

Included as coverage statement only:

```text
true live ingestion contract vocabulary
```

Excluded from milestone 018 gates:

```text
durable queues
brokers
cross-process providers/workers
ordered concurrent rebalance
multiple active rebalance-enabled processing batches
builder-transfer retained payload execution
physical worker-local state transfer
source-level migration
partition splitting
product-facing radar workflows
automatic silent borrowed fallback
```

## Runtime Readiness Contract

A runtime-shaped queued-owned readiness row must expose and pass the following
contract unless the decision trace explicitly classifies a named warning or
coverage gap.

### Provider And Provenance

Required fields:

```text
runtime surface name
input shape
provider mode
provider mode source
provider overlap mode
retention strategy
execution mode
worker count
worker queue capacity
provider queue capacity
retained-byte budget
prewarm enabled state
prewarm source
fallback/reference contour yes/no
default candidate contour yes/no
```

Pass requirement:

```text
queued-owned candidate rows must be visibly candidate or explicit
borrowed/reference rows must be visibly borrowed/reference
no row may silently convert queued-owned failure into borrowed success
```

### Lifecycle

Required phases to represent in gate evidence:

```text
configure
start
optional prewarm
intake
enqueue/backpressure
processing
drain
cancellation
fault
stop/dispose
```

Pass requirement:

```text
terminal state must be explicit and retained pressure must return to zero
after success, cancellation, fault, drain, and dispose paths
```

### Processing Completeness

Required fields:

```text
processing completeness
processing validation failed batches
worker failed batches
worker failed work items
validation status
processing result validity
```

Pass requirement:

```text
processing completeness: succeeded
processing validation failed batches: 0
worker failed batches/items: 0/0
validation: succeeded
```

Any processing-invalid batch is a blocker unless the row is a deliberately
failing negative test.

### Cleanup, Release, And Pressure

Required fields:

```text
current pending retained batches/bytes
current active retained batches/bytes
current combined retained batches/bytes
pending retained high-water
active retained high-water
combined retained high-water
retained payload release attempts
retained payload failed releases
provider overlap failed releases
retained payload pool rent/return/miss counts
retained event-array pool rent/return/miss counts
retained byte-array pool rent/return/miss counts
```

Pass requirement:

```text
current pending/active/combined retained counts and bytes: 0 at terminal state
retained payload failed releases: 0
provider overlap failed releases: 0
combined retained payload high-water <= configured retained-byte budget
```

### Queue And Backpressure

Required fields:

```text
enqueue attempts
enqueued batches
enqueue full count
enqueue timed out count
enqueue canceled count
enqueue closed count
enqueue faulted count
total enqueue wait time
total dequeue wait time
queue depth high-water
queued payload bytes high-water
completed/failed/canceled/skipped processing batch counts
total drain time
```

Pass requirement:

```text
happy-path rows:
  no full, timed-out, canceled, closed, or faulted enqueue results

backpressure rows:
  full, wait, timeout, cancellation, closed, or faulted outcomes must match
  the scenario policy and remain visible

post-fault rows:
  later intake must be rejected or shaped by explicit documented policy
```

### Ordering And Topology

Required fields:

```text
queued batch sequence
processing result sequence
final topology version
accepted move count
failed migration count
validation checksum/reference marker where available
```

Pass requirement:

```text
batches process in provider sequence order
each processed batch captures one topology snapshot
accepted topology changes publish only after successful processing
failed processing does not publish later success claims
```

## Runtime Thresholds And Bands

These thresholds are recorded before gate capture.

### Non-Negotiable Fail Thresholds

Any accepted readiness row fails if:

```text
provider/fallback provenance is ambiguous
queued-owned failure is silently replaced by borrowed success
processing completeness fails
processing validation failed batches > 0
worker failed batches/items > 0/0
validation fails
failed migrations > 0
retained payload failed releases > 0
provider overlap failed releases > 0
current retained pressure is non-zero at terminal state
combined retained high-water exceeds configured retained-byte budget
provider enqueue success is reported as processing success without drain proof
topology publication order is ambiguous or invalid
required observability fields are missing for the row interpretation
```

### Steady Runtime Cost Bands

Steady runtime rows are interpreted after startup/prewarm/first-use costs are
separated.

```text
steady allocation ratio versus reference:
  pass: <= 1.10x
  warning: > 1.10x and <= 1.20x
  optimize: > 1.20x and <= 1.35x
  fail: > 1.35x

steady elapsed ratio versus reference:
  pass: <= 1.00x
  warning: > 1.00x and <= 1.10x
  optimize: > 1.10x and <= 1.20x
  fail: > 1.20x

session candidate spread:
  pass: <= 7.50% of candidate average where repeated rows are captured
  warning: > 7.50% and <= 12.50%
  optimize: > 12.50% and <= 20.00%
  fail: > 20.00%
```

Interpretation notes:

```text
direct benchmark milestone 017 thresholds remain baseline context
filesystem timing variance can be carried only as a named warning if safety
  and allocation guardrails are clean
startup/prewarm/first-use costs must not be averaged into steady-state pass
```

### Startup, Prewarm, And First-Use Bands

Startup and prewarm costs are lifecycle costs, not hidden row allocation.

```text
prewarm attribution:
  pass only if enabled state, source, sizing, elapsed time, allocated bytes,
  and retained bytes are visible

prewarm retained-byte budget:
  pass only if prewarm retained bytes fit inside configured retained-byte
  budget or are explicitly outside retained queue pressure with named
  rationale

prewarm cleanup:
  pass only if stop/dispose returns retained pressure to zero

prewarm failure:
  fail unless startup/intake is explicitly blocked and the failure is visible

natural first-use allocation:
  use milestone 017 file/small-cache bands as historical warning context;
  natural first-use rows that reproduce allocation-blocked behavior cannot
  support runtime default readiness without a named accepted lifecycle cost
```

### Backpressure Bands

Backpressure rows are not judged by elapsed ratio first. They are judged by
policy conformance.

```text
queue full with ReturnFull:
  pass if enqueue returns Full visibly and retained resources are released

queue full with Wait and timeout:
  pass if enqueue returns TimedOut visibly and retained resources are released

queue full with Wait and cancellation:
  pass if enqueue returns Canceled visibly and retained resources are released

retained-byte budget exhausted:
  pass if enqueue waits, times out, cancels, or returns Full according to the
  selected mode and no retained pressure leak remains

post-fault enqueue:
  pass if rejected as Faulted or Closed with message and no hidden success

drain:
  pass if accepted work completes or is canceled according to explicit policy
  and retained pressure returns to zero
```

## Gate Matrix

The gate matrix is intentionally split into focused groups. A temporary runner
may emit one JSONL stream and one Markdown summary per group.

### Group A: Contract And Provenance

Purpose:

```text
prove the runtime-shaped harness can capture provider/fallback/default
provenance, queue telemetry, retained pressure, worker telemetry, processing
completeness, release health, and lifecycle terminal status
```

Rows:

```text
A1 direct benchmark baseline row from milestone 017 result contract
A2 queued-owned runtime archive provider candidate row
A3 explicit borrowed/reference row where comparable
A4 explicit queued-owned row proving candidate contour fields
```

Required outcome:

```text
all fields needed by the readiness contract are available or slice 3 gets a
reporting/contract task
```

### Group B: Steady Intake

Purpose:

```text
prove queued-owned runtime-shaped intake can process deterministic archive
input across representative corpus shapes
```

Rows:

```text
B1 KTLX 2026-05-04 bounded steady session
B2 KTLX 2026-05-05 named-risk bounded steady session
B3 KINX 2026-05-04 cross-radar steady session
B4 mixed local KINX/KTLX unfiltered steady session
B5 optional larger local mixed-cache session if runtime cost is acceptable
```

Minimum repeated rows:

```text
primary KTLX 2026-05-04: 3 candidate/reference pairs if practical
KTLX 2026-05-05 named risk: 2 candidate/reference pairs
KINX 2026-05-04: 1 candidate/reference pair
mixed local: 1 candidate/reference pair
```

Required outcome:

```text
processing completeness succeeds, worker failures are 0/0, release failures
are 0, retained pressure returns to zero, and steady cost bands are
classifiable per shape
```

### Group C: First-Use And Prewarm-Sensitive Rows

Purpose:

```text
separate natural first-use retained cost from any runtime prewarm candidate
```

Rows:

```text
C1 natural small-file runtime first-use KTLX 2026-05-04 2/4/8 published files
C2 natural small-file runtime first-use KINX 2026-05-04 2/4/8 published files
C3 natural small-file runtime first-use KTLX 2026-05-05 2/4/8 published files
C4 prewarmed or prewarm-candidate equivalents if slice 4 selects a prewarm
   candidate posture
```

Required outcome:

```text
natural first-use cost is classified visibly
prewarm, if used, is attributed outside steady measured allocation
no natural allocation-blocked row is hidden by aggregate success
```

### Group D: Queue Pressure And Retained-Byte Pressure

Purpose:

```text
prove bounded provider queue and retained-byte pressure behavior is explicit
and leak-free
```

Rows:

```text
D1 provider queue capacity 1, ReturnFull mode, second enqueue returns Full
D2 provider queue capacity 1, Wait mode with enqueue timeout, second enqueue
   returns TimedOut
D3 provider queue capacity 1, Wait mode with cancellation while waiting,
   second enqueue returns Canceled
D4 retained-byte budget below second batch payload, ReturnFull or equivalent
   rejection
D5 retained-byte budget exhausted with Wait plus timeout/cancellation
```

Required outcome:

```text
each backpressure result is visible
retained resources from rejected enqueue attempts are released
pending/active/combined retained pressure returns to zero
```

### Group E: Cancellation

Purpose:

```text
prove cancellation has explicit result states and cleanup
```

Rows:

```text
E1 cancellation before publish/enqueue
E2 cancellation while waiting for enqueue capacity
E3 cancellation after accepted enqueue before dequeue
E4 cancellation while active processing
E5 cancellation during drain
E6 cancellation during stop/dispose if a deterministic surface exists
```

Required outcome:

```text
terminal status is Canceled or a named explicit cancellation result
retained resources are released
queue state is closed or disposed as expected
no later success claim occurs
```

### Group F: Fault And Failure

Purpose:

```text
prove failure is fail-closed and operator-visible
```

Rows:

```text
F1 producer failure faults queue and prevents hidden success
F2 consumer processing validation failure faults session
F3 worker failure or worker exception path if deterministic injector exists
F4 rebalance validation or migration failure if deterministic injector exists
F5 retained release failure if deterministic injector exists
F6 post-fault enqueue is rejected visibly
```

Required outcome:

```text
terminal status is ProducerFailed or ConsumerFaulted/Faulted as appropriate
processing completeness fails visibly for failure rows
topology does not publish success from failed processing
pending retained resources are released
later intake is rejected or explicitly shaped
```

Coverage note:

```text
if worker/release/migration failure injection is not available without broad
test harness work, the decision trace must name that as a coverage gap or
slice 5 must add a small deterministic test helper
```

### Group G: Drain, Stop, And Dispose

Purpose:

```text
prove terminal cleanup behavior
```

Rows:

```text
G1 normal producer complete plus consumer drain
G2 queue close with accepted pending work drains accepted work
G3 dispose with pending queued work releases pending retained resources
G4 fault with pending queued work releases pending retained resources
G5 CancelQueued shutdown mode behavior if implemented or explicitly tested as
   missing
```

Required outcome:

```text
terminal retained pressure is zero
release failures are 0
session status matches expected terminal state
CancelQueued is either implemented/tested or explicitly carried as a blocker/
coverage gap
```

## Workload Selection

Local archive inputs allowed for runtime-shaped gates:

```text
data\nexrad\level2\2026\05\04\KTLX
data\nexrad\level2\2026\05\04\KINX
data\nexrad\level2\2026\05\05\KTLX
```

Selection rules:

```text
primary archive data rows should use non-MDM Archive Two base-data files
metadata and MDM files may be examined/skipped by cache-style selectors but
should not be primary MeasureFile-like proof
mixed-radar rows must size source universe to selected radar ids
every gate summary must report examined/skipped/published counts where
archive cache traversal is used
```

Runtime stand-in caveat:

```text
local archive replay gives deterministic runtime-shaped input, but it does
not prove network live feed behavior, external IO, broker durability, or
cross-process recovery
```

## Required Gate Output

Each gate group should produce:

```text
raw JSONL output
Markdown summary
command or runner description
input corpus and selector
provider/reference/candidate provenance
effective runtime contour
lifecycle phase status
queue telemetry
retained pressure telemetry
retained payload telemetry
prewarm telemetry if applicable
worker telemetry
processing completeness
validation/checksum/reference parity where applicable
release health
terminal cleanup state
per-row status
group interpretation
```

Suggested output locations:

```text
data\temp\m018-runtime-gate-runner\output\...
data\temp\m018-runtime-pressure-gate-runner\output\...
data\temp\m018-runtime-failure-gate-runner\output\...
```

## Slice 2 Decision

Slice 2 defines the contract and gate matrix but does not decide runtime
default migration.

The milestone proceeds to slice 3 with this posture:

```text
queued-owned is a runtime candidate selected explicitly for gates
runtime default posture remains undecided
runtime prewarm posture remains undecided until slice 4
CancelQueued shutdown behavior is a named gap to resolve or carry
true live ingestion remains a coverage gap unless new scope is added
runtime-shaped gates must prove lifecycle, pressure, cancellation, failure,
cleanup, processing completeness, and observability before decision trace
```

Proceed to slice 3:

```text
reporting, contract, and harness gap closure
```

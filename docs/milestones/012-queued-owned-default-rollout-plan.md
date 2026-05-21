# Milestone 012: Queued-Owned Default Rollout Implementation Plan

Status: draft.

This plan implements the milestone 012 architecture defined in
`012-queued-owned-default-rollout.md`.

The plan is intentionally scoped to an explicit provider-default rollout
decision for the already-measured milestone 011 contour:
`queued-owned + pooled-copy + producer-consumer`. It should not implement
`builder-transfer`, durable broker integration, live ingestion, cross-process
workers, concurrent rebalance-enabled processing, source-level migration,
partition splitting, or complex radar algorithms.

## Goal

Milestone 012 decides whether and how the optimized queued-owned contour
becomes the default provider behavior for the scoped archive rebalance
benchmark/runtime surface.

The target default contour is:

```text
provider mode: queued-owned
retention strategy: pooled-copy
provider overlap: producer-consumer
execution: async
queue capacity: 8
retained-byte budget: 536870912
queue telemetry: summary
overlap telemetry: summary
overlap consumer delay: disabled
```

The most important rules are:

```text
rollout thresholds are documented before final gate interpretation
blocking-borrowed remains explicitly selectable
same-run blocking-borrowed remains available as the benchmark oracle
queued-owned failure must not silently fall back to borrowed success
controlled consumer delay remains mechanics-only proof
the default contour should not drift while rollout evidence is captured
```

The first implementation target is deliberately conservative:

```text
scope default changes to processing rebalance archive benchmark options
make default-derived provider settings explicit in parsed options and output
preserve explicit --provider blocking-borrowed behavior
preserve explicit opt-in diagnostic contours for non-default queued-owned
  combinations
use existing retained-resource pressure and readiness evaluator contracts
capture Release evidence before closeout
```

## Starting Point

Milestone 011 is complete and provides:

```text
ProcessingBenchmarkArchiveRebalanceOptions
  DefaultCandidateProviderQueueCapacity = 8
  DefaultCandidateRetainedPayloadBytes = 536870912
  MatchesDefaultCandidateContour()
  ProviderOverlapEvidenceContour
  ProviderOverlapEvidenceScope

RadarProcessingQueuedProviderReadinessEvaluator
  EvaluateCorrectnessParity()
  EvaluateRetainedResourceReleaseHealth()
  EvaluateRetainedResourcePressure()
  EvaluateNaturalEvidence()
  EvaluatePerformanceDelta()
  EvaluateAllocationMovement()
  EvaluateRunVariance()

RadarProcessingArchiveRebalanceBenchmark
  MeasureFile()
  MeasureCache()
  queued-owned provider execution
  producer-consumer overlap execution
  retained payload telemetry
  provider queue and overlap telemetry

CLI output
  provider mode
  default-candidate contour
  provider overlap evidence contour
  provider overlap evidence scope
  retained-resource pressure telemetry
  release telemetry
```

Current defaults before milestone 012 implementation:

```text
provider mode: blocking-borrowed
provider overlap: none
retention strategy: snapshot-copy
queue capacity: 1
retained-byte budget: none
execution: partitioned barrier unless --execution async is supplied
queue telemetry: summary
overlap telemetry: summary, but rejected unless provider overlap is enabled
```

Milestone 011 gate facts to carry forward:

```text
expanded mixed-cache correctness parity: passed
expanded mixed-cache release failures: 0
expanded mixed-cache combined retained payload high-water: 54_413_280 bytes
retained-byte budget: 536_870_912 bytes
expanded mixed-cache borrowed elapsed ms: 77_530.68
expanded mixed-cache candidate elapsed ms: 71_181.17
post-optimization candidate allocation ratio: 1.066x borrowed
natural queue depth high-water: 1
controlled proof remains mechanics-only evidence
```

## Rollout Scope

The implementation should explicitly scope the first default change to:

```text
processing benchmark rebalance-archive CLI option parsing
RadarProcessingArchiveRebalanceBenchmark file/cache benchmark invocation paths
CLI benchmark output for file and cache results
tests that exercise archive rebalance benchmark defaults
documentation and handoff
```

The implementation should not change:

```text
synthetic processing benchmark defaults unless a slice explicitly proves they
  use the same provider surface
archive download, inspection, replay-only, or parse benchmark defaults
domain enum numeric values
retained payload factory strategy semantics
queued session ordering and rebalance commit rules
```

## Rollout Thresholds

Thresholds must be recorded before the final performance gate is interpreted.
The implementation can refine exact names and constants, but the plan should
start with these rollout thresholds:

```text
correctness parity:
  required, same-run borrowed reference must match

topology/rebalance parity:
  required, accepted moves, skipped decisions, failed migrations, and topology
  versions must match the reference

release failures:
  must equal 0

retained cleanup:
  current pending, active, and combined retained batch/byte counts must return
  to 0 at completion

retained pressure:
  combined retained payload high-water must be <= configured retained-byte
  budget

allocation:
  candidate/default allocated bytes must be <= 1.10x same-run borrowed
  allocated bytes

performance:
  candidate/default elapsed time must be <= 1.00x same-run borrowed elapsed
  time on the primary repeated matrix, unless the decision trace explicitly
  defers the rollout

variance:
  candidate repeated-run spread should be <= 7.50% of candidate average on the
  primary matrix

natural evidence:
  overlap consumer delay must be 0 and evidence scope must be natural rollout
  evidence, not controlled mechanics proof
```

Rationale for initial thresholds:

```text
allocation threshold 1.10x gives narrow headroom over the milestone 011
  measured 1.066x residual overhead while still rejecting a regression toward
  the pre-optimization allocation profile

performance threshold 1.00x keeps the first default rollout conservative:
  the new default should not be slower than the same-run borrowed oracle on the
  primary repeated matrix

variance threshold 7.50% allows some noise above the milestone 011 candidate
  5.01% primary spread while still preventing a noisy result from being used
  as rollout proof
```

Guardrail:

```text
Do not change thresholds after seeing final gate measurements. If thresholds
need to change, record the reason in the decision trace before interpreting the
gate.
```

## Target Implementation Shape

Domain should own any stable rollout-gate vocabulary that is not already
covered by readiness contracts. Infrastructure should continue to own benchmark
execution. Presentation should own option default expansion and operator
output.

Candidate layering:

```text
src/Domain/Processing
  readiness/rollout threshold contracts if existing readiness result shapes are
  not sufficient
  no provider default policy if the default is presentation/runtime-specific

src/Infrastructure/Processing
  benchmark default helper or constants only if infrastructure methods need a
  reusable default contour
  no silent fallback behavior

src/Presentation
  CLI option parsing default expansion
  source-of-value flags for default-derived provider settings if needed
  output labels for default rollout contour and fallback selection

tests/RadarPulse.Tests
  CLI option and output defaults
  benchmark invocation defaults
  readiness/rollout threshold interpretation
  fallback and failure policy guardrails
```

The implementation should keep these responsibilities separate:

```text
default expansion:
  how omitted provider-related CLI options resolve

explicit fallback:
  how --provider blocking-borrowed preserves old behavior

validation oracle:
  how borrowed reference runs remain available for gate comparison

rollout gate:
  how Release evidence is interpreted

operator output:
  how defaults and explicit options are made reproducible
```

## Implementation Slices

### 1. Baseline Default Surface Audit

Freeze the current default behavior before changing it.

Candidate focus:

```text
src/Presentation/Program.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
docs/milestones/012-queued-owned-default-rollout-plan.md
```

Required behavior:

```text
document every current provider-related default used by
  ProcessingBenchmarkArchiveRebalanceOptions.Parse()
document record-constructor defaults in ProcessingBenchmarkArchiveRebalanceOptions
document MeasureFile/MeasureCache provider defaults in
  RadarProcessingArchiveRebalanceBenchmark
document help text that still describes provider selection as optional
identify tests that currently expect blocking-borrowed output by default
identify explicit queued-owned tests that must continue to work
```

Expected tests or checks:

```text
existing CLI rebalance benchmark tests pass before behavior changes
existing queued provider readiness gate tests pass before behavior changes
no runtime behavior changes in this slice
```

Guardrail:

```text
Do not change defaults in this slice. Establish the audit trail and edit points
first.
```

### 2. Rollout Threshold Contracts

Capture rollout thresholds in stable code or documentation before final gate
measurement.

Candidate focus:

```text
src/Domain/Processing/RadarProcessingQueuedProviderReadinessEvaluator.cs
src/Domain/Processing/RadarProcessingQueuedProviderReadinessResult.cs
src/Domain/Processing/RadarProcessingQueuedProviderReadinessGate.cs
src/Domain/Processing/RadarProcessingQueuedProviderReadinessError.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProviderReadinessGateTests.cs
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
```

Required behavior:

```text
define the milestone 012 threshold values before final gate interpretation
reuse existing readiness evaluator methods where possible
add rollout-specific helper/contracts only if existing readiness methods cannot
  express completion pressure, allocation ratio, performance ratio, or variance
ensure cleanup completion checks include current pending, active, and combined
  retained counts/bytes returning to zero
ensure controlled evidence remains rejected for natural rollout evidence
```

Expected tests or checks:

```text
allocation ratio <= 1.10 passes and > 1.10 fails
performance ratio <= 1.00 passes and > 1.00 fails
candidate spread <= 7.50% passes and > 7.50% fails
release failures fail
missing active/combined retained pressure remains inconclusive where required
non-zero current retained pressure at completion fails rollout if represented
controlled consumer delay fails natural rollout evidence
```

Guardrail:

```text
Threshold tests should use synthetic values. Do not tune thresholds from a
fresh benchmark row in this slice.
```

### 3. Default Contour Constants And Option Provenance

Introduce the rollout contour as the default source of truth for presentation
option expansion.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
centralize rollout default constants next to existing milestone 011 candidate
  constants or in a small helper local to the CLI options type
track whether provider mode, retention strategy, provider overlap, queue
  capacity, retained-byte budget, queue telemetry, overlap telemetry, execution,
  and worker count were supplied explicitly or default-expanded
keep explicit --provider blocking-borrowed available
keep explicit --provider queued-owned variations available as opt-in
  diagnostics when they do not match the rollout contour
do not make source provenance leak into domain or infrastructure result types
  unless output cannot otherwise report it
```

Expected tests or checks:

```text
parsing with omitted provider options expands to the queued-owned rollout
  contour for the scoped command
parsing with --provider blocking-borrowed preserves borrowed provider,
  overlap none, no retained-byte budget, and fallback evidence labels
parsing with explicit queued-owned non-contour values remains opt-in diagnostic
  or is rejected according to existing validation rules
invalid builder-transfer remains rejected
controlled consumer delay remains rejected outside queued-owned
  producer-consumer contours
```

Guardrail:

```text
Do not change the candidate contour while making it default. The rollout
default should match milestone 011 unless a decision trace entry changes it
before gate capture.
```

### 4. CLI Default Expansion And Validation Rules

Change `processing benchmark rebalance-archive` omitted provider options to
the rollout contour within the scoped surface.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
omitting --provider uses queued-owned
omitting --retention-strategy under default queued-owned uses pooled-copy
omitting --provider-overlap under default queued-owned uses producer-consumer
omitting --execution under default queued-owned uses async
omitting --workers under default async uses the rollout worker count if the
  scoped contour requires workers 4; otherwise document why shard-count workers
  remain the default
omitting --queue-capacity under default queued-owned uses 8
omitting --queue-retained-bytes under default queued-owned uses 536870912
queue and overlap telemetry remain enabled at summary level for default rollout
overlap consumer delay remains zero unless explicitly supplied for controlled
  proof
explicit blocking-borrowed does not inherit queued-owned-only options
```

Expected tests or checks:

```text
default CLI smoke output prints Provider mode: queued-owned
default CLI smoke output prints Retention strategy: pooled-copy
default CLI smoke output prints Provider overlap mode: producer-consumer
default CLI smoke output prints Execution mode: async
default CLI smoke output prints provider queue capacity 8
default CLI smoke output prints retained-byte budget 536870912
explicit --provider blocking-borrowed output prints blocking-borrowed and does
  not print queued-owned readiness telemetry as default evidence
explicit --provider blocking-borrowed with queued-only options is rejected
existing explicit queued-owned controlled-proof tests still pass with updated
  labels
```

Guardrail:

```text
Do not silently reinterpret invalid mixed options as fallback. Reject impossible
or ambiguous option combinations.
```

### 5. Operator Output For Default Versus Explicit Selection

Make the new default auditable in CLI output.

Candidate focus:

```text
src/Presentation/Program.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
print whether provider mode came from default expansion or explicit option
print whether retention strategy, provider overlap, queue capacity, retained
  byte budget, execution, and worker count came from default expansion where
  useful
rename or supplement "Default-candidate contour" if needed so the output
  distinguishes rollout-default evidence from opt-in readiness evidence
keep existing evidence contour/scope strings stable unless tests and docs are
  intentionally updated
ensure fallback borrowed runs are visibly fallback/explicit, not natural
  queued-owned rollout evidence
```

Candidate output fields:

```text
Provider mode source: default|explicit
Retention strategy source: default|explicit|not-applicable
Provider overlap source: default|explicit|not-applicable
Provider queue capacity source: default|explicit|not-applicable
Provider queue retained byte capacity source: default|explicit|not-applicable
Provider default rollout contour: yes|no
Provider fallback contour: yes|no
```

Expected tests or checks:

```text
default run prints provider source default and rollout contour yes
explicit queued-owned rollout flags print provider source explicit and rollout
  contour yes
explicit borrowed run prints provider source explicit and fallback contour yes
natural opt-in diagnostic rows remain distinguishable from rollout evidence
controlled proof rows remain controlled-mechanics-proof
```

Guardrail:

```text
Do not remove milestone 011 evidence labels unless the replacement remains
clear for existing gate interpretation.
```

### 6. Benchmark Invocation Defaults And Same-Run Oracle Preservation

Ensure benchmark invocation paths receive the intended defaults and can still
run borrowed reference comparisons.

Candidate focus:

```text
src/Presentation/Program.cs
src/Infrastructure/Processing/RadarProcessingArchiveRebalanceBenchmark.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
tests/RadarPulse.Tests/Archive/NexradArchiveRadarEventBatchPublisherTests.cs
```

Required behavior:

```text
CLI passes expanded default rollout options to MeasureFile/MeasureCache
explicit --provider blocking-borrowed passes borrowed options to
  MeasureFile/MeasureCache
same-run borrowed reference commands remain documented for performance gates
infrastructure method optional parameters can remain blocking-borrowed if direct
  API callers rely on old defaults, as long as CLI rollout scope is explicit
if infrastructure method defaults are changed too, direct API tests must prove
  explicit blocking-borrowed remains available
```

Expected tests or checks:

```text
CLI default cache benchmark reaches queued-owned code path in smoke coverage
CLI explicit borrowed benchmark reaches blocking-borrowed code path
direct MeasureFile/MeasureCache tests are updated only if the method default
  scope changes
provider overlap is not allowed with borrowed provider
retention strategy is not allowed with borrowed provider unless explicitly
  documented otherwise
```

Guardrail:

```text
Do not remove the borrowed benchmark path. It remains the oracle even if it is
no longer the scoped CLI default.
```

### 7. Failure, Cleanup, And Fallback Guardrails Under Default Queued-Owned

Prove that default queued-owned failure paths fail closed.

Candidate focus:

```text
tests/RadarPulse.Tests/Archive/ArchiveOwnedRadarEventBatchQueueingPublisherTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingArchiveQueuedOverlapRunnerTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedProcessingSessionTests.cs
tests/RadarPulse.Tests/Processing/RadarProcessingQueuedRebalanceSessionTests.cs
tests/RadarPulse.Tests/Presentation/RadarPulseCliRebalanceBenchmarkTests.cs
```

Required behavior:

```text
retention failure does not silently rerun borrowed
producer failure releases accepted pending retained resources
processing failure releases active retained resources
validation failure is reported as candidate/default failure
release failure increments failed-release telemetry and fails readiness
cancellation releases accepted retained resources and leaves current pressure
  at zero
explicit borrowed fallback is selected only by explicit provider option
```

Expected tests or checks:

```text
existing milestone 011 failure/cancellation coverage still passes under the
  new default-expansion code
CLI or options tests prove fallback is explicit
readiness/rollout evaluator rejects release failures and non-zero cleanup
  pressure if represented
```

Guardrail:

```text
Do not add automatic fallback-to-borrowed behavior in milestone 012.
```

### 8. Focused Regression Pass Before Gate

Run focused verification after default behavior changes and before performance
measurement.

Candidate commands:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~ArchiveOwnedRadarEventBatchQueueingPublisherTests|FullyQualifiedName~RadarProcessingQueuedProcessingSessionTests|FullyQualifiedName~RadarProcessingQueuedRebalanceSessionTests"

dotnet build RadarPulse.sln -c Release --no-restore
```

Expected behavior:

```text
focused CLI tests pass with updated default expectations
focused readiness tests pass with rollout thresholds
focused failure/cleanup tests pass
Release build succeeds
```

Guardrail:

```text
Do not capture the rollout performance gate until focused defaults, readiness,
fallback, and cleanup coverage pass.
```

### 9. Natural Rollout Performance Gate

Capture the Release gate that decides the default rollout.

Candidate document:

```text
docs/milestones/012-queued-owned-default-rollout-performance-gate.md
```

Candidate commands:

```powershell
dotnet build RadarPulse.sln -c Release --no-restore

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --date 2026-05-04 --radar KTLX --max-files 220 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --provider blocking-borrowed --execution async --workers 4 --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4

dotnet src\Presentation\bin\Release\net10.0\RadarPulse.Cli.dll processing benchmark rebalance-archive --cache data\nexrad --max-files 1000000 --mode rebalance --iterations 1 --warmup-iterations 0 --parallelism 24 --partitions 24 --shards 4
```

Required capture:

```text
same-run borrowed reference rows
default queued-owned rows using omitted provider flags
expanded effective configuration proving default expansion
primary repeated KTLX matrix with enough rows to interpret spread
expanded mixed-cache row or matrix
correctness parity fields
topology/rebalance parity fields
release telemetry
pending, active, and combined retained pressure
allocation ratio against borrowed
elapsed ratio against borrowed
queue depth and HasQueuedAheadOverlap
evidence contour and scope
```

Expected interpretation:

```text
validation parity must pass
release failures must be 0
current retained pressure must return to 0
combined retained pressure must stay under 536_870_912 bytes
allocation ratio must be <= 1.10x borrowed
primary matrix elapsed ratio must be <= 1.00x borrowed
primary candidate spread must be <= 7.50% of candidate average
controlled proof rows, if captured, remain in a separate mechanics section
```

Guardrail:

```text
Do not close the milestone from a single favorable row. The gate must show
whether omitted provider flags truly use the rollout default.
```

### 10. Rollout Decision Trace

Record the default decision after the gate.

Candidate document:

```text
docs/milestones/012-queued-owned-default-rollout-decision-trace.md
```

Required decisions:

```text
whether queued-owned becomes the scoped default
which command/runtime surfaces are included in the default rollout
whether blocking-borrowed remains explicit fallback
whether same-run borrowed comparison remains required for future benchmark
  gates
whether residual allocation overhead is acceptable under the threshold
whether timing and variance support the default switch
whether retained pressure and release health support the default switch
```

Expected decision matrix:

```text
default provider posture
fallback posture
borrowed oracle posture
rollout threshold acceptance
controlled proof separation
residual allocation risk
out-of-scope runtime expansions
```

Guardrail:

```text
If the gate misses a threshold, do not quietly keep the implementation and call
the milestone successful. Either revert the default change before closeout or
record an explicit defer outcome where blocking-borrowed remains default.
```

### 11. Closeout And Handoff

Finalize the milestone documentation and project handoff.

Candidate documents:

```text
docs/milestones/012-queued-owned-default-rollout-closeout.md
docs/handoff.md
```

Required closeout content:

```text
final status
implemented behavior
not implemented behavior
final verification commands and results
performance gate summary
final default posture
fallback posture
residual risks
carry-forward items
```

Handoff must state one of:

```text
queued-owned + pooled-copy + producer-consumer is now the scoped default,
with blocking-borrowed available as explicit fallback

or

blocking-borrowed remains the default because the rollout gate missed a named
threshold
```

Expected verification before closeout:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter "FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests|FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests|FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests"

dotnet build RadarPulse.sln -c Release --no-restore

dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
```

Guardrail:

```text
Do not mark the milestone complete until handoff names the current provider
default unambiguously.
```

## Verification Strategy

Use focused tests after each implementation slice and broader verification
before the performance gate.

Expected focused coverage:

```text
CLI option parsing defaults
CLI output source/provenance labels
explicit blocking-borrowed fallback
explicit queued-owned diagnostic contours
controlled consumer-delay rejection and labeling
rollout threshold interpretation
retained-resource release health
retained pressure cleanup at completion
archive queued overlap default behavior
benchmark result propagation
```

Expected broad checks:

```powershell
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarPulseCliRebalanceBenchmarkTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingQueuedProviderReadinessGateTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore --filter FullyQualifiedName~RadarProcessingArchiveQueuedOverlapRunnerTests
dotnet test tests\RadarPulse.Tests\RadarPulse.Tests.csproj --no-restore
dotnet build RadarPulse.sln -c Release --no-restore
```

Expected performance gate:

```text
Release build before measurements
same-run blocking-borrowed async reference
default queued-owned rollout contour selected through omitted provider flags
active/combined retained pressure visible in output
repeated natural rows and variance interpretation
allocation ratio checked against 1.10x threshold
performance ratio checked against 1.00x threshold
controlled proof rows separated if captured
```

## Completion Checklist

```text
[ ] baseline default surface audit is captured
[ ] rollout thresholds are recorded before final gate interpretation
[ ] default contour constants/provenance are implemented or documented
[ ] CLI default expansion resolves to queued-owned rollout contour
[ ] explicit blocking-borrowed fallback remains selectable
[ ] same-run borrowed benchmark oracle remains available
[ ] operator output distinguishes default expansion, fallback, rollout
    contour, opt-in diagnostic, and controlled proof
[ ] invalid mixed provider options fail closed
[ ] failure, cancellation, and cleanup guardrails remain covered
[ ] focused regression pass succeeds before gate capture
[ ] natural rollout performance gate is captured
[ ] performance gate interprets correctness, cleanup, pressure, allocation,
    timing, and variance thresholds
[ ] decision trace records whether the provider default changed
[ ] closeout is written
[ ] handoff is updated with the current default provider posture
```

## Non-Goals

Milestone 012 does not implement:

```text
builder-transfer retained payload execution
durable queue or broker integration
live ingestion
cross-process provider or worker transport
multiple active rebalance-enabled processing batches
ordered concurrent rebalance commit barrier
source-level migration or partition splitting
physical worker-local state transfer
complex radar algorithms
visualization or product-facing radar analysis features
automatic silent fallback from queued-owned failure to borrowed success
```

## Closeout Question

The milestone closes by answering:

```text
Does queued-owned + pooled-copy + producer-consumer become the scoped default
provider behavior now?
```

The acceptable answers are:

```text
yes:
  queued-owned becomes the scoped default, blocking-borrowed remains explicit
  fallback, and the rollout gate passes under recorded thresholds

not yet:
  blocking-borrowed remains the default, queued-owned remains opt-in, and the
  decision trace names the missed threshold or missing evidence
```

The milestone should not close with an ambiguous "ready later" conclusion. It
should either change the default deliberately or preserve the old default with
a concrete blocker.
